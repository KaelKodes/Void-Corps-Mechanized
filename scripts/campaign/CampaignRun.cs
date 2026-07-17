using Godot;

namespace Mechanize;

/// <summary>Active campaign run across a sector of claim-locations.</summary>
public sealed class CampaignRun
{
	public const string SavePath = "user://mechanize_campaign.json";

	public int Seed { get; set; }
	public int SectorIndex { get; set; }
	public string CurrentNodeId { get; set; } = "";
	public bool Alive { get; set; } = true;
	public CampaignPhase Phase { get; set; } = CampaignPhase.ActiveOperations;
	public int ClaimsSecured { get; set; }
	public int ManufacturerPayoutEarned { get; set; }
	public SectorGraph Graph { get; set; } = null!;

	public CampaignNode? CurrentNode => Graph?.Get(CurrentNodeId);

	public static CampaignRun StartNew(int sectorIndex = 0, int seed = -1)
	{
		if (seed < 0)
			seed = (int)Time.GetTicksMsec();

		var graph = SectorGraphGenerator.Generate(
			"sector_claim_belt",
			"Claim Belt — contested locations",
			seed ^ (sectorIndex * 7919));

		var run = new CampaignRun
		{
			Seed = seed,
			SectorIndex = sectorIndex,
			Phase = CampaignPhase.ActiveOperations,
			Graph = graph,
			CurrentNodeId = graph.StartNode.Id,
			Alive = true
		};
		run.Save();
		return run;
	}

	public bool TryAdvanceTo(string nodeId)
	{
		if (!Alive || Graph == null)
			return false;
		if (!Graph.IsAdjacent(CurrentNodeId, nodeId))
			return false;
		var node = Graph.Get(nodeId);
		if (node == null || node.Kind == CampaignNodeKind.Start)
			return false;
		CurrentNodeId = nodeId;
		Save();
		return true;
	}

	public void MarkCurrentCleared(int completedOfferIndex = -1)
	{
		var node = CurrentNode;
		if (node != null)
		{
			node.Cleared = true;
			node.CompletedOfferIndex = completedOfferIndex;
			if (node.Kind == CampaignNodeKind.Warning)
				ClaimsSecured++;
		}

		Save();
	}

	public void AddManufacturerPayout(int scrap)
	{
		if (scrap > 0)
			ManufacturerPayoutEarned += scrap;
	}

	public void EndRun()
	{
		Alive = false;
		Save();
	}

	/// <summary>Full map visibility — all locations are known for route planning.</summary>
	public bool IsRevealed(CampaignNode node) => true;

	public void Save()
	{
		var dict = new Godot.Collections.Dictionary
		{
			["seed"] = Seed,
			["sector"] = SectorIndex,
			["node"] = CurrentNodeId,
			["alive"] = Alive,
			["phase"] = (int)Phase,
			["claims_secured"] = ClaimsSecured,
			["manufacturer_payout"] = ManufacturerPayoutEarned,
			["graph"] = Graph.ToDict()
		};
		var json = Json.Stringify(dict, "\t");
		using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(json);
	}

	public static CampaignRun? Load()
	{
		if (!Godot.FileAccess.FileExists(SavePath))
			return null;
		using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return null;
		var parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
			return null;
		var dict = parsed.AsGodotDictionary();
		if (!dict.ContainsKey("graph"))
			return null;

		var graph = SectorGraph.FromDict(dict["graph"].AsGodotDictionary());
		// Old saves lack location offers — force a fresh sector board.
		var needsMigrate = false;
		foreach (var n in graph.Nodes)
		{
			if (n.Kind is CampaignNodeKind.Mission or CampaignNodeKind.Warning
			    && (n.Offers.Count == 0 || string.IsNullOrEmpty(n.LocationClaimCode)))
			{
				needsMigrate = true;
				break;
			}
		}

		if (needsMigrate)
			return StartNew(dict.ContainsKey("sector") ? dict["sector"].AsInt32() : 0,
				dict.ContainsKey("seed") ? dict["seed"].AsInt32() : -1);

		return new CampaignRun
		{
			Seed = dict["seed"].AsInt32(),
			SectorIndex = dict["sector"].AsInt32(),
			CurrentNodeId = dict["node"].AsString(),
			Alive = !dict.ContainsKey("alive") || dict["alive"].AsBool(),
			Phase = dict.ContainsKey("phase") ? (CampaignPhase)dict["phase"].AsInt32() : CampaignPhase.ActiveOperations,
			ClaimsSecured = dict.ContainsKey("claims_secured") ? dict["claims_secured"].AsInt32() : 0,
			ManufacturerPayoutEarned = dict.ContainsKey("manufacturer_payout") ? dict["manufacturer_payout"].AsInt32() : 0,
			Graph = graph
		};
	}

	public static void ClearSave()
	{
		if (!Godot.FileAccess.FileExists(SavePath))
			return;
		using var dir = DirAccess.Open("user://");
		dir?.Remove("mechanize_campaign.json");
	}
}
