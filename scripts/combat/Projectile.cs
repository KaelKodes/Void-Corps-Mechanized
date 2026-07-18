using Godot;

namespace Mechanize;

public partial class Projectile : Area3D
{
	public Vector3 Velocity { get; set; }
	public float Damage { get; set; } = 10f;
	public float Lifetime { get; set; } = 2.5f;
	public float GravityAccel { get; set; }
	public Node? Source { get; set; }
	public TeamId SourceTeam { get; set; } = TeamId.Neutral;
	public TargetingMode TargetingMode { get; set; } = TargetingMode.Standard;
	public PartSlot? PreferredSlot { get; set; }
	/// <summary>False on client replicas — they show the shot but never apply damage.</summary>
	public bool DealsDamage { get; set; } = true;

	private float _age;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		AreaEntered += OnAreaEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		var from = GlobalPosition;

		if (GravityAccel > 0f)
			Velocity = new Vector3(Velocity.X, Velocity.Y - GravityAccel * dt, Velocity.Z);

		var to = from + Velocity * dt;

		if (TrySweep(from, to))
			return;

		GlobalPosition = to;
		if (Velocity.LengthSquared() > 0.01f)
			LookAt(GlobalPosition + Velocity, Vector3.Up);

		_age += dt;
		if (_age >= Lifetime)
			MeshMat.QueueFreeSafe(this);
	}

	/// <summary>
	/// Artillery-style lob: rises then falls onto the target. Still collides with cover mid-flight.
	/// Flight time is stretched for arc height; impact is solved to land on <paramref name="to"/>.
	/// Call after parenting into the scene tree (or use <see cref="SolveLob"/> for off-tree probes).
	/// </summary>
	public void LaunchLob(Vector3 from, Vector3 to, float preferredSpeed, float gravity = 26f)
	{
		var solved = SolveLob(from, to, preferredSpeed, gravity);
		GravityAccel = solved.Gravity;
		Velocity = solved.Velocity;
		Lifetime = solved.Lifetime;

		if (IsInsideTree())
		{
			GlobalPosition = from;
			if (Velocity.LengthSquared() > 0.01f)
				LookAt(from + Velocity, Vector3.Up);
		}
		else
		{
			// Off-tree probe (e.g. net sync solving velocity without spawning yet).
			Position = from;
			if (Velocity.LengthSquared() > 0.01f)
				LookAtFromPosition(from, from + Velocity, Vector3.Up);
		}
	}

	/// <summary>Ballistic solve used by <see cref="LaunchLob"/> and host net probes.</summary>
	public static (Vector3 Velocity, float Lifetime, float Gravity) SolveLob(
		Vector3 from,
		Vector3 to,
		float preferredSpeed,
		float gravity = 26f)
	{
		var delta = to - from;
		var heightDelta = delta.Y;
		var flat = new Vector3(delta.X, 0f, delta.Z);
		var horizontal = flat.Length();

		// Time to cover the ground distance at preferred speed, then stretch for a readable arc.
		var flightTime = Mathf.Clamp(horizontal / Mathf.Max(8f, preferredSpeed), 0.65f, 2.8f);
		flightTime += Mathf.Clamp(horizontal * 0.01f, 0.12f, 0.7f);

		// Standard ballistic: y(t) = y0 + vy*t - 0.5*g*t^2 lands at `to` when t = flightTime.
		var vy = (heightDelta + 0.5f * gravity * flightTime * flightTime) / flightTime;
		var horizontalVelocity = flat.LengthSquared() > 0.001f
			? flat.Normalized() * (horizontal / flightTime)
			: Vector3.Zero;

		var velocity = new Vector3(horizontalVelocity.X, vy, horizontalVelocity.Z);
		return (velocity, flightTime + 0.4f, gravity);
	}

	private bool TrySweep(Vector3 from, Vector3 to)
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return false;

		var excludes = new Godot.Collections.Array<Rid>();
		if (Source is CollisionObject3D sourceBody)
			excludes.Add(sourceBody.GetRid());

		// Pass through friendlies / non-damageables by excluding and retrying.
		for (var attempt = 0; attempt < 6; attempt++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.CollisionMask = CollisionMask;
			query.CollideWithAreas = true;
			query.CollideWithBodies = true;
			query.Exclude = excludes;

			var hit = space.IntersectRay(query);
			if (hit.Count == 0)
				return false;

			var collider = hit["collider"].AsGodotObject() as Node;
			if (AbsorbHit(collider))
			{
				GlobalPosition = hit["position"].AsVector3();
				return true;
			}

			if (collider is CollisionObject3D body)
				excludes.Add(body.GetRid());
			else
				return false;
		}

		return false;
	}

	private void OnBodyEntered(Node3D body) => AbsorbHit(body);
	private void OnAreaEntered(Area3D area) => AbsorbHit(area);

	/// <summary>Returns true if this projectile should stop (damage applied or world impact).</summary>
	private bool AbsorbHit(Node? node)
	{
		if (node == null)
			return false;
		if (Source != null && (node == Source || Source.IsAncestorOf(node) || node.IsAncestorOf(Source)))
			return false;

		var hitTeam = TeamUtil.GetTeam(node);
		if (SourceTeam != TeamId.Neutral && hitTeam != TeamId.Neutral && !TeamUtil.IsHostile(SourceTeam, hitTeam))
			return false;

		var mech = FindMech(node);
		if (mech?.Integrity != null)
		{
			if (DealsDamage)
			{
				mech.Integrity.ReceiveHit(
					Damage,
					GlobalPosition,
					PreferredSlot,
					TargetingMode == TargetingMode.AimedComponent);
				if (TelemetryUtil.IsPlayerSource(Source))
				{
					var telemetry = TelemetryUtil.Match(this)?.Telemetry;
					telemetry?.RecordHit(TelemetryTargetKind.Map, Name == "Missile");
					if (mech.Integrity.IsCollapsed || mech.Health?.IsDead == true)
						telemetry?.RecordKill(TelemetryTargetKind.Map);
				}
			}

			SfxService.Play("weapon_hit", (float)GD.RandRange(0.9, 1.1), -2f);
			MeshMat.QueueFreeSafe(this);
			return true;
		}

		var damageable = FindDamageable(node);
		if (damageable != null)
		{
			if (DealsDamage)
			{
				var kind = TelemetryUtil.Classify(node);
				damageable.ApplyDamage(Damage);
				if (TelemetryUtil.IsPlayerSource(Source))
				{
					var telemetry = TelemetryUtil.Match(this)?.Telemetry;
					telemetry?.RecordHit(kind, Name == "Missile");
					if (damageable.IsDead)
						telemetry?.RecordKill(kind);
				}
			}

			SfxService.Play("weapon_hit", (float)GD.RandRange(0.95, 1.15), -3f);
			MeshMat.QueueFreeSafe(this);
			return true;
		}

		// World / cover / other solid — stop the shot.
		if (hitTeam == TeamId.Neutral)
		{
			SfxService.Play("weapon_hit", 0.8f, -8f);
			MeshMat.QueueFreeSafe(this);
			return true;
		}

		return false;
	}

	private static MechController? FindMech(Node node)
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

	private static Damageable? FindDamageable(Node node)
	{
		var current = node;
		while (current != null)
		{
			if (current is Damageable direct)
				return direct;

			var child = current.GetNodeOrNull<Damageable>("Damageable");
			if (child != null)
				return child;

			current = current.GetParent();
		}

		return null;
	}

	public static Projectile Create(bool ballisticStyle = false)
	{
		var projectile = new Projectile
		{
			Name = ballisticStyle ? "Missile" : "Projectile",
			CollisionLayer = 4,
			CollisionMask = 1 | 2 | 8,
			Monitoring = true,
			Monitorable = false
		};

		var radius = ballisticStyle ? 0.28f : 0.18f;
		var collision = new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = radius + 0.05f }
		};
		projectile.AddChild(collision);

		var mesh = new MeshInstance3D
		{
			Mesh = ballisticStyle
				? new CapsuleMesh { Radius = 0.12f, Height = 0.55f }
				: new SphereMesh { Radius = radius, Height = radius * 2f },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = ballisticStyle ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.85f, 0.35f),
			EmissionEnabled = true,
			Emission = ballisticStyle ? new Color(1f, 0.35f, 0.1f) : new Color(1f, 0.7f, 0.2f),
			EmissionEnergyMultiplier = 2.2f
		};
		mesh.Mesh.SurfaceSetMaterial(0, mat);
		mesh.MaterialOverride = mat;
		if (ballisticStyle)
			mesh.Rotation = new Vector3(Mathf.Tau * 0.25f, 0f, 0f);
		projectile.AddChild(mesh);

		var light = new OmniLight3D
		{
			LightColor = ballisticStyle ? new Color(1f, 0.5f, 0.2f) : new Color(1f, 0.8f, 0.3f),
			LightEnergy = ballisticStyle ? 1.6f : 1.2f,
			OmniRange = ballisticStyle ? 5f : 4f
		};
		projectile.AddChild(light);

		return projectile;
	}
}
