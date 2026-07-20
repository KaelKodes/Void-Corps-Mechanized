using Godot;

namespace Mechanize;

public static class LocationLootService
{
	private static readonly RandomNumberGenerator Rng = new();

	static LocationLootService() => Rng.Randomize();

	/// <summary>
	/// Roll the selected location's public drop table. Rare odds improve slightly
	/// with repeat clears, while remaining rare enough to support farming.
	/// </summary>
	public static void Roll(
		SolarCampaignRun run,
		SolarLocationData location,
		MatchSession match)
	{
		foreach (var materialId in location.MaterialDrops)
		{
			var amount = Rng.RandiRange(2 + location.ThreatTier, 5 + location.ThreatTier * 2);
			match.AddMaterialDrop(materialId, amount);
		}

		if (location.CommonPartDrops.Count > 0 && Rng.Randf() < 0.32f)
		{
			var partId = location.CommonPartDrops[Rng.RandiRange(0, location.CommonPartDrops.Count - 1)];
			match.AddRewardPart(partId);
		}

		if (location.RarePartDrops.Count == 0)
			return;

		var clears = run.CompletionCount(location.Id);
		var rareChance = Mathf.Clamp(0.025f + clears * 0.004f, 0.025f, 0.12f);
		if (Rng.Randf() < rareChance)
		{
			var rareId = location.RarePartDrops[Rng.RandiRange(0, location.RarePartDrops.Count - 1)];
			match.AddRewardPart(rareId);
		}
	}
}
