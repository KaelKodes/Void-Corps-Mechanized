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
			["ashwhisk"] = CatalogBuilders.MakeManufacturer("ashwhisk", "Ashwhisk Corporation", new Color(0.62f, 0.68f, 0.72f),
				"Private chassis house. Precision posture, clutch mobility, predatory silhouette. Body kits only — guns stay Big Four.",
				"Feline chassis / clutch mobility"),
			["velhound"] = CatalogBuilders.MakeManufacturer("velhound", "Velhound", new Color(0.55f, 0.42f, 0.28f),
				"Pursuit chassis house. Broader stance, charge commitment, pack-proud pressure. Body kits only — guns stay Big Four.",
				"Canine chassis / pursuit pressure"),
		};

		var parts = new Dictionary<string, PartData>();
		CatalogFrames.Register(parts, manufacturers);
		CatalogWeapons.Register(parts, manufacturers);
		CatalogCoresSystems.Register(parts, manufacturers);
		CatalogMounts.Register(parts, manufacturers);
		CatalogTiers.Apply(parts);
		CatalogDurability.Apply(parts);
		CatalogPower.Apply(parts);
		CatalogWeight.Apply(parts);

		Manufacturers = manufacturers;
		Parts = parts;
	}

	/// <summary>
	/// Big Four only — convention, research, sector contracts, and reputation seeding.
	/// Ashwhisk / Velhound exist in <see cref="Manufacturers"/> for catalog tinting and skirmish loaners.
	/// </summary>
	public static readonly string[] CampaignManufacturerIds =
		["brimforge", "ourotech", "trinova", "lumina"];

	public static bool IsCampaignManufacturer(string id) =>
		!string.IsNullOrEmpty(id) && System.Array.IndexOf(CampaignManufacturerIds, id) >= 0;

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
		return SanitizeLoadout(new LoadoutData
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

	/// <summary>
	/// Cadet loaner — strong, once-near-BIS training chassis. Returned after graduation.
	/// </summary>
	public static LoadoutData CreateCadetLoanerLoadout()
	{
		EnsureBuilt();
		return SanitizeLoadout(new LoadoutData
		{
			LegsId = "legs_brin_fortress",
			TorsoId = "torso_brin_anvil",
			HeadId = "head_brin_warface",
			PowerCoreId = "core_brin_citadel",
			WeaponLId = "wep_brin_maul",
			WeaponRId = "wep_ouro_stitch",
			ShoulderLId = "shoulder_brin_barrage",
			ShoulderRId = "shoulder_ouro_tracker",
			BackpackId = "backpack_tri_mend",
			SystemsId = "systems_brin_vent"
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

	/// <summary>
	/// Mount / housing / head repair only. Does not strip parts for power —
	/// garage installs hard-block overbudget kits instead.
	/// </summary>
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

	/// <summary>
	/// Mount sanitize plus optional-module strip for authored / migrated loadouts
	/// that must remain deployable even if catalogue numbers shift.
	/// </summary>
	public static LoadoutData SanitizeLoadout(LoadoutData loadout)
	{
		SanitizeMounts(loadout);
		StripOverbudgetOptionalParts(loadout);
		return loadout;
	}

	/// <summary>Total standby PowerRequirement for a loadout (empty / missing = 0).</summary>
	public static float SumPowerRequirements(LoadoutData loadout)
	{
		EnsureBuilt();
		float sum = 0f;
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var part = GetPart(loadout.GetPartId(slot));
			if (part == null || part.VisualKind == "empty")
				continue;
			sum += Mathf.Max(0f, part.PowerRequirement);
		}

		return sum;
	}

	public static float GetCoreCapacity(LoadoutData loadout)
	{
		EnsureBuilt();
		var core = GetPart(loadout.PowerCoreId);
		if (core == null || core.Slot != PartSlot.PowerCore)
			return 0f;
		return Mathf.Max(0f, core.PowerCapacity);
	}

	public static bool IsPowerLegal(LoadoutData loadout) =>
		SumPowerRequirements(loadout) <= GetCoreCapacity(loadout) + 0.01f;

	/// <summary>Total mass of installed non-empty parts (empty mounts = 0).</summary>
	public static float SumWeight(LoadoutData loadout)
	{
		EnsureBuilt();
		float sum = 0f;
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var part = GetPart(loadout.GetPartId(slot));
			if (part == null || part.VisualKind == "empty")
				continue;
			sum += Mathf.Max(0f, part.Weight);
		}

		return sum;
	}

	public static float GetLoadRating(LoadoutData loadout)
	{
		EnsureBuilt();
		var legs = GetPart(loadout.LegsId);
		if (legs == null || legs.Slot != PartSlot.Legs)
			return 0f;
		return Mathf.Max(0f, legs.LoadRating);
	}

	public static bool IsOverLoadRating(LoadoutData loadout) =>
		SumWeight(loadout) > GetLoadRating(loadout) + 0.01f;

	/// <summary>
	/// True if replacing <paramref name="slot"/> with <paramref name="partId"/> stays within core capacity.
	/// Mount-unavailable slots are cleared for the trial; optional modules are never silently stripped.
	/// </summary>
	public static bool CanEquipPart(LoadoutData loadout, PartSlot slot, string partId)
	{
		EnsureBuilt();
		var trial = loadout.Clone();
		trial.SetPartId(slot, partId);
		if (slot is PartSlot.Torso or PartSlot.PowerCore)
			SanitizeMounts(trial);
		return IsPowerLegal(trial);
	}

	/// <summary>
	/// Drop optional modules (never legs/torso/head/core/weapons) until the kit is power-legal
	/// or nothing remains to drop. Used only for authored/migrated loadouts.
	/// </summary>
	private static void StripOverbudgetOptionalParts(LoadoutData loadout)
	{
		if (IsPowerLegal(loadout))
			return;

		var stripOrder = new[]
		{
			PartSlot.ShoulderR,
			PartSlot.ShoulderL,
			PartSlot.Backpack,
			PartSlot.Systems
		};

		foreach (var slot in stripOrder)
		{
			if (IsPowerLegal(loadout))
				return;
			if (!IsMountAvailable(loadout, slot))
				continue;

			var current = GetPart(loadout.GetPartId(slot));
			if (current == null || current.VisualKind == "empty")
				continue;

			loadout.SetPartId(slot, slot switch
			{
				PartSlot.Backpack => "backpack_none",
				PartSlot.Systems => "systems_none",
				_ => "shoulder_none"
			});
		}
	}

	public static LoadoutData CreateEnemyLoadout(int variant = 0)
	{
		EnsureBuilt();
		return CreateSkirmishPremade(variant).Loadout;
	}

	/// <summary>Skirmish chassis kits (same pool used for generic enemy MAPs).</summary>
	public static SkirmishPremadeDef[] SkirmishPremades
	{
		get
		{
			EnsureBuilt();
			return
			[
				CreateSkirmishPremade(0),
				CreateSkirmishPremade(1),
				CreateSkirmishPremade(2),
				CreateSkirmishPremade(3),
				CreateSkirmishPremade(4),
				CreateSkirmishPremade(5)
			];
		}
	}

	public static SkirmishPremadeDef CreateSkirmishPremade(int variant)
	{
		EnsureBuilt();
		return (variant % 6) switch
		{
			1 => new SkirmishPremadeDef(
				1,
				"OuroTech Razorhex",
				"OuroTech",
				"Gimbaled hex legs, stitch + scalpel, needle pods. Precision skirmisher.",
				SanitizeLoadout(new LoadoutData
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
				})),
			2 => new SkirmishPremadeDef(
				2,
				"Brimforge Fortress",
				"Brimforge",
				"Tracked fortress hull, maul + chain, barrage pods. Slow hammer.",
				SanitizeLoadout(new LoadoutData
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
				})),
			3 => new SkirmishPremadeDef(
				3,
				"Lumina Phase Oracle",
				"Lumina Vaultworks",
				"Phasehex crawler, volt + oracle lance, veil shroud. Experimental kit.",
				SanitizeLoadout(new LoadoutData
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
				})),
			4 => new SkirmishPremadeDef(
				4,
				"Ashwhisk Whisperframe",
				"Ashwhisk Corporation",
				"Coilstrider hull, balance-fin, licensed Stitch carbine + Bulwark plate. One gun, one guard — coil and re-orient.",
				SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_ash_coilstriders",
					TorsoId = "torso_ash_ashrib",
					HeadId = "head_ash_whisker",
					PowerCoreId = "core_ouro_needle",
					WeaponLId = "wep_ouro_stitch",
					WeaponRId = "wep_tri_bulwark",
					ShoulderLId = "shoulder_ouro_needle",
					ShoulderRId = "shoulder_ouro_tracker",
					BackpackId = "backpack_ash_stabilizer",
					SystemsId = "systems_ouro_radiator"
				})),
			5 => new SkirmishPremadeDef(
				5,
				"Velhound Yardbreaker",
				"Velhound",
				"Bracehound hull with licensed Brimforge maul + chain. Brace, surge, hold the line.",
				SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_vel_bracehounds",
					TorsoId = "torso_vel_ruff",
					HeadId = "head_vel_muzzle",
					PowerCoreId = "core_brin_citadel",
					WeaponLId = "wep_brin_maul",
					WeaponRId = "wep_brin_chain",
					ShoulderLId = "shoulder_brin_barrage",
					ShoulderRId = "shoulder_brin_deny",
					BackpackId = "backpack_brin_citadel",
					SystemsId = "systems_brin_slag"
				})),
			_ => new SkirmishPremadeDef(
				0,
				"Trinova Fleet Courier",
				"Trinova",
				"Courier striders, patrol + hybrid guns, fleet shoulders + mend. Balanced wing kit.",
				SanitizeLoadout(new LoadoutData
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
				}))
		};
	}
}

/// <summary>Named premade chassis offered in skirmish mech select (and enemy MAP variants).</summary>
public readonly struct SkirmishPremadeDef
{
	public SkirmishPremadeDef(int variant, string displayName, string manufacturer, string blurb, LoadoutData loadout)
	{
		Variant = variant;
		DisplayName = displayName;
		Manufacturer = manufacturer;
		Blurb = blurb;
		Loadout = loadout;
	}

	public int Variant { get; }
	public string DisplayName { get; }
	public string Manufacturer { get; }
	public string Blurb { get; }
	public LoadoutData Loadout { get; }
}
