using Godot;

namespace Mechanize;

/// <summary>
/// Sabotage corridor emplacement. Automates B→A hellfire; goes precise and aggressive when damaged.
/// </summary>
public partial class HellfireTurret : StaticBody3D
{
	public const float DefaultMaxHealth = 95f;
	public const float MuzzleHeight = 0.72f;

	public TeamId Team { get; private set; } = TeamId.Enemy;
	public bool IsAlive => !_destroyed && (_health == null || !_health.IsDead);
	public bool IsAggro => IsAlive && _aggroTimer > 0f;
	public float AggroIntensity => IsAggro ? Mathf.Clamp(_aggroTimer / 4.5f, 0.35f, 1f) : 0f;

	private Damageable? _health;
	private Node3D? _yawPivot;
	private Node3D? _barrel;
	private MeshInstance3D? _statusGlow;
	private StandardMaterial3D? _glowMat;
	private Label3D? _label;
	private bool _destroyed;
	private float _aggroTimer;
	private float _directFireCooldown;
	private float _faceYaw;
	private Color _idleGlow = new(0.95f, 0.45f, 0.2f);
	private Color _aggroGlow = new(1f, 0.2f, 0.15f);

	public static HellfireTurret Create(string name, Vector3 position, float faceYawDegrees = 0f)
	{
		var turret = new HellfireTurret
		{
			Name = name,
			Position = position,
			CollisionLayer = PhysicsLayers.Mechs,
			CollisionMask = 0
		};
		turret._faceYaw = Mathf.DegToRad(faceYawDegrees);
		turret.Build();
		return turret;
	}

	public Vector3 GetMuzzleWorld()
	{
		if (_barrel != null && GodotObject.IsInstanceValid(_barrel))
			return _barrel.GlobalPosition + (-_barrel.GlobalTransform.Basis.Z) * 0.55f;
		return GlobalPosition + new Vector3(0f, MuzzleHeight, 0f);
	}

	public void NotifyDamaged()
	{
		if (!IsAlive)
			return;
		// Taking fire wakes the battery — longer aggro stacks with repeated hits.
		_aggroTimer = Mathf.Min(7.5f, _aggroTimer + 2.8f);
		_directFireCooldown = Mathf.Min(_directFireCooldown, 0.12f);
		RefreshStatus();
	}

	/// <summary>
	/// Face default southbound lane, or snap toward the attacker when aggroed.
	/// Returns true when a direct-fire shot should spawn this frame.
	/// </summary>
	public bool TickAggro(float dt, Vector3 playerPos)
	{
		if (!IsAlive)
			return false;

		_aggroTimer = Mathf.Max(0f, _aggroTimer - dt);
		_directFireCooldown = Mathf.Max(0f, _directFireCooldown - dt);
		RefreshStatus();

		var wantYaw = _faceYaw;
		if (IsAggro)
		{
			var toPlayer = playerPos - GlobalPosition;
			toPlayer.Y = 0f;
			if (toPlayer.LengthSquared() > 0.01f)
				wantYaw = Mathf.Atan2(toPlayer.X, toPlayer.Z);
		}

		if (_yawPivot != null)
		{
			var current = _yawPivot.Rotation.Y;
			var next = Mathf.RotateToward(current, wantYaw, dt * (IsAggro ? 3.4f : 1.6f));
			_yawPivot.Rotation = new Vector3(0f, next, 0f);
		}

		if (!IsAggro || _directFireCooldown > 0f)
			return false;

		var dist = new Vector3(playerPos.X - GlobalPosition.X, 0f, playerPos.Z - GlobalPosition.Z).Length();
		if (dist > 78f)
			return false;

		_directFireCooldown = Mathf.Lerp(0.55f, 0.28f, AggroIntensity);
		return true;
	}

	private void Build()
	{
		_health = new Damageable { Name = "Damageable", MaxHealth = DefaultMaxHealth };
		AddChild(_health);
		_health.ResetHealth(DefaultMaxHealth);
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;

		var collision = new CollisionShape3D
		{
			Name = "Collision",
			Shape = new BoxShape3D { Size = new Vector3(2.2f, 2.4f, 2.2f) },
			Position = new Vector3(0f, 1.2f, 0f)
		};
		AddChild(collision);

		var steel = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.22f, 0.24f, 0.28f));
		var accent = SurfaceLibrary.Flat(
			new Color(0.55f, 0.18f, 0.12f),
			metallic: 0.4f,
			roughness: 0.45f,
			emission: _idleGlow,
			emissionEnergy: 0.55f);
		_glowMat = accent;

		var baseMesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(2.1f, 0.55f, 2.1f) },
			Position = new Vector3(0f, 0.28f, 0f),
			MaterialOverride = steel
		};
		AddChild(baseMesh);

		var pedestal = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.7f, Height = 0.9f, RadialSegments = 12 },
			Position = new Vector3(0f, 0.95f, 0f),
			MaterialOverride = steel
		};
		AddChild(pedestal);

		_yawPivot = new Node3D { Name = "Yaw", Position = new Vector3(0f, 1.35f, 0f) };
		_yawPivot.Rotation = new Vector3(0f, _faceYaw, 0f);
		AddChild(_yawPivot);

		var housing = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(1.35f, 0.7f, 1.1f) },
			Position = new Vector3(0f, 0.15f, 0f),
			MaterialOverride = steel
		};
		_yawPivot.AddChild(housing);

		_statusGlow = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.12f, 0.2f) },
			Position = new Vector3(0f, 0.45f, -0.4f),
			MaterialOverride = accent
		};
		_yawPivot.AddChild(_statusGlow);

		_barrel = new Node3D { Name = "Barrel", Position = new Vector3(0f, 0.12f, -0.55f) };
		_yawPivot.AddChild(_barrel);
		var barrelMesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.28f, 1.35f) },
			Position = new Vector3(0f, 0f, -0.55f),
			MaterialOverride = accent
		};
		_barrel.AddChild(barrelMesh);

		_label = new Label3D
		{
			Text = "HELLFIRE",
			Position = new Vector3(0f, 2.55f, 0f),
			FontSize = 36,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = _idleGlow
		};
		AddChild(_label);
		OcclusionSilhouette.EnsureOn(this);
	}

	private void OnDamaged(float amount, float remaining)
	{
		NotifyDamaged();
	}

	private void OnDied()
	{
		_destroyed = true;
		_aggroTimer = 0f;
		CollisionLayer = 0;
		if (_label != null)
		{
			_label.Text = "OFFLINE";
			_label.Modulate = new Color(0.45f, 0.45f, 0.5f);
		}

		if (_glowMat != null)
		{
			_glowMat.Emission = new Color(0.2f, 0.2f, 0.22f);
			_glowMat.EmissionEnergyMultiplier = 0.1f;
		}

		// Soft collapse — leave the wreck as lane cover silhouette.
		Scale = new Vector3(1f, 0.55f, 1f);
		SfxService.PlayImpactArmor(GlobalPosition, -4f);
	}

	private void RefreshStatus()
	{
		if (_destroyed || _label == null || _glowMat == null)
			return;

		if (IsAggro)
		{
			_label.Text = "RETURN FIRE";
			_label.Modulate = _aggroGlow;
			_glowMat.Emission = _aggroGlow;
			_glowMat.EmissionEnergyMultiplier = 1.4f + AggroIntensity;
		}
		else
		{
			_label.Text = "HELLFIRE";
			_label.Modulate = _idleGlow;
			_glowMat.Emission = _idleGlow;
			_glowMat.EmissionEnergyMultiplier = 0.55f;
		}
	}
}
