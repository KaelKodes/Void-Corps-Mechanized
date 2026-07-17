using Godot;

namespace Mechanize;

public sealed class SearchDestroyMission : MissionBase
{
	private MissionBuilding? _building;

	public SearchDestroyMission() : base(MissionType.SearchAndDestroy) { }

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();
		var site = PickBuildingSite();
		_building = MissionBuilding.Create("ClaimStructure", site, 380f + (int)Host.Difficulty * 60f,
			new Color(0.55f, 0.28f, 0.18f));
		root.AddChild(_building);
		_building.Destroyed += MarkObjectivesComplete;

		Host.SpawnEnemyMech("Trinova_Detachment", Host.EnemySpawnA, 0);
		if (Host.Difficulty == PilotDifficulty.Hard)
			Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1);
		else
			Host.DespawnEnemyMech("OuroTech_Detachment");

		Host.SpawnSupport("EnemyA_Tower", "gun_tower", TeamId.Enemy, site + new Vector3(8f, 0f, 4f));
		Host.SpawnSupport("EnemyA_Tank", "light_tank", TeamId.Enemy, site + new Vector3(-7f, 0f, -5f));
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -5f, 3f));
		Host.DespawnSupport("EnemyA_Buggy");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
		Host.DespawnSupport("Player_Tower");
	}

	public override string GetHudLine()
	{
		if (_building == null || _building.IsDestroyed)
			return "OBJECTIVE  Structure destroyed" + ExtractHudHint();
		return $"OBJECTIVE  Destroy structure  ({_building.HealthRatio * 100f:0}%)";
	}

	private Vector3 PickBuildingSite()
	{
		// Prefer map center, nudged away from player spawn.
		var site = new Vector3(0f, 0f, 0f);
		var away = site - Host.PlayerSpawn;
		away.Y = 0f;
		if (away.LengthSquared() < 1f)
			away = -Host.PlayerSpawn;
		away = away.Normalized();
		return site + away * 4f;
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
