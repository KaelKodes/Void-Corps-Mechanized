using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Local-pilot combat markers: green ally fodder chevrons in close sensor range,
/// and a red lock chevron on the current TAB target (drawn in world when in view).
/// Off-view locks are handled by <see cref="EnemyTargetSchematic"/>'s screen arrow.
/// </summary>
public partial class CombatChevronMarkers : Node3D
{
	public const string NodeName = "CombatChevronMarkers";

	private static readonly Color AllyGreen = new(0.22f, 0.92f, 0.38f, 0.95f);
	private static readonly Color LockRed = new(1f, 0.18f, 0.14f, 1f);

	private MechController? _mech;
	private readonly Dictionary<ulong, Label3D> _allyChevrons = new();
	private Label3D? _lockChevron;

	public static void EnsureOn(MechController host)
	{
		if (host.HangarDisplayOnly || !host.IsPlayerControlled)
			return;
		if (host.GetNodeOrNull(NodeName) != null)
			return;
		host.AddChild(new CombatChevronMarkers { Name = NodeName });
	}

	public override void _Ready()
	{
		_mech = GetParent() as MechController;
		TopLevel = true;
	}

	public override void _Process(double delta)
	{
		if (_mech == null || !GodotObject.IsInstanceValid(_mech))
			return;
		if (!_mech.IsLocalPilot || _mech.HangarDisplayOnly
		    || _mech.Integrity?.IsCollapsed == true
		    || _mech.Health?.IsDead == true)
		{
			ClearAllies();
			SetLockVisible(false);
			return;
		}

		UpdateAllyChevrons();
		UpdateLockChevron();
	}

	private void UpdateAllyChevrons()
	{
		var stats = _mech!.Assembler?.Stats ?? MechStats.BlindFallback;
		var closeRange = Mathf.Max(8f, stats.VisionRange);
		var seen = new HashSet<ulong>();
		var tree = _mech.GetTree();
		if (tree == null)
		{
			ClearAllies();
			return;
		}

		foreach (var node in tree.GetNodesInGroup("support"))
		{
			if (node is not SupportUnit unit || !unit.IsAlive)
				continue;
			if (!IsFieldPresent(unit))
				continue;
			if (TeamUtil.IsHostile(_mech.Team, unit.Team))
				continue;
			if (_mech.GlobalPosition.DistanceTo(unit.GlobalPosition) > closeRange)
				continue;

			var id = unit.GetInstanceId();
			seen.Add(id);
			var chevron = EnsureAlly(id);
			chevron.GlobalPosition = unit.GetAimPoint() + Vector3.Up * 1.15f;
			Billboard(chevron);
		}

		// Also mark allied mechs in close range (same team MAPs / co-op).
		foreach (var node in tree.GetNodesInGroup("mechs"))
		{
			if (node is not MechController other || other == _mech)
				continue;
			if (other.HangarDisplayOnly)
				continue;
			if (!IsFieldPresent(other))
				continue;
			if (other.Health?.IsDead == true || other.Integrity?.IsCollapsed == true)
				continue;
			if (TeamUtil.IsHostile(_mech.Team, other.Team))
				continue;
			if (_mech.GlobalPosition.DistanceTo(other.GlobalPosition) > closeRange)
				continue;

			var id = other.GetInstanceId();
			seen.Add(id);
			var chevron = EnsureAlly(id);
			chevron.GlobalPosition = other.GlobalPosition + Vector3.Up * 3.2f;
			Billboard(chevron);
		}

		var stale = new List<ulong>();
		foreach (var id in _allyChevrons.Keys)
		{
			if (!seen.Contains(id))
				stale.Add(id);
		}

		foreach (var id in stale)
		{
			if (_allyChevrons.Remove(id, out var label) && GodotObject.IsInstanceValid(label))
				label.QueueFree();
		}
	}

	private void UpdateLockChevron()
	{
		var lockNode = _mech!.SensorLockTarget;
		if (lockNode == null || !GodotObject.IsInstanceValid(lockNode))
		{
			SetLockVisible(false);
			return;
		}

		var aim = ResolveAimPoint(lockNode);
		var cam = _mech.GetViewport()?.GetCamera3D();
		if (cam == null || !cam.Current)
		{
			SetLockVisible(false);
			return;
		}

		var to = aim - cam.GlobalPosition;
		var forward = -cam.GlobalTransform.Basis.Z;
		var inFront = to.Normalized().Dot(forward.Normalized()) > 0.08f;
		if (!inFront)
		{
			// Off-view / behind — screen arrow owns the cue.
			SetLockVisible(false);
			return;
		}

		var chevron = EnsureLock();
		chevron.GlobalPosition = aim + Vector3.Up * 1.35f;
		Billboard(chevron);
		chevron.Visible = true;
	}

	private Label3D EnsureAlly(ulong id)
	{
		if (_allyChevrons.TryGetValue(id, out var existing) && GodotObject.IsInstanceValid(existing))
			return existing;

		var label = MakeChevron(AllyGreen, 0.85f);
		AddChild(label);
		_allyChevrons[id] = label;
		return label;
	}

	private Label3D EnsureLock()
	{
		if (_lockChevron != null && GodotObject.IsInstanceValid(_lockChevron))
			return _lockChevron;
		_lockChevron = MakeChevron(LockRed, 1.15f);
		AddChild(_lockChevron);
		return _lockChevron;
	}

	private void SetLockVisible(bool visible)
	{
		if (_lockChevron != null && GodotObject.IsInstanceValid(_lockChevron))
			_lockChevron.Visible = visible;
	}

	private void ClearAllies()
	{
		foreach (var label in _allyChevrons.Values)
		{
			if (GodotObject.IsInstanceValid(label))
				label.QueueFree();
		}

		_allyChevrons.Clear();
	}

	private static Label3D MakeChevron(Color color, float scale)
	{
		return new Label3D
		{
			Text = "▼",
			FontSize = 28,
			PixelSize = 0.0018f * scale,
			Modulate = color,
			OutlineSize = 4,
			OutlineModulate = new Color(0f, 0f, 0f, 0.75f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			FixedSize = true,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			RenderPriority = 20
		};
	}

	private static void Billboard(Label3D label)
	{
		// Label3D Billboard handles facing; keep upright.
		label.Rotation = Vector3.Zero;
	}

	/// <summary>
	/// Arena pools despawned units at (0,-50,0) while leaving them alive + friendly.
	/// Chevrons use NoDepthTest, so those ghosts draw through the floor at map center.
	/// </summary>
	private static bool IsFieldPresent(Node3D body)
	{
		if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
			return false;
		if (!body.Visible || body.ProcessMode == ProcessModeEnum.Disabled)
			return false;
		// Pooled under the pad / not yet deployed onto the fight floor.
		if (body.GlobalPosition.Y < -5f)
			return false;
		return true;
	}

	public static Vector3 ResolveAimPoint(Node3D node) => node switch
	{
		SupportUnit support => support.GetAimPoint(),
		MechController mech => mech.GlobalPosition + Vector3.Up * 2.4f,
		_ => node.GlobalPosition + Vector3.Up * 1.5f
	};
}
