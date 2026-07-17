using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum CampaignNodeKind
{
	Start,
	Mission,
	Warning
}

public sealed class CampaignNode
{
	public string Id { get; init; } = "";
	public int Column { get; init; }
	public int Row { get; init; }
	public CampaignNodeKind Kind { get; init; }
	public MissionType MissionType { get; set; } = MissionType.DestroyAllEnemies;
	public PilotDifficulty Difficulty { get; set; } = PilotDifficulty.Easy;
	public int Seed { get; set; }
	public BossEncounterId BossEncounterId { get; set; } = BossEncounterId.None;
	public bool Cleared { get; set; }
}

public sealed class SectorGraph
{
	public string SectorClaimCode { get; init; } = "";
	public string SectorTitle { get; init; } = "";
	public List<CampaignNode> Nodes { get; } = new();
	public List<(string From, string To)> Edges { get; } = new();

	public CampaignNode? Get(string id)
	{
		foreach (var n in Nodes)
		{
			if (n.Id == id)
				return n;
		}

		return null;
	}

	public CampaignNode StartNode => Get("n_0_0") ?? Nodes[0];

	public IEnumerable<CampaignNode> NeighborsForward(string fromId)
	{
		foreach (var (from, to) in Edges)
		{
			if (from == fromId)
			{
				var n = Get(to);
				if (n != null)
					yield return n;
			}
		}
	}

	public bool IsAdjacent(string fromId, string toId)
	{
		foreach (var (from, to) in Edges)
		{
			if (from == fromId && to == toId)
				return true;
		}

		return false;
	}

	public Godot.Collections.Dictionary ToDict()
	{
		var nodes = new Godot.Collections.Array();
		foreach (var n in Nodes)
		{
			nodes.Add(new Godot.Collections.Dictionary
			{
				["id"] = n.Id,
				["col"] = n.Column,
				["row"] = n.Row,
				["kind"] = (int)n.Kind,
				["mission"] = (int)n.MissionType,
				["diff"] = (int)n.Difficulty,
				["seed"] = n.Seed,
				["boss"] = (int)n.BossEncounterId,
				["cleared"] = n.Cleared
			});
		}

		var edges = new Godot.Collections.Array();
		foreach (var (from, to) in Edges)
			edges.Add(new Godot.Collections.Array { from, to });

		return new Godot.Collections.Dictionary
		{
			["claim"] = SectorClaimCode,
			["title"] = SectorTitle,
			["nodes"] = nodes,
			["edges"] = edges
		};
	}

	public static SectorGraph FromDict(Godot.Collections.Dictionary dict)
	{
		var graph = new SectorGraph
		{
			SectorClaimCode = dict.ContainsKey("claim") ? dict["claim"].AsString() : "",
			SectorTitle = dict.ContainsKey("title") ? dict["title"].AsString() : ""
		};

		if (dict.ContainsKey("nodes"))
		{
			foreach (var v in dict["nodes"].AsGodotArray())
			{
				var d = v.AsGodotDictionary();
				graph.Nodes.Add(new CampaignNode
				{
					Id = d["id"].AsString(),
					Column = d["col"].AsInt32(),
					Row = d["row"].AsInt32(),
					Kind = (CampaignNodeKind)d["kind"].AsInt32(),
					MissionType = (MissionType)d["mission"].AsInt32(),
					Difficulty = (PilotDifficulty)d["diff"].AsInt32(),
					Seed = d["seed"].AsInt32(),
					BossEncounterId = (BossEncounterId)d["boss"].AsInt32(),
					Cleared = d.ContainsKey("cleared") && d["cleared"].AsBool()
				});
			}
		}

		if (dict.ContainsKey("edges"))
		{
			foreach (var v in dict["edges"].AsGodotArray())
			{
				var arr = v.AsGodotArray();
				if (arr.Count >= 2)
					graph.Edges.Add((arr[0].AsString(), arr[1].AsString()));
			}
		}

		return graph;
	}
}
