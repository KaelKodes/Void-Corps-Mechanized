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
	public LoadoutData CurrentLoadout => Profile.Loadout;
	public VoidCorpsIdentity.ClaimSite CurrentClaim { get; private set; }
	public PilotDifficulty PendingDifficulty { get; set; } = PilotDifficulty.Easy;
	public MissionType PendingMission { get; set; } = MissionType.DestroyAllEnemies;
	public BossEncounterId PendingBossEncounter { get; set; } = BossEncounterId.None;
	public int PendingOfferIndex { get; set; } = -1;
	/// <summary>Manufacturer that offered the last campaign mission (drives post-mission shop).</summary>
	public string LastMissionManufacturerId { get; set; } = "";
	public bool LaunchSkirmishOnArenaLoad { get; set; }
	/// <summary>When true, main menu opens directly on skirmish setup.</summary>
	public bool OpenSkirmishSetupOnMenu { get; set; }
	/// <summary>When true, continue from results returns to campaign map.</summary>
	public bool ReturnToCampaignMap { get; set; }
	/// <summary>True when the current/last match was launched from campaign.</summary>
	public bool MatchFromCampaign { get; set; }

	public bool InCampaign => Campaign is { Alive: true };

	public override void _Ready()
	{
		GameCatalog.EnsureBuilt();
		SupportCatalog.EnsureBuilt();
		InputBindings.EnsureDefaultsCaptured();
		Profile = SaveService.LoadOrNew();
		CurrentClaim = VoidCorpsIdentity.PickClaimSite();
		SyncLoadoutFromProfile();
		Campaign = CampaignRun.Load();
		if (Campaign is { Alive: false })
			Campaign = null;
	}

	public void SyncLoadoutFromProfile()
	{
		Profile.Loadout = GameCatalog.SanitizeMounts(Profile.Loadout.Clone());
	}

	public void SetLoadout(LoadoutData loadout)
	{
		Profile.Loadout = GameCatalog.SanitizeMounts(loadout.Clone());
		Profile.GrantLoadoutOwnership(Profile.Loadout);
	}

	public void SetClaim(VoidCorpsIdentity.ClaimSite claim)
	{
		CurrentClaim = claim;
	}

	public void BeginSkirmish()
	{
		ReturnToCampaignMap = false;
		MatchFromCampaign = false;
		PendingBossEncounter = BossEncounterId.None;
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		Match.Begin(PendingDifficulty, Profile.LivesBank, PendingMission);
		LaunchSkirmishOnArenaLoad = true;
	}

	public void BeginCampaignRun(int sectorIndex = 0)
	{
		Campaign = CampaignRun.StartNew(sectorIndex);
		ReturnToCampaignMap = true;
		MatchFromCampaign = false;
		PendingOfferIndex = -1;
		LastMissionManufacturerId = "";
		ApplyLocationClaim(Campaign.CurrentNode);
	}

	public bool DeployCampaignNode(string nodeId, int offerIndex)
	{
		if (Campaign == null || !Campaign.Alive)
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
		LastMissionManufacturerId = offer.ManufacturerId;
		ReturnToCampaignMap = true;
		MatchFromCampaign = true;
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
				Campaign.EndRun();
				Campaign = null;
				ReturnToCampaignMap = false;
			}
		}
		else
		{
			Campaign.EndRun();
			Campaign = null;
			ReturnToCampaignMap = false;
		}
	}

	private void ApplyMissionOfferReputation()
	{
		if (Campaign?.CurrentNode == null || PendingOfferIndex < 0)
			return;

		var offer = Campaign.CurrentNode.GetOffer(PendingOfferIndex);
		if (offer == null)
			return;

		Profile.AddReputation(offer.ManufacturerId, offer.RepGain);
		Profile.AddReputation(offer.RivalManufacturerId, -offer.RepLoss);
	}

	private void ApplyCampaignClaimReward()
	{
		if (Campaign == null)
			return;

		var stipend = 20 + Mathf.Max(0, Match.RunScrap / 4);
		Profile.Scrap += stipend;
		Campaign.AddManufacturerPayout(stipend);

		if (!string.IsNullOrEmpty(LastMissionManufacturerId))
			Profile.AddReputation(LastMissionManufacturerId, 2);
	}

	private void ApplyLocationClaim(CampaignNode? node)
	{
		if (node == null || string.IsNullOrEmpty(node.LocationClaimCode))
			return;

		foreach (var claim in VoidCorpsIdentity.ClaimSites)
		{
			if (claim.Code == node.LocationClaimCode)
			{
				CurrentClaim = claim;
				return;
			}
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
		SaveProfile();
	}
}
