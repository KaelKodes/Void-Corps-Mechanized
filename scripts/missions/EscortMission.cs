using Godot;

namespace Mechanize;

public sealed class EscortMission : MissionBase
{
	private EscortAsset? _crawler;
	private MissionZone? _gate;

	public EscortMission() : base(MissionType.Escort) { }

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();

		var start = Host.PlayerSpawn + (Vector3.Zero - Host.PlayerSpawn).Normalized() * 6f;
		start.Y = 0f;
		var exit = Host.EnemySpawnA;
		exit.Y = 0f;

		_gate = MissionZone.Create("ExtractGate", exit, 10f, new Color(0.55f, 0.85f, 1f), "EXTRACT");
		root.AddChild(_gate);

		_crawler = EscortAsset.Create(start, exit);
		root.AddChild(_crawler);
		_crawler.SetEscort(Host.Player);

		Host.SpawnEnemyMech("Trinova_Detachment", Host.EnemySpawnA + new Vector3(8f, 0f, 6f), 0);
		if (Host.Difficulty != PilotDifficulty.Easy)
			Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1);
		else
			Host.DespawnEnemyMech("OuroTech_Detachment");

		Host.SpawnSupport("EnemyA_Tank", "light_tank", TeamId.Enemy, exit + new Vector3(-6f, 0f, 4f));
		Host.SpawnSupport("EnemyA_Buggy", "scout_buggy", TeamId.Enemy, Vector3.Zero + new Vector3(8f, 0f, -4f));
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, 4f, 2f));
		Host.DespawnSupport("Player_Tower");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
	}

	public override void OnFightStarted()
	{
		_crawler?.SetEscort(Host.Player);
	}

	public override void Tick(float dt)
	{
		if (!Host.IsFighting || _crawler == null)
			return;

		_crawler.SetEscort(Host.Player);

		if (_crawler.IsDestroyed)
		{
			SfxService.Play("alarm");
			Lose();
			return;
		}

		if (_crawler.HasArrived)
			MarkObjectivesComplete();
	}

	public override string GetHudLine()
	{
		if (_crawler == null)
			return "OBJECTIVE  Escort the crawler";
		if (_crawler.IsDestroyed)
			return "OBJECTIVE  Crawler lost";
		if (ObjectivesComplete || _crawler.HasArrived)
			return "OBJECTIVE  Crawler extracted" + ExtractHudHint();
		var hp = _crawler.HealthRatio * 100f;
		return $"OBJECTIVE  Escort crawler to extract  (HP {hp:0}% — stay close)";
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
