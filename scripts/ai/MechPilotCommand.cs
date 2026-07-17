using Godot;

namespace Mechanize;

public enum PilotTacticalState
{
	Hunt,
	Engage,
	Strafe,
	BreakContact,
	Hold
}

/// <summary>
/// Intent produced by an AI pilot and consumed by MechController through the same
/// locomotion / aim / fire pipeline as the player.
/// </summary>
public struct MechPilotCommand
{
	public Vector3 AimPoint;
	public PartSlot? AimedComponent;
	public float Turn;      // -1..1 locked chassis yaw
	public float Throttle;  // -1..1 forward/back
	public Vector2 Move;    // gimbaled strafe plane (x right, y forward)
	public bool FirePrimary;
	public bool FireSecondary;
	public bool Sprint;
	public int AbilityIndex; // -1 none
}
