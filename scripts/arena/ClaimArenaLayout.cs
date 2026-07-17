using Godot;

namespace Mechanize;

/// <summary>
/// Per-claim arena presentation: spawns, cover, crates, atmosphere.
/// One shared arena scene; layouts swap at load.
/// Drop beacons are derived from PlayerSpawn / enemy spawn pads at runtime
/// (player always; enemy only for mid-match viaDropBeacon arrivals).
/// </summary>
public sealed class ClaimArenaLayout
{
	public readonly struct CoverPiece
	{
		public CoverPiece(CoverKind kind, Vector3 position, float yawDegrees = 0f, float scale = 1f)
		{
			Kind = kind;
			Position = position;
			YawDegrees = yawDegrees;
			Scale = scale;
		}

		public CoverKind Kind { get; }
		/// <summary>Footprint center on the ground plane (Y ignored).</summary>
		public Vector3 Position { get; }
		public float YawDegrees { get; }
		public float Scale { get; }
	}

	public string ClaimCode { get; init; } = "";
	public Vector3 PlayerSpawn { get; init; }
	public Vector3 EnemySpawnA { get; init; }
	public Vector3 EnemySpawnB { get; init; }
	public CoverPiece[] Covers { get; init; } = [];
	public Vector3[] CratePositions { get; init; } = [];
	public Color FloorColor { get; init; }
	public Color WallColor { get; init; }
	public Color SkyColor { get; init; }
	public Color AmbientColor { get; init; }
	public float AmbientEnergy { get; init; } = 0.65f;
	public Color SunColor { get; init; } = Colors.White;
	public float SunEnergy { get; init; } = 1.15f;
	public Vector3 SunRotationDegrees { get; init; } = new(-45f, -35f, 0f);

	public static ClaimArenaLayout ForClaim(VoidCorpsIdentity.ClaimSite claim)
	{
		foreach (var layout in All)
		{
			if (layout.ClaimCode == claim.Code)
				return layout;
		}

		return All[0];
	}

	public static readonly ClaimArenaLayout[] All =
	[
		// Open disputed rock — jersey barriers, wrecked freight, sparse hard cover.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM 7-ORBITAL",
			PlayerSpawn = new Vector3(32f, 0f, 32f),
			EnemySpawnA = new Vector3(-32f, 0f, -32f),
			EnemySpawnB = new Vector3(32f, 0f, -32f),
			Covers =
			[
				new(CoverKind.BarrierRow, new Vector3(-8f, 0f, -6f), 28f),
				new(CoverKind.ShippingContainer, new Vector3(11f, 0f, 9f), -18f),
				new(CoverKind.ConcreteBarrier, new Vector3(2f, 0f, -14f), 45f),
				new(CoverKind.SemiTrailer, new Vector3(-16f, 0f, 12f), -35f, 0.9f),
				new(CoverKind.IndustrialShed, new Vector3(18f, 0f, -16f), 15f, 0.85f),
				new(CoverKind.OilTank, new Vector3(-22f, 0f, -8f), 0f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(6f, 0f, 18f), -10f),
				new(CoverKind.ShippingContainer, new Vector3(-4f, 0f, 4f), 70f, 0.95f)
			],
			CratePositions =
			[
				new Vector3(10f, 0f, -8f),
				new Vector3(-12f, 0f, 6f),
				new Vector3(6f, 0f, 16f),
				new Vector3(-4f, 0f, -18f)
			],
			FloorColor = new Color(0.28f, 0.22f, 0.16f),
			WallColor = new Color(0.42f, 0.34f, 0.26f),
			SkyColor = new Color(0.18f, 0.14f, 0.1f),
			AmbientColor = new Color(0.7f, 0.58f, 0.42f),
			AmbientEnergy = 0.72f,
			SunColor = new Color(1f, 0.92f, 0.75f),
			SunEnergy = 1.35f,
			SunRotationDegrees = new Vector3(-55f, -20f, 0f)
		},

		// Abandoned relay pad — container lanes, tanks, warehouse spine.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM GRID-ASH",
			PlayerSpawn = new Vector3(34f, 0f, 28f),
			EnemySpawnA = new Vector3(-34f, 0f, -28f),
			EnemySpawnB = new Vector3(-32f, 0f, 26f),
			Covers =
			[
				new(CoverKind.ContainerStack, new Vector3(0f, 0f, 0f), 90f),
				new(CoverKind.ShippingContainer, new Vector3(0f, 0f, -8f), 90f),
				new(CoverKind.ShippingContainer, new Vector3(0f, 0f, 8f), 90f),
				new(CoverKind.BarrierRow, new Vector3(12f, 0f, -14f), 0f),
				new(CoverKind.BarrierRow, new Vector3(-12f, 0f, 14f), 0f),
				new(CoverKind.Warehouse, new Vector3(18f, 0f, 4f), 90f, 0.75f),
				new(CoverKind.OilTankCluster, new Vector3(-18f, 0f, -6f), 20f, 0.8f),
				new(CoverKind.PipeRack, new Vector3(0f, 0f, 22f), 0f),
				new(CoverKind.SemiTrailer, new Vector3(14f, 0f, 16f), -90f, 0.85f),
				new(CoverKind.IndustrialShed, new Vector3(-14f, 0f, -16f), 0f, 0.9f)
			],
			CratePositions =
			[
				new Vector3(18f, 0f, -10f),
				new Vector3(-18f, 0f, 10f),
				new Vector3(8f, 0f, 18f),
				new Vector3(-8f, 0f, -18f),
				new Vector3(0f, 0f, -8f)
			],
			FloorColor = new Color(0.18f, 0.2f, 0.22f),
			WallColor = new Color(0.32f, 0.36f, 0.4f),
			SkyColor = new Color(0.1f, 0.12f, 0.14f),
			AmbientColor = new Color(0.55f, 0.62f, 0.68f),
			AmbientEnergy = 0.55f,
			SunColor = new Color(0.85f, 0.9f, 0.95f),
			SunEnergy = 0.95f,
			SunRotationDegrees = new Vector3(-40f, 50f, 0f)
		},

		// Cold dock shelf — harbor freight + skyline rim + fuel farm.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM BLACK-WHARF",
			PlayerSpawn = new Vector3(28f, 0f, 34f),
			EnemySpawnA = new Vector3(-28f, 0f, -34f),
			EnemySpawnB = new Vector3(30f, 0f, -30f),
			Covers =
			[
				new(CoverKind.ContainerStack, new Vector3(-12f, 0f, -4f), 0f),
				new(CoverKind.ContainerStack, new Vector3(-12f, 0f, 8f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(12f, 0f, -6f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(12f, 0f, 6f), 180f),
				new(CoverKind.BarrierRow, new Vector3(0f, 0f, -10f), 0f),
				new(CoverKind.BarrierRow, new Vector3(0f, 0f, 12f), 0f),
				new(CoverKind.OilTankCluster, new Vector3(-22f, 0f, 16f), 15f, 0.85f),
				new(CoverKind.OilTank, new Vector3(20f, 0f, -14f), -20f, 0.9f),
				new(CoverKind.SemiTrailer, new Vector3(0f, 0f, 0f), 45f, 0.9f),
				new(CoverKind.Warehouse, new Vector3(-20f, 0f, -18f), 25f, 0.7f),
				new(CoverKind.PipeRack, new Vector3(18f, 0f, 14f), -15f, 0.95f),
				// Skyline — rim dressing, not midfield cover.
				new(CoverKind.Skyscraper, new Vector3(-34f, 0f, 0f), 0f, 1.0f),
				new(CoverKind.Skyscraper, new Vector3(-32f, 0f, 10f), 12f, 0.85f),
				new(CoverKind.Skyscraper, new Vector3(34f, 0f, -4f), -8f, 1.1f),
				new(CoverKind.Skyscraper, new Vector3(32f, 0f, 12f), 5f, 0.75f),
				new(CoverKind.Skyscraper, new Vector3(0f, 0f, -36f), 90f, 0.9f),
				new(CoverKind.Skyscraper, new Vector3(-8f, 0f, 36f), -90f, 0.8f)
			],
			CratePositions =
			[
				new Vector3(-6f, 0f, 8f),
				new Vector3(6f, 0f, -6f),
				new Vector3(-18f, 0f, -4f),
				new Vector3(16f, 0f, 6f),
				new Vector3(2f, 0f, 16f)
			],
			FloorColor = new Color(0.12f, 0.14f, 0.18f),
			WallColor = new Color(0.2f, 0.26f, 0.34f),
			SkyColor = new Color(0.04f, 0.06f, 0.1f),
			AmbientColor = new Color(0.35f, 0.48f, 0.62f),
			AmbientEnergy = 0.48f,
			SunColor = new Color(0.55f, 0.7f, 0.9f),
			SunEnergy = 0.75f,
			SunRotationDegrees = new Vector3(-30f, -80f, 0f)
		}
	];
}
