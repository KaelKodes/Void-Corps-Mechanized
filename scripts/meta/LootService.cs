using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

public enum LootSource
{
	Cover,
	Support,
	EnemyMech
}

public static class LootService
{
	private static readonly RandomNumberGenerator Rng = new();

	static LootService()
	{
		Rng.Randomize();
	}

	public static int ScrapForCover() => Rng.RandiRange(2, 5);

	public static int ScrapForSupport(SupportUnitKind kind) => kind switch
	{
		SupportUnitKind.GunTower => Rng.RandiRange(12, 18),
		SupportUnitKind.LightTank => Rng.RandiRange(10, 15),
		_ => Rng.RandiRange(8, 12)
	};

	public static int ScrapForEnemyMech() => Rng.RandiRange(40, 60);

	public static string? RollSupportPartDrop(int maxTier = 1)
	{
		if (Rng.Randf() > 0.04f)
			return null;
		return RollRandomPart(maxTier, lowTierSlotsOnly: true);
	}

	public static string? RollEnemyMechPartDrop(LoadoutData? loadout, int maxTier = CatalogTiers.MaxTier)
	{
		if (Rng.Randf() > 0.10f)
			return null;

		if (loadout != null && Rng.Randf() < 0.65f)
		{
			var ids = new List<string>();
			foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
			{
				var id = loadout.GetPartId(slot);
				if (string.IsNullOrEmpty(id))
					continue;
				var part = GameCatalog.GetPart(id);
				if (part == null || part.VisualKind == "empty")
					continue;
				if (part.Tier > maxTier)
					continue;
				ids.Add(id);
			}
			if (ids.Count > 0)
				return ids[Rng.RandiRange(0, ids.Count - 1)];
		}

		return RollRandomPart(maxTier, lowTierSlotsOnly: false);
	}

	public static string? RollRandomPart(int maxTier, bool lowTierSlotsOnly)
	{
		GameCatalog.EnsureBuilt();
		var pool = GameCatalog.Parts.Values
			.Where(p => p.VisualKind != "empty")
			.Where(p => p.Tier <= maxTier)
			.Where(p => !lowTierSlotsOnly || p.Slot is PartSlot.Systems or PartSlot.Backpack or PartSlot.Head or PartSlot.ShoulderL or PartSlot.ShoulderR)
			.Select(p => p.Id)
			.ToList();
		if (pool.Count == 0)
			return null;
		return pool[Rng.RandiRange(0, pool.Count - 1)];
	}

	/// <summary>Legacy helper — prefers field-tier mount drops.</summary>
	public static string? RollRandomPart(bool lowTier) =>
		RollRandomPart(lowTier ? 1 : CatalogTiers.MaxTier, lowTierSlotsOnly: lowTier);

	/// <summary>Field crafting salvage. Heavier kills yield better mats.</summary>
	public static List<(string Id, int Amount)> RollMaterials(LootSource source, int maxTier = CatalogTiers.MaxTier)
	{
		var drops = new List<(string Id, int Amount)>();
		var tierCap = Mathf.Clamp(maxTier, 1, 3);

		switch (source)
		{
			case LootSource.Cover:
				if (Rng.Randf() < 0.28f)
					drops.Add((MaterialCatalog.Alloy, Rng.RandiRange(1, 2)));
				break;

			case LootSource.Support:
				TryAddMaterial(drops, MaterialCatalog.Alloy, 0.55f, 1, 3);
				TryAddMaterial(drops, MaterialCatalog.Servo, 0.35f, 1, 2);
				TryAddMaterial(drops, MaterialCatalog.Circuit, 0.3f, 1, 2);
				if (tierCap >= 2)
					TryAddMaterial(drops, MaterialCatalog.Optics, 0.12f, 1, 1);
				break;

			case LootSource.EnemyMech:
				TryAddMaterial(drops, MaterialCatalog.Alloy, 0.9f, 2, 5);
				TryAddMaterial(drops, MaterialCatalog.Servo, 0.7f, 1, 4);
				TryAddMaterial(drops, MaterialCatalog.Circuit, 0.65f, 1, 3);
				if (tierCap >= 2)
				{
					TryAddMaterial(drops, MaterialCatalog.Optics, 0.4f, 1, 2);
					TryAddMaterial(drops, MaterialCatalog.Reactor, 0.35f, 1, 2);
				}
				if (tierCap >= 3)
					TryAddMaterial(drops, MaterialCatalog.Exotic, 0.18f, 1, 1);
				break;
		}

		return drops;
	}

	private static void TryAddMaterial(
		List<(string Id, int Amount)> drops,
		string materialId,
		float chance,
		int minAmount,
		int maxAmount)
	{
		if (Rng.Randf() > chance)
			return;
		drops.Add((materialId, Rng.RandiRange(minAmount, maxAmount)));
	}

	/// <summary>Spawn ground loot the player must drive over. No auto-bank.</summary>
	public static void SpawnWorldDrops(
		Node parent,
		Vector3 origin,
		int scrap,
		string? partId = null,
		IReadOnlyList<(string Id, int Amount)>? materials = null)
	{
		if (parent == null)
			return;

		var session = parent.GetNodeOrNull<GameSession>("/root/GameSession");
		if (session?.Match.Active != true)
			return;

		var basePos = origin;
		basePos.Y = 0f;

		if (scrap > 0)
		{
			// Split large piles so the field reads as salvage, not one nugget.
			var piles = scrap >= 20 ? 3 : scrap >= 10 ? 2 : 1;
			var remaining = scrap;
			for (var i = 0; i < piles; i++)
			{
				var amount = i == piles - 1
					? remaining
					: Mathf.Max(1, remaining / (piles - i));
				remaining -= amount;
				var offset = new Vector3(
					Rng.RandfRange(-1.8f, 1.8f),
					0f,
					Rng.RandfRange(-1.8f, 1.8f));
				var drop = LootPickup.CreateScrap(basePos + offset, amount);
				parent.AddChild(drop);
			}
		}

		if (materials != null)
		{
			var index = 0;
			foreach (var (materialId, amount) in materials)
			{
				if (amount <= 0 || !MaterialCatalog.All.ContainsKey(materialId))
					continue;
				var angle = index * 1.1f;
				var offset = new Vector3(Mathf.Cos(angle) * 1.6f, 0f, Mathf.Sin(angle) * 1.6f);
				offset += new Vector3(Rng.RandfRange(-0.4f, 0.4f), 0f, Rng.RandfRange(-0.4f, 0.4f));
				parent.AddChild(LootPickup.CreateMaterial(basePos + offset, materialId, amount));
				index++;
			}
		}

		if (!string.IsNullOrEmpty(partId))
		{
			var offset = new Vector3(Rng.RandfRange(-1.2f, 1.2f), 0f, Rng.RandfRange(-1.2f, 1.2f));
			var drop = LootPickup.CreatePart(basePos + offset + new Vector3(0f, 0f, 1.2f), partId);
			parent.AddChild(drop);
		}
	}
}
