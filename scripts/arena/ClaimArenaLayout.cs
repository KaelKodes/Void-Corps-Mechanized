using Godot;

namespace Mechanize;

/// <summary>
/// Per-claim arena presentation: size class, spawns, cover, crates, atmosphere.
/// One shared arena scene; shell scales and layouts swap at load.
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
	public ArenaSize Size { get; init; } = ArenaSize.Small;
	public float MapVersion { get; init; } = 1.0f;
	/// <summary>Optional X half-extent override. &lt;= 0 uses <see cref="Size"/>.</summary>
	public float CustomHalfExtentX { get; init; } = -1f;
	/// <summary>Optional Z half-extent override. &lt;= 0 uses <see cref="Size"/>.</summary>
	public float CustomHalfExtentZ { get; init; } = -1f;
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

	public float HalfExtentX => CustomHalfExtentX > 0f ? CustomHalfExtentX : ArenaSizeUtil.HalfExtent(Size);
	public float HalfExtentZ => CustomHalfExtentZ > 0f ? CustomHalfExtentZ : ArenaSizeUtil.HalfExtent(Size);
	/// <summary>Largest half-extent — useful for travel-time estimates.</summary>
	public float HalfExtent => Mathf.Max(HalfExtentX, HalfExtentZ);
	public float PadLimitX => HalfExtentX - 4.5f;
	public float PadLimitZ => HalfExtentZ - 4.5f;
	/// <summary>Conservative clamp for callers that still use a single pad limit.</summary>
	public float PadLimit => Mathf.Min(PadLimitX, PadLimitZ);

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
		// ===== Gen 1.0 / SMALL (80×80) =====

		// Open disputed rock — jersey barriers, wrecked freight, sparse hard cover.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM 7-ORBITAL",
			Size = ArenaSize.Small,
			MapVersion = 1.0f,
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
			Size = ArenaSize.Small,
			MapVersion = 1.0f,
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
			Size = ArenaSize.Small,
			MapVersion = 1.0f,
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
		},

		// ===== Gen 2.0 / MEDIUM (96×96) — Slag Foundry =====
		// Cross-shaped pour floor: pipe racks as lanes, furnace sheds on the arms,
		// fuel farm in the SW pocket, freight staging NE.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM SLAG-FOUNDRY",
			Size = ArenaSize.Medium,
			MapVersion = 2.0f,
			PlayerSpawn = new Vector3(38f, 0f, 38f),
			EnemySpawnA = new Vector3(-38f, 0f, -38f),
			EnemySpawnB = new Vector3(36f, 0f, -34f),
			Covers =
			[
				// Furnace spine — N/S pipe corridor
				new(CoverKind.PipeRack, new Vector3(0f, 0f, -18f), 90f, 1.1f),
				new(CoverKind.PipeRack, new Vector3(0f, 0f, 0f), 90f, 1.15f),
				new(CoverKind.PipeRack, new Vector3(0f, 0f, 18f), 90f, 1.1f),
				// E/W slag lanes
				new(CoverKind.BarrierRow, new Vector3(-16f, 0f, 0f), 90f, 1.05f),
				new(CoverKind.BarrierRow, new Vector3(16f, 0f, 0f), 90f, 1.05f),
				new(CoverKind.ConcreteBarrier, new Vector3(-8f, 0f, 10f), 15f),
				new(CoverKind.ConcreteBarrier, new Vector3(10f, 0f, -12f), -20f),
				// Pour sheds
				new(CoverKind.Warehouse, new Vector3(-28f, 0f, 8f), 0f, 0.85f),
				new(CoverKind.IndustrialShed, new Vector3(26f, 0f, -10f), 90f, 1.0f),
				new(CoverKind.IndustrialShed, new Vector3(-24f, 0f, -22f), -15f, 0.95f),
				// Fuel + freight
				new(CoverKind.OilTankCluster, new Vector3(-30f, 0f, 28f), 25f, 0.95f),
				new(CoverKind.OilTank, new Vector3(30f, 0f, 24f), -10f, 1.0f),
				new(CoverKind.ContainerStack, new Vector3(22f, 0f, 32f), 0f, 1.0f),
				new(CoverKind.ShippingContainer, new Vector3(14f, 0f, 28f), 90f),
				new(CoverKind.SemiTrailer, new Vector3(-18f, 0f, -32f), 40f, 1.0f),
				new(CoverKind.SemiTrailer, new Vector3(32f, 0f, -28f), -70f, 0.95f),
				new(CoverKind.ShippingContainer, new Vector3(-6f, 0f, -28f), 0f),
				new(CoverKind.ContainerStack, new Vector3(8f, 0f, 16f), -90f, 0.9f)
			],
			CratePositions =
			[
				new Vector3(20f, 0f, -6f),
				new Vector3(-22f, 0f, 4f),
				new Vector3(6f, 0f, 30f),
				new Vector3(-10f, 0f, -20f),
				new Vector3(28f, 0f, 8f),
				new Vector3(-32f, 0f, -8f)
			],
			FloorColor = new Color(0.22f, 0.16f, 0.12f),
			WallColor = new Color(0.38f, 0.26f, 0.18f),
			SkyColor = new Color(0.14f, 0.08f, 0.06f),
			AmbientColor = new Color(0.75f, 0.42f, 0.28f),
			AmbientEnergy = 0.62f,
			SunColor = new Color(1f, 0.7f, 0.45f),
			SunEnergy = 1.2f,
			SunRotationDegrees = new Vector3(-48f, 25f, 0f)
		},

		// ===== Gen 2.0 / LARGE (112×112) — Spire-Null Plaza =====
		// Open plaza core, monument cover midfield, dense skyscraper rim,
		// freight pockets on the diagonals.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM SPIRE-NULL",
			Size = ArenaSize.Large,
			MapVersion = 2.0f,
			PlayerSpawn = new Vector3(46f, 0f, 44f),
			EnemySpawnA = new Vector3(-46f, 0f, -44f),
			EnemySpawnB = new Vector3(44f, 0f, -42f),
			Covers =
			[
				// Plaza hardpoints
				new(CoverKind.BarrierRow, new Vector3(0f, 0f, -14f), 0f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(0f, 0f, 14f), 0f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(-14f, 0f, 0f), 90f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(14f, 0f, 0f), 90f, 1.1f),
				new(CoverKind.ConcreteBarrier, new Vector3(-6f, 0f, -6f), 45f),
				new(CoverKind.ConcreteBarrier, new Vector3(6f, 0f, 6f), 45f),
				new(CoverKind.ContainerStack, new Vector3(0f, 0f, 0f), 0f, 1.05f),
				// Diagonal freight pockets
				new(CoverKind.Warehouse, new Vector3(-28f, 0f, 22f), 20f, 0.9f),
				new(CoverKind.Warehouse, new Vector3(30f, 0f, -24f), -25f, 0.85f),
				new(CoverKind.IndustrialShed, new Vector3(24f, 0f, 28f), 90f, 1.0f),
				new(CoverKind.OilTankCluster, new Vector3(-32f, 0f, -26f), 15f, 1.0f),
				new(CoverKind.OilTank, new Vector3(36f, 0f, 20f), -30f, 1.05f),
				new(CoverKind.PipeRack, new Vector3(-20f, 0f, 36f), 0f, 1.1f),
				new(CoverKind.SemiTrailer, new Vector3(18f, 0f, -36f), 55f, 1.0f),
				new(CoverKind.SemiTrailer, new Vector3(-36f, 0f, 12f), -80f, 0.95f),
				new(CoverKind.ShippingContainer, new Vector3(22f, 0f, 8f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(-24f, 0f, -8f), 90f),
				new(CoverKind.ContainerStack, new Vector3(8f, 0f, -28f), -15f),
				new(CoverKind.ContainerStack, new Vector3(-10f, 0f, 30f), 20f),
				// Skyline rim — large footprint city wall
				new(CoverKind.Skyscraper, new Vector3(-50f, 0f, -20f), 0f, 1.15f),
				new(CoverKind.Skyscraper, new Vector3(-48f, 0f, 0f), 8f, 1.0f),
				new(CoverKind.Skyscraper, new Vector3(-50f, 0f, 22f), -6f, 1.25f),
				new(CoverKind.Skyscraper, new Vector3(50f, 0f, -16f), 12f, 1.1f),
				new(CoverKind.Skyscraper, new Vector3(48f, 0f, 6f), -10f, 0.95f),
				new(CoverKind.Skyscraper, new Vector3(50f, 0f, 28f), 5f, 1.2f),
				new(CoverKind.Skyscraper, new Vector3(-18f, 0f, -50f), 90f, 1.05f),
				new(CoverKind.Skyscraper, new Vector3(8f, 0f, -52f), 90f, 1.3f),
				new(CoverKind.Skyscraper, new Vector3(28f, 0f, -50f), 90f, 0.9f),
				new(CoverKind.Skyscraper, new Vector3(-24f, 0f, 50f), -90f, 1.1f),
				new(CoverKind.Skyscraper, new Vector3(0f, 0f, 52f), -90f, 1.2f),
				new(CoverKind.Skyscraper, new Vector3(26f, 0f, 50f), -90f, 1.0f)
			],
			CratePositions =
			[
				new Vector3(16f, 0f, -10f),
				new Vector3(-18f, 0f, 12f),
				new Vector3(30f, 0f, 14f),
				new Vector3(-28f, 0f, -14f),
				new Vector3(4f, 0f, 36f),
				new Vector3(-6f, 0f, -34f),
				new Vector3(40f, 0f, -8f),
				new Vector3(-40f, 0f, 6f)
			],
			FloorColor = new Color(0.14f, 0.15f, 0.18f),
			WallColor = new Color(0.22f, 0.26f, 0.32f),
			SkyColor = new Color(0.05f, 0.07f, 0.11f),
			AmbientColor = new Color(0.4f, 0.5f, 0.65f),
			AmbientEnergy = 0.52f,
			SunColor = new Color(0.7f, 0.82f, 1f),
			SunEnergy = 0.9f,
			SunRotationDegrees = new Vector3(-35f, -110f, 0f)
		},

		// ===== Sabotage corridor — long approach (~800 units N/S, ~80 wide) =====
		// South ingress → north uplink. Cover scattered along the full run.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM ECHELON-RUN",
			Size = ArenaSize.Medium,
			MapVersion = 2.3f,
			CustomHalfExtentX = 40f,
			CustomHalfExtentZ = 400f,
			PlayerSpawn = new Vector3(0f, 0f, 385f),
			EnemySpawnA = new Vector3(0f, 0f, -385f),
			EnemySpawnB = new Vector3(16f, 0f, -370f),
			Covers =
			[
				// South approach
				new(CoverKind.BarrierRow, new Vector3(-14f, 0f, 340f), 8f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(10f, 0f, 325f), -12f),
				new(CoverKind.ShippingContainer, new Vector3(18f, 0f, 310f), 90f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(8f, 0f, 280f), -25f),
				new(CoverKind.OilTank, new Vector3(-22f, 0f, 255f), 0f, 0.8f),
				new(CoverKind.BarrierRow, new Vector3(12f, 0f, 220f), -5f, 0.9f),
				// Mid-south
				new(CoverKind.PipeRack, new Vector3(-20f, 0f, 185f), 90f, 0.95f),
				new(CoverKind.ConcreteBarrier, new Vector3(4f, 0f, 168f), 18f),
				new(CoverKind.ShippingContainer, new Vector3(-10f, 0f, 150f), 15f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(16f, 0f, 120f), 40f),
				new(CoverKind.BarrierRow, new Vector3(-8f, 0f, 90f), 0f, 0.85f),
				new(CoverKind.SemiTrailer, new Vector3(20f, 0f, 55f), -70f, 0.9f),
				// Midfield
				new(CoverKind.ContainerStack, new Vector3(-18f, 0f, 20f), 90f, 0.8f),
				new(CoverKind.ConcreteBarrier, new Vector3(6f, 0f, -10f), -15f),
				new(CoverKind.BarrierRow, new Vector3(14f, 0f, -45f), 12f, 0.9f),
				new(CoverKind.OilTank, new Vector3(-24f, 0f, -80f), 20f, 0.75f),
				new(CoverKind.ConcreteBarrier, new Vector3(-6f, 0f, -98f), 8f),
				new(CoverKind.ShippingContainer, new Vector3(10f, 0f, -115f), 0f, 0.9f),
				// Mid-north
				new(CoverKind.PipeRack, new Vector3(22f, 0f, -150f), 0f, 0.9f),
				new(CoverKind.BarrierRow, new Vector3(-16f, 0f, -185f), -8f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(4f, 0f, -220f), 30f),
				new(CoverKind.IndustrialShed, new Vector3(-22f, 0f, -255f), 90f, 0.75f),
				new(CoverKind.ShippingContainer, new Vector3(18f, 0f, -290f), -90f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(-12f, 0f, -305f), -18f),
				// North approach / uplink vicinity
				new(CoverKind.BarrierRow, new Vector3(-10f, 0f, -320f), 5f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(12f, 0f, -345f), -20f),
				new(CoverKind.OilTank, new Vector3(-20f, 0f, -360f), 0f, 0.7f),
				new(CoverKind.BarrierRow, new Vector3(8f, 0f, -375f), 0f, 0.8f)
			],
			CratePositions =
			[
				new Vector3(6f, 0f, 300f),
				new Vector3(-8f, 0f, 200f),
				new Vector3(10f, 0f, 100f),
				new Vector3(-4f, 0f, 0f),
				new Vector3(8f, 0f, -100f),
				new Vector3(-10f, 0f, -200f),
				new Vector3(4f, 0f, -300f),
				new Vector3(-6f, 0f, -350f)
			],
			FloorColor = new Color(0.12f, 0.14f, 0.18f),
			WallColor = new Color(0.2f, 0.24f, 0.3f),
			SkyColor = new Color(0.04f, 0.06f, 0.1f),
			AmbientColor = new Color(0.35f, 0.45f, 0.6f),
			AmbientEnergy = 0.55f,
			SunColor = new Color(0.55f, 0.75f, 1f),
			SunEnergy = 0.85f,
			SunRotationDegrees = new Vector3(-40f, 160f, 0f)
		},

		// ===== Escort haul-line — half-length drift (~400 units N/S, ~80 wide) =====
		// Surface pad (south) → ore vein (north). Cover flanks a wide central haul lane
		// so the rig always has a clear ~20-unit path down the middle.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM DRIFT-HAUL",
			Size = ArenaSize.Medium,
			MapVersion = 2.4f,
			CustomHalfExtentX = 40f,
			CustomHalfExtentZ = 200f,
			PlayerSpawn = new Vector3(0f, 0f, 182f),
			EnemySpawnA = new Vector3(0f, 0f, -176f),
			EnemySpawnB = new Vector3(18f, 0f, -158f),
			Covers =
			[
				// South pad approach
				new(CoverKind.BarrierRow, new Vector3(-20f, 0f, 150f), 8f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(22f, 0f, 138f), -14f),
				new(CoverKind.ShippingContainer, new Vector3(26f, 0f, 118f), 90f, 0.9f),
				new(CoverKind.OilTank, new Vector3(-27f, 0f, 108f), 0f, 0.85f),
				// Upper mid
				new(CoverKind.PipeRack, new Vector3(-24f, 0f, 74f), 0f, 1.0f),
				new(CoverKind.ConcreteBarrier, new Vector3(20f, 0f, 62f), 22f),
				new(CoverKind.SemiTrailer, new Vector3(25f, 0f, 34f), -70f, 0.95f),
				new(CoverKind.ShippingContainer, new Vector3(-22f, 0f, 22f), 15f, 0.9f),
				// Midfield chokes (kept off the center lane)
				new(CoverKind.ContainerStack, new Vector3(-24f, 0f, -6f), 90f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(18f, 0f, -18f), -18f),
				new(CoverKind.BarrierRow, new Vector3(22f, 0f, -46f), 12f, 0.9f),
				// Lower mid
				new(CoverKind.OilTankCluster, new Vector3(-28f, 0f, -70f), 20f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(-16f, 0f, -92f), 8f),
				new(CoverKind.ShippingContainer, new Vector3(20f, 0f, -104f), 0f, 0.9f),
				// Vein basin (north)
				new(CoverKind.IndustrialShed, new Vector3(-26f, 0f, -138f), 90f, 0.8f),
				new(CoverKind.PipeRack, new Vector3(24f, 0f, -132f), 0f, 0.95f),
				new(CoverKind.ConcreteBarrier, new Vector3(-14f, 0f, -160f), -18f),
				new(CoverKind.BarrierRow, new Vector3(14f, 0f, -170f), 0f, 0.85f)
			],
			CratePositions =
			[
				new Vector3(-8f, 0f, 150f),
				new Vector3(10f, 0f, 90f),
				new Vector3(-10f, 0f, 30f),
				new Vector3(8f, 0f, -30f),
				new Vector3(-8f, 0f, -90f),
				new Vector3(10f, 0f, -150f)
			],
			FloorColor = new Color(0.15f, 0.13f, 0.11f),
			WallColor = new Color(0.28f, 0.24f, 0.2f),
			SkyColor = new Color(0.05f, 0.05f, 0.07f),
			AmbientColor = new Color(0.55f, 0.5f, 0.42f),
			AmbientEnergy = 0.5f,
			SunColor = new Color(0.8f, 0.78f, 0.7f),
			SunEnergy = 0.8f,
			SunRotationDegrees = new Vector3(-52f, 150f, 0f)
		}
	];
}
