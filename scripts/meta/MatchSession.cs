using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum MatchOutcome
{
	InProgress,
	Victory,
	Defeat
}

/// <summary>
/// Runtime skirmish state — lives, run scrap, loot bag. Campaign will reuse this.
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
	public List<string> RunPartDrops { get; } = new();
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
		if (!RunPartDrops.Contains(partId))
			RunPartDrops.Add(partId);
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
			// Reconstruct buys so NextLifeCost matches host HUD.
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

	/// <summary>Commit run rewards into the persistent profile.</summary>
	public void ApplyToProfile(PlayerProfile profile)
	{
		profile.Scrap += RunScrap;
		foreach (var id in RunPartDrops)
			profile.Own(id);
		profile.LivesBank = Mathf.Max(0, LivesRemaining);
		profile.SkirmishesPlayed++;
		if (Outcome == MatchOutcome.Victory)
			profile.SkirmishesWon++;
	}
}
