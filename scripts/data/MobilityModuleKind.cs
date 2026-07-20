namespace Mechanize;

/// <summary>
/// Leg package mobility module. Catalogue legs set this; assembler rolls into MechStats.
/// Boosters = jump; Thrusters = dash. Mutually exclusive on a single part.
/// </summary>
public enum MobilityModuleKind
{
	None = 0,
	/// <summary>Horizontal dash (tap sprint when thrusters are equipped).</summary>
	Thruster = 1,
	/// <summary>Vertical jump (jump bind when boosters are equipped).</summary>
	Booster = 2
}
