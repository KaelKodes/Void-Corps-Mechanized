using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Active abilities from chassis attachments, bound to keys 1-6 in equip order.
/// Missiles / Mend Beacon: paint-then-release. Mend Beacon also supports Ctrl+self.
/// Pulse Repair: hold-to-channel.
/// </summary>
public partial class AbilityController : Node
{
	public const int MaxAbilitySlots = 6;

	private MechController? _mech;
	private MechAssembler? _assembler;
	private Damageable? _health;
	private readonly List<PartData> _boundAbilities = new();
	private readonly float[] _cooldowns = new float[MaxAbilitySlots];
	private float _shroudRemaining;
	private bool _shroudActive;
	private int _pulseSlot = -1;
	private bool _pulseActive;
	private float _pulseAiAutoRemaining;
	private const string PulseDrawKey = "pulse_repair";

	public IReadOnlyList<PartData> BoundAbilities => _boundAbilities;
	public bool IsCloaked => _shroudActive;
	public bool IsPulseRepairing => _pulseActive;

	public bool IsMissileAbility(int index)
	{
		if (index < 0 || index >= _boundAbilities.Count)
			return false;
		return _boundAbilities[index].AbilityId == AbilityId.MissileSalvo;
	}

	public bool IsPaintMissileAbility(int index) =>
		IsMissileAbility(index) && _boundAbilities[index].MissileGuidance == MissileGuidanceMode.Paint;

	public bool IsSensorMissileAbility(int index) =>
		IsMissileAbility(index) && _boundAbilities[index].MissileGuidance != MissileGuidanceMode.Paint;

	public bool IsMendBeaconAbility(int index)
	{
		if (index < 0 || index >= _boundAbilities.Count)
			return false;
		return _boundAbilities[index].AbilityId == AbilityId.MendPulse;
	}

	public bool IsPulseRepairAbility(int index)
	{
		if (index < 0 || index >= _boundAbilities.Count)
			return false;
		return _boundAbilities[index].AbilityId == AbilityId.PulseRepair;
	}

	/// <summary>Hold-to-paint then release (paint missiles + mend beacon).</summary>
	public bool IsPaintLockAbility(int index) => IsPaintMissileAbility(index) || IsMendBeaconAbility(index);

	/// <summary>
	/// Beneficial utilities that accept Ctrl+key self-cast (drop/channel on self).
	/// Hostile paint abilities like missiles are excluded.
	/// </summary>
	public bool IsBeneficialSelfCastAbility(int index)
	{
		if (index < 0 || index >= _boundAbilities.Count)
			return false;
		return _boundAbilities[index].AbilityId switch
		{
			AbilityId.MendPulse => true,
			AbilityId.PulseRepair => true,
			AbilityId.Shroud => true,
			AbilityId.HeatSink => true,
			AbilityId.ContactReveal => true,
			_ => false
		};
	}

	public bool CanActivate(int index)
	{
		if (_mech == null || index < 0 || index >= _boundAbilities.Count)
			return false;
		if (_cooldowns[index] > 0f)
			return false;
		if (_pulseActive)
			return false;
		var powerHeat = _mech.PowerHeat;
		if (powerHeat != null && !powerHeat.CanUseAbilities)
			return false;

		var part = _boundAbilities[index];
		var load = part.AbilityPowerLoad;
		if (powerHeat == null || load <= 0.01f)
		{
			if (part.AbilityId == AbilityId.MissileSalvo)
				return CanFireMissile(part);
			return true;
		}

		// Pulse is a sustained drain — need pool headroom, not the full burst amount.
		if (part.AbilityId == AbilityId.PulseRepair)
			return powerHeat.CurrentPower > 0.5f;

		if (part.AbilityId == AbilityId.MissileSalvo && !CanFireMissile(part))
			return false;

		return powerHeat.CanSpend(load);
	}

	/// <summary>
	/// Paint missiles always ready (subject to power/cd). Sensor missiles need a maintained TAB lock;
	/// vision vs contact is per <see cref="PartData.MissileGuidance"/>.
	/// AI has no TAB UI — an in-range hostile satisfies the same contact rules.
	/// </summary>
	public bool CanFireMissile(PartData part)
	{
		if (_mech == null || part.AbilityId != AbilityId.MissileSalvo)
			return false;

		if (part.MissileGuidance == MissileGuidanceMode.Paint)
			return true;

		var locked = ResolveSeekerTarget(part, out var inVision);
		if (locked == null)
			return false;

		return part.MissileGuidance switch
		{
			MissileGuidanceMode.SensorVision => inVision,
			MissileGuidanceMode.SensorContact => true,
			_ => false
		};
	}

	private MechController? ResolveSeekerTarget(PartData part, out bool inVision)
	{
		inVision = false;
		if (_mech == null)
			return null;

		var range = Mathf.Max(12f, part.Range);
		var locked = _mech.SensorLockMech;
		if (locked != null
		    && locked.Integrity?.IsCollapsed != true
		    && locked.Health?.IsDead != true
		    && _mech.GlobalPosition.DistanceTo(locked.GlobalPosition) <= range)
		{
			inVision = _mech.SensorLockInVision;
			return locked;
		}

		// AI (and any case without TAB lock): pick nearest hostile in weapon range.
		if (_mech.IsHumanPilot)
			return null;

		MechController? best = null;
		var bestDist = float.MaxValue;
		var scene = _mech.GetTree()?.CurrentScene ?? _mech.GetParent();
		if (scene == null)
			return null;

		foreach (var child in scene.GetChildren())
		{
			if (child is not MechController other || other == _mech)
				continue;
			if (!TeamUtil.IsHostile(_mech.Team, other.Team))
				continue;
			if (other.Integrity?.IsCollapsed == true || other.Health?.IsDead == true)
				continue;
			var dist = _mech.GlobalPosition.DistanceTo(other.GlobalPosition);
			if (dist > range || dist >= bestDist)
				continue;
			bestDist = dist;
			best = other;
		}

		if (best == null)
			return null;

		inVision = _mech.CanCombatId(best.GlobalPosition);
		return best;
	}

	public void Bind(MechController mech, MechAssembler assembler, Damageable? health)
	{
		_mech = mech;
		_assembler = assembler;
		_health = health;
		RebuildBindings();
	}

	public void RebuildBindings()
	{
		var previousCooldownById = new Dictionary<string, float>();
		for (var i = 0; i < _boundAbilities.Count && i < MaxAbilitySlots; i++)
		{
			var id = _boundAbilities[i].Id;
			if (string.IsNullOrEmpty(id))
				continue;
			previousCooldownById[id] = _cooldowns[i];
		}

		var pulsePartId = _pulseActive && _pulseSlot >= 0 && _pulseSlot < _boundAbilities.Count
			? _boundAbilities[_pulseSlot].Id
			: null;
		var hadShroud = _shroudActive;

		_boundAbilities.Clear();
		for (var i = 0; i < MaxAbilitySlots; i++)
			_cooldowns[i] = 0f;

		if (_assembler == null)
		{
			EndPulseRepair(applyCooldown: false);
			EndShroud();
			return;
		}

		foreach (var part in _assembler.GetActiveAbilityParts().Take(MaxAbilitySlots))
			_boundAbilities.Add(part);

		for (var i = 0; i < _boundAbilities.Count && i < MaxAbilitySlots; i++)
		{
			if (previousCooldownById.TryGetValue(_boundAbilities[i].Id, out var cd))
				_cooldowns[i] = cd;
		}

		// Only cancel channelled / shroud states if their part was actually lost.
		if (_pulseActive)
		{
			var pulseStillBound = !string.IsNullOrEmpty(pulsePartId)
				&& _boundAbilities.Exists(p => p.Id == pulsePartId);
			if (!pulseStillBound)
				EndPulseRepair(applyCooldown: false);
			else
			{
				_pulseSlot = _boundAbilities.FindIndex(p => p.Id == pulsePartId);
				if (_pulseSlot < 0)
					EndPulseRepair(applyCooldown: false);
			}
		}

		if (hadShroud)
		{
			var shroudStillBound = _boundAbilities.Exists(p => p.AbilityId == AbilityId.Shroud);
			if (!shroudStillBound)
				EndShroud();
		}
	}

	public float GetCooldownRemaining(int index)
	{
		if (index < 0 || index >= MaxAbilitySlots)
			return 0f;
		return _cooldowns[index];
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		for (var i = 0; i < MaxAbilitySlots; i++)
		{
			if (_cooldowns[i] > 0f)
				_cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);
		}

		if (_shroudActive)
		{
			_shroudRemaining -= dt;
			if (_shroudRemaining <= 0f)
				EndShroud();
		}

		if (_pulseActive)
			TickPulseRepair(dt);
	}

	public bool TryActivate(int index, Vector3 aimPoint)
	{
		if (_mech == null || _assembler == null)
			return false;
		if (index < 0 || index >= _boundAbilities.Count)
			return false;
		if (_cooldowns[index] > 0f)
			return false;

		var powerHeat = _mech.PowerHeat;
		if (powerHeat != null && !powerHeat.CanUseAbilities)
			return false;

		var part = _boundAbilities[index];

		// Pulse Repair is hold-to-channel; AI starts a short auto channel.
		if (part.AbilityId == AbilityId.PulseRepair)
			return BeginPulseRepair(index, aiAutoSeconds: _mech.IsPlayerControlled ? 0f : 2.2f);

		if (part.AbilityId == AbilityId.MissileSalvo && !CanFireMissile(part))
			return false;

		var load = part.AbilityPowerLoad;
		if (powerHeat != null && load > 0.01f && !powerHeat.CanSpend(load))
			return false;

		var used = part.AbilityId switch
		{
			AbilityId.MissileSalvo => ActivateMissileSalvo(part, aimPoint),
			AbilityId.MendPulse => ActivateMendBeacon(part, aimPoint),
			AbilityId.Shroud => ActivateShroud(part),
			AbilityId.ContactReveal => ActivateContactReveal(part),
			_ => false
		};

		if (used)
		{
			powerHeat?.TrySpend(load);
			_cooldowns[index] = part.AbilityCooldown;
			powerHeat?.AddHeat(part.AbilityHeatBurst);
			if (_mech.IsPlayerControlled)
				TelemetryUtil.Match(_mech)?.Telemetry.RecordUtilityUse();
		}

		return used;
	}

	public bool BeginPulseRepair(int index, float aiAutoSeconds = 0f)
	{
		if (!IsPulseRepairAbility(index) || !CanActivate(index) || _mech == null)
			return false;
		if (!HasAnythingToPulseRepair(index))
			return false;

		var part = _boundAbilities[index];
		var powerHeat = _mech.PowerHeat;
		if (powerHeat != null && part.AbilityPowerLoad > 0.01f
		    && !powerHeat.TryDraw(PulseDrawKey, part.AbilityPowerLoad))
			return false;

		_pulseSlot = index;
		_pulseActive = true;
		_pulseAiAutoRemaining = aiAutoSeconds;
		if (_mech.IsPlayerControlled)
			TelemetryUtil.Match(_mech)?.Telemetry.RecordUtilityUse();
		SfxService.Play("capture", 1.15f, -8f);
		return true;
	}

	public void EndPulseRepair(bool applyCooldown = true)
	{
		var slot = _pulseSlot;
		PartData? part = slot >= 0 && slot < _boundAbilities.Count ? _boundAbilities[slot] : null;
		var wasActive = _pulseActive;
		_pulseActive = false;
		_pulseSlot = -1;
		_pulseAiAutoRemaining = 0f;
		_mech?.PowerHeat?.Release(PulseDrawKey);

		if (applyCooldown && wasActive && part != null && slot >= 0)
			_cooldowns[slot] = part.AbilityCooldown;
	}

	private void TickPulseRepair(float dt)
	{
		if (!_pulseActive || _mech == null || _pulseSlot < 0 || _pulseSlot >= _boundAbilities.Count)
		{
			EndPulseRepair(applyCooldown: false);
			return;
		}

		var part = _boundAbilities[_pulseSlot];
		var powerHeat = _mech.PowerHeat;

		if (powerHeat != null && powerHeat.IsOverheated)
		{
			EndPulseRepair(applyCooldown: true);
			SfxService.PlayUiError(UiErrorTone.BuzzBuzz, -5f);
			return;
		}

		if (powerHeat != null && powerHeat.CurrentPower <= 0.01f)
		{
			EndPulseRepair(applyCooldown: true);
			return;
		}

		if (_pulseAiAutoRemaining > 0f)
		{
			_pulseAiAutoRemaining -= dt;
			if (_pulseAiAutoRemaining <= 0f)
			{
				EndPulseRepair(applyCooldown: true);
				return;
			}
		}

		var healed = ApplyDirectedMend(part, part.AbilityPower * dt);
		powerHeat?.AddHeat(part.AbilityHeatBurst * dt);

		if (powerHeat is { IsOverheated: true })
		{
			EndPulseRepair(applyCooldown: true);
			SfxService.PlayUiError(UiErrorTone.BuzzBuzz, -5f);
			return;
		}

		if (healed < 0.01f && !HasAnythingToPulseRepair(_pulseSlot))
			EndPulseRepair(applyCooldown: true);
	}

	private bool HasAnythingToPulseRepair(int slotHint = -1)
	{
		if (_mech == null)
			return false;

		if (_health != null && !_health.IsDead && _health.CurrentHealth < _health.MaxHealth - 0.5f)
			return true;

		if (_assembler != null)
		{
			foreach (var hp in _assembler.Hardpoints.Values)
			{
				if (hp.CanTakeDamage && hp.CurrentHp < hp.MaxHp - 0.5f)
					return true;
			}
		}

		var radius = 12f;
		if (slotHint >= 0 && slotHint < _boundAbilities.Count)
			radius = _boundAbilities[slotHint].AbilityRadius;
		else if (_pulseSlot >= 0 && _pulseSlot < _boundAbilities.Count)
			radius = _boundAbilities[_pulseSlot].AbilityRadius;

		var origin = _mech.GlobalPosition;
		var scene = _mech.GetTree().CurrentScene ?? _mech.GetParent();
		if (scene == null)
			return false;

		foreach (var child in scene.GetChildren())
		{
			if (child is MechController other && other != _mech
			    && other.Team == _mech.Team
			    && other.Visible
			    && other.Integrity?.IsCollapsed != true
			    && other.GlobalPosition.DistanceTo(origin) <= radius
			    && AllyNeedsRepair(other))
				return true;
			if (child is SupportUnit support
			    && support.Team == _mech.Team
			    && support.IsAlive
			    && support.Health != null
			    && support.GlobalPosition.DistanceTo(origin) <= radius
			    && support.Health.CurrentHealth < support.Health.MaxHealth - 0.5f)
				return true;
		}

		return false;
	}

	private static bool AllyNeedsRepair(MechController other)
	{
		if (other.Health != null && !other.Health.IsDead
		    && other.Health.CurrentHealth < other.Health.MaxHealth - 0.5f)
			return true;
		if (other.Assembler == null)
			return false;
		foreach (var hp in other.Assembler.Hardpoints.Values)
		{
			if (hp.CanTakeDamage && hp.CurrentHp < hp.MaxHp - 0.5f)
				return true;
		}
		return false;
	}

	private float ApplyDirectedMend(PartData part, float amount)
	{
		if (amount <= 0.01f || _mech == null)
			return 0f;

		var healed = 0f;
		var origin = _mech.GlobalPosition;
		var radius = part.AbilityRadius;

		if (_mech.Integrity != null)
			healed += _mech.Integrity.ApplyMend(amount);
		else if (_health != null && !_health.IsDead)
		{
			var before = _health.CurrentHealth;
			_health.ApplyHeal(amount);
			healed += _health.CurrentHealth - before;
		}

		var allyBudget = amount * 0.75f;
		var scene = _mech.GetTree().CurrentScene ?? _mech.GetParent();
		if (scene == null)
			return healed;

		foreach (var child in scene.GetChildren())
		{
			if (allyBudget <= 0.01f)
				break;

			if (child is MechController other && other != _mech
			    && other.Team == _mech.Team
			    && other.Visible
			    && other.Integrity?.IsCollapsed != true
			    && other.GlobalPosition.DistanceTo(origin) <= radius
			    && other.Integrity != null)
			{
				var gained = other.Integrity.ApplyMend(allyBudget);
				healed += gained;
				allyBudget -= gained;
			}
			else if (child is SupportUnit support
			         && support.Team == _mech.Team
			         && support.IsAlive
			         && support.GlobalPosition.DistanceTo(origin) <= radius
			         && support.Health != null)
			{
				var before = support.Health.CurrentHealth;
				support.Health.ApplyHeal(allyBudget);
				var gained = support.Health.CurrentHealth - before;
				healed += gained;
				allyBudget -= gained;
			}
		}

		if (healed > 0.01f && _mech.IsPlayerControlled)
			TelemetryUtil.Match(_mech)?.Telemetry.RecordHeal(healed);

		return healed;
	}

	private bool ActivateMendBeacon(PartData part, Vector3 aimPoint)
	{
		var parent = _mech!.GetTree().CurrentScene ?? _mech.GetParent();
		if (parent == null)
			return false;

		var duration = part.AbilityDuration > 0.1f ? part.AbilityDuration : 8f;
		var beacon = HealBeacon.Create(
			aimPoint,
			part.AbilityRadius > 0.1f ? part.AbilityRadius : 10f,
			part.AbilityPower > 0.1f ? part.AbilityPower : 12f,
			duration,
			_mech,
			offlineWhileHealing: false);
		parent.AddChild(beacon);
		SfxService.Play("capture", 0.9f, -3f);
		return true;
	}

	private bool ActivateMissileSalvo(PartData part, Vector3 aimPoint)
	{
		var count = Mathf.Max(1, Mathf.RoundToInt(part.AbilityPower));
		var origin = _mech!.GlobalPosition + Vector3.Up * 2.4f;
		origin.Y -= _mech.CarrierCombatLift;
		var parent = _mech.GetTree().CurrentScene ?? _mech.GetParent();
		if (parent == null)
			return false;

		var seeker = part.MissileGuidance != MissileGuidanceMode.Paint;
		MechController? lockTarget = null;
		PartSlot? preferred = null;
		var impact = aimPoint;
		impact.Y = Mathf.Max(0.4f, aimPoint.Y);

		if (seeker)
		{
			lockTarget = ResolveSeekerTarget(part, out _);
			if (lockTarget == null)
				return false;

			preferred = _mech.SensorFocusSlot ?? PartSlot.Torso;
			impact = ResolveMissileAimPoint(lockTarget, preferred.Value);
		}

		var targeting = preferred.HasValue ? TargetingMode.AimedComponent : TargetingMode.Standard;
		var preferredSlot = preferred.HasValue ? (int)preferred.Value : -1;

		for (var i = 0; i < count; i++)
		{
			var spread = (i - (count - 1) * 0.5f) * 0.18f;
			var target = impact + new Vector3(spread * 1.2f, 0f, spread * 0.8f);
			var spawn = origin + new Vector3(spread * 0.35f, 0.12f * i, 0f);
			var speed = Mathf.Max(12f, part.ProjectileSpeed);

			var style = ProjectileStyleUtil.FromPart(part);
			var bus = NetCombatBus.Find(parent);
			if (bus != null && _mech.GetTree()?.GetMultiplayer().MultiplayerPeer != null)
			{
				// Net path: aim at lock point at fire time (homing target not replicated yet).
				var lob = Projectile.SolveLob(spawn, target, speed);
				bus.HostSpawnProjectile(
					parent,
					_mech,
					spawn,
					lob.Velocity,
					part.Damage,
					lob.Lifetime,
					_mech.Team,
					targeting,
					preferredSlot,
					style,
					gravity: lob.Gravity,
					visualScale: Projectile.MechVisualScale);
			}
			else
			{
				var missile = Projectile.Create(style, Projectile.MechVisualScale);
				missile.Source = _mech;
				missile.SourceTeam = _mech.Team;
				missile.Damage = part.Damage;
				missile.TargetingMode = targeting;
				missile.PreferredSlot = preferred;
				parent.AddChild(missile);

				if (seeker && lockTarget != null)
				{
					missile.LaunchSeeker(spawn, lockTarget, preferred, speed);
				}
				else
				{
					missile.LaunchLob(spawn, target, speed);
				}
			}
		}

		if (_mech.IsHumanPilot)
		{
			var telemetry = TelemetryUtil.Match(_mech)?.Telemetry;
			for (var i = 0; i < count; i++)
				telemetry?.RecordShot(missile: true);
		}

		SfxService.PlayWorld("weapon_fire", _mech.GlobalPosition, 0.72f, -1f);
		return true;
	}

	private static Vector3 ResolveMissileAimPoint(MechController target, PartSlot focus)
	{
		if (target.Assembler?.Hardpoints.TryGetValue(focus, out var hp) == true
		    && hp.EquippedPart != null
		    && hp.EquippedPart.VisualKind != "empty"
		    && !hp.IsDestroyed)
			return hp.GlobalPosition;

		if (target.Assembler?.Hardpoints.TryGetValue(PartSlot.Torso, out var torso) == true)
			return torso.GlobalPosition;

		return target.GlobalPosition + Vector3.Up * 1.4f;
	}

	private bool ActivateShroud(PartData part)
	{
		_shroudActive = true;
		_shroudRemaining = part.AbilityDuration;
		ApplyShroudVisual(true);
		return true;
	}

	/// <summary>
	/// Turns the head's passive contact-scan 3D display on for <see cref="PartData.AbilityDuration"/>.
	/// Any future ability can also call <see cref="SensorContactScan.RevealWorldBlips"/> directly.
	/// </summary>
	private bool ActivateContactReveal(PartData part)
	{
		if (_mech == null)
			return false;

		var scan = _mech.GetNodeOrNull<SensorContactScan>(SensorContactScan.NodeName);
		if (scan == null)
			return false;

		scan.RevealWorldBlips(part.AbilityDuration > 0.05f ? part.AbilityDuration : 2.5f);
		SfxService.Play("confirm", 1.05f, -6f);
		return true;
	}

	private void EndShroud()
	{
		if (!_shroudActive)
			return;
		_shroudActive = false;
		_shroudRemaining = 0f;
		ApplyShroudVisual(false);
	}

	private void ApplyShroudVisual(bool cloaked)
	{
		if (_mech == null)
			return;

		foreach (var mi in _mech.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (mi.MaterialOverride is StandardMaterial3D mat)
			{
				mat.Transparency = cloaked
					? BaseMaterial3D.TransparencyEnum.Alpha
					: BaseMaterial3D.TransparencyEnum.Disabled;
				var color = mat.AlbedoColor;
				color.A = cloaked ? 0.35f : 1f;
				mat.AlbedoColor = color;
			}
		}
	}
}
