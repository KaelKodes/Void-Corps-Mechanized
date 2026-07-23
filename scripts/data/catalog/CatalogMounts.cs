using System.Collections.Generic;

namespace Mechanize;

/// <summary>Shoulder pods and backpack modules.</summary>
public static class CatalogMounts
{
	public static void Register(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		RegisterShoulders(parts, m);
		RegisterBackpacks(parts, m);
	}

	private static void RegisterShoulders(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		parts["shoulder_none"] = CatalogBuilders.Empty("shoulder_none", "Empty Shoulder", PartSlot.ShoulderL);

		// Brimforge — heavy missile / deny
		parts["shoulder_brin_pods"] = CatalogBuilders.AbilityPart("shoulder_brin_pods", "Havoc Missile Pods", "brimforge", m,
			PartSlot.ShoulderL, "missile", armor: 5, AbilityId.MissileSalvo, cd: 7f, power: 4f,
			heatBurst: 22f, powerLoad: 30f, damage: 14, range: 45f, proj: 28f);
		parts["shoulder_brin_barrage"] = CatalogBuilders.AbilityPart("shoulder_brin_barrage", "Barrage Nest", "brimforge", m,
			PartSlot.ShoulderL, "missile", armor: 8, AbilityId.MissileSalvo, cd: 9f, power: 5f,
			heatBurst: 28f, powerLoad: 36f, damage: 18, range: 40f, proj: 26f);
		parts["shoulder_brin_deny"] = CatalogBuilders.AbilityPart("shoulder_brin_deny", "Deny Swarm", "brimforge", m,
			PartSlot.ShoulderR, "missile", armor: 6, AbilityId.MissileSalvo, cd: 8f, power: 4f,
			heatBurst: 24f, powerLoad: 32f, damage: 12, range: 38f, proj: 30f);
		parts["shoulder_brin_siege"] = CatalogBuilders.AbilityPart("shoulder_brin_siege", "Siege Tubes", "brimforge", m,
			PartSlot.ShoulderL, "missile", armor: 10, AbilityId.MissileSalvo, cd: 11f, power: 6f,
			heatBurst: 32f, powerLoad: 40f, damage: 22, range: 50f, proj: 24f);

		// OuroTech — seekers / precision (sensor-lock fire; contact vs vision is per kit)
		parts["shoulder_ouro_tracker"] = CatalogBuilders.AbilityPart("shoulder_ouro_tracker", "Seeker Rack", "ourotech", m,
			PartSlot.ShoulderR, "missile", armor: 0, AbilityId.MissileSalvo, cd: 5.5f, power: 3f,
			heatBurst: 16f, powerLoad: 24f, damage: 10, range: 55f, proj: 36f,
			missileGuidance: MissileGuidanceMode.SensorVision);
		parts["shoulder_ouro_needle"] = CatalogBuilders.AbilityPart("shoulder_ouro_needle", "Needle Swarm", "ourotech", m,
			PartSlot.ShoulderL, "missile", armor: 2, AbilityId.MissileSalvo, cd: 4.5f, power: 2.5f,
			heatBurst: 12f, powerLoad: 20f, damage: 7, range: 60f, proj: 42f,
			missileGuidance: MissileGuidanceMode.SensorVision);
		parts["shoulder_ouro_caliper"] = CatalogBuilders.AbilityPart("shoulder_ouro_caliper", "Caliper Pods", "ourotech", m,
			PartSlot.ShoulderR, "missile", armor: 1, AbilityId.MissileSalvo, cd: 6.5f, power: 3.5f,
			heatBurst: 14f, powerLoad: 22f, damage: 12, range: 65f, proj: 40f,
			missileGuidance: MissileGuidanceMode.SensorContact);
		parts["shoulder_ouro_whisper"] = CatalogBuilders.AbilityPart("shoulder_ouro_whisper", "Whisper Salvo", "ourotech", m,
			PartSlot.ShoulderL, "missile", armor: 0, AbilityId.MissileSalvo, cd: 5f, power: 2.8f,
			heatBurst: 11f, powerLoad: 18f, damage: 8, range: 58f, proj: 38f,
			missileGuidance: MissileGuidanceMode.SensorVision);

		// Trinova — logistics / balanced
		parts["shoulder_tri_fleet"] = CatalogBuilders.AbilityPart("shoulder_tri_fleet", "Fleet Rockets", "trinova", m,
			PartSlot.ShoulderL, "missile", armor: 4, AbilityId.MissileSalvo, cd: 6f, power: 3.2f,
			heatBurst: 15f, powerLoad: 22f, damage: 11, range: 48f, proj: 32f);
		parts["shoulder_tri_convoy"] = CatalogBuilders.AbilityPart("shoulder_tri_convoy", "Convoy Launchers", "trinova", m,
			PartSlot.ShoulderR, "missile", armor: 5, AbilityId.MissileSalvo, cd: 7.5f, power: 3.5f,
			heatBurst: 17f, powerLoad: 25f, damage: 13, range: 44f, proj: 30f);
		parts["shoulder_tri_patrol"] = CatalogBuilders.AbilityPart("shoulder_tri_patrol", "Patrol Tubes", "trinova", m,
			PartSlot.ShoulderL, "missile", armor: 3, AbilityId.MissileSalvo, cd: 5.8f, power: 3f,
			heatBurst: 14f, powerLoad: 21f, damage: 9, range: 50f, proj: 34f);

		// Lumina — exotic / energy-tinged missiles
		parts["shoulder_lum_arc"] = CatalogBuilders.AbilityPart("shoulder_lum_arc", "Arc Microtorps", "lumina", m,
			PartSlot.ShoulderL, "missile", armor: 2, AbilityId.MissileSalvo, cd: 6.2f, power: 3.5f,
			heatBurst: 20f, powerLoad: 28f, damage: 11, range: 52f, proj: 35f);
		parts["shoulder_lum_ghost"] = CatalogBuilders.AbilityPart("shoulder_lum_ghost", "Ghost Swarm", "lumina", m,
			PartSlot.ShoulderR, "missile", armor: 1, AbilityId.MissileSalvo, cd: 5.2f, power: 3f,
			heatBurst: 18f, powerLoad: 30f, damage: 9, range: 56f, proj: 38f);
		parts["shoulder_lum_prism"] = CatalogBuilders.AbilityPart("shoulder_lum_prism", "Prism Volley", "lumina", m,
			PartSlot.ShoulderL, "missile", armor: 3, AbilityId.MissileSalvo, cd: 8f, power: 4f,
			heatBurst: 26f, powerLoad: 34f, damage: 15, range: 48f, proj: 33f);
	}

	private static void RegisterBackpacks(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		parts["backpack_none"] = CatalogBuilders.Empty("backpack_none", "Empty Backpack", PartSlot.Backpack);

		// Trinova mend / utility
		parts["backpack_tri_mend"] = CatalogBuilders.AbilityPart("backpack_tri_mend", "Mend Beacon", "trinova", m,
			PartSlot.Backpack, "backpack", armor: 10, AbilityId.MendPulse, cd: 22f, power: 14f,
			heatBurst: 16f, powerLoad: 22f, radius: 5.1f, duration: 8f, family: WeaponFamily.Support);
		parts["backpack_tri_field"] = CatalogBuilders.AbilityPart("backpack_tri_field", "Field Mend Node", "trinova", m,
			PartSlot.Backpack, "backpack", armor: 8, AbilityId.MendPulse, cd: 18f, power: 11f,
			heatBurst: 14f, powerLoad: 20f, radius: 4.2f, duration: 7f, family: WeaponFamily.Support);
		parts["backpack_tri_convoy"] = CatalogBuilders.AbilityPart("backpack_tri_convoy", "Convoy Restorer", "trinova", m,
			PartSlot.Backpack, "backpack", armor: 12, AbilityId.MendPulse, cd: 26f, power: 18f,
			heatBurst: 20f, powerLoad: 26f, radius: 6.0f, duration: 9f, family: WeaponFamily.Support);
		parts["backpack_tri_aegis"] = new PartData
		{
			Id = "backpack_tri_aegis", DisplayName = "Aegis Cap", ManufacturerId = "trinova",
			Slot = PartSlot.Backpack, Armor = 30, Tint = m["trinova"].AccentColor, VisualKind = "shield",
			StructureHp = 54f, HeatCapBonus = 10f
		};
		parts["backpack_tri_hauler"] = new PartData
		{
			Id = "backpack_tri_hauler", DisplayName = "Hauler Ballast", ManufacturerId = "trinova",
			Slot = PartSlot.Backpack, Armor = 22, MaxSpeed = -0.6f, Tint = m["trinova"].AccentColor,
			VisualKind = "shield", StructureHp = 62f, HeatCapBonus = 8f
		};

		// OuroTech repair / pulse
		parts["backpack_ouro_pulse"] = CatalogBuilders.AbilityPart("backpack_ouro_pulse", "Pulse Repair", "ourotech", m,
			PartSlot.Backpack, "backpack", armor: 6, AbilityId.PulseRepair, cd: 10f, power: 16f,
			heatBurst: 28f, powerLoad: 24f, radius: 12f, family: WeaponFamily.Support);
		parts["backpack_ouro_stitch"] = CatalogBuilders.AbilityPart("backpack_ouro_stitch", "Stitch Channel", "ourotech", m,
			PartSlot.Backpack, "backpack", armor: 4, AbilityId.PulseRepair, cd: 8f, power: 12f,
			heatBurst: 22f, powerLoad: 20f, radius: 10f, family: WeaponFamily.Support);
		parts["backpack_ouro_surge"] = CatalogBuilders.AbilityPart("backpack_ouro_surge", "Surge Mend", "ourotech", m,
			PartSlot.Backpack, "backpack", armor: 5, AbilityId.PulseRepair, cd: 12f, power: 22f,
			heatBurst: 34f, powerLoad: 30f, radius: 14f, family: WeaponFamily.Support);
		parts["backpack_ouro_cooler"] = new PartData
		{
			Id = "backpack_ouro_cooler", DisplayName = "Backplane Cooler", ManufacturerId = "ourotech",
			Slot = PartSlot.Backpack, Armor = 8, Tint = m["ourotech"].AccentColor, VisualKind = "heatsink",
			HeatDissipation = 6f, HeatCapBonus = 15f, MaxSpeed = 0.3f, TurnRateDegrees = 4f
		};
		parts["backpack_ouro_contact"] = CatalogBuilders.AbilityPart(
			"backpack_ouro_contact", "Contact Sweep", "ourotech", m,
			PartSlot.Backpack, "backpack", armor: 5, AbilityId.ContactReveal, cd: 14f, power: 1f,
			heatBurst: 10f, powerLoad: 16f, duration: 4f, family: WeaponFamily.Support);
		parts["backpack_ouro_contact"].ScannerRange = 12f;
		parts["backpack_ouro_contact"].ScannerResolution = 0.08f;
		parts["backpack_ouro_contact"].ScanPenetration = ScanPenetrationMode.Contact;
		parts["backpack_ouro_contact"].ScanBlipStyle = ScanBlipStyle.GroundRing;

		// Lumina shroud / experimental
		parts["backpack_lum_shroud"] = CatalogBuilders.AbilityPart("backpack_lum_shroud", "Shroud Drive", "lumina", m,
			PartSlot.Backpack, "shroud", armor: 5, AbilityId.Shroud, cd: 14f, power: 1f,
			heatBurst: 12f, powerLoad: 35f, duration: 4f, speed: 1f, family: WeaponFamily.Support);
		parts["backpack_lum_veil"] = CatalogBuilders.AbilityPart("backpack_lum_veil", "Veil Spool", "lumina", m,
			PartSlot.Backpack, "shroud", armor: 3, AbilityId.Shroud, cd: 11f, power: 1f,
			heatBurst: 10f, powerLoad: 30f, duration: 3.2f, speed: 1.2f, family: WeaponFamily.Support);
		parts["backpack_lum_phase"] = CatalogBuilders.AbilityPart("backpack_lum_phase", "Phase Shroud", "lumina", m,
			PartSlot.Backpack, "shroud", armor: 4, AbilityId.Shroud, cd: 16f, power: 1f,
			heatBurst: 15f, powerLoad: 40f, duration: 5f, speed: 0.8f, family: WeaponFamily.Support);
		parts["backpack_lum_capacitor"] = new PartData
		{
			Id = "backpack_lum_capacitor", DisplayName = "Arc Capacitor Pack", ManufacturerId = "lumina",
			Slot = PartSlot.Backpack, Armor = 6, Tint = m["lumina"].AccentColor, VisualKind = "backpack",
			HeatCapBonus = 20f, HeatDissipation = 4f, IdleHeatPerSec = 0.4f, MaxSpeed = 0.4f
		};

		// Brimforge armor / ballast
		parts["backpack_brin_plate"] = new PartData
		{
			Id = "backpack_brin_plate", DisplayName = "Ballast Plates", ManufacturerId = "brimforge",
			Slot = PartSlot.Backpack, Armor = 45, MaxSpeed = -1.5f, TurnRateDegrees = -15f,
			Tint = m["brimforge"].AccentColor, VisualKind = "shield", StructureHp = 70f, IdleHeatPerSec = 0.5f
		};
		parts["backpack_brin_citadel"] = new PartData
		{
			Id = "backpack_brin_citadel", DisplayName = "Citadel Shell", ManufacturerId = "brimforge",
			Slot = PartSlot.Backpack, Armor = 55, MaxSpeed = -2.2f, TurnRateDegrees = -20f,
			Tint = m["brimforge"].AccentColor, VisualKind = "shield", StructureHp = 85f, HeatCapBonus = 15f,
			IdleHeatPerSec = 0.7f
		};
		parts["backpack_brin_slag"] = new PartData
		{
			Id = "backpack_brin_slag", DisplayName = "Slag Reservoir", ManufacturerId = "brimforge",
			Slot = PartSlot.Backpack, Armor = 35, MaxSpeed = -1.0f, TurnRateDegrees = -10f,
			Tint = m["brimforge"].AccentColor, VisualKind = "shield", StructureHp = 70f, HeatCapBonus = 35f,
			HeatDissipation = 2f
		};
		parts["backpack_brin_mend"] = CatalogBuilders.AbilityPart("backpack_brin_mend", "Forge Patch Beacon", "brimforge", m,
			PartSlot.Backpack, "backpack", armor: 18, AbilityId.MendPulse, cd: 28f, power: 12f,
			heatBurst: 22f, powerLoad: 28f, radius: 4.5f, duration: 6f, family: WeaponFamily.Support);

		// Ashwhisk body-house stabilizer (fills the back slot — not a Big Four gun).
		parts["backpack_ash_stabilizer"] = new PartData
		{
			Id = "backpack_ash_stabilizer", DisplayName = "Balance Fin Stabilizer", ManufacturerId = "ashwhisk",
			Slot = PartSlot.Backpack, Armor = 14, MaxSpeed = 0.4f, TurnRateDegrees = 4f,
			Tint = m["ashwhisk"].AccentColor, VisualKind = "ash_stabilizer", StructureHp = 48f,
			IdleHeatPerSec = 0.35f
		};
	}
}
