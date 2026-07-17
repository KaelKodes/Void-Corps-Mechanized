using Godot;

namespace Mechanize;

public struct SupportPilotCommand
{
	public Vector2 Move;
	public Vector3 AimPoint;
	public bool Fire;
}

/// <summary>
/// Lightweight FSM for fodder: acquire hostile, chase/hold, fire with LoS.
/// Prefers enemy mechs over other support.
/// </summary>
public partial class SupportPilotAI : Node
{
	private SupportUnit? _unit;
	private Node3D? _target;
	private float _rethink;
	private float _strafeSign = 1f;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		_unit = GetParentOrNull<SupportUnit>();
		_strafeSign = _rng.Randf() > 0.5f ? 1f : -1f;
		_rethink = _rng.RandfRange(0.4f, 1.0f);
	}

	public SupportPilotCommand BuildCommand(float dt)
	{
		var cmd = new SupportPilotCommand
		{
			Move = Vector2.Zero,
			AimPoint = _unit?.GlobalPosition ?? Vector3.Zero,
			Fire = false
		};

		if (_unit == null || !_unit.IsAlive || _unit.Data == null)
			return cmd;

		_rethink -= dt;
		if (_rethink <= 0f || !TeamUtil.IsAliveCombatant(_target))
		{
			_target = AcquireTarget();
			_rethink = _rng.RandfRange(0.5f, 1.2f);
			if (_rng.Randf() < 0.3f)
				_strafeSign *= -1f;
		}

		if (_target == null)
			return cmd;

		var myPos = _unit.GlobalPosition;
		var theirPos = TeamUtil.GetAimPoint(_target);
		cmd.AimPoint = theirPos;

		var to = theirPos - myPos;
		to.Y = 0f;
		var distance = to.Length();
		var data = _unit.Data;

		if (distance > data.VisionRange)
		{
			_target = null;
			return cmd;
		}

		var hasLos = HasLineOfSight(myPos + Vector3.Up * 1.0f, theirPos);
		var dir = distance > 0.1f ? to / distance : -_unit.GlobalTransform.Basis.Z;
		var moveDir = dir;
		Vector3? clearAim = null;

		if (!hasLos)
		{
			clearAim = FindDestructibleBlockerAim(myPos + Vector3.Up * 1.0f, theirPos);
			moveDir = FindFlankDirection(dir);
			if (clearAim.HasValue)
				cmd.AimPoint = clearAim.Value;
		}

		if (!_unit.IsStaticUnit)
		{
			switch (data.Kind)
			{
				case SupportUnitKind.ScoutBuggy:
					if (!hasLos || distance > data.Range * 0.75f)
						cmd.Move = new Vector2(moveDir.X, moveDir.Z);
					else
					{
						var right = dir.Cross(Vector3.Up).Normalized();
						var kite = (-dir * 0.35f + right * _strafeSign).Normalized();
						cmd.Move = new Vector2(kite.X, kite.Z);
					}
					break;

				default: // light tank
					if (!hasLos || distance > data.Range * 0.85f)
						cmd.Move = new Vector2(moveDir.X, moveDir.Z);
					else if (distance < data.Range * 0.45f)
						cmd.Move = new Vector2(-dir.X, -dir.Z) * 0.6f;
					else
					{
						var right = dir.Cross(Vector3.Up).Normalized();
						var strafe = (right * _strafeSign * 0.8f + dir * 0.15f).Normalized();
						cmd.Move = new Vector2(strafe.X, strafe.Z);
					}
					break;
			}
		}

		if ((hasLos || clearAim.HasValue) && distance <= data.Range * 1.15f)
			cmd.Fire = true;

		return cmd;
	}

	private Vector3? FindDestructibleBlockerAim(Vector3 from, Vector3 to)
	{
		var space = _unit?.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return null;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 1;
		if (_unit != null)
			query.Exclude = new Godot.Collections.Array<Rid> { _unit.GetRid() };
		var hit = space.IntersectRay(query);
		if (hit.Count == 0)
			return null;

		var collider = hit["collider"].AsGodotObject() as Node;
		var current = collider;
		while (current != null)
		{
			var dmg = current as Damageable ?? current.GetNodeOrNull<Damageable>("Damageable");
			if (dmg != null && !dmg.IsDead)
				return hit["position"].AsVector3();
			current = current.GetParent();
		}

		return null;
	}

	private Vector3 FindFlankDirection(Vector3 preferred)
	{
		if (_unit == null)
			return preferred;

		var origin = _unit.GlobalPosition + Vector3.Up * 0.9f;
		var space = _unit.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return preferred;

		var ahead = PhysicsRayQueryParameters3D.Create(origin, origin + preferred * 14f);
		ahead.CollisionMask = 1;
		ahead.Exclude = new Godot.Collections.Array<Rid> { _unit.GetRid() };
		var block = space.IntersectRay(ahead);
		if (block.Count > 0)
		{
			var normal = block["normal"].AsVector3();
			normal.Y = 0f;
			if (normal.LengthSquared() > 0.01f)
			{
				normal = normal.Normalized();
				var along = normal.Cross(Vector3.Up).Normalized() * _strafeSign;
				var slide = along * 1.3f + normal * 0.5f + preferred * 0.15f;
				slide.Y = 0f;
				if (slide.LengthSquared() > 0.01f)
					return slide.Normalized();
			}
		}

		var right = preferred.Cross(Vector3.Up);
		if (right.LengthSquared() < 0.001f)
			right = Vector3.Right;
		return (right.Normalized() * _strafeSign + preferred * 0.25f).Normalized();
	}

	private Node3D? AcquireTarget()
	{
		if (_unit == null)
			return null;

		var root = GetTree()?.CurrentScene;
		if (root == null)
			return null;

		Node3D? bestMech = null;
		Node3D? bestSupport = null;
		Node3D? bestOther = null;
		var bestMechDist = float.MaxValue;
		var bestSupportDist = float.MaxValue;
		var bestOtherDist = float.MaxValue;
		var vision = _unit.Data?.VisionRange ?? 36f;
		var origin = _unit.GlobalPosition;

		foreach (var body in EnumerateCombatants(root))
		{
			if (!TeamUtil.IsAliveCombatant(body))
				continue;
			if (!TeamUtil.IsHostile(_unit.Team, TeamUtil.GetTeam(body)))
				continue;

			var dist = origin.DistanceTo(body.GlobalPosition);
			if (dist > vision)
				continue;

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

		return bestMech ?? bestSupport ?? bestOther;
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

	private bool HasLineOfSight(Vector3 from, Vector3 to)
	{
		var space = _unit?.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return true;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 1;
		if (_unit != null)
			query.Exclude = new Godot.Collections.Array<Rid> { _unit.GetRid() };
		var hit = space.IntersectRay(query);
		return hit.Count == 0;
	}
}
