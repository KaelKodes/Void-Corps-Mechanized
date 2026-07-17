using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum CampaignNodeKind
{
	Start,
	Mission,
	Warning
}

/// <summary>
/// A node on the sector board. Mission/Warning nodes are locations (claim arenas)
/// with up to three manufacturer mission offers.
/// </summary>
public sealed class CampaignNode
{
	public string Id { get; init; } = "";
	public int Column { get; init; }
	public int Row { get; init; }
	public CampaignNodeKind Kind { get; init; }
	/// <summary>Claim site code for arena layout. Empty on Start.</summary>
	public string LocationClaimCode { get; set; } = "";
	public string LocationDisplayName { get; set; } = "";
	public List<LocationMissionOffer> Offers { get; set; } = new();
	/// <summary>Offer completed when this location was cleared; -1 if not cleared via offer.</summary>
	public int CompletedOfferIndex { get; set; } = -1;
	public bool Cleared { get; set; }

	public LocationMissionOffer? GetOffer(int index)
	{
		if (index < 0 || index >= Offers.Count)
			return null;
		return Offers[index];
	}
}

public sealed class SectorGraph
{
	public string SectorId { get; init; } = "";
	public string SectorTitle { get; init; } = "";
	public List<CampaignNode> Nodes { get; } = new();
	public List<(string From, string To)> Edges { get; } = new();

	/// <summary>Legacy save key alias.</summary>
	public string SectorClaimCode => SectorId;

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
			var offers = new Godot.Collections.Array();
			foreach (var o in n.Offers)
				offers.Add(o.ToDict());

			nodes.Add(new Godot.Collections.Dictionary
			{
				["id"] = n.Id,
				["col"] = n.Column,
				["row"] = n.Row,
				["kind"] = (int)n.Kind,
				["location"] = n.LocationClaimCode,
				["location_name"] = n.LocationDisplayName,
				["offers"] = offers,
				["completed_offer"] = n.CompletedOfferIndex,
				["cleared"] = n.Cleared
			});
		}

		var edges = new Godot.Collections.Array();
		foreach (var (from, to) in Edges)
			edges.Add(new Godot.Collections.Array { from, to });

		return new Godot.Collections.Dictionary
		{
			["sector_id"] = SectorId,
			["claim"] = SectorId,
			["title"] = SectorTitle,
			["nodes"] = nodes,
			["edges"] = edges
		};
	}

	public static SectorGraph FromDict(Godot.Collections.Dictionary dict)
	{
		var graph = new SectorGraph
		{
			SectorId = dict.ContainsKey("sector_id")
				? dict["sector_id"].AsString()
				: dict.ContainsKey("claim") ? dict["claim"].AsString() : "",
			SectorTitle = dict.ContainsKey("title") ? dict["title"].AsString() : ""
		};

		if (dict.ContainsKey("nodes"))
		{
			foreach (var v in dict["nodes"].AsGodotArray())
			{
				var d = v.AsGodotDictionary();
				var node = new CampaignNode
				{
					Id = d["id"].AsString(),
					Column = d["col"].AsInt32(),
					Row = d["row"].AsInt32(),
					Kind = (CampaignNodeKind)d["kind"].AsInt32(),
					LocationClaimCode = d.ContainsKey("location") ? d["location"].AsString() : "",
					LocationDisplayName = d.ContainsKey("location_name") ? d["location_name"].AsString() : "",
					CompletedOfferIndex = d.ContainsKey("completed_offer") ? d["completed_offer"].AsInt32() : -1,
					Cleared = d.ContainsKey("cleared") && d["cleared"].AsBool()
				};

				if (d.ContainsKey("offers"))
				{
					foreach (var ov in d["offers"].AsGodotArray())
						node.Offers.Add(LocationMissionOffer.FromDict(ov.AsGodotDictionary()));
				}
				else if (d.ContainsKey("mission"))
				{
					// Legacy save: single mission → one anonymous offer.
					node.Offers.Add(new LocationMissionOffer
					{
						MissionType = (MissionType)d["mission"].AsInt32(),
						Difficulty = d.ContainsKey("diff")
							? (PilotDifficulty)d["diff"].AsInt32()
							: PilotDifficulty.Easy,
						Seed = d.ContainsKey("seed") ? d["seed"].AsInt32() : 0,
						BossEncounterId = d.ContainsKey("boss")
							? (BossEncounterId)d["boss"].AsInt32()
							: BossEncounterId.None,
						ManufacturerId = "trinova",
						RivalManufacturerId = "brimforge",
						RepGain = 3,
						RepLoss = 2
					});
				}

				graph.Nodes.Add(node);
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
