using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

public enum SolarLocationKind
{
	Command,
	Operation,
	Merchant
}

public sealed class SolarLocationData
{
	public string Id { get; init; } = "";
	public string DisplayName { get; init; } = "";
	public string RegionName { get; init; } = "";
	public SolarLocationKind Kind { get; init; }
	public int Column { get; init; }
	public int Row { get; init; }
	public string ClaimCode { get; init; } = "";
	public int ThreatTier { get; init; } = 1;
	public string MerchantName { get; init; } = "";
	public List<string> Prerequisites { get; init; } = new();
	public List<MissionType> Missions { get; init; } = new();
	public List<string> CommonPartDrops { get; init; } = new();
	public List<string> RarePartDrops { get; init; } = new();
	public List<string> MaterialDrops { get; init; } = new();
}

/// <summary>Authored, revisit-able locations in the company's target system.</summary>
public static class SolarSystemCatalog
{
	private static List<SolarLocationData>? _locations;
	public static IReadOnlyList<SolarLocationData> Locations => _locations ??= Build();

	public static SolarLocationData? Get(string id) => Locations.FirstOrDefault(l => l.Id == id);

	private static List<SolarLocationData> Build()
	{
		GameCatalog.EnsureBuilt();
		var claims = VoidCorpsIdentity.StandardClaimSites.ToList();
		var operations = new[]
		{
			("ferrum_reach", "Ferrum Reach", "Inner Belt", 1, 0, 1),
			("cinder_moon", "Cinder Moon", "Inner Belt", 1, 2, 1),
			("glass_wastes", "Glass Wastes", "Orison", 2, 0, 1),
			("relay_six", "Relay Six", "Orison", 2, 2, 1),
			("verdant_shelf", "Verdant Shelf", "Pelagos", 3, 0, 2),
			("drowned_array", "Drowned Array", "Pelagos", 3, 2, 2),
			("red_quarry", "Red Quarry", "Khepri", 4, 0, 2),
			("hollow_station", "Hollow Station", "Khepri", 4, 2, 2),
			("vault_perihelion", "Vault Perihelion", "Outer Dark", 5, 0, 3),
			("anomaly_crown", "Anomaly Crown", "Outer Dark", 5, 2, 3),
			("claim_nexus", "Claim Nexus", "Outer Dark", 6, 1, 3)
		};

		var parts = GameCatalog.Parts.Values
			.Where(p => p.VisualKind != "empty")
			.OrderBy(p => p.Tier)
			.ThenBy(p => p.Id)
			.ToList();
		var locations = new List<SolarLocationData>
		{
			new()
			{
				Id = "company_anchor",
				DisplayName = "Company Anchor",
				RegionName = "Entry Orbit",
				Kind = SolarLocationKind.Command,
				Column = 0,
				Row = 1,
				ThreatTier = 1
			}
		};

		for (var i = 0; i < operations.Length; i++)
		{
			var op = operations[i];
			var common = parts
				.Where(p => p.Tier <= op.Item6)
				.Where((_, index) => index % operations.Length == i % operations.Length)
				.Take(4)
				.Select(p => p.Id)
				.ToList();
			var rare = parts
				.Where(p => p.Tier >= op.Item6)
				.Where((_, index) => index % operations.Length == (i * 3 + 1) % operations.Length)
				.Take(2)
				.Select(p => p.Id)
				.ToList();
			var previousColumn = op.Item4 - 1;
			var prereqs = previousColumn <= 0
				? new List<string> { "company_anchor" }
				: locations
					.Where(l => l.Kind == SolarLocationKind.Operation && l.Column == previousColumn)
					.Select(l => l.Id)
					.ToList();
			var claim = claims[i % claims.Count];
			locations.Add(new SolarLocationData
			{
				Id = op.Item1,
				DisplayName = op.Item2,
				RegionName = op.Item3,
				Kind = SolarLocationKind.Operation,
				Column = op.Item4,
				Row = op.Item5,
				ClaimCode = claim.Code,
				ThreatTier = op.Item6,
				Prerequisites = prereqs,
				Missions = MissionsFor(i, op.Item6),
				CommonPartDrops = common,
				RarePartDrops = rare,
				MaterialDrops = MaterialsFor(op.Item6, i)
			});
		}

		locations.Add(Merchant("drift_market", "Drift Market", "Mara Venn", 2, 1, "ferrum_reach"));
		locations.Add(Merchant("pilgrim_exchange", "Pilgrim Exchange", "Old Kesh", 4, 1, "verdant_shelf"));
		locations.Add(Merchant("farline_bazaar", "Farline Bazaar", "Sable & Sons", 5, 1, "red_quarry"));
		return locations;
	}

	private static SolarLocationData Merchant(
		string id,
		string name,
		string merchant,
		int column,
		int row,
		string prerequisite) => new()
	{
		Id = id,
		DisplayName = name,
		RegionName = "Free Trader Route",
		Kind = SolarLocationKind.Merchant,
		Column = column,
		Row = row,
		MerchantName = merchant,
		Prerequisites = new List<string> { prerequisite },
		ThreatTier = Mathf.Clamp(column / 2, 1, 3)
	};

	private static List<MissionType> MissionsFor(int index, int tier)
	{
		var pool = new[]
		{
			MissionType.DestroyAllEnemies,
			MissionType.SearchAndDestroy,
			MissionType.CaptureArea,
			MissionType.CaptureMultipleAreas,
			MissionType.DataRetrieval,
			MissionType.SwarmDefend,
			MissionType.Escort,
			MissionType.Sabotage
		};
		var result = new List<MissionType>
		{
			pool[index % pool.Length],
			pool[(index + 3) % pool.Length]
		};
		// Every operation zone can support the employer's extraction chain.
		// These repeatable convoys fund settlement construction.
		if (!result.Contains(MissionType.Escort))
			result.Add(MissionType.Escort);
		if (tier >= 3 || index == 10)
			result.Add(MissionType.BossEncounter);
		return result;
	}

	private static List<string> MaterialsFor(int tier, int index)
	{
		var basic = new[] { MaterialCatalog.Alloy, MaterialCatalog.Servo, MaterialCatalog.Circuit };
		var result = new List<string> { basic[index % basic.Length], basic[(index + 1) % basic.Length] };
		if (tier >= 2)
			result.Add(index % 2 == 0 ? MaterialCatalog.Optics : MaterialCatalog.Reactor);
		if (tier >= 3)
			result.Add(MaterialCatalog.Exotic);
		return result;
	}
}

public sealed class SolarCampaignRun
{
	public const string SavePath = "user://mechanize_solar_campaign.json";
	public int CompanySeed { get; set; } = (int)Time.GetTicksMsec();
	public List<string> ConventionCompanyIds { get; } = new();
	public string SelectedCompanyId { get; set; } = "";
	public bool OnboardingComplete { get; set; }
	public int SettlementStage { get; set; }
	public int MiningConvoysCompleted { get; set; }
	public HashSet<string> UnlockedLocations { get; } = new() { "company_anchor" };
	public Dictionary<string, int> Completions { get; } = new();
	public string SelectedLocationId { get; set; } = "company_anchor";

	public bool IsUnlocked(string id) => UnlockedLocations.Contains(id);
	public int CompletionCount(string id) => Completions.GetValueOrDefault(id);
	public IReadOnlyList<FrontierCompanyData> ConventionCompanies
	{
		get
		{
			EnsureCompanies();
			return FrontierCompanyCatalog.Generate(CompanySeed, ConventionCompanyIds);
		}
	}
	public FrontierCompanyData? SelectedCompany =>
		ConventionCompanies.FirstOrDefault(c => c.Id == SelectedCompanyId);

	public void EnsureCompanies()
	{
		if (ConventionCompanyIds.Count >= 4)
			return;
		ConventionCompanyIds.Clear();
		foreach (var company in FrontierCompanyCatalog.Generate(CompanySeed))
			ConventionCompanyIds.Add(company.Id);
	}

	public string SettlementStageName => SettlementStage switch
	{
		0 => "Landing Charter",
		1 => "Survey Outpost",
		2 => "Frontier Settlement",
		_ => "Charter City"
	};

	public Dictionary<string, int> NextSettlementCost() => SettlementStage switch
	{
		0 => new Dictionary<string, int>
		{
			[MaterialCatalog.Alloy] = 12,
			[MaterialCatalog.Circuit] = 6
		},
		1 => new Dictionary<string, int>
		{
			[MaterialCatalog.Alloy] = 25,
			[MaterialCatalog.Servo] = 12,
			[MaterialCatalog.Circuit] = 10,
			[MaterialCatalog.Optics] = 4
		},
		2 => new Dictionary<string, int>
		{
			[MaterialCatalog.Alloy] = 45,
			[MaterialCatalog.Servo] = 20,
			[MaterialCatalog.Circuit] = 18,
			[MaterialCatalog.Reactor] = 10,
			[MaterialCatalog.Exotic] = 3
		},
		_ => new Dictionary<string, int>()
	};

	public int NextSettlementConvoyRequirement() => SettlementStage switch
	{
		0 => 1,
		1 => 3,
		2 => 6,
		_ => 0
	};

	public bool TryAdvanceSettlement(PlayerProfile profile)
	{
		if (SettlementStage >= 3 || MiningConvoysCompleted < NextSettlementConvoyRequirement())
			return false;
		var cost = NextSettlementCost();
		if (cost.Any(kv => profile.MaterialCount(kv.Key) < kv.Value))
			return false;
		foreach (var (id, amount) in cost)
			profile.AddMaterial(id, -amount);
		SettlementStage++;
		Save();
		return true;
	}

	public void Complete(string id)
	{
		Completions[id] = CompletionCount(id) + 1;
		RefreshUnlocks();
		Save();
	}

	public void RefreshUnlocks()
	{
		var changed = true;
		while (changed)
		{
			changed = false;
			foreach (var location in SolarSystemCatalog.Locations)
			{
				if (UnlockedLocations.Contains(location.Id))
					continue;
				if (location.Prerequisites.Count == 0
				    || location.Prerequisites.Any(p =>
					    p == "company_anchor" || CompletionCount(p) > 0))
				{
					UnlockedLocations.Add(location.Id);
					changed = true;
				}
			}
		}
	}

	public void Save()
	{
		EnsureCompanies();
		var unlocked = new Godot.Collections.Array();
		foreach (var id in UnlockedLocations.OrderBy(id => id))
			unlocked.Add(id);
		var completions = new Godot.Collections.Dictionary();
		foreach (var (id, count) in Completions)
			completions[id] = count;
		var companies = new Godot.Collections.Array();
		foreach (var id in ConventionCompanyIds)
			companies.Add(id);
		var dict = new Godot.Collections.Dictionary
		{
			["company_seed"] = CompanySeed,
			["companies"] = companies,
			["selected_company"] = SelectedCompanyId,
			["onboarding_complete"] = OnboardingComplete,
			["settlement_stage"] = SettlementStage,
			["mining_convoys"] = MiningConvoysCompleted,
			["selected"] = SelectedLocationId,
			["unlocked"] = unlocked,
			["completions"] = completions
		};
		using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(Json.Stringify(dict, "\t"));
	}

	public static SolarCampaignRun LoadOrNew()
	{
		var run = new SolarCampaignRun();
		if (!Godot.FileAccess.FileExists(SavePath))
		{
			run.RefreshUnlocks();
			run.Save();
			return run;
		}
		using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
		var parsed = file == null ? default : Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
			return run;
		var dict = parsed.AsGodotDictionary();
		run.CompanySeed = dict.ContainsKey("company_seed") ? dict["company_seed"].AsInt32() : run.CompanySeed;
		run.ConventionCompanyIds.Clear();
		if (dict.ContainsKey("companies"))
		{
			foreach (var value in dict["companies"].AsGodotArray())
				run.ConventionCompanyIds.Add(value.AsString());
		}
		run.SelectedCompanyId = dict.ContainsKey("selected_company") ? dict["selected_company"].AsString() : "";
		// Existing solar saves predate onboarding; preserve their unlocked map instead
		// of forcing a completed campaign back through the academy.
		run.OnboardingComplete = !dict.ContainsKey("onboarding_complete")
			|| dict["onboarding_complete"].AsBool();
		run.SettlementStage = dict.ContainsKey("settlement_stage") ? dict["settlement_stage"].AsInt32() : 0;
		run.MiningConvoysCompleted = dict.ContainsKey("mining_convoys") ? dict["mining_convoys"].AsInt32() : 0;
		run.UnlockedLocations.Clear();
		if (dict.ContainsKey("unlocked"))
		{
			foreach (var value in dict["unlocked"].AsGodotArray())
				run.UnlockedLocations.Add(value.AsString());
		}
		run.UnlockedLocations.Add("company_anchor");
		if (dict.ContainsKey("completions"))
		{
			foreach (var (key, value) in dict["completions"].AsGodotDictionary())
				run.Completions[key.AsString()] = value.AsInt32();
		}
		if (dict.ContainsKey("selected"))
			run.SelectedLocationId = dict["selected"].AsString();
		run.EnsureCompanies();
		if (run.OnboardingComplete && string.IsNullOrEmpty(run.SelectedCompanyId))
			run.SelectedCompanyId = run.ConventionCompanyIds[0];
		run.RefreshUnlocks();
		return run;
	}
}
