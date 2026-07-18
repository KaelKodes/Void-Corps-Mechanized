using System.Collections.Generic;

namespace Mechanize;

/// <summary>
/// Assigns Field / Claim / Threat tiers after catalogue registration.
/// Unlisted parts stay Tier 1 (field kit).
/// </summary>
public static class CatalogTiers
{
	public const int MaxTier = 3;

	public static string Label(int tier) => tier switch
	{
		2 => "T2 Claim",
		3 => "T3 Threat",
		_ => "T1 Field"
	};

	public static string ShortLabel(int tier) => tier switch
	{
		2 => "T2",
		3 => "T3",
		_ => "T1"
	};

	/// <summary>Max shop/loot tier for a 0-based sector index (0=S1 … 2=S3).</summary>
	public static int MaxTierForSector(int sectorIndex)
	{
		return MathfClamp(sectorIndex + 1, 1, MaxTier);
	}

	private static int MathfClamp(int v, int min, int max) =>
		v < min ? min : v > max ? max : v;

	public static void Apply(Dictionary<string, PartData> parts)
	{
		foreach (var id in Tier2)
		{
			if (parts.TryGetValue(id, out var p))
				p.Tier = 2;
		}

		foreach (var id in Tier3)
		{
			if (parts.TryGetValue(id, out var p))
				p.Tier = 3;
		}
	}

	private static readonly HashSet<string> Tier2 = new()
	{
		// Frames
		"legs_tri_courier", "legs_brin_biped", "legs_ouro_hex", "legs_ouro_duelist",
		"legs_lum_hex", "legs_tri_packhex", "legs_tri_tracks", "legs_ouro_rail", "legs_lum_magbelt",
		"torso_brin_slab", "torso_tri_cargo", "torso_ouro_thin", "torso_ouro_caliper",
		"torso_lum_shell", "torso_lum_prism",
		"head_tri_logistics", "head_tri_convoy", "head_brin_helm", "head_ouro_scope",
		"head_ouro_duelist", "head_lum_mask", "head_lum_ghost",
		// Cores / systems
		"core_tri_hauler", "core_ouro_pulse", "core_ouro_whisper", "core_brin_slag", "core_brin_anvil",
		"core_lum_arc", "core_lum_ghost",
		"systems_ouro_radiator", "systems_brin_vent", "systems_tri_balancer", "systems_tri_field",
		"systems_lum_cryo",
		// Weapons
		"wep_brin_maul", "wep_brin_rivet", "wep_brin_chain", "wep_brin_scatter",
		"wep_ouro_marksman", "wep_ouro_stitch", "wep_ouro_pulse", "wep_ouro_duelist",
		"wep_tri_patrol", "wep_tri_workhorse", "wep_tri_hybrid", "wep_tri_fleet",
		"wep_lum_volt", "wep_lum_ghost", "wep_lum_well",
		// Mounts
		"shoulder_brin_pods", "shoulder_brin_deny", "shoulder_ouro_tracker", "shoulder_ouro_needle",
		"shoulder_tri_fleet", "shoulder_tri_convoy", "shoulder_lum_arc", "shoulder_lum_ghost",
		"backpack_tri_field", "backpack_tri_aegis", "backpack_ouro_pulse", "backpack_ouro_stitch",
		"backpack_ouro_cooler", "backpack_lum_veil", "backpack_lum_capacitor", "backpack_brin_plate",
		"backpack_brin_mend"
	};

	private static readonly HashSet<string> Tier3 = new()
	{
		// Frames
		"legs_brin_bulwark", "legs_ouro_razorhex", "legs_lum_glasswalk", "legs_lum_phasehex",
		"legs_brin_siegehex", "legs_brin_tracks", "legs_brin_fortress", "legs_tri_hauler",
		"torso_brin_anvil", "torso_brin_citadel", "torso_ouro_apex", "torso_lum_oracle",
		"head_brin_visor", "head_brin_warface", "head_ouro_reticle", "head_ouro_whisper",
		"head_lum_oracle",
		// Cores / systems
		"core_ouro_apex", "core_brin_forge", "core_brin_citadel", "core_lum_oracle",
		"systems_ouro_servo", "systems_brin_slag", "systems_brin_armor",
		"systems_lum_phase", "systems_lum_oracle",
		// Weapons
		"wep_brin_anvil", "wep_brin_pile", "wep_brin_deny",
		"wep_ouro_longneedle", "wep_ouro_scalpel", "wep_ouro_whisper",
		"wep_tri_anchor",
		"wep_lum_prism", "wep_lum_surge", "wep_lum_oracle",
		// Mounts
		"shoulder_brin_barrage", "shoulder_brin_siege", "shoulder_ouro_caliper", "shoulder_ouro_whisper",
		"shoulder_lum_prism",
		"backpack_tri_convoy", "backpack_tri_hauler", "backpack_ouro_surge",
		"backpack_lum_shroud", "backpack_lum_phase",
		"backpack_brin_citadel", "backpack_brin_slag"
	};
}
