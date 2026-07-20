using Godot;

namespace Mechanize;

/// <summary>
/// Mid-objective pressure for claim missions: a fodder wave at a randomized progress
/// threshold, and sometimes a MAP drop after the objective is secured.
/// </summary>
public sealed class MissionPressure
{
	private static readonly string[] FodderUnits = ["scout_buggy", "light_tank", "scout_buggy", "light_tank"];

	private readonly IMissionHost _host;
	private readonly string _id;
	private readonly float _waveAt;
	private readonly bool _mapCounter;
	private readonly int _fodderCount;
	private bool _waveFired;
	private bool _mapFired;
	private int _spawnSeq;

	private MissionPressure(IMissionHost host, string id, float waveAt, bool mapCounter, int fodderCount)
	{
		_host = host;
		_id = id;
		_waveAt = waveAt;
		_mapCounter = mapCounter;
		_fodderCount = fodderCount;
	}

	public float WaveAt => _waveAt;
	public bool WaveFired => _waveFired;
	public bool MapCounterArmed => _mapCounter && !_mapFired;

	public static MissionPressure Create(IMissionHost host, string id, RandomNumberGenerator rng)
	{
		// Threshold varies per objective — keeps each run from feeling scripted.
		var waveAt = rng.RandfRange(0.32f, 0.68f);
		var mapChance = host.Difficulty switch
		{
			PilotDifficulty.Hard => 0.78f,
			PilotDifficulty.Medium => 0.55f,
			_ => 0.32f
		};
		var fodder = host.Difficulty switch
		{
			PilotDifficulty.Hard => 4,
			PilotDifficulty.Medium => 3,
			_ => 2
		};
		return new MissionPressure(host, id, waveAt, rng.Randf() < mapChance, fodder);
	}

	/// <summary>Call while the objective meter is climbing (0..1). Fires once past the threshold.</summary>
	public void NotifyProgress(float progress01, Vector3 focus)
	{
		if (_waveFired || progress01 + 0.001f < _waveAt)
			return;

		_waveFired = true;
		SpawnFodderWave(focus);
		SfxService.Play("alarm", 1f, -6f);
	}

	/// <summary>Call when the zone/cargo is secured. May drop a counter MAP.</summary>
	public void NotifyObjectiveSecured(Vector3 focus)
	{
		if (!_mapCounter || _mapFired)
			return;

		_mapFired = true;
		var spawn = PickMapDrop(focus);
		var variant = (_id.GetHashCode() & 1) == 0 ? 0 : 1;
		_host.SpawnEnemyMech($"Pressure_{_id}_MAP", spawn, variant, viaDropBeacon: true);
		SfxService.Play("alarm", 1f, -4f);
	}

	private void SpawnFodderWave(Vector3 focus)
	{
		for (var i = 0; i < _fodderCount; i++)
		{
			var unitId = FodderUnits[(_spawnSeq + i) % FodderUnits.Length];
			var angle = Mathf.Tau * (i + 0.5f) / _fodderCount + focus.X * 0.01f;
			var radius = 16f + i * 2.5f;
			var pos = focus + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			pos = ClampToArena(pos);
			var name = $"Pressure_{_id}_F{_spawnSeq++}";
			_host.SpawnSupport(name, unitId, TeamId.Enemy, pos, viaTelegraph: true);
		}
	}

	private Vector3 PickMapDrop(Vector3 focus)
	{
		var a = _host.EnemySpawnA;
		var b = _host.EnemySpawnB;
		var pick = focus.DistanceSquaredTo(a) <= focus.DistanceSquaredTo(b) ? a : b;
		// Offset so the drop doesn't land inside the claim ring.
		var away = pick - focus;
		away.Y = 0f;
		if (away.LengthSquared() < 0.01f)
			away = new Vector3(1f, 0f, 0f);
		away = away.Normalized();
		return ClampToArena(focus + away * 18f + away.Cross(Vector3.Up) * 6f);
	}

	private static Vector3 ClampToArena(Vector3 pos)
	{
		pos.X = Mathf.Clamp(pos.X, -34f, 34f);
		pos.Z = Mathf.Clamp(pos.Z, -34f, 34f);
		pos.Y = 0f;
		return pos;
	}
}
