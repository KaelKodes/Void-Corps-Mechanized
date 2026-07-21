using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Legs, torsos, and heads — MAP frame catalogue.</summary>
public static class CatalogFrames
{
	public static void Register(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		RegisterLegs(parts, m);
		RegisterTorsos(parts, m);
		RegisterHeads(parts, m);
	}

	private static void RegisterLegs(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		// --- Bipedal ---
		parts["legs_tri_biped"] = CatalogBuilders.Leg("legs_tri_biped", "Stride Biped", "trinova", m,
			25, 11f, 95f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.5f, sprintHeat: 16f, sprintLoad: 22f, moveHeat: 2f);
		parts["legs_tri_courier"] = CatalogBuilders.Leg("legs_tri_courier", "Courier Striders", "trinova", m,
			18, 13.5f, 105f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.65f, sprintHeat: 18f, sprintLoad: 20f, moveHeat: 1.6f, idleHeat: 0.4f);
		parts["legs_brin_biped"] = CatalogBuilders.Leg("legs_brin_biped", "Stomp Biped", "brimforge", m,
			45, 7.5f, 60f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.35f, sprintHeat: 22f, sprintLoad: 28f, moveHeat: 3.5f);
		parts["legs_brin_bulwark"] = CatalogBuilders.Leg("legs_brin_bulwark", "Bulwark Pedestals", "brimforge", m,
			58, 6.2f, 48f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.22f, sprintHeat: 26f, sprintLoad: 34f, moveHeat: 4.2f, idleHeat: 0.8f);
		parts["legs_ouro_duelist"] = CatalogBuilders.Leg("legs_ouro_duelist", "Duelist Spurs", "ourotech", m,
			16, 12.8f, 140f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.58f, sprintHeat: 13f, sprintLoad: 18f, moveHeat: 1.8f, idleHeat: 0.35f);
		parts["legs_lum_glasswalk"] = CatalogBuilders.Leg("legs_lum_glasswalk", "Glasswalk Digits", "lumina", m,
			14, 12f, 120f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.52f, sprintHeat: 12f, sprintLoad: 24f, moveHeat: 1.5f, idleHeat: 0.55f);

		// --- Hexapod ---
		parts["legs_ouro_hex"] = CatalogBuilders.Leg("legs_ouro_hex", "Skitter Hexapod", "ourotech", m,
			15, 12.5f, 130f, "legs_hex", LegMode.Gimbaled, LegType.Hexapod,
			canSprint: true, sprintMult: 1.55f, sprintHeat: 14f, sprintLoad: 20f, moveHeat: 2.5f);
		parts["legs_ouro_razorhex"] = CatalogBuilders.Leg("legs_ouro_razorhex", "Razorhex Array", "ourotech", m,
			12, 14f, 150f, "legs_hex", LegMode.Gimbaled, LegType.Hexapod,
			canSprint: true, sprintMult: 1.7f, sprintHeat: 16f, sprintLoad: 22f, moveHeat: 2.8f, idleHeat: 0.45f);
		parts["legs_lum_hex"] = CatalogBuilders.Leg("legs_lum_hex", "Vault Crawler", "lumina", m,
			20, 11f, 110f, "legs_hex", LegMode.Gimbaled, LegType.Hexapod,
			canSprint: true, sprintMult: 1.48f, sprintHeat: 15f, sprintLoad: 21f, moveHeat: 2.2f);
		parts["legs_lum_phasehex"] = CatalogBuilders.Leg("legs_lum_phasehex", "Phase Skitter", "lumina", m,
			11, 13.2f, 125f, "legs_hex", LegMode.Gimbaled, LegType.Hexapod,
			canSprint: true, sprintMult: 1.62f, sprintHeat: 11f, sprintLoad: 26f, moveHeat: 1.9f, idleHeat: 0.7f);
		parts["legs_tri_packhex"] = CatalogBuilders.Leg("legs_tri_packhex", "Packmule Hex", "trinova", m,
			28, 10f, 100f, "legs_hex", LegMode.Gimbaled, LegType.Hexapod,
			canSprint: true, sprintMult: 1.4f, sprintHeat: 17f, sprintLoad: 24f, moveHeat: 2.6f);
		parts["legs_brin_siegehex"] = CatalogBuilders.Leg("legs_brin_siegehex", "Siege Hexapod", "brimforge", m,
			40, 8f, 70f, "legs_hex", LegMode.Gimbaled, LegType.Hexapod,
			canSprint: true, sprintMult: 1.3f, sprintHeat: 24f, sprintLoad: 30f, moveHeat: 3.8f, idleHeat: 0.7f);

		// --- Tracks ---
		parts["legs_brin_tracks"] = CatalogBuilders.Leg("legs_brin_tracks", "Forge Tracks", "brimforge", m,
			55, 9f, 40f, "legs_tracks", LegMode.Locked, LegType.Tracks,
			canSprint: false, moveHeat: 4f, idleHeat: 1f);
		parts["legs_brin_fortress"] = CatalogBuilders.Leg("legs_brin_fortress", "Fortress Belts", "brimforge", m,
			70, 7.5f, 32f, "legs_tracks", LegMode.Locked, LegType.Tracks,
			canSprint: false, moveHeat: 5f, idleHeat: 1.3f);
		parts["legs_tri_tracks"] = CatalogBuilders.Leg("legs_tri_tracks", "Convoy Treads", "trinova", m,
			35, 10.5f, 55f, "legs_tracks", LegMode.Locked, LegType.Tracks,
			canSprint: false, moveHeat: 3f);
		parts["legs_tri_hauler"] = CatalogBuilders.Leg("legs_tri_hauler", "Hauler Continuous", "trinova", m,
			42, 9.5f, 50f, "legs_tracks", LegMode.Locked, LegType.Tracks,
			canSprint: false, moveHeat: 3.4f, idleHeat: 0.7f);
		parts["legs_ouro_rail"] = CatalogBuilders.Leg("legs_ouro_rail", "Railglide Tracks", "ourotech", m,
			22, 11.5f, 75f, "legs_tracks", LegMode.Locked, LegType.Tracks,
			canSprint: false, moveHeat: 2.4f, idleHeat: 0.45f);
		parts["legs_lum_magbelt"] = CatalogBuilders.Leg("legs_lum_magbelt", "Magbelt Cradle", "lumina", m,
			24, 10.8f, 65f, "legs_tracks", LegMode.Locked, LegType.Tracks,
			canSprint: false, moveHeat: 2.2f, idleHeat: 0.85f);

		// --- Boosters (jump) — bipedal; no sprint; MobilityModule.Booster ---
		parts["legs_tri_jumpjack"] = CatalogBuilders.BoosterLegs("legs_tri_jumpjack", "Jumpjack Struts", "trinova", m,
			20, 11.2f, 100f, jumpImpulse: 11f, jumpPower: 14f, jumpHeat: 10f, moveHeat: 1.9f, idleHeat: 0.45f);
		parts["legs_lum_boosters"] = CatalogBuilders.BoosterLegs("legs_lum_boosters", "Vault Boosters", "lumina", m,
			13, 11.8f, 118f, jumpImpulse: 14.5f, jumpPower: 16f, jumpHeat: 9f, moveHeat: 1.6f, idleHeat: 0.55f);
		parts["legs_brin_pilejack"] = CatalogBuilders.BoosterLegs("legs_brin_pilejack", "Pilejack Pedestals", "brimforge", m,
			42, 7.8f, 55f, jumpImpulse: 16f, jumpPower: 22f, jumpHeat: 18f, moveHeat: 3.6f, idleHeat: 0.85f);
		parts["legs_ouro_ascender"] = CatalogBuilders.BoosterLegs("legs_ouro_ascender", "Ascender Spurs", "ourotech", m,
			12, 12.5f, 135f, jumpImpulse: 13f, jumpPower: 12f, jumpHeat: 8f, moveHeat: 1.7f, idleHeat: 0.4f);

		// --- Thrusters (dash) — bipedal; no sprint; MobilityModule.Thruster ---
		parts["legs_ouro_thrusters"] = CatalogBuilders.ThrusterLegs("legs_ouro_thrusters", "Rail Thrusters", "ourotech", m,
			15, 12.2f, 130f, LegMode.Gimbaled,
			dashSpeed: 28f, dashDuration: 0.18f, dashCooldown: 1.1f, dashPower: 14f, dashHeat: 9f,
			moveHeat: 1.9f, idleHeat: 0.4f);
		parts["legs_lum_vector"] = CatalogBuilders.ThrusterLegs("legs_lum_vector", "Vector Skates", "lumina", m,
			12, 12f, 122f, LegMode.Gimbaled,
			dashSpeed: 32f, dashDuration: 0.16f, dashCooldown: 0.95f, dashPower: 15f, dashHeat: 8f,
			moveHeat: 1.7f, idleHeat: 0.5f);
		parts["legs_tri_slide"] = CatalogBuilders.ThrusterLegs("legs_tri_slide", "Slide Rails", "trinova", m,
			22, 11f, 95f, LegMode.Locked,
			dashSpeed: 24f, dashDuration: 0.2f, dashCooldown: 1.35f, dashPower: 12f, dashHeat: 11f,
			moveHeat: 2.1f, idleHeat: 0.45f);
		parts["legs_brin_charge"] = CatalogBuilders.ThrusterLegs("legs_brin_charge", "Charge Pedestals", "brimforge", m,
			48, 7.2f, 50f, LegMode.Locked,
			dashSpeed: 22f, dashDuration: 0.22f, dashCooldown: 1.6f, dashPower: 20f, dashHeat: 16f,
			moveHeat: 3.8f, idleHeat: 0.9f);

		// --- Ashwhisk / Velhound body houses (skirmish Whisperframe / Yardbreaker; T1 Field) ---
		parts["legs_ash_coilstriders"] = CatalogBuilders.Leg("legs_ash_coilstriders", "Coilstriders", "ashwhisk", m,
			16, 14f, 125f, "legs_ash_coil", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.68f, sprintHeat: 15f, sprintLoad: 19f, moveHeat: 1.5f, idleHeat: 0.35f);
		parts["legs_vel_bracehounds"] = CatalogBuilders.Leg("legs_vel_bracehounds", "Bracehounds", "velhound", m,
			38, 10.2f, 78f, "legs_biped", LegMode.Locked, LegType.Bipedal,
			canSprint: true, sprintMult: 1.42f, sprintHeat: 20f, sprintLoad: 26f, moveHeat: 3.0f, idleHeat: 0.65f);
	}

	private static void RegisterTorsos(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		parts["torso_brin_slab"] = CatalogBuilders.Torso("torso_brin_slab", "Slab Chassis", "brimforge", m,
			80, housing: 3, structureHp: 85, shoulders: 2, backs: 1,
			scale: new Vector3(1.2f, 1.1f, 1.15f), heatCap: 25, idleHeat: 1.5f);
		parts["torso_brin_anvil"] = CatalogBuilders.Torso("torso_brin_anvil", "Anvil Ribcage", "brimforge", m,
			95, housing: 3, structureHp: 100, shoulders: 2, backs: 1,
			scale: new Vector3(1.28f, 1.15f, 1.2f), heatCap: 35, idleHeat: 1.9f,
			visualKind: "torso_brin_anvil");
		parts["torso_brin_citadel"] = CatalogBuilders.Torso("torso_brin_citadel", "Citadel Hull", "brimforge", m,
			110, housing: 3, structureHp: 110, shoulders: 2, backs: 0,
			scale: new Vector3(1.35f, 1.2f, 1.25f), heatCap: 40, idleHeat: 2.2f);

		parts["torso_tri_frame"] = CatalogBuilders.Torso("torso_tri_frame", "Frame Lattice", "trinova", m,
			50, housing: 2, structureHp: 65, shoulders: 1, backs: 1, heatCap: 15, idleHeat: 1f);
		parts["torso_tri_cargo"] = CatalogBuilders.Torso("torso_tri_cargo", "Cargo Lattice", "trinova", m,
			48, housing: 2, structureHp: 68, shoulders: 1, backs: 1,
			scale: new Vector3(1.1f, 1.05f, 1.15f), heatCap: 18, idleHeat: 1.1f);
		parts["torso_tri_fleet"] = CatalogBuilders.Torso("torso_tri_fleet", "Fleet Intermediate", "trinova", m,
			55, housing: 2, structureHp: 75, shoulders: 2, backs: 1, heatCap: 20, idleHeat: 1.15f,
			visualKind: "torso_fleet");

		parts["torso_ouro_thin"] = CatalogBuilders.Torso("torso_ouro_thin", "Thinspine Chassis", "ourotech", m,
			32, housing: 2, structureHp: 55, shoulders: 2, backs: 1,
			scale: new Vector3(0.92f, 1.05f, 0.95f), heatCap: 12, idleHeat: 0.7f,
			visualKind: "torso_ouro_thin");
		parts["torso_ouro_caliper"] = CatalogBuilders.Torso("torso_ouro_caliper", "Caliper Frame", "ourotech", m,
			38, housing: 2, structureHp: 60, shoulders: 2, backs: 0,
			scale: new Vector3(0.9f, 1.08f, 0.92f), heatCap: 14, idleHeat: 0.65f);
		parts["torso_ouro_apex"] = CatalogBuilders.Torso("torso_ouro_apex", "Apex Gimbal Cage", "ourotech", m,
			42, housing: 3, structureHp: 70, shoulders: 2, backs: 1, heatCap: 16, idleHeat: 0.85f);

		parts["torso_lum_shell"] = CatalogBuilders.Torso("torso_lum_shell", "Vault Shell", "lumina", m,
			35, housing: 2, structureHp: 55, shoulders: 0, backs: 1,
			scale: new Vector3(0.95f, 1.05f, 0.95f), heatCap: 10, idleHeat: 0.8f);
		parts["torso_lum_prism"] = CatalogBuilders.Torso("torso_lum_prism", "Prism Carapace", "lumina", m,
			30, housing: 2, structureHp: 58, shoulders: 1, backs: 1,
			scale: new Vector3(0.93f, 1.1f, 0.93f), heatCap: 8, idleHeat: 0.95f);
		parts["torso_lum_oracle"] = CatalogBuilders.Torso("torso_lum_oracle", "Oracle Hull", "lumina", m,
			40, housing: 3, structureHp: 68, shoulders: 1, backs: 1, heatCap: 12, idleHeat: 1.05f,
			visualKind: "torso_lum_oracle");

		parts["torso_ash_ashrib"] = CatalogBuilders.Torso("torso_ash_ashrib", "Ashrib Cage", "ashwhisk", m,
			36, housing: 2, structureHp: 58, shoulders: 2, backs: 1,
			scale: new Vector3(0.9f, 1.08f, 0.92f), heatCap: 14, idleHeat: 0.75f,
			visualKind: "torso_ash_ashrib");
		parts["torso_vel_ruff"] = CatalogBuilders.Torso("torso_vel_ruff", "Ruff Plate", "velhound", m,
			72, housing: 3, structureHp: 88, shoulders: 2, backs: 1,
			scale: new Vector3(1.18f, 1.08f, 1.12f), heatCap: 28, idleHeat: 1.5f,
			visualKind: "torso_vel_ruff");
	}

	private static void RegisterHeads(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		parts["head_tri_optic"] = CatalogBuilders.Head("head_tri_optic", "Scout Dome", "trinova", m,
			12, turn: 8f, visionRange: 45f, visionAngle: 110f, close: 0.55f,
			scanRange: 70f, scanRes: 0.35f, idleHeat: 0.4f);
		parts["head_tri_logistics"] = CatalogBuilders.Head("head_tri_logistics", "Logistics Periscope", "trinova", m,
			14, turn: 6f, visionRange: 40f, visionAngle: 130f, close: 0.5f,
			scanRange: 80f, scanRes: 0.4f, idleHeat: 0.45f, speed: 0.3f);
		parts["head_tri_convoy"] = CatalogBuilders.Head("head_tri_convoy", "Convoy Spotter", "trinova", m,
			16, turn: 5f, visionRange: 50f, visionAngle: 100f, close: 0.48f,
			scanRange: 95f, scanRes: 0.42f, idleHeat: 0.5f);

		parts["head_brin_helm"] = CatalogBuilders.Head("head_brin_helm", "Slab Helm", "brimforge", m,
			28, turn: -5f, visionRange: 32f, visionAngle: 95f, close: 0.4f,
			scanRange: 50f, scanRes: 0.25f, idleHeat: 0.6f, scale: new Vector3(1.15f, 1.05f, 1.1f));
		parts["head_brin_visor"] = CatalogBuilders.Head("head_brin_visor", "Blast Visor", "brimforge", m,
			34, turn: -8f, visionRange: 28f, visionAngle: 85f, close: 0.35f,
			scanRange: 45f, scanRes: 0.22f, idleHeat: 0.7f, scale: new Vector3(1.2f, 1.1f, 1.15f));
		parts["head_brin_warface"] = CatalogBuilders.Head("head_brin_warface", "Warface Casque", "brimforge", m,
			40, turn: -10f, visionRange: 30f, visionAngle: 90f, close: 0.38f,
			scanRange: 55f, scanRes: 0.28f, idleHeat: 0.75f, scale: new Vector3(1.25f, 1.12f, 1.18f));

		parts["head_ouro_scope"] = CatalogBuilders.Head("head_ouro_scope", "Glass Eye", "ourotech", m,
			8, turn: 12f, visionRange: 58f, visionAngle: 75f, close: 0.9f,
			scanRange: 90f, scanRes: 0.55f, idleHeat: 0.5f, fireRateBonus: 0.08f);
		parts["head_ouro_reticle"] = CatalogBuilders.Head("head_ouro_reticle", "Reticle Crown", "ourotech", m,
			7, turn: 14f, visionRange: 62f, visionAngle: 70f, close: 0.95f,
			scanRange: 100f, scanRes: 0.62f, idleHeat: 0.55f, fireRateBonus: 0.12f);
		parts["head_ouro_whisper"] = CatalogBuilders.Head("head_ouro_whisper", "Whisper Lidar", "ourotech", m,
			6, turn: 10f, visionRange: 52f, visionAngle: 95f, close: 0.85f,
			scanRange: 110f, scanRes: 0.7f, idleHeat: 0.48f, fireRateBonus: 0.05f);
		parts["head_ouro_duelist"] = CatalogBuilders.Head("head_ouro_duelist", "Duelist Monocle", "ourotech", m,
			9, turn: 16f, visionRange: 48f, visionAngle: 80f, close: 1.0f,
			scanRange: 75f, scanRes: 0.5f, idleHeat: 0.42f, fireRateBonus: 0.1f);

		parts["head_lum_mask"] = CatalogBuilders.Head("head_lum_mask", "Vault Mask", "lumina", m,
			10, turn: 6f, visionRange: 42f, visionAngle: 120f, close: 0.65f,
			scanRange: 85f, scanRes: 0.45f, idleHeat: 0.45f, speed: 0.5f);
		parts["head_lum_oracle"] = CatalogBuilders.Head("head_lum_oracle", "Oracle Halo", "lumina", m,
			9, turn: 8f, visionRange: 55f, visionAngle: 140f, close: 0.7f,
			scanRange: 120f, scanRes: 0.58f, idleHeat: 0.65f, speed: 0.4f);
		parts["head_lum_ghost"] = CatalogBuilders.Head("head_lum_ghost", "Ghost Iris", "lumina", m,
			8, turn: 9f, visionRange: 46f, visionAngle: 115f, close: 0.72f,
			scanRange: 95f, scanRes: 0.52f, idleHeat: 0.6f, fireRateBonus: 0.06f);

		parts["head_ash_whisker"] = CatalogBuilders.Head("head_ash_whisker", "Whisker Array", "ashwhisk", m,
			7, turn: 13f, visionRange: 56f, visionAngle: 85f, close: 0.88f,
			scanRange: 92f, scanRes: 0.58f, idleHeat: 0.48f, fireRateBonus: 0.07f,
			scale: new Vector3(0.95f, 1.05f, 0.95f), visualKind: "head_ash_whisker");
		parts["head_vel_muzzle"] = CatalogBuilders.Head("head_vel_muzzle", "Muzzle Lidar", "velhound", m,
			22, turn: -2f, visionRange: 38f, visionAngle: 100f, close: 0.5f,
			scanRange: 65f, scanRes: 0.32f, idleHeat: 0.55f,
			scale: new Vector3(1.12f, 1.05f, 1.15f));
	}
}
