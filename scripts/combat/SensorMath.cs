using Godot;

namespace Mechanize;

/// <summary>
/// Shared sensor / vision checks for player and AI (no 360 cheat).
/// </summary>
public static class SensorMath
{
	public static bool IsInCombatVision(
		Vector3 viewerPos,
		Vector3 viewerForward,
		Vector3 targetPos,
		float visionRange,
		float visionAngleDeg)
	{
		var to = targetPos - viewerPos;
		to.Y = 0f;
		var dist = to.Length();
		if (dist > visionRange || dist < 0.05f)
			return false;

		viewerForward.Y = 0f;
		if (viewerForward.LengthSquared() < 0.001f)
			return true;

		var half = Mathf.DegToRad(visionAngleDeg * 0.5f);
		var angle = viewerForward.Normalized().AngleTo(to / dist);
		return angle <= half;
	}

	public static Vector3 AimForward(Node3D mech, Node3D? upperBody)
	{
		if (upperBody != null)
		{
			var f = -upperBody.GlobalTransform.Basis.Z;
			f.Y = 0f;
			if (f.LengthSquared() > 0.001f)
				return f.Normalized();
		}

		var forward = -mech.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		return forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
	}
}
