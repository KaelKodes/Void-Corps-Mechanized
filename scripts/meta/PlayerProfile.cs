using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Persistent player meta + active run kit (shared by campaign and skirmish for now).
/// Part ownership is quantity-based — one purchase = one equippable copy.
/// Implements <see cref="IPartInventory"/> so per-character bags can swap in later.
/// </summary>
public sealed class PlayerProfile : IPartInventory
{
	public const int StartingManufacturerRep = -10;
	public const int StartingAlignedRep = 20;
	public const int StartingLives = 2;
	public const string DefaultInventoryId = "pilot";

	/// <summary>Active bag id. Single-pilot for now; trade/characters will multiply this.</summary>
	public string InventoryId { get; set; } = DefaultInventoryId;

	public int Scrap { get; set; }
	public int LivesBank { get; set; } = StartingLives;
	public Dictionary<string, int> OwnedCounts { get; set; } = new();
	public LoadoutData Loadout { get; set; } = null!;
	public int SkirmishesPlayed { get; set; }
	public int SkirmishesWon { get; set; }
	public string MercCorpName { get; set; } = VoidCorpsIdentity.PlayerCorpCodename;
	public string AffiliatedManufacturerId { get; set; } = "";
	public Dictionary<string, int> ManufacturerReputation { get; set; } = new();

	/// <summary>Distinct owned part types (for UI totals).</summary>
	public int OwnedTypeCount => OwnedCounts.Count;

	/// <summary>Total owned copies across all parts.</summary>
	public int OwnedCopyCount => OwnedCounts.Values.Sum();

	public static PlayerProfile CreateNew()
	{
		GameCatalog.EnsureBuilt();
		var profile = new PlayerProfile
		{
			InventoryId = DefaultInventoryId,
			Scrap = 0,
			LivesBank = StartingLives,
			Loadout = GameCatalog.CreateStarterLoadout(),
			OwnedCounts = new Dictionary<string, int>(),
			MercCorpName = VoidCorpsIdentity.PlayerCorpCodename,
			AffiliatedManufacturerId = "",
			ManufacturerReputation = new Dictionary<string, int>(),
			SkirmishesPlayed = 0,
			SkirmishesWon = 0
		};
		profile.EnsureManufacturerState();
		profile.GrantLoadoutOwnership(profile.Loadout);
		return profile;
	}

	/// <summary>
	/// Wipe scrap/parts/loadout/affiliation/lives for a new campaign or total life loss.
	/// Soft meta (W/L, corp name, manufacturer rep) is kept.
	/// </summary>
	public void WipeRunInventory()
	{
		GameCatalog.EnsureBuilt();
		Scrap = 0;
		LivesBank = StartingLives;
		OwnedCounts.Clear();
		AffiliatedManufacturerId = "";
		Loadout = GameCatalog.CreateStarterLoadout();
		GrantLoadoutOwnership(Loadout);
	}

	public void EnsureManufacturerState()
	{
		foreach (var id in GameCatalog.Manufacturers.Keys)
		{
			if (!ManufacturerReputation.ContainsKey(id))
				ManufacturerReputation[id] = StartingManufacturerRep;
		}

		if (string.IsNullOrEmpty(MercCorpName))
			MercCorpName = VoidCorpsIdentity.PlayerCorpCodename;

		if (!string.IsNullOrEmpty(AffiliatedManufacturerId)
		    && ManufacturerReputation.ContainsKey(AffiliatedManufacturerId))
		{
			ManufacturerReputation[AffiliatedManufacturerId] =
				Mathf.Max(ManufacturerReputation[AffiliatedManufacturerId], StartingAlignedRep);
		}
	}

	public int ReputationWith(string manufacturerId)
	{
		EnsureManufacturerState();
		return ManufacturerReputation.GetValueOrDefault(manufacturerId);
	}

	public void SetAffiliation(string manufacturerId)
	{
		AffiliatedManufacturerId = manufacturerId;
		EnsureManufacturerState();
	}

	public void AddReputation(string manufacturerId, int amount)
	{
		if (string.IsNullOrEmpty(manufacturerId) || amount == 0)
			return;
		EnsureManufacturerState();
		ManufacturerReputation[manufacturerId] = ReputationWith(manufacturerId) + amount;
	}

	public void GrantLoadoutOwnership(LoadoutData loadout)
	{
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var id = loadout.GetPartId(slot);
			if (string.IsNullOrEmpty(id) || IsUnlimited(id))
				continue;
			if (OwnedCount(id) < 1)
				Own(id);
		}

		EnsureEmptyMounts();
		EnforceOwnedEquipLimits(loadout);
	}

	/// <summary>
	/// Unequip surplus copies when owned count is lower than how many slots use a part.
	/// Mount slots clear to empty; required slots grant the missing copy instead.
	/// </summary>
	public void EnforceOwnedEquipLimits(LoadoutData loadout)
	{
		var used = new Dictionary<string, int>();
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var id = loadout.GetPartId(slot);
			if (string.IsNullOrEmpty(id) || IsUnlimited(id))
				continue;

			used[id] = used.GetValueOrDefault(id) + 1;
			if (used[id] <= OwnedCount(id))
				continue;

			used[id]--;
			switch (slot)
			{
				case PartSlot.ShoulderL:
				case PartSlot.ShoulderR:
					loadout.SetPartId(slot, "shoulder_none");
					break;
				case PartSlot.Backpack:
					loadout.SetPartId(slot, "backpack_none");
					break;
				case PartSlot.Systems:
					loadout.SetPartId(slot, "systems_none");
					break;
				default:
					used[id]++;
					SetOwnedCount(id, used[id]);
					break;
			}
		}
	}

	/// <summary>Empty bays are unlimited placeholders, not loot.</summary>
	public void EnsureEmptyMounts()
	{
		SetOwnedCount("shoulder_none", 99);
		SetOwnedCount("backpack_none", 99);
		SetOwnedCount("systems_none", 99);
	}

	public int OwnedCount(string partId)
	{
		if (string.IsNullOrEmpty(partId))
			return 0;
		if (IsUnlimited(partId))
			return 99;
		return OwnedCounts.GetValueOrDefault(partId);
	}

	public bool Owns(string partId) => OwnedCount(partId) > 0;

	public void Own(string partId, int amount = 1)
	{
		if (string.IsNullOrEmpty(partId) || amount <= 0 || IsUnlimited(partId))
			return;
		SetOwnedCount(partId, OwnedCount(partId) + amount);
	}

	public bool TryRemoveOwned(string partId, int amount = 1)
	{
		if (string.IsNullOrEmpty(partId) || amount <= 0 || IsUnlimited(partId))
			return false;
		var have = OwnedCount(partId);
		if (have < amount)
			return false;
		SetOwnedCount(partId, have - amount);
		return true;
	}

	/// <summary>
	/// How many free copies remain for equipping into <paramref name="slot"/>,
	/// counting other draft slots that already use this part.
	/// </summary>
	public int AvailableForSlot(string partId, LoadoutData draft, PartSlot slot)
	{
		if (string.IsNullOrEmpty(partId))
			return 0;
		if (IsUnlimited(partId))
			return 99;

		var equippedElsewhere = 0;
		foreach (PartSlot other in System.Enum.GetValues(typeof(PartSlot)))
		{
			if (other == slot)
				continue;
			if (draft.GetPartId(other) == partId)
				equippedElsewhere++;
		}

		return Mathf.Max(0, OwnedCount(partId) - equippedElsewhere);
	}

	public int EquippedCount(LoadoutData loadout, string partId)
	{
		if (string.IsNullOrEmpty(partId))
			return 0;
		var count = 0;
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			if (loadout.GetPartId(slot) == partId)
				count++;
		}
		return count;
	}

	public int SpareCount(string partId)
	{
		if (IsUnlimited(partId))
			return 99;
		return Mathf.Max(0, OwnedCount(partId) - EquippedCount(Loadout, partId));
	}

	private void SetOwnedCount(string partId, int count)
	{
		if (string.IsNullOrEmpty(partId))
			return;
		if (count <= 0)
			OwnedCounts.Remove(partId);
		else
			OwnedCounts[partId] = count;
	}

	public static bool IsUnlimited(string partId)
	{
		var part = GameCatalog.GetPart(partId);
		return part?.VisualKind == "empty";
	}

	public Dictionary<string, Variant> ToDict()
	{
		var owned = new Godot.Collections.Dictionary();
		foreach (var (id, count) in OwnedCounts.OrderBy(kv => kv.Key))
			owned[id] = count;
		var rep = new Godot.Collections.Dictionary();
		foreach (var (id, value) in ManufacturerReputation.OrderBy(kv => kv.Key))
			rep[id] = value;

		return new Dictionary<string, Variant>
		{
			["inventory_id"] = InventoryId,
			["scrap"] = Scrap,
			["lives"] = LivesBank,
			["skirmishes_played"] = SkirmishesPlayed,
			["skirmishes_won"] = SkirmishesWon,
			["merc_corp_name"] = MercCorpName,
			["affiliated_manufacturer"] = AffiliatedManufacturerId,
			["manufacturer_rep"] = rep,
			["owned_counts"] = owned,
			["keybinds"] = InputBindings.ToDict(),
			["loadout"] = LoadoutToDict(Loadout)
		};
	}

	public static PlayerProfile FromDict(Godot.Collections.Dictionary dict)
	{
		GameCatalog.EnsureBuilt();
		var profile = CreateNew();
		if (dict.ContainsKey("inventory_id"))
			profile.InventoryId = dict["inventory_id"].AsString();
		if (dict.ContainsKey("scrap"))
			profile.Scrap = dict["scrap"].AsInt32();
		if (dict.ContainsKey("lives"))
			profile.LivesBank = dict["lives"].AsInt32();
		if (dict.ContainsKey("skirmishes_played"))
			profile.SkirmishesPlayed = dict["skirmishes_played"].AsInt32();
		if (dict.ContainsKey("skirmishes_won"))
			profile.SkirmishesWon = dict["skirmishes_won"].AsInt32();
		if (dict.ContainsKey("merc_corp_name"))
			profile.MercCorpName = dict["merc_corp_name"].AsString();
		if (dict.ContainsKey("affiliated_manufacturer"))
			profile.AffiliatedManufacturerId = dict["affiliated_manufacturer"].AsString();

		profile.OwnedCounts.Clear();
		if (dict.ContainsKey("owned_counts"))
		{
			foreach (var (key, value) in dict["owned_counts"].AsGodotDictionary())
				profile.SetOwnedCount(key.AsString(), value.AsInt32());
		}
		else if (dict.ContainsKey("owned"))
		{
			// Legacy save: unique IDs only — give one copy each.
			foreach (var v in dict["owned"].AsGodotArray())
				profile.Own(v.AsString());
		}

		profile.ManufacturerReputation.Clear();
		if (dict.ContainsKey("manufacturer_rep"))
		{
			foreach (var (key, value) in dict["manufacturer_rep"].AsGodotDictionary())
				profile.ManufacturerReputation[key.AsString()] = value.AsInt32();
		}

		if (dict.ContainsKey("loadout"))
			profile.Loadout = LoadoutFromDict(dict["loadout"].AsGodotDictionary());
		else
			profile.Loadout = GameCatalog.CreateStarterLoadout();

		profile.GrantLoadoutOwnership(profile.Loadout);
		profile.EnsureEmptyMounts();
		profile.EnsureManufacturerState();

		if (dict.ContainsKey("keybinds"))
			InputBindings.ApplyFromDict(dict["keybinds"].AsGodotDictionary());

		return profile;
	}

	private static Godot.Collections.Dictionary LoadoutToDict(LoadoutData l) => new()
	{
		["legs"] = l.LegsId,
		["torso"] = l.TorsoId,
		["head"] = l.HeadId,
		["core"] = l.PowerCoreId,
		["wep_l"] = l.WeaponLId,
		["wep_r"] = l.WeaponRId,
		["sh_l"] = l.ShoulderLId,
		["sh_r"] = l.ShoulderRId,
		["back"] = l.BackpackId,
		["sys"] = l.SystemsId
	};

	private static LoadoutData LoadoutFromDict(Godot.Collections.Dictionary d)
	{
		var loadout = new LoadoutData
		{
			LegsId = d.ContainsKey("legs") ? d["legs"].AsString() : "legs_tri_biped",
			TorsoId = d.ContainsKey("torso") ? d["torso"].AsString() : "torso_tri_frame",
			HeadId = d.ContainsKey("head") ? d["head"].AsString() : "head_tri_optic",
			PowerCoreId = d.ContainsKey("core") ? d["core"].AsString() : "core_tri_cell",
			WeaponLId = d.ContainsKey("wep_l") ? d["wep_l"].AsString() : "wep_brin_slug",
			WeaponRId = d.ContainsKey("wep_r") ? d["wep_r"].AsString() : "wep_ouro_rifle",
			ShoulderLId = d.ContainsKey("sh_l") ? d["sh_l"].AsString() : "shoulder_none",
			ShoulderRId = d.ContainsKey("sh_r") ? d["sh_r"].AsString() : "",
			BackpackId = d.ContainsKey("back") ? d["back"].AsString() : "backpack_none",
			SystemsId = d.ContainsKey("sys") ? d["sys"].AsString() : "systems_none"
		};
		return GameCatalog.SanitizeMounts(loadout);
	}
}
