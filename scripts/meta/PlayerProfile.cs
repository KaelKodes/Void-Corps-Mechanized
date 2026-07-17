using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Persistent player state shared by Skirmish and (later) Campaign.
/// Part ownership is quantity-based — one purchase = one equippable copy.
/// </summary>
public sealed class PlayerProfile
{
	public int Scrap { get; set; }
	public int LivesBank { get; set; } = 2;
	public Dictionary<string, int> OwnedCounts { get; set; } = new();
	public LoadoutData Loadout { get; set; } = null!;
	public int SkirmishesPlayed { get; set; }
	public int SkirmishesWon { get; set; }

	/// <summary>Distinct owned part types (for UI totals).</summary>
	public int OwnedTypeCount => OwnedCounts.Count;

	/// <summary>Total owned copies across all parts.</summary>
	public int OwnedCopyCount => OwnedCounts.Values.Sum();

	public static PlayerProfile CreateNew()
	{
		GameCatalog.EnsureBuilt();
		var loadout = GameCatalog.CreateStarterLoadout();
		var profile = new PlayerProfile
		{
			Scrap = 0,
			LivesBank = 2,
			Loadout = loadout,
			OwnedCounts = new Dictionary<string, int>()
		};
		profile.GrantLoadoutOwnership(loadout);
		profile.GrantStarterPool();
		return profile;
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

	public void GrantStarterPool()
	{
		// Extras beyond the equipped kit so the first garage has some choices.
		Own("legs_tri_biped");
		Own("legs_ouro_hex");
		Own("torso_tri_frame");
		Own("torso_tri_fleet");
		Own("head_tri_optic");
		Own("head_ouro_scope");
		Own("core_tri_cell");
		Own("wep_brin_slug");
		Own("wep_ouro_rifle");
		Own("wep_tri_burst");
		Own("wep_lum_arc");
		Own("shoulder_tri_patrol");
		Own("shoulder_brin_pods");
		Own("systems_ouro_heatsink");
		Own("systems_tri_coolant");
		Own("backpack_tri_mend");
		Own("backpack_ouro_pulse");
		EnsureEmptyMounts();
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

		return new Dictionary<string, Variant>
		{
			["scrap"] = Scrap,
			["lives"] = LivesBank,
			["skirmishes_played"] = SkirmishesPlayed,
			["skirmishes_won"] = SkirmishesWon,
			["owned_counts"] = owned,
			["keybinds"] = InputBindings.ToDict(),
			["loadout"] = LoadoutToDict(Loadout)
		};
	}

	public static PlayerProfile FromDict(Godot.Collections.Dictionary dict)
	{
		GameCatalog.EnsureBuilt();
		var profile = CreateNew();
		if (dict.ContainsKey("scrap"))
			profile.Scrap = dict["scrap"].AsInt32();
		if (dict.ContainsKey("lives"))
			profile.LivesBank = dict["lives"].AsInt32();
		if (dict.ContainsKey("skirmishes_played"))
			profile.SkirmishesPlayed = dict["skirmishes_played"].AsInt32();
		if (dict.ContainsKey("skirmishes_won"))
			profile.SkirmishesWon = dict["skirmishes_won"].AsInt32();

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

		if (dict.ContainsKey("loadout"))
			profile.Loadout = LoadoutFromDict(dict["loadout"].AsGodotDictionary());
		else
			profile.Loadout = GameCatalog.CreateStarterLoadout();

		profile.GrantLoadoutOwnership(profile.Loadout);
		if (profile.OwnedCounts.Count == 0)
			profile.GrantStarterPool();
		else
			profile.EnsureEmptyMounts();

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
