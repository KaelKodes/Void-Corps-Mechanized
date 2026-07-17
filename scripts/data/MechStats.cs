namespace Mechanize;

/// <summary>
/// Snapshot of mech performance derived entirely from equipped parts.
/// </summary>
public sealed class MechStats
{
	// Durability
	public float HullHp { get; init; } = 100f;
	public int ShoulderMounts { get; init; }
	public int BackMounts { get; init; }

	// Power
	public int PowerCoreClass { get; init; } = 1;
	public int PowerCoreHousing { get; init; } = 1;
	public float PowerCapacity { get; init; } = 100f;
	public float PowerOutput { get; init; } = 20f;

	// Heat
	public float HeatCap { get; init; } = 100f;
	public float HeatDissipation { get; init; } = 12f;
	public float IdleHeatPerSec { get; init; } = 1f;
	public float MoveHeatPerSec { get; init; }

	// Sensors (head)
	public float VisionRange { get; init; } = 35f;
	public float VisionAngleDeg { get; init; } = 110f;
	public float CloseTargeting { get; init; } = 0.5f;
	public float ScannerRange { get; init; } = 60f;
	public float ScannerResolution { get; init; } = 0.3f;

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

	public static MechStats BlindFallback => new()
	{
		HullHp = 40f,
		PowerCapacity = 40f,
		PowerOutput = 8f,
		HeatCap = 60f,
		HeatDissipation = 6f,
		IdleHeatPerSec = 2f,
		VisionRange = 12f,
		VisionAngleDeg = 50f,
		CloseTargeting = 0.15f,
		ScannerRange = 20f,
		ScannerResolution = 0.1f,
		WalkSpeed = 6f,
		TurnRateDegrees = 50f
	};
}
