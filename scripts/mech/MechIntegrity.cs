using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Per-component integrity, hit routing, collapse, and self-destruct.
/// </summary>
public partial class MechIntegrity : Node
{
	[Signal] public delegate void ComponentDamagedEventHandler(PartSlot slot, float amount, float remaining, float max);
	[Signal] public delegate void ComponentDestroyedEventHandler(PartSlot slot);
	[Signal] public delegate void MechCollapsedEventHandler();
	[Signal] public delegate void SelfDestructArmedEventHandler(bool armed);

	private MechController? _mech;
	private MechAssembler? _assembler;
	private Damageable? _torsoHealth;
	private bool _collapsed;
	private bool _selfDestructHolding;
	private float _selfDestructHold;
	private const float SelfDestructHoldTime = 3f;
	private const float SelfDestructRadius = 16f;
	private const float SelfDestructDamage = 85f;

	public bool IsCollapsed => _collapsed;
	public bool IsSelfDestructArmed => _selfDestructHolding && _selfDestructHold > 0f;
	public float SelfDestructProgress => _selfDestructHolding
		? Mathf.Clamp(_selfDestructHold / SelfDestructHoldTime, 0f, 1f)
		: 0f;

	public float LegMobilityFactor
	{
		get
		{
			if (_assembler == null)
				return 1f;
			if (!_assembler.Hardpoints.TryGetValue(PartSlot.Legs, out var legs))
				return 1f;
			return legs.MobilityFactor;
		}
	}

	public string LegStatusText
	{
		get
		{
			if (_assembler == null || !_assembler.Hardpoints.TryGetValue(PartSlot.Legs, out var legs))
				return "Legs --";
			if (legs.EquippedPart == null)
				return "Legs none";
			return $"Legs {legs.LimbsAlive}/{legs.LimbCount} ({legs.MobilityFactor * 100f:0}%)";
		}
	}

	public void Bind(MechController mech, MechAssembler assembler, Damageable? torsoHealth)
	{
		if (_torsoHealth != null)
			_torsoHealth.Died -= OnTorsoHealthDied;

		_mech = mech;
		_assembler = assembler;
		_torsoHealth = torsoHealth;
		_collapsed = false;
		CancelSelfDestruct();
		InitializeComponentHealth();

		if (_torsoHealth != null
		    && _assembler.Hardpoints.TryGetValue(PartSlot.Torso, out var torso))
		{
			torso.BindExternalHealth(_torsoHealth);
			_torsoHealth.Died += OnTorsoHealthDied;
		}
	}

	public void InitializeComponentHealth()
	{
		if (_assembler == null)
			return;

		foreach (var hp in _assembler.Hardpoints.Values)
			hp.InitializeIntegrity();
	}

	/// <summary>
	/// Mend torso structure and damaged (non-destroyed) components. Returns total HP restored.
	/// </summary>
	public float ApplyMend(float amount)
	{
		if (_collapsed || amount <= 0f || _assembler == null)
			return 0f;

		var healed = 0f;
		var remaining = amount;

		if (_assembler.Hardpoints.TryGetValue(PartSlot.Torso, out var torso)
		    && torso.CanTakeDamage)
		{
			var gained = torso.ApplyHeal(remaining * 0.35f);
			healed += gained;
			remaining = Mathf.Max(0f, remaining - gained);
			if (gained > 0.01f)
				EmitSignal(SignalName.ComponentDamaged, (int)PartSlot.Torso, -gained, torso.CurrentHp, torso.MaxHp);
		}

		if (remaining <= 0.01f)
			return healed;

		// Prefer the most damaged living packages.
		var packages = _assembler.Hardpoints.Values
			.Where(hp => hp.Slot != PartSlot.Torso
			             && hp.CanTakeDamage
			             && hp.CurrentHp < hp.MaxHp - 0.01f)
			.OrderBy(hp => hp.CurrentHp / Mathf.Max(1f, hp.MaxHp))
			.ToList();

		foreach (var hp in packages)
		{
			if (remaining <= 0.01f)
				break;
			var gained = hp.ApplyHeal(remaining);
			if (gained <= 0.01f)
				continue;
			healed += gained;
			remaining -= gained;
			EmitSignal(SignalName.ComponentDamaged, (int)hp.Slot, -gained, hp.CurrentHp, hp.MaxHp);
		}

		return healed;
	}

	public override void _Process(double delta)
	{
		if (_collapsed || _mech == null)
			return;

		UpdateSelfDestructHold((float)delta);
	}

	private void UpdateSelfDestructHold(float dt)
	{
		if (!_mech!.IsPlayerControlled)
			return;

		var holding = Input.IsActionPressed("self_destruct");
		if (!holding)
		{
			if (_selfDestructHolding)
			{
				CancelSelfDestruct();
				GD.Print("Self-destruct canceled.");
			}
			return;
		}

		if (!_selfDestructHolding)
		{
			_selfDestructHolding = true;
			_selfDestructHold = 0f;
			EmitSignal(SignalName.SelfDestructArmed, true);
			GD.Print("Self-destruct: hold Backspace...");
		}

		_selfDestructHold += dt;
		if (_selfDestructHold >= SelfDestructHoldTime)
			DetonateSelfDestruct();
	}

	private void CancelSelfDestruct()
	{
		var was = _selfDestructHolding;
		_selfDestructHolding = false;
		_selfDestructHold = 0f;
		if (was)
			EmitSignal(SignalName.SelfDestructArmed, false);
	}

	public PartSlot? ReceiveHit(float damage, Vector3 hitPosition, PartSlot? preferredSlot, bool aimedShot)
	{
		if (_collapsed || _assembler == null)
			return null;
		if (_mech?.IsInvulnerable == true)
			return null;

		var remaining = damage;
		if (_mech != null)
			remaining = _mech.TryAbsorbWithHeldShield(damage, hitPosition);

		if (remaining <= 0.01f)
			return _mech?.GetRaisedHeldShieldSlot();

		Hardpoint? target = null;

		if (aimedShot && preferredSlot.HasValue)
		{
			if (_assembler.Hardpoints.TryGetValue(preferredSlot.Value, out var preferred)
			    && preferred.CanTakeDamage)
				target = preferred;
			else
				// A precision round already in flight does not vanish when its target
				// component is destroyed; it continues into the central torso.
				target = GetTorso();
		}
		else
		{
			target = FindNearestComponent(hitPosition);
		}

		target ??= FindNearestComponent(hitPosition);
		target ??= GetTorso();
		if (target == null)
			return null;

		var before = target.CurrentHp;
		var packageDestroyed = target.ApplyComponentDamage(remaining, out var limbsLost);
		var structureLost = Mathf.Max(0f, before - target.CurrentHp);
		EmitSignal(SignalName.ComponentDamaged, (int)target.Slot, structureLost, target.CurrentHp, target.MaxHp);

		if (limbsLost > 0 && target.IsLegPackage)
			GD.Print($"{_mech?.Name} lost {limbsLost} limb(s). Legs {target.LimbsAlive}/{target.LimbCount}.");

		if (packageDestroyed && !target.IsDestroyed)
		{
			EmitSignal(SignalName.ComponentDestroyed, (int)target.Slot);
			OnComponentDestroyed(target);
		}

		EvaluateCollapse();
		return target.Slot;
	}

	public Hardpoint? FindNearestComponent(Vector3 worldPoint)
	{
		if (_assembler == null)
			return null;

		Hardpoint? best = null;
		var bestDist = float.MaxValue;
		foreach (var hp in _assembler.Hardpoints.Values)
		{
			if (!hp.CanTakeDamage)
				continue;
			var d = hp.GlobalPosition.DistanceSquaredTo(worldPoint);
			if (d < bestDist)
			{
				bestDist = d;
				best = hp;
			}
		}

		return best;
	}

	public Hardpoint? FindComponentNearAim(Vector3 aimPoint, float maxDistance = 4.5f)
	{
		var nearest = FindNearestComponent(aimPoint);
		if (nearest == null)
			return null;
		return nearest.GlobalPosition.DistanceTo(aimPoint) <= maxDistance ? nearest : null;
	}

	private void OnComponentDestroyed(Hardpoint hardpoint)
	{
		if (hardpoint.IsDestroyed)
			return;

		GD.Print($"{_mech?.Name} lost {hardpoint.Slot} ({hardpoint.EquippedPart?.DisplayName}).");
		hardpoint.MarkDestroyed();

		if (_mech?.Abilities != null && _assembler != null)
			_mech.Abilities.RebuildBindings();
		_assembler?.RefreshStatsAfterDamage();
		_mech?.PowerHeat?.RefreshStats(_assembler!);
	}

	private void EvaluateCollapse()
	{
		if (_assembler == null || _collapsed)
			return;

		if (IsDestroyed(PartSlot.Torso) || (_torsoHealth?.IsDead ?? false))
			Collapse("torso destroyed");
	}

	private bool IsDestroyed(PartSlot slot)
	{
		return _assembler != null
			&& _assembler.Hardpoints.TryGetValue(slot, out var hp)
			&& hp.IsDestroyed;
	}

	private void Collapse(string reason)
	{
		if (_collapsed)
			return;
		_collapsed = true;
		CancelSelfDestruct();

		var torso = GetTorso();
		if (torso is { IsDestroyed: false })
		{
			EmitSignal(SignalName.ComponentDestroyed, (int)PartSlot.Torso);
			OnComponentDestroyed(torso);
		}

		GD.Print($"{_mech?.Name} collapsed: {reason}");
		EmitSignal(SignalName.MechCollapsed);
		_mech?.SetControlsEnabled(false);
	}

	private void OnTorsoHealthDied()
	{
		var torso = GetTorso();
		if (torso is { IsDestroyed: false })
		{
			EmitSignal(SignalName.ComponentDestroyed, (int)PartSlot.Torso);
			OnComponentDestroyed(torso);
		}

		Collapse("torso destroyed");
	}

	private Hardpoint? GetTorso()
	{
		if (_assembler == null)
			return null;
		return _assembler.Hardpoints.TryGetValue(PartSlot.Torso, out var torso) ? torso : null;
	}

	private void DetonateSelfDestruct()
	{
		if (_mech == null || _collapsed)
			return;

		CancelSelfDestruct();
		var origin = _mech.GlobalPosition + Vector3.Up;
		var parent = _mech.GetTree().CurrentScene ?? _mech.GetParent();
		if (parent != null)
		{
			ShatterBurst.Spawn(parent, origin, new Color(1f, 0.35f, 0.1f), new Vector3(3f, 3f, 3f), 28);
			DamageRadius(parent, origin, SelfDestructRadius, SelfDestructDamage, _mech);
		}

		Collapse("self-destruct");
	}

	private static void DamageRadius(Node sceneRoot, Vector3 origin, float radius, float damage, Node source)
	{
		foreach (var damageable in FindDamageables(sceneRoot))
		{
			if (damageable.GetParent() == source || source.IsAncestorOf(damageable))
				continue;

			var host = damageable.GetParent() as Node3D;
			if (host == null)
				continue;
			if (host.GlobalPosition.DistanceTo(origin) > radius)
				continue;

			if (host is MechController otherMech && otherMech.Integrity != null)
				otherMech.Integrity.ReceiveHit(damage, origin, null, false);
			else
				damageable.ApplyDamage(damage);
		}
	}

	private static IEnumerable<Damageable> FindDamageables(Node root)
	{
		foreach (var child in root.GetChildren())
		{
			if (child is Damageable d)
				yield return d;
			foreach (var nested in FindDamageables(child))
				yield return nested;
		}
	}
}
