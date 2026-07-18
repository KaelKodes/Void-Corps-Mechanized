using Godot;

namespace Mechanize;

/// <summary>A mercenary company competing for the same frontier claims as the player.</summary>
public sealed class RivalCorpDef
{
	public required string Id { get; init; }
	public required string DisplayName { get; init; }
	public required string ShortName { get; init; }
	public required string Description { get; init; }
	public required Color AccentColor { get; init; }
}

/// <summary>A recurring AI pilot belonging to a rival corp.</summary>
public sealed class RivalPilotDef
{
	public required string Id { get; init; }
	public required string CorpId { get; init; }
	public required string Name { get; init; }
	public required string Callsign { get; init; }
	public required string Role { get; init; }
	public required string Brief { get; init; }
	public int LoadoutVariant { get; init; }
	public float PreferredRange { get; init; } = 22f;
	public float Aggression { get; init; } = 0.85f;
	public float HullMultiplier { get; init; } = 1.2f;

	public string DisplayName => $"{Callsign} · {Name}";
}

/// <summary>
/// Stable roster used by Warning bosses and recurring mini-boss appearances.
/// Corps are mercenary organizations, not manufacturers.
/// </summary>
public static class RivalRosterCatalog
{
	public static readonly RivalCorpDef[] Corps =
	[
		new RivalCorpDef
		{
			Id = "grey_banner",
			DisplayName = "Grey Banner Company",
			ShortName = "Grey Banner",
			Description = "A disciplined claim-enforcement company built from demobilized security crews.",
			AccentColor = new Color(0.55f, 0.6f, 0.66f)
		},
		new RivalCorpDef
		{
			Id = "wayfarer_coop",
			DisplayName = "Wayfarer Cooperative",
			ShortName = "Wayfarer",
			Description = "Independent salvage crews pooling transport, pilots, and claim expenses.",
			AccentColor = new Color(0.82f, 0.62f, 0.28f)
		},
		new RivalCorpDef
		{
			Id = "ninth_meridian",
			DisplayName = "Ninth Meridian Company",
			ShortName = "Ninth Meridian",
			Description = "A small high-risk outfit that specializes in arriving before a claim is secure.",
			AccentColor = new Color(0.68f, 0.34f, 0.4f)
		}
	];

	public static readonly RivalPilotDef[] Pilots =
	[
		new RivalPilotDef
		{
			Id = "anja_serrin",
			CorpId = "grey_banner",
			Name = "Anja Serrin",
			Callsign = "Bastion",
			Role = "Claim captain",
			Brief = "Serrin holds ground methodically and brings support before committing her MAP.",
			LoadoutVariant = 2,
			PreferredRange = 20f,
			Aggression = 0.88f,
			HullMultiplier = 1.55f
		},
		new RivalPilotDef
		{
			Id = "elias_rowe",
			CorpId = "grey_banner",
			Name = "Elias Rowe",
			Callsign = "Harrow",
			Role = "Breach pilot",
			Brief = "Rowe closes distance early and uses armor to force opponents out of position.",
			LoadoutVariant = 2,
			PreferredRange = 16f,
			Aggression = 0.96f,
			HullMultiplier = 1.45f
		},
		new RivalPilotDef
		{
			Id = "mina_saye",
			CorpId = "grey_banner",
			Name = "Mina Saye",
			Callsign = "Ledger",
			Role = "Contract pilot",
			Brief = "Saye is conservative with ammunition and rarely pursues beyond the paid objective.",
			LoadoutVariant = 0,
			PreferredRange = 24f,
			Aggression = 0.74f,
			HullMultiplier = 1.22f
		},
		new RivalPilotDef
		{
			Id = "nadi_kess",
			CorpId = "wayfarer_coop",
			Name = "Nadi Kess",
			Callsign = "Latch",
			Role = "Recovery pilot",
			Brief = "Kess stays mobile, isolates damaged targets, and leaves before a fight stops paying.",
			LoadoutVariant = 1,
			PreferredRange = 26f,
			Aggression = 0.86f,
			HullMultiplier = 1.38f
		},
		new RivalPilotDef
		{
			Id = "bram_toller",
			CorpId = "wayfarer_coop",
			Name = "Bram Toller",
			Callsign = "Cairn",
			Role = "Escort lead",
			Brief = "Toller protects Wayfarer assets first and treats enemy MAPs as obstacles to the route.",
			LoadoutVariant = 0,
			PreferredRange = 21f,
			Aggression = 0.78f,
			HullMultiplier = 1.32f
		},
		new RivalPilotDef
		{
			Id = "jules_orra",
			CorpId = "wayfarer_coop",
			Name = "Jules Orra",
			Callsign = "Needle",
			Role = "Survey marksman",
			Brief = "Orra uses survey data to establish long firing lanes before the opposition arrives.",
			LoadoutVariant = 1,
			PreferredRange = 31f,
			Aggression = 0.82f,
			HullMultiplier = 1.34f
		},
		new RivalPilotDef
		{
			Id = "yara_quill",
			CorpId = "ninth_meridian",
			Name = "Yara Quill",
			Callsign = "Morrow",
			Role = "Advance pilot",
			Brief = "Quill enters disputed sites ahead of her support and relies on surprise to keep them.",
			LoadoutVariant = 2,
			PreferredRange = 18f,
			Aggression = 1f,
			HullMultiplier = 1.5f
		},
		new RivalPilotDef
		{
			Id = "dev_arlen",
			CorpId = "ninth_meridian",
			Name = "Dev Arlen",
			Callsign = "Kite",
			Role = "Interdiction pilot",
			Brief = "Arlen keeps opponents moving and punishes anyone who turns toward the objective.",
			LoadoutVariant = 1,
			PreferredRange = 28f,
			Aggression = 0.9f,
			HullMultiplier = 1.3f
		},
		new RivalPilotDef
		{
			Id = "tomas_rhee",
			CorpId = "ninth_meridian",
			Name = "Tomas Rhee",
			Callsign = "Knell",
			Role = "Close assault",
			Brief = "Rhee accepts expensive trades if they end the dispute before reinforcements arrive.",
			LoadoutVariant = 2,
			PreferredRange = 14f,
			Aggression = 1f,
			HullMultiplier = 1.42f
		}
	];

	public static RivalCorpDef GetCorp(string id)
	{
		foreach (var corp in Corps)
		{
			if (corp.Id == id)
				return corp;
		}

		return Corps[0];
	}

	public static RivalPilotDef GetPilot(string id)
	{
		foreach (var pilot in Pilots)
		{
			if (pilot.Id == id)
				return pilot;
		}

		return Pilots[0];
	}

	public static RivalPilotDef PickPilot(int seed)
	{
		var index = Mathf.Abs(seed % Pilots.Length);
		return Pilots[index];
	}
}
