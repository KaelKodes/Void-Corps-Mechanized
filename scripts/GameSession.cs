using Godot;

namespace Mechanize;

/// <summary>
/// Session-wide profile, claim, match, campaign, and Void Corps ops state.
/// </summary>
public partial class GameSession : Node
{
	public PlayerProfile Profile { get; private set; } = null!;
	public MatchSession Match { get; } = new();
	/// <summary>Legacy linear run, exposed as the Roguelike game mode.</summary>
	public CampaignRun? Campaign { get; set; }
	/// <summary>Persistent, revisit-able solar-system campaign.</summary>
	public SolarCampaignRun SolarCampaign { get; private set; } = null!;
	public LoadoutData? CadetLoaner { get; private set; }
	/// <summary>Convention trial demo chassis — mutually exclusive with cadet loaner.</summary>
	public LoadoutData? ConventionDemoLoaner { get; private set; }
	/// <summary>Skirmish-only premade chassis — does not overwrite campaign garage loadout.</summary>
	public LoadoutData? SkirmishLoaner { get; private set; }
	public int SkirmishPremadeVariant { get; private set; } = -1;
	public LoadoutData CurrentLoadout =>
		ConventionDemoLoaner ?? CadetLoaner ?? SkirmishLoaner ?? Profile.Loadout;
	/// <summary>Cadet / convention / skirmish loaners — never inherit garage part wear.</summary>
	public bool UsingTemporaryLoaner =>
		CadetLoaner != null || ConventionDemoLoaner != null || SkirmishLoaner != null;
	public VoidCorpsIdentity.ClaimSite CurrentClaim { get; private set; }
	public PilotDifficulty PendingDifficulty { get; set; } = PilotDifficulty.Easy;
	/// <summary>Skirmish day/night lighting — driven by setup toggle; defaults Night.</summary>
	public ArenaPeriod PendingArenaPeriod { get; set; } = ArenaPeriod.Night;
	public MissionType PendingMission { get; set; } = MissionType.DestroyAllEnemies;
	public BossEncounterId PendingBossEncounter { get; set; } = BossEncounterId.None;
	public string PendingRivalPilotId { get; set; } = "";
	public string PendingRivalCorpId { get; set; } = "";
	public int PendingOfferIndex { get; set; } = -1;
	/// <summary>Manufacturer that offered the last campaign mission (drives post-mission shop).</summary>
	public string LastMissionManufacturerId { get; set; } = "";
	public bool LaunchSkirmishOnArenaLoad { get; set; }
	/// <summary>When true, main menu opens directly on skirmish setup.</summary>
	public bool OpenSkirmishSetupOnMenu { get; set; }
	/// <summary>When true, continue from results returns to campaign map.</summary>
	public bool ReturnToCampaignMap { get; set; }
	public bool ReturnToSolarMap { get; set; }
	/// <summary>Results continue should return to convention hall.</summary>
	public bool ReturnToConventionHall { get; set; }
	/// <summary>Studio boot bumper already played this app launch.</summary>
	public bool StudioIntroPlayed { get; set; }
	/// <summary>True when the current/last match was launched from campaign.</summary>
	public bool MatchFromCampaign { get; set; }
	public bool MatchFromSolarCampaign { get; set; }
	public string PendingSolarLocationId { get; private set; } = "";
	/// <summary>True when match is part of MAP Cadet Program (range / live fire).</summary>
	public bool MatchFromAcademy { get; set; }
	/// <summary>True when match is a manufacturer convention trial.</summary>
	public bool MatchFromConvention { get; set; }
	/// <summary>Results continue should launch the next academy arena beat.</summary>
	public bool LaunchAcademyContinue { get; set; }
	/// <summary>Results continue should open graduation / chat.</summary>
	public bool OpenAcademyGraduation { get; set; }
	/// <summary>True when match is a co-op detachment (listen server).</summary>
	public bool CoopMatch { get; set; }
	/// <summary>Active multiplayer mode for the current/pending match.</summary>
	public MultiplayerGameMode MultiplayerGameMode { get; set; } = MultiplayerGameMode.CoopRogueLike;
	/// <summary>Lobby roster snapshot applied from launch payload (humans + bots).</summary>
	public System.Collections.Generic.List<LobbySlot> LobbyRoster { get; private set; } = new();
	/// <summary>True when Team/FFA skirmish — no PvE waves, PvP win rules.</summary>
	public bool IsPvpMatch => LobbyModeRules.IsPvp(MultiplayerGameMode);
	/// <summary>Which economy bag <see cref="Profile"/> currently points at.</summary>
	public ProfileBagKind ActiveBag { get; private set; } = ProfileBagKind.Campaign;
	public bool UsingRoguelikeProfile
	{
		get => ActiveBag == ProfileBagKind.Roguelike;
		private set => ActiveBag = value ? ProfileBagKind.Roguelike : ProfileBagKind.Campaign;
	}
	public bool UsingSkirmishProfile => ActiveBag == ProfileBagKind.Skirmish;
	/// <summary>True when the current/last match was a skirmish (sandbox / PvP ladder bag).</summary>
	public bool MatchFromSkirmish { get; set; }
	/// <summary>Menu action waiting on first-time Cat/Dog lock.</summary>
	public PendingFactionContinue PendingFactionContinue { get; set; }
	/// <summary>True when at least one local profile slot exists.</summary>
	public bool HasAnyProfile => SaveService.AnyOccupiedSlot();
	/// <summary>Pending empty slot index for the create wizard (-1 = none).</summary>
	public int PendingCreateSlot { get; set; } = -1;

	public bool InCampaign => Campaign is { Alive: true };
	public bool InSolarCampaign => SolarCampaign != null;
	public bool InCadetProgram => Campaign is { Phase: CampaignPhase.CadetProgram, Alive: true };
	public bool InConvention => Campaign is { Phase: CampaignPhase.ManufacturerConvention, Alive: true };
	public bool InSolarOnboarding => Campaign is { SolarOnboarding: true, Alive: true }
		&& !SolarCampaign.OnboardingComplete;

	public FrontierCompanyData? GetFrontierCompany(string companyId)
	{
		foreach (var company in SolarCampaign.ConventionCompanies)
		{
			if (company.Id == companyId)
				return company;
		}
		return null;
	}

	public bool NeedsFactionPick
	{
		get
		{
			ActivateCampaignProfile();
			return !Profile.HasFaction;
		}
	}

	/// <summary>
	/// Campaign / roguelike entry: if faction unset, open pick UI; otherwise run <paramref name="whenReady"/>.
	/// </summary>
	public bool TryBeginWithFactionGate(PendingFactionContinue pending, System.Action whenReady)
	{
		ActivateCampaignProfile();
		if (Profile.HasFaction)
		{
			PendingFactionContinue = PendingFactionContinue.None;
			whenReady();
			return false;
		}

		PendingFactionContinue = pending;
		return true;
	}

	public void ContinueAfterFactionPick()
	{
		var pending = PendingFactionContinue;
		PendingFactionContinue = PendingFactionContinue.None;
		var tree = GetTree();
		if (tree == null)
			return;

		switch (pending)
		{
			case PendingFactionContinue.SolarTutorial:
				BeginSolarCampaign(reset: true);
				LaunchSolarOnboarding();
				tree.ChangeSceneToFile("res://scenes/arena.tscn");
				break;
			case PendingFactionContinue.SolarSkipConvention:
				BeginSolarCampaignSkipToConvention();
				tree.ChangeSceneToFile("res://scenes/convention_hall.tscn");
				break;
			case PendingFactionContinue.RoguelikeCadet:
				BeginCadetProgram();
				tree.ChangeSceneToFile("res://scenes/arena.tscn");
				break;
			case PendingFactionContinue.RoguelikeConvention:
				BeginConventionProgram();
				tree.ChangeSceneToFile("res://scenes/convention_hall.tscn");
				break;
			case PendingFactionContinue.NewProfile:
			default:
				tree.ChangeSceneToFile("res://scenes/main_menu.tscn");
				break;
		}
	}

	private void SeedRoguelikeIdentityFromMain(string? handle = null)
	{
		ActivateCampaignProfile();
		var faction = Profile.Faction;
		var portrait = Profile.PilotPortraitIndex;
		var resolved = handle ?? Profile.ResolveAccountHandle();
		Profile = PlayerProfile.CreateNew(resolved);
		Profile.SetAccountHandle(resolved);
		if (faction is FactionId.Cat or FactionId.Dog)
			Profile.SetFaction(faction, portrait, force: true);
	}

	public int ActiveSlotIndex => SaveService.ActiveSlot;

	public override void _Ready()
	{
		GameCatalog.EnsureBuilt();
		SupportCatalog.EnsureBuilt();
		ConventionCatalog.EnsureBuilt();
		InputBindings.EnsureDefaultsCaptured();
		SaveService.EnsureReady();
		EnsureActiveSlotLoaded();

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net != null)
			net.MatchLaunchReceived += OnNetMatchLaunch;
	}

	/// <summary>Load profile + campaign state for the manifest active/last-used slot.</summary>
	private void EnsureActiveSlotLoaded()
	{
		if (!SaveService.AnyOccupiedSlot())
		{
			// Fresh install: no auto-create — menu forces the create wizard.
			LoadEmptySession();
			return;
		}

		if (!SaveService.SlotOccupied(SaveService.ActiveSlot))
		{
			for (var i = 0; i < SaveService.MaxSlots; i++)
			{
				if (!SaveService.SlotOccupied(i))
					continue;
				LoadSlotIntoSession(i);
				return;
			}

			LoadEmptySession();
			return;
		}

		SaveService.SetActiveSlot(SaveService.ActiveSlot);
		LoadSlotIntoSession(SaveService.ActiveSlot);
	}

	private void LoadEmptySession()
	{
		ActiveBag = ProfileBagKind.Campaign;
		Profile = PlayerProfile.CreateNew();
		Campaign = null;
		SolarCampaign = new SolarCampaignRun();
		SolarCampaign.RefreshUnlocks();
		ClearTemporaryLoaners();
		CurrentClaim = VoidCorpsIdentity.PickClaimSite();
	}

	private void LoadSlotIntoSession(int slot)
	{
		SaveService.SetActiveSlot(slot);
		ActiveBag = ProfileBagKind.Campaign;
		Profile = SaveService.LoadCampaignProfile(slot);
		Profile.EnsureManufacturerState();
		SaveService.EnsureSkirmishBag(slot, Profile);
		PendingArenaPeriod = Profile.PreferredSkirmishArenaPeriod;
		CurrentClaim = VoidCorpsIdentity.PickClaimSite();
		SyncLoadoutFromProfile();
		Campaign = CampaignRun.Load();
		SolarCampaign = SolarCampaignRun.LoadOrNew();
		if (Campaign is { Alive: false })
			Campaign = null;
		ClearTemporaryLoaners();
		SaveService.UpdateSlotSummary(slot, Profile);
	}

	public bool SwitchToSlot(int slot)
	{
		slot = Mathf.Clamp(slot, 0, SaveService.MaxSlots - 1);
		if (!SaveService.SlotOccupied(slot))
			return false;

		DisconnectNetIfOnline();
		if (ActiveBag != ProfileBagKind.Campaign && SaveService.SlotOccupied(SaveService.ActiveSlot))
			SaveProfile();
		else if (SaveService.SlotOccupied(SaveService.ActiveSlot))
			SaveProfile();

		LoadSlotIntoSession(slot);
		return true;
	}

	/// <summary>Create a slot with handle only (faction still required via create wizard).</summary>
	public bool CreateSlot(int slot, string handle, bool makeActive = true)
		=> CreateSlot(slot, handle, FactionId.None, 0, makeActive);

	public bool CreateSlot(
		int slot,
		string handle,
		FactionId faction,
		int portraitIndex,
		bool makeActive = true)
	{
		slot = Mathf.Clamp(slot, 0, SaveService.MaxSlots - 1);
		if (SaveService.SlotOccupied(slot))
			return false;
		if (!SaveService.IsValidHandle(handle))
			return false;

		DisconnectNetIfOnline();
		if (SaveService.AnyOccupiedSlot() && SaveService.SlotOccupied(SaveService.ActiveSlot))
			SaveProfile();

		SaveService.EnsureSlotDir(slot);
		SaveService.SetActiveSlot(slot);
		ActiveBag = ProfileBagKind.Campaign;
		Profile = PlayerProfile.CreateNew(handle);
		Profile.SetAccountHandle(handle);
		if (faction is FactionId.Cat or FactionId.Dog)
			Profile.SetFaction(faction, portraitIndex, force: true);
		Campaign = null;
		CampaignRun.ClearSave();
		CampaignRun.ClearSolarOnboardingSave();
		SaveService.DeleteFileIfExists(SaveService.SolarCampaignPath(slot));
		SaveService.DeleteFileIfExists(SaveService.RoguelikePath(slot));
		SaveService.DeleteFileIfExists(SaveService.SkirmishPath(slot));
		SolarCampaign = SolarCampaignRun.LoadOrNew();
		ClearTemporaryLoaners();
		SaveService.SaveActiveProfile(Profile);
		SaveService.EnsureSkirmishBag(slot, Profile);
		SkirmishBagSync.MirrorUnlocks(slot);
		SolarCampaign.Save();
		PendingCreateSlot = -1;
		return true;
	}

	public bool RenameSlot(int slot, string handle)
	{
		slot = Mathf.Clamp(slot, 0, SaveService.MaxSlots - 1);
		if (!SaveService.IsValidHandle(handle))
			return false;
		if (!SaveService.SlotOccupied(slot))
			return false;

		handle = SaveService.SanitizeHandle(handle);
		if (slot == SaveService.ActiveSlot)
		{
			ActivateCampaignProfile();
			Profile.SetAccountHandle(handle);
			SaveProfile();
			SyncIdentityToSiblingBags(slot);
			return true;
		}

		var previous = SaveService.ActiveSlot;
		var profile = SaveService.LoadCampaignProfile(slot);
		profile.SetAccountHandle(handle);
		SaveService.Save(profile, SaveService.CampaignProfilePath(slot));
		SaveService.UpdateSlotSummary(slot, profile);
		SyncIdentityToSiblingBags(slot, profile);
		SaveService.SetActiveSlot(previous);
		return true;
	}

	public bool CopySlot(int from, int to)
	{
		from = Mathf.Clamp(from, 0, SaveService.MaxSlots - 1);
		to = Mathf.Clamp(to, 0, SaveService.MaxSlots - 1);
		if (from == to || !SaveService.SlotOccupied(from))
			return false;
		if (SaveService.SlotOccupied(to))
			return false;

		if (from == SaveService.ActiveSlot)
			SaveProfile();

		SaveService.DeleteSlotFiles(to);
		SaveService.CopySlotFiles(from, to);
		var copied = SaveService.LoadCampaignProfile(to);
		SaveService.UpdateSlotSummary(to, copied);
		return true;
	}

	public bool DeleteSlot(int slot)
	{
		slot = Mathf.Clamp(slot, 0, SaveService.MaxSlots - 1);
		if (!SaveService.SlotOccupied(slot))
			return false;

		DisconnectNetIfOnline();
		var deletingActive = slot == SaveService.ActiveSlot;
		if (deletingActive)
			ActiveBag = ProfileBagKind.Campaign;

		SaveService.DeleteSlotFiles(slot);

		if (!deletingActive)
			return true;

		for (var i = 0; i < SaveService.MaxSlots; i++)
		{
			if (!SaveService.SlotOccupied(i))
				continue;
			LoadSlotIntoSession(i);
			return true;
		}

		LoadEmptySession();
		return true;
	}

	public void SyncIdentityAfterCreate()
		=> SyncIdentityToSiblingBags(SaveService.ActiveSlot, Profile);

	private void SyncIdentityToSiblingBags(int slot, PlayerProfile? identity = null)
	{
		identity ??= SaveService.LoadCampaignProfile(slot);
		var handle = identity.ResolveAccountHandle();

		void Patch(string path)
		{
			if (!Godot.FileAccess.FileExists(path))
				return;
			var bag = SaveService.LoadOrNew(path);
			bag.SetAccountHandle(handle);
			if (identity.HasFaction)
				bag.CopyFactionIdentityFrom(identity);
			SaveService.Save(bag, path);
		}

		Patch(SaveService.RoguelikePath(slot));
		SaveService.EnsureSkirmishBag(slot, identity);
		Patch(SaveService.SkirmishPath(slot));
	}

	private void DisconnectNetIfOnline()
	{
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { IsOnline: true })
			net.DisconnectSession();
	}

	public void ActivateRoguelikeProfile()
	{
		if (ActiveBag == ProfileBagKind.Roguelike)
			return;
		PersistActiveBagToDisk();
		var path = SaveService.RoguelikePath(SaveService.ActiveSlot);
		if (!Godot.FileAccess.FileExists(path))
		{
			ActivateCampaignProfile();
			SaveService.Save(Profile, path);
		}

		Profile = SaveService.LoadOrNew(path);
		ActiveBag = ProfileBagKind.Roguelike;
		SyncLoadoutFromProfile();
	}

	public void ActivateSkirmishProfile()
	{
		if (ActiveBag == ProfileBagKind.Skirmish)
			return;
		PersistActiveBagToDisk();
		var slot = SaveService.ActiveSlot;
		var campaign = SaveService.LoadCampaignProfile(slot);
		SaveService.EnsureSkirmishBag(slot, campaign);
		SkirmishBagSync.MirrorUnlocks(slot);
		Profile = SaveService.LoadOrNew(SaveService.SkirmishPath(slot));
		ActiveBag = ProfileBagKind.Skirmish;
		SyncLoadoutFromProfile();
	}

	public void ActivateCampaignProfile() => ActivateMainProfile();

	public void ActivateMainProfile()
	{
		if (ActiveBag == ProfileBagKind.Campaign)
			return;
		PersistActiveBagToDisk();
		Profile = SaveService.LoadCampaignProfile(SaveService.ActiveSlot);
		ActiveBag = ProfileBagKind.Campaign;
		SyncLoadoutFromProfile();
	}

	private void PersistActiveBagToDisk()
	{
		if (!SaveService.SlotOccupied(SaveService.ActiveSlot) && ActiveBag == ProfileBagKind.Campaign)
			return;

		switch (ActiveBag)
		{
			case ProfileBagKind.Roguelike:
				SaveService.Save(Profile, SaveService.RoguelikePath(SaveService.ActiveSlot));
				break;
			case ProfileBagKind.Skirmish:
				SaveService.Save(Profile, SaveService.SkirmishPath(SaveService.ActiveSlot));
				break;
			default:
				if (SaveService.SlotOccupied(SaveService.ActiveSlot) || Profile.HasFaction)
					SaveService.SaveActiveProfile(Profile);
				break;
		}
	}

	private void OnNetMatchLaunch()
	{
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var payload = net?.PendingLaunch;
		if (payload == null || net == null)
			return;

		if (payload.ContainsKey("mp_mode"))
			MultiplayerGameMode = (MultiplayerGameMode)payload["mp_mode"].AsInt32();
		if (payload.ContainsKey("roster") && payload["roster"].VariantType == Variant.Type.Array)
			ApplyLobbyRoster(payload["roster"].AsGodotArray());

		var scene = payload.ContainsKey("scene") ? payload["scene"].AsString() : "arena";
		if (scene == "campaign")
		{
			if (net.Mode != NetSession.NetMode.Hosting)
			{
				CoopMatch = true;
				ReturnToCampaignMap = true;
				MatchFromCampaign = false;
			}

			net.ClearPendingLaunch();
			GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
			return;
		}

		ApplyLaunchPayload(payload);
		net.ClearPendingLaunch();
		GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
	}

	public void ApplyLobbyRoster(Godot.Collections.Array arr)
	{
		LobbyRoster.Clear();
		foreach (var item in arr)
		{
			if (item.VariantType == Variant.Type.Dictionary)
				LobbyRoster.Add(LobbySlot.FromDict(item.AsGodotDictionary()));
		}
	}

	public void ApplyLaunchPayload(Godot.Collections.Dictionary payload)
	{
		var fromCampaign = payload.ContainsKey("from_campaign") && payload["from_campaign"].AsBool();
		var scene = payload.ContainsKey("scene") ? payload["scene"].AsString() : "arena";
		var skirmishLaunch = !fromCampaign && scene != "campaign";

		if (payload.ContainsKey("mp_mode"))
			MultiplayerGameMode = (MultiplayerGameMode)payload["mp_mode"].AsInt32();
		if (payload.ContainsKey("roster") && payload["roster"].VariantType == Variant.Type.Array)
			ApplyLobbyRoster(payload["roster"].AsGodotArray());

		if (skirmishLaunch)
		{
			ClearCadetLoaner();
			ClearConventionDemoLoaner();
			if (SkirmishLoaner == null)
				SetSkirmishPremade(0);
			ActivateSkirmishProfile();
			MatchFromSkirmish = true;
			MatchFromAcademy = false;
			MatchFromConvention = false;
			MatchFromSolarCampaign = false;
		}
		else
		{
			ClearSkirmishLoaner();
			MatchFromSkirmish = false;
		}

		if (!EnsureDeployableLoadout("net launch"))
			return;

		var claimCode = payload.ContainsKey("claim") ? payload["claim"].AsString() : "";
		foreach (var claim in VoidCorpsIdentity.ClaimSites)
		{
			if (claim.Code == claimCode)
			{
				CurrentClaim = claim;
				break;
			}
		}

		PendingMission = payload.ContainsKey("mission")
			? (MissionType)payload["mission"].AsInt32()
			: MissionType.DestroyAllEnemies;
		PendingDifficulty = payload.ContainsKey("diff")
			? (PilotDifficulty)payload["diff"].AsInt32()
			: PilotDifficulty.Easy;
		PendingBossEncounter = payload.ContainsKey("boss")
			? (BossEncounterId)payload["boss"].AsInt32()
			: BossEncounterId.None;
		PendingRivalPilotId = payload.ContainsKey("rival_pilot") ? payload["rival_pilot"].AsString() : "";
		PendingRivalCorpId = payload.ContainsKey("rival_corp") ? payload["rival_corp"].AsString() : "";
		LastMissionManufacturerId = payload.ContainsKey("mfg") ? payload["mfg"].AsString() : "";
		MatchFromCampaign = fromCampaign;

		CoopMatch = true;
		ReturnToCampaignMap = MatchFromCampaign;
		PendingOfferIndex = -1;
		BeginMatchSession(Profile.LivesBank);
		LaunchSkirmishOnArenaLoad = true;
	}

	public Godot.Collections.Dictionary BuildLaunchPayload(bool fromCampaign, string scene = "arena")
	{
		var payload = new Godot.Collections.Dictionary
		{
			["scene"] = scene,
			["claim"] = CurrentClaim.Code,
			["mission"] = (int)PendingMission,
			["diff"] = (int)PendingDifficulty,
			["boss"] = (int)PendingBossEncounter,
			["rival_pilot"] = PendingRivalPilotId,
			["rival_corp"] = PendingRivalCorpId,
			["mfg"] = LastMissionManufacturerId,
			["from_campaign"] = fromCampaign,
			["mp_mode"] = (int)MultiplayerGameMode,
			["pvp"] = IsPvpMatch
		};

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { IsOnline: true } && net.OccupiedSlotCount() > 0)
			payload["roster"] = net.BuildRosterPayload();
		else if (LobbyRoster.Count > 0)
		{
			var arr = new Godot.Collections.Array();
			foreach (var slot in LobbyRoster)
				arr.Add(slot.ToDict());
			payload["roster"] = arr;
		}

		return payload;
	}

	public void BeginSkirmish()
	{
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		if (SkirmishLoaner == null)
		{
			GD.PushError("GameSession: refused skirmish — no premade selected.");
			return;
		}

		ActivateSkirmishProfile();
		if (!EnsureDeployableLoadout("skirmish"))
			return;

		ReturnToCampaignMap = false;
		ReturnToSolarMap = false;
		MatchFromCampaign = false;
		MatchFromSolarCampaign = false;
		MatchFromAcademy = false;
		MatchFromConvention = false;
		MatchFromSkirmish = true;
		CoopMatch = false;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		BeginMatchSession(Profile.LivesBank);
		LaunchSkirmishOnArenaLoad = true;
	}

	public void BeginCoopSkirmish()
	{
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		if (SkirmishLoaner == null)
		{
			GD.PushError("GameSession: refused coop skirmish — no premade selected.");
			return;
		}

		ActivateSkirmishProfile();
		if (!EnsureDeployableLoadout("coop skirmish"))
			return;

		ReturnToCampaignMap = false;
		ReturnToSolarMap = false;
		MatchFromCampaign = false;
		MatchFromSolarCampaign = false;
		MatchFromAcademy = false;
		MatchFromConvention = false;
		MatchFromSkirmish = true;
		CoopMatch = true;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		BeginMatchSession(Profile.LivesBank);
		LaunchSkirmishOnArenaLoad = true;
	}

	public void SyncLoadoutFromProfile()
	{
		Profile.Loadout = GameCatalog.SanitizeLoadout(Profile.Loadout.Clone());
	}

	/// <summary>
	/// Sanitize active loadout and refuse deploy when still power-illegal.
	/// Weight overload never blocks deploy.
	/// </summary>
	private bool EnsureDeployableLoadout(string context)
	{
		GameCatalog.EnsureBuilt();
		if (ConventionDemoLoaner != null)
			ConventionDemoLoaner = GameCatalog.SanitizeLoadout(ConventionDemoLoaner.Clone());
		else if (CadetLoaner != null)
			CadetLoaner = GameCatalog.SanitizeLoadout(CadetLoaner.Clone());
		else if (SkirmishLoaner != null)
			SkirmishLoaner = GameCatalog.SanitizeLoadout(SkirmishLoaner.Clone());
		else
			Profile.Loadout = GameCatalog.SanitizeLoadout(Profile.Loadout.Clone());

		var loadout = CurrentLoadout;
		if (GameCatalog.IsPowerLegal(loadout))
			return true;

		GD.PushError(
			$"GameSession: refused {context} — power overbudget " +
			$"({GameCatalog.SumPowerRequirements(loadout):0}/{GameCatalog.GetCoreCapacity(loadout):0}).");
		return false;
	}

	public void SetLoadout(LoadoutData loadout)
	{
		if (ConventionDemoLoaner != null)
		{
			ConventionDemoLoaner = GameCatalog.SanitizeLoadout(loadout.Clone());
			return;
		}

		if (CadetLoaner != null)
		{
			CadetLoaner = GameCatalog.SanitizeLoadout(loadout.Clone());
			return;
		}

		if (SkirmishLoaner != null)
		{
			SkirmishLoaner = GameCatalog.SanitizeLoadout(loadout.Clone());
			return;
		}

		Profile.Loadout = GameCatalog.SanitizeLoadout(loadout.Clone());
		Profile.GrantLoadoutOwnership(Profile.Loadout);
	}

	public void SetSkirmishPremade(int variant)
	{
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		var premade = GameCatalog.CreateSkirmishPremade(variant);
		SkirmishPremadeVariant = premade.Variant;
		SkirmishLoaner = premade.Loadout.Clone();
	}

	public void SetClaim(VoidCorpsIdentity.ClaimSite claim)
	{
		CurrentClaim = claim;
	}

	public void BeginSolarCampaign(bool reset = false)
	{
		ActivateCampaignProfile();
		if (reset)
		{
			SaveService.DeleteFileIfExists(SolarCampaignRun.SavePath);
			CampaignRun.ClearSolarOnboardingSave();
		}

		SolarCampaign = SolarCampaignRun.LoadOrNew();
		SolarCampaign.EnsureCompanies();
		if (!SolarCampaign.OnboardingComplete)
		{
			Campaign = CampaignRun.Load(solarOnboarding: true)
				?? CampaignRun.StartCadet(solarOnboarding: true);
			CadetLoaner = Campaign.Phase == CampaignPhase.CadetProgram
				? GameCatalog.CreateCadetLoanerLoadout()
				: null;
		}
		ReturnToSolarMap = SolarCampaign.OnboardingComplete;
		ReturnToCampaignMap = false;
		MatchFromSolarCampaign = false;
		MatchFromCampaign = false;
		MatchFromSkirmish = false;
		ClearTemporaryLoaners();
		SaveProfile();
	}

	public void LaunchSolarOnboarding()
	{
		if (!InSolarOnboarding)
			return;
		if (Campaign!.Phase == CampaignPhase.ManufacturerConvention)
		{
			ReturnToConventionHall = true;
			return;
		}
		ResumeCadetIfNeeded();
		if (!MatchFromAcademy)
			BeginAcademyRange();
	}

	/// <summary>
	/// Fresh solar campaign that skips cadet tutorials and opens the employer convention.
	/// </summary>
	public void BeginSolarCampaignSkipToConvention()
	{
		BeginSolarCampaign(reset: true);
		if (Campaign == null)
			Campaign = CampaignRun.StartCadet(solarOnboarding: true);
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		Campaign.SolarOnboarding = true;
		Campaign.EnterConventionGate();
		MatchFromAcademy = false;
		MatchFromCampaign = false;
		MatchFromConvention = false;
		MatchFromSolarCampaign = false;
		ReturnToCampaignMap = false;
		ReturnToSolarMap = false;
		ReturnToConventionHall = true;
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		SaveProfile();
		Campaign.Save();
	}

	public bool LaunchSolarLocation(string locationId, int missionIndex = 0)
	{
		var location = SolarSystemCatalog.Get(locationId);
		if (location == null
		    || location.Kind != SolarLocationKind.Operation
		    || !SolarCampaign.IsUnlocked(locationId)
		    || location.Missions.Count == 0
		    || !EnsureDeployableLoadout("solar campaign deploy"))
			return false;

		SolarCampaign.SelectedLocationId = locationId;
		SolarCampaign.Save();
		PendingSolarLocationId = locationId;
		PendingMission = location.Missions[Mathf.Clamp(missionIndex, 0, location.Missions.Count - 1)];
		PendingDifficulty = location.ThreatTier switch
		{
			1 => PilotDifficulty.Easy,
			2 => PilotDifficulty.Medium,
			_ => PilotDifficulty.Hard
		};
		PendingBossEncounter = PendingMission == MissionType.BossEncounter
			? BossEncounterCatalog.ForSectorClaim(location.ClaimCode)
			: BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = missionIndex;
		LastMissionManufacturerId = "";
		ReturnToSolarMap = true;
		ReturnToCampaignMap = false;
		ActivateCampaignProfile();
		MatchFromSolarCampaign = true;
		MatchFromCampaign = false;
		MatchFromAcademy = false;
		MatchFromConvention = false;
		MatchFromSkirmish = false;
		CoopMatch = GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true };

		foreach (var claim in VoidCorpsIdentity.ClaimSites)
		{
			if (claim.Code == location.ClaimCode)
			{
				CurrentClaim = claim;
				break;
			}
		}

		BeginMatchSession(Profile.LivesBank);
		LaunchSkirmishOnArenaLoad = true;
		return true;
	}

	public void OnSolarMissionResolved(MatchOutcome outcome)
	{
		var location = SolarSystemCatalog.Get(PendingSolarLocationId);
		if (location == null)
			return;

		if (outcome == MatchOutcome.Victory)
		{
			LocationLootService.Roll(SolarCampaign, location, Match);
			if (PendingMission == MissionType.Escort)
				SolarCampaign.MiningConvoysCompleted++;
			SolarCampaign.Complete(location.Id);
		}

		ReturnToSolarMap = true;
	}

	public void BeginCampaignRun(int sectorIndex = 0)
	{
		SeedRoguelikeIdentityFromMain();
		UsingRoguelikeProfile = true;
		ClearTemporaryLoaners();
		Campaign = CampaignRun.StartNew(sectorIndex);
		ReturnToCampaignMap = true;
		ReturnToConventionHall = false;
		MatchFromCampaign = false;
		MatchFromAcademy = false;
		MatchFromConvention = false;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		ApplyLocationClaim(Campaign.CurrentNode);
		SaveProfile();
	}

	/// <summary>Start MAP Cadet Program at the certification range.</summary>
	public void BeginCadetProgram()
	{
		SeedRoguelikeIdentityFromMain();
		UsingRoguelikeProfile = true;
		CampaignRun.ClearSave();
		Campaign = CampaignRun.StartCadet();
		CadetLoaner = GameCatalog.CreateCadetLoanerLoadout();
		MatchFromAcademy = true;
		MatchFromCampaign = false;
		CoopMatch = false;
		ReturnToCampaignMap = false;
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		CurrentClaim = VoidCorpsIdentity.ClaimSites[0];
		SaveProfile();
		BeginAcademyRange();
	}

	/// <summary>Start a fresh campaign at the Big Four convention, skipping academy tutorial beats.</summary>
	public void BeginConventionProgram()
	{
		SeedRoguelikeIdentityFromMain();
		UsingRoguelikeProfile = true;
		CampaignRun.ClearSave();
		ClearTemporaryLoaners();
		Campaign = CampaignRun.StartCadet();
		Campaign.EnterConventionGate();
		MatchFromAcademy = false;
		MatchFromCampaign = false;
		MatchFromConvention = false;
		CoopMatch = false;
		ReturnToCampaignMap = false;
		ReturnToConventionHall = false;
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		CurrentClaim = VoidCorpsIdentity.ClaimSites[0];
		SaveProfile();
	}

	public void ResumeCadetIfNeeded()
	{
		if (Campaign is not { Phase: CampaignPhase.CadetProgram, Alive: true })
			return;
		CadetLoaner ??= GameCatalog.CreateCadetLoanerLoadout();
		switch (Campaign.AcademyStep)
		{
			case AcademyStep.Range:
				BeginAcademyRange();
				break;
			case AcademyStep.LiveFire:
				BeginAcademyLiveFire();
				break;
			case AcademyStep.Graduation:
				OpenAcademyGraduation = true;
				break;
			default:
				Campaign.EnterConventionGate();
				ClearCadetLoaner();
				ClearSkirmishLoaner();
				ReturnToCampaignMap = true;
				break;
		}
	}

	public void BeginAcademyRange()
	{
		if (Campaign != null)
			Campaign.AcademyStep = AcademyStep.Range;
		CadetLoaner ??= GameCatalog.CreateCadetLoanerLoadout();
		PendingMission = MissionType.CadetRange;
		PendingDifficulty = PilotDifficulty.Easy;
		PendingBossEncounter = BossEncounterId.None;
		MatchFromAcademy = true;
		MatchFromCampaign = false;
		ReturnToCampaignMap = false;
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		CurrentClaim = VoidCorpsIdentity.ClaimSites[0];
		BeginMatchSession(99);
		LaunchSkirmishOnArenaLoad = true;
		Campaign?.Save();
	}

	public void BeginAcademyLiveFire()
	{
		if (Campaign != null)
			Campaign.AcademyStep = AcademyStep.LiveFire;
		CadetLoaner ??= GameCatalog.CreateCadetLoanerLoadout();
		var pool = new[]
		{
			MissionType.SearchAndDestroy,
			MissionType.DestroyAllEnemies,
			MissionType.CaptureArea
		};
		PendingMission = pool[(int)(Time.GetTicksMsec() % (ulong)pool.Length)];
		PendingDifficulty = PilotDifficulty.Easy;
		PendingBossEncounter = BossEncounterId.None;
		MatchFromAcademy = true;
		MatchFromCampaign = false;
		ReturnToCampaignMap = false;
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		CurrentClaim = VoidCorpsIdentity.ClaimSites[1 % VoidCorpsIdentity.ClaimSites.Length];
		BeginMatchSession(Profile.LivesBank > 0 ? Profile.LivesBank : MatchSession.StartingLives);
		LaunchSkirmishOnArenaLoad = true;
		Campaign?.Save();
	}

	public void OnAcademyMissionResolved(MatchOutcome outcome)
	{
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		ReturnToCampaignMap = false;

		if (Campaign == null)
			return;

		if (Campaign.AcademyStep == AcademyStep.Range)
		{
			if (outcome == MatchOutcome.Victory)
			{
				Campaign.AcademyStep = AcademyStep.LiveFire;
				Campaign.Save();
				LaunchAcademyContinue = true;
			}
			else
			{
				// Range cannot fail in practice; still recover.
				Campaign.RestartCadetFromRange();
				LaunchAcademyContinue = true;
			}

			return;
		}

		if (Campaign.AcademyStep == AcademyStep.LiveFire)
		{
			if (outcome == MatchOutcome.Victory)
			{
				Campaign.AcademyStep = AcademyStep.Graduation;
				Campaign.Save();
				OpenAcademyGraduation = true;
			}
			else
			{
				Campaign.RestartCadetFromRange();
				LaunchAcademyContinue = true;
			}
		}
	}

	public void CompleteAcademyGraduation()
	{
		ClearTemporaryLoaners();
		Campaign ??= CampaignRun.StartCadet();
		Campaign.EnterConventionGate();
		MatchFromAcademy = false;
		MatchFromCampaign = false;
		MatchFromConvention = false;
		ReturnToCampaignMap = !Campaign.SolarOnboarding;
		ReturnToConventionHall = Campaign.SolarOnboarding;
		OpenAcademyGraduation = false;
		LaunchAcademyContinue = false;
		SaveProfile();
	}

	public void ClearCadetLoaner() => CadetLoaner = null;
	public void ClearConventionDemoLoaner() => ConventionDemoLoaner = null;
	public void ClearSkirmishLoaner()
	{
		SkirmishLoaner = null;
		SkirmishPremadeVariant = -1;
	}

	public void ClearTemporaryLoaners()
	{
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		ClearSkirmishLoaner();
	}

	/// <summary>Open convention hall from the sector gate (advance onto convention node).</summary>
	public bool EnterConventionHallFromMap(string nodeId)
	{
		if (Campaign is not { Phase: CampaignPhase.ManufacturerConvention, Alive: true })
			return false;

		var node = Campaign.Graph.Get(nodeId);
		if (node == null)
			return false;

		if (Campaign.CurrentNodeId != nodeId && !Campaign.TryAdvanceTo(nodeId))
			return false;

		Campaign.Convention.EnsureAllManufacturers();
		Campaign.Save();
		ReturnToConventionHall = true;
		ReturnToCampaignMap = false;
		MatchFromConvention = false;
		return true;
	}

	public bool BeginManufacturerTrial(string manufacturerId)
	{
		if (Campaign is not { Phase: CampaignPhase.ManufacturerConvention, Alive: true })
			return false;

		ConventionCatalog.EnsureBuilt();
		var status = Campaign.Convention.Get(manufacturerId);
		if (status.Withdrawn || status.AttemptsRemaining <= 0)
			return false;

		var company = Campaign.SolarOnboarding ? GetFrontierCompany(manufacturerId) : null;
		var def = company?.TrialTemplate ?? ConventionCatalog.Get(manufacturerId);
		status.AttemptsRemaining--;
		Campaign.Convention.ActiveTrialManufacturerId = manufacturerId;
		Campaign.Convention.ActiveTrialSabotaged = false;
		Campaign.Save();

		ClearCadetLoaner();
		ClearSkirmishLoaner();
		ConventionDemoLoaner = def.DemoLoaner.Clone();
		PendingMission = def.TrialMission;
		PendingDifficulty = PilotDifficulty.Easy;
		PendingBossEncounter = BossEncounterId.None;
		LastMissionManufacturerId = manufacturerId;
		MatchFromConvention = true;
		MatchFromCampaign = false;
		MatchFromAcademy = false;
		ReturnToConventionHall = true;
		ReturnToCampaignMap = false;
		LaunchAcademyContinue = false;
		OpenAcademyGraduation = false;
		CoopMatch = false;
		CurrentClaim = VoidCorpsIdentity.ClaimSites[0];
		BeginMatchSession(Profile.LivesBank > 0 ? Profile.LivesBank : MatchSession.StartingLives);
		LaunchSkirmishOnArenaLoad = true;
		return true;
	}

	public void NotifyDemoCradleDestroyed(string manufacturerId)
	{
		if (Campaign == null || string.IsNullOrEmpty(manufacturerId))
			return;
		Campaign.Convention.ActiveTrialSabotaged = true;
		Campaign.Convention.Get(manufacturerId).Sabotaged = true;
		Campaign.Save();
	}

	public void OnConventionTrialResolved(MatchOutcome outcome)
	{
		ReturnToConventionHall = true;
		ReturnToCampaignMap = false;
		if (Campaign == null)
			return;

		var id = Campaign.Convention.ActiveTrialManufacturerId;
		if (string.IsNullOrEmpty(id))
			id = LastMissionManufacturerId;

		var status = Campaign.Convention.Get(id);
		if (Campaign.Convention.ActiveTrialSabotaged)
			status.Sabotaged = true;

		if (outcome == MatchOutcome.Victory)
		{
			status.Qualified = true;
			status.Withdrawn = false;
			Profile.Scrap += 15;
		}
		else if (status.AttemptsRemaining <= 0 && !status.Qualified)
		{
			status.Withdrawn = true;
		}

		Campaign.Convention.ActiveTrialManufacturerId = "";
		Campaign.Convention.ActiveTrialSabotaged = false;
		ClearConventionDemoLoaner();
		// Keep MatchFromConvention true until results Continue routes back to the hall.
		Campaign.Save();
		SaveProfile();
	}

	public bool SignManufacturer(string manufacturerId)
	{
		if (Campaign is not { Phase: CampaignPhase.ManufacturerConvention, Alive: true })
			return false;

		var status = Campaign.Convention.Get(manufacturerId);
		if (!status.Qualified || status.Withdrawn)
			return false;

		ConventionCatalog.EnsureBuilt();
		var company = Campaign.SolarOnboarding ? GetFrontierCompany(manufacturerId) : null;
		var def = company?.TrialTemplate ?? ConventionCatalog.Get(manufacturerId);
		ClearConventionDemoLoaner();
		ClearCadetLoaner();
		ClearSkirmishLoaner();
		// Signing package replaces the barren pre-contract kit.
		if (!Campaign.SolarOnboarding)
		{
			Profile.OwnedInstances.Clear();
			Profile.EquippedInstanceIds.Clear();
		}
		Profile.Loadout = def.SigningLoadout.Clone();
		Profile.GrantLoadoutOwnership(Profile.Loadout);
		foreach (var partId in def.SigningBonusPartIds)
			Profile.Own(partId);
		Profile.Scrap = Campaign.SolarOnboarding
			? Profile.Scrap + def.SigningBonusScrap
			: def.SigningBonusScrap;
		Profile.LivesBank = PlayerProfile.StartingLives + Mathf.Max(0, def.SigningBonusLives);
		Profile.UnlockOwnedBlueprints();

		if (Campaign.SolarOnboarding && company != null)
		{
			Profile.AffiliatedManufacturerId = "";
			Profile.EmployerCompanyId = company.Id;
			Profile.EmployerCompanyName = company.DisplayName;
			SolarCampaign.SelectedCompanyId = company.Id;
			SolarCampaign.OnboardingComplete = true;
			SolarCampaign.Save();
			Campaign.Save();
			MatchFromConvention = false;
			ReturnToConventionHall = false;
			ReturnToCampaignMap = false;
			ReturnToSolarMap = true;
			SaveProfile();
			return true;
		}

		Profile.SetAffiliation(manufacturerId);
		Campaign.EnterActiveOperations();
		MatchFromConvention = false;
		ReturnToConventionHall = false;
		ReturnToCampaignMap = true;
		ApplyLocationClaim(Campaign.CurrentNode);
		SaveProfile();
		return true;
	}

	public bool AcceptPityContract()
	{
		if (Campaign is not { Phase: CampaignPhase.ManufacturerConvention, Alive: true })
			return false;
		if (!Campaign.Convention.AllWithdrawnWithNoQualify())
			return false;

		ClearConventionDemoLoaner();
		ClearCadetLoaner();
		ClearSkirmishLoaner();
		Campaign.Convention.PityContractUsed = true;
		if (Campaign.SolarOnboarding)
		{
			var company = SolarCampaign.ConventionCompanies[0];
			var fallback = ConventionCatalog.Get("trinova");
			Profile.Loadout = fallback.SigningLoadout.Clone();
			Profile.GrantLoadoutOwnership(Profile.Loadout);
			Profile.Scrap += 20;
			Profile.LivesBank = Mathf.Max(Profile.LivesBank, PlayerProfile.StartingLives);
			Profile.UnlockOwnedBlueprints();
			Profile.AffiliatedManufacturerId = "";
			Profile.EmployerCompanyId = company.Id;
			Profile.EmployerCompanyName = company.DisplayName;
			SolarCampaign.SelectedCompanyId = company.Id;
			SolarCampaign.OnboardingComplete = true;
			SolarCampaign.Save();
			Campaign.Save();
			ReturnToConventionHall = false;
			ReturnToCampaignMap = false;
			ReturnToSolarMap = true;
			SaveProfile();
			return true;
		}
		ConventionCatalog.ApplyPityPackage(Profile);
		Campaign.EnterActiveOperations();
		ReturnToConventionHall = false;
		ReturnToCampaignMap = true;
		ApplyLocationClaim(Campaign.CurrentNode);
		SaveProfile();
		return true;
	}

	public bool DeployCampaignNode(string nodeId, int offerIndex)
	{
		if (Campaign == null || !Campaign.Alive)
			return false;
		if (Campaign.Phase == CampaignPhase.ManufacturerConvention)
			return false; // Use EnterConventionHallFromMap / hall UI instead.
		if (!EnsureDeployableLoadout("campaign deploy"))
			return false;

		// Validate the offer on the target node before moving the run pointer.
		var target = Campaign.Graph.Get(nodeId);
		var offer = target?.GetOffer(offerIndex);
		if (target == null || offer == null)
			return false;
		if (!Campaign.TryAdvanceTo(nodeId))
			return false;

		var node = Campaign.CurrentNode;
		if (node == null)
			return false;

		ApplyLocationClaim(node);
		PendingOfferIndex = offerIndex;
		PendingMission = offer.MissionType;
		PendingDifficulty = offer.Difficulty;
		PendingBossEncounter = offer.BossEncounterId;
		PendingRivalPilotId = offer.RivalPilotId;
		PendingRivalCorpId = offer.RivalCorpId;
		LastMissionManufacturerId = offer.ManufacturerId;
		ReturnToCampaignMap = true;
		ReturnToConventionHall = false;
		MatchFromCampaign = true;
		MatchFromConvention = false;
		CoopMatch = GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true };
		BeginMatchSession(Profile.LivesBank);
		LaunchSkirmishOnArenaLoad = true;
		return true;
	}

	public void OnCampaignNodeResolved(MatchOutcome outcome)
	{
		if (Campaign == null)
			return;

		if (outcome == MatchOutcome.Victory)
		{
			ApplyMissionOfferReputation();
			Campaign.MarkCurrentCleared(PendingOfferIndex);
			var node = Campaign.CurrentNode;
			if (node is { Kind: CampaignNodeKind.Warning })
			{
				ApplyCampaignClaimReward();
				if (Campaign.TryAdvanceSectorOrComplete())
				{
					// Next sector map ready; keep kit.
					ReturnToCampaignMap = true;
				}
				else
				{
					// Sector 3 claim secured — run complete, kit kept until new campaign / death.
					Campaign = null;
					ReturnToCampaignMap = false;
				}
			}
		}
		else
		{
			// Mission failed: only wipe when the pilot is out of lives.
			if (Match.LivesRemaining <= 0 || Profile.LivesBank <= 0)
				WipeCampaignDeath();
			else
				ReturnToCampaignMap = true;
		}
	}

	/// <summary>Total life loss mid-campaign — wipe kit and clear the run.</summary>
	public void WipeCampaignDeath()
	{
		Profile.WipeRunInventory();
		Campaign?.EndRun();
		Campaign = null;
		CampaignRun.ClearSave();
		ClearTemporaryLoaners();
		ReturnToCampaignMap = false;
		ReturnToConventionHall = false;
		MatchFromCampaign = false;
		MatchFromAcademy = false;
		MatchFromConvention = false;
		SaveProfile();
	}

	private void ApplyMissionOfferReputation()
	{
		// Manufacturer reputation has been retired. The persistent campaign uses
		// scrap-funded manufacturer tech trees instead.
	}

	private void ApplyCampaignClaimReward()
	{
		if (Campaign == null)
			return;

		// Warning / Titan fights are rival-corp claim contests, not manufacturer contracts.
		var stipend = 20 + Mathf.Max(0, Match.RunScrap / 4);
		Profile.Scrap += stipend;
		Campaign.AddManufacturerPayout(stipend);
	}

	private void ApplyLocationClaim(CampaignNode? node)
	{
		if (node == null || string.IsNullOrEmpty(node.LocationClaimCode))
			return;

		foreach (var claim in VoidCorpsIdentity.ClaimSites)
		{
			if (claim.Code != node.LocationClaimCode)
				continue;

			// Sabotage corridor is mission-forced in ArenaController; never park it on a normal claim node.
			CurrentClaim = claim.SabotageOnly
				? VoidCorpsIdentity.PickClaimSite()
				: claim;
			return;
		}
	}

	private void BeginMatchSession(int livesBank)
	{
		MatchRewardsCommitted = false;
		Match.Begin(PendingDifficulty, livesBank, PendingMission);
	}

	public void SaveProfile()
	{
		switch (ActiveBag)
		{
			case ProfileBagKind.Roguelike:
				SaveService.Save(Profile, SaveService.RoguelikePath(SaveService.ActiveSlot));
				break;
			case ProfileBagKind.Skirmish:
				SaveService.Save(Profile, SaveService.SkirmishPath(SaveService.ActiveSlot));
				break;
			default:
				SaveService.SaveActiveProfile(Profile);
				break;
		}

		Campaign?.Save();
		SolarCampaign?.Save();
	}

	/// <summary>
	/// Persist run scrap, loot, recovery flags, and final equipped conditions.
	/// Called once from Damage Assessment (or Results when assessment is skipped).
	/// </summary>
	public bool MatchRewardsCommitted { get; private set; }

	public void ResetMatchCommitFlag() => MatchRewardsCommitted = false;

	public void CommitMatchRewards()
	{
		if (MatchRewardsCommitted)
			return;

		var scrapEarned = Match.RunScrap;
		var skirmishMatch = MatchFromSkirmish || ActiveBag == ProfileBagKind.Skirmish;

		if (skirmishMatch)
		{
			if (ActiveBag != ProfileBagKind.Skirmish)
				ActivateSkirmishProfile();
			Match.ApplyToProfile(Profile, trackSkirmishRecord: true);
			SetLoadout(Profile.Loadout);
			SaveProfile();
			MatchRewardsCommitted = true;
			return;
		}

		if (MatchFromAcademy || MatchFromConvention)
		{
			Profile.Scrap += scrapEarned;
			if (Match.MissionType != MissionType.CadetRange)
				Profile.LivesBank = Mathf.Max(0, Match.LivesRemaining);
		}
		else
		{
			Match.ApplyToProfile(Profile, trackSkirmishRecord: false);
			SetLoadout(Profile.Loadout);
		}

		if (MatchFromCampaign
		    && (Match.LivesRemaining <= 0 || Profile.LivesBank <= 0)
		    && Match.Outcome != MatchOutcome.Victory)
		{
			WipeCampaignDeath();
			if (scrapEarned > 0)
				SkirmishBagSync.AfterPersistentEarn(SaveService.ActiveSlot, scrapEarned);
			else
				SkirmishBagSync.MirrorUnlocks(SaveService.ActiveSlot);
			MatchRewardsCommitted = true;
			return;
		}

		SaveProfile();
		SkirmishBagSync.AfterPersistentEarn(SaveService.ActiveSlot, scrapEarned);
		MatchRewardsCommitted = true;
	}

	/// <summary>Wipe the active slot inventory/progress (slot stays occupied with same handle + faction).</summary>
	public void NewProfile()
	{
		ActivateCampaignProfile();
		var handle = Profile.ResolveAccountHandle();
		var faction = Profile.Faction;
		var portrait = Profile.PilotPortraitIndex;
		Profile = PlayerProfile.CreateNew(handle);
		Profile.SetAccountHandle(handle);
		if (faction is FactionId.Cat or FactionId.Dog)
			Profile.SetFaction(faction, portrait, force: true);
		CampaignRun.ClearSave();
		CampaignRun.ClearSolarOnboardingSave();
		SaveService.DeleteFileIfExists(SaveService.RoguelikePath(SaveService.ActiveSlot));
		SaveService.DeleteFileIfExists(SaveService.SkirmishPath(SaveService.ActiveSlot));
		SaveService.DeleteFileIfExists(SaveService.SolarCampaignPath(SaveService.ActiveSlot));
		SolarCampaign = SolarCampaignRun.LoadOrNew();
		Campaign = null;
		ClearTemporaryLoaners();
		SaveProfile();
		SaveService.EnsureSkirmishBag(SaveService.ActiveSlot, Profile);
		SkirmishBagSync.MirrorUnlocks(SaveService.ActiveSlot);
	}

	/// <summary>Max part tier allowed in shop/loot for the active context.</summary>
	public int CurrentMaxLootTier()
	{
		if (MatchFromSolarCampaign)
			return SolarSystemCatalog.Get(PendingSolarLocationId)?.ThreatTier ?? 1;
		if (Campaign is { Phase: CampaignPhase.ActiveOperations, Alive: true })
			return Campaign.MaxLootTier;
		if (MatchFromAcademy || MatchFromConvention || InCadetProgram || InConvention)
			return 1;
		// Skirmish / post-run: full catalog when no live sector gate.
		return CatalogTiers.MaxTier;
	}
}
