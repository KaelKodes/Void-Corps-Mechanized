using Godot;

namespace Mechanize;

public partial class Hardpoint : Node3D
{
	[Export] public PartSlot Slot { get; set; } = PartSlot.WeaponL;

	private static readonly RandomNumberGenerator SpreadRng = new();

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
	/// <summary>Active part mesh root, if any (never a QueueFree'd leftover).</summary>
	public Node3D? Visual =>
		_visual != null && GodotObject.IsInstanceValid(_visual) && !_visual.IsQueuedForDeletion()
			? _visual
			: null;
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

	/// <summary>True when this hardpoint runs a ballistic magazine.</summary>
	public bool UsesMagazine =>
		EquippedPart is { WeaponFamily: WeaponFamily.Ballistic, MagazineSize: > 0 };

	public int AmmoInMag => _ammoInMag;
	public int MagazineCapacity => _magazineCapacity;
	public bool IsReloading => _reloadRemaining > 0.01f;
	public float ReloadRemaining => Mathf.Max(0f, _reloadRemaining);

	/// <summary>1 = full mobility, 0 = immobilized.</summary>
	public float MobilityFactor
	{
		get
		{
			if (!IsLegPackage)
				return IsDestroyed ? 0f : 1f;
			if (IsDestroyed || LimbsAlive <= 0 || MaxHp <= 0.01f)
				return 0f;

			// Shared package pool (no per-limb hitboxes yet): limp with damage,
			// immobilize only when the whole package is wrecked.
			var ratio = Mathf.Clamp(CurrentHp / MaxHp, 0f, 1f);
			return Mathf.Lerp(0.4f, 1f, ratio);
		}
	}

	private float _cooldown;
	private int _ammoInMag;
	private int _magazineCapacity;
	private float _reloadRemaining;
	private float _reloadDuration;
	private Vector3 _previousMeleeForward;
	private bool _hasPreviousMeleeForward;
	private ulong _latchedMeleeContactId;
	private bool _forcedMeleeSwing;
	private bool _forcedMeleeDamageActive;
	private bool _suppressMeleeDamageThisFrame;
	private float _forcedMeleeTime;
	private float _forcedMeleeDuration;
	private Vector3 _forcedMeleeBaseForward = Vector3.Forward;
	private Vector3 _forcedMeleeStartForward = Vector3.Forward;
	private Node3D? _visual;
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
		ResetMagazineState();

		if (part != null)
			EffectiveAimMode = part.AimMode;

		InitializeIntegrity();
		RebuildVisual();
	}

	/// <summary>Equip and immediately apply a stored condition snapshot.</summary>
	public void Equip(PartData? part, PartCondition? condition)
	{
		Equip(part);
		if (condition != null)
			RestoreCondition(condition);
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
			return;
		}

		ComponentArmor = Mathf.Max(0f, EquippedPart.Armor);

		// Legs keep a visual limb count (biped/tracks/hex) but one shared structure pool.
		// StructureHp is the per-limb budget; package MaxHp = StructureHp × limbs.
		LimbCount = IsLegPackage
			? (EquippedPart.LegType == LegType.Hexapod ? 6 : 2)
			: 1;
		_maxHp = Mathf.Max(1f, EquippedPart.StructureHp) * LimbCount;
		_currentHp = _maxHp;
		LimbsAlive = LimbCount;
		IsDestroyed = false;
	}

	public PartCondition CaptureCondition()
	{
		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || MaxHp <= 0.01f)
			return PartCondition.Full();

		if (IsDestroyed)
			return PartCondition.DestroyedState();

		var ratio = Mathf.Clamp(CurrentHp / MaxHp, 0f, 1f);
		return new PartCondition { Segments = [ratio], Destroyed = ratio <= 0.001f };
	}

	public void RestoreCondition(PartCondition condition)
	{
		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || MaxHp <= 0.01f)
			return;

		// Legacy multi-limb saves collapse via AverageRatio into the shared package pool.
		condition.EnsureSegmentCount(1);

		if (condition.Destroyed || condition.AverageRatio <= 0.001f)
		{
			MarkDestroyed();
			return;
		}

		IsDestroyed = false;
		var hp = Mathf.Clamp(condition.AverageRatio, 0.01f, 1f) * MaxHp;
		if (_externalHealth != null)
		{
			_externalHealth.ResetHealth(MaxHp);
			var missing = MaxHp - hp;
			if (missing > 0.01f)
				_externalHealth.ApplyDamage(missing);
		}
		else
		{
			_currentHp = hp;
		}

		LimbsAlive = CurrentHp > 0f ? LimbCount : 0;
		if (CurrentHp <= 0.01f)
			MarkDestroyed();
		else
			RebuildVisual();
	}

	/// <summary>
	/// Make an external health node authoritative for this component.
	/// The torso uses this to preserve existing AI, network, telemetry, and death hooks.
	/// </summary>
	public void BindExternalHealth(Damageable health)
	{
		_externalHealth = health;
		// Preserve whatever InitializeIntegrity / RestoreCondition already set.
		var target = Mathf.Max(1f, _maxHp);
		var current = Mathf.Clamp(_currentHp > 0f ? _currentHp : target, 0f, target);
		health.ResetHealth(target);
		var missing = target - current;
		if (missing > 0.01f)
			health.ApplyDamage(missing);
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
		RebuildVisual();
	}

	/// <summary>
	/// Returns true if the whole hardpoint/package was destroyed.
	/// <paramref name="limbsLostThisHit"/> is 1 only when the package collapses (legacy signal).
	/// </summary>
	public bool ApplyComponentDamage(float rawDamage, out int limbsLostThisHit)
	{
		limbsLostThisHit = 0;
		if (!CanTakeDamage)
			return false;

		var mitigated = rawDamage * (50f / (50f + Mathf.Max(0f, ComponentArmor)));

		if (_externalHealth != null)
			_externalHealth.ApplyDamage(mitigated);
		else
			_currentHp = Mathf.Max(0f, _currentHp - mitigated);

		if (CurrentHp > 0f)
		{
			LimbsAlive = LimbCount;
			return false;
		}

		LimbsAlive = 0;
		limbsLostThisHit = IsLegPackage ? LimbCount : 1;
		return true;
	}

	public bool ApplyComponentDamage(float rawDamage) => ApplyComponentDamage(rawDamage, out _);

	/// <summary>Restore package HP. Does not revive fully destroyed hardpoints.</summary>
	public float ApplyHeal(float amount)
	{
		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || IsDestroyed || amount <= 0f || MaxHp <= 0f)
			return 0f;

		var before = CurrentHp;
		if (_externalHealth != null)
			_externalHealth.ApplyHeal(amount);
		else
			_currentHp = Mathf.Min(_maxHp, _currentHp + amount);
		LimbsAlive = CurrentHp > 0f ? LimbCount : 0;
		return CurrentHp - before;
	}

	public void MarkDestroyed()
	{
		IsDestroyed = true;
		if (_externalHealth != null && !_externalHealth.IsDead)
			_externalHealth.ApplyDamage(_externalHealth.CurrentHealth + 1f);
		_currentHp = 0f;
		LimbsAlive = 0;
		if (_visual != null)
		{
			MeshMat.QueueFreeSafe(_visual);
			_visual = null;
		}
	}

	/// <summary>
	/// Third-person gimbal body block: total ~215° yaw, outward-biased so the arm
	/// cannot fold through the torso. Inward ~50°, outward ~165° from chassis forward.
	/// </summary>
	private const float GimbalInwardYaw = 0.873f;   // 50°
	private const float GimbalOutwardYaw = 2.880f;  // 165°

	public void AimAt(
		Vector3 worldPoint,
		Vector3 chassisForward,
		float elevationPitchRadians = 0f,
		bool clampGimbalBodyArc = false)
	{
		// Held shields use the cover pose driver — never LookAt.
		if (EquippedPart is { IsHeldShield: true })
			return;

		if (!CanFire)
			return;

		if (EquippedPart?.WeaponFamily == WeaponFamily.Melee && _forcedMeleeSwing)
		{
			AimForcedMeleeSwing();
			return;
		}

		var allowsElevation = EquippedPart is { AllowsFireElevation: true };
		var appliedPitch = allowsElevation ? elevationPitchRadians : 0f;

		if (EffectiveAimMode == AimMode.Gimbaled)
		{
			var to = worldPoint - GlobalPosition;
			if (to.LengthSquared() <= 0.01f)
				return;

			var horiz = Mathf.Sqrt(to.X * to.X + to.Z * to.Z);
			var flat = horiz > 0.001f
				? new Vector3(to.X, 0f, to.Z).Normalized()
				: Flatten(chassisForward);
			if (clampGimbalBodyArc)
				flat = ClampGimbalYawToBodyArc(flat, chassisForward, Slot);
			var pitch = horiz > 0.001f ? Mathf.Atan2(to.Y, horiz) : 0f;
			// Scroll elev (3rd person) stacks on mouse/world aim pitch.
			pitch = Mathf.Clamp(pitch + appliedPitch, -0.55f, 0.45f);
			LookAt(GlobalPosition + PitchedForward(flat, pitch) * 10f, Vector3.Up);
		}
		else
		{
			var forward = Flatten(chassisForward);
			if (forward.LengthSquared() < 0.001f)
				forward = Flatten(-GlobalTransform.Basis.Z);
			if (forward.LengthSquared() < 0.001f)
				forward = Vector3.Forward;

			var pitch = appliedPitch;
			if (allowsElevation)
			{
				var to = worldPoint - GlobalPosition;
				var horiz = Mathf.Sqrt(to.X * to.X + to.Z * to.Z);
				if (horiz > 0.01f)
					pitch = Mathf.Clamp(Mathf.Atan2(to.Y, horiz) + appliedPitch, -0.55f, 0.45f);
			}

			LookAt(GlobalPosition + PitchedForward(forward, pitch) * 10f, Vector3.Up);
		}
	}

	/// <summary>
	/// Clamps horizontal aim so a right arm prefers outward (+yaw) and a left arm
	/// prefers outward (−yaw); neither may swing deep through the chassis.
	/// </summary>
	private static Vector3 ClampGimbalYawToBodyArc(Vector3 aimFlat, Vector3 chassisForward, PartSlot slot)
	{
		var forward = Flatten(chassisForward);
		if (forward.LengthSquared() < 0.001f)
			return aimFlat;

		var right = forward.Cross(Vector3.Up);
		if (right.LengthSquared() < 0.001f)
			return aimFlat;
		right = right.Normalized();

		var yaw = Mathf.Atan2(aimFlat.Dot(right), aimFlat.Dot(forward));
		yaw = slot switch
		{
			PartSlot.WeaponR => Mathf.Clamp(yaw, -GimbalInwardYaw, GimbalOutwardYaw),
			PartSlot.WeaponL => Mathf.Clamp(yaw, -GimbalOutwardYaw, GimbalInwardYaw),
			_ => yaw
		};

		return (forward * Mathf.Cos(yaw) + right * Mathf.Sin(yaw)).Normalized();
	}

	private static Vector3 Flatten(Vector3 v)
	{
		v.Y = 0f;
		return v.LengthSquared() > 0.001f ? v.Normalized() : Vector3.Zero;
	}

	private static Vector3 PitchedForward(Vector3 flatForward, float pitchRadians)
	{
		flatForward.Y = 0f;
		if (flatForward.LengthSquared() < 0.001f)
			flatForward = Vector3.Forward;
		flatForward = flatForward.Normalized();
		var right = flatForward.Cross(Vector3.Up).Normalized();
		return flatForward.Rotated(right, pitchRadians).Normalized();
	}

	/// <summary>
	/// Starts the fire-button melee attack. The attack captures its heading so this arm
	/// can wind back and sweep while another gimbaled arm continues following the cursor.
	/// Passive cursor flailing remains available whenever this animation is idle.
	/// </summary>
	public bool TryStartMeleeSwing(Vector3 aimPoint, float fireRateMultiplier, float heatThrottle)
	{
		if (!CanFire
		    || EquippedPart == null
		    || EquippedPart.WeaponFamily != WeaponFamily.Melee
		    || _forcedMeleeSwing
		    || _cooldown > 0f)
			return false;

		var baseForward = aimPoint - GlobalPosition;
		baseForward.Y = 0f;
		if (baseForward.LengthSquared() < 0.01f)
			baseForward = -GlobalTransform.Basis.Z;
		baseForward.Y = 0f;
		if (baseForward.LengthSquared() < 0.01f)
			baseForward = Vector3.Forward;

		var startForward = -GlobalTransform.Basis.Z;
		startForward.Y = 0f;
		if (startForward.LengthSquared() < 0.01f)
			startForward = baseForward;

		var rate = EquippedPart.FireRate
		           * Mathf.Max(0.1f, fireRateMultiplier)
		           * Mathf.Max(0.2f, heatThrottle);
		_forcedMeleeDuration = Mathf.Clamp(0.82f / Mathf.Max(0.1f, rate), 0.5f, 1.25f);
		_forcedMeleeTime = 0f;
		_forcedMeleeBaseForward = baseForward.Normalized();
		_forcedMeleeStartForward = startForward.Normalized();
		_forcedMeleeDamageActive = false;
		_suppressMeleeDamageThisFrame = true;
		_forcedMeleeSwing = true;
		return true;
	}

	/// <summary>Advances the click attack from the owning mech's physics tick.</summary>
	public void AdvanceMeleeSwing(float dt)
	{
		if (!_forcedMeleeSwing)
			return;
		_forcedMeleeTime += Mathf.Max(0f, dt);
	}

	private void AimForcedMeleeSwing()
	{
		var progress = Mathf.Clamp(
			_forcedMeleeTime / Mathf.Max(0.01f, _forcedMeleeDuration),
			0f,
			1f);
		var windupDir = RotateFlat(_forcedMeleeBaseForward, Mathf.DegToRad(-78f));
		var finishDir = RotateFlat(_forcedMeleeBaseForward, Mathf.DegToRad(82f));
		Vector3 direction;

		if (progress < 0.24f)
		{
			var t = SmoothStep(progress / 0.24f);
			direction = _forcedMeleeStartForward.Slerp(windupDir, t).Normalized();
			_forcedMeleeDamageActive = false;
			_suppressMeleeDamageThisFrame = true;
		}
		else if (progress < 0.78f)
		{
			var wasActive = _forcedMeleeDamageActive;
			var t = SmoothStep((progress - 0.24f) / 0.54f);
			direction = windupDir.Slerp(finishDir, t).Normalized();
			_forcedMeleeDamageActive = true;
			_suppressMeleeDamageThisFrame = false;
			if (!wasActive)
			{
				_previousMeleeForward = windupDir;
				_hasPreviousMeleeForward = true;
				_latchedMeleeContactId = 0;
			}
		}
		else
		{
			var t = SmoothStep((progress - 0.78f) / 0.22f);
			direction = finishDir.Slerp(_forcedMeleeBaseForward, t).Normalized();
			_forcedMeleeDamageActive = false;
			_suppressMeleeDamageThisFrame = true;
		}

		LookAt(GlobalPosition + direction, Vector3.Up);

		if (progress >= 1f)
		{
			_forcedMeleeSwing = false;
			_forcedMeleeDamageActive = false;
		}
	}

	private static float SmoothStep(float t)
	{
		t = Mathf.Clamp(t, 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	private static Vector3 RotateFlat(Vector3 direction, float radians)
	{
		var c = Mathf.Cos(radians);
		var s = Mathf.Sin(radians);
		return new Vector3(
			direction.X * c - direction.Z * s,
			0f,
			direction.X * s + direction.Z * c).Normalized();
	}

	private static Vector3 ApplyAimSpread(Vector3 direction, float spreadRadians)
	{
		if (direction.LengthSquared() < 0.0001f)
			return Vector3.Forward;

		direction = direction.Normalized();
		var yaw = SpreadRng.RandfRange(-spreadRadians, spreadRadians);
		var pitch = SpreadRng.RandfRange(-spreadRadians * 0.55f, spreadRadians * 0.55f);

		var flat = new Vector3(direction.X, 0f, direction.Z);
		if (flat.LengthSquared() > 0.0001f)
		{
			flat = flat.Normalized();
			direction = RotateFlat(flat, yaw);
			direction = new Vector3(direction.X, Mathf.Clamp(direction.Y + pitch, -0.35f, 0.25f), direction.Z)
				.Normalized();
		}

		return direction;
	}

	public bool TryFire(
		Node source,
		Node parentForProjectile,
		float fireRateMultiplier,
		float heatThrottle,
		Vector3 aimPoint,
		PartSlot? aimedSlot,
		out float damageDealt,
		out float heatGenerated,
		bool forcePreferredSlot = false)
	{
		damageDealt = 0f;
		heatGenerated = 0f;
		if (!CanFire || EquippedPart == null)
			return false;
		if (_cooldown > 0f)
			return false;
		if (UsesMagazine && (IsReloading || _ammoInMag <= 0))
			return false;

		var rate = EquippedPart.FireRate * Mathf.Max(0.1f, fireRateMultiplier) * Mathf.Max(0.05f, heatThrottle);
		_cooldown = 1f / Mathf.Max(0.1f, rate);

		var allowsElevation = EquippedPart.AllowsFireElevation;

		var muzzle = GlobalPosition + (-GlobalTransform.Basis.Z) * 1.2f;
		// Riding a tall carrier: keep the visual mount elevated, but spawn shots at the
		// height they'd have on the ground so they still meet tanks / other MAPs.
		if (source is MechController { IsCarrierMounted: true } rider)
			muzzle.Y -= rider.CarrierCombatLift;

		Vector3 direction;
		if (allowsElevation)
		{
			// Fire down the pitched barrel — elevation comes from gun aim, not a spawn-plane cheat.
			direction = -GlobalTransform.Basis.Z;
			if (direction.LengthSquared() < 0.001f)
				direction = Vector3.Forward;
			else
				direction = direction.Normalized();
		}
		else if (EffectiveAimMode == AimMode.Fixed)
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

		if (source is MechController mechSpread && mechSpread.AimSpreadRadians > 0.0001f)
			direction = ApplyAimSpread(direction, mechSpread.AimSpreadRadians);

		var speed = EquippedPart.ProjectileSpeed;
		var velocity = direction * speed;
		var lifetime = EquippedPart.Range / Mathf.Max(1f, speed);
		var preferSlot = aimedSlot.HasValue
			&& (forcePreferredSlot
			    || (EffectiveAimMode == AimMode.Gimbaled
			        && EquippedPart.TargetingMode == TargetingMode.AimedComponent));
		var preferred = preferSlot && aimedSlot.HasValue ? (int)aimedSlot.Value : -1;
		var targeting = preferSlot ? TargetingMode.AimedComponent : EquippedPart.TargetingMode;
		var team = source is MechController mechSource ? mechSource.Team : TeamUtil.GetTeam(source);

		var style = ProjectileStyleUtil.FromPart(EquippedPart);
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
				targeting,
				preferred,
				style,
				gravity: 0f,
				visualScale: Projectile.MechVisualScale);
		}
		else
		{
			var projectile = Projectile.Create(style, Projectile.MechVisualScale);
			projectile.Source = source;
			projectile.SourceTeam = team;
			projectile.Damage = EquippedPart.Damage;
			projectile.Velocity = velocity;
			projectile.Lifetime = lifetime;
			projectile.TargetingMode = targeting;
			projectile.PreferredSlot = preferred >= 0 ? (PartSlot)preferred : null;
			parentForProjectile.AddChild(projectile);
			projectile.GlobalPosition = muzzle;
			projectile.LookAt(muzzle + direction, Vector3.Up);
		}

		var localPilot = source is MechController { IsLocalPilot: true };
		SfxService.PlayBallisticFire(
			EquippedPart,
			-3f,
			origin: muzzle,
			fullVolume: localPilot);

		if (UsesMagazine)
			_ammoInMag = Mathf.Max(0, _ammoInMag - 1);

		damageDealt = EquippedPart.Damage;
		heatGenerated = EquippedPart.HeatPerShot;
		return true;
	}

	/// <summary>
	/// Configure magazine capacity from the gun + chassis utility bonuses, then fill.
	/// Call after stats rebuild so MagazineBonus applies.
	/// </summary>
	public void ConfigureMagazine(int magazineBonus = 0)
	{
		if (EquippedPart is not { WeaponFamily: WeaponFamily.Ballistic, MagazineSize: > 0 } part)
		{
			_magazineCapacity = 0;
			_ammoInMag = 0;
			_reloadRemaining = 0f;
			_reloadDuration = 0f;
			return;
		}

		var capacity = Mathf.Max(1, part.MagazineSize + Mathf.Max(0, magazineBonus));
		var keepRatio = _magazineCapacity > 0
			? Mathf.Clamp(_ammoInMag / (float)_magazineCapacity, 0f, 1f)
			: 1f;
		_magazineCapacity = capacity;
		_ammoInMag = Mathf.Clamp(Mathf.RoundToInt(capacity * keepRatio), 0, capacity);
		if (_ammoInMag <= 0 && !IsReloading)
			_ammoInMag = capacity;
	}

	/// <summary>
	/// Start a reload if this is a ballistic mag gun that isn't already full / reloading.
	/// </summary>
	public bool TryBeginReload(float reloadSpeedBonus = 0f)
	{
		if (!UsesMagazine || EquippedPart == null || IsDestroyed)
			return false;
		if (IsReloading)
			return false;
		if (_ammoInMag >= _magazineCapacity && _magazineCapacity > 0)
			return false;

		var baseTime = Mathf.Max(0.35f, EquippedPart.ReloadTime);
		_reloadDuration = baseTime / Mathf.Max(0.25f, 1f + Mathf.Max(0f, reloadSpeedBonus));
		_reloadRemaining = _reloadDuration;
		SfxService.PlayBallisticReload(EquippedPart, -5f);
		return true;
	}

	private void ResetMagazineState()
	{
		_reloadRemaining = 0f;
		_reloadDuration = 0f;
		if (EquippedPart is { WeaponFamily: WeaponFamily.Ballistic, MagazineSize: > 0 } part)
		{
			_magazineCapacity = Mathf.Max(1, part.MagazineSize);
			_ammoInMag = _magazineCapacity;
		}
		else
		{
			_magazineCapacity = 0;
			_ammoInMag = 0;
		}
	}

	private void TickMagazine(float dt)
	{
		if (!UsesMagazine || _reloadRemaining <= 0f)
			return;

		_reloadRemaining = Mathf.Max(0f, _reloadRemaining - dt);
		if (_reloadRemaining > 0f)
			return;

		_ammoInMag = _magazineCapacity;
		// Chamber clack is the start of reload; finish stays quiet so we don't double-up.
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

		// The click attack's windup/recovery are animation only. Its forward sweep
		// uses the same physical contact tracing as free cursor flailing.
		if (_forcedMeleeSwing && !_forcedMeleeDamageActive || _suppressMeleeDamageThisFrame)
		{
			_previousMeleeForward = currentForward;
			_hasPreviousMeleeForward = true;
			_latchedMeleeContactId = 0;
			_suppressMeleeDamageThisFrame = false;
			return false;
		}

		if (!_hasPreviousMeleeForward)
		{
			_previousMeleeForward = currentForward;
			_hasPreviousMeleeForward = true;
		}

		var origin = GetMeleeOrigin(source);
		var reach = Mathf.Max(2.6f, EquippedPart.Range);
		var sweepHit = TraceMeleeSweep(source, origin, _previousMeleeForward, currentForward, reach);
		var currentHit = TraceMeleeBlade(source, origin, currentForward, reach);
		// Prefer the contact the blade is actively crossing; fall back to where it rests.
		var contactHit = sweepHit.Count > 0 ? sweepHit : currentHit;
		_previousMeleeForward = currentForward;

		var currentId = ContactId(currentHit);
		var contactId = ContactId(contactHit);
		var isNewContact = contactId != 0 && contactId != _latchedMeleeContactId;
		var canDriveCut = _cooldown <= 0f;

		if (isNewContact && canDriveCut)
		{
			var rate = Mathf.Max(0.1f, EquippedPart.FireRate);
			_cooldown = 1f / rate;
			heatGenerated = EquippedPart.HeatPerShot;
			dealtDamage = ApplyMeleeContact(source, contactHit, aimedSlot);
		}

		// Remaining in contact cannot machine-gun damage. Pull the blade clear, then
		// cross the object again (subject to contact cadence) to cut again.
		_latchedMeleeContactId = currentId != 0 ? currentId : contactId;
		return isNewContact && canDriveCut;
	}

	private Vector3 GetMeleeOrigin(Node source)
	{
		var origin = GlobalPosition;
		// Same height correction as gun muzzles: visual ride height shouldn't miss MAP hulls.
		if (source is MechController { IsCarrierMounted: true } rider)
			origin.Y -= rider.CarrierCombatLift;
		return origin;
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
		// Volume approx of the visible blade: width, thickness, and a downward bias so
		// elevated sockets still meet MAP hulls / short cover instead of only tall props.
		var right = direction.Cross(Vector3.Up);
		if (right.LengthSquared() < 0.001f)
			right = GlobalTransform.Basis.X;
		right = right.Normalized();
		var up = Vector3.Up;

		Godot.Collections.Dictionary? closest = null;
		var closestDistance = float.MaxValue;
		foreach (var along in new[] { 0.15f, 0.45f, 0.75f })
		{
			foreach (var lateral in new[] { 0f, -0.28f, 0.28f })
			{
				foreach (var vertical in new[] { 0.15f, -0.35f, -0.75f })
				{
					var rayOrigin = origin
						+ direction * (along * 0.45f * reach)
						+ right * lateral
						+ up * vertical;
					var to = origin + direction * reach + right * lateral + up * (vertical * 0.35f);
					var rayDir = to - rayOrigin;
					if (rayDir.LengthSquared() < 0.01f)
						continue;
					var rayLength = rayDir.Length();
					rayDir /= rayLength;

					var hit = TraceMeleeRay(source, rayOrigin, rayDir, rayLength);
					if (hit.Count == 0)
						continue;
					var impact = hit["position"].AsVector3();
					var distance = rayOrigin.DistanceSquaredTo(impact);
					if (distance >= closestDistance)
						continue;
					closestDistance = distance;
					closest = hit;
				}
			}
		}

		if (closest != null)
			return closest;

		// Forgiving fallback: any hostile MAP inside a short forward capsule.
		return TraceMeleeProximity(source, origin, direction, reach);
	}

	private Godot.Collections.Dictionary TraceMeleeProximity(
		Node source,
		Vector3 origin,
		Vector3 direction,
		float reach)
	{
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return new Godot.Collections.Dictionary();

		var tip = origin + direction * reach;
		var excludes = new Godot.Collections.Array<Rid>();
		if (source is CollisionObject3D sourceBody)
			excludes.Add(sourceBody.GetRid());

		var query = new PhysicsShapeQueryParameters3D
		{
			CollisionMask = PhysicsLayers.Mechs,
			CollideWithAreas = false,
			CollideWithBodies = true,
			Exclude = excludes,
			Transform = new Transform3D(Basis.Identity, tip),
			Shape = new SphereShape3D { Radius = 0.85f }
		};

		var hits = space.IntersectShape(query, 8);
		Godot.Collections.Dictionary? best = null;
		var bestDist = float.MaxValue;
		foreach (var hit in hits)
		{
			var collider = hit["collider"].AsGodotObject() as Node;
			if (collider == null)
				continue;
			if (collider == source || source.IsAncestorOf(collider) || collider.IsAncestorOf(source))
				continue;

			var mech = FindMech(collider);
			if (mech == null)
				continue;

			var sourceTeam = source is MechController sm ? sm.Team : TeamUtil.GetTeam(source);
			if (sourceTeam != TeamId.Neutral && mech.Team != TeamId.Neutral
			    && !TeamUtil.IsHostile(sourceTeam, mech.Team))
				continue;

			var point = TeamUtil.GetAimPoint(mech);
			var dist = tip.DistanceSquaredTo(point);
			if (dist >= bestDist)
				continue;
			bestDist = dist;
			best = new Godot.Collections.Dictionary
			{
				["collider"] = collider,
				["position"] = point
			};
		}

		return best ?? new Godot.Collections.Dictionary();
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
		{
			// Solid cover with no Damageable API — still a metal contact.
			SfxService.PlayImpactCover(impact, -4f);
			return false;
		}

		damageable.ApplyDamage(EquippedPart.Damage);
		SfxService.PlayImpactCover(impact, -3f);
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
		_forcedMeleeSwing = false;
		_forcedMeleeDamageActive = false;
		_suppressMeleeDamageThisFrame = false;
		_forcedMeleeTime = 0f;
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		if (_cooldown > 0f)
			_cooldown -= dt;
		TickMagazine(dt);

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
		var offset = ResolveDamageSmokeLocalOffset();
		if (_damageSmoke != null && GodotObject.IsInstanceValid(_damageSmoke))
		{
			_damageSmoke.Position = offset;
			return;
		}

		var scale = ResolveChassisClass() == MechChassisClass.Titan ? 1.8f : 1f;
		_damageSmoke = DamageSmoke.Create(scale);
		_damageSmoke.Position = offset;
		AddChild(_damageSmoke);
	}

	/// <summary>
	/// Cockpit hulls are hollow around the hardpoint origin — vent smoke on the
	/// roof/aft exterior so FP pilots aren't fogged out of their own cabin.
	/// </summary>
	private Vector3 ResolveDamageSmokeLocalOffset()
	{
		if (Slot == PartSlot.Torso && EquippedPart?.IsCockpitHull == true)
			return new Vector3(0f, 1.05f, 0.45f);

		return new Vector3(0f, 0.35f, 0f);
	}

	private void RebuildVisual()
	{
		RebuildVisualForCockpit(UsesEncasedPowerCore());
	}

	public void RebuildVisualForCockpit(bool encasedPowerCore)
	{
		if (_visual != null)
			MeshMat.QueueFreeSafe(_visual);
		_visual = null;

		if (EquippedPart == null || EquippedPart.VisualKind == "empty" || IsDestroyed)
			return;

		_visual = PartVisualFactory.Create(EquippedPart, ResolveChassisClass(), encasedPowerCore);
		AddChild(_visual);
	}

	private bool UsesEncasedPowerCore()
	{
		if (Slot != PartSlot.PowerCore)
			return false;

		var node = GetParent();
		while (node != null)
		{
			if (node is MechController mech && mech.Assembler != null)
			{
				var torso = mech.Assembler.Hardpoints.GetValueOrDefault(PartSlot.Torso)?.EquippedPart;
				return torso?.IsCockpitHull == true;
			}
			node = node.GetParent();
		}

		return false;
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
