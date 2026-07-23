using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Persistent player meta + active run kit.
/// Ownership is instance-based — each purchase is a physical copy with its own condition.
/// Count APIs remain as derived compatibility helpers.
/// </summary>
public sealed class PlayerProfile : IPartInventory
{
	public const int SchemaVersion = 6;
	public const int StartingManufacturerRep = -10;
	public const int StartingAlignedRep = 20;
	public const int StartingLives = 2;
	public const string DefaultInventoryId = "pilot";

	public int Schema { get; set; } = SchemaVersion;
	public string InventoryId { get; set; } = DefaultInventoryId;
	public int Scrap { get; set; }
	public int LivesBank { get; set; } = StartingLives;
	public List<OwnedPartInstance> OwnedInstances { get; set; } = new();
	public Dictionary<PartSlot, string> EquippedInstanceIds { get; set; } = new();
	public LoadoutData Loadout { get; set; } = null!;
	public int SkirmishesPlayed { get; set; }
	public int SkirmishesWon { get; set; }
	/// <summary>Last skirmish Day/Night toggle choice (defaults Night).</summary>
	public ArenaPeriod PreferredSkirmishArenaPeriod { get; set; } = ArenaPeriod.Night;
	/// <summary>Local/LAN account handle — also the profile slot display name.</summary>
	public string AccountHandle { get; set; } = "";
	public string MercCorpName { get; set; } = VoidCorpsIdentity.PlayerCorpCodename;
	/// <summary>Cat / Dog identity. Locked once set. Copied onto all mode bags.</summary>
	public FactionId Faction { get; set; } = FactionId.None;
	/// <summary>0–8 index into the faction's 3×3 pilot portrait sheet.</summary>
	public int PilotPortraitIndex { get; set; }
	public string AffiliatedManufacturerId { get; set; } = "";
	public string EmployerCompanyId { get; set; } = "";
	public string EmployerCompanyName { get; set; } = "";
	public Dictionary<string, int> ManufacturerReputation { get; set; } = new();
	public Dictionary<string, int> CraftingMaterials { get; set; } = new();
	public HashSet<string> UnlockedBlueprints { get; set; } = new();

	public bool HasFaction => Faction is FactionId.Cat or FactionId.Dog;

	/// <summary>Legacy/compat count map derived from instances (excludes unlimited empties).</summary>
	public Dictionary<string, int> OwnedCounts =>
		OwnedInstances
			.Where(i => !IsUnlimited(i.PartId))
			.GroupBy(i => i.PartId)
			.ToDictionary(g => g.Key, g => g.Count());

	public int OwnedTypeCount => OwnedCounts.Count;
	public int OwnedCopyCount => OwnedInstances.Count(i => !IsUnlimited(i.PartId));

	public static PlayerProfile CreateNew(string? accountHandle = null)
	{
		GameCatalog.EnsureBuilt();
		var handle = SaveService.SanitizeHandle(accountHandle);
		if (string.IsNullOrEmpty(handle))
			handle = VoidCorpsIdentity.PlayerCorpCodename;
		var profile = new PlayerProfile
		{
			Schema = SchemaVersion,
			InventoryId = DefaultInventoryId,
			Scrap = 0,
			LivesBank = StartingLives,
			Loadout = GameCatalog.CreateStarterLoadout(),
			OwnedInstances = new List<OwnedPartInstance>(),
			EquippedInstanceIds = new Dictionary<PartSlot, string>(),
			AccountHandle = handle,
			MercCorpName = handle,
			Faction = FactionId.None,
			PilotPortraitIndex = 0,
			AffiliatedManufacturerId = "",
			EmployerCompanyId = "",
			EmployerCompanyName = "",
			ManufacturerReputation = new Dictionary<string, int>(),
			CraftingMaterials = new Dictionary<string, int>(),
			UnlockedBlueprints = new HashSet<string>(),
			SkirmishesPlayed = 0,
			SkirmishesWon = 0
		};
		profile.EnsureManufacturerState();
		profile.GrantLoadoutOwnership(profile.Loadout);
		profile.OwnCombatSliceParts();
		profile.BindEquippedInstancesFromLoadout();
		profile.UnlockOwnedBlueprints();
		return profile;
	}

	public string ResolveAccountHandle()
	{
		if (!string.IsNullOrWhiteSpace(AccountHandle))
			return SaveService.SanitizeHandle(AccountHandle);
		if (!string.IsNullOrWhiteSpace(MercCorpName))
			return SaveService.SanitizeHandle(MercCorpName);
		return VoidCorpsIdentity.PlayerCorpCodename;
	}

	public void SetAccountHandle(string handle)
	{
		handle = SaveService.SanitizeHandle(handle);
		if (string.IsNullOrEmpty(handle))
			handle = VoidCorpsIdentity.PlayerCorpCodename;
		AccountHandle = handle;
		MercCorpName = handle;
	}

	/// <summary>
	/// Lock Cat/Dog + portrait for this profile. No-ops if already locked to a faction
	/// (unless <paramref name="force"/> — used when copying identity onto a fresh roguelike kit).
	/// </summary>
	public bool SetFaction(FactionId faction, int portraitIndex, bool force = false)
	{
		if (faction is not (FactionId.Cat or FactionId.Dog))
			return false;
		if (HasFaction && !force)
			return false;

		Faction = faction;
		PilotPortraitIndex = Mathf.Clamp(portraitIndex, 0, PilotPortraits.PortraitCount - 1);
		return true;
	}

	public void CopyFactionIdentityFrom(PlayerProfile source)
	{
		if (source == null || !source.HasFaction)
			return;
		SetFaction(source.Faction, source.PilotPortraitIndex, force: true);
	}

	public void WipeRunInventory()
	{
		GameCatalog.EnsureBuilt();
		Scrap = 0;
		LivesBank = StartingLives;
		OwnedInstances.Clear();
		EquippedInstanceIds.Clear();
		AffiliatedManufacturerId = "";
		EmployerCompanyId = "";
		EmployerCompanyName = "";
		Loadout = GameCatalog.CreateStarterLoadout();
		GrantLoadoutOwnership(Loadout);
		OwnCombatSliceParts();
		BindEquippedInstancesFromLoadout();
	}

	public void OwnCombatSliceParts()
	{
		Own("wep_brin_cleaver");
		Own("wep_tri_bulwark");
	}

	public int MaterialCount(string materialId) => CraftingMaterials.GetValueOrDefault(materialId);

	public void AddMaterial(string materialId, int amount)
	{
		if (!MaterialCatalog.All.ContainsKey(materialId) || amount == 0)
			return;
		CraftingMaterials[materialId] = Mathf.Max(0, MaterialCount(materialId) + amount);
	}

	public bool HasBlueprint(string partId) =>
		IsUnlimited(partId) || UnlockedBlueprints.Contains(partId);

	public void UnlockOwnedBlueprints()
	{
		foreach (var instance in OwnedInstances)
			UnlockedBlueprints.Add(instance.PartId);
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var partId = Loadout.GetPartId(slot);
			if (!string.IsNullOrEmpty(partId) && !IsUnlimited(partId))
				UnlockedBlueprints.Add(partId);
		}
	}

	public void EnsureManufacturerState()
	{
		foreach (var id in GameCatalog.CampaignManufacturerIds)
		{
			if (!ManufacturerReputation.ContainsKey(id))
				ManufacturerReputation[id] = StartingManufacturerRep;
		}

		if (string.IsNullOrEmpty(AccountHandle))
			AccountHandle = !string.IsNullOrEmpty(MercCorpName)
				? MercCorpName
				: VoidCorpsIdentity.PlayerCorpCodename;
		if (string.IsNullOrEmpty(MercCorpName))
			MercCorpName = AccountHandle;

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
		BindEquippedInstancesFromLoadout();
	}

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
					EquippedInstanceIds.Remove(slot);
					break;
				case PartSlot.Backpack:
					loadout.SetPartId(slot, "backpack_none");
					EquippedInstanceIds.Remove(slot);
					break;
				case PartSlot.Systems:
					loadout.SetPartId(slot, "systems_none");
					EquippedInstanceIds.Remove(slot);
					break;
				default:
					used[id]++;
					Own(id);
					break;
			}
		}

		BindEquippedInstancesFromLoadout();
	}

	public void EnsureEmptyMounts()
	{
		// Unlimited empties are virtual — no instances tracked.
	}

	public int OwnedCount(string partId)
	{
		if (string.IsNullOrEmpty(partId))
			return 0;
		if (IsUnlimited(partId))
			return 99;
		return OwnedInstances.Count(i => i.PartId == partId);
	}

	public bool Owns(string partId) => OwnedCount(partId) > 0;

	public void Own(string partId, int amount = 1)
	{
		if (string.IsNullOrEmpty(partId) || amount <= 0 || IsUnlimited(partId))
			return;
		for (var i = 0; i < amount; i++)
			OwnedInstances.Add(OwnedPartInstance.Create(partId));
	}

	public OwnedPartInstance OwnInstance(string partId, PartCondition? condition = null)
	{
		var instance = OwnedPartInstance.Create(partId, condition);
		if (!IsUnlimited(partId))
			OwnedInstances.Add(instance);
		return instance;
	}

	/// <summary>Accept a physical copy transferred by another pilot, preserving its stable ID.</summary>
	public OwnedPartInstance AdoptTransferredInstance(
		string instanceId,
		string partId,
		PartCondition condition)
	{
		var existing = GetInstance(instanceId);
		if (existing != null)
			return existing;

		var instance = OwnedPartInstance.Create(partId, condition);
		instance.InstanceId = instanceId;
		instance.Reserved = false;
		if (!IsUnlimited(partId))
			OwnedInstances.Add(instance);
		return instance;
	}

	public bool TryRemoveOwned(string partId, int amount = 1)
	{
		if (string.IsNullOrEmpty(partId) || amount <= 0 || IsUnlimited(partId))
			return false;
		var spares = GetSpareInstances(partId);
		if (spares.Count < amount)
			return false;
		for (var i = 0; i < amount; i++)
			OwnedInstances.Remove(spares[i]);
		return true;
	}

	public bool TryRemoveInstance(string instanceId)
	{
		var idx = OwnedInstances.FindIndex(i => i.InstanceId == instanceId);
		if (idx < 0)
			return false;
		foreach (var (slot, id) in EquippedInstanceIds.ToList())
		{
			if (id == instanceId)
				EquippedInstanceIds.Remove(slot);
		}

		OwnedInstances.RemoveAt(idx);
		return true;
	}

	public OwnedPartInstance? GetInstance(string instanceId) =>
		OwnedInstances.FirstOrDefault(i => i.InstanceId == instanceId);

	public List<OwnedPartInstance> GetInstances(string partId) =>
		OwnedInstances.Where(i => i.PartId == partId).ToList();

	public List<OwnedPartInstance> GetSpareInstances(string partId)
	{
		var equipped = new HashSet<string>(EquippedInstanceIds.Values);
		return OwnedInstances
			.Where(i => i.PartId == partId && !i.Reserved && !equipped.Contains(i.InstanceId))
			.OrderByDescending(i => i.Condition.AverageRatio)
			.ToList();
	}

	public OwnedPartInstance? GetEquippedInstance(PartSlot slot)
	{
		if (!EquippedInstanceIds.TryGetValue(slot, out var id))
			return null;
		return GetInstance(id);
	}

	public void SetEquippedInstance(PartSlot slot, OwnedPartInstance? instance)
	{
		if (instance == null || IsUnlimited(instance.PartId))
		{
			EquippedInstanceIds.Remove(slot);
			return;
		}

		EquippedInstanceIds[slot] = instance.InstanceId;
		Loadout.SetPartId(slot, instance.PartId);
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
		return GetSpareInstances(partId).Count;
	}

	public void BindEquippedInstancesFromLoadout()
	{
		var claimed = new HashSet<string>();
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var partId = Loadout.GetPartId(slot);
			if (string.IsNullOrEmpty(partId) || IsUnlimited(partId))
			{
				EquippedInstanceIds.Remove(slot);
				continue;
			}

			if (EquippedInstanceIds.TryGetValue(slot, out var existingId))
			{
				var existing = GetInstance(existingId);
				if (existing != null && existing.PartId == partId && claimed.Add(existingId))
					continue;
			}

			var free = OwnedInstances.FirstOrDefault(i =>
				i.PartId == partId && !i.Reserved && !claimed.Contains(i.InstanceId));
			if (free == null)
			{
				free = OwnInstance(partId);
			}

			claimed.Add(free.InstanceId);
			EquippedInstanceIds[slot] = free.InstanceId;
		}
	}

	public void RepairAllEquippedFully()
	{
		foreach (var slot in EquippedInstanceIds.Keys.ToList())
		{
			var instance = GetEquippedInstance(slot);
			instance?.Condition.SetFull();
		}
	}

	public Dictionary<string, Variant> ToDict()
	{
		var instances = new Godot.Collections.Array();
		foreach (var instance in OwnedInstances.OrderBy(i => i.PartId).ThenBy(i => i.InstanceId))
			instances.Add(instance.ToDict());

		var equipped = new Godot.Collections.Dictionary();
		foreach (var (slot, id) in EquippedInstanceIds.OrderBy(kv => (int)kv.Key))
			equipped[((int)slot).ToString()] = id;

		var rep = new Godot.Collections.Dictionary();
		foreach (var (id, value) in ManufacturerReputation.OrderBy(kv => kv.Key))
			rep[id] = value;

		var materials = new Godot.Collections.Dictionary();
		foreach (var (id, value) in CraftingMaterials.OrderBy(kv => kv.Key))
			materials[id] = value;

		var blueprints = new Godot.Collections.Array();
		foreach (var id in UnlockedBlueprints.OrderBy(id => id))
			blueprints.Add(id);

		// Keep owned_counts for older tooling / readability.
		var owned = new Godot.Collections.Dictionary();
		foreach (var (id, count) in OwnedCounts.OrderBy(kv => kv.Key))
			owned[id] = count;

		return new Dictionary<string, Variant>
		{
			["schema_version"] = SchemaVersion,
			["inventory_id"] = InventoryId,
			["scrap"] = Scrap,
			["lives"] = LivesBank,
			["skirmishes_played"] = SkirmishesPlayed,
			["skirmishes_won"] = SkirmishesWon,
			["skirmish_arena_period"] = (int)PreferredSkirmishArenaPeriod,
			["account_handle"] = AccountHandle,
			["merc_corp_name"] = MercCorpName,
			["faction"] = (int)Faction,
			["pilot_portrait"] = PilotPortraitIndex,
			["affiliated_manufacturer"] = AffiliatedManufacturerId,
			["employer_company"] = EmployerCompanyId,
			["employer_company_name"] = EmployerCompanyName,
			["manufacturer_rep"] = rep,
			["crafting_materials"] = materials,
			["unlocked_blueprints"] = blueprints,
			["owned_instances"] = instances,
			["equipped_instances"] = equipped,
			["owned_counts"] = owned,
			["keybinds"] = InputBindings.ToDict(),
			["loadout"] = LoadoutToDict(Loadout)
		};
	}

	public static PlayerProfile FromDict(Godot.Collections.Dictionary dict)
	{
		GameCatalog.EnsureBuilt();
		var profile = CreateNew();
		profile.OwnedInstances.Clear();
		profile.EquippedInstanceIds.Clear();

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
		if (dict.ContainsKey("skirmish_arena_period"))
		{
			var raw = dict["skirmish_arena_period"].AsInt32();
			profile.PreferredSkirmishArenaPeriod = raw is (int)ArenaPeriod.Day or (int)ArenaPeriod.Night
				? (ArenaPeriod)raw
				: ArenaPeriod.Night;
		}
		if (dict.ContainsKey("account_handle"))
			profile.AccountHandle = dict["account_handle"].AsString();
		if (dict.ContainsKey("merc_corp_name"))
			profile.MercCorpName = dict["merc_corp_name"].AsString();
		if (string.IsNullOrEmpty(profile.AccountHandle))
			profile.AccountHandle = profile.MercCorpName;
		if (string.IsNullOrEmpty(profile.MercCorpName))
			profile.MercCorpName = profile.AccountHandle;
		if (dict.ContainsKey("faction"))
		{
			var raw = dict["faction"].AsInt32();
			profile.Faction = raw is (int)FactionId.Cat or (int)FactionId.Dog
				? (FactionId)raw
				: FactionId.None;
		}
		if (dict.ContainsKey("pilot_portrait"))
			profile.PilotPortraitIndex = Mathf.Clamp(dict["pilot_portrait"].AsInt32(), 0, PilotPortraits.PortraitCount - 1);
		if (dict.ContainsKey("affiliated_manufacturer"))
			profile.AffiliatedManufacturerId = dict["affiliated_manufacturer"].AsString();
		if (dict.ContainsKey("employer_company"))
			profile.EmployerCompanyId = dict["employer_company"].AsString();
		if (dict.ContainsKey("employer_company_name"))
			profile.EmployerCompanyName = dict["employer_company_name"].AsString();

		profile.ManufacturerReputation.Clear();
		if (dict.ContainsKey("manufacturer_rep"))
		{
			foreach (var (key, value) in dict["manufacturer_rep"].AsGodotDictionary())
				profile.ManufacturerReputation[key.AsString()] = value.AsInt32();
		}

		profile.CraftingMaterials.Clear();
		if (dict.ContainsKey("crafting_materials"))
		{
			foreach (var (key, value) in dict["crafting_materials"].AsGodotDictionary())
				profile.CraftingMaterials[key.AsString()] = Mathf.Max(0, value.AsInt32());
		}

		profile.UnlockedBlueprints.Clear();
		var hasBlueprintSave = dict.ContainsKey("unlocked_blueprints");
		if (hasBlueprintSave)
		{
			foreach (var value in dict["unlocked_blueprints"].AsGodotArray())
				profile.UnlockedBlueprints.Add(value.AsString());
		}

		if (dict.ContainsKey("loadout"))
			profile.Loadout = LoadoutFromDict(dict["loadout"].AsGodotDictionary());
		else
			profile.Loadout = GameCatalog.CreateStarterLoadout();

		var schema = dict.ContainsKey("schema_version") ? dict["schema_version"].AsInt32() : 1;
		profile.Schema = schema;

		if (dict.ContainsKey("owned_instances"))
		{
			foreach (var entry in dict["owned_instances"].AsGodotArray())
			{
				if (entry.VariantType != Variant.Type.Dictionary)
					continue;
				var instance = OwnedPartInstance.FromDict(entry.AsGodotDictionary());
				if (string.IsNullOrEmpty(instance.PartId) || IsUnlimited(instance.PartId))
					continue;
				if (GameCatalog.GetPart(instance.PartId) == null)
				{
					GD.PushWarning($"PlayerProfile: skipping unknown part instance {instance.PartId}");
					continue;
				}

				profile.OwnedInstances.Add(instance);
			}

			if (dict.ContainsKey("equipped_instances"))
			{
				foreach (var (key, value) in dict["equipped_instances"].AsGodotDictionary())
				{
					if (!int.TryParse(key.AsString(), out var slotInt))
						continue;
					profile.EquippedInstanceIds[(PartSlot)slotInt] = value.AsString();
				}
			}
		}
		else
		{
			// Legacy count / unique-ID saves → full-condition instances.
			if (dict.ContainsKey("owned_counts"))
			{
				foreach (var (key, value) in dict["owned_counts"].AsGodotDictionary())
				{
					var partId = key.AsString();
					if (IsUnlimited(partId) || GameCatalog.GetPart(partId) == null)
						continue;
					var count = value.AsInt32();
					for (var i = 0; i < count; i++)
					{
						var instance = OwnedPartInstance.Create(partId);
						instance.InstanceId = $"legacy:{partId}:{i}";
						profile.OwnedInstances.Add(instance);
					}
				}
			}
			else if (dict.ContainsKey("owned"))
			{
				foreach (var v in dict["owned"].AsGodotArray())
				{
					var partId = v.AsString();
					if (IsUnlimited(partId) || GameCatalog.GetPart(partId) == null)
						continue;
					var instance = OwnedPartInstance.Create(partId);
					instance.InstanceId = $"legacy:{partId}:0";
					profile.OwnedInstances.Add(instance);
				}
			}
		}

		profile.GrantLoadoutOwnership(profile.Loadout);
		profile.OwnCombatSliceParts();
		profile.BindEquippedInstancesFromLoadout();
		if (!hasBlueprintSave)
			profile.UnlockOwnedBlueprints();
		profile.EnsureManufacturerState();
		profile.Schema = SchemaVersion;

		if (dict.ContainsKey("keybinds"))
			InputBindings.ApplyFromDict(dict["keybinds"].AsGodotDictionary());

		return profile;
	}

	public static bool IsUnlimited(string partId)
	{
		var part = GameCatalog.GetPart(partId);
		return part?.VisualKind == "empty";
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
		return GameCatalog.SanitizeLoadout(loadout);
	}
}
