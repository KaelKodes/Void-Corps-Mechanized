using System;
using System.Collections.Generic;
using System.Linq;
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
	private AimCrosshair? _aimCrosshair;
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
	private string _activeRivalPilotId = "";
	private string _activeRivalEnemyName = "";
	private bool _rivalAssigned;
	private MechChassisClass _activeBossChassisClass = MechChassisClass.Standard;
	private readonly List<FieldPartCrate> _fieldCrates = new();
	private readonly Dictionary<string, PendingTradeClaim> _pendingTradeClaims = new();

	private sealed class PendingTradeClaim
	{
		public required int OwnerPeerId;
		public required PartSlot Slot;
		public required string InstanceId;
		public required string PartId;
		public required Vector3 Position;
		public required MechController Mech;
	}

	private bool _playerDropStarted;

	private NetCombatBus? _netCombat;
	private DeploymentDirector? _deployment;
	private float _matchHudSyncTimer;
	private PadBarrierRing? _padBarriers;

	public override void _Ready()
	{
		_mech = GetNodeOrNull<MechController>(MechPath);
		_garage = GetNodeOrNull<GarageUi>(GaragePath);
		_hud = GetNodeOrNull<Label>(HudPath);
		_hint = GetNodeOrNull<Label>("UI/Hint");

		_netCombat = new NetCombatBus();
		_netCombat.EnsureUnder(this);
		_deployment = new DeploymentDirector { Name = "DeploymentDirector" };
		AddChild(_deployment);
		_deployment.Bind(
			this,
			PickAiTarget,
			() => _phase == MatchPhase.Fighting && !_matchResolved,
			() => _playerDropBeacon,
			beacon =>
			{
				if (_objectivesComplete && beacon == _playerDropBeacon)
					_playerDropBeacon?.ArmExtract();
			});
		_deployment.PhaseChanged += OnDeploymentPhaseChanged;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session != null)
		{
			// Mission-exclusive corridors always win; never the other way around.
			if (session.PendingMission == MissionType.Sabotage
			    || session.Match.MissionType == MissionType.Sabotage)
			{
				_claim = VoidCorpsIdentity.FindClaim(SabotageMission.ClaimCode)
				         ?? session.CurrentClaim;
			}
			else if (session.PendingMission == MissionType.Escort
			         || session.Match.MissionType == MissionType.Escort)
			{
				_claim = VoidCorpsIdentity.FindClaim(EscortMission.ClaimCode)
				         ?? session.CurrentClaim;
			}
			else if (session.CurrentClaim.SabotageOnly
			         || session.CurrentClaim.MissionOnly
			         || session.CurrentClaim.Code == SabotageMission.ClaimCode
			         || session.CurrentClaim.Code == EscortMission.ClaimCode)
			{
				_claim = VoidCorpsIdentity.PickClaimSite();
				session.SetClaim(_claim);
			}
			else
			{
				_claim = session.CurrentClaim;
			}

			if (session.PendingMission == MissionType.BossEncounter)
			{
				var encounter = BossEncounterCatalog.Get(session.PendingBossEncounter);
				_activeRivalPilotId = encounter.RivalPilotId;
				_activeBossChassisClass = encounter.ChassisClass;
			}
			else
			{
				_activeRivalPilotId = session.PendingRivalPilotId;
				_activeBossChassisClass = MechChassisClass.Standard;
			}
		}
		else
		{
			_claim = VoidCorpsIdentity.PickClaimSite();
		}
		SyncMatchFromSession(session);
		ApplyClaimMap();
		CreateMission();

		EnsureBrandHud();
		EnsureCountdownLabel();
		EnsureResultsUi();
		EnsurePauseMenu();
		EnsureMissionHud();
		EnsureMechHud();
		EnsureAimCrosshair();
		SetupCoopWings();
		PlaceCombatants();
		if (IsCoopMatch)
			HookAllWingDeaths();
		else
			HookPlayerDeath();

		var skipHangarPrep = session?.SkirmishLoaner != null
			|| (session is { MatchFromAcademy: true } && !IsCoopMatch);
		if (_garage != null)
		{
			_garage.ConfigurePrepMode(true);
			if (IsCoopMatch)
				_garage.LoadoutApplied += OnCoopReadyPressed;
			else
				_garage.LoadoutApplied += OnReadyPressed;
			_garage.FieldDeliveryRequested += OnFieldDeliveryRequested;
			_garage.VisibilityChanged += OnGarageVisibilityChanged;
			_garage.Visible = !skipHangarPrep;
			if (!skipHangarPrep)
			{
				_garage.MoveToFront();
				_garage.RefreshFromSession();
			}
		}

		SetCombatActive(false);
		SetCombatHudVisible(false);

		if (skipHangarPrep)
		{
			// Academy / skirmish loaners skip hangar and drop straight into the match.
			var loaner = session!.CurrentLoadout.Clone();
			if (IsCoopMatch)
				OnCoopReadyPressed(loaner);
			else
				BeginCountdown(loaner);
			RefreshCombatMousePolicy();
			UpdateHud();
			return;
		}

		_phase = MatchPhase.Prep;
		MusicService.Cue(MusicCue.Hangar);
		RefreshCombatMousePolicy();
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
		if (_pauseMenu == null)
		{
			_pauseMenu = new PauseMenuUi { Name = "PauseMenuUi", ZIndex = 100 };
			ui.AddChild(_pauseMenu);
		}

		_pauseMenu.ZIndex = 100;
		_pauseMenu.Closed -= OnPauseMenuClosed;
		_pauseMenu.Closed += OnPauseMenuClosed;
	}

	private void OnPauseMenuClosed()
	{
		if (GetTree() != null)
			GetTree().Paused = false;
		RefreshCombatMousePolicy();
		_mechHud?.Refresh(_mech);
		_aimCrosshair?.Refresh(_mech);
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
		RefreshCombatMousePolicy();
		_mechHud?.Refresh(_mech);
		_aimCrosshair?.Refresh(_mech);
	}

	private void OnGarageVisibilityChanged()
	{
		if (_garage == null)
			return;

		// Field hangar mid-fight: freeze wing + hide combat chrome while browsing.
		if (_phase == MatchPhase.Fighting)
		{
			SetLocalWingControls(!_garage.Visible);
			SetCombatHudVisible(!_garage.Visible);
		}

		RefreshCombatMousePolicy();
		UpdateHud();
	}

	/// <summary>
	/// Mouse stays free in hangar prep / pause / results.
	/// Capture only once countdown or fight is live (FP), unless UI is up.
	/// </summary>
	private void RefreshCombatMousePolicy()
	{
		var uiBlocks = _matchResolved
			|| _phase == MatchPhase.Prep
			|| (_pauseMenu?.IsOpen == true)
			|| (_results?.Visible == true)
			|| (_garage?.Visible == true);
		SetCameraUiCaptureBlocked(uiBlocks);
	}

	private void SetCameraUiCaptureBlocked(bool blocked)
	{
		if (GetViewport()?.GetCamera3D() is TopDownCamera camera)
			camera.SetUiBlocksCapture(blocked);
		else if (blocked)
			Input.MouseMode = Input.MouseModeEnum.Visible;
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
		{
			_mechHud.Visible = visible;
			if (visible)
			{
				_mechHud.MoveToFront();
				_mechHud.QueueApplyLayout();
			}
		}
		if (_aimCrosshair != null)
		{
			_aimCrosshair.Visible = visible;
			if (visible)
				_aimCrosshair.MoveToFront();
		}

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
		var pilot = session?.Profile.ResolveAccountHandle() ?? VoidCorpsIdentity.PlayerCorpCodename;
		string contract;
		if (session is { MatchFromCampaign: true, PendingMission: MissionType.BossEncounter })
		{
			var encounter = BossEncounterCatalog.Get(session.PendingBossEncounter);
			contract = $"{encounter.Corp.ShortName} Titan contest";
		}
		else if (session is { MatchFromCampaign: true, LastMissionManufacturerId.Length: > 0 })
		{
			contract = GameCatalog.GetManufacturer(session.LastMissionManufacturerId).DisplayName;
			if (!string.IsNullOrEmpty(session.PendingRivalPilotId))
			{
				var rival = RivalRosterCatalog.GetPilot(session.PendingRivalPilotId);
				var rivalCorp = RivalRosterCatalog.GetCorp(rival.CorpId);
				contract += $" vs {rival.Callsign}/{rivalCorp.ShortName}";
			}
		}
		else if (session?.Profile.AffiliatedManufacturerId is { Length: > 0 } id)
		{
			contract = GameCatalog.GetManufacturer(id).DisplayName;
		}
		else
		{
			contract = "open contract";
		}

		brief.Text = $"{pilot} // {contract}\n{_claim.Brief}";
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

		ApplyArenaShell(_layout);
		ApplyAtmosphere(_layout);
		_padBarriers = new PadBarrierRing(this);
		_padBarriers.Apply();
		RebuildCover(_layout);
		RebuildExteriorBackdrop(_layout);
		PlaceCrates(_layout);
	}

	private void RebuildExteriorBackdrop(ClaimArenaLayout layout)
	{
		var world = GetNodeOrNull<Node3D>("World");
		if (world == null)
			return;
		ClaimExteriorBackdrop.Rebuild(world, layout);
	}

	/// <summary>
	/// Perimeter bulkhead height. Sized for booster flight — stock walls were 4 m and trivially hoppable.
	/// </summary>
	private const float PerimeterWallHeight = 28f;

	/// <summary>Resize the shared floor/walls. Supports rectangular sabotage corridors.</summary>
	private void ApplyArenaShell(ClaimArenaLayout layout)
	{
		var halfX = layout.HalfExtentX;
		var halfZ = layout.HalfExtentZ;
		var extentX = halfX * 2f;
		var extentZ = halfZ * 2f;
		// Same pad mesh, oversized past the walls — continuous ground beyond the barrier (no second apron seam).
		var apron = ClaimExteriorBackdrop.FloorApron * 2f;
		var floorSize = new Vector3(extentX + apron, 1f, extentZ + apron);
		// West/East are pre-rotated 90° in arena.tscn, so their local X maps to world Z.
		var wallNS = new Vector3(extentX, PerimeterWallHeight, 1f);
		var wallEW = new Vector3(extentZ, PerimeterWallHeight, 1f);
		var wallY = PerimeterWallHeight * 0.5f;

		SetBoxMeshAndShape("World/Floor/Mesh", "World/Floor/Collision", floorSize);
		SetBoxMeshAndShape("World/WallNorth/Mesh", "World/WallNorth/Collision", wallNS);
		SetBoxMeshAndShape("World/WallSouth/Mesh", "World/WallSouth/Collision", wallNS);
		SetBoxMeshAndShape("World/WallWest/Mesh", "World/WallWest/Collision", wallEW);
		SetBoxMeshAndShape("World/WallEast/Mesh", "World/WallEast/Collision", wallEW);

		SetNodePosition("World/WallNorth", new Vector3(0f, wallY, -halfZ));
		SetNodePosition("World/WallSouth", new Vector3(0f, wallY, halfZ));
		SetNodePosition("World/WallWest", new Vector3(-halfX, wallY, 0f));
		SetNodePosition("World/WallEast", new Vector3(halfX, wallY, 0f));
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
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var period = session?.PendingArenaPeriod ?? ArenaPeriod.Night;
		var lighting = ClaimAtmosphere.Resolve(layout, period);

		var envNode = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (envNode?.Environment != null)
		{
			var env = envNode.Environment;
			ClaimExteriorBackdrop.ApplySky(env, layout, period);
			env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
			env.AmbientLightColor = lighting.AmbientColor;
			env.AmbientLightEnergy = lighting.AmbientEnergy;

			// Soft contact AO only. High intensity/power was salt-and-pepper on asphalt at night.
			env.SsaoEnabled = true;
			env.SsaoRadius = 1.8f;
			env.SsaoIntensity = period == ArenaPeriod.Day ? 0.12f : 0.18f;
			env.SsaoPower = 1.1f;
			env.SsaoHorizon = 0.04f;
			env.SsaoSharpness = 0.5f;

			env.GlowEnabled = true;
			env.GlowIntensity = period == ArenaPeriod.Day ? 0.12f : 0.22f;
			env.GlowStrength = 0.65f;
			env.GlowBloom = period == ArenaPeriod.Day ? 0.02f : 0.04f;
			env.TonemapMode = Godot.Environment.ToneMapper.Aces;
			env.TonemapExposure = lighting.Exposure;

			ClaimExteriorBackdrop.ApplyFog(env, layout, period);
		}

		var sun = GetNodeOrNull<DirectionalLight3D>("Sun");
		if (sun != null)
		{
			sun.LightColor = lighting.SunColor;
			sun.LightEnergy = lighting.SunEnergy;
			sun.RotationDegrees = lighting.SunRotationDegrees;
			sun.ShadowEnabled = true;
			sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
			sun.ShadowBias = 0.04f;
			sun.ShadowNormalBias = 1.0f;
			sun.DirectionalShadowMaxDistance = 180f;
			sun.LightAngularDistance = period == ArenaPeriod.Day ? 0.35f : 0.6f;
		}

		TintWorldMeshes(layout);
	}

	private void TintWorldMeshes(ClaimArenaLayout layout)
	{
		var floorMesh = GetNodeOrNull<MeshInstance3D>("World/Floor/Mesh");
		if (floorMesh != null)
			MeshMat.Bind(floorMesh, SurfaceLibrary.GetPadFloor(layout.ClaimCode, layout.FloorColor));
		// Perimeter visuals are the shield barrier ring (see PadBarrierRing) — not concrete bulkheads.
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
			var seed = i * 17
			           + (int)(piece.Position.X * 3f)
			           + (int)(piece.Position.Z * 7f)
			           + (int)piece.Kind * 31;
			var built = CoverVisualFactory.Build(piece.Kind, ambience, piece.Scale, seed);

			var body = new StaticBody3D
			{
				Name = $"Cover_{piece.Kind}_{i}",
				// Y is elevation for walkable decks / ramps; ground cover stays at 0.
				Position = piece.Position,
				RotationDegrees = new Vector3(0f, piece.YawDegrees, 0f)
			};
			body.AddChild(built.Visual);

			var collision = new CollisionShape3D
			{
				Name = "Collision",
				Shape = new BoxShape3D { Size = built.CollisionSize },
				Position = built.CollisionCenter,
				RotationDegrees = built.CollisionRotationDegrees,
				Disabled = false
			};
			body.AddChild(collision);

			for (var e = 0; e < built.ExtraCollisions.Length; e++)
			{
				var vol = built.ExtraCollisions[e];
				body.AddChild(new CollisionShape3D
				{
					Name = $"CollisionExtra_{e}",
					Shape = new BoxShape3D { Size = vol.Size },
					Position = vol.Center,
					RotationDegrees = vol.RotationDegrees,
					Disabled = false
				});
			}

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
						LootService.SpawnWorldDrops(
							parent,
							origin,
							LootService.ScrapForCover(),
							materials: LootService.RollMaterials(LootSource.Cover));
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
			_playerDropBeacon.GlobalPosition = DropBeacon.PadBesideSpawn(
				_playerSpawn,
				limit: _layout.PadLimitX,
				limitZ: _layout.PadLimitZ);
			_playerDropBeacon.SetState(DropBeaconState.Ready);
			return;
		}

		_playerDropBeacon = DropBeacon.Create(
			"PlayerDropBeacon",
			DropBeacon.PadBesideSpawn(_playerSpawn, limit: _layout.PadLimitX, limitZ: _layout.PadLimitZ),
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

		// Drop the failed UiHost experiment — Controls under CanvasLayer use viewport space.
		var staleHost = ui.GetNodeOrNull<Control>("UiHost");
		_mechHud = ui.GetNodeOrNull<MechHud>("MechHud")
			?? staleHost?.GetNodeOrNull<MechHud>("MechHud");
		if (_mechHud != null && GodotObject.IsInstanceValid(_mechHud))
		{
			if (_mechHud.GetParent() != ui)
				_mechHud.Reparent(ui);
			if (staleHost != null && GodotObject.IsInstanceValid(staleHost))
				staleHost.QueueFree();
			_mechHud.QueueApplyLayout();
			return;
		}

		if (staleHost != null && GodotObject.IsInstanceValid(staleHost))
			staleHost.QueueFree();

		_mechHud = new MechHud
		{
			Name = "MechHud",
			Visible = false
		};
		ui.AddChild(_mechHud);
		_mechHud.QueueApplyLayout();
	}

	private void EnsureAimCrosshair()
	{
		var ui = GetNodeOrNull("UI");
		if (ui == null)
			return;

		_aimCrosshair = ui.GetNodeOrNull<AimCrosshair>("AimCrosshair");
		if (_aimCrosshair != null && GodotObject.IsInstanceValid(_aimCrosshair))
		{
			if (_aimCrosshair.GetParent() != ui)
				_aimCrosshair.Reparent(ui);
			return;
		}

		_aimCrosshair = new AimCrosshair
		{
			Name = "AimCrosshair",
			Visible = false
		};
		ui.AddChild(_aimCrosshair);
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
		var extract = ActiveExtractBeacon();
		extract?.ArmExtract();
		SfxService.Confirm();
		SfxService.Play("alarm", 0.85f, -4f);
		GD.Print(extract != null && extract != _playerDropBeacon
			? "Objectives complete — hold Interact at the Exfil Uplink."
			: "Objectives complete — return to drop beacon and hold Interact to extract.");
	}

	MechController? IMissionHost.SpawnEnemyMech(string name, Vector3 position, int variant, bool viaDropBeacon) =>
		SpawnEnemyMech(name, position, variant, viaDropBeacon);

	void IMissionHost.DespawnEnemyMech(string name) => DespawnEnemyMech(name);

	SupportUnit IMissionHost.SpawnSupport(string name, string unitId, TeamId team, Vector3 position, bool viaTelegraph) =>
		SpawnSupport(name, unitId, team, position, viaTelegraph);

	void IMissionHost.DespawnSupport(string name) => DespawnSupport(name);

	bool IMissionHost.AllEnemyMechsDown() => AllEnemyMechsDown();

	void IMissionHost.FaceToward(Node3D body, Vector3 worldPoint) => FaceToward(body, worldPoint);

	private SupportUnit SpawnSupport(string name, string unitId, TeamId team, Vector3 position, bool viaTelegraph = false)
	{
		var existing = GetNodeOrNull<SupportUnit>(name);
		SupportUnit unit;
		if (existing != null)
		{
			existing.Configure(unitId, team);
			unit = existing;
		}
		else
		{
			unit = SupportUnit.Create(unitId, team, name);
			AddChild(unit);
			unit.Configure(unitId, team);
		}

		FaceToward(unit, Vector3.Zero);
		if (GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true })
			unit.AttachHostReplication();

		if (viaTelegraph && team == TeamId.Enemy && _phase == MatchPhase.Fighting)
		{
			var approach = position * 1.35f;
			approach.Y = 0f;
			if (approach.LengthSquared() < 4f)
				approach = position + new Vector3(0f, 0f, -16f);
			unit.GlobalPosition = approach;
			unit.Visible = false;
			unit.ProcessMode = ProcessModeEnum.Disabled;
			ScheduleSupportApproach(unit, position, approach, warningSeconds: 2f);
			return unit;
		}

		unit.GlobalPosition = position;
		unit.Visible = true;
		unit.ProcessMode = ProcessModeEnum.Inherit;
		return unit;
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
		RivalPilotDef? rivalPilot = null;
		var chassisClass = MechChassisClass.Standard;
		var shouldAssignRival = !_rivalAssigned && !string.IsNullOrEmpty(_activeRivalPilotId)
			&& (name == "Boss_Detachment" || name.EndsWith("_Detachment"));
		if (shouldAssignRival)
		{
			rivalPilot = RivalRosterCatalog.GetPilot(_activeRivalPilotId);
			variant = rivalPilot.LoadoutVariant;
			_rivalAssigned = true;
			_activeRivalEnemyName = name;
			if (name == "Boss_Detachment")
				chassisClass = _activeBossChassisClass;
			var corp = RivalRosterCatalog.GetCorp(rivalPilot.CorpId);
			var chassisLabel = MechChassisClassUtil.Label(chassisClass);
			GD.Print($"Rival contact: {rivalPilot.DisplayName}, {corp.DisplayName} — {chassisLabel}.");
		}

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
			enemy.ApplyChassisClass(chassisClass);
			enemy.RebuildFromLoadout(loadout);
		}
		else
		{
			enemy.ApplyChassisClass(chassisClass);
			enemy.RebuildFromLoadout(loadout);
		}

		enemy.Team = TeamId.Enemy;
		enemy.Visible = true;
		enemy.ProcessMode = ProcessModeEnum.Inherit;
		if (enemy.Health != null)
		{
			var hull = MechChassisClassUtil.HullMultiplier(chassisClass);
			if (rivalPilot != null && chassisClass != MechChassisClass.Titan)
				hull *= rivalPilot.HullMultiplier;
			else if (rivalPilot != null)
				hull *= Mathf.Lerp(1f, rivalPilot.HullMultiplier, 0.35f);
			enemy.Health.ResetHealth(enemy.Health.MaxHealth * hull);
		}
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
			if (rivalPilot != null)
			{
				pilot.PreferredRange = rivalPilot.PreferredRange
					+ MechChassisClassUtil.PreferredRangeBonus(chassisClass);
				pilot.Aggression = rivalPilot.Aggression;
			}
			if (_mech != null)
				pilot.SetTarget(PickAiTarget());
		}

		if (viaDropBeacon)
		{
			BeginDropIn(enemy, position, enableAiWhenDone: true, createBeacon: false, warningSeconds: 2f);
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

	private void BeginDropIn(
		MechController mech,
		Vector3 landing,
		bool enableAiWhenDone,
		bool createBeacon,
		float? durationOverride = null,
		float warningSeconds = 1.5f)
	{
		if (_deployment == null)
			return;

		var duration = durationOverride ?? (mech.ChassisClass == MechChassisClass.Titan ? 1.85f : 1.35f);
		var dropHeight = mech.ChassisClass == MechChassisClass.Titan
			? PlayerDropHeight * 1.75f
			: PlayerDropHeight;

		_deployment.SetPlayerDropBeacon(_playerDropBeacon);
		_deployment.Schedule(new DeploymentRequest
		{
			JobId = "",
			Kind = DeploymentKind.VesselDrop,
			Target = landing,
			Team = mech.Team,
			WarningSeconds = warningSeconds,
			FallSeconds = duration,
			OpenSeconds = VesselOpenDuration,
			DropHeight = dropHeight,
			CreateBeacon = createBeacon,
			ExistingBeacon = createBeacon ? null : _playerDropBeacon,
			Mech = mech,
			EnableAiWhenDone = enableAiWhenDone
		});
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
		_deployment?.SetPlayerDropBeacon(_playerDropBeacon);
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

	private void OnDeploymentPhaseChanged(string jobId, DeploymentPhase phase, Vector3 target, int team)
	{
		if (Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
			_netCombat?.HostDeploymentPhase(jobId, (int)phase, target, team);
	}

	public bool TryRequestFieldPart(PartSlot slot, string instanceId)
	{
		if (_matchResolved || _phase != MatchPhase.Fighting)
			return false;

		// Each peer owns their inventory — requests are local, never host-gated.
		return LocalRequestFieldPart(slot, instanceId);
	}

	public bool LocalRequestFieldPart(PartSlot slot, string instanceId)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null || _mech == null || _deployment == null)
			return false;

		var instance = session.Profile.GetInstance(instanceId);
		if (instance == null || instance.Reserved)
			return false;
		var part = GameCatalog.GetPart(instance.PartId);
		if (part == null || part.Slot != slot)
			return false;

		instance.Reserved = true;
		session.Match.TrackRecovery(instance.InstanceId);

		var landing = PickFieldLandingNear(_mech.GlobalPosition);
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var ownerPeer = net?.LocalPeerId ?? 0;
		var crate = FieldPartCrate.Create(
			instance.InstanceId,
			instance.PartId,
			slot,
			landing + new Vector3(0f, 18f, 0f),
			ownerPeerId: ownerPeer);
		crate.Visible = false;
		AddChild(crate);
		_fieldCrates.Add(crate);

		_deployment.Schedule(new DeploymentRequest
		{
			JobId = "",
			Kind = DeploymentKind.CargoPod,
			Target = landing,
			Team = TeamId.Player,
			WarningSeconds = 1.5f,
			FallSeconds = 1.15f,
			OpenSeconds = 0.45f,
			DropHeight = 18f,
			CreateBeacon = false,
			Cargo = crate
		});

		_netCombat?.BroadcastFieldCargoPod(
			ownerPeer,
			(int)slot,
			instance.InstanceId,
			instance.PartId,
			landing);
		return true;
	}

	/// <summary>Cosmetic cargo pod for another peer's resupply — no local inventory mutation.</summary>
	public void ObserveRemoteFieldCargoPod(int ownerPeer, PartSlot slot, string instanceId, string partId, Vector3 landing)
	{
		if (_deployment == null)
			return;

		var crate = FieldPartCrate.Create(
			instanceId,
			partId,
			slot,
			landing + new Vector3(0f, 18f, 0f),
			ownerPeerId: ownerPeer,
			visualOnly: true);
		crate.Visible = false;
		AddChild(crate);
		_fieldCrates.Add(crate);

		_deployment.Schedule(new DeploymentRequest
		{
			JobId = "",
			Kind = DeploymentKind.CargoPod,
			Target = landing,
			Team = TeamId.Player,
			WarningSeconds = 1.5f,
			FallSeconds = 1.15f,
			OpenSeconds = 0.45f,
			DropHeight = 18f,
			CreateBeacon = false,
			Cargo = crate
		});
	}

	public void ObserveRemoteFieldCrateLanded(int ownerPeer, PartSlot slot, string instanceId, string partId, Vector3 position)
	{
		// Replace any in-flight visual crate with a grounded one.
		foreach (var existing in _fieldCrates.ToArray())
		{
			if (!IsInstanceValid(existing))
				continue;
			if (existing.InstanceId == instanceId && existing.VisualOnly)
			{
				MeshMat.QueueFreeSafe(existing);
				_fieldCrates.Remove(existing);
			}
		}

		var crate = FieldPartCrate.Create(instanceId, partId, slot, position, ownerPeerId: ownerPeer, visualOnly: true);
		crate.MarkLanded();
		AddChild(crate);
		_fieldCrates.Add(crate);
	}

	public void ObserveRemoteFieldCrateConsumed(string instanceId)
	{
		foreach (var existing in _fieldCrates.ToArray())
		{
			if (!IsInstanceValid(existing))
				continue;
			if (existing.InstanceId != instanceId)
				continue;
			MeshMat.QueueFreeSafe(existing);
			_fieldCrates.Remove(existing);
		}
	}

	public bool TryInstallFieldCrate(FieldPartCrate crate, MechController mech)
	{
		if (!crate.Landed || _matchResolved)
			return false;
		if (!mech.IsPlayerControlled)
			return false;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return false;

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var localPeer = net?.LocalPeerId ?? 0;
		if (crate.OwnerPeerId != 0 && localPeer != 0 && crate.OwnerPeerId != localPeer)
		{
			if (_pendingTradeClaims.ContainsKey(crate.InstanceId))
				return false;

			_pendingTradeClaims[crate.InstanceId] = new PendingTradeClaim
			{
				OwnerPeerId = crate.OwnerPeerId,
				Slot = crate.Slot,
				InstanceId = crate.InstanceId,
				PartId = crate.PartId,
				Position = crate.GlobalPosition,
				Mech = mech
			};
			_netCombat?.RequestFieldTradeClaim(
				crate.OwnerPeerId,
				localPeer,
				(int)crate.Slot,
				crate.InstanceId,
				crate.PartId);
			return true;
		}

		var incoming = session.Profile.GetInstance(crate.InstanceId);
		var part = GameCatalog.GetPart(crate.PartId);
		if (incoming == null || part == null)
			return false;

		return InstallOwnedFieldInstance(crate, mech, incoming, part, localPeer);
	}

	private bool InstallOwnedFieldInstance(
		FieldPartCrate crate,
		MechController mech,
		OwnedPartInstance incoming,
		PartData part,
		int localPeer)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return false;

		var outgoing = session.Profile.GetEquippedInstance(crate.Slot);
		PartCondition? outgoingCondition = null;
		string? outgoingInstanceId = null;
		string? outgoingPartId = null;
		if (outgoing != null && mech.Assembler?.Hardpoints.TryGetValue(crate.Slot, out var hp) == true)
		{
			outgoingCondition = hp.CaptureCondition();
			outgoing.Condition = outgoingCondition.Clone();
			outgoingInstanceId = outgoing.InstanceId;
			outgoingPartId = outgoing.PartId;
		}

		if (!mech.InstallFieldPart(crate.Slot, part, incoming.Condition.Clone()))
			return false;

		incoming.Reserved = false;
		session.Profile.SetEquippedInstance(crate.Slot, incoming);
		session.SetLoadout(session.Profile.Loadout);
		_fieldCrates.Remove(crate);
		_netCombat?.BroadcastFieldCrateConsumed(crate.InstanceId);

		if (!string.IsNullOrEmpty(outgoingInstanceId) && !string.IsNullOrEmpty(outgoingPartId))
		{
			var dropPos = mech.GlobalPosition + mech.Transform.Basis.Z * -2.2f;
			dropPos.Y = 0.35f;
			if (outgoing != null)
				outgoing.Reserved = true;
			var dropped = FieldPartCrate.Create(
				outgoingInstanceId,
				outgoingPartId,
				crate.Slot,
				dropPos,
				ownerPeerId: localPeer);
			dropped.MarkLanded();
			AddChild(dropped);
			_fieldCrates.Add(dropped);
			session.Match.TrackRecovery(outgoingInstanceId);
			if (outgoing != null && outgoingCondition != null)
				outgoing.Condition = outgoingCondition;
			_netCombat?.BroadcastFieldCrateLanded(
				localPeer,
				(int)crate.Slot,
				outgoingInstanceId,
				outgoingPartId,
				dropPos);
		}

		return true;
	}

	/// <summary>
	/// Runs only on the crate owner's peer. The owner remains authoritative for
	/// relinquishing their inventory copy, but no host approval is involved.
	/// </summary>
	public bool AuthorizeFieldTradeClaim(
		int claimantPeer,
		PartSlot slot,
		string instanceId,
		string partId,
		out Godot.Collections.Dictionary condition)
	{
		condition = new Godot.Collections.Dictionary();
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null || claimantPeer <= 0)
			return false;

		var instance = session.Profile.GetInstance(instanceId);
		if (instance == null || !instance.Reserved || instance.PartId != partId)
			return false;
		var part = GameCatalog.GetPart(partId);
		if (part == null || part.Slot != slot)
			return false;

		var crate = _fieldCrates.FirstOrDefault(c =>
			IsInstanceValid(c)
			&& !c.VisualOnly
			&& c.InstanceId == instanceId
			&& c.Slot == slot);
		if (crate == null)
			return false;
		if (GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true })
		{
			if (!_wingByPeer.TryGetValue(claimantPeer, out var claimant)
			    || claimant.GlobalPosition.DistanceTo(crate.GlobalPosition) > 6f)
				return false;
		}

		condition = instance.Condition.ToDict();
		session.Match.UntrackRecovery(instanceId);
		if (!session.Profile.TryRemoveInstance(instanceId))
			return false;

		_fieldCrates.Remove(crate);
		MeshMat.QueueFreeSafe(crate);
		_netCombat?.BroadcastFieldCrateConsumed(instanceId);
		session.SaveProfile();
		return true;
	}

	public void CompleteFieldTradeClaim(
		int ownerPeer,
		PartSlot slot,
		string instanceId,
		string partId,
		Godot.Collections.Dictionary conditionDict)
	{
		if (!_pendingTradeClaims.Remove(instanceId, out var pending))
			return;
		if (pending.OwnerPeerId != ownerPeer
		    || pending.Slot != slot
		    || pending.PartId != partId
		    || !IsInstanceValid(pending.Mech))
			return;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var part = GameCatalog.GetPart(partId);
		if (session == null || part == null || part.Slot != slot)
			return;

		var condition = PartCondition.FromDict(conditionDict);
		var adopted = session.Profile.AdoptTransferredInstance(instanceId, partId, condition);
		session.Match.TrackRecovery(instanceId);

		var claimCrate = FieldPartCrate.Create(
			instanceId,
			partId,
			slot,
			pending.Position,
			ownerPeerId: GetNodeOrNull<NetSession>("/root/NetSession")?.LocalPeerId ?? 0);
		claimCrate.MarkLanded();
		AddChild(claimCrate);
		_fieldCrates.Add(claimCrate);

		var localPeer = GetNodeOrNull<NetSession>("/root/NetSession")?.LocalPeerId ?? 0;
		if (InstallOwnedFieldInstance(claimCrate, pending.Mech, adopted, part, localPeer))
		{
			MeshMat.QueueFreeSafe(claimCrate);
			session.SaveProfile();
			SfxService.Confirm();
		}
	}

	public void RejectFieldTradeClaim(string instanceId)
	{
		if (!_pendingTradeClaims.Remove(instanceId, out var pending))
			return;

		var existing = _fieldCrates.FirstOrDefault(c =>
			IsInstanceValid(c)
			&& c.VisualOnly
			&& c.InstanceId == instanceId);
		if (existing != null)
		{
			existing.ResetTransferClaim();
			SfxService.PlayUiError(UiErrorTone.DeeDoo);
			return;
		}

		var restored = FieldPartCrate.Create(
			pending.InstanceId,
			pending.PartId,
			pending.Slot,
			pending.Position,
			ownerPeerId: pending.OwnerPeerId,
			visualOnly: true);
		restored.MarkLanded();
		AddChild(restored);
		_fieldCrates.Add(restored);
		SfxService.PlayUiError(UiErrorTone.DeeDoo);
	}

	private Vector3 PickFieldLandingNear(Vector3 origin)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (var i = 0; i < 12; i++)
		{
			var angle = rng.Randf() * Mathf.Tau;
			var dist = rng.RandfRange(6f, 14f);
			var candidate = origin + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
			candidate.X = Mathf.Clamp(candidate.X, -_layout.PadLimitX, _layout.PadLimitX);
			candidate.Z = Mathf.Clamp(candidate.Z, -_layout.PadLimitZ, _layout.PadLimitZ);
			candidate.Y = 0f;
			if ((candidate - origin).Length() >= 5f)
				return candidate;
		}

		return new Vector3(
			Mathf.Clamp(origin.X + 8f, -_layout.PadLimitX, _layout.PadLimitX),
			0f,
			Mathf.Clamp(origin.Z, -_layout.PadLimitZ, _layout.PadLimitZ));
	}

	private void RecoverFieldCrates(GameSession session)
	{
		foreach (var crate in _fieldCrates.ToArray())
		{
			if (!IsInstanceValid(crate))
				continue;
			if (crate.Recoverable && !string.IsNullOrEmpty(crate.InstanceId))
			{
				session.Match.TrackRecovery(crate.InstanceId);
				var instance = session.Profile.GetInstance(crate.InstanceId);
				if (instance != null)
					instance.Reserved = false;
			}

			MeshMat.QueueFreeSafe(crate);
		}

		_fieldCrates.Clear();
	}

	public void ScheduleSupportApproach(SupportUnit unit, Vector3 target, Vector3 approachFrom, float warningSeconds = 2f)
	{
		_deployment?.Schedule(new DeploymentRequest
		{
			JobId = "",
			Kind = DeploymentKind.GroundApproach,
			Target = target,
			ApproachFrom = approachFrom,
			Team = unit.Team,
			WarningSeconds = warningSeconds,
			FallSeconds = 1.6f,
			Support = unit
		});
	}

	private void RemountWithTelegraph(MechController mech, Vector3 pad, LoadoutData loadout, bool fullRepair)
	{
		mech.RebuildFromLoadout(loadout, forceFullRepair: fullRepair);
		mech.GlobalPosition = pad;
		mech.Velocity = Vector3.Zero;
		FaceToward(mech, Vector3.Zero);
		_deployment?.Schedule(new DeploymentRequest
		{
			JobId = "",
			Kind = DeploymentKind.Remount,
			Target = pad,
			Team = TeamId.Player,
			WarningSeconds = 1.5f,
			FallSeconds = mech.ChassisClass == MechChassisClass.Titan ? 1.85f : 1.35f,
			OpenSeconds = VesselOpenDuration,
			DropHeight = PlayerDropHeight,
			CreateBeacon = false,
			Mech = mech
		});
		SetCombatActive(true);
	}

	private void TickExtraction(float dt)
	{
		if (!_objectivesComplete || _matchResolved || _mech == null)
			return;
		if (_phase != MatchPhase.Fighting)
			return;

		var beacon = ActiveExtractBeacon();
		if (beacon == null)
			return;

		var holding = Input.IsActionPressed("interact") && _mech is not { BlocksInteractForSeat: true };
		if (beacon.TickExtract(dt, holding, _mech.GlobalPosition))
		{
			SfxService.Confirm();
			ResolveMatch(MatchOutcome.Victory);
		}
	}

	private DropBeacon? ActiveExtractBeacon() =>
		_mission?.ExtractBeaconOverride ?? _playerDropBeacon;

	private void HookPlayerDeath()
	{
		if (_mech == null || _playerDeathHooked)
			return;

		_playerDeathHooked = true;
		if (_mech.Health != null)
			_mech.Health.Died += OnPlayerDown;
		if (_mech.Integrity != null)
		{
			_mech.Integrity.MechCollapsed += OnPlayerDown;
			HookIntegrityTelemetry(_mech.Integrity, playerOwned: true);
		}
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
		};
	}

	private void HookIntegrityTelemetry(MechIntegrity integrity, bool playerOwned)
	{
		var id = integrity.GetInstanceId();
		if (!_telemetryDamageHooked.Add(id))
			return;

		integrity.ComponentDamaged += (_, amount, _, _) =>
		{
			if (amount <= 0.01f || !playerOwned)
				return;
			GetNodeOrNull<GameSession>("/root/GameSession")
				?.Match.Telemetry.RecordDamageTaken(amount, TelemetryTargetKind.Map);
		};

		integrity.ComponentDestroyed += _ =>
		{
			if (!playerOwned)
				return;
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
			// Life insurance: remount with every equipped instance restored to new.
			if (downed.IsPlayerControlled || downed.OwningPeerId == (net?.LocalPeerId ?? 0))
				session.Profile.RepairAllEquippedFully();
			var pad = downed.GlobalPosition with { Y = 0f };
			if (pad.LengthSquared() < 0.01f)
				pad = _playerSpawn;
			RemountWithTelegraph(downed, pad, loadout, fullRepair: true);
			GD.Print($"Pilot remount (peer {downed.OwningPeerId}). Lives remaining: {session.Match.LivesRemaining}");
			return;
		}

		// Cadet range cannot fail — free remount forever.
		if (session is { MatchFromAcademy: true, PendingMission: MissionType.CadetRange })
		{
			RemountWithTelegraph(downed, _playerSpawn, session.CurrentLoadout, fullRepair: true);
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
				LootService.RollEnemyMechPartDrop(loadout, maxTier),
				LootService.RollMaterials(LootSource.EnemyMech, maxTier));
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
		EscortMission.ClearFieldInteractReservation();
		SetCombatActive(false);
		if (_garage != null)
			_garage.Visible = false;
		SetCombatHudVisible(false);
		RefreshCombatMousePolicy();

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session != null)
		{
			RecoverFieldCrates(session);
			if (_mech != null)
				session.Match.CaptureFinalCondition(_mech.CapturePartConditions());
		}

		session?.Match.End(outcome);
		if (session is { MatchFromAcademy: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnAcademyMissionResolved(outcome);
		else if (session is { MatchFromConvention: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnConventionTrialResolved(outcome);
		else if (session is { MatchFromSolarCampaign: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnSolarMissionResolved(outcome);
		else if (session is { MatchFromCampaign: true } && (Multiplayer.MultiplayerPeer == null || Multiplayer.IsServer()))
			session.OnCampaignNodeResolved(outcome);
		SfxService.Play(outcome == MatchOutcome.Victory ? "victory" : "defeat", 1f, -2f);

		if (Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
			_netCombat?.HostShowResults((int)outcome);
		else if (session != null)
			OpenPostMissionFlow(session);
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
			SfxService.PlayUiError(UiErrorTone.Incorrect);
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
		_padBarriers?.Tick(dt, _mech);
		_deployment?.Tick(dt);

		if (_phase == MatchPhase.Countdown)
			TickCountdown(dt);

		if (!_matchResolved
			&& _phase == MatchPhase.Fighting
			&& Input.IsActionJustPressed("toggle_garage")
			&& _garage != null
			&& !(_deployment?.HasActiveJobs ?? false))
		{
			_garage.Visible = !_garage.Visible;
			_garage.ConfigurePrepMode(false);
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
			return;
		BeginCountdown(loadout);
	}

	private void OnFieldDeliveryRequested(int slot, string instanceId)
	{
		if (_phase != MatchPhase.Fighting || _matchResolved)
			return;
		TryRequestFieldPart((PartSlot)slot, instanceId);
		if (_garage != null)
			_garage.Visible = false;
		SetLocalWingControls(true);
		SetCombatHudVisible(true);
	}

	private void BeginCountdown(LoadoutData loadout)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.SetLoadout(loadout);
		// Loaner kits (skirmish / academy / convention) spawn pristine — ignore garage wear.
		if (session is { UsingTemporaryLoaner: true })
			_mech?.RebuildFromLoadout(loadout, forceFullRepair: true);
		else
			_mech?.RebuildFromLoadout(loadout, BuildProfileConditions(session?.Profile));

		if (_garage != null)
		{
			_garage.Visible = false;
			_garage.ConfigurePrepMode(false);
		}

		SetCombatHudVisible(false);
		_enemyResolved.Clear();
		_objectivesComplete = false;
		PlaceCombatants();
		StagePlayerDropHold();
		SetCombatActive(false);

		_phase = MatchPhase.Countdown;
		_countdownRemaining = 5.25f;
		_lastCountdownSecond = -1;
		_playerDropStarted = false;
		RefreshCombatMousePolicy();
		// Sabotage starts its track on fight begin so the beat clock is at 0:00.
		if (string.IsNullOrEmpty(_mission?.PreferredCombatTrack))
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
				BeginDropIn(_mech, _playerSpawn, enableAiWhenDone: false, createBeacon: false, fallTime, warningSeconds: 0.05f);
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
			RefreshCombatMousePolicy();
			_mission?.OnFightStarted();
			SfxService.Play("fight", 1f, -1f);

			// Safety: if the timed drop never started, drop now (still rare).
			if (!_playerDropStarted && _mech != null)
			{
				_playerDropStarted = true;
				BeginDropIn(_mech, _playerSpawn, enableAiWhenDone: false, createBeacon: false, warningSeconds: 0.05f);
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
				var dropping = _deployment?.IsMechDeploying(mech) ?? false;
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
		var dropping = _deployment?.IsMechDeploying(_mech) ?? false;
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

		OpenPostMissionFlow(session);
	}

	private void OpenPostMissionFlow(GameSession session)
	{
		if (session.Match.FinalConditionBySlot.Count == 0 && _mech != null)
			session.Match.CaptureFinalCondition(_mech.CapturePartConditions());

		// Academy / convention / skirmish loaners skip damage assessment — nothing persistent to repair.
		var skipDamage = session.MatchFromAcademy
			|| session.MatchFromConvention
			|| session.SkirmishLoaner != null;
		if (!skipDamage)
		{
			EnsureDamageAssessmentUi();
			if (_damageAssessment != null)
			{
				_damageAssessment.MouseFilter = Control.MouseFilterEnum.Stop;
				_damageAssessment.Open(session, () => OpenResultsShop(session));
				_damageAssessment.MoveToFront();
				return;
			}
		}

		OpenResultsShop(session);
	}

	private void OpenResultsShop(GameSession session)
	{
		if (_results == null || _results.Visible)
			return;
		_results.MouseFilter = Control.MouseFilterEnum.Stop;
		_results.Open(session);
		_results.MoveToFront();
	}

	private DamageAssessmentUi? _damageAssessment;
	private readonly Dictionary<string, DeploymentTelegraph> _clientTelegraphs = new();

	public void ClientObserveDeployment(string jobId, DeploymentPhase phase, Vector3 target, TeamId team)
	{
		if (Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
			return;

		if (phase == DeploymentPhase.Warning)
		{
			if (_clientTelegraphs.TryGetValue(jobId, out var existing) && IsInstanceValid(existing))
				existing.QueueFree();
			var telegraph = DeploymentTelegraph.Create(
				$"ClientTelegraph_{jobId}",
				target,
				team,
				warningDuration: 2f);
			AddChild(telegraph);
			_clientTelegraphs[jobId] = telegraph;
			return;
		}

		if (!_clientTelegraphs.TryGetValue(jobId, out var marker) || !IsInstanceValid(marker))
			return;

		switch (phase)
		{
			case DeploymentPhase.Inbound:
				marker.MarkInbound();
				break;
			case DeploymentPhase.Impact:
				marker.MarkImpact();
				break;
			case DeploymentPhase.Activated:
				marker.QueueFree();
				_clientTelegraphs.Remove(jobId);
				break;
		}
	}

	private void EnsureDamageAssessmentUi()
	{
		if (_damageAssessment != null)
			return;
		var ui = GetNodeOrNull<CanvasLayer>("UI");
		if (ui == null)
			return;
		_damageAssessment = ui.GetNodeOrNull<DamageAssessmentUi>("DamageAssessmentUi");
		if (_damageAssessment != null)
			return;
		_damageAssessment = new DamageAssessmentUi
		{
			Name = "DamageAssessmentUi",
			Visible = false
		};
		ui.AddChild(_damageAssessment);
	}

	private static Dictionary<PartSlot, PartCondition>? BuildProfileConditions(PlayerProfile? profile)
	{
		if (profile == null)
			return null;
		var map = new Dictionary<PartSlot, PartCondition>();
		foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
		{
			var instance = profile.GetEquippedInstance(slot);
			if (instance == null)
				continue;
			map[slot] = instance.Condition.Clone();
		}

		return map.Count > 0 ? map : null;
	}

	private void UpdateHud()
	{
		if (_mech?.Health == null)
			return;

		_mechHud?.Refresh(_mech);
		_aimCrosshair?.Refresh(_mech);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var match = session?.Match;
		var corp = session?.Profile.ResolveAccountHandle() ?? VoidCorpsIdentity.PlayerCorpCodename;
		var contract = ResolveContractLine(session);
		var claimLine = $"[{ArenaSizeUtil.Label(_layout.Size)}] {_claim.Code} — {_claim.DisplayName}";
		var contractLine = $"{corp} // {contract}";
		var flavor = _claim.Brief ?? "";

		var objective = _mission?.GetHudLine() ?? "";
		// Keep FP glass readable — rival / floor-model notes stay on floating TP chrome only.
		var status = "";
		if (_objectivesComplete)
		{
			status = _mission?.ExtractBeaconOverride != null
				? "OBJECTIVES COMPLETE — hold F at Exfil Uplink"
				: "OBJECTIVES COMPLETE — hold F at drop beacon";
			if (ActiveExtractBeacon() is { } pad && pad.Contains(_mech.GlobalPosition))
			{
				status = pad == _playerDropBeacon
					? "Hold F — signal retrieval at drop beacon"
					: "Hold F — signal retrieval at Exfil Uplink";
			}
		}

		var runStrip = match == null
			? ""
			: $"LIVES {match.LivesRemaining}  ·  SCRAP {match.RunScrap}  ·  B buy ({match.NextLifeCost})";

		_mechHud?.SetMissionChrome(claimLine, contractLine, objective, flavor, status, runStrip);

		var diegetic = _mechHud?.IsUsingDiegeticCockpit == true;
		SyncFloatingMissionChrome(!diegetic);

		if (_hud == null)
			return;

		var sponsorLine = session?.InCampaign == true && !string.IsNullOrEmpty(session.Profile.AffiliatedManufacturerId)
			? $"  |  {GameCatalog.GetManufacturer(session.Profile.AffiliatedManufacturerId).DisplayName} LICENSE"
			: "";
		var runLine = match == null
			? ""
			: $"LIVES {match.LivesRemaining}  |  SCRAP {match.RunScrap}  |  B buy life ({match.NextLifeCost}){sponsorLine}";

		var cloaked = _mech.Abilities?.IsCloaked == true ? "  |  SHROUD" : "";
		var sd = _mech.Integrity?.IsSelfDestructArmed == true
			? $"  |  DENY-ASSET {Mathf.CeilToInt((_mech.Integrity.SelfDestructProgress) * 100f)}%"
			: "";
		var sprint = _mech.IsSprinting ? "  |  SPRINT" : "";
		// OVERHEAT relocates to L/R glass heat bars or the center chassis heat bar.
		_hud.Text = diegetic
			? $"{sprint}{cloaked}{sd}".Trim().TrimStart('|', ' ')
			: $"{runLine}{sprint}{cloaked}{sd}";

		if (_targetHud != null)
		{
			// FP cockpit: sensor binds live on Screen_Threat. Only show sharpshooter lock in overlay HUD.
			var aim = _mech.AimedComponentLabel;
			var showAim = !diegetic
				&& !string.IsNullOrEmpty(aim)
				&& !string.Equals(aim, "no system lock", System.StringComparison.OrdinalIgnoreCase);
			_targetHud.Visible = showAim;
			_targetHud.Text = showAim ? $"SYSTEM LOCK: {aim}" : "";
		}

		if (_missionHud != null)
		{
			var line = objective;
			var sessionHud = session;
			if (sessionHud?.PendingMission != MissionType.BossEncounter
			    && !string.IsNullOrEmpty(_activeRivalPilotId)
			    && !string.IsNullOrEmpty(_activeRivalEnemyName)
			    && GetNodeOrNull<MechController>(_activeRivalEnemyName) is { Health.IsDead: false })
			{
				var rivalPilot = RivalRosterCatalog.GetPilot(_activeRivalPilotId);
				var rivalCorp = RivalRosterCatalog.GetCorp(rivalPilot.CorpId);
				line += $"  |  RIVAL CONTACT: {rivalPilot.Callsign.ToUpperInvariant()} · {rivalCorp.ShortName}";
			}
			if (sessionHud is { MatchFromConvention: true }
			    && sessionHud.Campaign?.Convention.ActiveTrialSabotaged != true)
			{
				line += "  |  Floor model nearby — expensive if it shatters";
			}

			_missionHud.Text = line;
		}

		if (_hint != null && _garage != null)
		{
			if (diegetic && _phase == MatchPhase.Fighting && !_garage.Visible && !_matchResolved)
			{
				// Control tips live on cockpit panels / stick in FP.
				_hint.Visible = false;
			}
			else
			{
				_hint.Visible = true;
				_hint.Text = _matchResolved
					? "Skirmish complete — field exchange open"
					: _phase switch
					{
						MatchPhase.Prep => "HANGAR  |  pick a category, preview parts, EQUIP, then READY",
						MatchPhase.Countdown => "Claim dispute commencing...",
						_ when _garage.Visible => "T / EXIT close hangar  |  Deploy updates loadout",
						_ when _objectivesComplete && ActiveExtractBeacon() is { } extractPad
							&& extractPad.Contains(_mech.GlobalPosition)
							=> extractPad == _playerDropBeacon
								? "Hold F — signal retrieval at drop beacon"
								: "Hold F — signal retrieval at Exfil Uplink",
						_ when _objectivesComplete
							=> _mission?.ExtractBeaconOverride != null
								? "OBJECTIVES COMPLETE — hold F at the Exfil Uplink"
								: "OBJECTIVES COMPLETE — return to your drop beacon and hold F to extract",
						_ => "WASD move  |  mouse look (FP)  |  tap Shift dash / hold sprint  |  hold Space boost  |  LMB/RMB weapons  |  TAB lock  |  1-6 modules  |  P camera  |  Esc pause"
					};
			}
		}
	}

	private void SyncFloatingMissionChrome(bool show)
	{
		if (_claimHud != null)
			_claimHud.Visible = show;
		var brief = GetNodeOrNull<Label>("UI/ClaimBrief");
		if (brief != null)
			brief.Visible = show;
		if (_missionHud != null)
			_missionHud.Visible = show;
		// _hud stays visible for sprint / shroud / deny-asset tags when diegetic.
	}

	private string ResolveContractLine(GameSession? session)
	{
		if (session is { MatchFromCampaign: true, PendingMission: MissionType.BossEncounter })
		{
			var encounter = BossEncounterCatalog.Get(session.PendingBossEncounter);
			return $"{encounter.Corp.ShortName} Titan contest";
		}

		if (session is { MatchFromCampaign: true, LastMissionManufacturerId.Length: > 0 })
		{
			var contract = GameCatalog.GetManufacturer(session.LastMissionManufacturerId).DisplayName;
			if (!string.IsNullOrEmpty(session.PendingRivalPilotId))
			{
				var rival = RivalRosterCatalog.GetPilot(session.PendingRivalPilotId);
				var rivalCorp = RivalRosterCatalog.GetCorp(rival.CorpId);
				contract += $" vs {rival.Callsign}/{rivalCorp.ShortName}";
			}

			return contract;
		}

		if (session?.Profile.AffiliatedManufacturerId is { Length: > 0 } id)
			return GameCatalog.GetManufacturer(id).DisplayName;

		return "open contract";
	}
}
