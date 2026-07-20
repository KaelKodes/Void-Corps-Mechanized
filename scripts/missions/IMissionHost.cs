using Godot;

namespace Mechanize;

/// <summary>
/// Arena services exposed to mission runtimes.
/// </summary>
public interface IMissionHost
{
	Node3D Root { get; }
	MechController? Player { get; }
	Vector3 PlayerSpawn { get; }
	Vector3 EnemySpawnA { get; }
	Vector3 EnemySpawnB { get; }
	ClaimArenaLayout Layout { get; }
	PilotDifficulty Difficulty { get; }
	bool MatchResolved { get; }
	bool IsFighting { get; }
	bool ObjectivesComplete { get; }
	DropBeacon? PlayerDropBeacon { get; }

	void ReportMissionOutcome(MatchOutcome outcome);
	void NotifyObjectivesComplete();
	MechController? SpawnEnemyMech(string name, Vector3 position, int variant, bool viaDropBeacon = false);
	void DespawnEnemyMech(string name);
	SupportUnit SpawnSupport(string name, string unitId, TeamId team, Vector3 position, bool viaTelegraph = false);
	void DespawnSupport(string name);
	bool AllEnemyMechsDown();
	void FaceToward(Node3D body, Vector3 worldPoint);
}
