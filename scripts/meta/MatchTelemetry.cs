using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum TelemetryTargetKind
{
	Unknown,
	Building,
	Fodder,
	Map,
	Mad,
	Escort
}

/// <summary>
/// Player-facing mission telemetry. Starts in manufacturer trials and remains useful
/// for campaign scoring, rep, and debrief flavor.
/// </summary>
public sealed class MatchTelemetry
{
	public float MissionSeconds { get; private set; }
	public int ShotsFired { get; private set; }
	public int ShotsHit { get; private set; }
	public int MissilesFired { get; private set; }
	public int MissilesHit { get; private set; }
	public int UtilityUses { get; private set; }
	public float HealApplied { get; private set; }
	public float DamageSustained { get; private set; }
	public int HitsSustained { get; private set; }
	public float EscortDamageTaken { get; private set; }
	public int BuildingsDestroyed { get; private set; }
	public int FodderDestroyed { get; private set; }
	public int MapsDestroyed { get; private set; }
	public int MapsHit { get; private set; }
	public int MadsDestroyed { get; private set; }
	public int MadsHit { get; private set; }

	public float Accuracy => ShotsFired <= 0 ? 0f : (float)ShotsHit / ShotsFired;

	public void Tick(float dt)
	{
		if (dt > 0f)
			MissionSeconds += dt;
	}

	public void RecordShot(bool missile)
	{
		if (missile)
			MissilesFired++;
		else
			ShotsFired++;
	}

	public void RecordHit(TelemetryTargetKind kind, bool missile)
	{
		if (missile)
			MissilesHit++;
		else
			ShotsHit++;

		switch (kind)
		{
			case TelemetryTargetKind.Map:
				MapsHit++;
				break;
			case TelemetryTargetKind.Mad:
				MadsHit++;
				break;
		}
	}

	public void RecordKill(TelemetryTargetKind kind)
	{
		switch (kind)
		{
			case TelemetryTargetKind.Building:
				BuildingsDestroyed++;
				break;
			case TelemetryTargetKind.Fodder:
				FodderDestroyed++;
				break;
			case TelemetryTargetKind.Map:
				MapsDestroyed++;
				break;
			case TelemetryTargetKind.Mad:
				MadsDestroyed++;
				break;
		}
	}

	public void RecordUtilityUse() => UtilityUses++;

	public void RecordHeal(float amount)
	{
		if (amount > 0.01f)
			HealApplied += amount;
	}

	public void RecordDamageTaken(float amount, TelemetryTargetKind kind)
	{
		if (amount <= 0.01f)
			return;
		DamageSustained += amount;
		HitsSustained++;
		if (kind == TelemetryTargetKind.Escort)
			EscortDamageTaken += amount;
	}

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["mission_seconds"] = MissionSeconds,
		["shots_fired"] = ShotsFired,
		["shots_hit"] = ShotsHit,
		["missiles_fired"] = MissilesFired,
		["missiles_hit"] = MissilesHit,
		["utility_uses"] = UtilityUses,
		["heal_applied"] = HealApplied,
		["damage_sustained"] = DamageSustained,
		["hits_sustained"] = HitsSustained,
		["escort_damage_taken"] = EscortDamageTaken,
		["buildings_destroyed"] = BuildingsDestroyed,
		["fodder_destroyed"] = FodderDestroyed,
		["maps_destroyed"] = MapsDestroyed,
		["maps_hit"] = MapsHit,
		["mads_destroyed"] = MadsDestroyed,
		["mads_hit"] = MadsHit
	};

	public static MatchTelemetry FromDict(Godot.Collections.Dictionary dict)
	{
		var t = new MatchTelemetry();
		t.MissionSeconds = dict.ContainsKey("mission_seconds") ? dict["mission_seconds"].AsSingle() : 0f;
		t.ShotsFired = dict.ContainsKey("shots_fired") ? dict["shots_fired"].AsInt32() : 0;
		t.ShotsHit = dict.ContainsKey("shots_hit") ? dict["shots_hit"].AsInt32() : 0;
		t.MissilesFired = dict.ContainsKey("missiles_fired") ? dict["missiles_fired"].AsInt32() : 0;
		t.MissilesHit = dict.ContainsKey("missiles_hit") ? dict["missiles_hit"].AsInt32() : 0;
		t.UtilityUses = dict.ContainsKey("utility_uses") ? dict["utility_uses"].AsInt32() : 0;
		t.HealApplied = dict.ContainsKey("heal_applied") ? dict["heal_applied"].AsSingle() : 0f;
		t.DamageSustained = dict.ContainsKey("damage_sustained") ? dict["damage_sustained"].AsSingle() : 0f;
		t.HitsSustained = dict.ContainsKey("hits_sustained") ? dict["hits_sustained"].AsInt32() : 0;
		t.EscortDamageTaken = dict.ContainsKey("escort_damage_taken") ? dict["escort_damage_taken"].AsSingle() : 0f;
		t.BuildingsDestroyed = dict.ContainsKey("buildings_destroyed") ? dict["buildings_destroyed"].AsInt32() : 0;
		t.FodderDestroyed = dict.ContainsKey("fodder_destroyed") ? dict["fodder_destroyed"].AsInt32() : 0;
		t.MapsDestroyed = dict.ContainsKey("maps_destroyed") ? dict["maps_destroyed"].AsInt32() : 0;
		t.MapsHit = dict.ContainsKey("maps_hit") ? dict["maps_hit"].AsInt32() : 0;
		t.MadsDestroyed = dict.ContainsKey("mads_destroyed") ? dict["mads_destroyed"].AsInt32() : 0;
		t.MadsHit = dict.ContainsKey("mads_hit") ? dict["mads_hit"].AsInt32() : 0;
		return t;
	}

	public string BuildSummary()
	{
		var lines = new List<string>
		{
			$"Time  {Mathf.FloorToInt(MissionSeconds / 60f):00}:{Mathf.FloorToInt(MissionSeconds % 60f):00}",
			$"Ballistic / energy  {ShotsHit}/{ShotsFired} hit  ({Accuracy * 100f:0}%)",
			$"Missiles  {MissilesHit}/{MissilesFired} hit",
			$"Utility uses  {UtilityUses}",
			$"Heal applied  {HealApplied:0}",
			$"Damage sustained  {DamageSustained:0}  across {HitsSustained} hits"
		};

		if (BuildingsDestroyed > 0 || FodderDestroyed > 0 || MapsDestroyed > 0 || MadsDestroyed > 0)
		{
			lines.Add(
				$"Kills  buildings {BuildingsDestroyed}  fodder {FodderDestroyed}  MAPs {MapsDestroyed}  MADs {MadsDestroyed}");
		}

		if (MapsHit > 0 || MadsHit > 0)
			lines.Add($"Armor hits  MAPs {MapsHit}  MADs {MadsHit}");
		if (EscortDamageTaken > 0.01f)
			lines.Add($"Escort damage taken  {EscortDamageTaken:0}");

		return string.Join("\n", lines);
	}
}
