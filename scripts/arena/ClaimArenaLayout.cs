using Godot;

namespace Mechanize;

/// <summary>
/// Per-claim arena presentation: size class, spawns, cover, crates, atmosphere.
/// One shared arena scene; shell scales and layouts swap at load.
/// Gen 3.x = FPS layered pads (ground / mid deck / jump ledge). Minimum standard size = Medium.
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
		/// <summary>Footprint center. Y is elevation for walkable decks/ramps (usually 0 for ground-rooted pieces).</summary>
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
		// ===== Gen 3.5 / MEDIUM (96×96) — open / Halo-style sightlines =====
		// Mid stays readable; offset overpass = flank route; north ledge = power position.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM 7-ORBITAL",
			Size = ArenaSize.Medium,
			MapVersion = 3.5f,
			PlayerSpawn = new Vector3(38f, 0f, 38f),
			EnemySpawnA = new Vector3(-38f, 0f, -38f),
			EnemySpawnB = new Vector3(38f, 0f, -38f),
			Covers =
			[
				// Mid — light hardpoints only (CS mid / Halo courtyard)
				new(CoverKind.ConcreteBarrier, new Vector3(4f, 0f, -8f), 40f),
				new(CoverKind.ConcreteBarrier, new Vector3(-6f, 0f, 6f), -35f),
				new(CoverKind.ShippingContainer, new Vector3(0f, 0f, 14f), 90f, 0.9f),

				// West flank overpass — underpass + deck cut across (Hawken side path)
				new(CoverKind.CargoOverpass, new Vector3(-22f, 0f, 0f), 90f, 0.95f),
				new(CoverKind.DockRamp, new Vector3(-22f, 0f, -17.7f), -90f, 0.95f),
				new(CoverKind.DockRamp, new Vector3(-22f, 0f, 17.7f), 90f, 0.95f),

				// High — north rim ledge (boost / short jump)
				new(CoverKind.DockLedge, new Vector3(0f, 0f, 36f), 0f, 1.1f),
				new(CoverKind.ShippingContainer, new Vector3(8f, 0f, 30f), 0f, 0.9f),

				// SE approach (player)
				new(CoverKind.BarrierRow, new Vector3(28f, 0f, 24f), -20f),
				new(CoverKind.ShippingContainer, new Vector3(34f, 0f, 16f), 10f),
				new(CoverKind.ConcreteBarrier, new Vector3(22f, 0f, 32f), 15f),
				// NE
				new(CoverKind.ConcreteBarrier, new Vector3(30f, 0f, -22f), -15f),
				new(CoverKind.IndustrialShed, new Vector3(36f, 0f, -32f), 20f, 0.85f),
				new(CoverKind.BarrierRow, new Vector3(18f, 0f, -34f), 5f),
				// NW (enemy A)
				new(CoverKind.SemiTrailer, new Vector3(-32f, 0f, -28f), -40f, 0.9f),
				new(CoverKind.BarrierRow, new Vector3(-26f, 0f, -18f), 15f),
				new(CoverKind.ContainerStack, new Vector3(-36f, 0f, -12f), 90f, 0.9f),
				// SW / west
				new(CoverKind.OilTank, new Vector3(-36f, 0f, 8f), 0f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(-22f, 0f, 30f), 25f),
				new(CoverKind.ShippingContainer, new Vector3(-34f, 0f, 32f), 90f, 0.95f),
				new(CoverKind.OilTankCluster, new Vector3(-38f, 0f, 22f), 12f, 0.8f),
				// South pocket
				new(CoverKind.BarrierRow, new Vector3(8f, 0f, -36f), 5f),
				new(CoverKind.OilTank, new Vector3(-12f, 0f, -38f), 10f, 0.8f),
				new(CoverKind.PipeRack, new Vector3(36f, 0f, 8f), 90f, 0.9f)
			],
			CratePositions =
			[
				new Vector3(30f, 0f, 20f),
				new Vector3(-28f, 0f, -24f),
				new Vector3(32f, 0f, -28f),
				new Vector3(-30f, 0f, 28f),
				new Vector3(0f, 0f, -32f),
				new Vector3(-22f, 3.5f, 0f),
				new Vector3(2f, 2.5f, 36f)
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

		// ===== Gen 3.5 / MEDIUM — Grid Ash (CS branch / tunnel junction) =====
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM GRID-ASH",
			Size = ArenaSize.Medium,
			MapVersion = 3.5f,
			PlayerSpawn = new Vector3(36f, 0f, 38f),
			EnemySpawnA = new Vector3(-36f, 0f, -38f),
			EnemySpawnB = new Vector3(34f, 0f, -36f),
			Covers =
			[
				// N–S spine tunnels + mid junction
				new(CoverKind.ServiceTunnel, new Vector3(0f, 0f, -28f), 90f, 1.05f),
				new(CoverKind.ServiceTunnel, new Vector3(0f, 0f, 0f), 90f, 1.0f),
				new(CoverKind.ServiceTunnel, new Vector3(0f, 0f, 28f), 90f, 1.05f),
				new(CoverKind.ServiceTunnel, new Vector3(30f, 0f, 0f), 0f, 1.05f),
				new(CoverKind.ServiceTunnel, new Vector3(-30f, 0f, 0f), 0f, 1.05f),

				// Mid deck over junction
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, 0f), 0f, 1.15f),
				new(CoverKind.DockRamp, new Vector3(-21.4f, 0f, 0f), 0f, 1.15f),
				new(CoverKind.DockRamp, new Vector3(21.4f, 0f, 0f), 180f, 1.15f),
				new(CoverKind.DockRamp, new Vector3(40f, 0f, 0f), 180f),
				new(CoverKind.DockRamp, new Vector3(-40f, 0f, 0f), 0f),
				new(CoverKind.DockLedge, new Vector3(30f, 0f, 18f), 90f),

				// High — SW relay ledge
				new(CoverKind.DockLedge, new Vector3(-28f, 0f, -32f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(-28f, 0f, -26f), 90f),

				// Soft cover along approaches
				new(CoverKind.ContainerStack, new Vector3(-12f, 0f, -20f), 0f, 0.9f),
				new(CoverKind.ContainerStack, new Vector3(12f, 0f, 20f), 0f, 0.9f),
				new(CoverKind.BarrierRow, new Vector3(24f, 0f, 32f), 0f),
				new(CoverKind.BarrierRow, new Vector3(-24f, 0f, -32f), 0f),
				new(CoverKind.ConcreteBarrier, new Vector3(18f, 0f, -28f), 15f),
				new(CoverKind.ConcreteBarrier, new Vector3(-18f, 0f, 28f), -20f),
				new(CoverKind.ShippingContainer, new Vector3(36f, 0f, -22f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(-36f, 0f, 22f), 180f),

				// Rim hardpoints
				new(CoverKind.OilTankCluster, new Vector3(-38f, 0f, 32f), 20f, 0.85f),
				new(CoverKind.Warehouse, new Vector3(38f, 0f, -30f), 90f, 0.75f),
				new(CoverKind.PipeRack, new Vector3(16f, 0f, 38f), 0f, 1.0f),
				new(CoverKind.IndustrialShed, new Vector3(-36f, 0f, -36f), 0f, 0.9f),
				new(CoverKind.SemiTrailer, new Vector3(34f, 0f, 34f), -90f, 0.85f),
				new(CoverKind.OilTank, new Vector3(38f, 0f, 12f), -10f, 0.85f)
			],
			CratePositions =
			[
				new Vector3(0f, 0f, -28f),
				new Vector3(30f, 0f, 0f),
				new Vector3(-30f, 0f, 0f),
				new Vector3(28f, 0f, 30f),
				new Vector3(-32f, 0f, -30f),
				new Vector3(0f, 3.5f, 0f),
				new Vector3(-26f, 2.5f, -32f)
			],
			FloorColor = new Color(0.18f, 0.2f, 0.22f),
			WallColor = new Color(0.32f, 0.36f, 0.4f),
			SkyColor = new Color(0.1f, 0.12f, 0.14f),
			AmbientColor = new Color(0.55f, 0.62f, 0.68f),
			AmbientEnergy = 0.58f,
			SunColor = new Color(0.85f, 0.9f, 0.95f),
			SunEnergy = 0.95f,
			SunRotationDegrees = new Vector3(-40f, 50f, 0f)
		},

		// ===== Gen 3.5 / MEDIUM — Black Wharf (dock shelf + spine) =====
		// Skyline is exterior backdrop now — no in-pad skyscrapers.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM BLACK-WHARF",
			Size = ArenaSize.Medium,
			MapVersion = 3.5f,
			PlayerSpawn = new Vector3(34f, 0f, 40f),
			EnemySpawnA = new Vector3(-34f, 0f, -40f),
			EnemySpawnB = new Vector3(36f, 0f, -36f),
			Covers =
			[
				// Mid cut — overpass + ramps
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, 0f), 0f, 1.25f),
				new(CoverKind.DockRamp, new Vector3(-23.3f, 0f, 0f), 0f, 1.25f),
				new(CoverKind.DockRamp, new Vector3(23.3f, 0f, 0f), 180f, 1.25f),

				// High — north rim dock ledge
				new(CoverKind.DockLedge, new Vector3(0f, 0f, 34f), 0f, 1.15f),
				new(CoverKind.ShippingContainer, new Vector3(0f, 0f, 28f), 90f),
				// South low ledge for mirrored fight
				new(CoverKind.DockLedge, new Vector3(12f, 0f, -34f), 180f, 0.95f),

				// Soft cover on approach lanes
				new(CoverKind.BarrierRow, new Vector3(-18f, 0f, -28f), 0f),
				new(CoverKind.BarrierRow, new Vector3(18f, 0f, 28f), 0f),
				new(CoverKind.ConcreteBarrier, new Vector3(22f, 0f, -34f), -15f),
				new(CoverKind.ConcreteBarrier, new Vector3(-22f, 0f, 32f), 20f),
				new(CoverKind.BarrierRow, new Vector3(8f, 0f, -38f), 8f),
				new(CoverKind.ConcreteBarrier, new Vector3(-6f, 0f, 38f), -8f),

				// Corner / edge freight
				new(CoverKind.ContainerStack, new Vector3(-36f, 0f, -14f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(36f, 0f, 14f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(32f, 0f, -30f), 90f),
				new(CoverKind.SemiTrailer, new Vector3(-20f, 0f, -36f), 35f, 0.9f),
				new(CoverKind.ShippingContainer, new Vector3(-34f, 0f, 28f), 90f),
				new(CoverKind.ContainerStack, new Vector3(34f, 0f, -12f), 0f, 0.95f),

				new(CoverKind.OilTankCluster, new Vector3(-38f, 0f, 20f), 15f, 0.85f),
				new(CoverKind.OilTank, new Vector3(38f, 0f, -22f), -20f, 0.9f),
				new(CoverKind.Warehouse, new Vector3(-34f, 0f, -34f), 25f, 0.7f),
				new(CoverKind.PipeRack, new Vector3(34f, 0f, 32f), -15f, 0.95f),
				new(CoverKind.ServiceTunnel, new Vector3(0f, 0f, -18f), 0f, 0.9f)
			],
			CratePositions =
			[
				new Vector3(-20f, 0f, -30f),
				new Vector3(24f, 0f, 30f),
				new Vector3(-36f, 0f, 8f),
				new Vector3(36f, 0f, -8f),
				new Vector3(2f, 2.9f, 34f),
				new Vector3(-32f, 0f, -32f),
				new Vector3(0f, 3.5f, 0f)
			],
			FloorColor = new Color(0.12f, 0.14f, 0.18f),
			WallColor = new Color(0.2f, 0.26f, 0.34f),
			SkyColor = new Color(0.04f, 0.06f, 0.1f),
			AmbientColor = new Color(0.4f, 0.54f, 0.7f),
			AmbientEnergy = 0.55f,
			SunColor = new Color(0.55f, 0.7f, 0.9f),
			SunEnergy = 0.8f,
			SunRotationDegrees = new Vector3(-30f, -80f, 0f)
		},

		// ===== Gen 3.5 / MEDIUM — Slag Foundry (Hawken industrial cross) =====
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM SLAG-FOUNDRY",
			Size = ArenaSize.Medium,
			MapVersion = 3.5f,
			PlayerSpawn = new Vector3(38f, 0f, 38f),
			EnemySpawnA = new Vector3(-38f, 0f, -38f),
			EnemySpawnB = new Vector3(36f, 0f, -34f),
			Covers =
			[
				// Furnace spine — N/S pipe corridor (ground lanes)
				new(CoverKind.PipeRack, new Vector3(0f, 0f, -22f), 90f, 1.1f),
				new(CoverKind.PipeRack, new Vector3(0f, 0f, 22f), 90f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(-14f, 0f, 0f), 90f, 1.05f),
				new(CoverKind.BarrierRow, new Vector3(14f, 0f, 0f), 90f, 1.05f),
				new(CoverKind.ConcreteBarrier, new Vector3(-8f, 0f, 12f), 15f),
				new(CoverKind.ConcreteBarrier, new Vector3(10f, 0f, -12f), -20f),

				// Mid — E–W pour bridge over the cross (underpass through spine)
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, 0f), 0f, 1.1f),
				new(CoverKind.DockRamp, new Vector3(-20.5f, 0f, 0f), 0f, 1.1f),
				new(CoverKind.DockRamp, new Vector3(20.5f, 0f, 0f), 180f, 1.1f),

				// Flank service run under south arm
				new(CoverKind.ServiceTunnel, new Vector3(0f, 0f, -34f), 0f, 1.0f),
				// High — east furnace catwalk ledge (jump / boost)
				new(CoverKind.DockLedge, new Vector3(32f, 0f, -8f), 90f, 1.05f),
				new(CoverKind.ShippingContainer, new Vector3(32f, 0f, 0f), 0f, 0.9f),

				// Pour sheds
				new(CoverKind.Warehouse, new Vector3(-32f, 0f, 10f), 0f, 0.8f),
				new(CoverKind.IndustrialShed, new Vector3(28f, 0f, -18f), 90f, 1.0f),
				new(CoverKind.IndustrialShed, new Vector3(-26f, 0f, -28f), -15f, 0.95f),

				// Fuel SW + freight NE
				new(CoverKind.OilTankCluster, new Vector3(-34f, 0f, 30f), 25f, 0.95f),
				new(CoverKind.OilTank, new Vector3(34f, 0f, 26f), -10f, 1.0f),
				new(CoverKind.ContainerStack, new Vector3(24f, 0f, 34f), 0f, 1.0f),
				new(CoverKind.ShippingContainer, new Vector3(14f, 0f, 30f), 90f),
				new(CoverKind.SemiTrailer, new Vector3(-20f, 0f, -36f), 40f, 1.0f),
				new(CoverKind.SemiTrailer, new Vector3(34f, 0f, -32f), -70f, 0.95f),
				new(CoverKind.ShippingContainer, new Vector3(-8f, 0f, -30f), 0f),
				new(CoverKind.ContainerStack, new Vector3(8f, 0f, 18f), -90f, 0.9f),
				new(CoverKind.PipeRack, new Vector3(-36f, 0f, -8f), 0f, 0.95f)
			],
			CratePositions =
			[
				new Vector3(20f, 0f, -6f),
				new Vector3(-22f, 0f, 4f),
				new Vector3(6f, 0f, 32f),
				new Vector3(-10f, 0f, -22f),
				new Vector3(28f, 0f, 8f),
				new Vector3(-32f, 0f, -8f),
				new Vector3(0f, 3.5f, 0f),
				new Vector3(32f, 2.5f, -8f)
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

		// ===== Gen 3.5 / LARGE (112×112) — Spire-Null Plaza =====
		// CS plaza / Halo courtyard: open mid, layered flanks, skyline via exterior backdrop.
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM SPIRE-NULL",
			Size = ArenaSize.Large,
			MapVersion = 3.5f,
			PlayerSpawn = new Vector3(46f, 0f, 44f),
			EnemySpawnA = new Vector3(-46f, 0f, -44f),
			EnemySpawnB = new Vector3(44f, 0f, -42f),
			Covers =
			[
				// Plaza hardpoints (readable mid)
				new(CoverKind.BarrierRow, new Vector3(0f, 0f, -16f), 0f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(0f, 0f, 16f), 0f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(-16f, 0f, 0f), 90f, 1.1f),
				new(CoverKind.BarrierRow, new Vector3(16f, 0f, 0f), 90f, 1.1f),
				new(CoverKind.ConcreteBarrier, new Vector3(-8f, 0f, -8f), 45f),
				new(CoverKind.ConcreteBarrier, new Vector3(8f, 0f, 8f), 45f),
				new(CoverKind.ContainerStack, new Vector3(0f, 0f, 0f), 0f, 1.0f),

				// Mid deck — E–W cut across plaza (underpass through monument)
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, 0f), 0f, 1.2f),
				new(CoverKind.DockRamp, new Vector3(-22.3f, 0f, 0f), 0f, 1.2f),
				new(CoverKind.DockRamp, new Vector3(22.3f, 0f, 0f), 180f, 1.2f),

				// High — north plaza ledge + south mirror
				new(CoverKind.DockLedge, new Vector3(0f, 0f, 28f), 0f, 1.2f),
				new(CoverKind.DockLedge, new Vector3(-20f, 0f, -30f), 180f, 1.0f),

				// Diagonal freight pockets + flank tunnel
				new(CoverKind.Warehouse, new Vector3(-34f, 0f, 26f), 20f, 0.85f),
				new(CoverKind.Warehouse, new Vector3(36f, 0f, -28f), -25f, 0.8f),
				new(CoverKind.IndustrialShed, new Vector3(28f, 0f, 34f), 90f, 1.0f),
				new(CoverKind.ServiceTunnel, new Vector3(-28f, 0f, -20f), 45f, 1.0f),
				new(CoverKind.OilTankCluster, new Vector3(-38f, 0f, -30f), 15f, 1.0f),
				new(CoverKind.OilTank, new Vector3(42f, 0f, 24f), -30f, 1.05f),
				new(CoverKind.PipeRack, new Vector3(-24f, 0f, 42f), 0f, 1.1f),
				new(CoverKind.SemiTrailer, new Vector3(22f, 0f, -42f), 55f, 1.0f),
				new(CoverKind.SemiTrailer, new Vector3(-42f, 0f, 14f), -80f, 0.95f),
				new(CoverKind.ShippingContainer, new Vector3(26f, 0f, 10f), 0f),
				new(CoverKind.ShippingContainer, new Vector3(-28f, 0f, -10f), 90f),
				new(CoverKind.ContainerStack, new Vector3(10f, 0f, -34f), -15f),
				new(CoverKind.ContainerStack, new Vector3(-12f, 0f, 36f), 20f),
				new(CoverKind.BarrierRow, new Vector3(40f, 0f, -8f), 90f),
				new(CoverKind.BarrierRow, new Vector3(-40f, 0f, 8f), 90f)
			],
			CratePositions =
			[
				new Vector3(16f, 0f, -10f),
				new Vector3(-18f, 0f, 12f),
				new Vector3(34f, 0f, 16f),
				new Vector3(-32f, 0f, -16f),
				new Vector3(4f, 0f, 40f),
				new Vector3(-6f, 0f, -38f),
				new Vector3(0f, 3.5f, 0f),
				new Vector3(0f, 2.5f, 28f)
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

		// ===== Sabotage corridor — long approach (mission-exclusive) =====
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM ECHELON-RUN",
			Size = ArenaSize.Medium,
			MapVersion = 2.5f,
			CustomHalfExtentX = 40f,
			CustomHalfExtentZ = 400f,
			PlayerSpawn = new Vector3(0f, 0f, 385f),
			EnemySpawnA = new Vector3(0f, 0f, -385f),
			EnemySpawnB = new Vector3(16f, 0f, -370f),
			Covers =
			[
				new(CoverKind.BarrierRow, new Vector3(-14f, 0f, 340f), 8f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(10f, 0f, 325f), -12f),
				new(CoverKind.ShippingContainer, new Vector3(18f, 0f, 310f), 90f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(8f, 0f, 280f), -25f),
				new(CoverKind.OilTank, new Vector3(-22f, 0f, 255f), 0f, 0.8f),
				new(CoverKind.BarrierRow, new Vector3(12f, 0f, 220f), -5f, 0.9f),
				// Layered choke — mid-south overpass
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, 180f), 0f, 0.85f),
				new(CoverKind.DockRamp, new Vector3(-15.8f, 0f, 180f), 0f, 0.85f),
				new(CoverKind.DockRamp, new Vector3(15.8f, 0f, 180f), 180f, 0.85f),
				new(CoverKind.PipeRack, new Vector3(-20f, 0f, 150f), 90f, 0.95f),
				new(CoverKind.ConcreteBarrier, new Vector3(4f, 0f, 168f), 18f),
				new(CoverKind.ShippingContainer, new Vector3(-10f, 0f, 130f), 15f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(16f, 0f, 100f), 40f),
				new(CoverKind.BarrierRow, new Vector3(-8f, 0f, 70f), 0f, 0.85f),
				new(CoverKind.SemiTrailer, new Vector3(20f, 0f, 40f), -70f, 0.9f),
				new(CoverKind.ContainerStack, new Vector3(-18f, 0f, 10f), 90f, 0.8f),
				new(CoverKind.ConcreteBarrier, new Vector3(6f, 0f, -10f), -15f),
				new(CoverKind.DockLedge, new Vector3(-22f, 0f, -40f), 90f, 0.9f),
				new(CoverKind.BarrierRow, new Vector3(14f, 0f, -45f), 12f, 0.9f),
				new(CoverKind.OilTank, new Vector3(-24f, 0f, -80f), 20f, 0.75f),
				new(CoverKind.ConcreteBarrier, new Vector3(-6f, 0f, -98f), 8f),
				new(CoverKind.ShippingContainer, new Vector3(10f, 0f, -115f), 0f, 0.9f),
				// Mid-north layered choke
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, -160f), 0f, 0.85f),
				new(CoverKind.DockRamp, new Vector3(-15.8f, 0f, -160f), 0f, 0.85f),
				new(CoverKind.DockRamp, new Vector3(15.8f, 0f, -160f), 180f, 0.85f),
				new(CoverKind.PipeRack, new Vector3(22f, 0f, -190f), 0f, 0.9f),
				new(CoverKind.BarrierRow, new Vector3(-16f, 0f, -210f), -8f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(4f, 0f, -240f), 30f),
				new(CoverKind.IndustrialShed, new Vector3(-22f, 0f, -255f), 90f, 0.75f),
				new(CoverKind.ShippingContainer, new Vector3(18f, 0f, -290f), -90f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(-12f, 0f, -305f), -18f),
				new(CoverKind.BarrierRow, new Vector3(-10f, 0f, -320f), 5f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(12f, 0f, -345f), -20f),
				new(CoverKind.OilTank, new Vector3(-20f, 0f, -360f), 0f, 0.7f),
				new(CoverKind.BarrierRow, new Vector3(8f, 0f, -375f), 0f, 0.8f)
			],
			CratePositions =
			[
				new Vector3(6f, 0f, 300f),
				new Vector3(-8f, 0f, 200f),
				new Vector3(0f, 3.5f, 180f),
				new Vector3(10f, 0f, 100f),
				new Vector3(-4f, 0f, 0f),
				new Vector3(8f, 0f, -100f),
				new Vector3(0f, 3.5f, -160f),
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

		// ===== Escort haul-line (mission-exclusive) =====
		new ClaimArenaLayout
		{
			ClaimCode = "VC-CLAIM DRIFT-HAUL",
			Size = ArenaSize.Medium,
			MapVersion = 2.5f,
			CustomHalfExtentX = 40f,
			CustomHalfExtentZ = 200f,
			PlayerSpawn = new Vector3(0f, 0f, 182f),
			EnemySpawnA = new Vector3(0f, 0f, -176f),
			EnemySpawnB = new Vector3(18f, 0f, -158f),
			Covers =
			[
				new(CoverKind.BarrierRow, new Vector3(-20f, 0f, 150f), 8f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(22f, 0f, 138f), -14f),
				new(CoverKind.ShippingContainer, new Vector3(26f, 0f, 118f), 90f, 0.9f),
				new(CoverKind.OilTank, new Vector3(-27f, 0f, 108f), 0f, 0.85f),
				new(CoverKind.PipeRack, new Vector3(-24f, 0f, 74f), 0f, 1.0f),
				new(CoverKind.ConcreteBarrier, new Vector3(20f, 0f, 62f), 22f),
				// Mid haul overpass — clear center lane under
				new(CoverKind.CargoOverpass, new Vector3(0f, 0f, 20f), 0f, 0.9f),
				new(CoverKind.DockRamp, new Vector3(-16.7f, 0f, 20f), 0f, 0.9f),
				new(CoverKind.DockRamp, new Vector3(16.7f, 0f, 20f), 180f, 0.9f),
				new(CoverKind.SemiTrailer, new Vector3(25f, 0f, 40f), -70f, 0.95f),
				new(CoverKind.ShippingContainer, new Vector3(-22f, 0f, 4f), 15f, 0.9f),
				new(CoverKind.ContainerStack, new Vector3(-24f, 0f, -20f), 90f, 0.85f),
				new(CoverKind.ConcreteBarrier, new Vector3(18f, 0f, -28f), -18f),
				new(CoverKind.DockLedge, new Vector3(24f, 0f, -50f), -90f, 0.95f),
				new(CoverKind.BarrierRow, new Vector3(22f, 0f, -60f), 12f, 0.9f),
				new(CoverKind.OilTankCluster, new Vector3(-28f, 0f, -80f), 20f, 0.9f),
				new(CoverKind.ConcreteBarrier, new Vector3(-16f, 0f, -100f), 8f),
				new(CoverKind.ShippingContainer, new Vector3(20f, 0f, -112f), 0f, 0.9f),
				new(CoverKind.IndustrialShed, new Vector3(-26f, 0f, -138f), 90f, 0.8f),
				new(CoverKind.PipeRack, new Vector3(24f, 0f, -132f), 0f, 0.95f),
				new(CoverKind.ConcreteBarrier, new Vector3(-14f, 0f, -160f), -18f),
				new(CoverKind.BarrierRow, new Vector3(14f, 0f, -170f), 0f, 0.85f)
			],
			CratePositions =
			[
				new Vector3(-8f, 0f, 150f),
				new Vector3(10f, 0f, 90f),
				new Vector3(0f, 3.5f, 20f),
				new Vector3(-10f, 0f, 10f),
				new Vector3(8f, 0f, -40f),
				new Vector3(24f, 2.5f, -50f),
				new Vector3(-8f, 0f, -100f),
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
