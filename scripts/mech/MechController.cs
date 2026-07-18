using Godot;

namespace Mechanize;

public partial class MechController : CharacterBody3D
{
	[Export] public bool IsPlayerControlled { get; set; } = true;
	[Export] public TeamId Team { get; set; } = TeamId.Player;
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
	private bool _shieldRaisedL;
	private bool _shieldRaisedR;
	private bool _shieldBrokenL;
	private bool _shieldBrokenR;
	private Node3D? _carrierMount;
	private EscortAsset? _carrierAsset;
	private bool _carrierOverdrive;

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

	public void ConfigureNetworkPilot(int peerId, bool human)
	{
		OwningPeerId = peerId;
		IsPlayerControlled = human;
		Team = TeamId.Player;
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

	private void EnsureNetworkSync()
	{
		if (GetNodeOrNull("NetSync") != null)
			return;

		var sync = new MultiplayerSynchronizer { Name = "NetSync" };
		var cfg = new SceneReplicationConfig();
		cfg.AddProperty(new NodePath(":global_position"));
		cfg.AddProperty(new NodePath(":rotation"));
		cfg.AddProperty(new NodePath(":ReplicatedAimPoint"));
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
			UpdateAimedComponent();
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
		int abilityIndex)
	{
		_netTurn = turn;
		_netThrottle = throttle;
		_netMove = move;
		_netAim = aim;
		_netFirePrimary = firePrimary;
		_netFireSecondary = fireSecondary;
		_netSprint = sprint;
		_netAbilityIndex = abilityIndex;
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
		int abilityIndex)
	{
		if (!Multiplayer.IsServer())
			return;
		var sender = Multiplayer.GetRemoteSenderId();
		if (sender != OwningPeerId)
			return;
		ApplyNetworkInput(turn, throttle, move, aim, firePrimary, fireSecondary, sprint, abilityIndex);
	}

	public void RespawnAt(Vector3 position, LoadoutData loadout)
	{
		RebuildFromLoadout(loadout);
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
	public bool ControlsEnabled => _controlsEnabled;
	public bool IsSprinting => _sprinting;
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
		if (!IsPlayerControlled && _pilot == null)
		{
			_pilot = new MechPilotAI { Name = "MechPilotAI" };
			AddChild(_pilot);
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (IsPlayerControlled)
			Team = TeamId.Player;
		else if (Team == TeamId.Player)
			Team = TeamId.Enemy;

		var loadout = IsPlayerControlled
			? (session?.CurrentLoadout ?? GameCatalog.CreateStarterLoadout())
			: GameCatalog.CreateEnemyLoadout();
		RebuildFromLoadout(loadout);

		if (_health != null)
			_health.Died += OnDied;
		_integrity.MechCollapsed += OnDied;

		if (IsPlayerControlled)
		{
			_missileDecal = new MissileLockDecal { Name = "MissileLockDecal" };
			AddChild(_missileDecal);
		}

		OcclusionSilhouette.EnsureOn(this);
	}

	public void RebuildFromLoadout(LoadoutData loadout)
	{
		_assembler ??= GetNodeOrNull<MechAssembler>(AssemblerPath);
		_health ??= GetNodeOrNull<Damageable>(HealthPath);
		_upperBody ??= GetNodeOrNull<Node3D>(UpperBodyPath);
		_powerHeat ??= GetNodeOrNull<MechPowerHeat>("MechPowerHeat");
		_assembler?.Assemble(loadout);

		_integrity?.Bind(this, _assembler!, _health);
		_abilities?.Bind(this, _assembler!, _health);
		_powerHeat?.Bind(_assembler!);
		ApplyChassisClass(ChassisClass);
		StopSprint();
		ClearHeldShieldBattleState();
		SetControlsEnabled(true);
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

		float turn = 0f, throttle = 0f;
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
				UpdateAimedComponent();
				ReadPlayerMove(out turn, out throttle, out move);
				firePrimary = Input.IsActionPressed("fire_primary");
				fireSecondary = Input.IsActionPressed("fire_secondary");
				wantSprint = Input.IsActionPressed("sprint");
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
			UpdateAimedComponent();
			ReadPlayerMove(out turn, out throttle, out move);
			firePrimary = Input.IsActionPressed("fire_primary");
			fireSecondary = Input.IsActionPressed("fire_secondary");
			wantSprint = Input.IsActionPressed("sprint");
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
		}
		else
		{
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		UpdateSprint(wantSprint, move, throttle);

		var horizontal = _assembler?.LegMode == LegMode.Gimbaled
			? ProcessGimbaledLegMovement(dt, move)
			: ProcessLockedLegMovement(dt, turn, throttle);

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
		AimHardpoints();
		UpdateMeleeContacts();
		UpdateHeldShields(firePrimary, fireSecondary);

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
			UpdateAimedComponent();
			firePrimary = Input.IsActionPressed("fire_primary");
			fireSecondary = Input.IsActionPressed("fire_secondary");
			wantOverdrive = Input.IsActionPressed("sprint");
			abilityIndex = ReadPlayerAbilityInput();
		}
		else
		{
			UpdateAimedComponent();
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
		AimHardpoints();
		UpdateHeldShields(firePrimary, fireSecondary);

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
	/// Paint-lock: missiles + mend beacon (hold aim, release to deploy).
	/// Pulse Repair: hold to channel heal.
	/// </summary>
	private int ReadPlayerAbilityInput()
	{
		if (_abilities == null)
			return -1;

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

	private void ClearHeldShieldBattleState()
	{
		_shieldBrokenL = false;
		_shieldBrokenR = false;
		LowerHeldShields();
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
				_powerHeat.AddHeat(absorbed * heatPer);
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
				break;
			case PartSlot.WeaponR:
				_shieldBrokenR = true;
				if (_shieldRaisedR)
				{
					_powerHeat?.Release(ShieldDrainKey(slot));
					_shieldRaisedR = false;
				}
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
		ReadPlayerMove(out var turn, out var throttle, out var move);
		var firePrimary = Input.IsActionPressed("fire_primary");
		var fireSecondary = Input.IsActionPressed("fire_secondary");
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
			abilityIndex);
	}

	private void ReadPlayerMove(out float turn, out float throttle, out Vector2 move)
	{
		turn = 0f;
		throttle = 0f;
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
	}

	private Vector3 ProcessLockedLegMovement(float dt, float turnInput, float throttleInput)
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
		return forward * throttleInput * speed;
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
			var camera = GetViewport().GetCamera3D();
			var basis = camera?.GlobalTransform.Basis ?? Basis.Identity;
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
			return walk;
		var mult = _assembler?.Stats.SprintMultiplier ?? 1.45f;
		return walk * mult;
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

		var mouse = GetViewport().GetMousePosition();
		var from = camera.ProjectRayOrigin(mouse);
		var dir = camera.ProjectRayNormal(mouse);

		// Aim on a plane ~1m above the MAP's *grounded* feet. While riding a tall carrier,
		// subtract the deck lift so the cursor still paints at normal combat height.
		var planeY = GlobalPosition.Y - CarrierCombatLift + 1.0f;
		var plane = new Plane(Vector3.Up, planeY);
		var hit = plane.IntersectsRay(from, dir);
		_aimPoint = hit ?? (GlobalPosition - GlobalTransform.Basis.Z * 20f);
	}

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

		var mouse = GetViewport().GetMousePosition();
		var from = camera.ProjectRayOrigin(mouse);
		var to = from + camera.ProjectRayNormal(mouse) * 80f;
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
			? $" limbs {component.LimbsAlive}/{component.LimbCount}"
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
		foreach (var hp in _assembler.Hardpoints.Values)
			hp.AimAt(_aimPoint, chassisForward);
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
				_powerHeat?.AddHeat(contactHeat);
		}
	}

	private void FireWeapon(PartSlot slot)
	{
		if (_assembler == null)
			return;
		if (!_assembler.Hardpoints.TryGetValue(slot, out var hardpoint))
			return;
		if (hardpoint.EquippedPart == null)
			return;

		var part = hardpoint.EquippedPart;
		if (part.IsHeldShield || part.WeaponFamily == WeaponFamily.Melee)
			return;

		var powerCost = part.PowerPerShot;
		if (_powerHeat != null && powerCost > 0.01f && !_powerHeat.CanSpend(powerCost))
			return;

		var aimed = _aimedComponentSlot;
		if (aimed.HasValue && !CanCombatId(_aimPoint))
			aimed = null;

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
			out var heat);

		if (!fired)
			return;

		_powerHeat?.TrySpend(powerCost);
		if (IsHumanPilot)
			TelemetryUtil.Match(this)?.Telemetry.RecordShot(missile: false);
		_powerHeat?.AddHeat(heat * heatOver * ghost);
		NoteFamilyFire(part.WeaponFamily, slot);
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
