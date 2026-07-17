using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Seeded one-way sector DAG: start → branching missions → Warning boss.</summary>
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

	/// <summary>Column layout: start, three mission columns, warning.</summary>
	private static readonly int[] ColumnHeights = [1, 3, 2, 3, 2, 1];

	public static SectorGraph Generate(string claimCode, string sectorTitle, int seed, BossEncounterId warningBoss)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)seed;

		var graph = new SectorGraph
		{
			SectorClaimCode = claimCode,
			SectorTitle = sectorTitle
		};

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
					Kind = kind,
					Seed = (int)rng.Randi()
				};

				if (kind == CampaignNodeKind.Start)
				{
					node.MissionType = MissionType.DestroyAllEnemies;
					node.Difficulty = PilotDifficulty.Easy;
				}
				else if (kind == CampaignNodeKind.Warning)
				{
					node.MissionType = MissionType.BossEncounter;
					node.Difficulty = PilotDifficulty.Hard;
					node.BossEncounterId = warningBoss;
				}
				else
				{
					node.MissionType = MissionPool[(int)(rng.Randi() % (uint)MissionPool.Length)];
					node.Difficulty = PickDifficulty(col, rng);
				}

				list.Add(node);
				graph.Nodes.Add(node);
			}

			columns.Add(list);
		}

		// Forward edges: each node links to 1–2 nearest rows in the next column.
		for (var col = 0; col < columns.Count - 1; col++)
		{
			var cur = columns[col];
			var next = columns[col + 1];
			foreach (var node in cur)
			{
				var targets = PickTargets(node, next, rng);
				foreach (var t in targets)
					graph.Edges.Add((node.Id, t.Id));
			}

			// Ensure every next-column node is reachable.
			foreach (var n in next)
			{
				var reachable = false;
				foreach (var (from, to) in graph.Edges)
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
}
