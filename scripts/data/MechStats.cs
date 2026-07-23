namespace Mechanize;

/// <summary>
/// Snapshot of mech performance derived entirely from equipped parts.
/// </summary>
public sealed class MechStats
{
	// Durability
	/// <summary>Torso structure. This is the MAP's only defeat health pool.</summary>
	public float TorsoHp { get; init; } = 100f;
	public int ShoulderMounts { get; init; }
	public int BackMounts { get; init; }

	// Power
	public int PowerCoreClass { get; init; } = 1;
	public int PowerCoreHousing { get; init; } = 1;
	/// <summary>Core capacity. Installation budget for PowerRequirement totals.</summary>
	public float PowerCapacity { get; init; } = 100f;
	/// <summary>Power generated per second into the operational pool.</summary>
	public float PowerGeneration { get; init; } = 20f;
	/// <summary>Sum of living component PowerRequirement values.</summary>
	public float PowerReserved { get; init; }
	/// <summary>Capacity minus reserved. Size of the rechargeable combat pool.</summary>
	public float OperationalMax { get; init; } = 40f;

	// Heat
	public float HeatCap { get; init; } = 100f;
	public float HeatDissipation { get; init; } = 12f;
	public float IdleHeatPerSec { get; init; } = 1f;
	public float MoveHeatPerSec { get; init; }

	// Sensors (head baseline + component enhancers)
	public float VisionRange { get; init; } = 35f;
	public float VisionAngleDeg { get; init; } = 110f;
	public float CloseTargeting { get; init; } = 0.5f;
	public float ScannerRange { get; init; } = 60f;
	public float ScannerResolution { get; init; } = 0.3f;
	/// <summary>True = passive blips need a clear ray past cover; false = through-walls contact.</summary>
	public bool ScanRequiresLos { get; init; }
	public ScanBlipStyle ScanBlipStyle { get; init; } = ScanBlipStyle.WorldPip;
	/// <summary>Sum of living part MagazineBonus — added to ballistic mag size.</summary>
	public int MagazineBonus { get; init; }
	/// <summary>Sum of living part ReloadSpeedBonus — shortens ballistic reload.</summary>
	public float ReloadSpeedBonus { get; init; }

	// Mobility
	public float WalkSpeed { get; init; } = 10f;
	public float TurnRateDegrees { get; init; } = 80f;
	public float FireRateMultiplier { get; init; } = 1f;
	public bool CanSprint { get; init; }
	public float SprintMultiplier { get; init; } = 1.45f;
	public float SprintHeatPerSec { get; init; } = 18f;
	public float SprintPowerLoad { get; init; } = 25f;
	public LegMode LegMode { get; init; } = LegMode.Locked;
	public LegType LegType { get; init; } = LegType.Bipedal;

	// Thruster (dash)
	public bool HasThruster { get; init; }
	public float DashSpeed { get; init; }
	public float DashDuration { get; init; } = 0.18f;
	public float DashCooldown { get; init; } = 1.2f;
	public float DashPowerCost { get; init; }
	public float DashHeat { get; init; }

	// Booster (hold-to-thrust flight)
	public bool HasBooster { get; init; }
	/// <summary>Climb thrust while Space is held (m/s target up-rate).</summary>
	public float JumpImpulse { get; init; }
	/// <summary>Fuel seconds of booster flight; refills on landing.</summary>
	public float JumpDuration { get; init; } = 1.1f;
	/// <summary>Continuous power draw while thrusting.</summary>
	public float JumpPowerCost { get; init; }
	/// <summary>Heat per second while thrusting.</summary>
	public float JumpHeat { get; init; }

	/// <summary>Sum of installed non-empty part weights (includes destroyed parts).</summary>
	public float TotalWeight { get; init; }
	/// <summary>Living legs LoadRating. Zero when legs are missing/destroyed.</summary>
	public float LoadRating { get; init; }
	/// <summary>TotalWeight / LoadRating.</summary>
	public float LoadRatio { get; init; } = 1f;
	/// <summary>1 at/under rating → 0 at 200% rating.</summary>
	public float WeightMoveMultiplier { get; init; } = 1f;
	/// <summary>(WeightMoveMultiplier)² — turn degrades faster under overload.</summary>
	public float WeightTurnMultiplier { get; init; } = 1f;

	public static MechStats BlindFallback => new()
	{
		TorsoHp = 40f,
		PowerCapacity = 40f,
		PowerGeneration = 8f,
		PowerReserved = 0f,
		OperationalMax = 40f,
		HeatCap = 60f,
		HeatDissipation = 6f,
		IdleHeatPerSec = 2f,
		VisionRange = 12f,
		VisionAngleDeg = 50f,
		CloseTargeting = 0.15f,
		ScannerRange = 20f,
		ScannerResolution = 0.1f,
		ScanRequiresLos = true,
		ScanBlipStyle = ScanBlipStyle.WorldPip,
		WalkSpeed = 6f,
		TurnRateDegrees = 50f
	};
}
