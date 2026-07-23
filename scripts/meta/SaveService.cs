using System;
using Godot;

namespace Mechanize;

public sealed class ProfileSlotSummary
{
	public bool Occupied;
	public string AccountHandle = "";
	public long LastPlayedUnix;
	public int Faction;
	public int PortraitIndex;
}

public sealed class ProfileManifest
{
	public int ActiveSlot;
	public int LastUsedSlot;
	public ProfileSlotSummary[] Slots { get; } = CreateEmptySlots();

	private static ProfileSlotSummary[] CreateEmptySlots()
	{
		var slots = new ProfileSlotSummary[SaveService.MaxSlots];
		for (var i = 0; i < slots.Length; i++)
			slots[i] = new ProfileSlotSummary();
		return slots;
	}

	public Godot.Collections.Dictionary ToDict()
	{
		var arr = new Godot.Collections.Array();
		foreach (var slot in Slots)
		{
			arr.Add(new Godot.Collections.Dictionary
			{
				["occupied"] = slot.Occupied,
				["handle"] = slot.AccountHandle,
				["last_played"] = slot.LastPlayedUnix,
				["faction"] = slot.Faction,
				["portrait"] = slot.PortraitIndex
			});
		}

		return new Godot.Collections.Dictionary
		{
			["active_slot"] = ActiveSlot,
			["last_used_slot"] = LastUsedSlot,
			["slots"] = arr
		};
	}

	public static ProfileManifest FromDict(Godot.Collections.Dictionary dict)
	{
		var m = new ProfileManifest();
		m.ActiveSlot = dict.ContainsKey("active_slot")
			? Mathf.Clamp(dict["active_slot"].AsInt32(), 0, SaveService.MaxSlots - 1)
			: 0;
		m.LastUsedSlot = dict.ContainsKey("last_used_slot")
			? Mathf.Clamp(dict["last_used_slot"].AsInt32(), 0, SaveService.MaxSlots - 1)
			: m.ActiveSlot;
		if (!dict.ContainsKey("slots"))
			return m;

		var i = 0;
		foreach (var entry in dict["slots"].AsGodotArray())
		{
			if (i >= SaveService.MaxSlots)
				break;
			if (entry.VariantType != Variant.Type.Dictionary)
			{
				i++;
				continue;
			}

			var d = entry.AsGodotDictionary();
			m.Slots[i].Occupied = d.ContainsKey("occupied") && d["occupied"].AsBool();
			m.Slots[i].AccountHandle = d.ContainsKey("handle") ? d["handle"].AsString() : "";
			m.Slots[i].LastPlayedUnix = d.ContainsKey("last_played") ? d["last_played"].AsInt64() : 0;
			m.Slots[i].Faction = d.ContainsKey("faction") ? d["faction"].AsInt32() : 0;
			m.Slots[i].PortraitIndex = d.ContainsKey("portrait") ? d["portrait"].AsInt32() : 0;
			i++;
		}

		return m;
	}
}

/// <summary>
/// Local profile I/O. Four named slots under user://profiles/; legacy single-file saves migrate to slot 0.
/// </summary>
public static class SaveService
{
	public const int MaxSlots = 4;
	public const int MaxHandleLength = 24;

	public const string LegacySavePath = "user://mechanize_save.json";
	public const string LegacyRoguelikeSavePath = "user://mechanize_roguelike_profile.json";
	public const string LegacySolarCampaignPath = "user://mechanize_solar_campaign.json";
	public const string LegacyCampaignPath = "user://mechanize_campaign.json";
	public const string LegacySolarOnboardingPath = "user://mechanize_solar_onboarding.json";

	public const string ProfilesRoot = "user://profiles";
	public const string ManifestPath = "user://profiles/manifest.json";

	/// <summary>Compat aliases — resolve to the active slot.</summary>
	public static string SavePath => CampaignProfilePath(ActiveSlot);
	public static string RoguelikeSavePath => RoguelikePath(ActiveSlot);

	private static ProfileManifest? _manifest;

	public static int ActiveSlot => Manifest.ActiveSlot;

	public static ProfileManifest Manifest
	{
		get
		{
			_manifest ??= LoadManifest();
			return _manifest;
		}
	}

	public static string SlotDir(int slot) => $"{ProfilesRoot}/slot_{ClampSlot(slot)}";
	/// <summary>Campaign economy bag (solar). Legacy <c>profile.json</c> is migrated on load.</summary>
	public static string CampaignProfilePath(int slot) => $"{SlotDir(slot)}/campaign_profile.json";
	public static string LegacyMainProfilePath(int slot) => $"{SlotDir(slot)}/profile.json";
	/// <summary>Alias for campaign bag path.</summary>
	public static string ProfilePath(int slot) => CampaignProfilePath(slot);
	public static string RoguelikePath(int slot) => $"{SlotDir(slot)}/roguelike_profile.json";
	public static string SkirmishPath(int slot) => $"{SlotDir(slot)}/skirmish_profile.json";
	public static string SolarCampaignPath(int slot) => $"{SlotDir(slot)}/solar_campaign.json";
	public static string CampaignPath(int slot) => $"{SlotDir(slot)}/campaign.json";
	public static string SolarOnboardingPath(int slot) => $"{SlotDir(slot)}/solar_onboarding.json";

	public static bool HasSave() => CampaignBagExists(ActiveSlot);

	public static bool AnyOccupiedSlot()
	{
		for (var i = 0; i < MaxSlots; i++)
		{
			if (SlotOccupied(i))
				return true;
		}

		return false;
	}

	public static bool CampaignBagExists(int slot)
		=> Godot.FileAccess.FileExists(CampaignProfilePath(slot))
		   || Godot.FileAccess.FileExists(LegacyMainProfilePath(slot));

	public static void EnsureReady()
	{
		EnsureProfilesRoot();
		_manifest = LoadManifest();
		MigrateLegacyIfNeeded();
		_manifest = LoadManifest();
		if (!Manifest.Slots[Manifest.LastUsedSlot].Occupied
		    && !AnyOccupied(Manifest))
		{
			// Fresh install: leave all empty; GameSession creates slot 0 on demand.
			return;
		}

		if (!Manifest.Slots[Manifest.LastUsedSlot].Occupied)
		{
			for (var i = 0; i < MaxSlots; i++)
			{
				if (!Manifest.Slots[i].Occupied)
					continue;
				Manifest.LastUsedSlot = i;
				Manifest.ActiveSlot = i;
				SaveManifest(Manifest);
				break;
			}
		}
		else
		{
			Manifest.ActiveSlot = Manifest.LastUsedSlot;
			SaveManifest(Manifest);
		}
	}

	public static void SetActiveSlot(int slot)
	{
		slot = ClampSlot(slot);
		Manifest.ActiveSlot = slot;
		Manifest.LastUsedSlot = slot;
		if (Manifest.Slots[slot].Occupied)
			Manifest.Slots[slot].LastPlayedUnix = NowUnix();
		SaveManifest(Manifest);
	}

	public static void UpdateSlotSummary(int slot, PlayerProfile profile)
	{
		slot = ClampSlot(slot);
		var s = Manifest.Slots[slot];
		s.Occupied = true;
		s.AccountHandle = profile.ResolveAccountHandle();
		s.LastPlayedUnix = NowUnix();
		s.Faction = (int)profile.Faction;
		s.PortraitIndex = profile.PilotPortraitIndex;
		SaveManifest(Manifest);
	}

	public static void ClearSlotSummary(int slot)
	{
		slot = ClampSlot(slot);
		Manifest.Slots[slot] = new ProfileSlotSummary();
		SaveManifest(Manifest);
	}

	public static void Save(PlayerProfile profile)
		=> Save(profile, ProfilePath(ActiveSlot));

	public static void Save(PlayerProfile profile, string path)
	{
		EnsureParentDir(path);
		var dict = new Godot.Collections.Dictionary();
		foreach (var (key, value) in profile.ToDict())
			dict[key] = value;

		var json = Json.Stringify(dict, "\t");
		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(json);
		GD.Print($"Profile saved (schema {PlayerProfile.SchemaVersion}, {profile.OwnedCopyCount} part copies, {profile.Scrap} scrap).");
	}

	public static void SaveActiveProfile(PlayerProfile profile)
	{
		Save(profile, CampaignProfilePath(ActiveSlot));
		UpdateSlotSummary(ActiveSlot, profile);
		// Drop legacy main bag name after successful write.
		DeleteFileIfExists(LegacyMainProfilePath(ActiveSlot));
	}

	public static PlayerProfile LoadOrNew()
		=> LoadCampaignProfile(ActiveSlot);

	public static PlayerProfile LoadOrNew(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
			return PlayerProfile.CreateNew();

		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return PlayerProfile.CreateNew();

		var text = file.GetAsText();
		var parsed = Json.ParseString(text);
		if (parsed.VariantType != Variant.Type.Dictionary)
			return PlayerProfile.CreateNew();

		return PlayerProfile.FromDict(parsed.AsGodotDictionary());
	}

	public static PlayerProfile LoadCampaignProfile(int slot)
	{
		slot = ClampSlot(slot);
		var modern = CampaignProfilePath(slot);
		if (Godot.FileAccess.FileExists(modern))
			return LoadOrNew(modern);

		var legacy = LegacyMainProfilePath(slot);
		if (Godot.FileAccess.FileExists(legacy))
			return LoadOrNew(legacy);

		return PlayerProfile.CreateNew();
	}

	public static PlayerProfile LoadActiveProfile()
		=> LoadCampaignProfile(ActiveSlot);

	public static PlayerProfile LoadSkirmishProfile(int slot)
		=> LoadOrNew(SkirmishPath(ClampSlot(slot)));

	public static ProfileManifest LoadManifest()
	{
		EnsureProfilesRoot();
		if (!Godot.FileAccess.FileExists(ManifestPath))
			return new ProfileManifest();

		using var file = Godot.FileAccess.Open(ManifestPath, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return new ProfileManifest();

		var parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
			return new ProfileManifest();

		return ProfileManifest.FromDict(parsed.AsGodotDictionary());
	}

	public static void SaveManifest(ProfileManifest manifest)
	{
		EnsureProfilesRoot();
		_manifest = manifest;
		using var file = Godot.FileAccess.Open(ManifestPath, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(Json.Stringify(manifest.ToDict(), "\t"));
	}

	public static bool SlotOccupied(int slot)
	{
		slot = ClampSlot(slot);
		if (Manifest.Slots[slot].Occupied)
			return true;
		return CampaignBagExists(slot);
	}

	public static void EnsureSlotDir(int slot)
	{
		var path = SlotDir(slot);
		EnsureDir(path);
	}

	public static void DeleteSlotFiles(int slot)
	{
		slot = ClampSlot(slot);
		DeleteFileIfExists(CampaignProfilePath(slot));
		DeleteFileIfExists(LegacyMainProfilePath(slot));
		DeleteFileIfExists(RoguelikePath(slot));
		DeleteFileIfExists(SkirmishPath(slot));
		DeleteFileIfExists(SolarCampaignPath(slot));
		DeleteFileIfExists(CampaignPath(slot));
		DeleteFileIfExists(SolarOnboardingPath(slot));
		ClearSlotSummary(slot);
	}

	public static void CopySlotFiles(int from, int to)
	{
		from = ClampSlot(from);
		to = ClampSlot(to);
		if (from == to)
			return;
		EnsureSlotDir(to);
		if (Godot.FileAccess.FileExists(CampaignProfilePath(from)))
			CopyFileIfExists(CampaignProfilePath(from), CampaignProfilePath(to));
		else
			CopyFileIfExists(LegacyMainProfilePath(from), CampaignProfilePath(to));
		CopyFileIfExists(RoguelikePath(from), RoguelikePath(to));
		CopyFileIfExists(SkirmishPath(from), SkirmishPath(to));
		CopyFileIfExists(SolarCampaignPath(from), SolarCampaignPath(to));
		CopyFileIfExists(CampaignPath(from), CampaignPath(to));
		CopyFileIfExists(SolarOnboardingPath(from), SolarOnboardingPath(to));
	}

	public static string SanitizeHandle(string? handle)
	{
		if (string.IsNullOrWhiteSpace(handle))
			return "";
		handle = handle.Trim().Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
		while (handle.Contains("  "))
			handle = handle.Replace("  ", " ");
		if (handle.Length > MaxHandleLength)
			handle = handle[..MaxHandleLength];
		return handle.Trim();
	}

	public static bool IsValidHandle(string? handle)
	{
		var s = SanitizeHandle(handle);
		return s.Length >= 1 && s.Length <= MaxHandleLength;
	}

	public static void DeleteFileIfExists(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
			return;
		var abs = ProjectSettings.GlobalizePath(path);
		DirAccess.RemoveAbsolute(abs);
	}

	private static void MigrateLegacyIfNeeded()
	{
		if (Godot.FileAccess.FileExists(ManifestPath))
		{
			// Refresh occupied flags from disk if profile files exist.
			ReconcileOccupiedFromDisk();
			return;
		}

		if (!Godot.FileAccess.FileExists(LegacySavePath))
		{
			SaveManifest(new ProfileManifest());
			return;
		}

		EnsureSlotDir(0);
		CopyFileIfExists(LegacySavePath, CampaignProfilePath(0));
		CopyFileIfExists(LegacyRoguelikeSavePath, RoguelikePath(0));
		CopyFileIfExists(LegacySolarCampaignPath, SolarCampaignPath(0));
		CopyFileIfExists(LegacyCampaignPath, CampaignPath(0));
		CopyFileIfExists(LegacySolarOnboardingPath, SolarOnboardingPath(0));

		var profile = LoadCampaignProfile(0);
		Save(profile, CampaignProfilePath(0));
		EnsureSkirmishBag(0, profile);
		var manifest = new ProfileManifest
		{
			ActiveSlot = 0,
			LastUsedSlot = 0
		};
		manifest.Slots[0].Occupied = true;
		manifest.Slots[0].AccountHandle = profile.ResolveAccountHandle();
		manifest.Slots[0].LastPlayedUnix = NowUnix();
		manifest.Slots[0].Faction = (int)profile.Faction;
		manifest.Slots[0].PortraitIndex = profile.PilotPortraitIndex;
		SaveManifest(manifest);
		GD.Print("Migrated legacy profile into slot 0.");
	}

	/// <summary>Ensure a skirmish bag exists for the slot (seeded empty with identity).</summary>
	public static void EnsureSkirmishBag(int slot, PlayerProfile identitySource)
	{
		slot = ClampSlot(slot);
		var path = SkirmishPath(slot);
		if (Godot.FileAccess.FileExists(path))
			return;

		var skirmish = PlayerProfile.CreateNew(identitySource.ResolveAccountHandle());
		skirmish.SetAccountHandle(identitySource.ResolveAccountHandle());
		if (identitySource.HasFaction)
			skirmish.CopyFactionIdentityFrom(identitySource);
		skirmish.Scrap = 0;
		skirmish.SkirmishesPlayed = 0;
		skirmish.SkirmishesWon = 0;
		Save(skirmish, path);
	}

	private static void ReconcileOccupiedFromDisk()
	{
		var changed = false;
		for (var i = 0; i < MaxSlots; i++)
		{
			var exists = CampaignBagExists(i);
			if (exists == Manifest.Slots[i].Occupied
			    && !(exists && string.IsNullOrEmpty(Manifest.Slots[i].AccountHandle)))
			{
				if (exists && Godot.FileAccess.FileExists(LegacyMainProfilePath(i))
				    && !Godot.FileAccess.FileExists(CampaignProfilePath(i)))
				{
					var migrated = LoadOrNew(LegacyMainProfilePath(i));
					Save(migrated, CampaignProfilePath(i));
					DeleteFileIfExists(LegacyMainProfilePath(i));
					EnsureSkirmishBag(i, migrated);
					changed = true;
				}

				continue;
			}

			Manifest.Slots[i].Occupied = exists;
			if (exists)
			{
				var p = LoadCampaignProfile(i);
				Manifest.Slots[i].AccountHandle = p.ResolveAccountHandle();
				Manifest.Slots[i].Faction = (int)p.Faction;
				Manifest.Slots[i].PortraitIndex = p.PilotPortraitIndex;
				if (Godot.FileAccess.FileExists(LegacyMainProfilePath(i))
				    && !Godot.FileAccess.FileExists(CampaignProfilePath(i)))
				{
					Save(p, CampaignProfilePath(i));
					DeleteFileIfExists(LegacyMainProfilePath(i));
				}

				EnsureSkirmishBag(i, p);
			}

			changed = true;
		}

		if (changed)
			SaveManifest(Manifest);
	}

	private static bool AnyOccupied(ProfileManifest m)
	{
		foreach (var s in m.Slots)
		{
			if (s.Occupied)
				return true;
		}

		return false;
	}

	private static void EnsureProfilesRoot() => EnsureDir(ProfilesRoot);

	private static void EnsureDir(string userPath)
	{
		var abs = ProjectSettings.GlobalizePath(userPath);
		DirAccess.MakeDirRecursiveAbsolute(abs);
	}

	private static void EnsureParentDir(string filePath)
	{
		var abs = ProjectSettings.GlobalizePath(filePath);
		var parent = abs.GetBaseDir();
		if (!string.IsNullOrEmpty(parent))
			DirAccess.MakeDirRecursiveAbsolute(parent);
	}

	private static void CopyFileIfExists(string from, string to)
	{
		if (!Godot.FileAccess.FileExists(from))
			return;
		EnsureParentDir(to);
		using var src = Godot.FileAccess.Open(from, Godot.FileAccess.ModeFlags.Read);
		if (src == null)
			return;
		var text = src.GetAsText();
		using var dst = Godot.FileAccess.Open(to, Godot.FileAccess.ModeFlags.Write);
		dst?.StoreString(text);
	}

	private static int ClampSlot(int slot) => Mathf.Clamp(slot, 0, MaxSlots - 1);

	private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
