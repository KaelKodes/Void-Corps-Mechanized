namespace Mechanize;

/// <summary>
/// Leg package mobility module. Catalogue legs set this; assembler rolls into MechStats.
/// Dedicated booster / thruster kits emphasize one axis but still ship Both (stock other).
/// Standard legs always include stock jump + dash via <see cref="Both"/>.
/// </summary>
public enum MobilityModuleKind
{
	None = 0,
	/// <summary>Horizontal dash (tap sprint when thrusters are equipped).</summary>
	Thruster = 1,
	/// <summary>Hold-to-thrust flight (Space). Fuel = JumpDuration; climb = JumpImpulse.</summary>
	Booster = 2,
	/// <summary>Jump + dash on one package (stock on every legs kit).</summary>
	Both = 3
}
