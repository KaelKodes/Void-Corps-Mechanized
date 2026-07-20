using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

public enum MatchOutcome
{
	InProgress,
	Victory,
	Defeat
}

/// <summary>
/// Runtime skirmish state — lives, run scrap, loot bag, recovery cargo.
/// </summary>
public sealed class MatchSession
{
	public const int StartingLives = 2;
	public const int BaseLifeCost = 50;
	public const int LifeCostStep = 25;

	public PilotDifficulty Difficulty { get; set; } = PilotDifficulty.Easy;
	public MissionType MissionType { get; set; } = MissionType.DestroyAllEnemies;
	public int LivesRemaining { get; set; } = StartingLives;
	public int RunScrap { get; set; }
	public int LivesBoughtThisRun { get; set; }
	/// <summary>Loot part IDs collected this run (quantity-safe, for debrief UI).</summary>
	public List<string> RunPartDrops { get; } = new();
	/// <summary>Owned instances created from world loot pickups this run.</summary>
	public List<string> MissionCargoInstanceIds { get; } = new();
	/// <summary>Parts awarded by a location loot table and banked at debrief.</summary>
	public List<string> PendingPartRewards { get; } = new();
	/// <summary>Crafting materials awarded by mission/location loot tables.</summary>
	public Dictionary<string, int> RunMaterialDrops { get; } = new();
	/// <summary>Owned instances recovered by drones at mission end (field crates / unused deliveries).</summary>
	public List<string> RecoveryInstanceIds { get; } = new();
	/// <summary>Final equipped-slot condition snapshot captured at resolution.</summary>
	public Dictionary<PartSlot, PartCondition> FinalConditionBySlot { get; } = new();
	public MatchOutcome Outcome { get; set; } = MatchOutcome.InProgress;
	public bool Active { get; set; }
	public MatchTelemetry Telemetry { get; private set; } = new();

	public int NextLifeCost => BaseLifeCost + LivesBoughtThisRun * LifeCostStep;

	public void Begin(PilotDifficulty difficulty, int profileLivesBank, MissionType missionType = MissionType.DestroyAllEnemies)
	{
		Difficulty = difficulty;
		MissionType = missionType;
		LivesRemaining = Mathf.Max(1, Mathf.Min(StartingLives, profileLivesBank > 0 ? profileLivesBank : StartingLives));
		RunScrap = 0;
		LivesBoughtThisRun = 0;
		RunPartDrops.Clear();
		MissionCargoInstanceIds.Clear();
		PendingPartRewards.Clear();
		RunMaterialDrops.Clear();
		RecoveryInstanceIds.Clear();
		FinalConditionBySlot.Clear();
		Outcome = MatchOutcome.InProgress;
		Active = true;
		Telemetry = new MatchTelemetry();
	}

	public void AddScrap(int amount)
	{
		if (amount <= 0 || !Active)
			return;
		RunScrap += amount;
	}

	public void AddPartDrop(string partId)
	{
		if (string.IsNullOrEmpty(partId) || !Active)
			return;
		RunPartDrops.Add(partId);
	}

	/// <summary>Own a loot copy immediately so Field Hangar can request it; recovery-protected.</summary>
	public OwnedPartInstance? AddPartDropInstance(PlayerProfile profile, string partId)
	{
		if (string.IsNullOrEmpty(partId) || !Active || profile == null)
			return null;
		var instance = profile.OwnInstance(partId);
		RunPartDrops.Add(partId);
		if (!MissionCargoInstanceIds.Contains(instance.InstanceId))
			MissionCargoInstanceIds.Add(instance.InstanceId);
		TrackRecovery(instance.InstanceId);
		return instance;
	}

	public void AddRewardPart(string partId)
	{
		if (string.IsNullOrEmpty(partId))
			return;
		RunPartDrops.Add(partId);
		PendingPartRewards.Add(partId);
	}

	public void AddMaterialDrop(string materialId, int amount)
	{
		if (amount <= 0 || !MaterialCatalog.All.ContainsKey(materialId))
			return;
		RunMaterialDrops[materialId] = RunMaterialDrops.GetValueOrDefault(materialId) + amount;
	}

	public void TrackRecovery(string instanceId)
	{
		if (string.IsNullOrEmpty(instanceId) || RecoveryInstanceIds.Contains(instanceId))
			return;
		RecoveryInstanceIds.Add(instanceId);
	}

	public void UntrackRecovery(string instanceId)
	{
		if (!string.IsNullOrEmpty(instanceId))
			RecoveryInstanceIds.Remove(instanceId);
	}

	public void CaptureFinalCondition(Dictionary<PartSlot, PartCondition> snapshot)
	{
		FinalConditionBySlot.Clear();
		foreach (var (slot, condition) in snapshot)
			FinalConditionBySlot[slot] = condition.Clone();
	}

	public bool TrySpendLifeForRespawn()
	{
		if (!Active || LivesRemaining <= 1)
			return false;
		LivesRemaining--;
		return true;
	}

	public bool TryBuyLifeWithScrap()
	{
		var cost = NextLifeCost;
		if (RunScrap < cost)
			return false;
		RunScrap -= cost;
		LivesRemaining++;
		LivesBoughtThisRun++;
		return true;
	}

	public void ApplyHostSnapshot(int lives, int scrap, int nextLifeCostHint = -1)
	{
		LivesRemaining = Mathf.Max(0, lives);
		RunScrap = Mathf.Max(0, scrap);
		if (nextLifeCostHint >= 0 && BaseLifeCost > 0)
		{
			var steps = Mathf.Max(0, (nextLifeCostHint - BaseLifeCost) / LifeCostStep);
			LivesBoughtThisRun = steps;
		}
	}

	public void End(MatchOutcome outcome)
	{
		Outcome = outcome;
		Active = false;
	}

	public void Tick(float dt)
	{
		if (Active)
			Telemetry.Tick(dt);
	}

	/// <summary>Commit run rewards into the persistent profile (condition applied separately).</summary>
	public void ApplyToProfile(PlayerProfile profile)
	{
		profile.Scrap += RunScrap;
		// Mission cargo instances are already owned on pickup; only bank legacy ID-only drops.
		if (MissionCargoInstanceIds.Count == 0)
		{
			foreach (var id in RunPartDrops)
				profile.Own(id);
		}
		else
		{
			foreach (var id in PendingPartRewards)
				profile.Own(id);
		}

		foreach (var (materialId, amount) in RunMaterialDrops)
			profile.AddMaterial(materialId, amount);

		foreach (var instanceId in RecoveryInstanceIds)
		{
			var instance = profile.GetInstance(instanceId);
			if (instance != null)
				instance.Reserved = false;
		}

		profile.LivesBank = Mathf.Max(0, LivesRemaining);
		profile.SkirmishesPlayed++;
		if (Outcome == MatchOutcome.Victory)
			profile.SkirmishesWon++;

		foreach (var (slot, condition) in FinalConditionBySlot)
		{
			var equipped = profile.GetEquippedInstance(slot);
			if (equipped == null)
				continue;
			equipped.Condition = condition.Clone();
			equipped.Condition.EnsureSegmentCount(PartCondition.SegmentCountFor(GameCatalog.GetPart(equipped.PartId)));
		}
	}
}
