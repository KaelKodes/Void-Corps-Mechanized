using System.Collections.Generic;
using Godot;

namespace Mechanize;

public partial class ArenaController : Node3D, IMissionHost
{
	private enum MatchPhase
	{
		Prep,
		Countdown,
		Fighting
	}

	[Export] public NodePath MechPath { get; set; } = "Mech";
	[Export] public NodePath GaragePath { get; set; } = "UI/GarageUi";
	[Export] public NodePath HudPath { get; set; } = "UI/Hud";

	/// <summary>Practice default is Easy. Hard is the original lethal pilot profile.</summary>
	[Export] public PilotDifficulty EnemyDifficulty { get; set; } = PilotDifficulty.Easy;

	private Vector3 _playerSpawn = new(32f, 0f, 32f);
	private Vector3 _enemySpawnA = new(-32f, 0f, -32f);
	private Vector3 _enemySpawnB = new(32f, 0f, -32f);
	private ClaimArenaLayout _layout = ClaimArenaLayout.All[0];
	private Label? _missionHud;
	private MechHud? _mechHud;
	private MissionBase? _mission;

	private MechController? _mech;
	private GarageUi? _garage;
	private Label? _hud;
	private Label? _hint;
	private Label? _abilityHud;
	private Label? _targetHud;
	private Label? _claimHud;
	private Label? _countdownLabel;
	private ResultsShopUi? _results;
	private PauseMenuUi? _pauseMenu;
	private DropBeacon? _playerDropBeacon;
	private bool _objectivesComplete;
	private readonly List<DropInSequence> _dropIns = new();
	private VoidCorpsIdentity.ClaimSite _claim;
	private MatchPhase _phase = MatchPhase.Prep;
	private float _countdownRemaining;
	private int _lastCountdownSecond = -1;
	private bool _matchResolved;
	private bool _playerDownPending;
	private bool _playerDeathHooked;
	private bool _buyLifeLatch;
	private readonly HashSet<string> _enemyDeathHooked = new();
	private readonly HashSet<string> _enemyResolved = new();
	private readonly Dictionary<string, LoadoutData> _enemyLoadouts = new();
	private readonly HashSet<ulong> _telemetryDamageHooked = new();

	private sealed class DropInSequence
	{
		public required MechController Mech;
		public required Vector3 Landing;
		public required float FallDuration;
		public float OpenDuration = 0.75f;
		public DropBeacon? Beacon;
		public bool EnableAiWhenDone;
		public float Elapsed;
		public float StartY;
		public bool Opening;
	}

	private bool _playerDropStarted;

	private NetCombatBus? _netCombat;
	private float _matchHudSyncTimer;

	public override void _Ready()
	{
		_mech = GetNodeOrNull<MechController>(MechPath);
		_garage = GetNodeOrNull<GarageUi>(GaragePath);
		_hud = GetNodeOrNull<Label>(HudPath);
		_hint = GetNodeOrNull<Label>("UI/Hint");

		_netCombat = new NetCombatBus();
		_netCombat.EnsureUnder(this);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		_claim = session?.CurrentClaim ?? VoidCorpsIdentity.PickClaimSite();
		SyncMatchFromSession(session);
		ApplyClaimMap();
		CreateMission();

		EnsureBrandHud();
		EnsureCountdownLabel();
		EnsureResultsUi();
		EnsurePauseMenu();
		EnsureMissionHud();
		EnsureMechHud();
		SetupCoopWings();
		PlaceCombatants();
		if (IsCoopMatch)
			HookAllWingDeaths();
		else
			HookPlayerDeath();

		if (_garage != null)
		{
			_garage.ConfigurePrepMode(true);
			if (IsCoopMatch)
				_garage.LoadoutApplied += OnCoopReadyPressed;
			else
				_garage.LoadoutApplied += OnReadyPressed;
			_garage.Visible = true;
			_garage.MoveToFront();
			_garage.RefreshFromSession();
		}

		SetCombatActive(false);
		_phase = MatchPhase.Prep;
		SetCombatHudVisible(false);
		MusicService.Cue(MusicCue.Hangar);
		UpdateHud();
	}

	private void SyncMatchFromSession(GameSession? session)
	{
		if (session == null)
			return;

		if (!session.Match.Active)
			session.Match.Begin(session.PendingDifficulty, session.Profile.LivesBank, session.PendingMission);

		EnemyDifficulty = session.Match.Difficulty;
		session.PendingDifficulty = EnemyDifficulty;
		session.LaunchSkirmishOnArenaLoad = false;
	}

	private void EnsureResultsUi()
	{
		var ui = GetNodeOrNull("UI");
		if (ui == null)
			return;

		_results = ui.GetNodeOrNull<ResultsShopUi>("ResultsShopUi");
		if (_results != null)
			return;

		_results = new ResultsShopUi
		{
			Name = "ResultsShopUi",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ui.AddChild(_results);
	}

	private void EnsurePauseMenu()
	{
		var ui = GetNodeOrNull("UI");
		if (ui == null)
			return;

		_pauseMenu = ui.GetNodeOrNull<PauseMenuUi>("PauseMenuUi");
		if (_pauseMenu != null)
			return;

		_pauseMenu = new PauseMenuUi { Name = "PauseMenuUi" };
		ui.AddChild(_pauseMenu);
	}

	private bool CanOpenPause()
	{
		if (_matchResolved || (_results?.Visible ?? false))
			return false;
		return _phase is MatchPhase.Prep or MatchPhase.Fighting or MatchPhase.Countdown;
	}

	private void TogglePauseMenu()
	{
		if (_pauseMenu == null || !CanOpenPause())
			return;

		if (_pauseMenu.IsOpen)
		{
			_pauseMenu.Close();
			GetTree().Paused = false;
			return;
		}

		if (_garage != null && _garage.Visible && _phase == MatchPhase.Fighting)
		{
			_garage.Visible = false;
			SetCombatActive(true);
			SetCombatHudVisible(true);
		}

		_pauseMenu.Open();
		GetTree().Paused = true;
	}

	private void SetCombatHudVisible(bool visible)
	{
		if (_hud != null)
			_hud.Visible = visible;
		if (_claimHud != null)
			_claimHud.Visible = visible;
		if (_abilityHud != null)
			_abilityHud.Visible = false;
		if (_targetHud != null)
			_targetHud.Visible = visible;
		if (_hint != null)
			_hint.Visible = visible;
		if (_missionHud != null)
			_missionHud.Visible = visible;
		if (_mechHud != null)
			_mechHud.Visible = visible;

		var brief = GetNodeOrNull<Label>("UI/ClaimBrief");
		if (brief != null)
			brief.Visible = visible;
	}

	private void EnsureBrandHud()
	{
		var ui = GetNode("UI");

		_claimHud = GetNodeOrNull<Label>("UI/ClaimHud");
		if (_claimHud == null)
		{
			_claimHud = new Label
			{
				Name = "ClaimHud",
				Position = new Vector2(24, 8),
				Size = new Vector2(1200, 28),
				Modulate = new Color(0.85f, 0.72f, 0.42f)
			};
			_claimHud.AddThemeFontSizeOverride("font_size", 18);
			ui.AddChild(_claimHud);
		}
		else
		{
			_claimHud.Position = new Vector2(24, 8);
			_claimHud.Size = new Vector2(1200, 28);
		}

		_claimHud.Text =
			$"{VoidCorpsIdentity.ProductTitle}  //  [{ArenaSizeUtil.Label(_layout.Size)}]  {_claim.Code}  —  {_claim.DisplayName}";

		var brief = GetNodeOrNull<Label>("UI/ClaimBrief");
		if (brief == null)
		{
			brief = new Label
			{
				Name = "ClaimBrief",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Modulate = new Color(0.72f, 0.8f, 0.86f, 0.9f),
				Text = _claim.Brief
			};
			brief.AddThemeFontSizeOverride("font_size", 14);
			ui.AddChild(brief);
		}

		// Sit under sector title, above LIVES / SCRAP.
		brief.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		brief.Position = new Vector2(24, 34);
		brief.Size = new Vector2(1100, 28);
		brief.OffsetLeft = 24;
		brief.OffsetTop = 34;
		brief.OffsetRight = 1124;
		brief.OffsetBottom = 62;
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var corp = session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename;
		var contract = session is { MatchFromCampaign: true, LastMissionManufacturerId.Length: > 0 }
			? GameCatalog.GetManufacturer(session.LastMissionManufacturerId).DisplayName
			: session?.Profile.AffiliatedManufacturerId is { Length: > 0 } id
				? GameCatalog.GetManufacturer(id).DisplayName
				: "open contract";
		brief.Text = $"{corp} // {contract}\n{_claim.Brief}";
		brief.Modulate = new Color(0.72f, 0.8f, 0.86f, 0.9f);

		if (_hud != null)
		{
			_hud.Position = new Vector2(24, 66);
			_hud.Size = new Vector2(1100, 32);
		}

		// Legacy top AbilityHud (HEAT/PWR/modules text) — kept hidden; data lives on MechHud.
		_abilityHud = GetNodeOrNull<Label>("UI/AbilityHud");
		if (_abilityHud != null)
			_abilityHud.Visible = false;

		_targetHud = GetNodeOrNull<Label>("UI/TargetHud");
		if (_targetHud == null)
		{
			_targetHud = new Label
			{
				Name = "TargetHud",
				Position = new Vector2(24, 98),
				Size = new Vector2(900, 32)
			};
			_targetHud.AddThemeFontSizeOverride("font_size", 17);
			ui.AddChild(_targetHud);
		}
		else
		{
			_targetHud.Position = new Vector2(24, 98);
		}
	}

	private void EnsureCountdownLabel()
	{
		_countdownLabel = GetNodeOrNull<Label>("UI/Countdown");
		if (_countdownLabel != null)
			return;

		_countdownLabel = new Label
		{
			Name = "Countdown",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			Modulate = new Color(0.95f, 0.82f, 0.4f)
		};
		_countdownLabel.AddThemeFontSizeOverride("font_size", 120);
		GetNode("UI").AddChild(_countdownLabel);
	}

	private void ApplyClaimMap()
	{
		_layout = ClaimArenaLayout.ForClaim(_claim);
		_playerSpawn = _layout.PlayerSpawn;
		_enemySpawnA = _layout.EnemySpawnA;
		_enemySpawnB = _layout.EnemySpawnB;

		ApplyArenaShell(_layout.Size);
		ApplyAtmosphere(_layout);
		RebuildCover(_layout);
		PlaceCrates(_layout);
	}

	/// <summary>Resize the shared floor/walls for Small / Medium / Large footprints.</summary>
	private void ApplyArenaShell(ArenaSize size)
	{
		var extent = ArenaSizeUtil.Extent(size);
		var half = ArenaSizeUtil.HalfExtent(size);
		var floorSize = new Vector3(extent, 1f, extent);
		var wallSize = new Vector3(extent, 4f, 1f);

		SetBoxMeshAndShape("World/Floor/Mesh", "World/Floor/Collision", floorSize);
		foreach (var name in new[] { "WallNorth", "WallSouth", "WallWest", "WallEast" })
			SetBoxMeshAndShape($"World/{name}/Mesh", $"World/{name}/Collision", wallSize);

		SetNodePosition("World/WallNorth", new Vector3(0f, 2f, -half));
		SetNodePosition("World/WallSouth", new Vector3(0f, 2f, half));
		SetNodePosition("World/WallWest", new Vector3(-half, 2f, 0f));
		SetNodePosition("World/WallEast", new Vector3(half, 2f, 0f));
	}

	private void SetBoxMeshAndShape(string meshPath, string collisionPath, Vector3 size)
	{
		var meshInst = GetNodeOrNull<MeshInstance3D>(meshPath);
		if (meshInst != null)
		{
			if (meshInst.Mesh is BoxMesh box)
				box.Size = size;
			else
				meshInst.Mesh = new BoxMesh { Size = size };
		}

		var collision = GetNodeOrNull<CollisionShape3D>(collisionPath);
		if (collision != null)
		{
			if (collision.Shape is BoxShape3D shape)
				shape.Size = size;
			else
				collision.Shape = new BoxShape3D { Size = size };
		}
	}

	private void SetNodePosition(string path, Vector3 position)
	{
		var node = GetNodeOrNull<Node3D>(path);
		if (node != null)
			node.Position = position;
	}

	private void ApplyAtmosphere(ClaimArenaLayout layout)
	{
		var envNode = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (envNode?.Environment != null)
		{
			var env = envNode.Environment;
			env.BackgroundMode = Godot.Environment.BGMode.Color;
			env.BackgroundColor = layout.SkyColor;
			env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
			env.AmbientLightColor = layout.AmbientColor;
			env.AmbientLightEnergy = layout.AmbientEnergy;
		}

		var sun = GetNodeOrNull<DirectionalLight3D>("Sun");
		if (sun != null)
		{
			sun.LightColor = layout.SunColor;
			sun.LightEnergy = layout.SunEnergy;
			sun.RotationDegrees = layout.SunRotationDegrees;
		}

		TintWorldMeshes(layout.FloorColor, layout.WallColor);
	}

	private void TintWorldMeshes(Color floorColor, Color wallColor)
	{
		var floorMesh = GetNodeOrNull<MeshInstance3D>("World/Floor/Mesh");
		if (floorMesh != null)
		{
			var floorMat = new StandardMaterial3D
			{
				AlbedoColor = floorColor,
				Roughness = 0.92f,
				Metallic = 0.08f
			};
			MeshMat.Bind(floorMesh, floorMat);
		}

		var wallMat = new StandardMaterial3D
		{
			AlbedoColor = wallColor,
			Roughness = 0.88f,
			Metallic = 0.12f
		};

		foreach (var name in new[] { "WallNorth", "WallSouth", "WallWest", "WallEast" })
		{
			var mesh = GetNodeOrNull<MeshInstance3D>($"World/{name}/Mesh");
			if (mesh != null)
				MeshMat.Bind(mesh, wallMat);
		}
	}

	private void RebuildCover(ClaimArenaLayout layout)
	{
		var world = GetNodeOrNull<Node3D>("World");
		if (world == null)
			return;

		// Hide scene-authored defaults; runtime covers replace them per claim.
		foreach (var name in new[] { "CoverA", "CoverB" })
		{
			var legacy = world.GetNodeOrNull<Node3D>(name);
			if (legacy != null)
			{
				legacy.Visible = false;
				legacy.ProcessMode = ProcessModeEnum.Disabled;
				if (legacy is CollisionObject3D body)
					body.CollisionLayer = 0;
			}
		}

		var root = world.GetNodeOrNull<Node3D>("ClaimCover");
		if (root != null)
		{
			world.RemoveChild(root);
			root.Free();
		}

		root = new Node3D { Name = "ClaimCover" };
		world.AddChild(root);

		for (var i = 0; i < layout.Covers.Length; i++)
		{
			var piece = layout.Covers[i];
			// Slight paint variance so identical kinds don't look stamped.
			var ambience = layout.WallColor.Lightened((i % 5) * 0.03f - 0.06f);
			var built = CoverVisualFactory.Build(piece.Kind, ambience, piece.Scale);

			var body = new StaticBody3D
			{
				Name = $"Cover_{piece.Kind}_{i}",
				Position = new Vector3(piece.Position.X, 0f, piece.Position.Z),
				RotationDegrees = new Vector3(0f, piece.YawDegrees, 0f)
			};
			body.AddChild(built.Visual);

			var collision = new CollisionShape3D
			{
				Name = "Collision",
				Shape = new BoxShape3D { Size = built.CollisionSize },
				Position = built.CollisionCenter,
				Disabled = false
			};
			body.AddChild(collision);
			root.AddChild(body);
			// World layer — mechs mask layer 1; projectiles also hit layer 1.
			body.CollisionLayer = PhysicsLayers.World;
			body.CollisionMask = 0;

			if (built.Destructible)
			{
				var hp = built.Health;
				var damageable = new Damageable { Name = "Damageable", MaxHealth = hp };
				body.AddChild(damageable);
				damageable.ResetHealth(hp);
				var capturedBody = body;
				var capturedSize = built.CollisionSize;
				var shatterColor = built.ShatterColor;
				damageable.Died += () =>
				{
					if (!GodotObject.IsInstanceValid(capturedBody))
						return;
					capturedBody.CollisionLayer = 0;
					var parent = capturedBody.GetTree()?.CurrentScene ?? capturedBody.GetParent();
					if (parent != null)
					{
						var origin = capturedBody.GlobalPosition + Vector3.Up * (capturedSize.Y * 0.5f);
						ShatterBurst.Spawn(parent, origin, shatterColor, capturedSize, 18);
						LootService.SpawnWorldDrops(parent, origin, LootService.ScrapForCover());
					}

					MeshMat.QueueFreeSafe(capturedBody);
				};
			}
		}
	}

	private void PlaceCrates(ClaimArenaLayout layout)
	{
		var targets = GetNodeOrNull<Node3D>("Targets");
		if (targets == null)
			return;

		var crates = new System.Collections.Generic.List<DummyTarget>();
		foreach (var child in targets.GetChildren())
		{
			if (child is DummyTarget crate)
				crates.Add(crate);
		}

		for (var i = 0; i < crates.Count; i++)
		{
			if (i < layout.CratePositions.Length)
			{
				crates[i].Visible = true;
				crates[i].ProcessMode = ProcessModeEnum.Inherit;
				crates[i].GlobalPosition = layout.CratePositions[i];
				if (crates[i] is CollisionObject3D body)
					body.CollisionLayer = crates[i].BlocksMovement ? PhysicsLayers.World : PhysicsLayers.Targets;
			}
			else
			{
				crates[i].Visible = false;
				crates[i].ProcessMode = ProcessModeEnum.Disabled;
				crates[i].GlobalPosition = new Vector3(0f, -40f, 0f);
			}
		}

		// Spawn extras if this claim wants more scrap props than the scene authored.
		for (var i = crates.Count; i < layout.CratePositions.Length; i++)
		{
			var extra = new DummyTarget
			{
				Name = $"CrateExtra_{i}",
				MaxHealth = 80f + i * 10f,
				ShatterPieces = 12 + i,
				AliveColor = layout.WallColor.Lightened(0.25f),
				BlocksMovement = true
			};
			targets.AddChild(extra);
			extra.GlobalPosition = layout.CratePositions[i];
			extra.CollisionLayer = PhysicsLayers.World;
		}
	}

	private void PlaceCombatants()
	{
		EnsurePlayerDropBeacon();

		if (IsCoopMatch && _wingByPeer.Count > 0)
		{
			PlaceWingMechsAtSpawn();
		}
		else if (_mech != null)
		{
			_mech.Team = TeamId.Player;
			_mech.Visible = true;
			_mech.GlobalPosition = _playerSpawn;
			FaceToward(_mech, Vector3.Zero);
		}

		if (_mission == null)
			CreateMission();
		_mission!.SetupBattlefield();
		SpawnConventionDemoCradleIfNeeded();
		HookMissionTelemetry();
	}

	private void SpawnConventionDemoCradleIfNeeded()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session is not { MatchFromConvention: true })
			return;

		var mfgId = session.Campaign?.Convention.ActiveTrialManufacturerId
			?? session.LastMissionManufacturerId;
		if (string.IsNullOrEmpty(mfgId))
			return;

		var root = GetNodeOrNull<Node3D>("MissionRuntime") ?? this;
		var cradle = new DemoCradle
		{
			ManufacturerId = mfgId,
			MaxHealth = 120f
		};
		root.AddChild(cradle);
		// Offset from player spawn — visible spite target, not on the critical path.
		cradle.GlobalPosition = _playerSpawn + new Vector3(12f, 0f, 10f);
	}

	private void EnsurePlayerDropBeacon()
	{
		var existing = GetNodeOrNull<DropBeacon>("PlayerDropBeacon");
		if (existing != null)
		{
			_playerDropBeacon = existing;
			_playerDropBeacon.GlobalPosition = DropBeacon.PadBesideSpawn(_playerSpawn, limit: _layout.PadLimit);
			_playerDropBeacon.SetState(DropBeaconState.Ready);
			return;
		}

		_playerDropBeacon = DropBeacon.Create(
			"PlayerDropBeacon",
			DropBeacon.PadBesideSpawn(_playerSpawn, limit: _layout.PadLimit),
			TeamId.Player);
		AddChild(_playerDropBeacon);
	}

	private void CreateMission()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var type = session?.Match.MissionType ?? session?.PendingMission ?? MissionType.DestroyAllEnemies;
		var boss = session?.PendingBossEncounter ?? BossEncounterId.None;
		_mission = MissionFactory.Create(type, boss);
		_mission.Bind(this);
		_objectivesComplete = false;
	}

	private void EnsureMissionHud()
	{
		var ui = GetNodeOrNull("UI");
		if (ui == null)
			return;

		_missionHud = ui.GetNodeOrNull<Label>("MissionHud");
		if (_missionHud == null)
		{
			_missionHud = new Label { Name = "MissionHud" };
			_missionHud.AddThemeFontSizeOverride("font_size", 17);
			ui.AddChild(_missionHud);
		}

		// Top-right → top-center band, clear of the left claim stack.
		_missionHud.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_missionHud.AnchorLeft = 0.42f;
		_missionHud.AnchorRight = 1f;
		_missionHud.AnchorTop = 0f;
		_missionHud.AnchorBottom = 0f;
		_missionHud.OffsetLeft = 0f;
		_missionHud.OffsetTop = 12f;
		_missionHud.OffsetRight = -28f;
		_missionHud.OffsetBottom = 56f;
		_missionHud.HorizontalAlignment = HorizontalAlignment.Right;
		_missionHud.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_missionHud.Modulate = new Color(0.75f, 0.88f, 0.95f);
	}

	private void EnsureMechHud()
	{
		var ui = GetNodeOrNull("UI");
		if (ui == null)
			return;

		ui.GetNodeOrNull("ComponentIntegrityHud")?.QueueFree();

		_mechHud = ui.GetNodeOrNull<MechHud>("MechHud");
		if (_mechHud != null)
		{
			_mechHud.ApplyLayout();
			return;
		}

		_mechHud = new MechHud
		{
			Name = "MechHud",
			Visible = false
		};
		ui.AddChild(_mechHud);
		_mechHud.ApplyLayout();
	}

	// --- IMissionHost ---
	public Node3D Root => this;
	public MechController? Player => _mech;
	public Vector3 PlayerSpawn => _playerSpawn;
	public Vector3 EnemySpawnA => _enemySpawnA;
	public Vector3 EnemySpawnB => _enemySpawnB;
	public ClaimArenaLayout Layout => _layout;
	public PilotDifficulty Difficulty => EnemyDifficulty;
	public bool MatchResolved => _matchResolved;
	public bool IsFighting => _phase == MatchPhase.Fighting && !_matchResolved;
	public bool ObjectivesComplete => _objectivesComplete;
	public DropBeacon? PlayerDropBeacon => _playerDropBeacon;

	public void ReportMissionOutcome(MatchOutcome outcome) => ResolveMatch(outcome);

	public void NotifyObjectivesComplete()
	{
		if (_matchResolved || _objectivesComplete)
			return;

		_objectivesComplete = true;
		_playerDropBeacon?.ArmExtract();
		SfxService.Confirm();
		SfxService.Play("alarm", 0.85f, -4f);
		GD.Print("Objectives complete — return to drop beacon and hold Interact to extract.");
	}

	MechController? IMissionHost.SpawnEnemyMech(string name, Vector3 position, int variant, bool viaDropBeacon) =>
		SpawnEnemyMech(name, position, variant, viaDropBeacon);

	void IMissionHost.DespawnEnemyMech(string name) => DespawnEnemyMech(name);

	SupportUnit IMissionHost.SpawnSupport(string name, string unitId, TeamId team, Vector3 position)
	{
		SpawnSupport(name, unitId, team, position);
		return GetNodeOrNull<SupportUnit>(name)!;
	}

	void IMissionHost.DespawnSupport(string name) => DespawnSupport(name);

	bool IMissionHost.AllEnemyMechsDown() => AllEnemyMechsDown();

	void IMissionHost.FaceToward(Node3D body, Vector3 worldPoint) => FaceToward(body, worldPoint);

	private void SpawnSupport(string name, string unitId, TeamId team, Vector3 position)
	{
		var existing = GetNodeOrNull<SupportUnit>(name);
		if (existing != null)
		{
			existing.Configure(unitId, team);
			existing.GlobalPosition = position;
			existing.Visible = true;
			existing.ProcessMode = ProcessModeEnum.Inherit;
			FaceToward(existing, Vector3.Zero);
			if (GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true })
				existing.AttachHostReplication();
			return;
		}

		var unit = SupportUnit.Create(unitId, team, name);
		AddChild(unit);
		unit.Configure(unitId, team);
		unit.GlobalPosition = position;
		FaceToward(unit, Vector3.Zero);
		if (GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true })
			unit.AttachHostReplication();
	}

	private void DespawnSupport(string name)
	{
		var unit = GetNodeOrNull<SupportUnit>(name);
		if (unit == null)
			return;
		unit.Visible = false;
		unit.ProcessMode = ProcessModeEnum.Disabled;
		unit.GlobalPosition = new Vector3(0f, -50f, 0f);
	}

	private MechController? SpawnEnemyMech(string name, Vector3 position, int variant, bool viaDropBeacon = false)
	{
		MechController? enemy = GetNodeOrNull<MechController>(name);
		var loadout = GameCatalog.CreateEnemyLoadout(variant);
		_enemyLoadouts[name] = loadout;
		_enemyResolved.Remove(name);

		if (enemy == null)
		{
			var packed = GD.Load<PackedScene>("res://scenes/mech.tscn");
			if (packed == null)
				return null;

			enemy = packed.Instantiate<MechController>();
			enemy.Name = name;
			enemy.IsPlayerControlled = false;
			enemy.Team = TeamId.Enemy;
			AddChild(enemy);
			enemy.RebuildFromLoadout(loadout);
		}
		else
		{
			enemy.RebuildFromLoadout(loadout);
		}

		enemy.Team = TeamId.Enemy;
		enemy.Visible = true;
		enemy.ProcessMode = ProcessModeEnum.Inherit;
		FaceToward(enemy, Vector3.Zero);
		HookEnemyDeath(enemy);

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { IsOnline: true })
			enemy.AttachHostReplication();

		var pilot = enemy.GetNodeOrNull<MechPilotAI>("MechPilotAI");
		if (pilot != null)
		{
			pilot.PreferredRange = 18f + variant * 4f;
			pilot.ApplyDifficulty(EnemyDifficulty);
			if (EnemyDifficulty == PilotDifficulty.Hard)
				pilot.Aggression = variant == 0 ? 0.75f : 0.9f;
			if (_mech != null)
				pilot.SetTarget(PickAiTarget());
		}

		if (viaDropBeacon)
		{
			BeginDropIn(enemy, position, enableAiWhenDone: true, createBeacon: true);
		}
		else
		{
			enemy.GlobalPosition = position;
			enemy.SetControlsEnabled(true);
		}

		return enemy;
	}

	private MechController? PickAiTarget()
	{
		MechController? best = null;
		var bestDist = float.MaxValue;
		foreach (var mech in _wingByPeer.Values)
		{
			if (!TeamUtil.IsAliveCombatant(mech))
				continue;
			var d = mech.GlobalPosition.LengthSquared();
			// Prefer nearest to origin / map center as a stable default; callers can refine later.
			if (d < bestDist)
			{
				bestDist = d;
				best = mech;
			}
		}

		return best ?? _mech;
	}

	private const float PlayerDropHeight = 28f;
	private const float VesselOpenDuration = 0.75f;

	private void BeginDropIn(MechController mech, Vector3 landing, bool enableAiWhenDone, bool createBeacon, float? durationOverride = null)
	{
		DropBeacon? beacon = null;
		if (createBeacon)
		{
			var beaconName = $"DropBeacon_{mech.Name}";
			var old = GetNodeOrNull<DropBeacon>(beaconName);
			old?.QueueFree();

			var pad = DropBeacon.PadBesideSpawn(landing, limit: _layout.PadLimit);
			beacon = DropBeacon.Create(beaconName, pad, mech.Team);
			landing = new Vector3(pad.X, 0f, pad.Z);
			AddChild(beacon);
		}
		else
		{
			beacon = _playerDropBeacon;
			if (beacon != null)
				landing = new Vector3(beacon.GlobalPosition.X, 0f, beacon.GlobalPosition.Z);
			else
				landing = new Vector3(landing.X, 0f, landing.Z);
		}

		var duration = durationOverride ?? 1.35f;
		var startY = mech.GlobalPosition.Y > landing.Y + 4f
			? mech.GlobalPosition.Y
			: landing.Y + PlayerDropHeight;

		SealMechInVessel(mech, beacon, landing, startY);

		_dropIns.Add(new DropInSequence
		{
			Mech = mech,
			Landing = landing,
			FallDuration = duration,
			OpenDuration = VesselOpenDuration,
			Beacon = beacon,
			EnableAiWhenDone = enableAiWhenDone,
			Elapsed = 0f,
			StartY = startY,
			Opening = false
		});

		SfxService.Play("alarm", 1.15f, -7f);
	}

	private static void SealMechInVessel(MechController mech, DropBeacon? beacon, Vector3 landing, float altitude)
	{
		var pos = new Vector3(landing.X, altitude, landing.Z);
		if (beacon != null)
		{
			beacon.GlobalPosition = pos;
			beacon.MarkDropping();
		}

		mech.Visible = false;
		mech.GlobalPosition = pos;
		mech.Velocity = Vector3.Zero;
		mech.SetControlsEnabled(false);
		if (!mech.IsPlayerControlled)
		{
			var pilot = mech.GetNodeOrNull<MechPilotAI>("MechPilotAI");
			pilot?.SetTarget(null);
		}
	}

	/// <summary>Park the sealed delivery vessel at drop altitude for countdown 5→2.</summary>
	private void StagePlayerDropHold()
	{
		if (_mech == null)
			return;

		EnsurePlayerDropBeacon();
		var land = _playerSpawn;
		if (_playerDropBeacon != null)
		{
			land = new Vector3(
				_playerDropBeacon.GlobalPosition.X,
				0f,
				_playerDropBeacon.GlobalPosition.Z);
		}

		SealMechInVessel(_mech, _playerDropBeacon, land, land.Y + PlayerDropHeight);
	}

	private void TickDropIns(float dt)
	{
		for (var i = _dropIns.Count - 1; i >= 0; i--)
		{
			var seq = _dropIns[i];
			if (!IsInstanceValid(seq.Mech))
			{
				_dropIns.RemoveAt(i);
				continue;
			}

			seq.Elapsed += dt;

			if (!seq.Opening)
			{
				var t = Mathf.Clamp(seq.Elapsed / Mathf.Max(0.05f, seq.FallDuration), 0f, 1f);
				var eased = t * t;
				var y = Mathf.Lerp(seq.StartY, seq.Landing.Y, eased);
				var pos = new Vector3(seq.Landing.X, y, seq.Landing.Z);
				seq.Mech.GlobalPosition = pos;
				seq.Mech.Velocity = Vector3.Zero;
				if (seq.Beacon != null && IsInstanceValid(seq.Beacon))
					seq.Beacon.GlobalPosition = pos;

				if (t < 1f)
					continue;

				seq.Opening = true;
				seq.Elapsed = 0f;
				seq.Mech.GlobalPosition = seq.Landing;
				if (seq.Beacon != null && IsInstanceValid(seq.Beacon))
				{
					seq.Beacon.GlobalPosition = seq.Landing;
					seq.Beacon.MarkOpening();
					seq.Beacon.SetOpenAmount(0f);
				}

				SfxService.Play("drop_impact", 1f, -2f);
				continue;
			}

			var openT = Mathf.Clamp(seq.Elapsed / Mathf.Max(0.05f, seq.OpenDuration), 0f, 1f);
			seq.Beacon?.SetOpenAmount(openT);

			if (openT >= 0.45f && !seq.Mech.Visible)
			{
				seq.Mech.Visible = true;
				seq.Mech.GlobalPosition = seq.Landing + new Vector3(0f, 0.05f, 0f);
			}

			if (openT < 1f)
				continue;

			seq.Mech.Visible = true;
			seq.Mech.GlobalPosition = seq.Landing;
			seq.Mech.SetControlsEnabled(_phase == MatchPhase.Fighting);
			seq.Beacon?.MarkReady();
			if (_objectivesComplete && seq.Beacon == _playerDropBeacon)
				_playerDropBeacon?.ArmExtract();

			if (seq.EnableAiWhenDone && !seq.Mech.IsPlayerControlled && _phase == MatchPhase.Fighting)
			{
				var pilot = seq.Mech.GetNodeOrNull<MechPilotAI>("MechPilotAI");
				if (pilot != null)
					pilot.SetTarget(PickAiTarget());
			}

			_dropIns.RemoveAt(i);
		}
	}

	private void TickExtraction(float dt)
	{
		if (!_objectivesComplete || _matchResolved || _playerDropBeacon == null || _mech == null)
			return;
		if (_phase != MatchPhase.Fighting)
			return;

		var holding = Input.IsActionPressed("interact");
		if (_playerDropBeacon.TickExtract(dt, holding, _mech.GlobalPosition))
		{
			SfxService.Confirm();
			ResolveMatch(MatchOutcome.Victory);
		}
	}

	private void HookPlayerDeath()
	{
		if (_mech == null || _playerDeathHooked)
			return;

		_playerDeathHooked = true;
		if (_mech.Health != null)
		{
			_mech.Health.Died += OnPlayerDown;
			HookDamageTelemetry(_mech.Health, TelemetryTargetKind.Map, playerOwned: true);
		}
		if (_mech.Integrity != null)
			_mech.Integrity.MechCollapsed += OnPlayerDown;
	}

	private void HookMissionTelemetry()
	{
		var missionRoot = GetNodeOrNull<Node>("MissionRuntime");
		if (missionRoot == null)
			return;

		foreach (var node in missionRoot.GetChildren())
		{
			if (node is EscortAsset escort && escort.GetNodeOrNull<Damageable>("Damageable") is { } dmg)
				HookDamageTelemetry(dmg, TelemetryTargetKind.Escort, playerOwned: true);
		}
	}

	private void HookDamageTelemetry(Damageable damageable, TelemetryTargetKind kind, bool playerOwned)
	{
		var id = damageable.GetInstanceId();
		if (!_telemetryDamageHooked.Add(id))
			return;

		damageable.Damaged += (amount, _) =>
		{
			if (amount <= 0.01f || !playerOwned)
				return;
			GetNodeOrNull<GameSession>("/root/GameSession")?.Match.Telemetry.RecordDamageTaken(amount, kind);
			if (kind == TelemetryTargetKind.Map)
				SfxService.PlayDamageSustained();
		};
	}

	private void HookEnemyDeath(MechController enemy)
	{
		if (!_enemyDeathHooked.Add(enemy.Name))
			return;

		if (enemy.Health != null)
			enemy.Health.Died += () => OnEnemyMechDown(enemy);
		if (enemy.Integrity != null)
			enemy.Integrity.MechCollapsed += () => OnEnemyMechDown(enemy);
	}

	private void OnPlayerDown()
	{
		if (_matchResolved || _phase != MatchPhase.Fighting || _playerDownPending)
			return;

		_playerDownPending = true;
		CallDeferred(MethodName.HandlePlayerDown);
	}

	private void HandlePlayerDown()
	{
		_playerDownPending = false;
		if (_matchResolved || _phase != MatchPhase.Fighting)
			return;

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { IsOnline: true } && !Multiplayer.IsServer())
			return;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
		{
			ResolveMatch(MatchOutcome.Defeat);
			return;
		}

		MechController? downed = null;
		foreach (var mech in _wingByPeer.Values)
		{
			if (mech.Health?.IsDead == true || mech.Integrity?.IsCollapsed == true)
			{
				downed = mech;
				break;
			}
		}

		downed ??= _mech;
		if (downed == null)
		{
			ResolveMatch(MatchOutcome.Defeat);
			return;
		}

		if (session.Match.TrySpendLifeForRespawn())
		{
			var loadout = _wingLoadouts.GetValueOrDefault(downed.OwningPeerId)
			              ?? (downed.OwningPeerId == (net?.LocalPeerId ?? 0)
				              ? session.CurrentLoadout
				              : GameCatalog.CreateStarterLoadout());
			var pad = downed.GlobalPosition with { Y = 0f };
			if (pad.LengthSquared() < 0.01f)
				pad = _playerSpawn;
			downed.RespawnAt(pad, loadout);
			FaceToward(downed, Vector3.Zero);
			SetCombatActive(true);
			GD.Print($"Pilot remount (peer {downed.OwningPeerId}). Lives remaining: {session.Match.LivesRemaining}");
			return;
		}

		// Cadet range cannot fail — free remount forever.
		if (session is { MatchFromAcademy: true, PendingMission: MissionType.CadetRange })
		{
			downed.RespawnAt(_playerSpawn, session.CurrentLoadout);
			FaceToward(downed, Vector3.Zero);
			SetCombatActive(true);
			return;
		}

		ResolveMatch(MatchOutcome.Defeat);
	}

	private void OnEnemyMechDown(MechController enemy)
	{
		if (_matchResolved || _phase != MatchPhase.Fighting)
			return;
		if (!_enemyResolved.Add(enemy.Name))
			return;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session?.Match.Active == true)
		{
			_enemyLoadouts.TryGetValue(enemy.Name, out var loadout);
			var parent = (Node)this;
			var maxTier = session.CurrentMaxLootTier();
			LootService.SpawnWorldDrops(
				parent,
				enemy.GlobalPosition,
				LootService.ScrapForEnemyMech(),
				LootService.RollEnemyMechPartDrop(loadout, maxTier));
		}

		enemy.Visible = false;
		enemy.ProcessMode = ProcessModeEnum.Disabled;
		enemy.SetControlsEnabled(false);

		_mission?.NotifyEnemyMechDown(enemy);
	}

	private bool AllEnemyMechsDown()
	{
		foreach (var child in GetChildren())
		{
			if (child is not MechController mech || mech.IsPlayerControlled || mech.Team != TeamId.Enemy)
				continue;
			if (!mech.Visible || mech.ProcessMode == ProcessModeEnum.Disabled)
				continue;
			if (_enemyResolved.Contains(mech.Name))
				continue;
			if (mech.Integrity?.IsCollapsed == true || mech.Health?.IsDead == true)
				continue;
			return false;
		}

		return true;
	}

	private void ResolveMatch(MatchOutcome outcome)
	{
		if (_matchResolved)
			return;

		if (Multiplayer.MultiplayerPeer != null && !Multiplayer.IsServer())
			return;

		_matchResolved = true;
		_phase = MatchPhase.Fighting;
		_dropIns.Clear();
		SetCombatActive(false);
		if (_garage != null)
			_garage.Visible = false;
		SetCombatHudVisible(false);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.Match.End(outcome);
		if (session is { MatchFromAcademy: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnAcademyMissionResolved(outcome);
		else if (session is { MatchFromConvention: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnConventionTrialResolved(outcome);
		else if (session is { MatchFromCampaign: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnCampaignNodeResolved(outcome);
		SfxService.Play(outcome == MatchOutcome.Victory ? "victory" : "defeat", 1f, -2f);

		if (Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
			_netCombat?.HostShowResults((int)outcome);
		else if (_results != null && session != null)
		{
			_results.MouseFilter = Control.MouseFilterEnum.Stop;
			_results.Open(session);
			_results.MoveToFront();
		}
	}

	private void TryBuyLifeWithScrap()
	{
		if (_matchResolved || _phase != MatchPhase.Fighting)
			return;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session?.Match.TryBuyLifeWithScrap() == true)
		{
			SfxService.Confirm();
			GD.Print($"Bought life. Lives {session.Match.LivesRemaining}, scrap {session.Match.RunScrap}.");
		}
		else
			SfxService.Play("alarm", 1.1f, -6f);
	}

	private void DespawnEnemyMech(string name)
	{
		var enemy = GetNodeOrNull<MechController>(name);
		if (enemy == null)
			return;

		enemy.Visible = false;
		enemy.ProcessMode = ProcessModeEnum.Disabled;
		enemy.GlobalPosition = new Vector3(0f, -50f, 0f);
		var pilot = enemy.GetNodeOrNull<MechPilotAI>("MechPilotAI");
		pilot?.SetTarget(null);
	}

	private static void FaceToward(Node3D body, Vector3 worldPoint)
	{
		var dir = worldPoint - body.GlobalPosition;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			return;
		body.LookAt(body.GlobalPosition + dir.Normalized(), Vector3.Up);
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("pause") && CanOpenPause())
			TogglePauseMenu();

		if (GetTree().Paused || (_pauseMenu?.IsOpen ?? false))
			return;

		var dt = (float)delta;
		TickDropIns(dt);

		if (_phase == MatchPhase.Countdown)
			TickCountdown(dt);

		if (!_matchResolved
			&& _phase == MatchPhase.Fighting
			&& Input.IsActionJustPressed("toggle_garage")
			&& _garage != null
			&& _dropIns.Count == 0)
		{
			_garage.Visible = !_garage.Visible;
			_garage.ConfigurePrepMode(false);
			SetLocalWingControls(!_garage.Visible);
			SetCombatHudVisible(!_garage.Visible);
			if (_garage.Visible)
			{
				_garage.MoveToFront();
				_garage.RefreshFromSession();
			}
		}

		if (!_matchResolved && _phase == MatchPhase.Fighting)
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			var isAuthority = Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer();
			if (isAuthority)
				session?.Match.Tick(dt);

			_matchHudSyncTimer += dt;
			if (_matchHudSyncTimer >= 0.35f && Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
			{
				_matchHudSyncTimer = 0f;
				if (session?.Match != null)
				{
					_netCombat?.HostSyncMatchHud(
						session.Match.LivesRemaining,
						session.Match.RunScrap,
						session.Match.NextLifeCost);
				}
			}

			var buyPressed = Input.IsActionPressed("buy_life");
			if (buyPressed && !_buyLifeLatch)
			{
				_buyLifeLatch = true;
				if (isAuthority)
					TryBuyLifeWithScrap();
			}
			else if (!buyPressed)
			{
				_buyLifeLatch = false;
			}

			if (isAuthority)
			{
				TickExtraction(dt);
				_mission?.Tick(dt);
			}
		}

		UpdateHud();
	}

	private void OnReadyPressed(LoadoutData loadout)
	{
		if (_phase != MatchPhase.Prep)
		{
			// Mid-fight garage apply: only this peer's MAP + profile.
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			session?.SetLoadout(loadout);
			_mech?.RebuildFromLoadout(loadout);
			if (IsCoopMatch && Multiplayer.MultiplayerPeer != null && _mech != null)
				Rpc(MethodName.RpcApplyWingLoadout, Multiplayer.GetUniqueId(), loadout.ToDict());
			if (_garage != null)
				_garage.Visible = false;
			SetLocalWingControls(true);
			SetCombatHudVisible(true);
			return;
		}

		BeginCountdown(loadout);
	}

	private void BeginCountdown(LoadoutData loadout)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.SetLoadout(loadout);
		_mech?.RebuildFromLoadout(loadout);

		if (_garage != null)
		{
			_garage.Visible = false;
			_garage.ConfigurePrepMode(false);
		}

		SetCombatHudVisible(false);
		_enemyResolved.Clear();
		_objectivesComplete = false;
		_dropIns.Clear();
		PlaceCombatants();
		StagePlayerDropHold();
		SetCombatActive(false);

		_phase = MatchPhase.Countdown;
		_countdownRemaining = 5.25f;
		_lastCountdownSecond = -1;
		_playerDropStarted = false;
		MusicService.Cue(MusicCue.Combat);
		if (_countdownLabel != null)
		{
			_countdownLabel.Visible = true;
			_countdownLabel.Text = "5";
		}
	}

	private void TickCountdown(float dt)
	{
		_countdownRemaining -= dt;
		var second = Mathf.CeilToInt(_countdownRemaining);

		// Drop on "1" so the mech is grounded when FIGHT starts.
		if (!_playerDropStarted && second <= 1 && _countdownRemaining > 0f && _mech != null)
		{
			_playerDropStarted = true;
			var fallTime = Mathf.Clamp(
				_countdownRemaining - VesselOpenDuration - 0.05f,
				0.4f,
				1.1f);
			if (IsCoopMatch && _wingByPeer.Count > 0)
				DropAllWings(fallTime);
			else
				BeginDropIn(_mech, _playerSpawn, enableAiWhenDone: false, createBeacon: false, fallTime);
		}

		if (_countdownRemaining <= 0f)
		{
			if (_countdownLabel != null)
			{
				_countdownLabel.Text = "FIGHT!";
				_countdownLabel.Modulate = new Color(1f, 0.35f, 0.25f);
			}

			_phase = MatchPhase.Fighting;
			SetCombatActive(true);
			SetCombatHudVisible(true);
			_mission?.OnFightStarted();
			SfxService.Play("fight", 1f, -1f);

			// Safety: if the timed drop never started, drop now (still rare).
			if (!_playerDropStarted && _mech != null)
			{
				_playerDropStarted = true;
				BeginDropIn(_mech, _playerSpawn, enableAiWhenDone: false, createBeacon: false);
			}

			var tree = GetTree();
			if (tree != null && _countdownLabel != null)
			{
				var timer = tree.CreateTimer(0.85);
				timer.Timeout += () =>
				{
					if (IsInstanceValid(_countdownLabel))
						_countdownLabel.Visible = false;
				};
			}
			return;
		}

		if (second != _lastCountdownSecond && _countdownLabel != null)
		{
			_lastCountdownSecond = second;
			_countdownLabel.Text = second.ToString();
			_countdownLabel.Modulate = new Color(0.95f, 0.82f, 0.4f);
			if (second > 0)
				SfxService.Play("countdown", 1f, -3f);
		}
	}

	private void SetCombatActive(bool active)
	{
		foreach (var child in GetChildren())
		{
			if (child is MechController mech)
			{
				var dropping = false;
				foreach (var seq in _dropIns)
				{
					if (seq.Mech == mech)
					{
						dropping = true;
						break;
					}
				}

				mech.SetControlsEnabled(active && !dropping);
				mech.Velocity = Vector3.Zero;
			}
			else if (child is SupportUnit support)
			{
				support.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
				support.Velocity = Vector3.Zero;
			}
			else if (child is Node3D group && group.Name == "MissionRuntime")
			{
				foreach (var nested in group.GetChildren())
				{
					if (nested is EscortAsset escort)
					{
						escort.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
						if (!active)
							escort.Velocity = Vector3.Zero;
					}
					else if (nested is SupportUnit nestedSupport)
					{
						nestedSupport.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
						nestedSupport.Velocity = Vector3.Zero;
					}
				}
			}
		}
	}

	/// <summary>Enable/disable only the local pilot's MAP — never the whole wing or enemy AI.</summary>
	private void SetLocalWingControls(bool enabled)
	{
		if (_mech == null)
			return;
		var dropping = false;
		foreach (var seq in _dropIns)
		{
			if (seq.Mech == _mech)
			{
				dropping = true;
				break;
			}
		}

		_mech.SetControlsEnabled(enabled && !dropping && _phase == MatchPhase.Fighting);
		if (!enabled)
			_mech.Velocity = Vector3.Zero;
	}

	public void ClientPresentResults(MatchOutcome outcome)
	{
		_matchResolved = true;
		_phase = MatchPhase.Fighting;
		SetLocalWingControls(false);
		if (_garage != null)
			_garage.Visible = false;
		SetCombatHudVisible(false);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return;

		if (session.Match.Outcome == MatchOutcome.InProgress)
			session.Match.End(outcome);

		if (_results == null || _results.Visible)
			return;

		_results.MouseFilter = Control.MouseFilterEnum.Stop;
		_results.Open(session);
		_results.MoveToFront();
	}

	private void UpdateHud()
	{
		if (_mech?.Health == null)
			return;

		_mechHud?.Refresh(_mech);

		if (_hud == null)
			return;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var match = session?.Match;
		var sponsorLine = session?.InCampaign == true && !string.IsNullOrEmpty(session.Profile.AffiliatedManufacturerId)
			? $"  |  {GameCatalog.GetManufacturer(session.Profile.AffiliatedManufacturerId).DisplayName} REP {session.Profile.ReputationWith(session.Profile.AffiliatedManufacturerId):+0;-0;0}"
			: "";
		var runLine = match == null
			? ""
			: $"LIVES {match.LivesRemaining}  |  SCRAP {match.RunScrap}  |  B buy life ({match.NextLifeCost}){sponsorLine}";

		var power = _mech.PowerHeat;
		var cloaked = _mech.Abilities?.IsCloaked == true ? "  |  SHROUD" : "";
		var sd = _mech.Integrity?.IsSelfDestructArmed == true
			? $"  |  DENY-ASSET {Mathf.CeilToInt((_mech.Integrity.SelfDestructProgress) * 100f)}%"
			: "";
		var sprint = _mech.IsSprinting ? "  |  SPRINT" : "";
		var overheat = power?.IsOverheated == true ? "  |  OVERHEAT" : "";
		_hud.Text = $"{runLine}{sprint}{overheat}{cloaked}{sd}";

		if (_targetHud != null)
		{
			_targetHud.Text = string.IsNullOrEmpty(_mech.AimedComponentLabel)
				? ""
				: $"SYSTEM LOCK: {_mech.AimedComponentLabel}";
		}

		if (_missionHud != null)
		{
			var line = _mission?.GetHudLine() ?? "";
			var sessionHud = GetNodeOrNull<GameSession>("/root/GameSession");
			if (sessionHud is { MatchFromConvention: true }
			    && sessionHud.Campaign?.Convention.ActiveTrialSabotaged != true)
			{
				line += "  |  Floor model nearby — expensive if it shatters";
			}

			_missionHud.Text = line;
		}

		if (_hint != null && _garage != null)
		{
			_hint.Text = _matchResolved
				? "Skirmish complete — field exchange open"
				: _phase switch
				{
					MatchPhase.Prep => "PREP SCREEN  |  assemble your MAP  |  press READY when staged",
					MatchPhase.Countdown => "Claim dispute commencing...",
					_ when _garage.Visible => "T close garage  |  Deploy updates loadout",
					_ when _objectivesComplete && _playerDropBeacon != null
						&& _playerDropBeacon.Contains(_mech.GlobalPosition)
						=> "Hold E — signal retrieval at drop beacon",
					_ when _objectivesComplete
						=> "OBJECTIVES COMPLETE — return to your drop beacon and hold E to extract",
					_ => "Shift sprint  |  LMB/RMB weapons  |  B buy life  |  E extract  |  1-6 modules  |  T field garage"
				};
		}
	}
}
