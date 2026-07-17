using Godot;

namespace Mechanize;

/// <summary>Active roguelike campaign run across claim-sectors.</summary>
public sealed class CampaignRun
{
	public const string SavePath = "user://mechanize_campaign.json";

	public int Seed { get; set; }
	public int SectorIndex { get; set; }
	public string CurrentNodeId { get; set; } = "";
	public int ScoutRange { get; set; } = 1;
	public bool Alive { get; set; } = true;
	public SectorGraph Graph { get; set; } = null!;

	public CampaignNode? CurrentNode => Graph?.Get(CurrentNodeId);

	public static CampaignRun StartNew(int sectorIndex = 0, int seed = -1)
	{
		if (seed < 0)
			seed = (int)Time.GetTicksMsec();

		sectorIndex = Mathf.Clamp(sectorIndex, 0, VoidCorpsIdentity.ClaimSites.Length - 1);
		var claim = VoidCorpsIdentity.ClaimSites[sectorIndex];
		var boss = BossEncounterCatalog.ForSectorClaim(claim.Code);
		var graph = SectorGraphGenerator.Generate(
			claim.Code,
			$"Sector {sectorIndex + 1} — {claim.DisplayName}",
			seed ^ (sectorIndex * 7919),
			boss);

		var run = new CampaignRun
		{
			Seed = seed,
			SectorIndex = sectorIndex,
			Graph = graph,
			CurrentNodeId = graph.StartNode.Id,
			ScoutRange = 1,
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

	public void MarkCurrentCleared()
	{
		var node = CurrentNode;
		if (node != null)
			node.Cleared = true;
		Save();
	}

	public void EndRun()
	{
		Alive = false;
		Save();
	}

	public bool IsRevealed(CampaignNode node)
	{
		if (node.Id == CurrentNodeId || node.Cleared)
			return true;
		if (Graph == null)
			return false;

		// Scout range 1: adjacent forward from current.
		if (ScoutRange <= 1)
			return Graph.IsAdjacent(CurrentNodeId, node.Id);

		// Reserved: deeper scout walks forward hops.
		var frontier = new System.Collections.Generic.HashSet<string> { CurrentNodeId };
		for (var hop = 0; hop < ScoutRange; hop++)
		{
			var next = new System.Collections.Generic.HashSet<string>();
			foreach (var id in frontier)
			{
				foreach (var n in Graph.NeighborsForward(id))
					next.Add(n.Id);
			}

			if (next.Contains(node.Id))
				return true;
			frontier = next;
		}

		return false;
	}

	public void Save()
	{
		var dict = new Godot.Collections.Dictionary
		{
			["seed"] = Seed,
			["sector"] = SectorIndex,
			["node"] = CurrentNodeId,
			["scout"] = ScoutRange,
			["alive"] = Alive,
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

		return new CampaignRun
		{
			Seed = dict["seed"].AsInt32(),
			SectorIndex = dict["sector"].AsInt32(),
			CurrentNodeId = dict["node"].AsString(),
			ScoutRange = dict.ContainsKey("scout") ? dict["scout"].AsInt32() : 1,
			Alive = !dict.ContainsKey("alive") || dict["alive"].AsBool(),
			Graph = SectorGraph.FromDict(dict["graph"].AsGodotDictionary())
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
