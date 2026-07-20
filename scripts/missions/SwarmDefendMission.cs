using Godot;

namespace Mechanize;

public sealed class SwarmDefendMission : MissionBase
{
	private MissionZone? _pad;
	private int _waveIndex;
	private float _waveCooldown = 2f;
	private float _overrun;
	private int _aliveFodder;
	private bool _mechsSpawned;
	private bool _started;

	private static readonly string[] WaveUnits =
	[
		"scout_buggy", "scout_buggy", "light_tank", "gun_tower",
		"scout_buggy", "light_tank", "scout_buggy", "light_tank"
	];

	public SwarmDefendMission() : base(MissionType.SwarmDefend) { }

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();
		_waveIndex = 0;
		_waveCooldown = 3f;
		_overrun = 0f;
		_aliveFodder = 0;
		_mechsSpawned = false;
		_started = false;

		var padPos = Host.PlayerSpawn * 0.35f;
		padPos.Y = 0f;
		_pad = MissionZone.Create("DefendPad", padPos, 14f, new Color(0.95f, 0.55f, 0.25f), "HOLD PAD");
		root.AddChild(_pad);

		Host.DespawnEnemyMech("Trinova_Detachment");
		Host.DespawnEnemyMech("OuroTech_Detachment");
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -5f, 3f));
		Host.SpawnSupport("Player_Tower", "gun_tower", TeamId.Player, padPos + new Vector3(5f, 0f, -4f));
		Host.DespawnSupport("EnemyA_Tank");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyA_Buggy");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
	}

	public override void OnFightStarted()
	{
		_started = true;
		_waveCooldown = 1.5f;
	}

	public override void Tick(float dt)
	{
		if (!_started || !Host.IsFighting || _pad == null)
			return;

		CountAliveFodder();
		UpdateOverrun(dt);

		if (_overrun >= 12f)
		{
			SfxService.Play("alarm");
			Lose();
			return;
		}

		if (_waveIndex < 3)
		{
			_waveCooldown -= dt;
			if (_waveCooldown <= 0f && _aliveFodder <= 1)
			{
				SpawnWave(_waveIndex);
				_waveIndex++;
				_waveCooldown = 4f;
			}
		}
		else if (!_mechsSpawned && _aliveFodder == 0)
		{
			_mechsSpawned = true;
			Host.SpawnEnemyMech("Trinova_Detachment", Host.EnemySpawnA, 0, viaDropBeacon: true);
			if (Host.Difficulty != PilotDifficulty.Easy)
				Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1, viaDropBeacon: true);
		}
		else if (_mechsSpawned && Host.AllEnemyMechsDown() && _aliveFodder == 0)
		{
			MarkObjectivesComplete();
		}
	}

	public override void NotifyEnemyMechDown(MechController enemy)
	{
		if (_mechsSpawned && Host.AllEnemyMechsDown() && _aliveFodder == 0)
			MarkObjectivesComplete();
	}

	public override string GetHudLine()
	{
		if (ObjectivesComplete)
			return "OBJECTIVE  Swarm cleared" + ExtractHudHint();
		if (_overrun > 0.1f)
			return $"OBJECTIVE  Hold the pad  OVERRUN {_overrun / 12f * 100f:0}%";
		if (!_mechsSpawned)
			return $"OBJECTIVE  Swarm defend  wave {Mathf.Min(_waveIndex + 1, 3)}/3";
		return "OBJECTIVE  Finish surviving MAPs";
	}

	private void SpawnWave(int wave)
	{
		var count = 4 + wave * 2 + (int)Host.Difficulty;
		for (var i = 0; i < count; i++)
		{
			var unitId = WaveUnits[(wave * 3 + i) % WaveUnits.Length];
			if (unitId == "gun_tower" && wave == 0)
				unitId = "scout_buggy";

			var edge = i % 4;
			var pos = edge switch
			{
				0 => new Vector3(-28f + i * 2f, 0f, -28f),
				1 => new Vector3(28f, 0f, -24f + i * 2f),
				2 => new Vector3(24f - i * 2f, 0f, 28f),
				_ => new Vector3(-28f, 0f, 20f - i * 2f)
			};

			var name = $"Swarm_{wave}_{i}";
			var mobile = unitId != "gun_tower";
			Host.SpawnSupport(name, unitId, TeamId.Enemy, pos, viaTelegraph: mobile);
		}
	}

	private void CountAliveFodder()
	{
		_aliveFodder = 0;
		foreach (var child in Host.Root.GetChildren())
		{
			if (child is SupportUnit support && support.Team == TeamId.Enemy && support.IsAlive
				&& support.Name.ToString().StartsWith("Swarm_"))
				_aliveFodder++;
		}
	}

	private void UpdateOverrun(float dt)
	{
		if (_pad == null)
			return;

		var hostiles = 0;
		foreach (var child in Host.Root.GetChildren())
		{
			if (child is SupportUnit support && support.Team == TeamId.Enemy && support.IsAlive
				&& _pad.Contains(support.GlobalPosition))
				hostiles++;
			if (child is MechController mech && mech.Team == TeamId.Enemy && mech.Visible
				&& mech.Integrity?.IsCollapsed != true && _pad.Contains(mech.GlobalPosition))
				hostiles += 2;
		}

		if (hostiles > 0)
		{
			_overrun = Mathf.Min(12f, _overrun + dt * (0.7f + hostiles * 0.25f));
			_pad.SetColor(new Color(1f, 0.35f, 0.25f));
			_pad.SetLabel($"OVERRUN {_overrun / 12f * 100f:0}%");
		}
		else
		{
			_overrun = Mathf.Max(0f, _overrun - dt * 0.65f);
			_pad.SetColor(new Color(0.95f, 0.55f, 0.25f));
			_pad.SetLabel("HOLD PAD");
		}
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
