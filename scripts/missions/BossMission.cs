using Godot;

namespace Mechanize;

/// <summary>
/// Sector Warning fight: Showdown, Swarm-to-Boss, or Hidden Boss (S&amp;D facade).
/// </summary>
public sealed class BossMission : MissionBase
{
	private readonly BossEncounterDef _encounter;
	private MissionBuilding? _building;
	private int _waveIndex;
	private float _waveCooldown;
	private float _calmTimer = -1f;
	private bool _bossSpawned;
	private bool _started;
	private int _aliveFodder;
	private const string BossMechName = "Boss_Detachment";

	private static readonly string[] WaveUnits =
	[
		"scout_buggy", "scout_buggy", "light_tank", "gun_tower",
		"scout_buggy", "light_tank", "scout_buggy", "light_tank"
	];

	public BossMission(BossEncounterDef encounter) : base(MissionType.BossEncounter)
	{
		_encounter = encounter;
	}

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();
		_bossSpawned = false;
		_calmTimer = -1f;
		_waveIndex = 0;
		_waveCooldown = 2f;
		_started = false;
		_aliveFodder = 0;

		Host.DespawnEnemyMech("Trinova_Detachment");
		Host.DespawnEnemyMech("OuroTech_Detachment");
		Host.DespawnEnemyMech(BossMechName);
		Host.DespawnSupport("EnemyA_Tank");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyA_Buggy");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");

		switch (_encounter.Template)
		{
			case BossEncounterTemplate.Showdown:
				SetupShowdown();
				break;
			case BossEncounterTemplate.HiddenBoss:
				SetupHiddenBoss(root);
				break;
			default:
				SetupSwarmToBoss(root);
				break;
		}
	}

	private void SetupShowdown()
	{
		Host.SpawnEnemyMech(BossMechName, Host.EnemySpawnA, _encounter.LoadoutVariant, viaDropBeacon: false);
		_bossSpawned = true;
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -5f, 3f));
		Host.DespawnSupport("Player_Tower");
	}

	private void SetupHiddenBoss(Node3D root)
	{
		var site = PickBuildingSite();
		_building = MissionBuilding.Create("BossCocoon", site, 320f + (int)Host.Difficulty * 40f,
			new Color(0.4f, 0.22f, 0.28f));
		root.AddChild(_building);
		_building.Destroyed += OnHiddenStructureDestroyed;

		Host.SpawnSupport("EnemyA_Tower", "gun_tower", TeamId.Enemy, site + new Vector3(7f, 0f, 4f));
		Host.SpawnSupport("EnemyA_Buggy", "scout_buggy", TeamId.Enemy, site + new Vector3(-6f, 0f, -3f));
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -4f, 2f));
		Host.DespawnSupport("Player_Tower");
	}

	private void SetupSwarmToBoss(Node3D root)
	{
		var padPos = Host.PlayerSpawn * 0.4f;
		padPos.Y = 0f;
		var pad = MissionZone.Create("SwarmPad", padPos, 12f, new Color(0.95f, 0.45f, 0.3f), "HOLD");
		root.AddChild(pad);
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -5f, 3f));
		Host.SpawnSupport("Player_Tower", "gun_tower", TeamId.Player, padPos + new Vector3(4f, 0f, -3f));
	}

	public override void OnFightStarted()
	{
		_started = true;
		_waveCooldown = 1.2f;
	}

	public override void Tick(float dt)
	{
		if (!_started || !Host.IsFighting || ObjectivesComplete)
			return;

		if (_encounter.Template == BossEncounterTemplate.SwarmToBoss && !_bossSpawned)
			TickSwarm(dt);

		if (_calmTimer >= 0f)
		{
			_calmTimer -= dt;
			if (_calmTimer <= 0f)
			{
				_calmTimer = -1f;
				SpawnBossDrop();
			}
		}
	}

	private void TickSwarm(float dt)
	{
		CountAliveFodder();
		if (_waveIndex < 3)
		{
			_waveCooldown -= dt;
			if (_waveCooldown <= 0f && _aliveFodder <= 1)
			{
				SpawnWave(_waveIndex);
				_waveIndex++;
				_waveCooldown = 3.5f;
			}
		}
		else if (_aliveFodder == 0 && _calmTimer < 0f && !_bossSpawned)
		{
			_calmTimer = 2.4f;
			SfxService.Play("countdown", 0.7f, -8f);
			GD.Print("The swarm falls silent…");
		}
	}

	private void OnHiddenStructureDestroyed()
	{
		if (_bossSpawned || ObjectivesComplete)
			return;
		SfxService.Play("alarm", 0.9f, -3f);
		GD.Print(_encounter.ArrivalLine);
		var site = _building?.GlobalPosition ?? Host.EnemySpawnA;
		Host.SpawnEnemyMech(BossMechName, site, _encounter.LoadoutVariant, viaDropBeacon: false);
		_bossSpawned = true;
	}

	private void SpawnBossDrop()
	{
		if (_bossSpawned)
			return;
		SfxService.Play("alarm", 0.85f, -2f);
		SfxService.Confirm();
		GD.Print(_encounter.ArrivalLine);
		Host.SpawnEnemyMech(BossMechName, Host.EnemySpawnA, _encounter.LoadoutVariant, viaDropBeacon: true);
		_bossSpawned = true;
	}

	public override void NotifyEnemyMechDown(MechController enemy)
	{
		if (!_bossSpawned || ObjectivesComplete)
			return;
		if (enemy.Name != BossMechName)
			return;
		MarkObjectivesComplete();
	}

	public override string GetHudLine()
	{
		var pilot = _encounter.Pilot;
		var corp = _encounter.Corp;
		var titan = $"{MechChassisClassUtil.ShortLabel(_encounter.ChassisClass)} {pilot.Callsign.ToUpperInvariant()} · {corp.ShortName}";
		if (ObjectivesComplete)
			return $"TITAN DOWN — {titan}" + ExtractHudHint();

		return _encounter.Template switch
		{
			BossEncounterTemplate.Showdown =>
				$"TITAN CONTEST — eliminate {titan}",
			BossEncounterTemplate.HiddenBoss when !_bossSpawned =>
				_building == null || _building.IsDestroyed
					? "TITAN CRADLE BREACHED"
					: $"BREACH TITAN CRADLE  ({_building.HealthRatio * 100f:0}%)",
			BossEncounterTemplate.HiddenBoss =>
				$"TITAN DEPLOYED — eliminate {titan}",
			BossEncounterTemplate.SwarmToBoss when _calmTimer >= 0f =>
				$"TITAN DROP INBOUND — {titan}",
			BossEncounterTemplate.SwarmToBoss when !_bossSpawned =>
				$"BREAK {corp.ShortName.ToUpperInvariant()} PERIMETER  wave {Mathf.Min(_waveIndex + 1, 3)}/3",
			_ => $"TITAN CONTEST — eliminate {titan}"
		};
	}

	private void SpawnWave(int wave)
	{
		var count = 3 + wave + (int)Host.Difficulty;
		for (var i = 0; i < count; i++)
		{
			var unitId = WaveUnits[(wave * 3 + i) % WaveUnits.Length];
			if (unitId == "gun_tower" && wave == 0)
				unitId = "scout_buggy";
			var edge = i % 4;
			var pos = edge switch
			{
				0 => new Vector3(-26f + i * 2f, 0f, -26f),
				1 => new Vector3(26f, 0f, -22f + i * 2f),
				2 => new Vector3(22f - i * 2f, 0f, 26f),
				_ => new Vector3(-26f, 0f, 18f - i * 2f)
			};
			Host.SpawnSupport($"BossSwarm_{wave}_{i}", unitId, TeamId.Enemy, pos);
		}
	}

	private void CountAliveFodder()
	{
		_aliveFodder = 0;
		foreach (var child in Host.Root.GetChildren())
		{
			if (child is SupportUnit support && support.Team == TeamId.Enemy && support.IsAlive
			    && support.Name.ToString().StartsWith("BossSwarm_"))
				_aliveFodder++;
		}
	}

	private Vector3 PickBuildingSite()
	{
		var site = Vector3.Zero;
		var away = site - Host.PlayerSpawn;
		away.Y = 0f;
		if (away.LengthSquared() < 1f)
			away = -Host.PlayerSpawn;
		return site + away.Normalized() * 4f;
	}

	private Vector3 Offset(Vector3 from, float right, float forward)
	{
		var dir = Vector3.Zero - from;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			dir = new Vector3(0f, 0f, -1f);
		dir = dir.Normalized();
		var side = dir.Cross(Vector3.Up).Normalized();
		return from + side * right + dir * forward;
	}
}
