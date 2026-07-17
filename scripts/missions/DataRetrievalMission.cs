using Godot;

namespace Mechanize;

public sealed class DataRetrievalMission : MissionBase
{
	private MissionBuilding? _archive;
	private MissionZone? _basePad;
	private Node3D? _root;
	private bool _diskReady;
	private bool _carrying;

	public DataRetrievalMission() : base(MissionType.DataRetrieval) { }

	public override void SetupBattlefield()
	{
		_root = EnsureMissionRoot();
		_carrying = false;
		_diskReady = false;

		_basePad = MissionZone.Create("AlliedPad", Host.PlayerSpawn, 12f, new Color(0.35f, 0.85f, 0.55f), "ALLIED PAD");
		_root.AddChild(_basePad);

		var archivePos = Host.EnemySpawnA * 0.55f;
		archivePos.Y = 0f;
		_archive = MissionBuilding.Create("DataArchive", archivePos, 300f + (int)Host.Difficulty * 50f,
			new Color(0.25f, 0.45f, 0.55f));
		_root.AddChild(_archive);
		_archive.Destroyed += OnArchiveDestroyed;

		Host.SpawnEnemyMech("Trinova_Detachment", Host.EnemySpawnA, 0);
		if (Host.Difficulty == PilotDifficulty.Hard)
			Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1);
		else
			Host.DespawnEnemyMech("OuroTech_Detachment");

		Host.SpawnSupport("EnemyA_Tower", "gun_tower", TeamId.Enemy, archivePos + new Vector3(6f, 0f, -5f));
		Host.SpawnSupport("EnemyA_Buggy", "scout_buggy", TeamId.Enemy, archivePos + new Vector3(-8f, 0f, 3f));
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -4f, 2f));
		Host.DespawnSupport("Player_Tower");
		Host.DespawnSupport("EnemyA_Tank");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
	}

	private void OnArchiveDestroyed()
	{
		if (_root == null || _diskReady)
			return;
		_diskReady = true;
		var spawn = (_archive?.GlobalPosition ?? Vector3.Zero) + new Vector3(0f, 0f, 2f);
		var disk = DataDiskPickup.Create(spawn);
		_root.AddChild(disk);
		disk.Collected += () => _carrying = true;
	}

	public override void Tick(float dt)
	{
		if (!Host.IsFighting || !_carrying || Host.Player == null || _basePad == null)
			return;

		if (_basePad.Contains(Host.Player.GlobalPosition))
			MarkObjectivesComplete();
	}

	public override string GetHudLine()
	{
		if (ObjectivesComplete)
			return "OBJECTIVE  Data secured" + ExtractHudHint();
		if (_carrying)
			return "OBJECTIVE  Return data disk to allied pad";
		if (_diskReady)
			return "OBJECTIVE  Recover the data disk";
		if (_archive != null && !_archive.IsDestroyed)
			return $"OBJECTIVE  Breach archive  ({_archive.HealthRatio * 100f:0}%)";
		return "OBJECTIVE  Data retrieval";
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
