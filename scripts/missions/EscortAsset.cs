using Godot;

namespace Mechanize;

/// <summary>
/// Allied mining rig. Advances toward a destination only while the player stays nearby;
/// can hold position while filling cargo.
/// </summary>
public partial class EscortAsset : CharacterBody3D
{
	public float MaxHealth { get; private set; } = 280f;
	public float MoveSpeed { get; set; } = 2.4f;
	public float EscortRadius { get; set; } = 16f;
	/// <summary>How close the pilot must be to hop onto the rig deck.</summary>
	public float MountRadius { get; set; } = 7f;
	/// <summary>Speed multiplier while a mounted MAP diverts sprint power into the drivetrain.</summary>
	public float OverdriveMultiplier { get; set; } = 1.35f;

	private Damageable? _health;
	private Vector3 _destination;
	private MechController? _escort;
	private bool _arrived;
	private bool _dead;
	private bool _hold;
	private float _cargoFill;
	private bool _overdrive;

	private Vector3 _lastPos;
	private float _stuckTimer;
	private float _strafeSign = 1f;
	/// <summary>Wall-follow: stay on one flank until the goal lane reopens for a sustained stretch.</summary>
	private bool _wallFollow;
	private float _wallFollowAge;
	private float _goalClearTimer;
	private Vector3 _followDir = Vector3.Forward;
	private Vector3 _wallNormal = Vector3.Right;
	private float _progressAtFollowStart;
	private float _reverseTimer;
	private readonly RandomNumberGenerator _rng = new();
	private string _companyRigTag = "";

	public bool HasArrived => _arrived;
	public bool IsHolding => _hold;
	public bool CanReceiveOverdrive => !_dead && !_hold && !_arrived;
	public bool IsOverdriveActive => _overdrive && CanReceiveOverdrive;
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
		ExitWallFollow();
		_stuckTimer = 0f;
		_reverseTimer = 0f;
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

	public void SetOverdrive(bool active) => _overdrive = active && CanReceiveOverdrive;

	/// <summary>Deck anchor the pilot pins to while riding.</summary>
	public Node3D? MountPoint => GetNodeOrNull<Node3D>("MountPoint");

	public bool CanMountFrom(Vector3 worldPos) =>
		!_dead
		&& _health?.IsDead != true
		&& MountPoint != null
		&& worldPos.DistanceTo(GlobalPosition) <= MountRadius;

	/// <summary>A ground-level spot clear of walls beside/behind the rig to drop the pilot on dismount.</summary>
	public Vector3 SafeDismountPosition()
	{
		var right = GlobalTransform.Basis.X;
		right.Y = 0f;
		right = right.LengthSquared() < 0.01f ? Vector3.Right : right.Normalized();
		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.LengthSquared() < 0.01f ? Vector3.Forward : forward.Normalized();

		foreach (var dir in new[] { right, -right, -forward, forward })
		{
			if (ProbeClearanceFrom(GlobalPosition + Vector3.Up * 0.9f, dir, 5.5f) >= 4.6f)
				return (GlobalPosition + dir * 4.2f) with { Y = 0f };
		}

		return (GlobalPosition + right * 4.2f) with { Y = 0f };
	}

	private void Build()
	{
		_rng.Randomize();
		_strafeSign = _rng.Randf() > 0.5f ? 1f : -1f;
		FloorSnapLength = 0.4f;
		FloorMaxAngle = Mathf.DegToRad(50f);

		var mat = SurfaceLibrary.Get(SurfaceLibrary.Kind.PaintedMetal, new Color(0.55f, 0.48f, 0.32f));
		var dark = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.32f, 0.28f, 0.22f));
		var glass = SurfaceLibrary.Flat(
			new Color(0.85f, 0.65f, 0.25f),
			metallic: 0.1f,
			roughness: 0.3f,
			emission: new Color(0.85f, 0.55f, 0.15f),
			emissionEnergy: 0.75f);
		var drill = SurfaceLibrary.Get(SurfaceLibrary.Kind.Steel, new Color(0.4f, 0.42f, 0.45f));

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

		// Rider deck + mount anchor — the pilot pins their feet to this node when mounted.
		AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(2.2f, 0.16f, 2.4f) }, drill, new Vector3(0f, 1.3f, 0.25f)));
		AddChild(new Node3D { Name = "MountPoint", Position = new Vector3(0f, 1.34f, 0.25f) });

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
		OcclusionSilhouette.EnsureOn(this);
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
			_reverseTimer = 0f;
			ExitWallFollow();
			_lastPos = GlobalPosition;
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		var preferred = toGoal.Normalized();
		UpdateStuck(dt);
		var moveDir = ChooseMoveDir(preferred, dt);

		var targetYaw = Mathf.Atan2(-moveDir.X, -moveDir.Z);
		var turnRate = _wallFollow || _reverseTimer > 0f ? 1.6f : 2.6f;
		Rotation = new Vector3(Rotation.X, Mathf.RotateToward(Rotation.Y, targetYaw, turnRate * dt), Rotation.Z);
		var speed = MoveSpeed * (IsOverdriveActive ? OverdriveMultiplier : 1f);
		Velocity = new Vector3(moveDir.X * speed, Velocity.Y, moveDir.Z * speed);
		ApplyGravity(dt);
		MoveAndSlide();

		if (GetSlideCollisionCount() > 0)
			NoteWallFromSlide(preferred);
	}

	private void UpdateLabel()
	{
		var label = GetNodeOrNull<Label3D>("Label");
		if (label == null || _health == null)
			return;
		if (string.IsNullOrEmpty(_companyRigTag))
		{
			var company = GetNodeOrNull<GameSession>("/root/GameSession")?.SolarCampaign.SelectedCompany;
			_companyRigTag = company == null ? "MINING RIG" : $"{company.ShortName.ToUpperInvariant()} RIG";
		}

		var hp = $"{Mathf.CeilToInt(_health.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";
		if (_hold)
			label.Text = $"{_companyRigTag}  ·  MINING {Mathf.RoundToInt(_cargoFill * 100f)}%  ·  {hp}";
		else if (IsOverdriveActive)
			label.Text = $"{_companyRigTag}  ·  OVERDRIVE  ·  {hp}";
		else
			label.Text = $"{_companyRigTag}  {hp}";
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
		if (speed < 0.22f)
			_stuckTimer += dt;
		else
			_stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 1.2f);

		if (_stuckTimer < 1.8f)
			return;

		_stuckTimer = 0f;
		// Boxed in: reverse a beat, then flip flank and resume wall-follow.
		_reverseTimer = 1.1f;
		BeginWallFollow(FlatDirToGoal(), forceOpposite: true);
	}

	private Vector3 ChooseMoveDir(Vector3 preferred, float dt)
	{
		if (_reverseTimer > 0f)
		{
			_reverseTimer -= dt;
			var back = (-preferred + FlatCross(preferred) * _strafeSign * 0.55f);
			back.Y = 0f;
			if (back.LengthSquared() > 0.01f)
				return back.Normalized();
			return -preferred;
		}

		if (_wallFollow)
		{
			_wallFollowAge += dt;
			return TickWallFollow(preferred, dt);
		}

		// Need a long clear lane before trusting "direct" — short gaps still trap the rig.
		if (IsPathClear(preferred, 8f))
			return preferred;

		BeginWallFollow(preferred, forceOpposite: false);
		return _followDir;
	}

	private Vector3 TickWallFollow(Vector3 preferred, float dt)
	{
		// Stay on the wall until the goal has been openly clear for a while —
		// not just one lucky frame between two crates.
		if (IsPathClear(preferred, 9f))
			_goalClearTimer += dt;
		else
			_goalClearTimer = 0f;

		var madeProgress = ProgressToGoal() + 2.5f < _progressAtFollowStart;
		var heldLongEnough = _wallFollowAge >= 3.5f;
		if (_goalClearTimer >= 0.85f && heldLongEnough && (madeProgress || _wallFollowAge >= 7f))
		{
			ExitWallFollow();
			return preferred;
		}

		// Hard cap so a dead alley eventually re-evaluates.
		if (_wallFollowAge > 14f)
		{
			_strafeSign *= -1f;
			BeginWallFollow(preferred, forceOpposite: true);
		}

		var along = FlatCross(_wallNormal) * _strafeSign;
		if (along.LengthSquared() < 0.01f)
			along = FlatCross(preferred) * _strafeSign;

		// Corner: follow lane blocked → rotate around the obstacle on the same side.
		if (!IsPathClear(along, 2.4f))
		{
			var corner = (-_wallNormal * 0.15f + along * 0.35f - preferred * 0.1f);
			// Probe for a free tangent around the corner (±45° / ±90° on the committed side).
			var best = along;
			var bestClear = 0f;
			foreach (var ang in new[] { 0.4f, 0.8f, 1.2f, 1.6f, -0.35f })
			{
				var dir = RotateYaw(along, ang * _strafeSign);
				var clear = ProbeClearance(dir, 5f);
				if (clear > bestClear)
				{
					bestClear = clear;
					best = dir;
				}
			}

			if (bestClear > 1.2f)
				along = best;
			else
			{
				// Hug outward off the wall then resume tangent.
				along = (_wallNormal * 0.7f + along).Normalized();
			}
		}
		else
		{
			// Keep light contact with the wall so we don't peel off into the wrong corridor.
			var hug = along + _wallNormal * 0.12f;
			hug.Y = 0f;
			if (hug.LengthSquared() > 0.01f)
				along = hug.Normalized();
		}

		_followDir = along;
		return along;
	}

	private void NoteWallFromSlide(Vector3 preferred)
	{
		var hit = GetSlideCollision(0);
		var normal = hit.GetNormal();
		normal.Y = 0f;
		if (normal.LengthSquared() < 0.01f)
			return;

		normal = normal.Normalized();
		// Smooth the remembered wall so chatter between faces doesn't flip us.
		_wallNormal = _wallFollow
			? (_wallNormal * 0.65f + normal * 0.35f).Normalized()
			: normal;

		if (!_wallFollow)
			BeginWallFollow(preferred, forceOpposite: false);
		else
		{
			var along = FlatCross(_wallNormal) * _strafeSign;
			if (along.LengthSquared() > 0.01f)
				_followDir = (along * 1.5f + _wallNormal * 0.35f).Normalized();
		}
	}

	private void BeginWallFollow(Vector3 preferred, bool forceOpposite)
	{
		var side = PickFlankSide(preferred, forceOpposite);
		_strafeSign = side;
		_wallFollow = true;
		_wallFollowAge = 0f;
		_goalClearTimer = 0f;
		_progressAtFollowStart = ProgressToGoal();

		if (_wallNormal.LengthSquared() < 0.01f)
			_wallNormal = -preferred;

		var along = FlatCross(_wallNormal) * _strafeSign;
		if (along.LengthSquared() < 0.01f || ProbeClearance(along, 3f) < 1.5f)
			along = FlatCross(preferred) * _strafeSign;

		_followDir = (along * 1.4f + _wallNormal * 0.3f).Normalized();
	}

	private void ExitWallFollow()
	{
		_wallFollow = false;
		_wallFollowAge = 0f;
		_goalClearTimer = 0f;
	}

	/// <summary>
	/// Score each flank by walking several virtual steps along it and asking
	/// "from here, can I see the goal?" — picks the corridor that opens, not the one-step nudge.
	/// </summary>
	private float PickFlankSide(Vector3 preferred, bool forceOpposite)
	{
		var right = FlatCross(preferred);
		float ScoreSide(float sign)
		{
			var along = right * sign;
			var score = 0f;
			var cursor = GlobalPosition + Vector3.Up * 0.9f;
			const float step = 3.2f;
			const int steps = 6; // ~19m of look-ahead at crawler scale
			for (var i = 0; i < steps; i++)
			{
				var stepClear = ProbeClearanceFrom(cursor, along, step + 0.5f);
				if (stepClear < 1.1f)
				{
					// Dead alley — still credit earlier progress, then stop.
					score += i * 0.4f;
					break;
				}

				cursor += along * Mathf.Min(step, stepClear - 0.3f);
				score += stepClear * 0.35f;

				// From this offset, how open is the path toward the goal?
				var toGoal = _destination - cursor;
				toGoal.Y = 0f;
				if (toGoal.LengthSquared() < 0.01f)
				{
					score += 40f;
					break;
				}

				var goalDir = toGoal.Normalized();
				var goalClear = ProbeClearanceFrom(cursor, goalDir, 12f);
				score += goalClear * 1.8f;
				if (goalClear > 10f)
				{
					score += 25f + (steps - i) * 3f; // earlier opening wins
					break;
				}
			}

			return score;
		}

		var leftScore = ScoreSide(-1f);
		var rightScore = ScoreSide(1f);

		if (forceOpposite)
			return _strafeSign > 0f ? -1f : 1f;

		// Strong hysteresis: keep current side unless the other is clearly better.
		if (_wallFollow)
		{
			var current = _strafeSign > 0f ? rightScore : leftScore;
			var other = _strafeSign > 0f ? leftScore : rightScore;
			if (other > current + 12f)
				return -_strafeSign;
			return _strafeSign;
		}

		return rightScore >= leftScore ? 1f : -1f;
	}

	private float ProgressToGoal()
	{
		var d = _destination - GlobalPosition;
		d.Y = 0f;
		return d.Length();
	}

	private bool IsPathClear(Vector3 dir, float distance)
	{
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			return true;
		dir = dir.Normalized();

		var right = FlatCross(dir);
		var origins = new[]
		{
			GlobalPosition + Vector3.Up * 0.9f,
			GlobalPosition + Vector3.Up * 0.9f + right * 1.15f,
			GlobalPosition + Vector3.Up * 0.9f - right * 1.15f
		};

		foreach (var origin in origins)
		{
			if (ProbeClearanceFrom(origin, dir, distance) < distance * 0.9f)
				return false;
		}

		return true;
	}

	private float ProbeClearance(Vector3 dir, float distance)
	{
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			return distance;
		dir = dir.Normalized();
		var right = FlatCross(dir);
		var c = ProbeClearanceFrom(GlobalPosition + Vector3.Up * 0.9f, dir, distance);
		var r = ProbeClearanceFrom(GlobalPosition + Vector3.Up * 0.9f + right * 1.15f, dir, distance);
		var l = ProbeClearanceFrom(GlobalPosition + Vector3.Up * 0.9f - right * 1.15f, dir, distance);
		return Mathf.Min(c, Mathf.Min(r, l));
	}

	private float ProbeClearanceFrom(Vector3 origin, Vector3 dir, float distance)
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return distance;

		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			return distance;
		dir = dir.Normalized();

		var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir * distance);
		query.CollisionMask = 1 | 8;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = space.IntersectRay(query);
		if (hit.Count == 0)
			return distance;
		return origin.DistanceTo(hit["position"].AsVector3());
	}

	private Vector3 FlatDirToGoal()
	{
		var preferred = _destination - GlobalPosition;
		preferred.Y = 0f;
		if (preferred.LengthSquared() > 0.01f)
			return preferred.Normalized();
		return -GlobalTransform.Basis.Z;
	}

	private static Vector3 FlatCross(Vector3 forward)
	{
		var right = forward.Cross(Vector3.Up);
		if (right.LengthSquared() < 0.001f)
			right = Vector3.Right;
		return right.Normalized();
	}

	private static Vector3 RotateYaw(Vector3 dir, float radians)
	{
		var c = Mathf.Cos(radians);
		var s = Mathf.Sin(radians);
		return new Vector3(dir.X * c - dir.Z * s, 0f, dir.X * s + dir.Z * c).Normalized();
	}
}
