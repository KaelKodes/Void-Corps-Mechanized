using Godot;

namespace Mechanize;

/// <summary>
/// Session-wide profile, claim, match, campaign, and Void Corps ops state.
/// </summary>
public partial class GameSession : Node
{
	public PlayerProfile Profile { get; private set; } = null!;
	public MatchSession Match { get; } = new();
	public CampaignRun? Campaign { get; set; }
	public LoadoutData? CadetLoaner { get; private set; }
	/// <summary>Convention trial demo chassis — mutually exclusive with cadet loaner.</summary>
	public LoadoutData? ConventionDemoLoaner { get; private set; }
	public LoadoutData CurrentLoadout => ConventionDemoLoaner ?? CadetLoaner ?? Profile.Loadout;
	public VoidCorpsIdentity.ClaimSite CurrentClaim { get; private set; }
	public PilotDifficulty PendingDifficulty { get; set; } = PilotDifficulty.Easy;
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
	/// <summary>Results continue should return to convention hall.</summary>
	public bool ReturnToConventionHall { get; set; }
	/// <summary>Studio boot bumper already played this app launch.</summary>
	public bool StudioIntroPlayed { get; set; }
	/// <summary>True when the current/last match was launched from campaign.</summary>
	public bool MatchFromCampaign { get; set; }
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

	public bool InCampaign => Campaign is { Alive: true };
	public bool InCadetProgram => Campaign is { Phase: CampaignPhase.CadetProgram, Alive: true };
	public bool InConvention => Campaign is { Phase: CampaignPhase.ManufacturerConvention, Alive: true };

	public override void _Ready()
	{
		GameCatalog.EnsureBuilt();
		SupportCatalog.EnsureBuilt();
		ConventionCatalog.EnsureBuilt();
		InputBindings.EnsureDefaultsCaptured();
		Profile = SaveService.LoadOrNew();
		CurrentClaim = VoidCorpsIdentity.PickClaimSite();
		SyncLoadoutFromProfile();
		Campaign = CampaignRun.Load();
		if (Campaign is { Alive: false })
			Campaign = null;

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net != null)
			net.MatchLaunchReceived += OnNetMatchLaunch;
	}

	private void OnNetMatchLaunch()
	{
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var payload = net?.PendingLaunch;
		if (payload == null || net == null)
			return;

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

	public void ApplyLaunchPayload(Godot.Collections.Dictionary payload)
	{
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
		MatchFromCampaign = payload.ContainsKey("from_campaign") && payload["from_campaign"].AsBool();
		CoopMatch = true;
		ReturnToCampaignMap = MatchFromCampaign;
		PendingOfferIndex = -1;
		Match.Begin(PendingDifficulty, Profile.LivesBank, PendingMission);
		LaunchSkirmishOnArenaLoad = true;
	}

	public Godot.Collections.Dictionary BuildLaunchPayload(bool fromCampaign, string scene = "arena")
	{
		return new Godot.Collections.Dictionary
		{
			["scene"] = scene,
			["claim"] = CurrentClaim.Code,
			["mission"] = (int)PendingMission,
			["diff"] = (int)PendingDifficulty,
			["boss"] = (int)PendingBossEncounter,
			["rival_pilot"] = PendingRivalPilotId,
			["rival_corp"] = PendingRivalCorpId,
			["mfg"] = LastMissionManufacturerId,
			["from_campaign"] = fromCampaign
		};
	}

	public void BeginSkirmish()
	{
		if (!EnsureDeployableLoadout("skirmish"))
			return;

		ReturnToCampaignMap = false;
		MatchFromCampaign = false;
		CoopMatch = false;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		Match.Begin(PendingDifficulty, Profile.LivesBank, PendingMission);
		LaunchSkirmishOnArenaLoad = true;
	}

	public void BeginCoopSkirmish()
	{
		if (!EnsureDeployableLoadout("coop skirmish"))
			return;

		ReturnToCampaignMap = false;
		MatchFromCampaign = false;
		CoopMatch = true;
		PendingBossEncounter = BossEncounterId.None;
		PendingRivalPilotId = "";
		PendingRivalCorpId = "";
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		Match.Begin(PendingDifficulty, Profile.LivesBank, PendingMission);
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

		Profile.Loadout = GameCatalog.SanitizeLoadout(loadout.Clone());
		Profile.GrantLoadoutOwnership(Profile.Loadout);
	}

	public void SetClaim(VoidCorpsIdentity.ClaimSite claim)
	{
		CurrentClaim = claim;
	}

	public void BeginCampaignRun(int sectorIndex = 0)
	{
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		Profile.WipeRunInventory();
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
		CampaignRun.ClearSave();
		Profile.WipeRunInventory();
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
		CampaignRun.ClearSave();
		Profile.WipeRunInventory();
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
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
		Match.Begin(PendingDifficulty, 99, PendingMission);
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
		Match.Begin(PendingDifficulty, Profile.LivesBank > 0 ? Profile.LivesBank : MatchSession.StartingLives, PendingMission);
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
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		Campaign ??= CampaignRun.StartCadet();
		Campaign.EnterConventionGate();
		MatchFromAcademy = false;
		MatchFromCampaign = false;
		MatchFromConvention = false;
		ReturnToCampaignMap = true;
		ReturnToConventionHall = false;
		OpenAcademyGraduation = false;
		LaunchAcademyContinue = false;
		SaveProfile();
	}

	public void ClearCadetLoaner() => CadetLoaner = null;
	public void ClearConventionDemoLoaner() => ConventionDemoLoaner = null;

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

		var def = ConventionCatalog.Get(manufacturerId);
		status.AttemptsRemaining--;
		Campaign.Convention.ActiveTrialManufacturerId = manufacturerId;
		Campaign.Convention.ActiveTrialSabotaged = false;
		Campaign.Save();

		ClearCadetLoaner();
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
		Match.Begin(PendingDifficulty, Profile.LivesBank > 0 ? Profile.LivesBank : MatchSession.StartingLives, PendingMission);
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
			Profile.AddReputation(id, 2);
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
		var def = ConventionCatalog.Get(manufacturerId);
		ClearConventionDemoLoaner();
		ClearCadetLoaner();
		// Signing package replaces the barren pre-contract kit.
		Profile.OwnedCounts.Clear();
		Profile.Loadout = def.SigningLoadout.Clone();
		Profile.GrantLoadoutOwnership(Profile.Loadout);
		foreach (var partId in def.SigningBonusPartIds)
			Profile.Own(partId);
		Profile.Scrap = def.SigningBonusScrap;
		Profile.LivesBank = PlayerProfile.StartingLives + Mathf.Max(0, def.SigningBonusLives);
		Profile.SetAffiliation(manufacturerId);
		Profile.AddReputation(manufacturerId, 5);

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
		ConventionCatalog.ApplyPityPackage(Profile);
		Campaign.Convention.PityContractUsed = true;
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
		if (!Campaign.TryAdvanceTo(nodeId))
			return false;

		var node = Campaign.CurrentNode;
		if (node == null)
			return false;

		var offer = node.GetOffer(offerIndex);
		if (offer == null)
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
		Match.Begin(PendingDifficulty, Profile.LivesBank, PendingMission);
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
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		ReturnToCampaignMap = false;
		ReturnToConventionHall = false;
		MatchFromCampaign = false;
		MatchFromAcademy = false;
		MatchFromConvention = false;
		SaveProfile();
	}

	private void ApplyMissionOfferReputation()
	{
		if (Campaign?.CurrentNode == null || PendingOfferIndex < 0)
			return;

		var offer = Campaign.CurrentNode.GetOffer(PendingOfferIndex);
		if (offer == null)
			return;
		if (offer.MissionType == MissionType.BossEncounter)
			return;

		if (!string.IsNullOrEmpty(offer.ManufacturerId) && offer.RepGain != 0)
			Profile.AddReputation(offer.ManufacturerId, offer.RepGain);
		if (!string.IsNullOrEmpty(offer.RivalManufacturerId) && offer.RepLoss != 0)
			Profile.AddReputation(offer.RivalManufacturerId, -offer.RepLoss);
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

	public void SaveProfile()
	{
		SaveService.Save(Profile);
		Campaign?.Save();
	}

	public void NewProfile()
	{
		Profile = PlayerProfile.CreateNew();
		CampaignRun.ClearSave();
		Campaign = null;
		ClearCadetLoaner();
		ClearConventionDemoLoaner();
		SaveProfile();
	}

	/// <summary>Max part tier allowed in shop/loot for the active context.</summary>
	public int CurrentMaxLootTier()
	{
		if (Campaign is { Phase: CampaignPhase.ActiveOperations, Alive: true })
			return Campaign.MaxLootTier;
		if (MatchFromAcademy || MatchFromConvention || InCadetProgram || InConvention)
			return 1;
		// Skirmish / post-run: full catalog when no live sector gate.
		return CatalogTiers.MaxTier;
	}
}
