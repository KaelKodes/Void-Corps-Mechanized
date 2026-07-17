using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

public sealed class ShopOffer
{
	public string PartId { get; init; } = "";
	public int Price { get; init; }
}

public static class ShopService
{
	private static readonly RandomNumberGenerator Rng = new();

	static ShopService()
	{
		Rng.Randomize();
	}

	public static List<ShopOffer> GenerateStock(
		PlayerProfile profile,
		int count = 5,
		string? manufacturerId = null)
	{
		GameCatalog.EnsureBuilt();
		var all = GameCatalog.Parts.Values
			.Where(p => p.VisualKind != "empty")
			.ToList();

		List<PartData> candidates;
		if (!string.IsNullOrEmpty(manufacturerId))
		{
			candidates = all.Where(p => p.ManufacturerId == manufacturerId).ToList();
			if (candidates.Count == 0)
				candidates = all;
		}
		else
		{
			candidates = all;
		}

		// Bias toward parts the player has few/no copies of.
		var unseen = candidates.Where(p => profile.OwnedCount(p.Id) == 0).ToList();
		var pool = unseen.Count >= count / 2 ? unseen.Concat(candidates).ToList() : candidates;

		var stock = new List<ShopOffer>();
		var used = new HashSet<string>();
		var attempts = 0;
		while (stock.Count < count && attempts++ < 80 && pool.Count > 0)
		{
			var part = pool[Rng.RandiRange(0, pool.Count - 1)];
			if (!used.Add(part.Id))
				continue;
			stock.Add(new ShopOffer
			{
				PartId = part.Id,
				Price = PriceFor(part)
			});
		}

		// If manufacturer pool was thin, fill remaining slots from other brands.
		if (stock.Count < count && !string.IsNullOrEmpty(manufacturerId))
		{
			var filler = all.Where(p => !used.Contains(p.Id)).ToList();
			attempts = 0;
			while (stock.Count < count && attempts++ < 40 && filler.Count > 0)
			{
				var part = filler[Rng.RandiRange(0, filler.Count - 1)];
				if (!used.Add(part.Id))
					continue;
				stock.Add(new ShopOffer
				{
					PartId = part.Id,
					Price = PriceFor(part)
				});
			}
		}

		return stock;
	}

	public static int PriceFor(PartData part)
	{
		var basePrice = 25;
		basePrice += Mathf.RoundToInt(part.Armor * 0.4f);
		basePrice += Mathf.RoundToInt(part.Damage * 2f);
		basePrice += Mathf.RoundToInt(part.PowerCapacity * 0.15f);
		basePrice += part.PowerCoreClass * 20;
		basePrice += Mathf.RoundToInt(part.VisionRange * 0.3f);
		if (part.GrantsActiveAbility)
			basePrice += 35;
		return Mathf.Clamp(basePrice, 20, 200);
	}

	public static int SellValue(PartData part) => Mathf.Max(8, PriceFor(part) / 2);

	public static bool TryBuy(PlayerProfile profile, ShopOffer offer)
	{
		if (profile.Scrap < offer.Price)
			return false;
		profile.Scrap -= offer.Price;
		profile.Own(offer.PartId);
		return true;
	}

	public static bool TrySell(PlayerProfile profile, string partId)
	{
		if (PlayerProfile.IsUnlimited(partId))
			return false;
		if (profile.SpareCount(partId) <= 0)
			return false;
		var part = GameCatalog.GetPart(partId);
		if (part == null || part.VisualKind == "empty")
			return false;

		if (!profile.TryRemoveOwned(partId))
			return false;
		profile.Scrap += SellValue(part);
		return true;
	}
}
