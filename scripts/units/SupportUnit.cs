using Godot;

namespace Mechanize;

/// <summary>
/// Lightweight AI fodder unit — not a mech. Fixed archetype profile, single HP pool.
/// </summary>
public partial class SupportUnit : CharacterBody3D
{
	[Export] public string UnitId { get; set; } = "light_tank";
	[Export] public TeamId Team { get; set; } = TeamId.Enemy;

	private SupportUnitData? _data;
	private Damageable? _health;
	private SupportPilotAI? _pilot;
	private Node3D? _visual;
	private Node3D? _turret;
	private DamageSmoke? _damageSmoke;
	private float _fireCooldown;
	private bool _alive = true;

	public SupportUnitData? Data => _data;
	public Damageable? Health => _health;
	public bool IsAlive => _alive && (_health?.IsDead != true);
	public bool IsStaticUnit => _data?.PreferStatic == true;

	public override void _Ready()
	{
		SupportCatalog.EnsureBuilt();
		_data = SupportCatalog.Get(UnitId) ?? SupportCatalog.Get("light_tank");
		CollisionLayer = 2;
		CollisionMask = 1 | 8;

		EnsureCollision();
		EnsureHealth();
		BuildVisual();
		EnsureDamageSmoke();
		OcclusionSilhouette.EnsureOn(this);

		_pilot = GetNodeOrNull<SupportPilotAI>("SupportPilotAI");
		if (_pilot == null)
		{
			_pilot = new SupportPilotAI { Name = "SupportPilotAI" };
			AddChild(_pilot);
		}

		if (_health != null)
			_health.Died += OnDied;
	}

	public void Configure(string unitId, TeamId team)
	{
		UnitId = unitId;
		Team = team;
		_data = SupportCatalog.Get(unitId) ?? _data;
		_alive = true;
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
		EnsureCollision();
		if (_health != null && _data != null)
		{
			_health.ResetHealth(_data.MaxHealth + _data.Armor);
		}
		BuildVisual();
		EnsureDamageSmoke();
		OcclusionSilhouette.EnsureOn(this);
	}

	public Vector3 GetAimPoint() => GlobalPosition + Vector3.Up * (IsStaticUnit ? 1.6f : 0.9f);

	public override void _Process(double delta)
	{
		EnsureDamageSmoke();
		_damageSmoke?.SetHealth(
			_health?.CurrentHealth ?? 0f,
			_health?.MaxHealth ?? 0f,
			IsAlive);
	}

	public void AttachHostReplication()
	{
		SetMultiplayerAuthority(1);
		if (GetNodeOrNull("NetSync") != null)
			return;
		var sync = new MultiplayerSynchronizer { Name = "NetSync" };
		var cfg = new SceneReplicationConfig();
		cfg.AddProperty(new NodePath(":global_position"));
		cfg.AddProperty(new NodePath(":rotation"));
		if (_health != null || GetNodeOrNull("Damageable") != null)
		{
			cfg.AddProperty(new NodePath("Damageable:ReplicatedHealth"));
			cfg.AddProperty(new NodePath("Damageable:MaxHealth"));
		}

		sync.ReplicationConfig = cfg;
		AddChild(sync);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Multiplayer.MultiplayerPeer != null && !Multiplayer.IsServer())
			return;

		if (!IsAlive || _data == null)
		{
			Velocity = Vector3.Zero;
			return;
		}

		var dt = (float)delta;
		_fireCooldown = Mathf.Max(0f, _fireCooldown - dt);

		if (_pilot != null)
		{
			var cmd = _pilot.BuildCommand(dt);
			ApplyCommand(cmd, dt);
		}

		if (!IsStaticUnit)
		{
			if (!IsOnFloor())
				Velocity = new Vector3(Velocity.X, Velocity.Y - 28f * dt, Velocity.Z);
			else if (Velocity.Y < 0f)
				Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
			MoveAndSlide();
		}
		else
		{
			Velocity = Vector3.Zero;
		}
	}

	private void ApplyCommand(SupportPilotCommand cmd, float dt)
	{
		if (_data == null)
			return;

		if (!IsStaticUnit && cmd.Move.LengthSquared() > 0.01f)
		{
			var dir = new Vector3(cmd.Move.X, 0f, cmd.Move.Y);
			if (dir.LengthSquared() > 0.001f)
			{
				dir = dir.Normalized();
				var targetYaw = Mathf.Atan2(-dir.X, -dir.Z);
				Rotation = new Vector3(Rotation.X,
					Mathf.RotateToward(Rotation.Y, targetYaw, Mathf.DegToRad(_data.TurnRateDegrees) * dt),
					Rotation.Z);
				Velocity = new Vector3(dir.X * _data.Speed, Velocity.Y, dir.Z * _data.Speed);
			}
		}
		else if (!IsStaticUnit)
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
		}

		AimTurret(cmd.AimPoint, dt);

		if (cmd.Fire)
			TryFire(cmd.AimPoint);
	}

	private void AimTurret(Vector3 aimPoint, float dt)
	{
		var yawNode = _turret ?? this;
		var to = aimPoint - yawNode.GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 0.01f)
			return;

		var desired = Mathf.Atan2(-to.X, -to.Z);
		if (_turret != null)
		{
			var rate = Mathf.DegToRad((_data?.TurnRateDegrees ?? 90f) * 1.4f) * dt;
			_turret.Rotation = new Vector3(0f, Mathf.RotateToward(_turret.Rotation.Y, desired - Rotation.Y, rate), 0f);
		}
		else if (IsStaticUnit)
		{
			var rate = Mathf.DegToRad((_data?.TurnRateDegrees ?? 90f) * dt);
			Rotation = new Vector3(Rotation.X, Mathf.RotateToward(Rotation.Y, desired, rate), Rotation.Z);
		}
	}

	private void TryFire(Vector3 aimPoint)
	{
		if (_fireCooldown > 0f || _data == null)
			return;
		if (Multiplayer.MultiplayerPeer != null && !Multiplayer.IsServer())
			return;

		_fireCooldown = 1f / Mathf.Max(0.2f, _data.FireRate);
		var muzzle = GetAimPoint();
		var dir = aimPoint - muzzle;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			dir = -GlobalTransform.Basis.Z;
		dir = dir.Normalized();

		var parent = GetTree()?.CurrentScene ?? GetParent();
		if (parent == null)
			return;

		var muzzlePos = muzzle + dir * 0.8f;
		var velocity = dir * _data.ProjectileSpeed;
		var lifetime = _data.Range / Mathf.Max(1f, _data.ProjectileSpeed);
		var style = ProjectileStyleUtil.FromSupport(_data);
		var bus = NetCombatBus.Find(parent);
		if (bus != null && GetTree()?.GetMultiplayer().MultiplayerPeer != null)
		{
			bus.HostSpawnProjectile(
				parent,
				this,
				muzzlePos,
				velocity,
				_data.Damage,
				lifetime,
				Team,
				TargetingMode.Standard,
				-1,
				style,
				gravity: 0f);
		}
		else
		{
			var projectile = Projectile.Create(style);
			projectile.Source = this;
			projectile.SourceTeam = Team;
			projectile.Damage = _data.Damage;
			projectile.Velocity = velocity;
			projectile.Lifetime = lifetime;
			parent.AddChild(projectile);
			projectile.GlobalPosition = muzzlePos;
			projectile.LookAt(projectile.GlobalPosition + dir, Vector3.Up);
		}

		SfxService.Play("weapon_fire", (float)GD.RandRange(1.05, 1.25), -6f);
	}

	private void EnsureCollision()
	{
		var existing = GetNodeOrNull<CollisionShape3D>("Collision");
		var size = _data?.Kind switch
		{
			SupportUnitKind.GunTower => new Vector3(1.4f, 2.0f, 1.4f),
			SupportUnitKind.ScoutBuggy => new Vector3(1.3f, 1.4f, 1.8f),
			_ => new Vector3(1.6f, 1.6f, 2.0f)
		};
		var y = size.Y * 0.5f;

		if (existing == null)
		{
			existing = new CollisionShape3D
			{
				Name = "Collision",
				Position = new Vector3(0f, y, 0f),
				Shape = new BoxShape3D { Size = size }
			};
			AddChild(existing);
			return;
		}

		existing.Position = new Vector3(0f, y, 0f);
		if (existing.Shape is BoxShape3D box)
			box.Size = size;
		else
			existing.Shape = new BoxShape3D { Size = size };
	}

	private void EnsureHealth()
	{
		_health = GetNodeOrNull<Damageable>("Damageable");
		if (_health == null)
		{
			_health = new Damageable { Name = "Damageable" };
			AddChild(_health);
		}

		if (_data != null)
			_health.ResetHealth(_data.MaxHealth + _data.Armor);
	}

	private void BuildVisual()
	{
		if (_visual != null)
			MeshMat.QueueFreeSafe(_visual);
		if (_turret != null)
			MeshMat.QueueFreeSafe(_turret);
		_visual = null;
		_turret = null;
		if (_data == null)
			return;

		var body = TeamBodyColor();
		var mat = MakeMat(body, 0.32f, 0.55f, TeamAccentColor(), 0.35f);
		var dark = MakeMat(body.Darkened(0.28f), 0.4f, 0.5f);
		var accent = MakeMat(TeamAccentColor(), 0.25f, 0.4f, TeamAccentColor(), 0.9f);

		_visual = new Node3D { Name = "Visual" };
		AddChild(_visual);

		switch (_data.Kind)
		{
			case SupportUnitKind.GunTower:
			{
				AddBox(_visual, dark, new Vector3(1.25f, 0.28f, 1.25f) * _data.VisualScale, new Vector3(0f, 0.14f, 0f));
				AddBox(_visual, mat, new Vector3(1.0f, 0.35f, 1.0f) * _data.VisualScale, new Vector3(0f, 0.35f, 0f));
				AddBox(_visual, mat, new Vector3(0.5f, 1.35f, 0.5f) * _data.VisualScale, new Vector3(0f, 1.05f, 0f));
				AddBox(_visual, dark, new Vector3(0.18f, 0.9f, 0.18f), new Vector3(0.45f, 0.7f, 0.45f));
				AddBox(_visual, dark, new Vector3(0.18f, 0.9f, 0.18f), new Vector3(-0.45f, 0.7f, 0.45f));
				_turret = new Node3D { Name = "Turret", Position = new Vector3(0f, 1.6f, 0f) };
				_visual.AddChild(_turret);
				AddBox(_turret, mat, new Vector3(0.75f, 0.32f, 0.75f), Vector3.Zero);
				AddBox(_turret, dark, new Vector3(0.2f, 0.2f, 1.0f), new Vector3(0f, 0.05f, -0.5f));
				AddBox(_turret, accent, new Vector3(0.12f, 0.12f, 0.12f), new Vector3(0f, 0.05f, -1.0f));
				break;
			}
			case SupportUnitKind.ScoutBuggy:
			{
				AddBox(_visual, mat, new Vector3(0.95f, 0.32f, 1.35f) * _data.VisualScale, new Vector3(0f, 0.48f, 0f));
				AddBox(_visual, dark, new Vector3(0.7f, 0.18f, 0.7f), new Vector3(0f, 0.7f, 0.15f));
				AddBox(_visual, accent, new Vector3(0.45f, 0.12f, 0.08f), new Vector3(0f, 0.78f, -0.15f));
				AddWheel(_visual, dark, new Vector3(-0.5f, 0.22f, -0.45f));
				AddWheel(_visual, dark, new Vector3(0.5f, 0.22f, -0.45f));
				AddWheel(_visual, dark, new Vector3(-0.5f, 0.22f, 0.5f));
				AddWheel(_visual, dark, new Vector3(0.5f, 0.22f, 0.5f));
				_turret = new Node3D { Name = "Turret", Position = new Vector3(0f, 0.88f, -0.2f) };
				_visual.AddChild(_turret);
				AddBox(_turret, mat, new Vector3(0.16f, 0.12f, 0.6f), new Vector3(0f, 0f, -0.2f));
				AddBox(_turret, accent, new Vector3(0.08f, 0.08f, 0.1f), new Vector3(0f, 0f, -0.52f));
				break;
			}
			default:
			{
				AddBox(_visual, mat, new Vector3(1.35f, 0.5f, 1.7f) * _data.VisualScale, new Vector3(0f, 0.48f, 0f));
				AddBox(_visual, dark, new Vector3(1.1f, 0.12f, 1.3f), new Vector3(0f, 0.72f, 0f));
				AddBox(_visual, mat, new Vector3(0.85f, 0.32f, 0.85f), new Vector3(0f, 0.88f, 0.05f));
				AddBox(_visual, accent, new Vector3(0.4f, 0.1f, 0.08f), new Vector3(0f, 0.95f, -0.35f));
				AddWheel(_visual, dark, new Vector3(-0.7f, 0.22f, -0.55f));
				AddWheel(_visual, dark, new Vector3(0.7f, 0.22f, -0.55f));
				AddWheel(_visual, dark, new Vector3(-0.7f, 0.22f, 0.55f));
				AddWheel(_visual, dark, new Vector3(0.7f, 0.22f, 0.55f));
				_turret = new Node3D { Name = "Turret", Position = new Vector3(0f, 1.05f, 0f) };
				_visual.AddChild(_turret);
				AddBox(_turret, mat, new Vector3(0.58f, 0.28f, 0.58f), Vector3.Zero);
				AddBox(_turret, dark, new Vector3(0.16f, 0.16f, 0.95f), new Vector3(0f, 0.02f, -0.45f));
				AddBox(_turret, accent, new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0f, 0.02f, -0.95f));
				break;
			}
		}
	}

	private void EnsureDamageSmoke()
	{
		if (_damageSmoke != null && GodotObject.IsInstanceValid(_damageSmoke))
		{
			_damageSmoke.Position = new Vector3(0f, IsStaticUnit ? 1.5f : 0.9f, 0f);
			return;
		}

		_damageSmoke = DamageSmoke.Create(IsStaticUnit ? 1.25f : 1f);
		_damageSmoke.Position = new Vector3(0f, IsStaticUnit ? 1.5f : 0.9f, 0f);
		AddChild(_damageSmoke);
	}

	private static StandardMaterial3D MakeMat(
		Color albedo,
		float metallic,
		float roughness,
		Color? emission = null,
		float emissionEnergy = 0f)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = albedo,
			Metallic = metallic,
			Roughness = roughness
		};
		if (emission.HasValue && emissionEnergy > 0f)
		{
			mat.EmissionEnabled = true;
			mat.Emission = emission.Value;
			mat.EmissionEnergyMultiplier = emissionEnergy;
		}
		return mat;
	}

	private Color TeamBodyColor()
	{
		// Clear team read first; slight kind shift so tanks/towers/buggies aren't identical.
		var baseColor = Team == TeamId.Player
			? new Color(0.28f, 0.52f, 0.92f)
			: new Color(0.88f, 0.22f, 0.18f);
		return _data?.Kind switch
		{
			SupportUnitKind.GunTower => baseColor.Darkened(0.12f),
			SupportUnitKind.ScoutBuggy => baseColor.Lightened(0.1f),
			_ => baseColor
		};
	}

	private Color TeamAccentColor() =>
		Team == TeamId.Player
			? new Color(0.35f, 0.65f, 1f)
			: new Color(1f, 0.3f, 0.22f);

	private static void AddBox(Node3D parent, Material mat, Vector3 size, Vector3 position)
	{
		parent.AddChild(MeshMat.Make(new BoxMesh { Size = size }, mat, position));
	}

	private static void AddWheel(Node3D parent, Material mat, Vector3 position)
	{
		parent.AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.18f },
			mat,
			position,
			Vector3.Forward * Mathf.Tau * 0.25f));
	}

	private void OnDied()
	{
		_alive = false;
		Velocity = Vector3.Zero;
		_damageSmoke?.Stop();
		ProcessMode = ProcessModeEnum.Disabled;
		Visible = false;

		if (Team == TeamId.Enemy && _data != null)
		{
			var parent = GetTree()?.CurrentScene ?? GetParent();
			if (parent != null)
			{
				var session = GetNodeOrNull<GameSession>("/root/GameSession");
				var maxTier = session?.CurrentMaxLootTier() ?? 1;
				LootService.SpawnWorldDrops(
					parent,
					GlobalPosition,
					LootService.ScrapForSupport(_data.Kind),
					LootService.RollSupportPartDrop(maxTier));
			}
		}

		GD.Print($"{Name} ({_data?.DisplayName}) destroyed.");
	}

	public static SupportUnit Create(string unitId, TeamId team, string? name = null)
	{
		SupportCatalog.EnsureBuilt();
		var unit = new SupportUnit
		{
			Name = name ?? unitId,
			UnitId = unitId,
			Team = team
		};
		return unit;
	}
}
