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
	/// <summary>Optional seeker track — steers toward lock while the target remains valid.</summary>
	public MechController? HomingTarget { get; set; }
	public float HomingSpeed { get; set; }
	public float HomingTurnRate { get; set; } = 4.5f;
	/// <summary>False on client replicas — they show the shot but never apply damage.</summary>
	public bool DealsDamage { get; set; } = true;
	/// <summary>Disable for dense hazards so misses against cover do not flood the mix.</summary>
	public bool PlaysWorldImpactSfx { get; set; } = true;
	/// <summary>When false, the shot is still blocked by cover but never damages it (bullet-hell hazards).</summary>
	public bool DamagesWorldObjects { get; set; } = true;

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

		UpdateHoming(dt);

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

	private void UpdateHoming(float dt)
	{
		if (HomingTarget == null || HomingSpeed <= 0.01f)
			return;

		if (!GodotObject.IsInstanceValid(HomingTarget)
		    || HomingTarget.Integrity?.IsCollapsed == true
		    || HomingTarget.Health?.IsDead == true)
		{
			// Lock broken mid-flight — keep last velocity (no more steer).
			HomingTarget = null;
			return;
		}

		var aim = ResolveHomingAim(HomingTarget, PreferredSlot);
		var toAim = aim - GlobalPosition;
		if (toAim.LengthSquared() < 0.01f)
			return;

		var desired = toAim.Normalized() * HomingSpeed;
		var turn = Mathf.Clamp(HomingTurnRate * dt, 0f, 1f);
		Velocity = Velocity.Lerp(desired, turn);
	}

	private static Vector3 ResolveHomingAim(MechController target, PartSlot? focus)
	{
		if (focus.HasValue
		    && target.Assembler?.Hardpoints.TryGetValue(focus.Value, out var hp) == true
		    && hp.EquippedPart != null
		    && hp.EquippedPart.VisualKind != "empty"
		    && !hp.IsDestroyed)
			return hp.GlobalPosition;

		if (target.Assembler?.Hardpoints.TryGetValue(PartSlot.Torso, out var torso) == true)
			return torso.GlobalPosition;

		return target.GlobalPosition + Vector3.Up * 1.4f;
	}

	/// <summary>
	/// Sensor-lock seeker: low-arc launch that steers toward the locked mech / focus band.
	/// If the lock dies mid-flight, the missile keeps its last velocity.
	/// </summary>
	public void LaunchSeeker(
		Vector3 from,
		MechController target,
		PartSlot? focus,
		float speed,
		float gravity = 6f)
	{
		HomingTarget = target;
		PreferredSlot = focus;
		HomingSpeed = Mathf.Max(12f, speed);
		HomingTurnRate = 5.2f;
		TargetingMode = TargetingMode.AimedComponent;

		var aim = ResolveHomingAim(target, focus);
		var delta = aim - from;
		var horizontal = new Vector3(delta.X, 0f, delta.Z).Length();
		Lifetime = Mathf.Clamp(horizontal / HomingSpeed + 0.85f, 1.1f, 4.2f);
		GravityAccel = gravity;

		var dir = delta.LengthSquared() > 0.01f ? delta.Normalized() : Vector3.Forward;
		Velocity = dir * HomingSpeed + Vector3.Up * (HomingSpeed * 0.12f);

		if (IsInsideTree())
		{
			GlobalPosition = from;
			LookAt(from + Velocity, Vector3.Up);
		}
		else
		{
			Position = from;
			LookAtFromPosition(from, from + Velocity, Vector3.Up);
		}
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
			var impact = hit["position"].AsVector3();
			if (AbsorbHit(collider, impact))
			{
				GlobalPosition = impact;
				return true;
			}

			if (collider is CollisionObject3D body)
				excludes.Add(body.GetRid());
			else
				return false;
		}

		return false;
	}

	private void OnBodyEntered(Node3D body) => AbsorbHit(body, body.GlobalPosition);
	private void OnAreaEntered(Area3D area) => AbsorbHit(area, area.GlobalPosition);

	/// <summary>Returns true if this projectile should stop (damage applied or world impact).</summary>
	private bool AbsorbHit(Node? node, Vector3 impactPosition)
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
					impactPosition,
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
			else
			{
				SfxService.PlayImpactLight(impactPosition, -6f);
			}

			MeshMat.QueueFreeSafe(this);
			return true;
		}

		var damageable = FindDamageable(node);
		if (damageable != null)
		{
			// Hazard projectiles are blocked by cover but never chew through it.
			if (DealsDamage && DamagesWorldObjects)
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

			if (PlaysWorldImpactSfx)
				SfxService.PlayImpactCover(impactPosition, -3f);
			MeshMat.QueueFreeSafe(this);
			return true;
		}

		// World / cover / other solid — stop the shot.
		if (hitTeam == TeamId.Neutral)
		{
			if (PlaysWorldImpactSfx)
				SfxService.PlayImpactCover(impactPosition, -8f);
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

	public const float MechVisualScale = 1.1f;

	public static Projectile Create(bool ballisticStyle = false) =>
		Create(ballisticStyle ? ProjectileStyle.DumbRocket : ProjectileStyle.CarbineTracer);

	public static Projectile Create(ProjectileStyle style, float visualScale = 1f)
	{
		var projectile = new Projectile
		{
			Name = ProjectileStyleUtil.IsMissile(style) ? "Missile" : "Projectile",
			CollisionLayer = 4,
			CollisionMask = 1 | 2 | 8,
			Monitoring = true,
			Monitorable = false
		};

		var spec = StyleSpec(style);
		var scale = Mathf.Max(0.05f, visualScale);
		var collision = new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = spec.CollisionRadius * scale }
		};
		projectile.AddChild(collision);

		var visual = new Node3D
		{
			Name = "Visual",
			Scale = Vector3.One * scale
		};
		projectile.AddChild(visual);

		var mesh = new MeshInstance3D
		{
			Mesh = spec.Mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = MakeMat(spec.Albedo, spec.Emission, spec.EmissionEnergy)
		};
		if (spec.MeshPitchQuarterTurn)
			mesh.Rotation = new Vector3(Mathf.Tau * 0.25f, 0f, 0f);
		visual.AddChild(mesh);

		if (spec.TrailLength > 0.05f)
		{
			var trailMat = MakeMat(
				new Color(spec.Emission.R, spec.Emission.G, spec.Emission.B, 0.45f),
				spec.Emission,
				spec.EmissionEnergy * 0.55f);
			var trail = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(spec.TrailWidth, spec.TrailWidth, spec.TrailLength) },
				Position = new Vector3(0f, 0f, spec.TrailLength * 0.45f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = trailMat
			};
			visual.AddChild(trail);
		}

		visual.AddChild(new OmniLight3D
		{
			LightColor = spec.Emission,
			LightEnergy = spec.LightEnergy,
			OmniRange = spec.LightRange
		});

		return projectile;
	}

	private sealed class VisualSpec
	{
		public Mesh Mesh = null!;
		public Color Albedo;
		public Color Emission;
		public float EmissionEnergy;
		public float CollisionRadius;
		public float LightEnergy;
		public float LightRange;
		public float TrailLength;
		public float TrailWidth;
		public bool MeshPitchQuarterTurn;
	}

	private static VisualSpec StyleSpec(ProjectileStyle style) => style switch
	{
		ProjectileStyle.Slug => new VisualSpec
		{
			Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = 0.28f },
			Albedo = new Color(0.35f, 0.34f, 0.32f),
			Emission = new Color(1f, 0.55f, 0.25f),
			EmissionEnergy = 1.1f,
			CollisionRadius = 0.16f,
			LightEnergy = 0.7f,
			LightRange = 3.2f,
			TrailLength = 0.35f,
			TrailWidth = 0.04f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.Shell => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.11f, Height = 0.42f },
			Albedo = new Color(0.55f, 0.42f, 0.22f),
			Emission = new Color(1f, 0.45f, 0.15f),
			EmissionEnergy = 1.6f,
			CollisionRadius = 0.22f,
			LightEnergy = 1.1f,
			LightRange = 4.5f,
			TrailLength = 0.55f,
			TrailWidth = 0.07f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.ApNeedle => new VisualSpec
		{
			Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.04f, Height = 0.48f },
			Albedo = new Color(0.22f, 0.24f, 0.28f),
			Emission = new Color(0.45f, 0.85f, 1f),
			EmissionEnergy = 1.8f,
			CollisionRadius = 0.12f,
			LightEnergy = 0.85f,
			LightRange = 3.5f,
			TrailLength = 0.7f,
			TrailWidth = 0.025f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.CarbineTracer => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.045f, Height = 0.22f },
			Albedo = new Color(0.9f, 0.55f, 0.2f),
			Emission = new Color(1f, 0.55f, 0.18f),
			EmissionEnergy = 2.0f,
			CollisionRadius = 0.14f,
			LightEnergy = 0.9f,
			LightRange = 3.4f,
			TrailLength = 0.5f,
			TrailWidth = 0.03f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.Autocannon => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.06f, Height = 0.26f },
			Albedo = new Color(1f, 0.5f, 0.18f),
			Emission = new Color(1f, 0.4f, 0.1f),
			EmissionEnergy = 2.2f,
			CollisionRadius = 0.15f,
			LightEnergy = 1.0f,
			LightRange = 3.8f,
			TrailLength = 0.4f,
			TrailWidth = 0.04f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.ScatterPellet => new VisualSpec
		{
			Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
			Albedo = new Color(0.4f, 0.38f, 0.35f),
			Emission = new Color(0.85f, 0.55f, 0.3f),
			EmissionEnergy = 0.7f,
			CollisionRadius = 0.12f,
			LightEnergy = 0.45f,
			LightRange = 2.4f,
			TrailLength = 0.15f,
			TrailWidth = 0.03f
		},
		ProjectileStyle.EnergyBolt => new VisualSpec
		{
			Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f },
			Albedo = new Color(0.4f, 0.9f, 1f, 0.85f),
			Emission = new Color(0.35f, 0.85f, 1f),
			EmissionEnergy = 3.2f,
			CollisionRadius = 0.18f,
			LightEnergy = 1.4f,
			LightRange = 4.8f,
			TrailLength = 0.45f,
			TrailWidth = 0.08f
		},
		ProjectileStyle.EnergyLance => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.055f, Height = 0.65f },
			Albedo = new Color(0.55f, 0.8f, 1f, 0.9f),
			Emission = new Color(0.3f, 0.7f, 1f),
			EmissionEnergy = 3.6f,
			CollisionRadius = 0.16f,
			LightEnergy = 1.5f,
			LightRange = 5.2f,
			TrailLength = 0.85f,
			TrailWidth = 0.05f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.BeamSlug => new VisualSpec
		{
			Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.9f) },
			Albedo = new Color(0.75f, 0.55f, 1f, 0.9f),
			Emission = new Color(0.7f, 0.4f, 1f),
			EmissionEnergy = 4.0f,
			CollisionRadius = 0.14f,
			LightEnergy = 1.7f,
			LightRange = 5.5f,
			TrailLength = 1.1f,
			TrailWidth = 0.04f
		},
		ProjectileStyle.CoilPulse => new VisualSpec
		{
			Mesh = new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.16f, Rings = 8, RingSegments = 12 },
			Albedo = new Color(1f, 0.45f, 0.85f, 0.9f),
			Emission = new Color(1f, 0.35f, 0.8f),
			EmissionEnergy = 3.4f,
			CollisionRadius = 0.22f,
			LightEnergy = 1.8f,
			LightRange = 5.8f,
			TrailLength = 0.3f,
			TrailWidth = 0.1f
		},
		ProjectileStyle.Spark => new VisualSpec
		{
			Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f },
			Albedo = new Color(0.7f, 1f, 1f),
			Emission = new Color(0.5f, 1f, 1f),
			EmissionEnergy = 3.8f,
			CollisionRadius = 0.1f,
			LightEnergy = 0.8f,
			LightRange = 2.8f,
			TrailLength = 0.25f,
			TrailWidth = 0.02f
		},
		ProjectileStyle.DumbRocket => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.11f, Height = 0.55f },
			Albedo = new Color(0.45f, 0.4f, 0.35f),
			Emission = new Color(1f, 0.4f, 0.12f),
			EmissionEnergy = 2.4f,
			CollisionRadius = 0.26f,
			LightEnergy = 1.5f,
			LightRange = 5f,
			TrailLength = 0.75f,
			TrailWidth = 0.09f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.Seeker => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.07f, Height = 0.58f },
			Albedo = new Color(0.25f, 0.3f, 0.35f),
			Emission = new Color(0.35f, 0.9f, 1f),
			EmissionEnergy = 2.6f,
			CollisionRadius = 0.2f,
			LightEnergy = 1.3f,
			LightRange = 4.6f,
			TrailLength = 0.7f,
			TrailWidth = 0.05f,
			MeshPitchQuarterTurn = true
		},
		ProjectileStyle.ArcMicrotorp => new VisualSpec
		{
			Mesh = new CapsuleMesh { Radius = 0.08f, Height = 0.5f },
			Albedo = new Color(0.55f, 0.4f, 1f, 0.9f),
			Emission = new Color(0.65f, 0.35f, 1f),
			EmissionEnergy = 3.2f,
			CollisionRadius = 0.2f,
			LightEnergy = 1.6f,
			LightRange = 5.2f,
			TrailLength = 0.8f,
			TrailWidth = 0.07f,
			MeshPitchQuarterTurn = true
		},
		_ => new VisualSpec // HazardOrb
		{
			Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f },
			Albedo = new Color(1f, 0.25f, 0.2f),
			Emission = new Color(1f, 0.2f, 0.15f),
			EmissionEnergy = 2.5f,
			CollisionRadius = 0.2f,
			LightEnergy = 1.2f,
			LightRange = 4f,
			TrailLength = 0.35f,
			TrailWidth = 0.08f
		}
	};

	private static StandardMaterial3D MakeMat(Color albedo, Color emission, float emissionEnergy)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = albedo,
			EmissionEnabled = true,
			Emission = emission,
			EmissionEnergyMultiplier = emissionEnergy,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			DisableReceiveShadows = true
		};
		if (albedo.A < 0.99f)
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		return mat;
	}
}
