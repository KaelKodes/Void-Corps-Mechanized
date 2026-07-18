using Godot;

namespace Mechanize;

public partial class Hardpoint : Node3D
{
	[Export] public PartSlot Slot { get; set; } = PartSlot.WeaponL;

	public PartData? EquippedPart { get; private set; }
	public AimMode EffectiveAimMode { get; private set; } = AimMode.Fixed;
	public bool IsWeapon => Slot is PartSlot.WeaponL or PartSlot.WeaponR;
	public bool IsDestroyed { get; private set; }
	public float CurrentHp => _externalHealth?.CurrentHealth ?? _currentHp;
	public float MaxHp => _externalHealth?.MaxHealth ?? _maxHp;
	public float ComponentArmor { get; private set; }
	public int LimbCount { get; private set; } = 1;
	public int LimbsAlive { get; private set; } = 1;
	public bool IsLegPackage => Slot == PartSlot.Legs;
	/// <summary>
	/// Power cores are encased in the torso — they are not a separate hit location.
	/// Core loss only happens when the torso collapses (MAP defeat).
	/// </summary>
	public bool CanTakeDamage =>
		Slot != PartSlot.PowerCore
		&& EquippedPart != null
		&& !IsDestroyed
		&& EquippedPart.VisualKind != "empty";
	public bool CanFire => IsWeapon && EquippedPart != null && !IsDestroyed
	                       && EquippedPart is { IsHeldShield: false, Damage: > 0f };

	/// <summary>1 = full mobility, 0 = immobilized.</summary>
	public float MobilityFactor
	{
		get
		{
			if (!IsLegPackage)
				return IsDestroyed ? 0f : 1f;
			if (LimbCount <= 0 || LimbsAlive <= 0)
				return 0f;

			var type = EquippedPart?.LegType ?? LegType.Bipedal;
			if (type is LegType.Bipedal or LegType.Tracks)
			{
				// 2 limbs: both = full, one = crawl, zero = stuck
				return LimbsAlive switch
				{
					2 => 1f,
					1 => 0.28f,
					_ => 0f
				};
			}

			// Hexapod: each lost leg slows harder; 1 left is a pathetic crawl.
			return LimbsAlive switch
			{
				6 => 1f,
				5 => 0.82f,
				4 => 0.62f,
				3 => 0.42f,
				2 => 0.26f,
				1 => 0.12f,
				_ => 0f
			};
		}
	}

	private float _cooldown;
	private Vector3 _previousMeleeForward;
	private bool _hasPreviousMeleeForward;
	private ulong _latchedMeleeContactId;
	private Node3D? _visual;
	private float[] _limbHp = System.Array.Empty<float>();
	private float _limbMaxHp;
	private float _currentHp;
	private float _maxHp;
	private Damageable? _externalHealth;
	private DamageSmoke? _damageSmoke;

	public void Equip(PartData? part)
	{
		Visible = true;
		EquippedPart = part;
		EffectiveAimMode = AimMode.Fixed;
		IsDestroyed = false;
		ResetMeleeContactState();

		if (part != null)
			EffectiveAimMode = part.AimMode;

		InitializeIntegrity();
		RebuildVisual();
	}

	public void InitializeIntegrity()
	{
		_externalHealth = null;

		if (EquippedPart == null || EquippedPart.VisualKind == "empty")
		{
			_maxHp = 0f;
			_currentHp = 0f;
			ComponentArmor = 0f;
			LimbCount = 0;
			LimbsAlive = 0;
			_limbHp = System.Array.Empty<float>();
			return;
		}

		ComponentArmor = Mathf.Max(0f, EquippedPart.Armor);

		if (IsLegPackage)
		{
			LimbCount = EquippedPart.LegType == LegType.Hexapod ? 6 : 2;
			_limbMaxHp = Mathf.Max(1f, EquippedPart.StructureHp);
			_limbHp = new float[LimbCount];
			for (var i = 0; i < LimbCount; i++)
				_limbHp[i] = _limbMaxHp;
			LimbsAlive = LimbCount;
			_maxHp = _limbMaxHp * LimbCount;
			_currentHp = _maxHp;
		}
		else
		{
			LimbCount = 1;
			LimbsAlive = 1;
			_maxHp = Mathf.Max(1f, EquippedPart.StructureHp);
			_limbMaxHp = _maxHp;
			_limbHp = new[] { _maxHp };
			_currentHp = _maxHp;
		}

		IsDestroyed = false;
	}

	/// <summary>
	/// Make an external health node authoritative for this component.
	/// The torso uses this to preserve existing AI, network, telemetry, and death hooks.
	/// </summary>
	public void BindExternalHealth(Damageable health)
	{
		_externalHealth = health;
		health.ResetHealth(_maxHp);
	}

	public void Clear()
	{
		Visible = true;
		EquippedPart = null;
		EffectiveAimMode = AimMode.Fixed;
		IsDestroyed = false;
		_externalHealth = null;
		_currentHp = 0f;
		_maxHp = 0f;
		LimbCount = 0;
		LimbsAlive = 0;
		_limbHp = System.Array.Empty<float>();
		RebuildVisual();
	}

	/// <summary>
	/// Returns true if the whole hardpoint/package was destroyed.
	/// For legs, also reports how many individual limbs were lost this hit.
	/// </summary>
	public bool ApplyComponentDamage(float rawDamage, out int limbsLostThisHit)
	{
		limbsLostThisHit = 0;
		if (!CanTakeDamage)
			return false;

		var mitigated = rawDamage * (50f / (50f + Mathf.Max(0f, ComponentArmor)));

		if (!IsLegPackage || _limbHp.Length == 0)
		{
			if (_externalHealth != null)
				_externalHealth.ApplyDamage(mitigated);
			else
				_currentHp = Mathf.Max(0f, _currentHp - mitigated);

			if (CurrentHp > 0f)
				return false;
			LimbsAlive = 0;
			limbsLostThisHit = 1;
			return true;
		}

		// Concentrate fire on one living limb (sharpshooter / nearest-limb fantasy).
		var index = FindMostDamagedLivingLimb();
		if (index < 0)
			return true;

		var beforeAlive = LimbsAlive;
		_limbHp[index] = Mathf.Max(0f, _limbHp[index] - mitigated);
		RecalculateLegTotals();
		limbsLostThisHit = beforeAlive - LimbsAlive;
		return LimbsAlive <= 0;
	}

	public bool ApplyComponentDamage(float rawDamage) => ApplyComponentDamage(rawDamage, out _);

	/// <summary>Restore package / limb HP. Does not revive fully destroyed hardpoints.</summary>
	public float ApplyHeal(float amount)
	{
		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || IsDestroyed || amount <= 0f || MaxHp <= 0f)
			return 0f;

		if (!IsLegPackage || _limbHp.Length == 0)
		{
			var before = CurrentHp;
			if (_externalHealth != null)
				_externalHealth.ApplyHeal(amount);
			else
				_currentHp = Mathf.Min(_maxHp, _currentHp + amount);
			if (_limbHp.Length > 0)
				_limbHp[0] = CurrentHp;
			return CurrentHp - before;
		}

		var healed = 0f;
		var remaining = amount;
		while (remaining > 0.01f)
		{
			var index = FindMostDamagedLivingLimb();
			if (index < 0)
				break;
			var space = _limbMaxHp - _limbHp[index];
			if (space <= 0.01f)
			{
				// All living limbs full — stop.
				var anyDamaged = false;
				for (var i = 0; i < _limbHp.Length; i++)
				{
					if (_limbHp[i] > 0f && _limbHp[i] < _limbMaxHp - 0.01f)
					{
						anyDamaged = true;
						break;
					}
				}
				if (!anyDamaged)
					break;
				continue;
			}

			var add = Mathf.Min(remaining, space);
			_limbHp[index] += add;
			remaining -= add;
			healed += add;
		}

		RecalculateLegTotals();
		return healed;
	}

	private int FindMostDamagedLivingLimb()
	{
		var best = -1;
		var bestHp = float.MaxValue;
		for (var i = 0; i < _limbHp.Length; i++)
		{
			if (_limbHp[i] <= 0f)
				continue;
			if (_limbHp[i] < bestHp)
			{
				bestHp = _limbHp[i];
				best = i;
			}
		}

		return best;
	}

	private void RecalculateLegTotals()
	{
		var alive = 0;
		var sum = 0f;
		foreach (var hp in _limbHp)
		{
			if (hp > 0f)
				alive++;
			sum += hp;
		}

		LimbsAlive = alive;
		_currentHp = sum;
	}

	public void MarkDestroyed()
	{
		IsDestroyed = true;
		if (_externalHealth != null && !_externalHealth.IsDead)
			_externalHealth.ApplyDamage(_externalHealth.CurrentHealth + 1f);
		_currentHp = 0f;
		LimbsAlive = 0;
		for (var i = 0; i < _limbHp.Length; i++)
			_limbHp[i] = 0f;
		if (_visual != null)
		{
			MeshMat.QueueFreeSafe(_visual);
			_visual = null;
		}
	}

	public void AimAt(Vector3 worldPoint, Vector3 chassisForward)
	{
		if (!CanFire)
			return;

		if (EffectiveAimMode == AimMode.Gimbaled)
		{
			var flat = worldPoint;
			flat.Y = GlobalPosition.Y;
			if (GlobalPosition.DistanceSquaredTo(flat) > 0.01f)
				LookAt(flat, Vector3.Up);
		}
		else
		{
			var target = GlobalPosition + chassisForward;
			target.Y = GlobalPosition.Y;
			LookAt(target, Vector3.Up);
		}
	}

	public bool TryFire(
		Node source,
		Node parentForProjectile,
		float fireRateMultiplier,
		float heatThrottle,
		Vector3 aimPoint,
		PartSlot? aimedSlot,
		out float damageDealt,
		out float heatGenerated)
	{
		damageDealt = 0f;
		heatGenerated = 0f;
		if (!CanFire || EquippedPart == null)
			return false;
		if (_cooldown > 0f)
			return false;

		var rate = EquippedPart.FireRate * Mathf.Max(0.1f, fireRateMultiplier) * Mathf.Max(0.05f, heatThrottle);
		_cooldown = 1f / Mathf.Max(0.1f, rate);

		var muzzle = GlobalPosition + (-GlobalTransform.Basis.Z) * 1.2f;
		// Riding a tall carrier: keep the visual mount elevated, but spawn shots at the
		// height they'd have on the ground so they still meet tanks / other MAPs.
		if (source is MechController { IsCarrierMounted: true } rider)
			muzzle.Y -= rider.CarrierCombatLift;

		Vector3 direction;
		if (EffectiveAimMode == AimMode.Fixed)
		{
			// Fixed mounts fire down the barrel. Cursor aim must never bend their shot.
			direction = -GlobalTransform.Basis.Z;
			if (direction.LengthSquared() < 0.001f)
				direction = Vector3.Forward;
			else
				direction = direction.Normalized();
			// Flatten pitch — barrel may be elevated visually but the shot stays level.
			direction = new Vector3(direction.X, 0f, direction.Z);
			if (direction.LengthSquared() < 0.001f)
				direction = Vector3.Forward;
			else
				direction = direction.Normalized();
		}
		else
		{
			var toTarget = aimPoint - muzzle;
			if (toTarget.LengthSquared() < 0.01f)
			{
				direction = -GlobalTransform.Basis.Z;
			}
			else
			{
				// Gimbals track the cursor, with bounded pitch.
				direction = toTarget.Normalized();
				var flat = new Vector3(direction.X, 0f, direction.Z);
				if (flat.LengthSquared() > 0.001f)
				{
					flat = flat.Normalized();
					var pitch = Mathf.Clamp(direction.Y, -0.35f, 0.25f);
					direction = new Vector3(flat.X, pitch, flat.Z).Normalized();
				}
			}
		}

		var speed = EquippedPart.ProjectileSpeed;
		var velocity = direction * speed;
		var lifetime = EquippedPart.Range / Mathf.Max(1f, speed);
		var preferred = EffectiveAimMode == AimMode.Gimbaled
			&& EquippedPart.TargetingMode == TargetingMode.AimedComponent
			&& aimedSlot.HasValue
			? (int)aimedSlot.Value
			: -1;
		var team = source is MechController mechSource ? mechSource.Team : TeamUtil.GetTeam(source);

		var bus = NetCombatBus.Find(parentForProjectile);
		if (bus != null && parentForProjectile.GetTree()?.GetMultiplayer().MultiplayerPeer != null)
		{
			bus.HostSpawnProjectile(
				parentForProjectile,
				source,
				muzzle,
				velocity,
				EquippedPart.Damage,
				lifetime,
				team,
				EquippedPart.TargetingMode,
				preferred,
				ballistic: false,
				gravity: 0f);
		}
		else
		{
			var projectile = Projectile.Create();
			projectile.Source = source;
			projectile.SourceTeam = team;
			projectile.Damage = EquippedPart.Damage;
			projectile.Velocity = velocity;
			projectile.Lifetime = lifetime;
			projectile.TargetingMode = EquippedPart.TargetingMode;
			projectile.PreferredSlot = preferred >= 0 ? (PartSlot)preferred : null;
			parentForProjectile.AddChild(projectile);
			projectile.GlobalPosition = muzzle;
			projectile.LookAt(muzzle + direction, Vector3.Up);
		}

		SfxService.Play("weapon_fire", (float)GD.RandRange(0.92, 1.08), -3f);

		damageDealt = EquippedPart.Damage;
		heatGenerated = EquippedPart.HeatPerShot;
		return true;
	}

	/// <summary>
	/// Cursor-driven melee. The gimbaled hardpoint is the animation: we sweep the blade
	/// segment between its previous and current facing. Air swings are free; entering
	/// cover or a target produces one contact event, damage where applicable, and heat.
	/// </summary>
	public bool TryMeleeContact(
		Node source,
		PartSlot? aimedSlot,
		out float heatGenerated,
		out bool dealtDamage)
	{
		heatGenerated = 0f;
		dealtDamage = false;
		if (!CanFire || EquippedPart == null || EquippedPart.WeaponFamily != WeaponFamily.Melee)
		{
			ResetMeleeContactState();
			return false;
		}

		var currentForward = -GlobalTransform.Basis.Z;
		currentForward.Y = 0f;
		if (currentForward.LengthSquared() < 0.001f)
			return false;
		currentForward = currentForward.Normalized();

		if (!_hasPreviousMeleeForward)
		{
			_previousMeleeForward = currentForward;
			_hasPreviousMeleeForward = true;
		}

		var origin = GlobalPosition;
		var reach = Mathf.Max(1.25f, EquippedPart.Range);
		var sweepHit = TraceMeleeSweep(source, origin, _previousMeleeForward, currentForward, reach);
		var currentHit = TraceMeleeBlade(source, origin, currentForward, reach);
		_previousMeleeForward = currentForward;

		var currentId = ContactId(currentHit);
		var sweepId = ContactId(sweepHit);
		var isNewContact = sweepId != 0 && sweepId != _latchedMeleeContactId;
		var canDriveCut = _cooldown <= 0f;

		if (isNewContact && canDriveCut)
		{
			var rate = Mathf.Max(0.1f, EquippedPart.FireRate);
			_cooldown = 1f / rate;
			heatGenerated = EquippedPart.HeatPerShot;
			dealtDamage = ApplyMeleeContact(source, sweepHit, aimedSlot);
			SfxService.Play("weapon_hit", (float)GD.RandRange(0.88, 1.06), dealtDamage ? -1f : -5f);
		}

		// Remaining in contact cannot machine-gun damage. Pull the blade clear, then
		// cross the object again (subject to contact cadence) to cut again.
		_latchedMeleeContactId = currentId;
		return isNewContact && canDriveCut;
	}

	private Godot.Collections.Dictionary TraceMeleeSweep(
		Node source,
		Vector3 origin,
		Vector3 fromForward,
		Vector3 toForward,
		float reach)
	{
		var dot = Mathf.Clamp(fromForward.Dot(toForward), -1f, 1f);
		var angle = Mathf.Acos(dot);
		var steps = Mathf.Clamp(Mathf.CeilToInt(angle / Mathf.DegToRad(6f)), 1, 24);

		for (var i = 1; i <= steps; i++)
		{
			var direction = fromForward.Slerp(toForward, i / (float)steps).Normalized();
			var hit = TraceMeleeBlade(source, origin, direction, reach);
			if (hit.Count > 0)
				return hit;
		}

		return new Godot.Collections.Dictionary();
	}

	private Godot.Collections.Dictionary TraceMeleeBlade(
		Node source,
		Vector3 origin,
		Vector3 direction,
		float reach)
	{
		// Three parallel traces approximate the visible blade width while preserving
		// first-solid blocking. Pick the nearest physical contact across them.
		var right = direction.Cross(Vector3.Up);
		if (right.LengthSquared() < 0.001f)
			right = GlobalTransform.Basis.X;
		right = right.Normalized();

		Godot.Collections.Dictionary? closest = null;
		var closestDistance = float.MaxValue;
		foreach (var offset in new[] { 0f, -0.18f, 0.18f })
		{
			var rayOrigin = origin + right * offset;
			var hit = TraceMeleeRay(source, rayOrigin, direction, reach);
			if (hit.Count == 0)
				continue;
			var impact = hit["position"].AsVector3();
			var distance = rayOrigin.DistanceSquaredTo(impact);
			if (distance >= closestDistance)
				continue;
			closestDistance = distance;
			closest = hit;
		}

		return closest ?? new Godot.Collections.Dictionary();
	}

	private Godot.Collections.Dictionary TraceMeleeRay(Node source, Vector3 origin, Vector3 direction, float reach)
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return new Godot.Collections.Dictionary();

		var excludes = new Godot.Collections.Array<Rid>();
		if (source is CollisionObject3D sourceBody)
			excludes.Add(sourceBody.GetRid());

		for (var attempt = 0; attempt < 4; attempt++)
		{
			var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * reach);
			query.CollisionMask = PhysicsLayers.World | PhysicsLayers.Mechs | PhysicsLayers.Targets;
			query.CollideWithAreas = true;
			query.CollideWithBodies = true;
			query.Exclude = excludes;

			var hit = space.IntersectRay(query);
			if (hit.Count == 0)
				return hit;

			var collider = hit["collider"].AsGodotObject() as Node;
			if (collider != null
			    && (collider == source || source.IsAncestorOf(collider) || collider.IsAncestorOf(source)))
			{
				if (collider is CollisionObject3D collision)
				{
					excludes.Add(collision.GetRid());
					continue;
				}
			}

			return hit;
		}

		return new Godot.Collections.Dictionary();
	}

	private bool ApplyMeleeContact(
		Node source,
		Godot.Collections.Dictionary hit,
		PartSlot? aimedSlot)
	{
		if (hit.Count == 0 || EquippedPart == null)
			return false;

		var collider = hit["collider"].AsGodotObject() as Node;
		if (collider == null)
			return false;
		var impact = hit["position"].AsVector3();
		var sourceTeam = source is MechController sourceMech ? sourceMech.Team : TeamUtil.GetTeam(source);
		var hitTeam = TeamUtil.GetTeam(collider);

		// Friendly contact still engages the arm (heat), but never deals friendly damage.
		if (sourceTeam != TeamId.Neutral && hitTeam != TeamId.Neutral
		    && !TeamUtil.IsHostile(sourceTeam, hitTeam))
			return false;

		var mech = FindMech(collider);
		if (mech?.Integrity != null)
		{
			var preferred = EquippedPart.TargetingMode == TargetingMode.AimedComponent
				? aimedSlot
				: null;
			mech.Integrity.ReceiveHit(EquippedPart.Damage, impact, preferred, preferred.HasValue);
			RecordMeleeTelemetry(source, mech, null);
			return true;
		}

		var damageable = FindDamageable(collider);
		if (damageable == null)
			return false; // solid cover: contact heat, no structure damage API

		damageable.ApplyDamage(EquippedPart.Damage);
		RecordMeleeTelemetry(source, null, damageable);
		return true;
	}

	private static void RecordMeleeTelemetry(Node source, MechController? mech, Damageable? damageable)
	{
		if (!TelemetryUtil.IsPlayerSource(source))
			return;
		var telemetry = TelemetryUtil.Match(source)?.Telemetry;
		var kind = mech != null ? TelemetryTargetKind.Map : TelemetryUtil.Classify(damageable?.GetParent());
		telemetry?.RecordHit(kind, false);
		if (mech?.Integrity?.IsCollapsed == true || mech?.Health?.IsDead == true || damageable?.IsDead == true)
			telemetry?.RecordKill(kind);
	}

	private static ulong ContactId(Godot.Collections.Dictionary hit)
	{
		if (hit.Count == 0)
			return 0;
		return (hit["collider"].AsGodotObject() as GodotObject)?.GetInstanceId() ?? 0;
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

	private void ResetMeleeContactState()
	{
		_hasPreviousMeleeForward = false;
		_previousMeleeForward = Vector3.Zero;
		_latchedMeleeContactId = 0;
	}

	public override void _Process(double delta)
	{
		if (_cooldown > 0f)
			_cooldown -= (float)delta;

		EnsureDamageSmoke();
		_damageSmoke?.SetHealth(
			CurrentHp,
			MaxHp,
			EquippedPart != null
			&& EquippedPart.VisualKind != "empty"
			&& Slot != PartSlot.PowerCore);
	}

	private void EnsureDamageSmoke()
	{
		if (_damageSmoke != null && GodotObject.IsInstanceValid(_damageSmoke))
			return;

		var scale = ResolveChassisClass() == MechChassisClass.Titan ? 1.8f : 1f;
		_damageSmoke = DamageSmoke.Create(scale);
		_damageSmoke.Position = new Vector3(0f, 0.35f, 0f);
		AddChild(_damageSmoke);
	}

	private void RebuildVisual()
	{
		if (_visual != null)
			MeshMat.QueueFreeSafe(_visual);
		_visual = null;

		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || IsDestroyed)
			return;

		_visual = PartVisualFactory.Create(EquippedPart, ResolveChassisClass());
		AddChild(_visual);
	}

	private MechChassisClass ResolveChassisClass()
	{
		var node = GetParent();
		while (node != null)
		{
			if (node is MechController mech)
				return mech.ChassisClass;
			node = node.GetParent();
		}

		return MechChassisClass.Standard;
	}
}
