using System.Collections.Generic;
using Godot;

namespace Mechanize;

public partial class MechController : CharacterBody3D
{
	[Export] public bool IsPlayerControlled { get; set; } = true;
	[Export] public TeamId Team { get; set; } = TeamId.Player;
	/// <summary>Main-menu / cinematic props — no combat systems or X-ray silhouette.</summary>
	public bool HangarDisplayOnly { get; set; }
	[Export] public NodePath AssemblerPath { get; set; } = "MechAssembler";
	[Export] public NodePath HealthPath { get; set; } = "Damageable";
	[Export] public NodePath UpperBodyPath { get; set; } = "Sockets/UpperBody";

	/// <summary>Multiplayer peer that pilots this MAP. 0 = AI / offline single-player.</summary>
	public int OwningPeerId { get; set; }

	private MechAssembler? _assembler;
	private Damageable? _health;
	private AbilityController? _abilities;
	private MechIntegrity? _integrity;
	private MechPowerHeat? _powerHeat;
	private MechPilotAI? _pilot;
	private Node3D? _upperBody;
	private Vector3 _aimPoint;
	private PartSlot? _aimedComponentSlot;
	private string _aimedComponentLabel = "";
	private bool _controlsEnabled = true;
	private bool _sprinting;
	private bool _wasMoving;
	private float _respawnInvuln;
	private int _missileLockSlot = -1;
	private Vector3 _missilePaintPoint;
	private bool _missilePaintValid;
	private MissileLockDecal? _missileDecal;
	private PartSlot? _ghostLastSlot;
	private WeaponFamily _ghostLastFamily = WeaponFamily.None;
	private float _ghostLastTime;
	private float _dryFireReadyL;
	private float _dryFireReadyR;
	private bool _overheatFxActive;
	private bool _shieldRaisedL;
	private bool _shieldRaisedR;
	private bool _shieldBrokenL;
	private bool _shieldBrokenR;
	private float _shieldPoseBlendL;
	private float _shieldPoseBlendR;
	private Vector3 _shieldRestPosL;
	private Vector3 _shieldRestPosR;
	private Vector3 _shieldRestRotL;
	private Vector3 _shieldRestRotR;
	private bool _shieldRestCachedL;
	private bool _shieldRestCachedR;

	private const float ShieldRaiseSeconds = 0.2f;
	// Cover pose: modest forward (~¼ of the first draft), ~80% of the prior center pull.
	private static readonly Vector3 ShieldCoverOffsetL = new(0.6f, 0.05f, -0.14f);
	private static readonly Vector3 ShieldCoverOffsetR = new(-0.6f, 0.05f, -0.14f);

	// FP / mobility
	private Vector3 _moveVelocity;
	private float _sprintPressTime = -1f;
	private bool _sprintHoldCommitted;
	private float _dashRemaining;
	private float _dashCooldown;
	private Vector3 _dashDirection = Vector3.Forward;
	private const float SprintTapThreshold = 0.18f;
	private Node3D? _carrierMount;
	private EscortAsset? _carrierAsset;
	private bool _carrierOverdrive;
	/// <summary>-1..1 scroll bias while a fire bind is held. 0 = default shot height.</summary>
	private float _fireElevation;
	private float _netFireElevation;
	private float _fpHeadLookYaw;
	private float _fpHeadLookPitch;
	/// <summary>1 = full walk/sprint speed. Ctrl+scroll governor; Ctrl+middle-click resets.</summary>
	private float _speedGovernor = 1f;
	private MechController? _sensorLock;
	private PartSlot? _sensorFocus;
	private bool _sensorLockInVision;

	/// <summary>Max barrel pitch from scroll + sensor assist (~30°).</summary>
	public const float FireElevationMaxPitch = 0.52f;
	/// <summary>Scroll-only pitch cap — subtle manual trim while firing (~12°).</summary>
	public const float FireElevationScrollMaxPitch = 0.21f;
	private const float FireElevationStep = 0.11f;
	private const float FpHeadLookMaxYaw = 0.61f; // ~35°
	private const float FpHeadLookMaxPitch = 0.38f; // ~22°
	private const float FpAltLookSensitivity = 0.0035f;
	private const float SpeedGovernorMin = 0.15f;
	private const float SpeedGovernorStep = 0.07f;
	private static readonly PartSlot[] SensorFocusOrder =
	[
		PartSlot.Torso,
		PartSlot.Head,
		PartSlot.Legs,
		PartSlot.WeaponL,
		PartSlot.WeaponR
	];

	private float _netTurn;
	private float _netThrottle;
	private Vector2 _netMove;
	private Vector3 _netAim;
	private bool _netFirePrimary;
	private bool _netFireSecondary;
	private bool _netSprint;
	private int _netAbilityIndex = -1;
	private bool _hasNetInput;
	private float _netInputAge;

	public MechChassisClass ChassisClass { get; private set; } = MechChassisClass.Standard;
	public bool IsInvulnerable => _respawnInvuln > 0f;
	public bool IsHumanPilot => OwningPeerId != 0 || IsPlayerControlled;
	public bool IsLocalPilot
	{
		get
		{
			var net = GetNodeOrNull<NetSession>("/root/NetSession");
			if (net is not { IsOnline: true })
				return IsPlayerControlled;
			return OwningPeerId != 0 && OwningPeerId == net.LocalPeerId;
		}
	}

	public void ConfigureNetworkPilot(int peerId, bool human, TeamId team = TeamId.Player)
	{
		OwningPeerId = peerId;
		IsPlayerControlled = human;
		Team = team;
		SetMultiplayerAuthority(1); // host-authoritative simulation
		EnsureNetworkSync();
		if (human && _missileDecal == null)
		{
			_missileDecal = new MissileLockDecal { Name = "MissileLockDecal" };
			AddChild(_missileDecal);
		}
	}

	public void AttachHostReplication()
	{
		SetMultiplayerAuthority(1);
		EnsureNetworkSync();
	}

	/// <summary>Aim point replicated so clients can pose turrets / show locks for remote MAPs.</summary>
	[Export]
	public Vector3 ReplicatedAimPoint
	{
		get => _aimPoint;
		set => _aimPoint = value;
	}

	public float ReplicatedFireElevation
	{
		get => _fireElevation;
		set => _fireElevation = Mathf.Clamp(value, -1f, 1f);
	}

	private void EnsureNetworkSync()
	{
		if (GetNodeOrNull("NetSync") != null)
			return;

		var sync = new MultiplayerSynchronizer { Name = "NetSync" };
		var cfg = new SceneReplicationConfig();
		cfg.AddProperty(new NodePath(":global_position"));
		cfg.AddProperty(new NodePath(":rotation"));
		cfg.AddProperty(new NodePath(":ReplicatedAimPoint"));
		cfg.AddProperty(new NodePath(":ReplicatedFireElevation"));
		cfg.AddProperty(new NodePath("Damageable:ReplicatedHealth"));
		cfg.AddProperty(new NodePath("Damageable:MaxHealth"));
		cfg.AddProperty(new NodePath("MechPowerHeat:ReplicatedHeat"));
		cfg.AddProperty(new NodePath("MechPowerHeat:ReplicatedLoad"));
		cfg.AddProperty(new NodePath("MechPowerHeat:ReplicatedOperationalMax"));
		sync.ReplicationConfig = cfg;
		AddChild(sync);
	}

	public override void _Process(double delta)
	{
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is not { IsOnline: true } || Multiplayer.IsServer())
			return;

		// Clients: pose upper body / hardpoints from replicated aim; local pilot keeps mouse aim for HUD.
		if (IsLocalPilot && _controlsEnabled)
		{
			UpdateAimFromMouse();
			TickFirstPersonHeadLook();
			UpdateAimedComponent();
			TickSensorLock();
			ApplySensorAimAssist();
			UpdateMissileDecal();
		}

		UpdateUpperBodyAim((float)delta);
		AimHardpoints();
	}

	public void ApplyNetworkInput(
		float turn,
		float throttle,
		Vector2 move,
		Vector3 aim,
		bool firePrimary,
		bool fireSecondary,
		bool sprint,
		int abilityIndex,
		float fireElevation = 0f)
	{
		_netTurn = turn;
		_netThrottle = throttle;
		_netMove = move;
		_netAim = aim;
		_netFirePrimary = firePrimary;
		_netFireSecondary = fireSecondary;
		_netSprint = sprint;
		_netAbilityIndex = abilityIndex;
		_netFireElevation = Mathf.Clamp(fireElevation, -1f, 1f);
		_hasNetInput = true;
		_netInputAge = 0f;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void RpcSubmitInput(
		float turn,
		float throttle,
		Vector2 move,
		Vector3 aim,
		bool firePrimary,
		bool fireSecondary,
		bool sprint,
		int abilityIndex,
		float fireElevation)
	{
		if (!Multiplayer.IsServer())
			return;
		var sender = Multiplayer.GetRemoteSenderId();
		if (sender != OwningPeerId)
			return;
		ApplyNetworkInput(turn, throttle, move, aim, firePrimary, fireSecondary, sprint, abilityIndex, fireElevation);
	}

	public void RespawnAt(Vector3 position, LoadoutData loadout, bool fullRepair = true)
	{
		IReadOnlyDictionary<PartSlot, PartCondition>? conditions = null;
		if (!fullRepair && IsPlayerControlled)
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			conditions = BuildPlayerConditionMap(session?.Profile);
		}

		RebuildFromLoadout(loadout, conditions, fullRepair);
		GlobalPosition = position;
		Velocity = Vector3.Zero;
		_respawnInvuln = 2.5f;
		SetControlsEnabled(true);
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
	}

	public MechAssembler? Assembler => _assembler;
	public Damageable? Health => _health;
	public AbilityController? Abilities => _abilities;
	public MechIntegrity? Integrity => _integrity;
	public MechPowerHeat? PowerHeat => _powerHeat;
	public MechPilotAI? Pilot => _pilot;
	public Vector3 AimPoint => _aimPoint;
	public PartSlot? AimedComponentSlot => _aimedComponentSlot;
	public string AimedComponentLabel => _aimedComponentLabel;
	/// <summary>-1 (legs) .. +1 (high / Titan upper). Scroll bias while firing; sensor focus adds assist pitch.</summary>
	public float FireElevationNormalized =>
		Mathf.Clamp(FireElevationPitchRadians / FireElevationMaxPitch, -1.25f, 1.25f);
	public float FireElevationPitchRadians
	{
		get
		{
			var scroll = _fireElevation * FireElevationScrollMaxPitch;
			if (!TryGetSensorAssistPitch(out var assist))
				return scroll;
			return Mathf.Clamp(assist + scroll, -FireElevationMaxPitch, FireElevationMaxPitch);
		}
	}
	/// <summary>1 = full speed. Below 1 while the Ctrl+scroll governor is engaged.</summary>
	public float SpeedGovernor => _speedGovernor;
	public bool IsSpeedGovernorActive => _speedGovernor < 0.995f;
	public MechController? SensorLockTarget =>
		_sensorLock != null && GodotObject.IsInstanceValid(_sensorLock) ? _sensorLock : null;
	public PartSlot? SensorFocusSlot => _sensorFocus;
	public bool SensorLockInVision => _sensorLockInVision;
	public bool ControlsEnabled => _controlsEnabled;
	public bool IsSprinting => _sprinting;
	public bool IsDashing => _dashRemaining > 0.01f;
	/// <summary>True while local FP hides hips/legs to avoid cockpit mesh poke-through.</summary>
	public bool FirstPersonHideLowerBody { get; private set; }

	/// <summary>
	/// Hide hips/leg visuals for local first-person until cockpit shells exist.
	/// Does not affect collision or other players' view of this MAP.
	/// </summary>
	public void SetFirstPersonHideLowerBody(bool hide)
	{
		FirstPersonHideLowerBody = hide;
		ApplyFirstPersonLowerBodyVisibility();
	}

	private void ApplyFirstPersonLowerBodyVisibility()
	{
		var hips = GetNodeOrNull<Node3D>("Sockets/Hips");
		if (hips != null)
			hips.Visible = !FirstPersonHideLowerBody;
	}
	/// <summary>Locomotion spread tier — wide reticle + projectile cone while moving or dashing.</summary>
	public bool IsWideFire => _wasMoving || IsDashing || IsSprinting;
	/// <summary>Planted aim — tight reticle and no ballistic spread.</summary>
	public bool IsStationaryAim => !IsWideFire;
	/// <summary>Sprint blocks weapon discharge.</summary>
	public bool CanFireWeapons => !IsSprinting;
	/// <summary>Ballistic spread applied per shot while <see cref="IsWideFire"/>.</summary>
	public float AimSpreadRadians => IsStationaryAim ? 0f : 0.075f;
	/// <summary>True while riding an escort carrier (no steering; aim + weapons stay live).</summary>
	public bool IsCarrierMounted => _carrierMount != null && GodotObject.IsInstanceValid(_carrierMount);
	/// <summary>
	/// How far above the floor the MAP has been lifted while mounted.
	/// Projectiles subtract this so shots still travel at grounded combat height.
	/// </summary>
	public float CarrierCombatLift => IsCarrierMounted ? Mathf.Max(0f, GlobalPosition.Y) : 0f;
	public bool IsCarrierOverdriveActive => _carrierOverdrive;
	public bool IsHeldShieldRaised(PartSlot slot) => slot switch
	{
		PartSlot.WeaponL => _shieldRaisedL,
		PartSlot.WeaponR => _shieldRaisedR,
		_ => false
	};
	public bool IsHeldShieldBroken(PartSlot slot) => slot switch
	{
		PartSlot.WeaponL => _shieldBrokenL,
		PartSlot.WeaponR => _shieldBrokenR,
		_ => false
	};
	public PartSlot? GetRaisedHeldShieldSlot()
	{
		if (_shieldRaisedL) return PartSlot.WeaponL;
		if (_shieldRaisedR) return PartSlot.WeaponR;
		return null;
	}

	public override void _Ready()
	{
		AddToGroup("mechs");
		_assembler = GetNodeOrNull<MechAssembler>(AssemblerPath);
		_health = GetNodeOrNull<Damageable>(HealthPath);
		_upperBody = GetNodeOrNull<Node3D>(UpperBodyPath);

		_abilities = GetNodeOrNull<AbilityController>("AbilityController");
		if (_abilities == null)
		{
			_abilities = new AbilityController { Name = "AbilityController" };
			AddChild(_abilities);
		}

		_integrity = GetNodeOrNull<MechIntegrity>("MechIntegrity");
		if (_integrity == null)
		{
			_integrity = new MechIntegrity { Name = "MechIntegrity" };
			AddChild(_integrity);
		}

		_powerHeat = GetNodeOrNull<MechPowerHeat>("MechPowerHeat");
		if (_powerHeat == null)
		{
			_powerHeat = new MechPowerHeat { Name = "MechPowerHeat" };
			AddChild(_powerHeat);
		}

		_pilot = GetNodeOrNull<MechPilotAI>("MechPilotAI");
		if (!HangarDisplayOnly && !IsPlayerControlled && _pilot == null)
		{
			_pilot = new MechPilotAI { Name = "MechPilotAI" };
			AddChild(_pilot);
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (!HangarDisplayOnly)
		{
			if (IsPlayerControlled)
				Team = TeamId.Player;
			else if (Team == TeamId.Player)
				Team = TeamId.Enemy;
		}

		var loadout = IsPlayerControlled
			? (session?.CurrentLoadout ?? GameCatalog.CreateStarterLoadout())
			: GameCatalog.CreateEnemyLoadout();
		if (HangarDisplayOnly || !IsPlayerControlled || session is { UsingTemporaryLoaner: true })
			RebuildFromLoadout(loadout, forceFullRepair: true);
		else
			RebuildFromLoadout(loadout, BuildPlayerConditionMap(session?.Profile));

		if (_health != null)
			_health.Died += OnDied;
		_integrity.MechCollapsed += OnDied;

		if (IsPlayerControlled)
		{
			_missileDecal = new MissileLockDecal { Name = "MissileLockDecal" };
			AddChild(_missileDecal);
		}

		if (!HangarDisplayOnly)
			OcclusionSilhouette.EnsureOn(this);
		if (!HangarDisplayOnly)
			MechLegAnimator.EnsureOn(this);
	}

	public void RebuildFromLoadout(
		LoadoutData loadout,
		IReadOnlyDictionary<PartSlot, PartCondition>? conditions = null,
		bool forceFullRepair = false)
	{
		_assembler ??= GetNodeOrNull<MechAssembler>(AssemblerPath);
		_health ??= GetNodeOrNull<Damageable>(HealthPath);
		_upperBody ??= GetNodeOrNull<Node3D>(UpperBodyPath);
		_powerHeat ??= GetNodeOrNull<MechPowerHeat>("MechPowerHeat");

		Dictionary<PartSlot, PartCondition>? effective = null;
		if (!forceFullRepair && conditions != null && conditions.Count > 0)
			effective = new Dictionary<PartSlot, PartCondition>(conditions);

		_assembler?.Assemble(loadout, effective);
		_integrity?.Bind(this, _assembler!, _health, effective);
		_abilities?.Bind(this, _assembler!, _health);
		_powerHeat?.Bind(_assembler!);
		ApplyChassisClass(ChassisClass);
		StopSprint();
		ClearHeldShieldBattleState();
		SetControlsEnabled(true);
		ApplyFirstPersonLowerBodyVisibility();
		GetNodeOrNull<MechLegAnimator>(MechLegAnimator.NodeName)?.CallDeferred("BindRig");
	}

	/// <summary>Swap a single slot in the field without resetting the rest of the chassis.</summary>
	public bool InstallFieldPart(PartSlot slot, PartData part, PartCondition? incomingCondition = null)
	{
		if (_assembler == null || !_assembler.Hardpoints.TryGetValue(slot, out var hardpoint))
			return false;

		hardpoint.Equip(part, incomingCondition ?? PartCondition.Full(PartCondition.SegmentCountFor(part)));
		_assembler.RefreshStatsAfterDamage();
		_powerHeat?.Bind(_assembler);
		_abilities?.Bind(this, _assembler, _health);
		if (slot == PartSlot.WeaponL)
		{
			_shieldPoseBlendL = 0f;
			_shieldRestCachedL = false;
			if (part.IsHeldShield)
			{
				hardpoint.Position = Vector3.Zero;
				hardpoint.Rotation = Vector3.Zero;
			}
		}
		else if (slot == PartSlot.WeaponR)
		{
			_shieldPoseBlendR = 0f;
			_shieldRestCachedR = false;
			if (part.IsHeldShield)
			{
				hardpoint.Position = Vector3.Zero;
				hardpoint.Rotation = Vector3.Zero;
			}
		}
		GetNodeOrNull<MechLegAnimator>(MechLegAnimator.NodeName)?.CallDeferred("BindRig");
		return true;
	}

	public Dictionary<PartSlot, PartCondition> CapturePartConditions() =>
		_integrity?.CaptureConditions() ?? new Dictionary<PartSlot, PartCondition>();

	private static Dictionary<PartSlot, PartCondition>? BuildPlayerConditionMap(PlayerProfile? profile)
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

	/// <summary>
	/// Applies a size-class silhouette. Titans use denser part meshes
	/// (<see cref="TitanPartVisualFactory"/>) on the MAP socket rig at ~4× scale.
	/// </summary>
	public void ApplyChassisClass(MechChassisClass chassisClass)
	{
		ChassisClass = chassisClass;
		var scale = MechChassisClassUtil.VisualScale(chassisClass);

		var sockets = GetNodeOrNull<Node3D>("Sockets");
		if (sockets != null)
			sockets.Scale = Vector3.One * scale;

		var collision = GetNodeOrNull<CollisionShape3D>("Collision");
		if (collision != null)
		{
			var box = new BoxShape3D { Size = new Vector3(1.6f, 2.2f, 1.6f) * scale };
			collision.Shape = box;
			collision.Position = new Vector3(0f, 1.1f * scale, 0f);
		}
	}

	public void SetControlsEnabled(bool enabled)
	{
		_controlsEnabled = enabled;
		if (!enabled)
		{
			Velocity = Vector3.Zero;
			StopCarrierOverdrive();
			StopSprint();
			LowerHeldShields();
			_fireElevation = 0f;
			ClearSensorLock();
		}
	}

	/// <summary>Pin the MAP to a carrier deck: locomotion + gravity off, aim/fire stay live.</summary>
	public void MountToCarrier(Node3D mountPoint)
	{
		_carrierMount = mountPoint;
		_carrierAsset = mountPoint.GetParentOrNull<EscortAsset>();
		StopCarrierOverdrive();
		StopSprint();
		LowerHeldShields();
		Velocity = Vector3.Zero;
	}

	/// <summary>Release from the carrier and set down at a ground position beside it.</summary>
	public void DismountFromCarrier(Vector3 groundPosition)
	{
		StopCarrierOverdrive();
		_carrierMount = null;
		_carrierAsset = null;
		Velocity = Vector3.Zero;
		GlobalPosition = groundPosition with { Y = GlobalPosition.Y };
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		if (_respawnInvuln > 0f)
			_respawnInvuln = Mathf.Max(0f, _respawnInvuln - dt);
		if (_hasNetInput)
		{
			_netInputAge += dt;
			if (_netInputAge > 0.35f)
				_hasNetInput = false;
		}

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var online = net is { IsOnline: true };

		TickOverheatAudio();

		// Clients do not simulate host-auth mechs — replication drives their transforms.
		if (online && !Multiplayer.IsServer())
		{
			if (IsLocalPilot && _controlsEnabled)
				SubmitLocalInputToHost();
			return;
		}

		if (!_controlsEnabled || (_health?.IsDead ?? false) || (_integrity?.IsCollapsed ?? false))
		{
			ClearMissileLock();
			_abilities?.EndPulseRepair(applyCooldown: false);
			Velocity = Vector3.Zero;
			StopCarrierOverdrive();
			StopSprint();
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		if (IsCarrierMounted)
		{
			TickMountedRide(dt);
			return;
		}

		float turn = 0f, throttle = 0f, strafe = 0f;
		var move = Vector2.Zero;
		var firePrimary = false;
		var fireSecondary = false;
		var abilityIndex = -1;
		var wantSprint = false;

		if (online && OwningPeerId != 0)
		{
			if (OwningPeerId == Multiplayer.GetUniqueId())
			{
				UpdateAimFromMouse();
				TickFirstPersonHeadLook();
				UpdateAimedComponent();
				TickSensorLock();
				ApplySensorAimAssist();
				ReadPlayerMove(out turn, out throttle, out move, out strafe);
				firePrimary = Input.IsActionPressed("fire_primary");
				fireSecondary = Input.IsActionPressed("fire_secondary");
				wantSprint = UpdateSprintGesture(dt);
				TryJumpInput();
				abilityIndex = ReadPlayerAbilityInput();
			}
			else if (_hasNetInput)
			{
				turn = _netTurn;
				throttle = _netThrottle;
				move = _netMove;
				_aimPoint = _netAim;
				firePrimary = _netFirePrimary;
				fireSecondary = _netFireSecondary;
				wantSprint = _netSprint;
				abilityIndex = _netAbilityIndex;
				_netAbilityIndex = -1;
				UpdateAimedComponent();
			}
			else
			{
				ApplyGravity(dt);
				MoveAndSlide();
				return;
			}
		}
		else if (IsPlayerControlled)
		{
			UpdateAimFromMouse();
			TickFirstPersonHeadLook();
			UpdateAimedComponent();
			TickSensorLock();
			ApplySensorAimAssist();
			ReadPlayerMove(out turn, out throttle, out move, out strafe);
			firePrimary = Input.IsActionPressed("fire_primary");
			fireSecondary = Input.IsActionPressed("fire_secondary");
			wantSprint = UpdateSprintGesture(dt);
			TryJumpInput();
			abilityIndex = ReadPlayerAbilityInput();
		}
		else if (_pilot != null)
		{
			var cmd = _pilot.BuildCommand(dt);
			_aimPoint = cmd.AimPoint;
			_aimedComponentSlot = cmd.AimedComponent;
			_aimedComponentLabel = cmd.AimedComponent.HasValue
				? $"AI lock {cmd.AimedComponent}"
				: "";
			turn = cmd.Turn;
			throttle = cmd.Throttle;
			move = cmd.Move;
			firePrimary = cmd.FirePrimary;
			fireSecondary = cmd.FireSecondary;
			abilityIndex = cmd.AbilityIndex;
			wantSprint = cmd.Sprint;
			_fireElevation = 0f;
		}
		else
		{
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		UpdateSprint(wantSprint, move, throttle);
		var remotePilot = online && OwningPeerId != 0 && OwningPeerId != Multiplayer.GetUniqueId();
		UpdateFireElevationState(firePrimary || fireSecondary, fromRemotePeer: remotePilot);

		TickDashCooldown(dt);
		Vector3 desiredHorizontal;
		if (IsLocalFirstPerson() && IsPlayerControlled && !remotePilot)
			desiredHorizontal = ProcessFirstPersonLegMovement(dt, move);
		else if (_assembler?.LegMode == LegMode.Gimbaled)
			desiredHorizontal = ProcessGimbaledLegMovement(dt, move);
		else
			desiredHorizontal = ProcessLockedLegMovement(dt, turn, throttle, remotePilot ? move.X : strafe);

		var horizontal = ApplyMoveAcceleration(desiredHorizontal, dt);
		horizontal = ApplyDashOverride(horizontal, dt);

		var moving = horizontal.LengthSquared() > 0.05f;
		if (moving)
		{
			var stats = _assembler?.Stats ?? MechStats.BlindFallback;
			_powerHeat?.AddHeat(stats.MoveHeatPerSec * dt * (_sprinting ? 1f : 1f));
			if (_sprinting)
				_powerHeat?.AddHeat(stats.SprintHeatPerSec * dt);
		}
		_wasMoving = moving;

		Velocity = new Vector3(horizontal.X, Velocity.Y, horizontal.Z);
		ApplyGravity(dt);
		MoveAndSlide();

		UpdateUpperBodyAim(dt);
		AdvanceMeleeSwings(dt);
		UpdateHeldShields(firePrimary, fireSecondary);
		AimHardpoints();
		UpdateHeldShieldPoses(dt);
		UpdateMeleeContacts();

		if (firePrimary)
			FireWeapon(PartSlot.WeaponL);
		if (fireSecondary)
			FireWeapon(PartSlot.WeaponR);
		if (abilityIndex >= 0)
		{
			var aim = _aimPoint;
			// AI drops mend beacons on their own pad; player paints via hold/release.
			if (!IsHumanPilot && _abilities?.IsMendBeaconAbility(abilityIndex) == true)
				aim = GlobalPosition;
			_abilities?.TryActivate(abilityIndex, aim);
		}

		UpdateMissileDecal();
	}

	/// <summary>
	/// Riding an escort carrier: no locomotion or gravity, but the pilot keeps full aim,
	/// weapons, shields and abilities. Gimbaled torsos retain 360° coverage; locked chassis
	/// fire along the rig's facing (the trade-off for a mobile firing platform).
	/// </summary>
	private void TickMountedRide(float dt)
	{
		var firePrimary = false;
		var fireSecondary = false;
		var abilityIndex = -1;
		var wantOverdrive = false;

		if (IsPlayerControlled)
		{
			UpdateAimFromMouse();
			TickFirstPersonHeadLook();
			UpdateAimedComponent();
			TickSensorLock();
			ApplySensorAimAssist();
			firePrimary = Input.IsActionPressed("fire_primary");
			fireSecondary = Input.IsActionPressed("fire_secondary");
			wantOverdrive = UpdateSprintGesture(dt);
			TryJumpInput();
			abilityIndex = ReadPlayerAbilityInput();
			UpdateFireElevationState(firePrimary || fireSecondary);
		}
		else
		{
			UpdateAimedComponent();
			_fireElevation = 0f;
		}

		if (_carrierMount != null && GodotObject.IsInstanceValid(_carrierMount))
		{
			GlobalPosition = _carrierMount.GlobalPosition;
			// Chassis follows the rig's heading; gimbaled torso still swings freely to the aim point.
			Rotation = new Vector3(Rotation.X, _carrierMount.GlobalRotation.Y, Rotation.Z);
		}
		Velocity = Vector3.Zero;

		UpdateCarrierOverdrive(wantOverdrive, dt);
		UpdateUpperBodyAim(dt);
		AdvanceMeleeSwings(dt);
		UpdateHeldShields(firePrimary, fireSecondary);
		AimHardpoints();
		UpdateHeldShieldPoses(dt);
		UpdateMeleeContacts();

		if (firePrimary)
			FireWeapon(PartSlot.WeaponL);
		if (fireSecondary)
			FireWeapon(PartSlot.WeaponR);
		if (abilityIndex >= 0)
			_abilities?.TryActivate(abilityIndex, _aimPoint);

		UpdateMissileDecal();
	}

	private void UpdateCarrierOverdrive(bool requested, float dt)
	{
		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var carrierReady = _carrierAsset is { CanReceiveOverdrive: true };
		var powerReady = _powerHeat?.CanUseAbilities == true;

		if (!requested || !carrierReady || !powerReady)
		{
			StopCarrierOverdrive();
			return;
		}

		if (!_carrierOverdrive)
		{
			var draw = Mathf.Max(0f, stats.SprintPowerLoad);
			if (_powerHeat != null && !_powerHeat.TryDraw("carrier_overdrive", draw))
			{
				StopCarrierOverdrive();
				return;
			}
			_carrierOverdrive = true;
		}

		_carrierAsset?.SetOverdrive(true);
		_powerHeat?.AddHeat(Mathf.Max(0f, stats.SprintHeatPerSec * 2f) * dt);
	}

	private void StopCarrierOverdrive()
	{
		_powerHeat?.Release("carrier_overdrive");
		_carrierAsset?.SetOverdrive(false);
		_carrierOverdrive = false;
	}

	/// <summary>
	/// Paint-lock: paint missiles + mend beacon (hold aim, release to deploy).
	/// Sensor missiles: tap with a maintained TAB lock (vision/contact per kit).
	/// Ctrl+key self-cast: beneficial utilities only (mend beacon drops at feet).
	/// Pulse Repair: hold to channel heal.
	/// </summary>
	private int ReadPlayerAbilityInput()
	{
		if (_abilities == null)
			return -1;

		var selfCast = Input.IsKeyPressed(Key.Ctrl);

		// Active pulse repair channel — release ends it.
		if (_abilities.IsPulseRepairing)
		{
			var pulseHeld = false;
			for (var i = 0; i < AbilityController.MaxAbilitySlots; i++)
			{
				if (!_abilities.IsPulseRepairAbility(i))
					continue;
				if (Input.IsActionPressed($"ability_{i + 1}"))
				{
					pulseHeld = true;
					break;
				}
			}

			if (!pulseHeld)
				_abilities.EndPulseRepair(applyCooldown: true);
			return -1;
		}

		// Continue / update active paint lock (missile or mend beacon).
		if (_missileLockSlot >= 0)
		{
			var action = $"ability_{_missileLockSlot + 1}";
			if (Input.IsActionPressed(action) && _abilities.IsPaintLockAbility(_missileLockSlot))
			{
				_missilePaintPoint = _aimPoint;
				_missilePaintValid = CanCombatId(_missilePaintPoint) && _abilities.CanActivate(_missileLockSlot);
				return -1;
			}

			// Released.
			if (_missilePaintValid && _abilities.IsPaintLockAbility(_missileLockSlot))
				_abilities.TryActivate(_missileLockSlot, _missilePaintPoint);

			ClearMissileLock();
			return -1;
		}

		for (var i = 0; i < AbilityController.MaxAbilitySlots; i++)
		{
			var action = $"ability_{i + 1}";
			if (_abilities.IsPaintLockAbility(i))
			{
				if (Input.IsActionJustPressed(action) && _abilities.CanActivate(i))
				{
					// Beneficial paint utilities: Ctrl skips aim and casts on self.
					if (selfCast && _abilities.IsBeneficialSelfCastAbility(i))
					{
						_abilities.TryActivate(i, GlobalPosition);
						return -1;
					}

					_missileLockSlot = i;
					_missilePaintPoint = _aimPoint;
					_missilePaintValid = CanCombatId(_missilePaintPoint);
				}
				continue;
			}

			if (_abilities.IsPulseRepairAbility(i))
			{
				if (Input.IsActionJustPressed(action) && _abilities.CanActivate(i))
					_abilities.BeginPulseRepair(i);
				continue;
			}

			if (Input.IsActionJustPressed(action))
				return i;
		}

		return -1;
	}

	private void ClearMissileLock()
	{
		_missileLockSlot = -1;
		_missilePaintValid = false;
		_missileDecal?.SetState(false, Vector3.Zero, false);
	}

	private void UpdateMissileDecal()
	{
		if (_missileDecal == null)
			return;
		if (_missileLockSlot < 0 || _abilities == null)
		{
			_missileDecal.SetState(false, Vector3.Zero, false);
			return;
		}

		var healPaint = _abilities.IsMendBeaconAbility(_missileLockSlot);
		_missileDecal.SetState(true, _missilePaintPoint, _missilePaintValid, healPaint);
	}

	private void UpdateSprint(bool wantSprint, Vector2 move, float throttle)
	{
		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var moving = move.LengthSquared() > 0.05f || Mathf.Abs(throttle) > 0.05f;
		var can = _powerHeat?.CanSprint == true && stats.CanSprint && moving;

		if (wantSprint && can)
		{
			if (!_sprinting)
			{
				if (_powerHeat != null && !_powerHeat.TryDraw("sprint", stats.SprintPowerLoad))
				{
					_sprinting = false;
					return;
				}
				_sprinting = true;
			}
		}
		else
		{
			StopSprint();
		}
	}

	private void StopSprint()
	{
		if (_sprinting)
			_powerHeat?.Release("sprint");
		_sprinting = false;
	}

	/// <summary>
	/// Tap sprint (under threshold) = thruster dash. Hold past threshold = sustained sprint.
	/// </summary>
	private bool UpdateSprintGesture(float dt)
	{
		var pressed = Input.IsActionPressed("sprint");
		if (Input.IsActionJustPressed("sprint"))
		{
			_sprintPressTime = 0f;
			_sprintHoldCommitted = false;
		}

		if (pressed && _sprintPressTime >= 0f)
		{
			_sprintPressTime += dt;
			if (_sprintPressTime >= SprintTapThreshold)
				_sprintHoldCommitted = true;
		}

		if (Input.IsActionJustReleased("sprint"))
		{
			var tap = _sprintPressTime >= 0f && !_sprintHoldCommitted && _sprintPressTime < SprintTapThreshold;
			_sprintPressTime = -1f;
			_sprintHoldCommitted = false;
			if (tap)
				TryStartDash();
			return false;
		}

		return pressed && _sprintHoldCommitted;
	}

	private void TickDashCooldown(float dt)
	{
		if (dt > 0f && _dashCooldown > 0f)
			_dashCooldown = Mathf.Max(0f, _dashCooldown - dt);
		if (_dashRemaining > 0f)
			_dashRemaining = Mathf.Max(0f, _dashRemaining - dt);
	}

	private void TryStartDash()
	{
		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var mobility = _integrity?.LegMobilityFactor ?? 1f;
		if (!stats.HasThruster || stats.DashSpeed <= 0.1f || mobility <= 0.001f)
			return;
		if (_dashCooldown > 0.01f || _dashRemaining > 0.01f)
			return;
		if (_powerHeat != null && stats.DashPowerCost > 0.01f && !_powerHeat.CanSpend(stats.DashPowerCost))
			return;

		var dir = ResolveDashDirection();
		if (dir.LengthSquared() < 0.01f)
			return;

		if (_powerHeat != null && stats.DashPowerCost > 0.01f)
			_powerHeat.TrySpend(stats.DashPowerCost);
		_powerHeat?.AddHeat(stats.DashHeat);

		_dashDirection = dir.Normalized();
		_dashRemaining = Mathf.Max(0.08f, stats.DashDuration);
		_dashCooldown = Mathf.Max(0.2f, stats.DashCooldown);
		StopSprint();
	}

	private Vector3 ResolveDashDirection()
	{
		var move = Vector2.Zero;
		if (Input.IsActionPressed("move_forward")) move.Y += 1f;
		if (Input.IsActionPressed("move_back")) move.Y -= 1f;
		if (Input.IsActionPressed("turn_left")) move.X -= 1f;
		if (Input.IsActionPressed("turn_right")) move.X += 1f;
		if (IsLocalFirstPerson())
		{
			if (Input.IsPhysicalKeyPressed(Key.Q)) move.X -= 1f;
			if (Input.IsPhysicalKeyPressed(Key.E)) move.X += 1f;
		}

		Basis basis;
		if (IsLocalFirstPerson() && GetViewport()?.GetCamera3D() is TopDownCamera cam)
		{
			var yaw = cam.BodyLookYaw;
			basis = Basis.FromEuler(new Vector3(0f, yaw, 0f));
		}
		else
		{
			basis = GlobalTransform.Basis;
		}

		var forward = -basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		var right = basis.X;
		right.Y = 0f;
		right = right.Normalized();

		if (move.LengthSquared() > 0.01f)
			return (right * move.X + forward * move.Y).Normalized();
		return forward;
	}

	private Vector3 ApplyDashOverride(Vector3 horizontal, float dt)
	{
		if (_dashRemaining <= 0f)
			return horizontal;

		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var speed = stats.DashSpeed * (_integrity?.LegMobilityFactor ?? 1f);
		var dashVel = _dashDirection * speed;
		_moveVelocity = dashVel;
		return dashVel;
	}

	private void TryJumpInput()
	{
		if (!Input.IsActionJustPressed("jump"))
			return;

		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var mobility = _integrity?.LegMobilityFactor ?? 1f;
		if (!stats.HasBooster || stats.JumpImpulse <= 0.1f || mobility <= 0.001f)
			return;
		if (!IsOnFloor())
			return;
		if (_powerHeat != null && stats.JumpPowerCost > 0.01f && !_powerHeat.CanSpend(stats.JumpPowerCost))
			return;

		if (_powerHeat != null && stats.JumpPowerCost > 0.01f)
			_powerHeat.TrySpend(stats.JumpPowerCost);
		_powerHeat?.AddHeat(stats.JumpHeat);
		Velocity = new Vector3(Velocity.X, stats.JumpImpulse, Velocity.Z);
	}

	private Vector3 ApplyMoveAcceleration(Vector3 desired, float dt)
	{
		var legType = _assembler?.LegType ?? LegType.Bipedal;
		// Lower = massier. Walk should feel like hauling chassis; sprint/dash snap.
		float accel;
		if (_dashRemaining > 0f)
			accel = 40f;
		else if (_sprinting)
			accel = 22f;
		else if (legType == LegType.Tracks)
			accel = 16f;
		else if (legType == LegType.Hexapod)
			accel = 3.2f;
		else
			accel = 3.8f; // biped walk — deliberately sluggish

		var t = 1f - Mathf.Exp(-accel * dt);
		_moveVelocity = _moveVelocity.Lerp(desired, t);
		if (desired.LengthSquared() < 0.01f && _moveVelocity.LengthSquared() < 0.04f)
			_moveVelocity = Vector3.Zero;
		return _moveVelocity;
	}

	/// <summary>
	/// FP: WASD legs relative to body look. LegMode does not tank-turn or change aim here.
	/// </summary>
	private Vector3 ProcessFirstPersonLegMovement(float dt, Vector2 moveInput)
	{
		var mobility = _integrity?.LegMobilityFactor ?? 1f;
		if (mobility <= 0.001f)
			return Vector3.Zero;

		if (GetViewport()?.GetCamera3D() is not TopDownCamera cam)
			return Vector3.Zero;

		var yaw = cam.BodyLookYaw;
		var forward = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
		var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));

		if (moveInput.LengthSquared() < 0.01f)
			return Vector3.Zero;

		var moveDirection = (right * moveInput.X + forward * moveInput.Y);
		if (moveDirection.LengthSquared() < 0.01f)
			return Vector3.Zero;
		moveDirection = moveDirection.Normalized();

		// Legs slowly face travel direction under the look-driven torso (clunky walk turn-in).
		var targetYaw = Mathf.Atan2(-moveDirection.X, -moveDirection.Z);
		var turnScale = _sprinting ? 1.1f : 0.45f;
		var turnRateRadians = Mathf.DegToRad(
			(_assembler?.TurnRateDegrees ?? 80f) * GetTurnMultiplier() * GetWeightTurnMultiplier() * mobility)
			* dt * turnScale;
		Rotation = new Vector3(Rotation.X, Mathf.RotateToward(Rotation.Y, targetYaw, turnRateRadians), Rotation.Z);

		return moveDirection * GetCurrentSpeed() * mobility;
	}

	private void ClearHeldShieldBattleState()
	{
		_shieldBrokenL = false;
		_shieldBrokenR = false;
		LowerHeldShields();
		ResetHeldShieldPoses();
	}

	private void LowerHeldShields()
	{
		if (_shieldRaisedL)
			_powerHeat?.Release(ShieldDrainKey(PartSlot.WeaponL));
		if (_shieldRaisedR)
			_powerHeat?.Release(ShieldDrainKey(PartSlot.WeaponR));
		_shieldRaisedL = false;
		_shieldRaisedR = false;
	}

	private static string ShieldDrainKey(PartSlot slot) => $"shield_{slot}";

	private void UpdateHeldShields(bool firePrimary, bool fireSecondary)
	{
		UpdateHeldShieldArm(PartSlot.WeaponL, firePrimary, ref _shieldRaisedL, ref _shieldBrokenL);
		UpdateHeldShieldArm(PartSlot.WeaponR, fireSecondary, ref _shieldRaisedR, ref _shieldBrokenR);
	}

	/// <summary>
	/// Pull raised shields forward and toward chassis center (cover pose).
	/// </summary>
	private void UpdateHeldShieldPoses(float dt)
	{
		var rate = 1f / Mathf.Max(0.05f, ShieldRaiseSeconds);
		UpdateHeldShieldPoseArm(
			PartSlot.WeaponL, _shieldRaisedL, ref _shieldPoseBlendL,
			ref _shieldRestPosL, ref _shieldRestRotL, ref _shieldRestCachedL,
			ShieldCoverOffsetL, rate, dt);
		UpdateHeldShieldPoseArm(
			PartSlot.WeaponR, _shieldRaisedR, ref _shieldPoseBlendR,
			ref _shieldRestPosR, ref _shieldRestRotR, ref _shieldRestCachedR,
			ShieldCoverOffsetR, rate, dt);
	}

	private void UpdateHeldShieldPoseArm(
		PartSlot slot,
		bool raised,
		ref float blend,
		ref Vector3 restPos,
		ref Vector3 restRot,
		ref bool restCached,
		Vector3 coverOffset,
		float rate,
		float dt)
	{
		if (_assembler == null || !_assembler.Hardpoints.TryGetValue(slot, out var hp))
		{
			restCached = false;
			blend = 0f;
			return;
		}

		if (hp.EquippedPart is not { IsHeldShield: true } || hp.IsDestroyed)
		{
			if (restCached)
				ApplyShieldPose(hp, restPos, restRot, 0f, coverOffset);
			restCached = false;
			blend = 0f;
			return;
		}

		if (!restCached)
		{
			restPos = hp.Position;
			restRot = hp.Rotation;
			restCached = true;
		}

		var target = raised ? 1f : 0f;
		blend = Mathf.MoveToward(blend, target, rate * dt);
		ApplyShieldPose(hp, restPos, restRot, blend, coverOffset);
	}

	private static void ApplyShieldPose(
		Hardpoint hp, Vector3 restPos, Vector3 restRot, float blend, Vector3 coverOffset)
	{
		hp.Position = restPos + coverOffset * blend;
		// Keep socket-facing rest rotation — plate is authored along local -Z.
		hp.Rotation = restRot;
	}

	private void ResetHeldShieldPoses()
	{
		SnapShieldPoseToRest(PartSlot.WeaponL, ref _shieldRestPosL, ref _shieldRestRotL, ref _shieldRestCachedL);
		SnapShieldPoseToRest(PartSlot.WeaponR, ref _shieldRestPosR, ref _shieldRestRotR, ref _shieldRestCachedR);
		_shieldPoseBlendL = 0f;
		_shieldPoseBlendR = 0f;
		_shieldRestCachedL = false;
		_shieldRestCachedR = false;
	}

	private void SnapShieldPoseToRest(
		PartSlot slot, ref Vector3 restPos, ref Vector3 restRot, ref bool restCached)
	{
		if (_assembler == null || !_assembler.Hardpoints.TryGetValue(slot, out var hp))
			return;

		if (restCached)
		{
			hp.Position = restPos;
			hp.Rotation = restRot;
		}
		else
		{
			hp.Position = Vector3.Zero;
			hp.Rotation = Vector3.Zero;
		}
	}

	private void UpdateHeldShieldArm(PartSlot slot, bool wantRaise, ref bool raised, ref bool broken)
	{
		var part = GetLivingHeldShield(slot);
		if (part == null || broken)
		{
			if (raised)
			{
				_powerHeat?.Release(ShieldDrainKey(slot));
				raised = false;
			}

			return;
		}

		var canRaise = _powerHeat is { IsOverheated: false, CurrentPower: > 0.5f };
		if (!wantRaise || !canRaise)
		{
			if (raised)
			{
				_powerHeat?.Release(ShieldDrainKey(slot));
				raised = false;
			}

			return;
		}

		if (raised && _powerHeat is { CurrentPower: <= 0.01f })
		{
			_powerHeat.Release(ShieldDrainKey(slot));
			raised = false;
			return;
		}

		if (!raised)
		{
			if (_powerHeat != null && !_powerHeat.TryDraw(ShieldDrainKey(slot), part.ShieldPowerPerSec))
				return;
			raised = true;
		}
	}

	private PartData? GetLivingHeldShield(PartSlot slot)
	{
		if (_assembler == null || !_assembler.Hardpoints.TryGetValue(slot, out var hp))
			return null;
		if (hp.IsDestroyed || hp.EquippedPart is not { IsHeldShield: true })
			return null;
		return hp.EquippedPart;
	}

	/// <summary>
	/// Soak damage with a raised held shield if the impact is inside its forward arc.
	/// Returns remaining damage after absorption.
	/// </summary>
	public float TryAbsorbWithHeldShield(float damage, Vector3 hitPosition)
	{
		if (damage <= 0.01f)
			return damage;

		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (!IsHeldShieldRaised(slot) || IsHeldShieldBroken(slot))
				continue;
			var part = GetLivingHeldShield(slot);
			if (part == null)
				continue;
			if (!IsImpactInShieldArc(hitPosition, part.ShieldArcDegrees))
				continue;

			var heatPer = Mathf.Max(0.05f, part.ShieldHeatPerDamage);
			var headroom = _powerHeat != null
				? Mathf.Max(0f, _powerHeat.Stats.HeatCap - _powerHeat.CurrentHeat)
				: 0f;
			var absorbable = headroom / heatPer;
			var absorbed = Mathf.Min(damage, absorbable);
			if (absorbed > 0.01f && _powerHeat != null)
			{
				_powerHeat.AddArmHeat(slot, absorbed * heatPer);
				if (_powerHeat.IsOverheated)
					BreakHeldShield(slot);
			}

			return Mathf.Max(0f, damage - absorbed);
		}

		return damage;
	}

	private void BreakHeldShield(PartSlot slot)
	{
		switch (slot)
		{
			case PartSlot.WeaponL:
				_shieldBrokenL = true;
				if (_shieldRaisedL)
				{
					_powerHeat?.Release(ShieldDrainKey(slot));
					_shieldRaisedL = false;
				}

				_shieldPoseBlendL = 0f;
				SnapShieldPoseToRest(slot, ref _shieldRestPosL, ref _shieldRestRotL, ref _shieldRestCachedL);
				_shieldRestCachedL = false;
				break;
			case PartSlot.WeaponR:
				_shieldBrokenR = true;
				if (_shieldRaisedR)
				{
					_powerHeat?.Release(ShieldDrainKey(slot));
					_shieldRaisedR = false;
				}

				_shieldPoseBlendR = 0f;
				SnapShieldPoseToRest(slot, ref _shieldRestPosR, ref _shieldRestRotR, ref _shieldRestCachedR);
				_shieldRestCachedR = false;
				break;
		}

		SfxService.Play("alarm", 0.9f, -4f);
		GD.Print($"{Name} held shield broke ({slot}).");
	}

	private bool IsImpactInShieldArc(Vector3 hitPosition, float arcDegrees)
	{
		var facing = _upperBody != null
			? -_upperBody.GlobalTransform.Basis.Z
			: -GlobalTransform.Basis.Z;
		facing.Y = 0f;
		if (facing.LengthSquared() < 0.001f)
			return true;
		facing = facing.Normalized();

		var toHit = hitPosition - GlobalPosition;
		toHit.Y = 0f;
		if (toHit.LengthSquared() < 0.25f)
			return true;
		toHit = toHit.Normalized();

		var half = Mathf.Max(10f, arcDegrees) * 0.5f;
		return facing.Dot(toHit) >= Mathf.Cos(Mathf.DegToRad(half));
	}

	private void SubmitLocalInputToHost()
	{
		UpdateAimFromMouse();
		TickFirstPersonHeadLook();
		ReadPlayerMove(out var turn, out var throttle, out var move, out var strafe);
		// Pack FP Q/E strafe into move.X for locked-leg hosts (A/D already mirror turn).
		if (Mathf.Abs(strafe) > 0.01f && _assembler?.LegMode != LegMode.Gimbaled)
			move.X = strafe;
		var firePrimary = Input.IsActionPressed("fire_primary");
		var fireSecondary = Input.IsActionPressed("fire_secondary");
		UpdateFireElevationState(firePrimary || fireSecondary);
		var wantSprint = Input.IsActionPressed("sprint");
		var abilityIndex = ReadPlayerAbilityInput();
		RpcId(
			1,
			MethodName.RpcSubmitInput,
			turn,
			throttle,
			move,
			_aimPoint,
			firePrimary,
			fireSecondary,
			wantSprint,
			abilityIndex,
			_fireElevation);
	}

	private void ReadPlayerMove(out float turn, out float throttle, out Vector2 move, out float strafe)
	{
		turn = 0f;
		throttle = 0f;
		strafe = 0f;
		move = Vector2.Zero;

		if (Input.IsActionPressed("turn_left"))
		{
			turn += 1f;
			move.X -= 1f;
		}
		if (Input.IsActionPressed("turn_right"))
		{
			turn -= 1f;
			move.X += 1f;
		}
		if (Input.IsActionPressed("move_forward"))
		{
			throttle += 1f;
			move.Y += 1f;
		}
		if (Input.IsActionPressed("move_back"))
		{
			throttle -= GetReverseMultiplier();
			move.Y -= 1f;
		}

		// First-person only: Q/E strafe relative to chassis facing.
		if (IsLocalFirstPerson())
		{
			if (Input.IsPhysicalKeyPressed(Key.Q))
			{
				strafe -= 1f;
				move.X -= 1f;
			}
			if (Input.IsPhysicalKeyPressed(Key.E))
			{
				strafe += 1f;
				move.X += 1f;
			}
		}
	}

	private bool IsLocalFirstPerson() =>
		IsPlayerControlled
		&& GetViewport()?.GetCamera3D() is TopDownCamera { IsFirstPerson: true };

	private Vector3 ProcessLockedLegMovement(float dt, float turnInput, float throttleInput, float strafeInput = 0f)
	{
		var turnRate = (_assembler?.TurnRateDegrees ?? 80f) * GetTurnMultiplier() * GetWeightTurnMultiplier();
		var mobility = _integrity?.LegMobilityFactor ?? 1f;
		turnRate *= Mathf.Lerp(0.4f, 1f, mobility);
		RotateY(Mathf.DegToRad(turnInput * turnRate * dt));

		if (mobility <= 0.001f)
			return Vector3.Zero;

		var speed = GetCurrentSpeed() * mobility;
		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		var right = forward.Cross(Vector3.Up).Normalized();
		return forward * throttleInput * speed + right * Mathf.Clamp(strafeInput, -1f, 1f) * speed;
	}

	private Vector3 ProcessGimbaledLegMovement(float dt, Vector2 moveInput)
	{
		var mobility = _integrity?.LegMobilityFactor ?? 1f;
		if (mobility < 0.5f)
			return ProcessLockedLegMovement(dt, -Mathf.Clamp(moveInput.X, -1f, 1f), moveInput.Y);

		if (moveInput == Vector2.Zero || mobility <= 0.001f)
			return Vector3.Zero;

		Vector3 moveDirection;
		if (IsPlayerControlled)
		{
			// First person: steer relative to chassis (head look must not change move direction).
			// Third person: keep camera-relative strafe/forward.
			Basis basis;
			if (IsLocalFirstPerson())
			{
				basis = GlobalTransform.Basis;
			}
			else
			{
				var camera = GetViewport().GetCamera3D();
				basis = camera?.GlobalTransform.Basis ?? Basis.Identity;
			}

			var worldForward = -basis.Z;
			worldForward.Y = 0f;
			worldForward = worldForward.Normalized();
			var worldRight = basis.X;
			worldRight.Y = 0f;
			worldRight = worldRight.Normalized();
			moveDirection = (worldRight * moveInput.X + worldForward * moveInput.Y).Normalized();
		}
		else
		{
			var forward = -GlobalTransform.Basis.Z;
			forward.Y = 0f;
			if (_pilot?.Target != null)
			{
				forward = _pilot.Target.GlobalPosition - GlobalPosition;
				forward.Y = 0f;
			}
			if (forward.LengthSquared() < 0.001f)
				forward = -GlobalTransform.Basis.Z;
			forward = forward.Normalized();
			var right = forward.Cross(Vector3.Up).Normalized();
			moveDirection = right * moveInput.X + forward * moveInput.Y;
			if (moveDirection.LengthSquared() < 0.001f)
				return Vector3.Zero;
			moveDirection = moveDirection.Normalized();
		}

		var targetYaw = Mathf.Atan2(-moveDirection.X, -moveDirection.Z);
		var currentYaw = Rotation.Y;
		var turnRateRadians = Mathf.DegToRad(
			(_assembler?.TurnRateDegrees ?? 80f) * GetTurnMultiplier() * GetWeightTurnMultiplier() * mobility) * dt * 1.35f;
		Rotation = new Vector3(Rotation.X, Mathf.RotateToward(currentYaw, targetYaw, turnRateRadians), Rotation.Z);

		return moveDirection * GetCurrentSpeed() * mobility;
	}

	private float GetCurrentSpeed()
	{
		var walk = (_assembler?.MaxSpeed ?? 10f) * GetSpeedMultiplier() * GetWeightMoveMultiplier();
		if (!_sprinting)
			return walk * _speedGovernor;
		var mult = _assembler?.Stats.SprintMultiplier ?? 1.45f;
		return walk * mult * _speedGovernor;
	}

	private float GetWeightMoveMultiplier() =>
		Mathf.Clamp(_assembler?.Stats.WeightMoveMultiplier ?? 1f, 0f, 1f);

	private float GetWeightTurnMultiplier() =>
		Mathf.Clamp(_assembler?.Stats.WeightTurnMultiplier ?? 1f, 0f, 1f);

	private float GetTurnMultiplier() => _assembler?.LegType switch
	{
		LegType.Tracks => 0.7f,
		LegType.Hexapod => 1.25f,
		_ => 1f
	};

	private float GetSpeedMultiplier()
	{
		var chassis = MechChassisClassUtil.SpeedMultiplier(ChassisClass);
		var legs = _assembler?.LegType switch
		{
			LegType.Tracks => 1.05f,
			LegType.Hexapod => 0.95f,
			_ => 1f
		};
		return chassis * legs;
	}

	private float GetReverseMultiplier() => _assembler?.LegType switch
	{
		LegType.Tracks => 0.85f,
		LegType.Bipedal => 0.7f,
		_ => 0.9f
	};

	private void ApplyGravity(float dt)
	{
		if (!IsOnFloor())
			Velocity = new Vector3(Velocity.X, Velocity.Y - 30f * dt, Velocity.Z);
		else if (Velocity.Y < 0f)
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
	}

	private void UpdateAimFromMouse()
	{
		var camera = GetViewport().GetCamera3D();
		if (camera == null)
			return;

		Vector3 from;
		Vector3 dir;
		if (IsLocalFirstPerson())
		{
			// Captured mouselook: aim from view center, not a screen cursor.
			var center = camera.GetViewport().GetVisibleRect().Size * 0.5f;
			from = camera.ProjectRayOrigin(center);
			dir = camera.ProjectRayNormal(center);
			var space = GetWorld3D().DirectSpaceState;
			var query = PhysicsRayQueryParameters3D.Create(from, from + dir * 480f);
			query.CollideWithAreas = false;
			query.Exclude = [GetRid()];
			var hit = space.IntersectRay(query);
			_aimPoint = hit.Count > 0 ? (Vector3)hit["position"] : from + dir * 120f;
			return;
		}

		var mouse = camera.GetViewport().GetMousePosition();
		from = camera.ProjectRayOrigin(mouse);
		dir = camera.ProjectRayNormal(mouse);

		var planeY = GlobalPosition.Y - CarrierCombatLift + 1.0f;
		var plane = new Plane(Vector3.Up, planeY);
		var planeHit = plane.IntersectsRay(from, dir);
		_aimPoint = planeHit ?? (GlobalPosition - GlobalTransform.Basis.Z * 20f);
	}

	/// <summary>
	/// First-person only: arrow keys or Alt+mouse offset the head cam (~35°). Release snaps center.
	/// </summary>
	private void TickFirstPersonHeadLook()
	{
		if (!IsLocalFirstPerson())
		{
			ClearFirstPersonHeadLook();
			return;
		}

		var yawIn = 0f;
		var pitchIn = 0f;
		if (Input.IsPhysicalKeyPressed(Key.Left))
			yawIn += 1f;
		if (Input.IsPhysicalKeyPressed(Key.Right))
			yawIn -= 1f;
		if (Input.IsPhysicalKeyPressed(Key.Up))
			pitchIn += 1f;
		if (Input.IsPhysicalKeyPressed(Key.Down))
			pitchIn -= 1f;

		if (yawIn != 0f || pitchIn != 0f)
		{
			_fpHeadLookYaw = yawIn * FpHeadLookMaxYaw;
			_fpHeadLookPitch = pitchIn * FpHeadLookMaxPitch;
		}
		else if (!Input.IsKeyPressed(Key.Alt))
		{
			_fpHeadLookYaw = 0f;
			_fpHeadLookPitch = 0f;
		}

		if (GetViewport().GetCamera3D() is TopDownCamera cam)
			cam.SetHeadLookOffset(_fpHeadLookYaw, _fpHeadLookPitch);
	}

	private void ClearFirstPersonHeadLook()
	{
		if (Mathf.Abs(_fpHeadLookYaw) < 0.0001f && Mathf.Abs(_fpHeadLookPitch) < 0.0001f)
			return;
		_fpHeadLookYaw = 0f;
		_fpHeadLookPitch = 0f;
		if (GetViewport()?.GetCamera3D() is TopDownCamera cam)
			cam.SetHeadLookOffset(0f, 0f);
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsLocalPilot || !_controlsEnabled || !IsLocalFirstPerson())
			return;
		if (@event is not InputEventMouseMotion motion)
			return;
		if (!Input.IsKeyPressed(Key.Alt))
			return;

		_fpHeadLookYaw = Mathf.Clamp(
			_fpHeadLookYaw - motion.Relative.X * FpAltLookSensitivity,
			-FpHeadLookMaxYaw,
			FpHeadLookMaxYaw);
		_fpHeadLookPitch = Mathf.Clamp(
			_fpHeadLookPitch - motion.Relative.Y * FpAltLookSensitivity,
			-FpHeadLookMaxPitch,
			FpHeadLookMaxPitch);

		if (GetViewport().GetCamera3D() is TopDownCamera cam)
			cam.SetHeadLookOffset(_fpHeadLookYaw, _fpHeadLookPitch);
	}

	/// <summary>
	/// Ctrl+scroll / Ctrl+middle-click: speed governor (both camera modes).
	/// Alt+scroll in FP: inspect zoom (handled by TopDownCamera).
	/// Third person without Ctrl: scroll while firing adjusts barrel elevation.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsLocalPilot || !_controlsEnabled)
			return;
		if (@event is not InputEventMouseButton { Pressed: true } mouse)
			return;

		var ctrl = Input.IsKeyPressed(Key.Ctrl);
		if (ctrl)
		{
			if (mouse.ButtonIndex == MouseButton.Middle)
			{
				_speedGovernor = 1f;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
			{
				var delta = mouse.ButtonIndex == MouseButton.WheelUp
					? SpeedGovernorStep
					: -SpeedGovernorStep;
				_speedGovernor = Mathf.Clamp(_speedGovernor + delta, SpeedGovernorMin, 1f);
				GetViewport().SetInputAsHandled();
			}

			return;
		}

		// First person: Alt+scroll is camera inspect zoom; bare scroll does nothing.
		if (IsLocalFirstPerson())
			return;

		if (mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
			return;

		var firing = Input.IsActionPressed("fire_primary") || Input.IsActionPressed("fire_secondary");
		if (!firing || !HasElevatingWeaponEquipped())
			return;

		_fireElevation = Mathf.Clamp(
			_fireElevation + (mouse.ButtonIndex == MouseButton.WheelUp ? FireElevationStep : -FireElevationStep),
			-1f,
			1f);
		GetViewport().SetInputAsHandled();
	}

	private static void GetCameraAimRay(Camera3D camera, out Vector3 from, out Vector3 dir)
	{
		var mouse = camera.GetViewport().GetMousePosition();
		from = camera.ProjectRayOrigin(mouse);
		dir = camera.ProjectRayNormal(mouse);
	}

	private void UpdateFireElevationState(bool firing, bool fromRemotePeer = false)
	{
		if (!firing)
		{
			_fireElevation = 0f;
			return;
		}

		if (fromRemotePeer)
			_fireElevation = _netFireElevation;
	}

	private bool HasElevatingWeaponEquipped()
	{
		if (_assembler == null)
			return false;
		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (_assembler.Hardpoints.TryGetValue(slot, out var hp)
			    && hp is { CanFire: true, EquippedPart.AllowsFireElevation: true })
				return true;
		}

		return false;
	}

	public void ClearSensorLock()
	{
		_sensorLock = null;
		_sensorFocus = null;
		_sensorLockInVision = false;
	}

	public void SetSensorFocus(PartSlot slot)
	{
		if (SensorLockTarget == null)
			return;
		_sensorFocus = slot;
	}

	private void TickSensorLock()
	{
		if (Input.IsActionJustPressed("target_next"))
			CycleSensorTarget();
		else if (Input.IsActionJustPressed("target_clear") && _sensorLock != null)
		{
			ClearSensorLock();
			SfxService.Click();
		}
		else if (Input.IsActionJustPressed("target_focus_cycle"))
			CycleSensorFocus();

		ValidateSensorLock();
	}

	private void ValidateSensorLock()
	{
		var target = SensorLockTarget;
		if (target == null)
		{
			_sensorLockInVision = false;
			return;
		}

		if (target.Health?.IsDead == true
		    || target.Integrity?.IsCollapsed == true
		    || !IsHostileContact(target)
		    || !IsInSensorAcquireRange(target))
		{
			ClearSensorLock();
			return;
		}

		_sensorLockInVision = CanCombatId(target.GlobalPosition);
		_sensorFocus ??= PartSlot.Torso;
	}

	private void CycleSensorTarget()
	{
		var contacts = GatherSensorContacts();
		if (contacts.Count == 0)
		{
			ClearSensorLock();
			SfxService.Play("alarm", 1.15f, -8f);
			return;
		}

		var current = SensorLockTarget;
		var index = current == null ? -1 : contacts.IndexOf(current);
		var next = contacts[(index + 1) % contacts.Count];
		_sensorLock = next;
		_sensorFocus ??= PartSlot.Torso;
		_sensorLockInVision = CanCombatId(next.GlobalPosition);
		SfxService.Confirm();
	}

	private void CycleSensorFocus()
	{
		if (SensorLockTarget == null)
		{
			CycleSensorTarget();
			return;
		}

		var current = _sensorFocus ?? PartSlot.Torso;
		var idx = System.Array.IndexOf(SensorFocusOrder, current);
		if (idx < 0)
			idx = 0;
		_sensorFocus = SensorFocusOrder[(idx + 1) % SensorFocusOrder.Length];
		SfxService.Click();
	}

	private List<MechController> GatherSensorContacts()
	{
		var result = new List<MechController>();
		var tree = GetTree();
		if (tree == null)
			return result;

		foreach (var node in tree.GetNodesInGroup("mechs"))
		{
			if (node is not MechController other || other == this)
				continue;
			if (!IsHostileContact(other) || !IsInSensorAcquireRange(other))
				continue;
			if (other.Health?.IsDead == true || other.Integrity?.IsCollapsed == true)
				continue;
			result.Add(other);
		}

		if (result.Count == 0)
			CollectHostileMechs(tree.CurrentScene ?? tree.Root, result);

		result.Sort((a, b) =>
			GlobalPosition.DistanceSquaredTo(a.GlobalPosition)
				.CompareTo(GlobalPosition.DistanceSquaredTo(b.GlobalPosition)));
		return result;
	}

	private void CollectHostileMechs(Node node, List<MechController> into)
	{
		if (node is MechController other
		    && other != this
		    && IsHostileContact(other)
		    && IsInSensorAcquireRange(other)
		    && other.Health?.IsDead != true
		    && other.Integrity?.IsCollapsed != true
		    && !into.Contains(other))
			into.Add(other);

		foreach (var child in node.GetChildren())
			CollectHostileMechs(child, into);
	}

	private bool IsHostileContact(MechController other) =>
		TeamUtil.IsHostile(Team, other.Team);

	private bool IsInSensorAcquireRange(MechController other)
	{
		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var range = Mathf.Max(stats.VisionRange, stats.ScannerRange);
		range = Mathf.Max(range, 18f);
		return GlobalPosition.DistanceTo(other.GlobalPosition) <= range;
	}

	private void ApplySensorAimAssist()
	{
		var target = SensorLockTarget;
		if (target == null || !_sensorLockInVision || _sensorFocus == null)
			return;

		var focusHp = ResolveFocusHardpoint(target, _sensorFocus.Value);
		if (focusHp == null)
			return;

		var desired = focusHp.GlobalPosition;
		// Soft pull — keeps high-speed tracking playable without Foxhole free-aim precision.
		_aimPoint = _aimPoint.Lerp(desired, 0.55f);
		_aimedComponentSlot = focusHp.CanTakeDamage ? focusHp.Slot : _sensorFocus;
		_aimedComponentLabel =
			$"SENSOR  {ShortFocus(_sensorFocus.Value)}  ·  {focusHp.EquippedPart?.DisplayName ?? "structure"} " +
			$"({Mathf.CeilToInt(focusHp.CurrentHp)}/{Mathf.CeilToInt(Mathf.Max(1f, focusHp.MaxHp))})";
	}

	private bool TryGetSensorAssistPitch(out float pitchRadians)
	{
		pitchRadians = 0f;
		var target = SensorLockTarget;
		if (target == null || _sensorFocus == null)
			return false;

		var focusHp = ResolveFocusHardpoint(target, _sensorFocus.Value);
		if (focusHp == null)
			return false;

		var muzzle = ResolvePrimaryMuzzle();
		var to = focusHp.GlobalPosition - muzzle;
		var horiz = Mathf.Sqrt(to.X * to.X + to.Z * to.Z);
		if (horiz < 0.05f)
			return false;

		pitchRadians = Mathf.Atan2(to.Y, horiz);
		return true;
	}

	private Vector3 ResolvePrimaryMuzzle()
	{
		if (_assembler != null)
		{
			foreach (var slot in new[] { PartSlot.WeaponR, PartSlot.WeaponL })
			{
				if (_assembler.Hardpoints.TryGetValue(slot, out var hp)
				    && hp is { CanFire: true, EquippedPart: not null })
				{
					var barrel = -hp.GlobalTransform.Basis.Z;
					if (barrel.LengthSquared() > 0.001f)
						return hp.GlobalPosition + barrel.Normalized() * 1.2f;
				}
			}
		}

		return GlobalPosition + Vector3.Up * (1.2f - CarrierCombatLift);
	}

	private static Hardpoint? ResolveFocusHardpoint(MechController target, PartSlot focus)
	{
		if (target.Assembler == null)
			return null;
		if (target.Assembler.Hardpoints.TryGetValue(focus, out var hp)
		    && hp.EquippedPart != null
		    && hp.EquippedPart.VisualKind != "empty")
			return hp;

		return target.Assembler.Hardpoints.GetValueOrDefault(PartSlot.Torso)
		       ?? target.Integrity?.FindNearestComponent(target.GlobalPosition);
	}

	private static string ShortFocus(PartSlot slot) => slot switch
	{
		PartSlot.Head => "HEAD",
		PartSlot.Torso => "TORSO",
		PartSlot.Legs => "LEGS",
		PartSlot.WeaponL => "L ARM",
		PartSlot.WeaponR => "R ARM",
		_ => slot.ToString().ToUpperInvariant()
	};

	private void UpdateAimedComponent()
	{
		_aimedComponentSlot = null;
		_aimedComponentLabel = "";

		if (_assembler == null)
			return;

		var hasSharpshooter =
			(_assembler.Hardpoints.TryGetValue(PartSlot.WeaponL, out var left)
				&& left.EquippedPart?.TargetingMode == TargetingMode.AimedComponent && !left.IsDestroyed)
			|| (_assembler.Hardpoints.TryGetValue(PartSlot.WeaponR, out var right)
				&& right.EquippedPart?.TargetingMode == TargetingMode.AimedComponent && !right.IsDestroyed);

		if (!hasSharpshooter)
			return;

		var camera = GetViewport().GetCamera3D();
		if (camera == null)
		{
			_aimedComponentLabel = "no system lock";
			return;
		}

		GetCameraAimRay(camera, out var from, out var dir);
		var to = from + dir * 80f;
		var space = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 2 | 8;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = space.IntersectRay(query);
		if (hit.Count == 0)
		{
			_aimedComponentLabel = "no system lock";
			return;
		}

		var collider = hit["collider"].AsGodotObject() as Node;
		var other = FindMech(collider);
		var impact = hit["position"].AsVector3();
		if (other?.Integrity == null)
		{
			_aimedComponentLabel = "no system lock";
			return;
		}

		if (!CanCombatId(other.GlobalPosition))
		{
			_aimedComponentLabel = "out of sensor cone";
			return;
		}

		var stats = _assembler.Stats;
		var close = stats.CloseTargeting;
		if (close < 0.35f && GD.Randf() > close + 0.4f)
		{
			_aimedComponentLabel = "lock unstable";
			return;
		}

		var component = other.Integrity.FindNearestComponent(impact);
		if (component == null)
		{
			_aimedComponentLabel = "no system lock";
			return;
		}

		_aimedComponentSlot = component.Slot;
		var limbText = component.IsLegPackage
			? $" move {component.MobilityFactor * 100f:0}%"
			: "";
		_aimedComponentLabel =
			$"{component.Slot} {component.EquippedPart?.DisplayName}{limbText} " +
			$"({Mathf.CeilToInt(component.CurrentHp)}/{Mathf.CeilToInt(component.MaxHp)})";
		_aimPoint = component.GlobalPosition;
	}

	public bool CanCombatId(Vector3 worldPoint)
	{
		var stats = _assembler?.Stats ?? MechStats.BlindFallback;
		var forward = SensorMath.AimForward(this, _upperBody);
		return SensorMath.IsInCombatVision(
			GlobalPosition + Vector3.Up * 1.4f,
			forward,
			worldPoint + Vector3.Up * 1.3f,
			stats.VisionRange,
			stats.VisionAngleDeg);
	}

	private static MechController? FindMech(Node? node)
	{
		var current = node;
		while (current != null)
		{
			if (current is MechController mech)
				return mech;
			current = current.GetParent();
		}

		return null;
	}

	private void UpdateUpperBodyAim(float dt)
	{
		if (_upperBody == null)
			return;

		// FP: torso follows body look yaw (mouse). Legs stay independent under it.
		if (IsLocalFirstPerson() && IsPlayerControlled
		    && GetViewport()?.GetCamera3D() is TopDownCamera fpCam)
		{
			var desiredWorldYaw = fpCam.BodyLookYaw;
			var chassisYaw = GlobalRotation.Y;
			var desiredLocalYaw = desiredWorldYaw - chassisYaw;
			var currentYaw = _upperBody.Rotation.Y;
			var rotateSpeed = Mathf.DegToRad((_assembler?.TurnRateDegrees ?? 80f) * 2.4f) * dt;
			_upperBody.Rotation = new Vector3(
				_upperBody.Rotation.X,
				Mathf.RotateToward(currentYaw, desiredLocalYaw, rotateSpeed),
				_upperBody.Rotation.Z);
			return;
		}

		if (_assembler?.LegMode == LegMode.Gimbaled)
		{
			var localTarget = ToLocal(_aimPoint);
			localTarget.Y = _upperBody.Position.Y;
			if (_upperBody.Position.DistanceSquaredTo(localTarget) > 0.01f)
			{
				var currentYaw = _upperBody.Rotation.Y;
				var desiredYaw = Mathf.Atan2(localTarget.X, localTarget.Z) + Mathf.Pi;
				var rotateSpeed = Mathf.DegToRad((_assembler?.TurnRateDegrees ?? 80f) * 1.6f) * dt;
				_upperBody.Rotation = new Vector3(
					_upperBody.Rotation.X,
					Mathf.RotateToward(currentYaw, desiredYaw, rotateSpeed),
					_upperBody.Rotation.Z);
			}
		}
		else
		{
			_upperBody.Rotation = new Vector3(_upperBody.Rotation.X, 0f, _upperBody.Rotation.Z);
		}
	}

	private void AimHardpoints()
	{
		if (_assembler == null)
			return;

		var chassisForward = _upperBody != null
			? -_upperBody.GlobalTransform.Basis.Z
			: -GlobalTransform.Basis.Z;
		// First person: mouse ray supplies pitch; skip scroll elevation bias.
		// Third person: clamp gimbal yaw so arms cannot fold through the body (~215° outward-biased).
		var elevationPitch = IsLocalFirstPerson() ? 0f : FireElevationPitchRadians;
		var clampGimbalBody = !IsLocalFirstPerson();
		foreach (var hp in _assembler.Hardpoints.Values)
			hp.AimAt(_aimPoint, chassisForward, elevationPitch, clampGimbalBody);
	}

	private void AdvanceMeleeSwings(float dt)
	{
		if (_assembler == null)
			return;

		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (_assembler.Hardpoints.TryGetValue(slot, out var hardpoint)
			    && hardpoint.EquippedPart?.WeaponFamily == WeaponFamily.Melee)
				hardpoint.AdvanceMeleeSwing(dt);
		}
	}

	private void UpdateMeleeContacts()
	{
		if (_assembler == null)
			return;

		var aimed = _aimedComponentSlot;
		if (aimed.HasValue && !CanCombatId(_aimPoint))
			aimed = null;

		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (!_assembler.Hardpoints.TryGetValue(slot, out var hardpoint)
			    || hardpoint.EquippedPart?.WeaponFamily != WeaponFamily.Melee)
				continue;

			if (hardpoint.TryMeleeContact(this, aimed, out var contactHeat, out _))
				_powerHeat?.AddArmHeat(slot, contactHeat);
		}
	}

	private void FireWeapon(PartSlot slot)
	{
		if (_assembler == null)
			return;
		if (_sprinting)
			return;
		if (!_assembler.Hardpoints.TryGetValue(slot, out var hardpoint))
			return;
		if (hardpoint.EquippedPart == null)
			return;

		var part = hardpoint.EquippedPart;
		if (part.IsHeldShield)
			return;

		var powerCost = part.PowerPerShot;
		if (_powerHeat != null && powerCost > 0.01f && !_powerHeat.CanSpend(powerCost))
		{
			TryPlayDryFire(slot, part);
			return;
		}

		if (part.WeaponFamily == WeaponFamily.Melee)
		{
			hardpoint.TryStartMeleeSwing(
				_aimPoint,
				_assembler.FireRateMultiplier,
				_powerHeat?.FireRateThrottle ?? 1f);
			return;
		}

		var aimed = _aimedComponentSlot;
		if (aimed.HasValue && !CanCombatId(_aimPoint))
			aimed = null;

		var sensorAssist = SensorLockTarget != null
			&& _sensorLockInVision
			&& _sensorFocus.HasValue
			&& aimed.HasValue;

		var heatOver = _powerHeat != null && _powerHeat.HeatRatio > 0.7f ? 1.35f : 1f;
		var ghost = FamilyHeatMultiplier(part.WeaponFamily, slot);
		var parent = GetTree().CurrentScene ?? GetParent();

		var fired = hardpoint.TryFire(
			this,
			parent!,
			_assembler.FireRateMultiplier,
			_powerHeat?.FireRateThrottle ?? 1f,
			_aimPoint,
			aimed,
			out _,
			out var heat,
			forcePreferredSlot: sensorAssist);

		if (!fired)
			return;

		_powerHeat?.TrySpend(powerCost);
		if (IsHumanPilot)
			TelemetryUtil.Match(this)?.Telemetry.RecordShot(missile: false);
		_powerHeat?.AddArmHeat(slot, heat * heatOver * ghost);
		NoteFamilyFire(part.WeaponFamily, slot);
	}

	/// <summary>Empty-chamber click when the pilot holds fire without enough power.</summary>
	private void TryPlayDryFire(PartSlot slot, PartData part)
	{
		if (!IsLocalPilot || part.WeaponFamily == WeaponFamily.Melee)
			return;

		var now = Time.GetTicksMsec() * 0.001f;
		var interval = part.FireRate > 0.05f ? 1f / part.FireRate : 0.2f;
		interval = Mathf.Clamp(interval, 0.09f, 0.4f);

		if (slot == PartSlot.WeaponR)
		{
			if (now < _dryFireReadyR)
				return;
			_dryFireReadyR = now + interval;
		}
		else
		{
			if (now < _dryFireReadyL)
				return;
			_dryFireReadyL = now + interval;
		}

		SfxService.PlayDryFire(-4f);
	}

	private void TickOverheatAudio()
	{
		if (!IsLocalPilot || HangarDisplayOnly || _powerHeat == null)
		{
			if (_overheatFxActive)
			{
				SfxService.EndOverheatFx();
				_overheatFxActive = false;
			}
			return;
		}

		var dead = _health?.IsDead == true || _integrity?.IsCollapsed == true;
		var overheated = !dead && _powerHeat.IsOverheated;
		if (overheated == _overheatFxActive)
			return;

		if (overheated)
		{
			SfxService.BeginOverheatFx();
			_overheatFxActive = true;
		}
		else
		{
			SfxService.EndOverheatFx();
			_overheatFxActive = false;
		}
	}

	/// <summary>
	/// Ghost-heat lite: firing the other arm weapon of the same family within 0.55s
	/// spikes heat (dual ballistic/energy alpha strikes get expensive).
	/// </summary>
	private float FamilyHeatMultiplier(WeaponFamily family, PartSlot slot)
	{
		if (family is WeaponFamily.None or WeaponFamily.Support or WeaponFamily.Melee)
			return 1f;

		var now = Time.GetTicksMsec() * 0.001f;
		if (family == _ghostLastFamily
		    && _ghostLastSlot.HasValue
		    && _ghostLastSlot.Value != slot
		    && now - _ghostLastTime < 0.55f)
			return 1.6f;
		return 1f;
	}

	private void NoteFamilyFire(WeaponFamily family, PartSlot slot)
	{
		if (family is WeaponFamily.None or WeaponFamily.Support or WeaponFamily.Melee)
			return;
		_ghostLastFamily = family;
		_ghostLastSlot = slot;
		_ghostLastTime = Time.GetTicksMsec() * 0.001f;
	}

	private void OnDied()
	{
		SetControlsEnabled(false);
		GD.Print($"{Name} destroyed.");
	}
}
