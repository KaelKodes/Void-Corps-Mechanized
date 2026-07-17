using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// In-code catalog of manufacturers and parts. Catalogue content lives under scripts/data/catalog/.
/// </summary>
public static class GameCatalog
{
	public static IReadOnlyDictionary<string, ManufacturerData> Manufacturers { get; private set; } = null!;
	public static IReadOnlyDictionary<string, PartData> Parts { get; private set; } = null!;

	private static bool _built;

	public static void EnsureBuilt()
	{
		if (_built)
			return;
		_built = true;

		var manufacturers = new Dictionary<string, ManufacturerData>
		{
			["brimforge"] = CatalogBuilders.MakeManufacturer("brimforge", "Brimforge", new Color(0.72f, 0.38f, 0.18f),
				"Heavy industry house. Forge-slab armor, kinetic iron, and deny-the-asset doctrine. Ships kit to any corp that can pay.",
				"Heavy armor / kinetic"),
			["ourotech"] = CatalogBuilders.MakeManufacturer("ourotech", "OuroTech", new Color(0.25f, 0.55f, 0.78f),
				"Precision license-house. Servo gimbals, seekers, and surgical fire control — not a fighting corp.",
				"Precision / targeting"),
			["trinova"] = CatalogBuilders.MakeManufacturer("trinova", "Trinova", new Color(0.35f, 0.72f, 0.42f),
				"Fleet-logistics manufacturer gone surface-side. Balanced frames, mend beacons, claim utility.",
				"Utility / hybrid"),
			["lumina"] = CatalogBuilders.MakeManufacturer("lumina", "Lumina Vaultworks", new Color(0.78f, 0.62f, 0.95f),
				"Vault-lab experimental wing. Energy arcs, shroud drives, and classified crawler kits.",
				"Energy / experimental"),
		};

		var parts = new Dictionary<string, PartData>();
		CatalogFrames.Register(parts, manufacturers);
		CatalogWeapons.Register(parts, manufacturers);
		CatalogCoresSystems.Register(parts, manufacturers);
		CatalogMounts.Register(parts, manufacturers);

		Manufacturers = manufacturers;
		Parts = parts;
	}

	public static ManufacturerData GetManufacturer(string id)
	{
		EnsureBuilt();
		return Manufacturers.TryGetValue(id, out var m) ? m : Manufacturers["trinova"];
	}

	public static PartData? GetPart(string id)
	{
		EnsureBuilt();
		return Parts.TryGetValue(id, out var p) ? p : null;
	}

	public static IEnumerable<PartData> GetPartsForSlot(PartSlot slot)
	{
		EnsureBuilt();
		if (slot is PartSlot.WeaponL or PartSlot.WeaponR)
			return Parts.Values.Where(p => p.Slot is PartSlot.WeaponL or PartSlot.WeaponR);
		if (slot is PartSlot.ShoulderL or PartSlot.ShoulderR)
			return Parts.Values.Where(p => p.Slot is PartSlot.ShoulderL or PartSlot.ShoulderR);
		return Parts.Values.Where(p => p.Slot == slot);
	}

	public static IEnumerable<PartData> GetLegalPowerCores(LoadoutData loadout)
	{
		EnsureBuilt();
		var housing = GetPart(loadout.TorsoId)?.PowerCoreHousing ?? 1;
		return Parts.Values.Where(p => p.Slot == PartSlot.PowerCore && p.PowerCoreClass <= housing);
	}

	public static LoadoutData CreateStarterLoadout()
	{
		EnsureBuilt();
		return SanitizeMounts(new LoadoutData
		{
			LegsId = "legs_tri_biped",
			TorsoId = "torso_tri_fleet",
			HeadId = "head_tri_optic",
			PowerCoreId = "core_tri_cell",
			WeaponLId = "wep_brin_slug",
			WeaponRId = "wep_ouro_rifle",
			ShoulderLId = "shoulder_tri_patrol",
			ShoulderRId = "shoulder_none",
			BackpackId = "backpack_tri_mend",
			SystemsId = "systems_tri_coolant"
		});
	}

	public static bool IsMountAvailable(LoadoutData loadout, PartSlot slot)
	{
		EnsureBuilt();
		if (slot is not (PartSlot.ShoulderL or PartSlot.ShoulderR or PartSlot.Backpack))
			return true;
		var torso = GetPart(loadout.TorsoId);
		return torso?.ProvidesMount(slot) ?? false;
	}

	public static LoadoutData SanitizeMounts(LoadoutData loadout)
	{
		EnsureBuilt();
		var torso = GetPart(loadout.TorsoId);
		if (torso != null)
		{
			if (!torso.ProvidesMount(PartSlot.ShoulderL))
				loadout.ShoulderLId = "";
			if (!torso.ProvidesMount(PartSlot.ShoulderR))
				loadout.ShoulderRId = "";
			if (!torso.ProvidesMount(PartSlot.Backpack))
				loadout.BackpackId = "";
		}

		var housing = torso?.PowerCoreHousing ?? 1;
		var core = GetPart(loadout.PowerCoreId);
		if (core == null || core.Slot != PartSlot.PowerCore || core.PowerCoreClass > housing)
		{
			loadout.PowerCoreId = GetLegalPowerCores(loadout).OrderBy(c => c.PowerCoreClass).FirstOrDefault()?.Id
				?? "core_tri_cell";
		}

		if (string.IsNullOrEmpty(loadout.HeadId) || GetPart(loadout.HeadId) == null)
			loadout.HeadId = "head_tri_optic";

		return loadout;
	}

	public static LoadoutData CreateEnemyLoadout(int variant = 0)
	{
		EnsureBuilt();
		return (variant % 4) switch
		{
			1 => SanitizeMounts(new LoadoutData
			{
				LegsId = "legs_ouro_razorhex",
				TorsoId = "torso_ouro_thin",
				HeadId = "head_ouro_reticle",
				PowerCoreId = "core_ouro_needle",
				WeaponLId = "wep_ouro_stitch",
				WeaponRId = "wep_ouro_scalpel",
				ShoulderLId = "shoulder_ouro_needle",
				ShoulderRId = "shoulder_ouro_tracker",
				BackpackId = "backpack_ouro_stitch",
				SystemsId = "systems_ouro_radiator"
			}),
			2 => SanitizeMounts(new LoadoutData
			{
				LegsId = "legs_brin_fortress",
				TorsoId = "torso_brin_anvil",
				HeadId = "head_brin_warface",
				PowerCoreId = "core_brin_citadel",
				WeaponLId = "wep_brin_maul",
				WeaponRId = "wep_brin_chain",
				ShoulderLId = "shoulder_brin_barrage",
				ShoulderRId = "shoulder_brin_deny",
				BackpackId = "backpack_brin_citadel",
				SystemsId = "systems_brin_slag"
			}),
			3 => SanitizeMounts(new LoadoutData
			{
				LegsId = "legs_lum_phasehex",
				TorsoId = "torso_lum_oracle",
				HeadId = "head_lum_oracle",
				PowerCoreId = "core_lum_ghost",
				WeaponLId = "wep_lum_volt",
				WeaponRId = "wep_lum_oracle",
				ShoulderLId = "shoulder_lum_ghost",
				ShoulderRId = "",
				BackpackId = "backpack_lum_veil",
				SystemsId = "systems_lum_phase"
			}),
			_ => SanitizeMounts(new LoadoutData
			{
				LegsId = "legs_tri_courier",
				TorsoId = "torso_tri_fleet",
				HeadId = "head_tri_convoy",
				PowerCoreId = "core_tri_fleet",
				WeaponLId = "wep_tri_patrol",
				WeaponRId = "wep_tri_hybrid",
				ShoulderLId = "shoulder_tri_fleet",
				ShoulderRId = "shoulder_tri_patrol",
				BackpackId = "backpack_tri_mend",
				SystemsId = "systems_tri_coolant"
			})
		};
	}
}
