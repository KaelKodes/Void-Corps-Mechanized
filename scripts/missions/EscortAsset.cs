using Godot;

namespace Mechanize;

/// <summary>
/// Allied mining rig. Advances toward a destination only while the player stays nearby;
/// can hold position while filling cargo.
/// </summary>
public partial class EscortAsset : CharacterBody3D
{
	public float MaxHealth { get; private set; } = 280f;
	public float MoveSpeed { get; set; } = 2.2f;
	public float EscortRadius { get; set; } = 16f;

	private Damageable? _health;
	private Vector3 _destination;
	private MechController? _escort;
	private bool _arrived;
	private bool _dead;
	private bool _hold;
	private float _cargoFill;

	private Vector3 _lastPos;
	private float _stuckTimer;
	private float _strafeSign = 1f;
	private float _detourTimer;
	private Vector3 _detourDir = Vector3.Forward;
	private readonly RandomNumberGenerator _rng = new();

	public bool HasArrived => _arrived;
	public bool IsHolding => _hold;
	public bool IsDestroyed => _dead || (_health?.IsDead ?? false);
	public float HealthRatio => _health == null || _health.MaxHealth <= 0f
		? 0f
		: _health.CurrentHealth / _health.MaxHealth;
	public float CargoFill => _cargoFill;
	public bool IsEscorted =>
		_escort != null
		&& GodotObject.IsInstanceValid(_escort)
		&& _escort.Integrity?.IsCollapsed != true
		&& _escort.GlobalPosition.DistanceTo(GlobalPosition) <= EscortRadius;

	public static EscortAsset Create(Vector3 start, Vector3 destination)
	{
		var asset = new EscortAsset
		{
			Name = "MiningRig",
			Position = start,
			_destination = destination,
			CollisionLayer = 2,
			CollisionMask = 1 | 8
		};
		asset.Build();
		return asset;
	}

	public void SetEscort(MechController? mech) => _escort = mech;

	public void SetDestination(Vector3 destination)
	{
		_destination = destination with { Y = 0f };
		_arrived = false;
		_hold = false;
	}

	public void SetHold(bool hold)
	{
		_hold = hold;
		if (hold)
		{
			_arrived = true;
			Velocity = Vector3.Zero;
		}
	}

	public void SetCargoFill(float fill01) => _cargoFill = Mathf.Clamp(fill01, 0f, 1f);

	private void Build()
	{
		_rng.Randomize();
		_strafeSign = _rng.Randf() > 0.5f ? 1f : -1f;
		FloorSnapLength = 0.4f;
		FloorMaxAngle = Mathf.DegToRad(50f);

		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.48f, 0.32f),
			Roughness = 0.65f,
			Metallic = 0.3f
		};
		var dark = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.32f, 0.28f, 0.22f),
			Roughness = 0.55f,
			Metallic = 0.4f
		};
		var glass = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.85f, 0.65f, 0.25f),
			EmissionEnabled = true,
			Emission = new Color(0.85f, 0.55f, 0.15f),
			EmissionEnergyMultiplier = 0.75f,
			Roughness = 0.3f
		};
		var drill = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.4f, 0.42f, 0.45f),
			Roughness = 0.4f,
			Metallic = 0.7f
		};

		AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(2.4f, 1.0f, 3.6f) }, mat, new Vector3(0f, 0.75f, 0f)));
		AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(2.1f, 0.2f, 2.0f) }, dark, new Vector3(0f, 1.3f, -0.5f)));
		AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(1.5f, 0.95f, 1.35f) }, mat, new Vector3(0f, 1.55f, 0.75f)));
		AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(1.15f, 0.35f, 0.08f) }, glass, new Vector3(0f, 1.7f, 1.4f)));
		// Drill mast / bit — reads as a mining rig, not a soft crawler.
		AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.35f, 2.2f, 0.35f) }, drill, new Vector3(0f, 2.2f, -1.1f)));
		AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.55f, Height = 0.7f },
			drill,
			new Vector3(0f, 0.55f, -1.1f)));
		AddWheel(dark, new Vector3(-1.1f, 0.35f, -1.2f));
		AddWheel(dark, new Vector3(1.1f, 0.35f, -1.2f));
		AddWheel(dark, new Vector3(-1.1f, 0.35f, 1.2f));
		AddWheel(dark, new Vector3(1.1f, 0.35f, 1.2f));

		AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(2.4f, 1.4f, 3.6f) },
			Position = new Vector3(0f, 0.75f, 0f)
		});

		_health = new Damageable { Name = "Damageable", MaxHealth = MaxHealth };
		AddChild(_health);
		_health.ResetHealth(MaxHealth);
		_health.Died += () =>
		{
			_dead = true;
			Velocity = Vector3.Zero;
			ProcessMode = ProcessModeEnum.Disabled;
			Visible = false;
		};

		var label = new Label3D
		{
			Name = "Label",
			Text = "MINING RIG",
			Position = new Vector3(0f, 3.2f, 0f),
			FontSize = 40,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(0.95f, 0.78f, 0.4f)
		};
		AddChild(label);
	}

	private void AddWheel(Material mat, Vector3 position)
	{
		AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 0.28f },
			mat,
			position,
			Vector3.Forward * Mathf.Tau * 0.25f));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead || _health?.IsDead == true)
		{
			Velocity = Vector3.Zero;
			return;
		}

		var dt = (float)delta;
		UpdateLabel();

		if (_hold || _arrived)
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		var toGoal = _destination - GlobalPosition;
		toGoal.Y = 0f;
		if (toGoal.Length() < 3.5f)
		{
			_arrived = true;
			Velocity = Vector3.Zero;
			return;
		}

		if (!IsEscorted)
		{
			_stuckTimer = 0f;
			_lastPos = GlobalPosition;
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		var preferred = toGoal.Normalized();
		UpdateStuck(dt);
		_detourTimer = Mathf.Max(0f, _detourTimer - dt);

		Vector3 moveDir;
		if (_detourTimer > 0f)
			moveDir = BlendTowardGoal(_detourDir, preferred, 0.35f);
		else
			moveDir = SteerAroundObstacles(preferred);

		var targetYaw = Mathf.Atan2(-moveDir.X, -moveDir.Z);
		Rotation = new Vector3(Rotation.X, Mathf.RotateToward(Rotation.Y, targetYaw, 2.2f * dt), Rotation.Z);
		Velocity = new Vector3(moveDir.X * MoveSpeed, Velocity.Y, moveDir.Z * MoveSpeed);
		ApplyGravity(dt);
		MoveAndSlide();

		if (GetSlideCollisionCount() > 0 && _detourTimer <= 0f)
		{
			var hit = GetSlideCollision(0);
			var normal = hit.GetNormal();
			normal.Y = 0f;
			if (normal.LengthSquared() > 0.01f)
			{
				normal = normal.Normalized();
				var along = normal.Cross(Vector3.Up).Normalized() * _strafeSign;
				_detourDir = (along * 1.4f + normal * 0.55f + preferred * 0.2f).Normalized();
				_detourTimer = _rng.RandfRange(0.7f, 1.4f);
			}
		}
	}

	private void UpdateLabel()
	{
		var label = GetNodeOrNull<Label3D>("Label");
		if (label == null || _health == null)
			return;

		var hp = $"{Mathf.CeilToInt(_health.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";
		if (_hold)
			label.Text = $"MINING  {Mathf.RoundToInt(_cargoFill * 100f)}%  ·  {hp}";
		else
			label.Text = $"MINING RIG  {hp}";
	}

	private void ApplyGravity(float dt)
	{
		if (!IsOnFloor())
			Velocity = new Vector3(Velocity.X, Velocity.Y - 28f * dt, Velocity.Z);
		else if (Velocity.Y < 0f)
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
	}

	private void UpdateStuck(float dt)
	{
		var moved = GlobalPosition.DistanceTo(_lastPos);
		_lastPos = GlobalPosition;
		var speed = moved / Mathf.Max(dt, 0.001f);
		if (speed < 0.35f)
			_stuckTimer += dt;
		else
			_stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 0.6f);

		if (_stuckTimer < 0.85f)
			return;

		_stuckTimer = 0f;
		_strafeSign *= -1f;
		var preferred = _destination - GlobalPosition;
		preferred.Y = 0f;
		if (preferred.LengthSquared() > 0.01f)
			preferred = preferred.Normalized();
		else
			preferred = -GlobalTransform.Basis.Z;

		_detourDir = PickOpenDirection(preferred, forceOpposite: true);
		_detourTimer = _rng.RandfRange(1.1f, 2.0f);
	}

	private Vector3 SteerAroundObstacles(Vector3 preferred)
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return preferred;

		var origin = GlobalPosition + Vector3.Up * 0.9f;
		var probeDist = 4.5f;
		var ahead = PhysicsRayQueryParameters3D.Create(origin, origin + preferred * probeDist);
		ahead.CollisionMask = 1 | 8;
		ahead.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var block = space.IntersectRay(ahead);
		if (block.Count == 0)
			return preferred;

		return PickOpenDirection(preferred, forceOpposite: false);
	}

	private Vector3 PickOpenDirection(Vector3 preferred, bool forceOpposite)
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return preferred;

		var origin = GlobalPosition + Vector3.Up * 0.9f;
		var lateral = preferred.Cross(Vector3.Up);
		if (lateral.LengthSquared() < 0.001f)
			lateral = Vector3.Right;
		lateral = lateral.Normalized() * (forceOpposite ? -_strafeSign : _strafeSign);

		Vector3 best = lateral;
		var bestScore = float.MinValue;
		for (var i = 0; i < 14; i++)
		{
			var ang = i * Mathf.Tau / 14f;
			var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
			var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir * 6.5f);
			query.CollisionMask = 1 | 8;
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var hit = space.IntersectRay(query);
			var clear = hit.Count == 0 ? 6.5f : origin.DistanceTo(hit["position"].AsVector3());
			var toward = dir.Dot(preferred);
			var flank = dir.Dot(lateral);
			var score = clear * 1.6f + toward * 2.4f + flank * 2.0f;
			if (score > bestScore)
			{
				bestScore = score;
				best = dir;
			}
		}

		return best.LengthSquared() > 0.01f ? best.Normalized() : preferred;
	}

	private static Vector3 BlendTowardGoal(Vector3 detour, Vector3 preferred, float preferredWeight)
	{
		var blended = detour + preferred * preferredWeight;
		blended.Y = 0f;
		return blended.LengthSquared() > 0.01f ? blended.Normalized() : detour;
	}
}
