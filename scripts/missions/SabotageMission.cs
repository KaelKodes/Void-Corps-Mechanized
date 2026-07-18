using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Sabotage run: dodge music-synced volleys from Point A to Point B, plant, then extract at the Exfil Uplink.
/// </summary>
public sealed class SabotageMission : MissionBase
{
	public const string TrackPath = "res://audio/BullethellTracks/Echelon 5.wav";
	public const string ClaimCode = "VC-CLAIM ECHELON-RUN";

	private enum Phase
	{
		Approach,
		Planting,
		ExtractReady
	}

	private Phase _phase = Phase.Approach;
	private MissionZone? _objective;
	private DropBeacon? _exfil;
	private BulletHellBeatMap? _beatMap;
	private BulletHellMusicClock? _clock;
	private BulletHellPatternDirector? _director;
	private readonly List<BulletHellOnset> _dueOnsets = new();
	private readonly List<float> _dueBeats = new();
	private readonly List<HellfireTurret> _turrets = new();
	private float _plantSeconds = 2.4f;
	private float _plantProgress;
	private float _integrityAtStart = 1f;
	private bool _musicStarted;
	private Vector3 _pointB;

	public SabotageMission() : base(MissionType.Sabotage) { }

	public override string? PreferredCombatTrack => TrackPath;
	public override DropBeacon? ExtractBeaconOverride => _exfil;

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();
		_phase = Phase.Approach;
		_plantProgress = 0f;
		_musicStarted = false;
		_plantSeconds = Host.Difficulty switch
		{
			PilotDifficulty.Hard => 3.0f,
			PilotDifficulty.Medium => 2.4f,
			_ => 1.8f
		};

		_pointB = Host.EnemySpawnA;
		_pointB.Y = 0f;
		if (_pointB.DistanceTo(Host.PlayerSpawn) < 24f)
		{
			_pointB = new Vector3(0f, 0f, -Host.Layout.PadLimitZ + 2f);
		}

		_objective = MissionZone.Create(
			"SabotageTarget",
			_pointB,
			9f,
			new Color(0.95f, 0.35f, 0.55f),
			"PLANT");
		root.AddChild(_objective);

		_exfil = DropBeacon.Create("ExfilUplink", _pointB + new Vector3(0f, 0f, -1.5f), TeamId.Player, radius: 6.5f);
		_exfil.SetReadyLabel("EXFIL UPLINK");
		_exfil.SetOpenAmount(1f);
		_exfil.SetState(DropBeaconState.Ready);
		root.AddChild(_exfil);

		// Pure dodge run — no rival MAPs cluttering the lane.
		Host.DespawnEnemyMech("Trinova_Detachment");
		Host.DespawnEnemyMech("OuroTech_Detachment");
		Host.DespawnSupport("EnemyA_Tank");
		Host.DespawnSupport("EnemyA_Buggy");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
		Host.DespawnSupport("Player_Tank");
		Host.DespawnSupport("Player_Tower");

		SabotageCorridorScenery.Build(root, Host.Layout);
		SpawnHellfireTurrets(root);

		try
		{
			_beatMap = WavMusicAnalyzer.AnalyzeOrLoad(TrackPath);
			_clock = new BulletHellMusicClock(_beatMap);
			var northZ = -Host.Layout.HalfExtentZ + 2.5f;
			_director = new BulletHellPatternDirector(Host, root, northZ, Host.Layout.HalfExtentX - 2f)
			{
				DamageScale = Host.Difficulty switch
				{
					PilotDifficulty.Hard => 1.15f,
					PilotDifficulty.Medium => 1f,
					_ => 0.75f
				},
				DensityScale = Host.Difficulty switch
				{
					PilotDifficulty.Hard => 1.2f,
					PilotDifficulty.Medium => 1f,
					_ => 0.85f
				}
			};
			_director.BindTurrets(_turrets);
		}
		catch (System.Exception ex)
		{
			GD.PushError($"SabotageMission: beat-map failed — {ex.Message}");
			_beatMap = null;
			_clock = null;
			_director = null;
		}
	}

	public override void OnFightStarted()
	{
		_integrityAtStart = PlayerHealthRatio();
		_clock?.Reset();
		_musicStarted = false;
		EnsureTrackPlaying();
	}

	public override void Tick(float dt)
	{
		if (!Host.IsFighting || ObjectivesComplete)
			return;

		EnsureTrackPlaying();

		var player = Host.Player;
		if (player == null)
			return;

		if (_clock != null && _director != null)
		{
			_clock.Sync(_dueOnsets, _dueBeats);
			_director.Tick(dt, _clock, _dueOnsets, _dueBeats, player.GlobalPosition);
		}

		switch (_phase)
		{
			case Phase.Approach:
				if (_objective != null && _objective.Contains(player.GlobalPosition))
				{
					_phase = Phase.Planting;
					_plantProgress = 0f;
					_objective.SetLabel("HOLD E — PLANT");
					SfxService.Confirm();
				}
				break;

			case Phase.Planting:
				TickPlant(dt, player.GlobalPosition);
				break;
		}
	}

	private void TickPlant(float dt, Vector3 playerPos)
	{
		if (_objective == null)
			return;

		var holding = Input.IsActionPressed("interact");
		var inZone = _objective.Contains(playerPos);
		if (!inZone || !holding)
		{
			_plantProgress = Mathf.Max(0f, _plantProgress - dt * 0.85f);
			_objective.SetLabel(inZone ? "HOLD E — PLANT" : "PLANT");
			return;
		}

		_plantProgress = Mathf.Min(1f, _plantProgress + dt / _plantSeconds);
		_objective.SetLabel($"PLANTING {Mathf.RoundToInt(_plantProgress * 100f)}%");
		if (_plantProgress < 1f)
			return;

		_phase = Phase.ExtractReady;
		_objective.SetLabel("UPLINK LIVE");
		_exfil?.ArmExtract();
		MarkObjectivesComplete();
		SfxService.Play("alarm", 0.9f, -3f);
	}

	private void EnsureTrackPlaying()
	{
		if (_musicStarted)
			return;
		MusicService.CueAbsolute(TrackPath, loop: false);
		_musicStarted = true;
	}

	public override string GetHudLine()
	{
		if (ObjectivesComplete)
			return "OBJECTIVE  Payload planted — hold E at EXFIL UPLINK" + ExtractHudHint();

		var bpm = _beatMap != null ? $"{_beatMap.Bpm:0}" : "--";
		var grade = IntegrityGradeLabel();
		return _phase switch
		{
			Phase.Approach =>
				$"OBJECTIVE  Push past Hellfire turrets to the uplink  ·  {bpm} BPM  ·  {grade}",
			Phase.Planting =>
				$"OBJECTIVE  Hold E to plant sabotage package  ·  {Mathf.RoundToInt(_plantProgress * 100f)}%  ·  {grade}",
			_ => $"OBJECTIVE  Sabotage run  ·  {grade}"
		};
	}

	private void SpawnHellfireTurrets(Node3D root)
	{
		_turrets.Clear();
		var halfX = Host.Layout.HalfExtentX;
		// Face +Z (south / Point A) — Point B sits behind them to the north.
		const float faceSouth = 180f;
		var slots = new (float X, float Z)[]
		{
			(-halfX + 10f, 300f), (halfX - 10f, 270f),
			(-halfX + 12f, 220f), (halfX - 8f, 185f),
			(-halfX + 9f, 140f), (halfX - 11f, 100f),
			(-halfX + 11f, 55f), (halfX - 9f, 15f),
			(-halfX + 10f, -30f), (halfX - 10f, -75f),
			(-halfX + 12f, -120f), (halfX - 8f, -165f),
			(-halfX + 9f, -210f), (halfX - 11f, -255f),
			(-halfX + 10f, -300f), (halfX - 10f, -340f)
		};

		for (var i = 0; i < slots.Length; i++)
		{
			var (x, z) = slots[i];
			var turret = HellfireTurret.Create($"Hellfire_{i}", new Vector3(x, 0f, z), faceSouth);
			root.AddChild(turret);
			_turrets.Add(turret);
		}
	}

	private string IntegrityGradeLabel()
	{
		var ratio = PlayerHealthRatio();
		var retained = _integrityAtStart > 0.01f ? ratio / _integrityAtStart : ratio;
		return retained switch
		{
			>= 0.92f => "GRADE GHOST",
			>= 0.75f => "GRADE CLEAN",
			>= 0.45f => "GRADE COMPROMISED",
			_ => "GRADE BURNED"
		};
	}

	private float PlayerHealthRatio()
	{
		var health = Host.Player?.Health;
		if (health == null || health.MaxHealth <= 0.01f)
			return 1f;
		return Mathf.Clamp(health.CurrentHealth / health.MaxHealth, 0f, 1f);
	}
}
