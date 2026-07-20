using System.Collections.Generic;

namespace Mechanize;

/// <summary>Arm weapons — shared WeaponL / WeaponR pool.</summary>
public static class CatalogWeapons
{
	public static void Register(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		// Existing + expanded. Slot stored as WeaponL; garage treats L/R as one pool.
		var s = PartSlot.WeaponL;

		// --- Brimforge ballistic ---
		parts["wep_brin_slug"] = CatalogBuilders.Weapon("wep_brin_slug", "Slug Cannon", "brimforge", m, s, "cannon",
			18, 1.4f, 38f, 40f, AimMode.Fixed, 8f, 12f, WeaponFamily.Ballistic);
		parts["wep_brin_maul"] = CatalogBuilders.Weapon("wep_brin_maul", "Maul Howitzer", "brimforge", m, s, "cannon",
			28, 0.85f, 34f, 36f, AimMode.Fixed, 14f, 22f, WeaponFamily.Ballistic);
		parts["wep_brin_rivet"] = CatalogBuilders.Weapon("wep_brin_rivet", "Rivet Gun", "brimforge", m, s, "rifle",
			7, 7.5f, 28f, 48f, AimMode.Fixed, 1.8f, 7f, WeaponFamily.Ballistic);
		parts["wep_brin_anvil"] = CatalogBuilders.Weapon("wep_brin_anvil", "Anvil Mortar", "brimforge", m, s, "cannon",
			34, 0.65f, 42f, 28f, AimMode.Fixed, 18f, 28f, WeaponFamily.Ballistic);
		parts["wep_brin_chain"] = CatalogBuilders.Weapon("wep_brin_chain", "Chain Autocannon", "brimforge", m, s, "cannon",
			11, 4.2f, 36f, 44f, AimMode.Fixed, 4.5f, 14f, WeaponFamily.Ballistic);
		parts["wep_brin_pile"] = CatalogBuilders.Weapon("wep_brin_pile", "Pile Driver", "brimforge", m, s, "cannon",
			40, 0.5f, 22f, 32f, AimMode.Fixed, 22f, 32f, WeaponFamily.Ballistic);
		parts["wep_brin_scatter"] = CatalogBuilders.Weapon("wep_brin_scatter", "Scatter Bore", "brimforge", m, s, "cannon",
			14, 2.2f, 24f, 38f, AimMode.Fixed, 9f, 16f, WeaponFamily.Ballistic);
		parts["wep_brin_deny"] = CatalogBuilders.Weapon("wep_brin_deny", "Deny-Asset Lance", "brimforge", m, s, "cannon",
			22, 1.1f, 30f, 35f, AimMode.Fixed, 12f, 20f, WeaponFamily.Ballistic);

		// --- OuroTech precision ---
		parts["wep_ouro_rifle"] = CatalogBuilders.Weapon("wep_ouro_rifle", "Needle Rifle", "ourotech", m, s, "rifle",
			8, 6f, 50f, 70f, AimMode.Gimbaled, 2.2f, 6f, WeaponFamily.Ballistic);
		parts["wep_ouro_marksman"] = CatalogBuilders.Weapon("wep_ouro_marksman", "Marksman Caliper", "ourotech", m, s, "rifle",
			16, 1.8f, 55f, 85f, AimMode.Gimbaled, 6f, 10f, WeaponFamily.Ballistic,
			TargetingMode.AimedComponent);
		parts["wep_ouro_stitch"] = CatalogBuilders.Weapon("wep_ouro_stitch", "Stitch Carbine", "ourotech", m, s, "rifle",
			5, 9.5f, 40f, 75f, AimMode.Gimbaled, 1.4f, 5f, WeaponFamily.Ballistic);
		parts["wep_ouro_longneedle"] = CatalogBuilders.Weapon("wep_ouro_longneedle", "Longneedle", "ourotech", m, s, "rifle",
			12, 2.4f, 62f, 90f, AimMode.Gimbaled, 4.5f, 9f, WeaponFamily.Ballistic);
		parts["wep_ouro_scalpel"] = CatalogBuilders.Weapon("wep_ouro_scalpel", "Scalpel Rail", "ourotech", m, s, "rifle",
			20, 1.5f, 58f, 95f, AimMode.Gimbaled, 8f, 14f, WeaponFamily.Ballistic,
			TargetingMode.AimedComponent);
		parts["wep_ouro_pulse"] = CatalogBuilders.Weapon("wep_ouro_pulse", "Pulse Needle", "ourotech", m, s, "rifle",
			6, 7.5f, 46f, 72f, AimMode.Gimbaled, 1.9f, 7f, WeaponFamily.Ballistic);
		parts["wep_ouro_duelist"] = CatalogBuilders.Weapon("wep_ouro_duelist", "Duelist Sidearm", "ourotech", m, s, "rifle",
			10, 3.5f, 36f, 68f, AimMode.Gimbaled, 3.2f, 8f, WeaponFamily.Ballistic);
		parts["wep_ouro_whisper"] = CatalogBuilders.Weapon("wep_ouro_whisper", "Whisper Bore", "ourotech", m, s, "rifle",
			9, 4f, 48f, 80f, AimMode.Gimbaled, 2.8f, 6f, WeaponFamily.Ballistic);

		// --- Trinova hybrid ---
		parts["wep_tri_burst"] = CatalogBuilders.Weapon("wep_tri_burst", "Burst Pod", "trinova", m, s, "rifle",
			6, 8f, 32f, 55f, AimMode.Fixed, 1.6f, 5f, WeaponFamily.Ballistic);
		parts["wep_tri_patrol"] = CatalogBuilders.Weapon("wep_tri_patrol", "Patrol Carbine", "trinova", m, s, "rifle",
			7, 5.5f, 38f, 58f, AimMode.Fixed, 2f, 6f, WeaponFamily.Ballistic);
		parts["wep_tri_convoy"] = CatalogBuilders.Weapon("wep_tri_convoy", "Convoy Autogun", "trinova", m, s, "rifle",
			5, 10f, 30f, 52f, AimMode.Fixed, 1.3f, 5f, WeaponFamily.Ballistic);
		parts["wep_tri_workhorse"] = CatalogBuilders.Weapon("wep_tri_workhorse", "Workhorse Cannon", "trinova", m, s, "cannon",
			14, 2f, 36f, 42f, AimMode.Fixed, 6f, 11f, WeaponFamily.Ballistic);
		parts["wep_tri_hybrid"] = CatalogBuilders.Weapon("wep_tri_hybrid", "Hybrid Projector", "trinova", m, s, "energy",
			9, 3.5f, 40f, 60f, AimMode.Gimbaled, 4f, 10f, WeaponFamily.Energy);
		parts["wep_tri_fleet"] = CatalogBuilders.Weapon("wep_tri_fleet", "Fleet Softgun", "trinova", m, s, "rifle",
			8, 4.5f, 44f, 62f, AimMode.Gimbaled, 2.5f, 7f, WeaponFamily.Ballistic);
		parts["wep_tri_anchor"] = CatalogBuilders.Weapon("wep_tri_anchor", "Anchor Shot", "trinova", m, s, "cannon",
			20, 1.2f, 33f, 40f, AimMode.Fixed, 9f, 15f, WeaponFamily.Ballistic);

		// --- Lumina energy ---
		parts["wep_lum_arc"] = CatalogBuilders.Weapon("wep_lum_arc", "Arc Lance", "lumina", m, s, "energy",
			12, 2.5f, 45f, 60f, AimMode.Gimbaled, 7f, 14f, WeaponFamily.Energy);
		parts["wep_lum_volt"] = CatalogBuilders.Weapon("wep_lum_volt", "Volt Needle", "lumina", m, s, "energy",
			7, 5.5f, 42f, 78f, AimMode.Gimbaled, 3.5f, 11f, WeaponFamily.Energy);
		parts["wep_lum_prism"] = CatalogBuilders.Weapon("wep_lum_prism", "Prism Beam", "lumina", m, s, "energy",
			15, 1.8f, 52f, 100f, AimMode.Gimbaled, 9f, 18f, WeaponFamily.Energy);
		parts["wep_lum_surge"] = CatalogBuilders.Weapon("wep_lum_surge", "Surge Coil", "lumina", m, s, "energy",
			24, 1.0f, 38f, 55f, AimMode.Fixed, 14f, 26f, WeaponFamily.Energy);
		parts["wep_lum_ghost"] = CatalogBuilders.Weapon("wep_lum_ghost", "Ghost Arc", "lumina", m, s, "energy",
			10, 3.2f, 48f, 70f, AimMode.Gimbaled, 5.5f, 16f, WeaponFamily.Energy);
		parts["wep_lum_oracle"] = CatalogBuilders.Weapon("wep_lum_oracle", "Oracle Ray", "lumina", m, s, "energy",
			18, 1.6f, 60f, 110f, AimMode.Gimbaled, 10f, 20f, WeaponFamily.Energy,
			TargetingMode.AimedComponent);
		parts["wep_lum_spark"] = CatalogBuilders.Weapon("wep_lum_spark", "Spark Cascade", "lumina", m, s, "energy",
			4, 11f, 28f, 65f, AimMode.Gimbaled, 1.8f, 9f, WeaponFamily.Energy);
		parts["wep_lum_well"] = CatalogBuilders.Weapon("wep_lum_well", "Well Emitter", "lumina", m, s, "energy",
			13, 2.2f, 40f, 58f, AimMode.Fixed, 8f, 17f, WeaponFamily.Energy);

		// Combat slice: one melee + one held shield (arm slots).
		parts["wep_brin_cleaver"] = CatalogBuilders.MeleeWeapon(
			"wep_brin_cleaver", "Forge Cleaver", "brimforge", m, s, "cleaver",
			damage: 32f, fireRate: 0.85f, range: 2.85f,
			heatShot: 3f, powerLoad: 0f, armor: 26f, structureHp: 68f);
		parts["wep_tri_bulwark"] = CatalogBuilders.HeldShield(
			"wep_tri_bulwark", "Bulwark Plate", "trinova", m, s, "held_shield",
			arcDegrees: 120f, raisePowerPerSec: 14f, heatPerDamage: 0.5f,
			armor: 28f, structureHp: 78f, powerReq: 9f);
	}
}
