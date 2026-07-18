using Godot;

namespace Mechanize;

/// <summary>
/// Physical size tier for a MAP/MAD frame.
/// Distinct from manned (MAP) vs unmanned (MAD) operation class.
/// </summary>
public enum MechChassisClass
{
	/// <summary>Standard field MAP — player and most rival pilots.</summary>
	Standard = 0,
	/// <summary>Superheavy claim-breaker. Roughly 4× a standard MAP silhouette.</summary>
	Titan = 1
}

public static class MechChassisClassUtil
{
	public const float StandardScale = 1f;
	public const float TitanScale = 4f;

	public static float VisualScale(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan ? TitanScale : StandardScale;

	public static float HullMultiplier(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan ? 4.25f : 1f;

	public static float SpeedMultiplier(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan ? 0.55f : 1f;

	public static float PreferredRangeBonus(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan ? 14f : 0f;

	public static string Label(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan ? "Titan-class MAP" : "MAP";

	public static string ShortLabel(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan ? "TITAN" : "MAP";

	public static Color Accent(MechChassisClass chassisClass) =>
		chassisClass == MechChassisClass.Titan
			? new Color(0.92f, 0.42f, 0.28f)
			: new Color(0.7f, 0.78f, 0.86f);
}
