namespace Mechanize;

/// <summary>Shared projectile silhouettes. Many weapons share one style.</summary>
public enum ProjectileStyle
{
	Slug,
	Shell,
	ApNeedle,
	CarbineTracer,
	Autocannon,
	ScatterPellet,
	EnergyBolt,
	EnergyLance,
	BeamSlug,
	CoilPulse,
	Spark,
	DumbRocket,
	Seeker,
	ArcMicrotorp,
	HazardOrb
}

public static class ProjectileStyleUtil
{
	public static ProjectileStyle FromPart(PartData? part)
	{
		if (part == null)
			return ProjectileStyle.CarbineTracer;

		return part.WeaponFamily switch
		{
			WeaponFamily.Energy => FromEnergy(part),
			WeaponFamily.Missile => FromMissile(part),
			WeaponFamily.Ballistic => FromBallistic(part),
			_ => ProjectileStyle.CarbineTracer
		};
	}

	public static ProjectileStyle FromSupport(SupportUnitData? data)
	{
		if (data == null)
			return ProjectileStyle.CarbineTracer;
		// MAD guns read as light ballistics.
		return data.FireRate >= 4f
			? ProjectileStyle.Autocannon
			: ProjectileStyle.CarbineTracer;
	}

	public static bool IsMissile(ProjectileStyle style) =>
		style is ProjectileStyle.DumbRocket or ProjectileStyle.Seeker or ProjectileStyle.ArcMicrotorp;

	private static ProjectileStyle FromEnergy(PartData part)
	{
		if (part.FireRate >= 9f)
			return ProjectileStyle.Spark;
		if (part.Damage >= 20f && part.FireRate <= 1.2f)
			return ProjectileStyle.CoilPulse;
		if (part.VisualKind == "energy" && part.Range >= 50f && part.ProjectileSpeed >= 90f)
			return part.Damage >= 15f ? ProjectileStyle.BeamSlug : ProjectileStyle.EnergyLance;
		if (part.Id.Contains("prism") || part.Id.Contains("oracle"))
			return part.Id.Contains("prism") ? ProjectileStyle.BeamSlug : ProjectileStyle.EnergyLance;
		if (part.Id.Contains("surge") || part.Id.Contains("well"))
			return ProjectileStyle.CoilPulse;
		if (part.Id.Contains("arc") || part.Id.Contains("oracle") || part.Range >= 55f)
			return ProjectileStyle.EnergyLance;
		return ProjectileStyle.EnergyBolt;
	}

	private static ProjectileStyle FromMissile(PartData part)
	{
		if (part.ManufacturerId == "lumina"
		    || part.Id.Contains("arc")
		    || part.Id.Contains("ghost")
		    || part.Id.Contains("prism"))
			return ProjectileStyle.ArcMicrotorp;
		if (part.MissileGuidance != MissileGuidanceMode.Paint)
			return ProjectileStyle.Seeker;
		return ProjectileStyle.DumbRocket;
	}

	private static ProjectileStyle FromBallistic(PartData part)
	{
		if (part.Id.Contains("scatter"))
			return ProjectileStyle.ScatterPellet;

		if (part.VisualKind == "rifle")
		{
			if (part.TargetingMode == TargetingMode.AimedComponent || part.Range >= 55f)
				return ProjectileStyle.ApNeedle;
			if (part.FireRate >= 8f)
				return ProjectileStyle.CarbineTracer;
			if (part.FireRate >= 5.5f && part.Damage <= 8f)
				return ProjectileStyle.CarbineTracer;
			return ProjectileStyle.CarbineTracer;
		}

		// Cannons
		if (part.FireRate >= 3.5f)
			return ProjectileStyle.Autocannon;
		if (part.Damage >= 25f || part.Id.Contains("maul") || part.Id.Contains("anvil")
		    || part.Id.Contains("pile") || part.Id.Contains("mortar"))
			return ProjectileStyle.Shell;
		return ProjectileStyle.Slug;
	}
}
