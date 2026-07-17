using Godot;

namespace Mechanize;

public partial class Hardpoint : Node3D
{
	[Export] public PartSlot Slot { get; set; } = PartSlot.WeaponL;

	public PartData? EquippedPart { get; private set; }
	public AimMode EffectiveAimMode { get; private set; } = AimMode.Fixed;
	public bool IsWeapon => Slot is PartSlot.WeaponL or PartSlot.WeaponR;
	public bool IsDestroyed { get; private set; }
	public float CurrentHp { get; private set; }
	public float MaxHp { get; private set; }
	public float ComponentArmor { get; private set; }
	public int LimbCount { get; private set; } = 1;
	public int LimbsAlive { get; private set; } = 1;
	public bool IsLegPackage => Slot == PartSlot.Legs;
	public bool CanTakeDamage => EquippedPart != null && !IsDestroyed && EquippedPart.VisualKind != "empty";
	public bool CanFire => IsWeapon && EquippedPart != null && !IsDestroyed && EquippedPart.Damage > 0f;

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
	private Node3D? _visual;
	private float[] _limbHp = System.Array.Empty<float>();
	private float _limbMaxHp;

	public void Equip(PartData? part)
	{
		EquippedPart = part;
		EffectiveAimMode = AimMode.Fixed;
		IsDestroyed = false;

		if (part != null)
			EffectiveAimMode = part.AimMode;

		InitializeIntegrity();
		RebuildVisual();
	}

	public void InitializeIntegrity()
	{
		if (EquippedPart == null || EquippedPart.VisualKind == "empty")
		{
			MaxHp = 0f;
			CurrentHp = 0f;
			ComponentArmor = 0f;
			LimbCount = 0;
			LimbsAlive = 0;
			_limbHp = System.Array.Empty<float>();
			return;
		}

		ComponentArmor = EquippedPart.ComponentArmor > 0f
			? EquippedPart.ComponentArmor
			: EquippedPart.Armor * 0.35f;

		if (IsLegPackage)
		{
			LimbCount = EquippedPart.LegType == LegType.Hexapod ? 6 : 2;
			_limbMaxHp = EquippedPart.ComponentMaxHp > 0f
				? EquippedPart.ComponentMaxHp
				: 28f + EquippedPart.Armor * 0.9f;
			_limbHp = new float[LimbCount];
			for (var i = 0; i < LimbCount; i++)
				_limbHp[i] = _limbMaxHp;
			LimbsAlive = LimbCount;
			MaxHp = _limbMaxHp * LimbCount;
			CurrentHp = MaxHp;
		}
		else
		{
			LimbCount = 1;
			LimbsAlive = 1;
			MaxHp = EquippedPart.ComponentMaxHp > 0f
				? EquippedPart.ComponentMaxHp
				: 35f + EquippedPart.Armor * 1.25f;
			_limbMaxHp = MaxHp;
			_limbHp = new[] { MaxHp };
			CurrentHp = MaxHp;
		}

		IsDestroyed = false;
	}

	public void Clear()
	{
		EquippedPart = null;
		EffectiveAimMode = AimMode.Fixed;
		IsDestroyed = false;
		CurrentHp = 0f;
		MaxHp = 0f;
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
			CurrentHp = Mathf.Max(0f, CurrentHp - mitigated);
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
			CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
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
		CurrentHp = sum;
	}

	public void MarkDestroyed()
	{
		IsDestroyed = true;
		CurrentHp = 0f;
		LimbsAlive = 0;
		for (var i = 0; i < _limbHp.Length; i++)
			_limbHp[i] = 0f;
		Visible = false;
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
		var toTarget = aimPoint - muzzle;
		Vector3 direction;
		if (toTarget.LengthSquared() < 0.01f)
		{
			direction = -GlobalTransform.Basis.Z;
		}
		else
		{
			// Allow enough pitch to hit short fodder / tall mechs without sniping the sky.
			direction = toTarget.Normalized();
			var flat = new Vector3(direction.X, 0f, direction.Z);
			if (flat.LengthSquared() > 0.001f)
			{
				flat = flat.Normalized();
				var pitch = Mathf.Clamp(direction.Y, -0.35f, 0.25f);
				direction = new Vector3(flat.X, pitch, flat.Z).Normalized();
			}
		}

		var projectile = Projectile.Create();
		projectile.Source = source;
		projectile.SourceTeam = source is MechController mechSource ? mechSource.Team : TeamUtil.GetTeam(source);
		projectile.Damage = EquippedPart.Damage;
		projectile.Velocity = direction * EquippedPart.ProjectileSpeed;
		projectile.Lifetime = EquippedPart.Range / Mathf.Max(1f, EquippedPart.ProjectileSpeed);
		projectile.TargetingMode = EquippedPart.TargetingMode;
		projectile.PreferredSlot = EquippedPart.TargetingMode == TargetingMode.AimedComponent
			? aimedSlot
			: null;

		parentForProjectile.AddChild(projectile);
		projectile.GlobalPosition = muzzle;
		projectile.LookAt(muzzle + direction, Vector3.Up);

		SfxService.Play("weapon_fire", (float)GD.RandRange(0.92, 1.08), -3f);

		damageDealt = EquippedPart.Damage;
		heatGenerated = EquippedPart.HeatPerShot;
		return true;
	}

	public override void _Process(double delta)
	{
		if (_cooldown > 0f)
			_cooldown -= (float)delta;
	}

	private void RebuildVisual()
	{
		_visual?.QueueFree();
		_visual = null;

		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || IsDestroyed)
			return;

		_visual = PartVisualFactory.Create(EquippedPart);
		AddChild(_visual);
	}
}
