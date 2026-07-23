using Godot;

namespace Mechanize;

/// <summary>
/// Day/Night lighting derived from authored night claim colors (geometry stays the same).
/// </summary>
public static class ClaimAtmosphere
{
	public readonly struct Lighting
	{
		public Color AmbientColor { get; init; }
		public float AmbientEnergy { get; init; }
		public Color SunColor { get; init; }
		public float SunEnergy { get; init; }
		public Vector3 SunRotationDegrees { get; init; }
		public float Exposure { get; init; }
	}

	public static Lighting Resolve(ClaimArenaLayout layout, ArenaPeriod period)
	{
		if (period == ArenaPeriod.Night)
		{
			return new Lighting
			{
				AmbientColor = layout.AmbientColor,
				AmbientEnergy = layout.AmbientEnergy,
				SunColor = layout.SunColor,
				SunEnergy = layout.SunEnergy,
				SunRotationDegrees = layout.SunRotationDegrees,
				Exposure = 1.0f
			};
		}

		// Day — lift from night authoring into a readable daytime pad.
		var ambient = layout.AmbientColor.Lightened(0.28f).Lerp(new Color(0.72f, 0.78f, 0.88f), 0.45f);
		var sun = layout.SunColor.Lightened(0.2f).Lerp(new Color(1f, 0.96f, 0.88f), 0.55f);
		var rot = layout.SunRotationDegrees;
		// Higher sun angle for day (more overhead).
		rot.X = Mathf.Clamp(rot.X - 25f, -75f, -20f);

		return new Lighting
		{
			AmbientColor = ambient,
			AmbientEnergy = Mathf.Clamp(layout.AmbientEnergy * 1.35f + 0.25f, 0.7f, 1.35f),
			SunColor = sun,
			SunEnergy = Mathf.Clamp(layout.SunEnergy * 1.8f + 0.6f, 1.4f, 3.2f),
			SunRotationDegrees = rot,
			Exposure = 1.05f
		};
	}
}
