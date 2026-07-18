using System.Collections.Generic;

namespace Mechanize;

/// <summary>Power cores and systems bay modules.</summary>
public static class CatalogCoresSystems
{
	public static void Register(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		RegisterCores(parts, m);
		RegisterSystems(parts, m);
	}

	private static void RegisterCores(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		parts["core_tri_cell"] = CatalogBuilders.Core("core_tri_cell", "Logistics Cell", "trinova", m,
			cls: 1, capacity: 100f, output: 16f, heatCap: 80f, idleHeat: 1.2f, dissipate: 4f);
		parts["core_tri_fleet"] = CatalogBuilders.Core("core_tri_fleet", "Fleet Pack Cell", "trinova", m,
			cls: 1, capacity: 110f, output: 18f, heatCap: 85f, idleHeat: 1.1f, dissipate: 5f);
		parts["core_tri_hauler"] = CatalogBuilders.Core("core_tri_hauler", "Hauler Dynamo", "trinova", m,
			cls: 2, capacity: 135f, output: 22f, heatCap: 95f, idleHeat: 1.4f, dissipate: 5.5f);

		parts["core_ouro_pulse"] = CatalogBuilders.Core("core_ouro_pulse", "Pulse Reactor", "ourotech", m,
			cls: 2, capacity: 130f, output: 26f, heatCap: 95f, idleHeat: 1.5f, dissipate: 6f);
		parts["core_ouro_needle"] = CatalogBuilders.Core("core_ouro_needle", "Needle Turbine", "ourotech", m,
			cls: 1, capacity: 95f, output: 22f, heatCap: 70f, idleHeat: 1.0f, dissipate: 7f);
		parts["core_ouro_apex"] = CatalogBuilders.Core("core_ouro_apex", "Apex Synchronizer", "ourotech", m,
			cls: 3, capacity: 160f, output: 32f, heatCap: 105f, idleHeat: 1.7f, dissipate: 8f);
		parts["core_ouro_whisper"] = CatalogBuilders.Core("core_ouro_whisper", "Whisper Spool", "ourotech", m,
			cls: 2, capacity: 125f, output: 28f, heatCap: 88f, idleHeat: 1.3f, dissipate: 7.5f);

		parts["core_brin_forge"] = CatalogBuilders.Core("core_brin_forge", "Forge Heart", "brimforge", m,
			cls: 3, capacity: 180f, output: 26f, heatCap: 120f, idleHeat: 2.2f, dissipate: 5f);
		parts["core_brin_slag"] = CatalogBuilders.Core("core_brin_slag", "Slag Furnace", "brimforge", m,
			cls: 2, capacity: 150f, output: 20f, heatCap: 130f, idleHeat: 2.5f, dissipate: 4f, armor: 18f);
		parts["core_brin_citadel"] = CatalogBuilders.Core("core_brin_citadel", "Citadel Reactor", "brimforge", m,
			cls: 3, capacity: 200f, output: 24f, heatCap: 140f, idleHeat: 2.8f, dissipate: 4.5f, armor: 22f);
		parts["core_brin_anvil"] = CatalogBuilders.Core("core_brin_anvil", "Anvil Core", "brimforge", m,
			cls: 2, capacity: 145f, output: 22f, heatCap: 115f, idleHeat: 2.0f, dissipate: 5.5f, armor: 16f);

		parts["core_lum_arc"] = CatalogBuilders.Core("core_lum_arc", "Arc Well", "lumina", m,
			cls: 2, capacity: 130f, output: 30f, heatCap: 88f, idleHeat: 1.8f, dissipate: 8f);
		parts["core_lum_prism"] = CatalogBuilders.Core("core_lum_prism", "Prism Heart", "lumina", m,
			cls: 1, capacity: 105f, output: 24f, heatCap: 75f, idleHeat: 1.6f, dissipate: 9f);
		parts["core_lum_oracle"] = CatalogBuilders.Core("core_lum_oracle", "Oracle Well", "lumina", m,
			cls: 3, capacity: 155f, output: 34f, heatCap: 92f, idleHeat: 2.0f, dissipate: 10f);
		parts["core_lum_ghost"] = CatalogBuilders.Core("core_lum_ghost", "Ghost Capacitor", "lumina", m,
			cls: 2, capacity: 120f, output: 32f, heatCap: 70f, idleHeat: 2.2f, dissipate: 11f);
	}

	private static void RegisterSystems(Dictionary<string, PartData> parts, Dictionary<string, ManufacturerData> m)
	{
		parts["systems_none"] = CatalogBuilders.Empty("systems_none", "Empty Systems Bay", PartSlot.Systems);

		parts["systems_ouro_heatsink"] = new PartData
		{
			Id = "systems_ouro_heatsink", DisplayName = "Cascade Heat Sink", ManufacturerId = "ourotech",
			Slot = PartSlot.Systems, TurnRateDegrees = 5f, Tint = m["ourotech"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.35f,
			HeatDissipation = 14f, HeatCapBonus = 20f
		};
		parts["systems_ouro_radiator"] = new PartData
		{
			Id = "systems_ouro_radiator", DisplayName = "Needle Radiator", ManufacturerId = "ourotech",
			Slot = PartSlot.Systems, TurnRateDegrees = 8f, Tint = m["ourotech"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.2f,
			HeatDissipation = 18f, HeatCapBonus = 12f, MaxSpeed = 0.4f
		};
		parts["systems_ouro_servo"] = new PartData
		{
			Id = "systems_ouro_servo", DisplayName = "Servo Accelerator", ManufacturerId = "ourotech",
			Slot = PartSlot.Systems, TurnRateDegrees = 18f, Tint = m["ourotech"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.45f,
			HeatDissipation = 8f, HeatCapBonus = 8f, MaxSpeed = 0.6f
		};

		parts["systems_brin_vent"] = new PartData
		{
			Id = "systems_brin_vent", DisplayName = "Forge Vents", ManufacturerId = "brimforge",
			Slot = PartSlot.Systems, Armor = 15, Tint = m["brimforge"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.15f,
			HeatDissipation = 8f, HeatCapBonus = 30f
		};
		parts["systems_brin_slag"] = new PartData
		{
			Id = "systems_brin_slag", DisplayName = "Slag Dumpers", ManufacturerId = "brimforge",
			Slot = PartSlot.Systems, Armor = 22, Tint = m["brimforge"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink,
			HeatDissipation = 6f, HeatCapBonus = 45f, MaxSpeed = -0.8f, TurnRateDegrees = -5f
		};
		parts["systems_brin_armor"] = new PartData
		{
			Id = "systems_brin_armor", DisplayName = "Reactive Plate Bay", ManufacturerId = "brimforge",
			Slot = PartSlot.Systems, Armor = 35, Tint = m["brimforge"].AccentColor, VisualKind = "shield",
			StructureHp = 65f, HeatCapBonus = 10f, HeatDissipation = 3f, MaxSpeed = -1.2f
		};

		parts["systems_tri_coolant"] = new PartData
		{
			Id = "systems_tri_coolant", DisplayName = "Logistics Coolant", ManufacturerId = "trinova",
			Slot = PartSlot.Systems, Tint = m["trinova"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.1f,
			HeatDissipation = 11f, HeatCapBonus = 22f, IdleHeatPerSec = -0.2f
		};
		parts["systems_tri_balancer"] = new PartData
		{
			Id = "systems_tri_balancer", DisplayName = "Load Balancer", ManufacturerId = "trinova",
			Slot = PartSlot.Systems, Tint = m["trinova"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink,
			HeatDissipation = 9f, HeatCapBonus = 18f, MaxSpeed = 0.5f, TurnRateDegrees = 6f
		};
		parts["systems_tri_field"] = new PartData
		{
			Id = "systems_tri_field", DisplayName = "Field Regulator", ManufacturerId = "trinova",
			Slot = PartSlot.Systems, Armor = 8, Tint = m["trinova"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.18f,
			HeatDissipation = 10f, HeatCapBonus = 16f, StructureHp = 48f
		};

		parts["systems_lum_cryo"] = new PartData
		{
			Id = "systems_lum_cryo", DisplayName = "Cryo Lattice", ManufacturerId = "lumina",
			Slot = PartSlot.Systems, Tint = m["lumina"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.25f,
			HeatDissipation = 16f, HeatCapBonus = 15f, IdleHeatPerSec = 0.3f
		};
		parts["systems_lum_phase"] = new PartData
		{
			Id = "systems_lum_phase", DisplayName = "Phase Sink", ManufacturerId = "lumina",
			Slot = PartSlot.Systems, Tint = m["lumina"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.3f,
			HeatDissipation = 20f, HeatCapBonus = 10f, MaxSpeed = 0.8f, IdleHeatPerSec = 0.5f
		};
		parts["systems_lum_oracle"] = new PartData
		{
			Id = "systems_lum_oracle", DisplayName = "Oracle Condenser", ManufacturerId = "lumina",
			Slot = PartSlot.Systems, Tint = m["lumina"].AccentColor, VisualKind = "heatsink",
			AbilityKind = AbilityKind.Passive, AbilityId = AbilityId.HeatSink, FireRateBonus = 0.22f,
			HeatDissipation = 15f, HeatCapBonus = 25f, TurnRateDegrees = 4f
		};
	}
}
