using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Seeded one-way sector DAG: start → location branches → Warning boss.
/// Locations are claim arenas; each non-boss location gets up to 3 manufacturer offers.
/// </summary>
public static class SectorGraphGenerator
{
	private static readonly MissionType[] MissionPool =
	[
		MissionType.DestroyAllEnemies,
		MissionType.SearchAndDestroy,
		MissionType.CaptureArea,
		MissionType.CaptureMultipleAreas,
		MissionType.DataRetrieval,
		MissionType.SwarmDefend,
		MissionType.Escort
	];

	/// <summary>Start, two location columns, warning. Five claim sites fill the location slots.</summary>
	private static readonly int[] ColumnHeights = [1, 2, 2, 1];

	public static SectorGraph Generate(string sectorId, string sectorTitle, int seed)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)seed;
		GameCatalog.EnsureBuilt();

		var claims = VoidCorpsIdentity.ClaimSites.ToList();
		Shuffle(claims, rng);

		var graph = new SectorGraph
		{
			SectorId = sectorId,
			SectorTitle = sectorTitle
		};

		var claimIndex = 0;
		var columns = new List<List<CampaignNode>>();
		for (var col = 0; col < ColumnHeights.Length; col++)
		{
			var list = new List<CampaignNode>();
			var height = ColumnHeights[col];
			for (var row = 0; row < height; row++)
			{
				var kind = col == 0
					? CampaignNodeKind.Start
					: col == ColumnHeights.Length - 1
						? CampaignNodeKind.Warning
						: CampaignNodeKind.Mission;

				var node = new CampaignNode
				{
					Id = $"n_{col}_{row}",
					Column = col,
					Row = row,
					Kind = kind
				};

				if (kind == CampaignNodeKind.Start)
				{
					node.LocationDisplayName = "Staging";
				}
				else
				{
					var claim = claims[claimIndex % claims.Count];
					claimIndex++;
					node.LocationClaimCode = claim.Code;
					node.LocationDisplayName = claim.DisplayName;

					if (kind == CampaignNodeKind.Warning)
					{
						var bossId = BossEncounterCatalog.ForSectorClaim(claim.Code);
						node.Offers.Add(BuildBossOffer(claim, bossId, rng));
					}
					else
					{
						node.Offers.AddRange(BuildLocationOffers(col, rng));
					}
				}

				list.Add(node);
				graph.Nodes.Add(node);
			}

			columns.Add(list);
		}

		for (var col = 0; col < columns.Count - 1; col++)
		{
			var cur = columns[col];
			var next = columns[col + 1];
			foreach (var node in cur)
			{
				foreach (var t in PickTargets(node, next, rng))
					graph.Edges.Add((node.Id, t.Id));
			}

			foreach (var n in next)
			{
				var reachable = false;
				foreach (var (_, to) in graph.Edges)
				{
					if (to == n.Id)
					{
						reachable = true;
						break;
					}
				}

				if (reachable)
					continue;

				var donor = cur[Mathf.Clamp(n.Row, 0, cur.Count - 1)];
				graph.Edges.Add((donor.Id, n.Id));
			}
		}

		return graph;
	}

	private static List<LocationMissionOffer> BuildLocationOffers(int column, RandomNumberGenerator rng)
	{
		var manufacturers = GameCatalog.Manufacturers.Keys.ToList();
		Shuffle(manufacturers, rng);
		var offerCount = Mathf.Min(3, manufacturers.Count);
		var usedRivals = new HashSet<string>();
		var offers = new List<LocationMissionOffer>();

		for (var i = 0; i < offerCount; i++)
		{
			var mfg = manufacturers[i];
			var rivalPool = manufacturers.Where(id => id != mfg && !usedRivals.Contains(id)).ToList();
			if (rivalPool.Count == 0)
				rivalPool = manufacturers.Where(id => id != mfg).ToList();
			var rival = rivalPool[(int)(rng.Randi() % (uint)rivalPool.Count)];
			usedRivals.Add(rival);

			offers.Add(new LocationMissionOffer
			{
				ManufacturerId = mfg,
				RivalManufacturerId = rival,
				MissionType = MissionPool[(int)(rng.Randi() % (uint)MissionPool.Length)],
				Difficulty = PickDifficulty(column, rng),
				RepGain = 3,
				RepLoss = 2,
				Seed = (int)rng.Randi()
			});
		}

		return offers;
	}

	private static LocationMissionOffer BuildBossOffer(
		VoidCorpsIdentity.ClaimSite claim,
		BossEncounterId bossId,
		RandomNumberGenerator rng)
	{
		var manufacturers = GameCatalog.Manufacturers.Keys.ToList();
		var mfg = manufacturers[(int)(rng.Randi() % (uint)manufacturers.Count)];
		var rivalPool = manufacturers.Where(id => id != mfg).ToList();
		var rival = rivalPool[(int)(rng.Randi() % (uint)rivalPool.Count)];

		return new LocationMissionOffer
		{
			ManufacturerId = mfg,
			RivalManufacturerId = rival,
			MissionType = MissionType.BossEncounter,
			Difficulty = PilotDifficulty.Hard,
			RepGain = 5,
			RepLoss = 3,
			Seed = (int)rng.Randi(),
			BossEncounterId = bossId
		};
	}

	private static PilotDifficulty PickDifficulty(int column, RandomNumberGenerator rng)
	{
		var roll = rng.Randf();
		if (column <= 1)
			return roll < 0.7f ? PilotDifficulty.Easy : PilotDifficulty.Medium;
		if (column == 2)
			return roll < 0.45f ? PilotDifficulty.Easy : roll < 0.85f ? PilotDifficulty.Medium : PilotDifficulty.Hard;
		return roll < 0.35f ? PilotDifficulty.Medium : PilotDifficulty.Hard;
	}

	private static List<CampaignNode> PickTargets(CampaignNode from, List<CampaignNode> next, RandomNumberGenerator rng)
	{
		var sorted = new List<CampaignNode>(next);
		sorted.Sort((a, b) =>
		{
			var da = Mathf.Abs(a.Row - from.Row);
			var db = Mathf.Abs(b.Row - from.Row);
			return da.CompareTo(db);
		});

		var count = 1 + (rng.Randf() < 0.55f && sorted.Count > 1 ? 1 : 0);
		var result = new List<CampaignNode>();
		for (var i = 0; i < count && i < sorted.Count; i++)
			result.Add(sorted[i]);
		return result;
	}

	private static void Shuffle<T>(List<T> list, RandomNumberGenerator rng)
	{
		for (var i = list.Count - 1; i > 0; i--)
		{
			var j = (int)(rng.Randi() % (uint)(i + 1));
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
