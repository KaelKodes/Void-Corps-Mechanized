using Godot;

namespace Mechanize;

/// <summary>Active campaign run — cadet academy, convention gate, or claim-sector ops.</summary>
public sealed class CampaignRun
{
	public const string SavePath = "user://mechanize_campaign.json";
	/// <summary>0-based; display as Sector 1..MaxSectors.</summary>
	public const int MaxSectors = 3;

	public int Seed { get; set; }
	public int SectorIndex { get; set; }
	public string CurrentNodeId { get; set; } = "";
	public bool Alive { get; set; } = true;
	public CampaignPhase Phase { get; set; } = CampaignPhase.ActiveOperations;
	public AcademyStep AcademyStep { get; set; } = AcademyStep.None;
	public int ClaimsSecured { get; set; }
	public int ManufacturerPayoutEarned { get; set; }
	public SectorGraph Graph { get; set; } = null!;
	public ConventionState Convention { get; set; } = new();

	public CampaignNode? CurrentNode => Graph?.Get(CurrentNodeId);
	public bool InCadetProgram => Phase == CampaignPhase.CadetProgram;
	public bool AtConventionGate => Phase == CampaignPhase.ManufacturerConvention;
	public bool InConventionHall => Phase == CampaignPhase.ManufacturerConvention;

	public static CampaignRun StartNew(int sectorIndex = 0, int seed = -1)
	{
		if (seed < 0)
			seed = (int)Time.GetTicksMsec();

		var graph = SectorGraphGenerator.Generate(
			$"sector_{sectorIndex + 1}",
			$"Sector {sectorIndex + 1}/{MaxSectors} — contested locations",
			seed ^ (sectorIndex * 7919));

		var run = new CampaignRun
		{
			Seed = seed,
			SectorIndex = sectorIndex,
			Phase = CampaignPhase.ActiveOperations,
			AcademyStep = AcademyStep.None,
			Graph = graph,
			CurrentNodeId = graph.StartNode.Id,
			Alive = true
		};
		run.Save();
		return run;
	}

	public static CampaignRun StartCadet(int seed = -1)
	{
		if (seed < 0)
			seed = (int)Time.GetTicksMsec();

		// Placeholder graph until graduation installs the convention gate.
		var graph = SectorGraphGenerator.GenerateConventionGate(seed);
		var run = new CampaignRun
		{
			Seed = seed,
			SectorIndex = 0,
			Phase = CampaignPhase.CadetProgram,
			AcademyStep = AcademyStep.Range,
			Graph = graph,
			CurrentNodeId = graph.StartNode.Id,
			Alive = true
		};
		run.Save();
		return run;
	}

	public void RestartCadetFromRange()
	{
		Phase = CampaignPhase.CadetProgram;
		AcademyStep = AcademyStep.Range;
		Alive = true;
		Save();
	}

	public void EnterConventionGate()
	{
		Phase = CampaignPhase.ManufacturerConvention;
		AcademyStep = AcademyStep.ConventionGate;
		Graph = SectorGraphGenerator.GenerateConventionGate(Seed ^ 4242);
		CurrentNodeId = Graph.StartNode.Id;
		Convention = new ConventionState();
		Convention.EnsureAllManufacturers();
		Alive = true;
		Save();
	}

	public void EnterActiveOperations()
	{
		Phase = CampaignPhase.ActiveOperations;
		AcademyStep = AcademyStep.None;
		Graph = SectorGraphGenerator.Generate(
			$"sector_{SectorIndex + 1}",
			$"Sector {SectorIndex + 1}/{MaxSectors} — contested locations",
			Seed ^ (SectorIndex * 7919) ^ 9176);
		CurrentNodeId = Graph.StartNode.Id;
		Alive = true;
		Save();
	}

	/// <summary>After a Warning claim: advance to next sector, or mark the run complete (kit kept).</summary>
	public bool TryAdvanceSectorOrComplete()
	{
		if (SectorIndex + 1 < MaxSectors)
		{
			SectorIndex++;
			EnterActiveOperations();
			return true;
		}

		Alive = false;
		Save();
		return false;
	}

	public int MaxLootTier => CatalogTiers.MaxTierForSector(SectorIndex);

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
			["academy_step"] = (int)AcademyStep,
			["claims_secured"] = ClaimsSecured,
			["manufacturer_payout"] = ManufacturerPayoutEarned,
			["graph"] = Graph.ToDict(),
			["convention"] = Convention.ToDict()
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
		var phase = dict.ContainsKey("phase")
			? (CampaignPhase)dict["phase"].AsInt32()
			: CampaignPhase.ActiveOperations;

		var needsMigrate = false;
		if (phase == CampaignPhase.ActiveOperations)
		{
			foreach (var n in graph.Nodes)
			{
				if (n.Kind is CampaignNodeKind.Mission or CampaignNodeKind.Warning
				    && (n.Offers.Count == 0 || string.IsNullOrEmpty(n.LocationClaimCode)))
				{
					needsMigrate = true;
					break;
				}
			}
		}

		if (needsMigrate)
			return StartNew(dict.ContainsKey("sector") ? dict["sector"].AsInt32() : 0,
				dict.ContainsKey("seed") ? dict["seed"].AsInt32() : -1);

		var convention = dict.ContainsKey("convention") && dict["convention"].VariantType == Variant.Type.Dictionary
			? ConventionState.FromDict(dict["convention"].AsGodotDictionary())
			: new ConventionState();
		convention.EnsureAllManufacturers();

		return new CampaignRun
		{
			Seed = dict["seed"].AsInt32(),
			SectorIndex = dict["sector"].AsInt32(),
			CurrentNodeId = dict["node"].AsString(),
			Alive = !dict.ContainsKey("alive") || dict["alive"].AsBool(),
			Phase = phase,
			AcademyStep = dict.ContainsKey("academy_step")
				? (AcademyStep)dict["academy_step"].AsInt32()
				: AcademyStep.None,
			ClaimsSecured = dict.ContainsKey("claims_secured") ? dict["claims_secured"].AsInt32() : 0,
			ManufacturerPayoutEarned = dict.ContainsKey("manufacturer_payout") ? dict["manufacturer_payout"].AsInt32() : 0,
			Graph = graph,
			Convention = convention
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
