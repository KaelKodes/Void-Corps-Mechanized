using Godot;

namespace Mechanize;

public static class TeamUtil
{
	public static TeamId GetTeam(Node? node)
	{
		var current = node;
		while (current != null)
		{
			if (current is MechController mech)
				return mech.Team;
			if (current is SupportUnit support)
				return support.Team;
			if (current is EscortAsset)
				return TeamId.Player;
			current = current.GetParent();
		}

		return TeamId.Neutral;
	}

	public static bool IsHostile(TeamId a, TeamId b)
	{
		if (a == TeamId.Neutral || b == TeamId.Neutral)
			return false;
		return a != b;
	}

	public static bool IsAliveCombatant(Node3D? node)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
			return false;
		if (node is MechController mech)
			return mech.Integrity?.IsCollapsed != true && mech.Health?.IsDead != true;
		if (node is SupportUnit support)
			return support.IsAlive;
		if (node is EscortAsset escort)
			return !escort.IsDestroyed && !escort.HasArrived;
		return false;
	}

	public static Vector3 GetAimPoint(Node3D node)
	{
		if (node is SupportUnit support)
			return support.GetAimPoint();
		if (node is EscortAsset)
			return node.GlobalPosition + Vector3.Up * 1.1f;
		return node.GlobalPosition + Vector3.Up * 1.3f;
	}

	public static Vector3 GetVelocity(Node3D node)
	{
		if (node is CharacterBody3D body)
			return body.Velocity;
		return Vector3.Zero;
	}
}
