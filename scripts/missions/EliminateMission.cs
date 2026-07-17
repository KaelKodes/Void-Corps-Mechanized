using Godot;

namespace Mechanize;

public sealed class EliminateMission : MissionBase
{
	public EliminateMission() : base(MissionType.DestroyAllEnemies) { }

	public override void SetupBattlefield()
	{
		EnsureMissionRoot();
		Host.SpawnEnemyMech("Trinova_Detachment", Host.EnemySpawnA, 0);
		if (Host.Difficulty != PilotDifficulty.Easy)
			Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1);
		else
			Host.DespawnEnemyMech("OuroTech_Detachment");

		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -6f, 4f));
		Host.SpawnSupport("Player_Tower", "gun_tower", TeamId.Player, Offset(Host.PlayerSpawn, 5f, -3f));
		Host.SpawnSupport("EnemyA_Tank", "light_tank", TeamId.Enemy, Offset(Host.EnemySpawnA, 5f, 4f));
		Host.SpawnSupport("EnemyA_Tower", "gun_tower", TeamId.Enemy, Offset(Host.EnemySpawnA, -4f, 6f));
		Host.SpawnSupport("EnemyA_Buggy", "scout_buggy", TeamId.Enemy, Offset(Host.EnemySpawnA, 8f, -2f));

		if (Host.Difficulty != PilotDifficulty.Easy)
		{
			Host.SpawnSupport("EnemyB_Tank", "light_tank", TeamId.Enemy, Offset(Host.EnemySpawnB, -5f, 3f));
			Host.SpawnSupport("EnemyB_Tower", "gun_tower", TeamId.Enemy, Offset(Host.EnemySpawnB, 3f, 5f));
		}
		else
		{
			Host.DespawnSupport("EnemyB_Tank");
			Host.DespawnSupport("EnemyB_Tower");
		}
	}

	public override void NotifyEnemyMechDown(MechController enemy)
	{
		if (Host.AllEnemyMechsDown())
			MarkObjectivesComplete();
	}

	public override string GetHudLine() =>
		"OBJECTIVE  Destroy all enemy MAPs" + ExtractHudHint();

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
