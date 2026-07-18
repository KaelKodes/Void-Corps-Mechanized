using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Tactile AI pilot: uses real turn rates, aim modes, limb mobility, projectile lead,
/// cover checks, and loadout-aware weapon choices. No snapped facing or cheat weapons.
/// Hard preserves the original lethal profile; Easy/Medium dial the same brain down.
/// </summary>
public partial class MechPilotAI : Node
{
	[Export] public PilotDifficulty Difficulty { get; set; } = PilotDifficulty.Medium;
	[Export] public float PreferredRange { get; set; } = 22f;
	[Export] public float Aggression { get; set; } = 0.7f;
	[Export] public float AimNoiseDegrees { get; set; } = 4.5f;
	[Export] public float ReactionTime { get; set; } = 0.28f;

	/// <summary>1 = full projectile lead (Hard). Lower = more misses on movers.</summary>
	[Export] public float LeadFactor { get; set; } = 1f;

	/// <summary>How often the pilot is allowed to fire while "bursting".</summary>
	[Export] public float FireDiscipline { get; set; } = 1f;

	[Export] public bool UseSharpshooter { get; set; } = true;
	[Export] public bool UseAbilitiesAggressively { get; set; } = true;

	private MechController? _mech;
	private Node3D? _target;
	private PilotTacticalState _state = PilotTacticalState.Hunt;
	private float _stateTimer;
	private float _acquireTimer;
	private float _rethinkTimer;
	private float _strafeSign = 1f;
	private float _burstTimer;
	private float _burstCooldown;
	private bool _bursting;
	private float _burstLengthScale = 1f;
	private float _burstGapScale = 1f;
	private float _rethinkScale = 1f;
	private float _facingDotMin = 0.72f;
	private readonly RandomNumberGenerator _rng = new();
	private Vector3 _aimJitter;
	private Vector3 _lastPos;
	private float _stuckTimer;
	private Vector3? _obstacleAim;
	private float _flankCommit;

	public PilotTacticalState State => _state;
	public Node3D? Target => _target;

	public override void _Ready()
	{
		_rng.Randomize();
		_mech = GetParentOrNull<MechController>();
		_strafeSign = _rng.Randf() > 0.5f ? 1f : -1f;
		ApplyDifficulty(Difficulty);
		_rethinkTimer = _rng.RandfRange(0.6f, 1.4f) * _rethinkScale;
	}

	/// <summary>
	/// Hard == original lethal pilot. Medium/Easy keep the same tactics with worse hands.
	/// </summary>
	public void ApplyDifficulty(PilotDifficulty difficulty)
	{
		Difficulty = difficulty;
		switch (difficulty)
		{
			case PilotDifficulty.Easy:
				Aggression = 0.28f;
				AimNoiseDegrees = 18f;
				ReactionTime = 0.95f;
				LeadFactor = 0.15f;
				FireDiscipline = 0.45f;
				UseSharpshooter = false;
				UseAbilitiesAggressively = false;
				_burstLengthScale = 0.55f;
				_burstGapScale = 2.4f;
				_rethinkScale = 1.8f;
				_facingDotMin = 0.88f;
				PreferredRange = Mathf.Max(PreferredRange, 26f);
				break;

			case PilotDifficulty.Medium:
				Aggression = 0.5f;
				AimNoiseDegrees = 10f;
				ReactionTime = 0.55f;
				LeadFactor = 0.55f;
				FireDiscipline = 0.75f;
				UseSharpshooter = true;
				UseAbilitiesAggressively = false;
				_burstLengthScale = 0.8f;
				_burstGapScale = 1.45f;
				_rethinkScale = 1.25f;
				_facingDotMin = 0.8f;
				break;

			default: // Hard — preserved original profile
				Aggression = 0.78f;
				AimNoiseDegrees = 4.5f;
				ReactionTime = 0.28f;
				LeadFactor = 1f;
				FireDiscipline = 1f;
				UseSharpshooter = true;
				UseAbilitiesAggressively = true;
				_burstLengthScale = 1f;
				_burstGapScale = 1f;
				_rethinkScale = 1f;
				_facingDotMin = 0.72f;
				break;
		}
	}

	public void SetTarget(Node3D? target)
	{
		_target = target;
		_acquireTimer = ReactionTime;
	}

	public MechPilotCommand BuildCommand(float dt)
	{
		var cmd = new MechPilotCommand
		{
			AimPoint = _mech?.GlobalPosition - (_mech?.GlobalTransform.Basis.Z ?? Vector3.Forward) * 10f ?? Vector3.Zero,
			AimedComponent = null,
			Turn = 0f,
			Throttle = 0f,
			Move = Vector2.Zero,
			FirePrimary = false,
			FireSecondary = false,
			AbilityIndex = -1
		};

		if (_mech == null || !_mech.ControlsEnabled || _mech.Integrity?.IsCollapsed == true)
			return cmd;

		RefreshTarget();
		if (!TeamUtil.IsAliveCombatant(_target))
		{
			_state = PilotTacticalState.Hold;
			return cmd;
		}

		_acquireTimer = Mathf.Max(0f, _acquireTimer - dt);
		_stateTimer += dt;
		_rethinkTimer -= dt;
		if (_rethinkTimer <= 0f)
		{
			ChooseState();
			_rethinkTimer = _rng.RandfRange(0.7f, 1.6f) * _rethinkScale;
		}

		UpdateBurst(dt);

		var myPos = _mech.GlobalPosition;
		var theirPos = TeamUtil.GetAimPoint(_target!);
		var toTarget = theirPos - myPos;
		toTarget.Y = 0f;
		var distance = toTarget.Length();
		var dir = distance > 0.1f ? toTarget / distance : -_mech.GlobalTransform.Basis.Z;

		var eye = myPos + Vector3.Up * 1.4f;
		var hasLos = HasLineOfSight(eye, theirPos);
		_obstacleAim = null;
		if (!hasLos)
			_obstacleAim = FindDestructibleBlockerAim(eye, theirPos);

		UpdateStuck(dt, myPos, hasLos);
		_flankCommit = Mathf.Max(0f, _flankCommit - dt);

		cmd.AimPoint = _obstacleAim ?? ComputeAimPoint(dt, distance);

		var inVision = _mech.CanCombatId(theirPos);
		cmd.AimedComponent = (_obstacleAim == null && inVision) ? PickComponentTarget() : null;
		DriveMovement(ref cmd, dir, distance, hasLos);
		cmd.Sprint = ShouldSprint(distance, hasLos) && (_mech.Assembler?.Stats.CanSprint ?? false);
		ApplyHealBeaconBehavior(ref cmd);
		DecideWeapons(ref cmd, distance, hasLos, inVision);
		if (HealBeacon.FindBestFor(_mech) is { OfflineWhileHealing: true } offline
		    && offline.Contains(_mech.GlobalPosition)
		    && NeedsRepair())
		{
			cmd.FirePrimary = false;
			cmd.FireSecondary = false;
			cmd.Sprint = false;
		}

		DecideAbility(ref cmd, distance, hasLos);

		return cmd;
	}

	/// <summary>
	/// If damaged and a mend field is available, hold the pad and keep fighting from it
	/// until full or the field expires. Offline mend variants lock the unit down.
	/// </summary>
	private void ApplyHealBeaconBehavior(ref MechPilotCommand cmd)
	{
		if (_mech == null || !NeedsRepair())
			return;

		var beacon = HealBeacon.FindBestFor(_mech);
		if (beacon == null)
			return;

		var myPos = _mech.GlobalPosition;
		var toCenter = beacon.GlobalPosition - myPos;
		toCenter.Y = 0f;
		var dist = toCenter.Length();

		if (beacon.OfflineWhileHealing && beacon.Contains(myPos))
		{
			cmd.Move = Vector2.Zero;
			cmd.Throttle = 0f;
			cmd.Turn = 0f;
			cmd.Sprint = false;
			cmd.FirePrimary = false;
			cmd.FireSecondary = false;
			return;
		}

		if (!beacon.Contains(myPos))
		{
			// Approach the field; still allow weapons via DecideWeapons (already applied after — wait, DecideWeapons is AFTER this call)
			// So we only override movement here; weapons decided after. Good for approach.
			var dir = dist > 0.1f ? toCenter / dist : Vector3.Zero;
			ApplyMoveToward(ref cmd, dir, commit: true);
			cmd.Sprint = dist > beacon.Radius * 0.5f;
			return;
		}

		// Inside: stay near center, don't leave the radius.
		if (dist > beacon.Radius * 0.35f)
		{
			var dir = toCenter / Mathf.Max(0.1f, dist);
			ApplyMoveToward(ref cmd, dir, commit: false);
		}
		else
		{
			cmd.Move = Vector2.Zero;
			cmd.Throttle = 0f;
		}

		cmd.Sprint = false;
	}

	private void ApplyMoveToward(ref MechPilotCommand cmd, Vector3 worldDir, bool commit)
	{
		if (_mech == null)
			return;

		var locked = _mech.Assembler?.LegMode != LegMode.Gimbaled
			|| (_mech.Integrity?.LegMobilityFactor ?? 1f) < 0.5f;

		if (locked)
		{
			var forward = -_mech.GlobalTransform.Basis.Z;
			forward.Y = 0f;
			if (forward.LengthSquared() < 0.01f)
				forward = Vector3.Forward;
			forward = forward.Normalized();
			var right = forward.Cross(Vector3.Up).Normalized();
			var flat = worldDir;
			flat.Y = 0f;
			if (flat.LengthSquared() < 0.01f)
				return;
			flat = flat.Normalized();
			cmd.Turn = Mathf.Clamp(forward.Cross(flat).Y * 2.5f, -1f, 1f);
			cmd.Throttle = Mathf.Clamp(forward.Dot(flat), commit ? 0.35f : 0.15f, 1f);
			cmd.Move = Vector2.Zero;
		}
		else
		{
			var forward = -_mech.GlobalTransform.Basis.Z;
			forward.Y = 0f;
			forward = forward.LengthSquared() > 0.01f ? forward.Normalized() : Vector3.Forward;
			var right = forward.Cross(Vector3.Up).Normalized();
			var flat = worldDir;
			flat.Y = 0f;
			if (flat.LengthSquared() < 0.01f)
				return;
			flat = flat.Normalized();
			cmd.Move = new Vector2(flat.Dot(right), -flat.Dot(forward));
			cmd.Throttle = 0f;
			cmd.Turn = 0f;
		}
	}

	private bool NeedsRepair()
	{
		if (_mech == null)
			return false;
		if (_mech.Health != null && !_mech.Health.IsDead
		    && _mech.Health.CurrentHealth < _mech.Health.MaxHealth * 0.92f)
			return true;
		if (_mech.Assembler == null)
			return false;
		foreach (var hp in _mech.Assembler.Hardpoints.Values)
		{
			if (hp.CanTakeDamage && hp.CurrentHp < hp.MaxHp * 0.92f)
				return true;
		}
		return false;
	}

	private void UpdateStuck(float dt, Vector3 myPos, bool hasLos)
	{
		if (hasLos)
		{
			_stuckTimer = 0f;
			_lastPos = myPos;
			return;
		}

		var moved = myPos.DistanceTo(_lastPos);
		_lastPos = myPos;
		var speed = moved / Mathf.Max(dt, 0.001f);
		if (speed < 0.55f)
			_stuckTimer += dt;
		else
			_stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 0.5f);

		if (_stuckTimer > 1.25f)
		{
			_strafeSign *= -1f;
			_stuckTimer = 0f;
			_flankCommit = 2.2f;
		}
	}

	private void RefreshTarget()
	{
		if (TeamUtil.IsAliveCombatant(_target)
			&& _target != null
			&& TeamUtil.IsHostile(_mech!.Team, TeamUtil.GetTeam(_target)))
		{
			// Keep current mech target; otherwise allow reacquire toward higher priority.
			if (_target is MechController)
				return;
		}

		_target = null;
		var root = GetTree()?.CurrentScene;
		if (root == null || _mech == null)
			return;

		Node3D? bestMech = null;
		Node3D? bestSupport = null;
		Node3D? bestOther = null;
		var bestMechDist = float.MaxValue;
		var bestSupportDist = float.MaxValue;
		var bestOtherDist = float.MaxValue;
		var origin = _mech.GlobalPosition;

		foreach (var body in EnumerateCombatants(root))
		{
			if (body == _mech || !TeamUtil.IsAliveCombatant(body))
				continue;
			if (!TeamUtil.IsHostile(_mech.Team, TeamUtil.GetTeam(body)))
				continue;

			var dist = origin.DistanceTo(body.GlobalPosition);
			if (body is MechController)
			{
				if (dist < bestMechDist)
				{
					bestMechDist = dist;
					bestMech = body;
				}
			}
			else if (body is SupportUnit)
			{
				if (dist < bestSupportDist)
				{
					bestSupportDist = dist;
					bestSupport = body;
				}
			}
			else if (dist < bestOtherDist)
			{
				bestOtherDist = dist;
				bestOther = body;
			}
		}

		var pick = bestMech ?? bestSupport ?? bestOther;
		if (pick != null)
			SetTarget(pick);
	}

	private static System.Collections.Generic.IEnumerable<Node3D> EnumerateCombatants(Node root)
	{
		foreach (var child in root.GetChildren())
		{
			if (child is MechController or SupportUnit or EscortAsset)
				yield return (Node3D)child;

			if (child is Node3D group && group.Name == "MissionRuntime")
			{
				foreach (var nested in group.GetChildren())
				{
					if (nested is MechController or SupportUnit or EscortAsset)
						yield return (Node3D)nested;
				}
			}
		}
	}

	private void ChooseState()
	{
		if (_mech?.Health == null || _target == null)
		{
			_state = PilotTacticalState.Hold;
			return;
		}

		var hpRatio = _mech.Health.CurrentHealth / Mathf.Max(1f, _mech.Health.MaxHealth);
		var mobility = _mech.Integrity?.LegMobilityFactor ?? 1f;
		var distance = _mech.GlobalPosition.DistanceTo(_target.GlobalPosition);
		var los = HasLineOfSight(
			_mech.GlobalPosition + Vector3.Up * 1.4f,
			TeamUtil.GetAimPoint(_target));

		if (mobility < 0.3f || hpRatio < 0.22f)
		{
			_state = PilotTacticalState.BreakContact;
			_stateTimer = 0f;
			return;
		}

		if (!los)
		{
			// Never sit face-first into cover — flank or blast a hole.
			_state = PilotTacticalState.Hunt;
			_flankCommit = Mathf.Max(_flankCommit, 1.4f);
			return;
		}

		if (distance > PreferredRange * 1.35f)
		{
			_state = PilotTacticalState.Hunt;
			return;
		}

		if (distance < PreferredRange * 0.55f)
		{
			_state = PilotTacticalState.BreakContact;
			return;
		}

		_state = _rng.Randf() < Aggression ? PilotTacticalState.Engage : PilotTacticalState.Strafe;
		if (_rng.Randf() < 0.35f)
			_strafeSign *= -1f;
	}

	private Vector3 ComputeAimPoint(float dt, float distance)
	{
		if (_target == null || _mech == null)
			return _mech?.GlobalPosition ?? Vector3.Zero;

		var targetPoint = TeamUtil.GetAimPoint(_target);

		// Lead shots using our fastest living projectile weapon.
		var projectileSpeed = EstimateProjectileSpeed();
		if (projectileSpeed > 1f)
		{
			var travel = distance / projectileSpeed;
			targetPoint += TeamUtil.GetVelocity(_target) * travel * LeadFactor;
		}

		// Sharpshooter aims by tactical priority. Under torso-death, finishing the
		// cockpit is the default kill path; disablement only comes first when useful.
		if (UseSharpshooter && HasSharpshooter()
			&& _target is MechController targetMech
			&& targetMech.Assembler != null
			&& _mech.CanCombatId(targetMech.GlobalPosition))
		{
			foreach (var slot in SharpshooterPriority(targetMech))
			{
				if (targetMech.Assembler.Hardpoints.TryGetValue(slot, out var hp) && hp.CanTakeDamage)
				{
					targetPoint = hp.GlobalPosition;
					break;
				}
			}
		}

		// Human-feel aim noise that settles as we track.
		_aimJitter = _aimJitter.Lerp(
			new Vector3(
				_rng.RandfRange(-1f, 1f),
				_rng.RandfRange(-0.2f, 0.35f),
				_rng.RandfRange(-1f, 1f)) * Mathf.DegToRad(AimNoiseDegrees) * distance * 0.35f,
			1f - Mathf.Exp(-3f * dt));

		return targetPoint + _aimJitter;
	}

	private PartSlot? PickComponentTarget()
	{
		if (!UseSharpshooter || _target is not MechController targetMech
			|| targetMech.Assembler == null || !HasSharpshooter())
			return null;

		foreach (var slot in SharpshooterPriority(targetMech))
		{
			if (targetMech.Assembler.Hardpoints.TryGetValue(slot, out var hp) && hp.CanTakeDamage)
				return slot;
		}

		return null;
	}

	/// <summary>
	/// Torso is the only defeat location. Prefer it once the target is soft, already
	/// impaired, or when this pilot is playing aggressively. Otherwise strip mobility
	/// / weapons first so Medium AI still feels tactical.
	/// </summary>
	private PartSlot[] SharpshooterPriority(MechController target)
	{
		var torsoRatio = 1f;
		if (target.Health != null && target.Health.MaxHealth > 0.01f)
			torsoRatio = target.Health.CurrentHealth / target.Health.MaxHealth;

		var mobility = target.Integrity?.LegMobilityFactor ?? 1f;
		var canShoot = false;
		if (target.Assembler != null)
		{
			foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
			{
				if (target.Assembler.Hardpoints.TryGetValue(slot, out var hp) && hp.CanFire)
				{
					canShoot = true;
					break;
				}
			}
		}

		var finishNow = UseAbilitiesAggressively
			|| Aggression >= 0.7f
			|| torsoRatio <= 0.55f
			|| mobility < 0.45f
			|| !canShoot;

		return finishNow
			? new[]
			{
				PartSlot.Torso, PartSlot.Legs, PartSlot.WeaponL, PartSlot.WeaponR,
				PartSlot.Head, PartSlot.ShoulderL, PartSlot.ShoulderR, PartSlot.Systems
			}
			: new[]
			{
				PartSlot.Legs, PartSlot.WeaponL, PartSlot.WeaponR, PartSlot.Head,
				PartSlot.Torso, PartSlot.ShoulderL, PartSlot.ShoulderR, PartSlot.Systems
			};
	}

	private void DriveMovement(ref MechPilotCommand cmd, Vector3 dirToTarget, float distance, bool hasLos)
	{
		if (_mech == null)
			return;

		var locked = _mech.Assembler?.LegMode != LegMode.Gimbaled
			|| (_mech.Integrity?.LegMobilityFactor ?? 1f) < 0.5f;

		var desiredDir = dirToTarget;
		switch (_state)
		{
			case PilotTacticalState.Hunt:
				desiredDir = hasLos && _flankCommit <= 0f
					? dirToTarget
					: FindFlankDirection(dirToTarget);
				break;
			case PilotTacticalState.Engage:
				desiredDir = hasLos ? dirToTarget : FindFlankDirection(dirToTarget);
				break;
			case PilotTacticalState.Strafe:
				desiredDir = hasLos
					? (dirToTarget + Lateral(dirToTarget) * _strafeSign * 1.4f).Normalized()
					: FindFlankDirection(dirToTarget);
				break;
			case PilotTacticalState.BreakContact:
				desiredDir = (-dirToTarget + Lateral(dirToTarget) * _strafeSign).Normalized();
				break;
			case PilotTacticalState.Hold:
				desiredDir = hasLos ? dirToTarget : FindFlankDirection(dirToTarget);
				break;
		}

		if (locked)
		{
			var forward = -_mech.GlobalTransform.Basis.Z;
			forward.Y = 0f;
			forward = forward.Normalized();
			var angle = forward.SignedAngleTo(desiredDir, Vector3.Up);
			cmd.Turn = Mathf.Clamp(angle / Mathf.DegToRad(35f), -1f, 1f);

			var facingDot = forward.Dot(desiredDir);
			if (_state == PilotTacticalState.BreakContact)
				cmd.Throttle = facingDot > 0.2f ? -0.85f : 0.35f;
			else if (!hasLos || _state == PilotTacticalState.Hunt)
				cmd.Throttle = facingDot > 0.2f ? 1f : 0.45f;
			else if (distance > PreferredRange)
				cmd.Throttle = facingDot > 0.4f ? 0.8f : 0.1f;
			else if (distance < PreferredRange * 0.7f)
				cmd.Throttle = facingDot > 0.5f ? -0.55f : 0f;
			else
				cmd.Throttle = facingDot > 0.65f ? 0.15f : 0f;
		}
		else
		{
			// Gimbaled: strafe in world plane. Legs face move dir via controller.
			var right = Lateral(dirToTarget);
			var move = Vector3.Zero;

			if (_state == PilotTacticalState.BreakContact)
				move = -dirToTarget * 0.9f + right * _strafeSign * 0.7f;
			else if (!hasLos || _state == PilotTacticalState.Hunt)
				move = desiredDir;
			else if (_state == PilotTacticalState.Strafe)
				move = right * _strafeSign + dirToTarget * (distance > PreferredRange ? 0.35f : -0.2f);
			else
				move = right * _strafeSign * 0.65f + dirToTarget * 0.2f;

			move.Y = 0f;
			if (move.LengthSquared() > 0.001f)
				move = move.Normalized();

			// Convert world move into "camera-less" input: x = right relative to target bearing, y = along bearing.
			cmd.Move = new Vector2(move.Dot(right), move.Dot(dirToTarget));
			if (cmd.Move.Length() > 1f)
				cmd.Move = cmd.Move.Normalized();
		}
	}

	private bool ShouldSprint(float distance, bool hasLos)
	{
		if (_mech?.PowerHeat?.CanSprint != true)
			return false;
		if (_state == PilotTacticalState.BreakContact)
			return true;
		if (!hasLos)
			return true;
		if (_state == PilotTacticalState.Hunt && distance > PreferredRange * 1.15f)
			return Aggression > 0.35f;
		return false;
	}

	private void DecideWeapons(ref MechPilotCommand cmd, float distance, bool hasLos, bool inVision)
	{
		if (_acquireTimer > 0f || _mech?.Assembler == null)
			return;

		var clearing = _obstacleAim.HasValue;
		if (!hasLos && !clearing)
			return;

		var facingOk = FacingAllowsFire(cmd.AimPoint);
		if (!facingOk && !HasGimbalWeapon())
			return;

		if (!_bursting)
			return;

		// Easy/Medium sometimes hesitate mid-burst.
		if (!clearing && FireDiscipline < 0.999f && _rng.Randf() > FireDiscipline)
			return;

		// Precision / sharpshooter only inside combat vision cone (not while clearing cover).
		if (!clearing && !inVision && UseSharpshooter && HasSharpshooter())
			return;

		// Use weapons appropriate to range.
		if (_mech.Assembler.Hardpoints.TryGetValue(PartSlot.WeaponL, out var left) && left.CanFire)
		{
			var range = left.EquippedPart?.Range ?? 30f;
			if (clearing || distance < range * 1.05f)
				cmd.FirePrimary = true;
		}

		if (_mech.Assembler.Hardpoints.TryGetValue(PartSlot.WeaponR, out var right) && right.CanFire)
		{
			var range = right.EquippedPart?.Range ?? 30f;
			if (clearing || distance < range * 1.05f)
			{
				var needsVision = right.EquippedPart?.TargetingMode == TargetingMode.AimedComponent;
				cmd.FireSecondary = clearing
					|| ((!needsVision || inVision) && (distance > 8f || needsVision));
			}
		}
	}

	private void DecideAbility(ref MechPilotCommand cmd, float distance, bool hasLos)
	{
		if (_mech?.Abilities == null || _acquireTimer > 0f)
			return;

		var hpRatio = _mech.Health != null
			? _mech.Health.CurrentHealth / Mathf.Max(1f, _mech.Health.MaxHealth)
			: 1f;

		for (var i = 0; i < _mech.Abilities.BoundAbilities.Count; i++)
		{
			if (_mech.Abilities.GetCooldownRemaining(i) > 0.05f)
				continue;

			var part = _mech.Abilities.BoundAbilities[i];
			switch (part.AbilityId)
			{
				case AbilityId.MissileSalvo when hasLos && distance > 12f && distance < 48f:
					if (!UseAbilitiesAggressively && _rng.Randf() > 0.35f)
						continue;
					cmd.AbilityIndex = i;
					return;
				case AbilityId.MendPulse when NeedsRepair():
					// Drop a heal pad on ourselves; soak logic holds the field.
					cmd.AbilityIndex = i;
					return;
				case AbilityId.PulseRepair when NeedsRepair():
					cmd.AbilityIndex = i;
					return;
				case AbilityId.Shroud when hpRatio < (UseAbilitiesAggressively ? 0.35f : 0.2f)
					|| (_state == PilotTacticalState.BreakContact && UseAbilitiesAggressively):
					cmd.AbilityIndex = i;
					return;
			}
		}
	}

	private void UpdateBurst(float dt)
	{
		if (_bursting)
		{
			_burstTimer -= dt;
			if (_burstTimer <= 0f)
			{
				_bursting = false;
				_burstCooldown = _rng.RandfRange(0.25f, 0.7f) * _burstGapScale;
			}
			return;
		}

		_burstCooldown -= dt;
		if (_burstCooldown <= 0f)
		{
			_bursting = true;
			_burstTimer = _rng.RandfRange(0.35f, 0.9f) * (0.6f + Aggression) * _burstLengthScale;
		}
	}

	private bool FacingAllowsFire(Vector3 aimPoint)
	{
		if (_mech == null)
			return false;
		if (HasGimbalWeapon() || _mech.Assembler?.LegMode == LegMode.Gimbaled)
			return true;

		var forward = -_mech.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		var to = aimPoint - _mech.GlobalPosition;
		to.Y = 0f;
		if (to.LengthSquared() < 0.01f)
			return true;
		return forward.Normalized().Dot(to.Normalized()) > _facingDotMin;
	}

	private bool HasGimbalWeapon()
	{
		if (_mech?.Assembler == null)
			return false;
		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (_mech.Assembler.Hardpoints.TryGetValue(slot, out var hp)
				&& hp.CanFire
				&& hp.EffectiveAimMode == AimMode.Gimbaled)
				return true;
		}
		return false;
	}

	private bool HasSharpshooter()
	{
		if (_mech?.Assembler == null)
			return false;
		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (_mech.Assembler.Hardpoints.TryGetValue(slot, out var hp)
				&& hp.CanFire
				&& hp.EquippedPart?.TargetingMode == TargetingMode.AimedComponent)
				return true;
		}
		return false;
	}

	private float EstimateProjectileSpeed()
	{
		float best = 40f;
		if (_mech?.Assembler == null)
			return best;
		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (_mech.Assembler.Hardpoints.TryGetValue(slot, out var hp) && hp.CanFire)
				best = Mathf.Max(best, hp.EquippedPart?.ProjectileSpeed ?? 40f);
		}
		return best;
	}

	private bool HasLineOfSight(Vector3 from, Vector3 to)
	{
		var space = _mech?.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return true;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 1; // world/cover only
		if (_mech != null)
			query.Exclude = new Godot.Collections.Array<Rid> { _mech.GetRid() };
		var hit = space.IntersectRay(query);
		return hit.Count == 0;
	}

	private Vector3? FindDestructibleBlockerAim(Vector3 from, Vector3 to)
	{
		var space = _mech?.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return null;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 1;
		if (_mech != null)
			query.Exclude = new Godot.Collections.Array<Rid> { _mech.GetRid() };
		var hit = space.IntersectRay(query);
		if (hit.Count == 0)
			return null;

		var collider = hit["collider"].AsGodotObject() as Node;
		if (FindDamageable(collider) == null)
			return null;

		return hit["position"].AsVector3();
	}

	private static Damageable? FindDamageable(Node? node)
	{
		var current = node;
		while (current != null)
		{
			if (current is Damageable direct)
				return direct;
			var child = current.GetNodeOrNull<Damageable>("Damageable");
			if (child != null && !child.IsDead)
				return child;
			current = current.GetParent();
		}

		return null;
	}

	private Vector3 FindFlankDirection(Vector3 preferred)
	{
		if (_mech == null)
			return preferred;

		var origin = _mech.GlobalPosition + Vector3.Up * 1.2f;
		var space = _mech.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return preferred;

		// If the straight path is blocked, slide along the hit surface using committed flank side.
		var ahead = PhysicsRayQueryParameters3D.Create(origin, origin + preferred * 18f);
		ahead.CollisionMask = 1;
		ahead.Exclude = new Godot.Collections.Array<Rid> { _mech.GetRid() };
		var block = space.IntersectRay(ahead);
		if (block.Count > 0)
		{
			var normal = block["normal"].AsVector3();
			normal.Y = 0f;
			if (normal.LengthSquared() > 0.01f)
			{
				normal = normal.Normalized();
				var along = normal.Cross(Vector3.Up).Normalized() * _strafeSign;
				var slide = (along * 1.35f + normal * 0.55f + preferred * 0.2f);
				slide.Y = 0f;
				if (slide.LengthSquared() > 0.01f)
					return slide.Normalized();
			}
		}

		Vector3 best = Lateral(preferred) * _strafeSign;
		var bestScore = float.MinValue;
		for (var i = 0; i < 12; i++)
		{
			var ang = i * Mathf.Tau / 12f;
			var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
			var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir * 14f);
			query.CollisionMask = 1;
			query.Exclude = new Godot.Collections.Array<Rid> { _mech.GetRid() };
			var hit = space.IntersectRay(query);
			var clear = hit.Count == 0 ? 14f : origin.DistanceTo(hit["position"].AsVector3());
			var flankBias = dir.Dot(Lateral(preferred) * _strafeSign);
			var towardBias = dir.Dot(preferred);
			// Prefer open flank paths over charging the obstacle.
			var score = clear * 1.4f + flankBias * 4.5f + towardBias * 1.2f;
			if (score > bestScore)
			{
				bestScore = score;
				best = dir;
			}
		}

		return best.LengthSquared() > 0.01f ? best.Normalized() : preferred;
	}

	private static Vector3 Lateral(Vector3 forward)
	{
		var right = forward.Cross(Vector3.Up);
		if (right.LengthSquared() < 0.001f)
			right = Vector3.Right;
		return right.Normalized();
	}
}
