namespace Mechanize;

/// <summary>Matches project.godot layer_names 3d_physics.</summary>
public static class PhysicsLayers
{
	public const uint World = 1;
	public const uint Mechs = 2;
	public const uint Projectiles = 4;
	public const uint Targets = 8;

	public const uint MechMask = World | Targets;
	public const uint ProjectileMask = World | Mechs | Targets;
}
