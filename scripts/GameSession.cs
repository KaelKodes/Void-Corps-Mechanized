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
		Match.Begin(PendingDifficulty, Profile.LivesBank, PendingMission);
		LaunchSkirmishOnArenaLoad = true;
	}

	public void BeginCampaignRun(int sectorIndex = 0)
	{
		Campaign = CampaignRun.StartNew(sectorIndex);
		ReturnToCampaignMap = true;
		MatchFromCampaign = false;
		ApplySectorClaim();
	}

	public bool DeployCampaignNode(string nodeId)
	{
		if (Campaign == null || !Campaign.Alive)
			return false;
		if (!Campaign.TryAdvanceTo(nodeId))
			return false;

		var node = Campaign.CurrentNode;
		if (node == null)
			return false;

		ApplySectorClaim();
		PendingMission = node.MissionType;
		PendingDifficulty = node.Difficulty;
		PendingBossEncounter = node.BossEncounterId;
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
			Campaign.MarkCurrentCleared();
			var node = Campaign.CurrentNode;
			if (node is { Kind: CampaignNodeKind.Warning })
			{
				// Advance to next sector or end run.
				var next = Campaign.SectorIndex + 1;
				if (next >= VoidCorpsIdentity.ClaimSites.Length)
				{
					Campaign.EndRun();
					Campaign = null;
					ReturnToCampaignMap = false;
				}
				else
				{
					Campaign = CampaignRun.StartNew(next, Campaign.Seed ^ (next * 9973));
					ReturnToCampaignMap = true;
				}
			}
		}
		else
		{
			Campaign.EndRun();
			Campaign = null;
			ReturnToCampaignMap = false;
		}
	}

	private void ApplySectorClaim()
	{
		if (Campaign == null)
			return;
		var idx = Mathf.Clamp(Campaign.SectorIndex, 0, VoidCorpsIdentity.ClaimSites.Length - 1);
		CurrentClaim = VoidCorpsIdentity.ClaimSites[idx];
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
