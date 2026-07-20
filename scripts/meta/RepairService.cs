using Godot;

namespace Mechanize;

/// <summary>
/// Full-repair economy. Repairs are always complete — no partial slides, no wear stacking.
/// Destroyed reconstruction can exceed buy-new for cheap parts (labor + premium).
/// </summary>
public static class RepairService
{
	/// <summary>Fraction of catalog price charged per missing integrity when not destroyed.</summary>
	public const float NormalRepairFactor = 0.55f;

	/// <summary>Extra fraction of catalog price when reconstructing a destroyed part.</summary>
	public const float DestroyedPremiumFactor = 0.85f;

	/// <summary>Flat labor scrap added on destroyed reconstruction.</summary>
	public const int DestroyedFlatLabor = 25;

	public static int ReplacementPrice(PartData part) => ShopService.PriceFor(part);

	public static int RepairCost(PartData part, PartCondition condition)
	{
		if (part == null || !condition.NeedsRepair)
			return 0;

		var replace = ReplacementPrice(part);
		var proportional = Mathf.Max(1, Mathf.CeilToInt(replace * condition.MissingRatio * NormalRepairFactor));
		if (!condition.Destroyed)
			return proportional;

		var premium = Mathf.CeilToInt(replace * DestroyedPremiumFactor) + DestroyedFlatLabor;
		return proportional + premium;
	}

	public static bool TryRepair(
		PlayerProfile profile,
		PartSlot slot,
		PartCondition conditionSnapshot,
		out int spent)
	{
		spent = 0;
		var instance = profile.GetEquippedInstance(slot);
		if (instance == null)
			return false;

		var part = GameCatalog.GetPart(instance.PartId);
		if (part == null)
			return false;

		var cost = RepairCost(part, conditionSnapshot);
		if (cost <= 0)
		{
			instance.Condition.SetFull();
			conditionSnapshot.SetFull();
			return true;
		}

		if (profile.Scrap < cost)
			return false;

		profile.Scrap -= cost;
		spent = cost;
		instance.Condition.SetFull();
		conditionSnapshot.SetFull();
		return true;
	}

	public static int RepairAllAffordable(
		PlayerProfile profile,
		System.Collections.Generic.IReadOnlyDictionary<PartSlot, PartCondition> snapshots)
	{
		var total = 0;
		foreach (var (slot, condition) in snapshots)
		{
			if (!condition.NeedsRepair)
				continue;
			if (TryRepair(profile, slot, condition, out var spent))
				total += spent;
		}

		return total;
	}
}
