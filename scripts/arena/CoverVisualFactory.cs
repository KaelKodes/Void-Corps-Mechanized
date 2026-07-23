using Godot;

namespace Mechanize;

/// <summary>
/// Procedural cover props — shipping containers, barriers, tanks, vehicles, buildings.
/// Default collision is a single AABB so AI / projectiles keep working the same way.
/// Walkable decks use a thin floor volume plus optional extra pillar colliders.
/// </summary>
public static class CoverVisualFactory
{
	public readonly struct CollisionVolume
	{
		public CollisionVolume(Vector3 size, Vector3 center, Vector3? rotationDegrees = null)
		{
			Size = size;
			Center = center;
			RotationDegrees = rotationDegrees ?? Vector3.Zero;
		}

		public Vector3 Size { get; }
		public Vector3 Center { get; }
		/// <summary>Local Euler degrees for the collider (e.g. ramp slope).</summary>
		public Vector3 RotationDegrees { get; }
	}

	public readonly struct BuiltCover
	{
		public BuiltCover(
			Node3D visual,
			Vector3 collisionSize,
			float health,
			bool destructible,
			Color shatterColor,
			Vector3? collisionCenter = null,
			CollisionVolume[]? extraCollisions = null,
			Vector3? collisionRotationDegrees = null)
		{
			Visual = visual;
			CollisionSize = collisionSize;
			CollisionCenter = collisionCenter ?? new Vector3(0f, collisionSize.Y * 0.5f, 0f);
			CollisionRotationDegrees = collisionRotationDegrees ?? Vector3.Zero;
			Health = health;
			Destructible = destructible;
			ShatterColor = shatterColor;
			ExtraCollisions = extraCollisions ?? [];
		}

		public Node3D Visual { get; }
		public Vector3 CollisionSize { get; }
		/// <summary>Local center of the primary blocking / floor volume.</summary>
		public Vector3 CollisionCenter { get; }
		/// <summary>Local Euler degrees for the primary collider (ramps).</summary>
		public Vector3 CollisionRotationDegrees { get; }
		/// <summary>Additional colliders (pillars, etc.) parented to the same body.</summary>
		public CollisionVolume[] ExtraCollisions { get; }
		public float Health { get; }
		public bool Destructible { get; }
		public Color ShatterColor { get; }
	}

	/// <summary>Deck top height for <see cref="CoverKind.CargoOverpass"/> (walkable surface).</summary>
	public const float CargoOverpassDeckTop = 3.5f;
	/// <summary>Clearance under overpass deck underside.</summary>
	public const float CargoOverpassClearance = 3.2f;
	/// <summary>Walkable top for <see cref="CoverKind.DockLedge"/>.</summary>
	public const float DockLedgeTop = 2.5f;
	/// <summary>Interior clearance height for <see cref="CoverKind.ServiceTunnel"/>.</summary>
	public const float ServiceTunnelClearance = 3.2f;

	public static BuiltCover Build(CoverKind kind, Color ambience, float scale = 1f)
	{
		scale = Mathf.Clamp(scale, 0.65f, 1.6f);
		return kind switch
		{
			CoverKind.ShippingContainer => BuildShippingContainer(ambience, scale, stacked: false),
			CoverKind.ContainerStack => BuildShippingContainer(ambience, scale, stacked: true),
			CoverKind.ConcreteBarrier => BuildConcreteBarrier(ambience, scale, row: false),
			CoverKind.BarrierRow => BuildConcreteBarrier(ambience, scale, row: true),
			CoverKind.OilTank => BuildOilTank(ambience, scale, cluster: false),
			CoverKind.OilTankCluster => BuildOilTank(ambience, scale, cluster: true),
			CoverKind.SemiTrailer => BuildSemi(ambience, scale),
			CoverKind.Warehouse => BuildWarehouse(ambience, scale),
			CoverKind.IndustrialShed => BuildIndustrialShed(ambience, scale),
			CoverKind.Skyscraper => BuildSkyscraper(ambience, scale),
			CoverKind.PipeRack => BuildPipeRack(ambience, scale),
			CoverKind.CargoOverpass => BuildCargoOverpass(ambience, scale),
			CoverKind.DockRamp => BuildDockRamp(ambience, scale),
			CoverKind.DockLedge => BuildDockLedge(ambience, scale),
			CoverKind.ServiceTunnel => BuildServiceTunnel(ambience, scale),
			_ => BuildShippingContainer(ambience, scale, stacked: false)
		};
	}

	private static BuiltCover BuildShippingContainer(Color ambience, float scale, bool stacked)
	{
		var root = new Node3D { Name = "ShippingContainer" };
		var paint = PickContainerPaint(ambience);
		var body = MakeMat(paint, 0.45f, 0.5f, surface: SurfaceLibrary.Kind.PaintedMetal);
		var rib = MakeMat(paint.Darkened(0.22f), 0.5f, 0.45f, surface: SurfaceLibrary.Kind.Steel);
		var door = MakeMat(paint.Darkened(0.35f), 0.55f, 0.4f, surface: SurfaceLibrary.Kind.PaintedMetal);
		var corner = MakeMat(new Color(0.35f, 0.35f, 0.32f), 0.7f, 0.35f, surface: SurfaceLibrary.Kind.Steel);
		var accent = MakeMat(paint.Lightened(0.2f), 0.3f, 0.45f, surface: SurfaceLibrary.Kind.PaintedMetal);

		var tiers = stacked ? 2 : 1;
		var unit = new Vector3(6.1f, 2.6f, 2.45f) * scale;
		for (var t = 0; t < tiers; t++)
		{
			var y = (t + 0.5f) * unit.Y;
			AddBox(root, body, unit, new Vector3(0f, y, 0f));
			// Corrugation ribs
			for (var i = -2; i <= 2; i++)
			{
				AddBox(root, rib,
					new Vector3(0.12f, unit.Y * 0.92f, unit.Z * 1.02f),
					new Vector3(i * unit.X * 0.16f, y, 0f));
			}
			// End doors
			AddBox(root, door, new Vector3(0.08f, unit.Y * 0.88f, unit.Z * 0.92f),
				new Vector3(unit.X * 0.5f, y, 0f));
			AddBox(root, door, new Vector3(0.08f, unit.Y * 0.88f, unit.Z * 0.92f),
				new Vector3(-unit.X * 0.5f, y, 0f));
			AddBox(root, accent, new Vector3(0.06f, unit.Y * 0.35f, 0.08f),
				new Vector3(unit.X * 0.52f, y, unit.Z * 0.15f));
			// Corner castings
			foreach (var sx in new[] { -1f, 1f })
			foreach (var sz in new[] { -1f, 1f })
			{
				AddBox(root, corner, new Vector3(0.22f, 0.22f, 0.22f) * scale,
					new Vector3(sx * unit.X * 0.48f, y - unit.Y * 0.42f, sz * unit.Z * 0.48f));
				AddBox(root, corner, new Vector3(0.22f, 0.22f, 0.22f) * scale,
					new Vector3(sx * unit.X * 0.48f, y + unit.Y * 0.42f, sz * unit.Z * 0.48f));
			}
		}

		var size = new Vector3(unit.X, unit.Y * tiers, unit.Z);
		return Finish(root, size, stacked ? 220f : 150f, true, paint);
	}

	private static BuiltCover BuildConcreteBarrier(Color ambience, float scale, bool row)
	{
		var root = new Node3D { Name = row ? "BarrierRow" : "ConcreteBarrier" };
		var concrete = MakeMat(new Color(0.55f, 0.54f, 0.5f).Lerp(ambience, 0.15f), 0.05f, 0.92f,
			surface: SurfaceLibrary.Kind.Concrete);
		var dark = MakeMat(new Color(0.4f, 0.39f, 0.36f), 0.05f, 0.9f, surface: SurfaceLibrary.Kind.Concrete);
		var stripe = MakeMat(new Color(0.85f, 0.7f, 0.2f), 0.1f, 0.7f);

		var count = row ? 4 : 1;
		var segment = new Vector3(2.4f, 1.15f, 0.7f) * scale;
		var gap = segment.X * 1.05f;
		var totalX = gap * count;
		for (var i = 0; i < count; i++)
		{
			var x = -totalX * 0.5f + gap * 0.5f + i * gap;
			// Jersey profile: wide base, narrow top
			AddBox(root, concrete, new Vector3(segment.X, segment.Y * 0.45f, segment.Z * 1.35f),
				new Vector3(x, segment.Y * 0.22f, 0f));
			AddBox(root, concrete, new Vector3(segment.X * 0.92f, segment.Y * 0.65f, segment.Z),
				new Vector3(x, segment.Y * 0.65f, 0f));
			AddBox(root, dark, new Vector3(segment.X * 0.85f, segment.Y * 0.12f, segment.Z * 0.7f),
				new Vector3(x, segment.Y * 1.05f, 0f));
			AddBox(root, stripe, new Vector3(segment.X * 0.7f, 0.08f, 0.06f),
				new Vector3(x, segment.Y * 0.55f, segment.Z * 0.52f));
		}

		// Slightly thicker than the mesh so CharacterBody can't tunnel at sprint speed.
		var size = new Vector3(totalX, segment.Y * 1.15f, Mathf.Max(1.35f, segment.Z * 1.6f));
		return Finish(root, size, row ? 160f : 110f, true, concrete.AlbedoColor);
	}

	private static BuiltCover BuildOilTank(Color ambience, float scale, bool cluster)
	{
		var root = new Node3D { Name = cluster ? "OilTankCluster" : "OilTank" };
		var steel = MakeMat(new Color(0.42f, 0.38f, 0.32f).Lerp(ambience, 0.2f), 0.65f, 0.4f,
			surface: SurfaceLibrary.Kind.Steel);
		var rust = MakeMat(new Color(0.55f, 0.28f, 0.14f), 0.4f, 0.55f, surface: SurfaceLibrary.Kind.Rust);
		var dark = MakeMat(new Color(0.2f, 0.2f, 0.22f), 0.7f, 0.35f, surface: SurfaceLibrary.Kind.Steel);
		var warn = MakeMat(new Color(0.9f, 0.55f, 0.1f), 0.2f, 0.5f, new Color(0.9f, 0.45f, 0.05f), 0.25f);

		void AddTank(Vector3 offset, float radius, float height)
		{
			AddCylinder(root, steel, radius, height, offset + new Vector3(0f, height * 0.5f, 0f));
			AddCylinder(root, rust, radius * 1.02f, height * 0.12f, offset + new Vector3(0f, height * 0.55f, 0f));
			AddCylinder(root, dark, radius * 0.95f, 0.15f, offset + new Vector3(0f, height + 0.05f, 0f));
			AddCylinder(root, warn, radius * 0.2f, 0.45f, offset + new Vector3(0f, height + 0.35f, 0f));
			// Legs / skirt
			for (var i = 0; i < 4; i++)
			{
				var a = i * Mathf.Tau * 0.25f;
				AddBox(root, dark, new Vector3(0.18f, height * 0.2f, 0.18f) * scale,
					offset + new Vector3(Mathf.Cos(a) * radius * 0.75f, height * 0.1f, Mathf.Sin(a) * radius * 0.75f));
			}
		}

		if (cluster)
		{
			var r = 2.1f * scale;
			var h = 5.5f * scale;
			AddTank(new Vector3(-2.6f * scale, 0f, -1.4f * scale), r, h);
			AddTank(new Vector3(2.6f * scale, 0f, -1.4f * scale), r, h);
			AddTank(new Vector3(0f, 0f, 2.2f * scale), r * 0.85f, h * 0.85f);
			// Catwalk
			AddBox(root, dark, new Vector3(7.2f * scale, 0.15f, 1.2f * scale), new Vector3(0f, h * 0.72f, -1.4f * scale));
			var size = new Vector3(8.5f, h + 0.6f, 7.5f) * scale;
			return Finish(root, size, 300f, true, steel.AlbedoColor);
		}

		{
			var r = 2.8f * scale;
			var h = 6.5f * scale;
			AddTank(Vector3.Zero, r, h);
			var size = new Vector3(r * 2.2f, h + 0.5f, r * 2.2f);
			return Finish(root, size, 240f, true, steel.AlbedoColor);
		}
	}

	private static BuiltCover BuildSemi(Color ambience, float scale)
	{
		var root = new Node3D { Name = "SemiTrailer" };
		var cabPaint = MakeMat(new Color(0.7f, 0.18f, 0.14f).Lerp(ambience, 0.15f), 0.35f, 0.45f,
			surface: SurfaceLibrary.Kind.PaintedMetal);
		var trailer = MakeMat(new Color(0.75f, 0.72f, 0.65f), 0.25f, 0.55f,
			surface: SurfaceLibrary.Kind.PaintedMetal);
		var dark = MakeMat(new Color(0.18f, 0.18f, 0.2f), 0.5f, 0.4f, surface: SurfaceLibrary.Kind.Steel);
		var glass = MakeMat(new Color(0.35f, 0.55f, 0.7f), 0.1f, 0.25f, new Color(0.4f, 0.65f, 0.85f), 0.4f);
		var chrome = MakeMat(new Color(0.55f, 0.55f, 0.58f), 0.85f, 0.3f, surface: SurfaceLibrary.Kind.Steel);

		// Cab
		AddBox(root, cabPaint, new Vector3(2.4f, 2.2f, 2.6f) * scale, new Vector3(0f, 1.5f * scale, 5.4f * scale));
		AddBox(root, glass, new Vector3(2.1f, 0.7f, 0.1f) * scale, new Vector3(0f, 2.1f * scale, 6.7f * scale));
		AddBox(root, dark, new Vector3(2.5f, 0.35f, 2.8f) * scale, new Vector3(0f, 0.55f * scale, 5.4f * scale));
		// Trailer box
		AddBox(root, trailer, new Vector3(2.55f, 2.8f, 8.5f) * scale, new Vector3(0f, 2.0f * scale, -0.8f * scale));
		AddBox(root, dark, new Vector3(2.4f, 0.2f, 8.3f) * scale, new Vector3(0f, 0.7f * scale, -0.8f * scale));
		AddBox(root, chrome, new Vector3(2.6f, 0.12f, 0.12f) * scale, new Vector3(0f, 3.3f * scale, 3.4f * scale));
		// Wheels
		AddWheel(root, dark, new Vector3(-1.2f, 0.45f, 4.6f) * scale, scale);
		AddWheel(root, dark, new Vector3(1.2f, 0.45f, 4.6f) * scale, scale);
		AddWheel(root, dark, new Vector3(-1.2f, 0.45f, -3.2f) * scale, scale);
		AddWheel(root, dark, new Vector3(1.2f, 0.45f, -3.2f) * scale, scale);
		AddWheel(root, dark, new Vector3(-1.2f, 0.45f, -4.6f) * scale, scale);
		AddWheel(root, dark, new Vector3(1.2f, 0.45f, -4.6f) * scale, scale);

		var size = new Vector3(2.8f, 3.5f, 12.5f) * scale;
		return Finish(root, size, 200f, true, trailer.AlbedoColor);
	}

	private static BuiltCover BuildWarehouse(Color ambience, float scale)
	{
		var root = new Node3D { Name = "Warehouse" };
		var wall = MakeMat(new Color(0.55f, 0.5f, 0.42f).Lerp(ambience, 0.25f), 0.15f, 0.85f,
			surface: SurfaceLibrary.Kind.Concrete);
		var roof = MakeMat(new Color(0.28f, 0.3f, 0.34f), 0.4f, 0.55f, surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.22f, 0.22f, 0.24f), 0.35f, 0.6f, surface: SurfaceLibrary.Kind.Steel);
		var door = MakeMat(new Color(0.35f, 0.38f, 0.4f), 0.45f, 0.5f, surface: SurfaceLibrary.Kind.PaintedMetal);

		var w = 14f * scale;
		var h = 6.5f * scale;
		var d = 10f * scale;
		AddBox(root, wall, new Vector3(w, h, d), new Vector3(0f, h * 0.5f, 0f));
		AddBox(root, roof, new Vector3(w * 1.08f, 0.35f, d * 1.08f), new Vector3(0f, h + 0.1f, 0f));
		AddBox(root, dark, new Vector3(w * 0.08f, h * 1.05f, d * 1.02f), new Vector3(-w * 0.48f, h * 0.5f, 0f));
		AddBox(root, dark, new Vector3(w * 0.08f, h * 1.05f, d * 1.02f), new Vector3(w * 0.48f, h * 0.5f, 0f));
		// Bay doors
		AddBox(root, door, new Vector3(w * 0.35f, h * 0.7f, 0.12f), new Vector3(-w * 0.2f, h * 0.35f, d * 0.5f));
		AddBox(root, door, new Vector3(w * 0.28f, h * 0.7f, 0.12f), new Vector3(w * 0.25f, h * 0.35f, d * 0.5f));
		AddBox(root, MakeMat(new Color(0.7f, 0.55f, 0.2f), 0.2f, 0.5f, surface: SurfaceLibrary.Kind.PaintedMetal),
			new Vector3(w * 0.5f, 0.2f, 0.08f),
			new Vector3(0f, h * 0.85f, d * 0.52f));

		var size = new Vector3(w * 1.05f, h + 0.5f, d * 1.05f);
		return Finish(root, size, 320f, true, wall.AlbedoColor);
	}

	private static BuiltCover BuildIndustrialShed(Color ambience, float scale)
	{
		var root = new Node3D { Name = "IndustrialShed" };
		var wall = MakeMat(new Color(0.4f, 0.45f, 0.48f).Lerp(ambience, 0.2f), 0.3f, 0.7f,
			surface: SurfaceLibrary.Kind.Concrete);
		var roof = MakeMat(new Color(0.25f, 0.28f, 0.3f), 0.5f, 0.5f, surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.2f, 0.2f, 0.22f), 0.4f, 0.55f, surface: SurfaceLibrary.Kind.Steel);

		var w = 8f * scale;
		var h = 4.2f * scale;
		var d = 6f * scale;
		AddBox(root, wall, new Vector3(w, h, d), new Vector3(0f, h * 0.5f, 0f));
		AddBox(root, roof, new Vector3(w * 1.1f, 0.25f, d * 1.1f), new Vector3(0f, h + 0.05f, 0f));
		AddBox(root, dark, new Vector3(w * 0.4f, h * 0.65f, 0.1f), new Vector3(0f, h * 0.32f, d * 0.5f));
		AddBox(root, dark, new Vector3(0.2f, h * 1.2f, 0.2f), new Vector3(w * 0.4f, h * 0.7f, -d * 0.35f));

		var size = new Vector3(w * 1.1f, h + 0.4f, d * 1.1f);
		return Finish(root, size, 200f, true, wall.AlbedoColor);
	}

	private static BuiltCover BuildSkyscraper(Color ambience, float scale)
	{
		var root = new Node3D { Name = "Skyscraper" };
		var facade = MakeMat(new Color(0.28f, 0.32f, 0.38f).Lerp(ambience, 0.15f), 0.55f, 0.4f,
			surface: SurfaceLibrary.Kind.Concrete);
		var glass = MakeMat(new Color(0.25f, 0.4f, 0.55f), 0.2f, 0.25f, new Color(0.35f, 0.55f, 0.75f), 0.35f);
		var dark = MakeMat(new Color(0.12f, 0.13f, 0.15f), 0.4f, 0.5f, surface: SurfaceLibrary.Kind.Steel);
		var trim = MakeMat(new Color(0.55f, 0.5f, 0.35f).Lerp(ambience, 0.2f), 0.45f, 0.45f,
			surface: SurfaceLibrary.Kind.PaintedMetal);

		var w = 5.5f * scale;
		var d = 5.5f * scale;
		var h = 28f * scale;
		AddBox(root, facade, new Vector3(w, h, d), new Vector3(0f, h * 0.5f, 0f));
		// Window bands
		var floors = 10;
		for (var i = 0; i < floors; i++)
		{
			var y = 2.2f * scale + i * (h - 3f * scale) / floors;
			AddBox(root, glass, new Vector3(w * 0.92f, 0.55f * scale, 0.08f), new Vector3(0f, y, d * 0.5f));
			AddBox(root, glass, new Vector3(0.08f, 0.55f * scale, d * 0.92f), new Vector3(w * 0.5f, y, 0f));
			AddBox(root, glass, new Vector3(0.08f, 0.55f * scale, d * 0.92f), new Vector3(-w * 0.5f, y, 0f));
		}
		AddBox(root, dark, new Vector3(w * 1.05f, 0.4f, d * 1.05f), new Vector3(0f, h + 0.1f, 0f));
		AddBox(root, trim, new Vector3(w * 0.4f, 1.8f * scale, d * 0.4f), new Vector3(0f, h + 1.1f * scale, 0f));
		AddBox(root, dark, new Vector3(w * 1.15f, 0.5f, d * 1.15f), new Vector3(0f, 0.25f, 0f)); // plinth

		var size = new Vector3(w * 1.15f, h + 2.5f * scale, d * 1.15f);
		// Backdrop — tough, not field cover to punch through mid-fight.
		return Finish(root, size, 2000f, false, facade.AlbedoColor);
	}

	private static BuiltCover BuildPipeRack(Color ambience, float scale)
	{
		var root = new Node3D { Name = "PipeRack" };
		var frame = MakeMat(new Color(0.35f, 0.35f, 0.32f).Lerp(ambience, 0.15f), 0.6f, 0.4f,
			surface: SurfaceLibrary.Kind.Steel);
		var pipeA = MakeMat(new Color(0.55f, 0.45f, 0.25f), 0.5f, 0.45f, surface: SurfaceLibrary.Kind.Rust);
		var pipeB = MakeMat(new Color(0.3f, 0.4f, 0.45f), 0.55f, 0.4f, surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.5f, surface: SurfaceLibrary.Kind.Steel);

		var len = 10f * scale;
		var h = 3.8f * scale;
		var depth = 2.4f * scale;
		// Legs — sizes already include scale; do not multiply Vector3 by scale again.
		foreach (var x in new[] { -len * 0.45f, len * 0.45f })
		foreach (var z in new[] { -depth * 0.4f, depth * 0.4f })
			AddBox(root, frame, new Vector3(0.28f * scale, h, 0.28f * scale), new Vector3(x, h * 0.5f, z));
		AddBox(root, frame, new Vector3(len, 0.22f * scale, depth), new Vector3(0f, h, 0f));
		AddBox(root, dark, new Vector3(len, 0.16f * scale, depth * 0.8f), new Vector3(0f, h * 0.55f, 0f));
		AddCylinder(root, pipeA, 0.28f * scale, len * 0.95f, new Vector3(0f, h * 0.75f, -0.4f * scale),
			Vector3.Forward * Mathf.Tau * 0.25f);
		AddCylinder(root, pipeB, 0.22f * scale, len * 0.95f, new Vector3(0f, h * 0.75f, 0.4f * scale),
			Vector3.Forward * Mathf.Tau * 0.25f);
		AddCylinder(root, pipeA, 0.18f * scale, len * 0.95f, new Vector3(0f, h * 0.4f, 0f),
			Vector3.Forward * Mathf.Tau * 0.25f);

		// Solid blocking volume for the whole rack (lanes, not walk-through).
		var size = new Vector3(len + 0.4f * scale, h + 0.35f * scale, depth + 0.35f * scale);
		return Finish(root, size, 180f, true, frame.AlbedoColor);
	}

	/// <summary>
	/// Thin E–W cargo deck. Primary collision is the walkable slab only;
	/// end pillars are extras so midfield stays an underpass.
	/// Deck top ≈ 3.5 m, underside clearance ≈ 3.2 m.
	/// </summary>
	private static BuiltCover BuildCargoOverpass(Color ambience, float scale)
	{
		var root = new Node3D { Name = "CargoOverpass" };
		var steel = MakeMat(new Color(0.32f, 0.36f, 0.42f).Lerp(ambience, 0.25f), 0.55f, 0.45f,
			surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.16f, 0.18f, 0.22f), 0.5f, 0.5f, surface: SurfaceLibrary.Kind.Steel);
		var rail = MakeMat(new Color(0.45f, 0.4f, 0.28f).Lerp(ambience, 0.15f), 0.35f, 0.55f,
			surface: SurfaceLibrary.Kind.PaintedMetal);
		var rust = MakeMat(new Color(0.5f, 0.28f, 0.16f), 0.3f, 0.65f, surface: SurfaceLibrary.Kind.Rust);

		var deckLen = 32f * scale;
		var deckWidth = 8f * scale;
		var deckThick = 0.35f * scale;
		var clearance = CargoOverpassClearance * scale;
		var deckTop = CargoOverpassDeckTop * scale;
		var deckCenterY = clearance + deckThick * 0.5f;

		// Walkable slab
		AddBox(root, steel, new Vector3(deckLen, deckThick, deckWidth), new Vector3(0f, deckCenterY, 0f));
		// Underside ribs
		for (var i = -3; i <= 3; i++)
		{
			AddBox(root, dark,
				new Vector3(0.22f * scale, 0.18f * scale, deckWidth * 0.92f),
				new Vector3(i * deckLen * 0.12f, clearance - 0.05f * scale, 0f));
		}
		// Side rails
		foreach (var sz in new[] { -1f, 1f })
		{
			AddBox(root, rail,
				new Vector3(deckLen * 0.98f, 0.55f * scale, 0.12f * scale),
				new Vector3(0f, deckTop + 0.2f * scale, sz * deckWidth * 0.48f));
			AddBox(root, dark,
				new Vector3(deckLen * 0.98f, 0.08f * scale, 0.18f * scale),
				new Vector3(0f, deckTop + 0.48f * scale, sz * deckWidth * 0.48f));
		}
		// Warning stripes on deck
		AddBox(root, rust,
			new Vector3(deckLen * 0.9f, 0.04f * scale, 0.35f * scale),
			new Vector3(0f, deckTop + 0.02f * scale, 0f));

		// End pillars — leave center lane open under the deck
		var pillarH = clearance;
		var pillarSize = new Vector3(1.1f * scale, pillarH, 1.1f * scale);
		var pillarXs = new[] { -deckLen * 0.42f, deckLen * 0.42f };
		var pillarZs = new[] { -deckWidth * 0.35f, deckWidth * 0.35f };
		var extras = new System.Collections.Generic.List<CollisionVolume>(4);
		foreach (var px in pillarXs)
		foreach (var pz in pillarZs)
		{
			var center = new Vector3(px, pillarH * 0.5f, pz);
			AddBox(root, dark, pillarSize, center);
			AddBox(root, steel,
				new Vector3(pillarSize.X * 1.15f, 0.2f * scale, pillarSize.Z * 1.15f),
				new Vector3(px, 0.1f * scale, pz));
			extras.Add(new CollisionVolume(pillarSize, center));
		}

		var floorSize = new Vector3(deckLen, deckThick, deckWidth);
		var floorCenter = new Vector3(0f, deckCenterY, 0f);
		return FinishWalkable(root, floorSize, floorCenter, steel.AlbedoColor, extras.ToArray());
	}

	/// <summary>
	/// Stepped ramp visual rising along local +X, with a continuous slope collider (~34°).
	/// Low end at local X negative; high end meets <see cref="CargoOverpassDeckTop"/>.
	/// </summary>
	private static BuiltCover BuildDockRamp(Color ambience, float scale)
	{
		var root = new Node3D { Name = "DockRamp" };
		var steel = MakeMat(new Color(0.38f, 0.4f, 0.44f).Lerp(ambience, 0.2f), 0.45f, 0.55f,
			surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.2f, 0.22f, 0.26f), 0.4f, 0.6f, surface: SurfaceLibrary.Kind.Steel);
		var stripe = MakeMat(new Color(0.85f, 0.65f, 0.15f), 0.15f, 0.65f);

		var deckTop = CargoOverpassDeckTop * scale;
		// Horizontal run → atan(3.5/5.2) ≈ 34° (under 45° floor max).
		var run = 5.2f * scale;
		var width = 7.2f * scale;
		var steps = 8;
		var stepRun = run / steps;
		var stepRise = deckTop / steps;

		for (var i = 0; i < steps; i++)
		{
			var topY = (i + 1) * stepRise;
			var x = -run * 0.5f + stepRun * (i + 0.5f);
			var stepThick = Mathf.Max(0.22f * scale, stepRise * 0.85f);
			AddBox(root, i % 2 == 0 ? steel : dark,
				new Vector3(stepRun * 1.02f, stepThick, width),
				new Vector3(x, topY - stepThick * 0.5f, 0f));
			if (i % 2 == 0)
			{
				AddBox(root, stripe,
					new Vector3(stepRun * 0.7f, 0.05f * scale, 0.12f * scale),
					new Vector3(x, topY + 0.02f * scale, width * 0.35f));
				AddBox(root, stripe,
					new Vector3(stepRun * 0.7f, 0.05f * scale, 0.12f * scale),
					new Vector3(x, topY + 0.02f * scale, -width * 0.35f));
			}
		}

		foreach (var sz in new[] { -1f, 1f })
		{
			AddBox(root, dark,
				new Vector3(run, deckTop * 0.35f, 0.18f * scale),
				new Vector3(0f, deckTop * 0.2f, sz * width * 0.52f));
		}

		// Continuous slope collider — CharacterBody walks this, not the decorative steps.
		var angleDeg = Mathf.RadToDeg(Mathf.Atan(deckTop / run));
		var slopeLen = Mathf.Sqrt(run * run + deckTop * deckTop);
		var thick = 0.45f * scale;
		var floorSize = new Vector3(slopeLen, thick, width);
		// Midpoint of the ramp surface, nudged so the top face sits on the grade.
		var floorCenter = new Vector3(0f, deckTop * 0.5f, 0f);
		var floorRot = new Vector3(0f, 0f, angleDeg);
		return FinishWalkable(
			root,
			floorSize,
			floorCenter,
			steel.AlbedoColor,
			extras: null,
			primaryRotationDegrees: floorRot);
	}

	/// <summary>
	/// Short dock ledge — walkable top at ~2.5 m with open legs (not a solid block).
	/// Jumpjack+ can mount from ground; single container (~2.6 m) is a free step-up.
	/// </summary>
	private static BuiltCover BuildDockLedge(Color ambience, float scale)
	{
		var root = new Node3D { Name = "DockLedge" };
		var steel = MakeMat(new Color(0.3f, 0.34f, 0.4f).Lerp(ambience, 0.22f), 0.5f, 0.5f,
			surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.15f, 0.17f, 0.2f), 0.45f, 0.55f, surface: SurfaceLibrary.Kind.Steel);
		var rail = MakeMat(new Color(0.5f, 0.45f, 0.3f), 0.35f, 0.55f, surface: SurfaceLibrary.Kind.PaintedMetal);

		var top = DockLedgeTop * scale;
		var deckThick = 0.3f * scale;
		var deckLen = 12f * scale;
		var deckWidth = 6f * scale;
		var deckCenterY = top - deckThick * 0.5f;
		var legH = top - deckThick;

		AddBox(root, steel, new Vector3(deckLen, deckThick, deckWidth), new Vector3(0f, deckCenterY, 0f));
		// Rail on three sides (open toward south / approach)
		AddBox(root, rail,
			new Vector3(deckLen * 0.98f, 0.45f * scale, 0.1f * scale),
			new Vector3(0f, top + 0.15f * scale, -deckWidth * 0.48f));
		foreach (var sx in new[] { -1f, 1f })
		{
			AddBox(root, rail,
				new Vector3(0.1f * scale, 0.45f * scale, deckWidth * 0.9f),
				new Vector3(sx * deckLen * 0.48f, top + 0.15f * scale, -deckWidth * 0.05f));
		}

		var legSize = new Vector3(0.85f * scale, legH, 0.85f * scale);
		var extras = new System.Collections.Generic.List<CollisionVolume>(4);
		foreach (var sx in new[] { -1f, 1f })
		foreach (var sz in new[] { -1f, 1f })
		{
			var center = new Vector3(sx * deckLen * 0.4f, legH * 0.5f, sz * deckWidth * 0.35f);
			AddBox(root, dark, legSize, center);
			extras.Add(new CollisionVolume(legSize, center));
		}

		var floorSize = new Vector3(deckLen, deckThick, deckWidth);
		var floorCenter = new Vector3(0f, deckCenterY, 0f);
		return FinishWalkable(root, floorSize, floorCenter, steel.AlbedoColor, extras.ToArray());
	}

	/// <summary>
	/// Hollow corridor along local X — open ends, thin side walls + walkable roof.
	/// Interior ~3.2 m high × ~4.5 m wide; length ~16 at scale 1.
	/// </summary>
	private static BuiltCover BuildServiceTunnel(Color ambience, float scale)
	{
		var root = new Node3D { Name = "ServiceTunnel" };
		var steel = MakeMat(new Color(0.34f, 0.38f, 0.42f).Lerp(ambience, 0.2f), 0.5f, 0.5f,
			surface: SurfaceLibrary.Kind.Steel);
		var dark = MakeMat(new Color(0.18f, 0.2f, 0.24f), 0.45f, 0.55f, surface: SurfaceLibrary.Kind.Steel);
		var accent = MakeMat(new Color(0.55f, 0.48f, 0.22f).Lerp(ambience, 0.1f), 0.3f, 0.55f,
			surface: SurfaceLibrary.Kind.PaintedMetal);
		var cable = MakeMat(new Color(0.25f, 0.45f, 0.4f), 0.4f, 0.45f, surface: SurfaceLibrary.Kind.Steel);

		var length = 16f * scale;
		var clearW = 4.5f * scale;
		var clearH = ServiceTunnelClearance * scale;
		var wallT = 0.4f * scale;
		var roofT = 0.3f * scale;
		var wallH = clearH;
		var roofCenterY = clearH + roofT * 0.5f;
		var roofTop = clearH + roofT;
		var wallZ = clearW * 0.5f + wallT * 0.5f;

		// Side walls (open on ±X ends)
		var wallSize = new Vector3(length, wallH, wallT);
		var wallL = new Vector3(0f, wallH * 0.5f, -wallZ);
		var wallR = new Vector3(0f, wallH * 0.5f, wallZ);
		AddBox(root, steel, wallSize, wallL);
		AddBox(root, steel, wallSize, wallR);
		// Inner ribbing
		for (var i = -2; i <= 2; i++)
		{
			var x = i * length * 0.18f;
			AddBox(root, dark,
				new Vector3(0.18f * scale, wallH * 0.9f, wallT * 1.1f),
				new Vector3(x, wallH * 0.5f, -wallZ));
			AddBox(root, dark,
				new Vector3(0.18f * scale, wallH * 0.9f, wallT * 1.1f),
				new Vector3(x, wallH * 0.5f, wallZ));
		}
		// Roof slab (walkable)
		var roofSize = new Vector3(length, roofT, clearW + wallT * 2f);
		var roofCenter = new Vector3(0f, roofCenterY, 0f);
		AddBox(root, dark, roofSize, roofCenter);
		AddBox(root, accent,
			new Vector3(length * 0.92f, 0.06f * scale, 0.25f * scale),
			new Vector3(0f, roofTop + 0.02f * scale, 0f));
		// Cable runs under roof
		AddCylinder(root, cable, 0.12f * scale, length * 0.9f,
			new Vector3(0f, clearH - 0.25f * scale, -clearW * 0.28f),
			Vector3.Forward * Mathf.Tau * 0.25f);
		AddCylinder(root, cable, 0.1f * scale, length * 0.9f,
			new Vector3(0f, clearH - 0.25f * scale, clearW * 0.28f),
			Vector3.Forward * Mathf.Tau * 0.25f);

		var extras = new[]
		{
			new CollisionVolume(wallSize, wallL),
			new CollisionVolume(wallSize, wallR)
		};
		return FinishWalkable(root, roofSize, roofCenter, steel.AlbedoColor, extras);
	}

	/// <summary>Thin walkable floor + optional extras. Does not fill underpass volume.</summary>
	private static BuiltCover FinishWalkable(
		Node3D root,
		Vector3 floorSize,
		Vector3 floorCenter,
		Color tint,
		CollisionVolume[]? extras = null,
		Vector3? primaryRotationDegrees = null)
	{
		return new BuiltCover(
			root,
			floorSize,
			health: 9999f,
			destructible: false,
			tint,
			floorCenter,
			extras,
			primaryRotationDegrees);
	}

	/// <summary>
	/// Prefer measured visual AABB so collision matches what the player sees
	/// (fixes scale drift / overhangs that let mechs ghost through).
	/// </summary>
	private static BuiltCover Finish(
		Node3D root,
		Vector3 fallbackSize,
		float health,
		bool destructible,
		Color shatterColor)
	{
		if (!TryMeasureAabb(root, out var center, out var size))
			return new BuiltCover(root, fallbackSize, health, destructible, shatterColor);

		// Pad slightly so sprinting CharacterBodies don't tunnel thin faces.
		size += new Vector3(0.15f, 0.1f, 0.15f);
		size.X = Mathf.Max(size.X, fallbackSize.X * 0.85f);
		size.Y = Mathf.Max(size.Y, fallbackSize.Y * 0.85f);
		size.Z = Mathf.Max(size.Z, fallbackSize.Z * 0.85f);
		// Keep volume sitting on the ground plane.
		center.Y = size.Y * 0.5f;
		return new BuiltCover(root, size, health, destructible, shatterColor, center);
	}

	private static bool TryMeasureAabb(Node3D root, out Vector3 center, out Vector3 size)
	{
		var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
		var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
		var any = false;
		AccumulateMeshes(root, Transform3D.Identity, ref min, ref max, ref any);
		if (!any)
		{
			center = Vector3.Zero;
			size = Vector3.One;
			return false;
		}

		center = (min + max) * 0.5f;
		size = max - min;
		return size.X > 0.05f && size.Y > 0.05f && size.Z > 0.05f;
	}

	private static void AccumulateMeshes(
		Node node,
		Transform3D parent,
		ref Vector3 min,
		ref Vector3 max,
		ref bool any)
	{
		var local = parent;
		if (node is Node3D n3)
			local = parent * n3.Transform;

		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			var aabb = mi.Mesh.GetAabb();
			var corners = new[]
			{
				aabb.Position,
				aabb.Position + new Vector3(aabb.Size.X, 0f, 0f),
				aabb.Position + new Vector3(0f, aabb.Size.Y, 0f),
				aabb.Position + new Vector3(0f, 0f, aabb.Size.Z),
				aabb.Position + new Vector3(aabb.Size.X, aabb.Size.Y, 0f),
				aabb.Position + new Vector3(aabb.Size.X, 0f, aabb.Size.Z),
				aabb.Position + new Vector3(0f, aabb.Size.Y, aabb.Size.Z),
				aabb.Position + aabb.Size
			};
			foreach (var c in corners)
			{
				var w = local * c;
				min = new Vector3(Mathf.Min(min.X, w.X), Mathf.Min(min.Y, w.Y), Mathf.Min(min.Z, w.Z));
				max = new Vector3(Mathf.Max(max.X, w.X), Mathf.Max(max.Y, w.Y), Mathf.Max(max.Z, w.Z));
				any = true;
			}
		}

		foreach (var child in node.GetChildren())
			AccumulateMeshes(child, local, ref min, ref max, ref any);
	}

	private static Color PickContainerPaint(Color ambience)
	{
		var paints = new[]
		{
			new Color(0.15f, 0.35f, 0.55f),
			new Color(0.55f, 0.2f, 0.15f),
			new Color(0.2f, 0.45f, 0.28f),
			new Color(0.7f, 0.55f, 0.15f),
			new Color(0.45f, 0.45f, 0.48f)
		};
		var idx = Mathf.Abs((int)(ambience.R * 17f + ambience.G * 31f + ambience.B * 13f)) % paints.Length;
		return paints[idx].Lerp(ambience, 0.12f);
	}

	private static StandardMaterial3D MakeMat(
		Color albedo,
		float metallic,
		float roughness,
		Color? emission = null,
		float emissionEnergy = 0f,
		SurfaceLibrary.Kind? surface = null)
	{
		// Emissive accents stay flat; everything else prefers tileable PBR when available.
		if (surface.HasValue && emissionEnergy <= 0.001f)
			return SurfaceLibrary.Get(surface.Value, albedo);

		return SurfaceLibrary.Flat(albedo, metallic, roughness, emission, emissionEnergy);
	}

	private static void AddBox(Node3D parent, Material mat, Vector3 size, Vector3 position)
	{
		parent.AddChild(MeshMat.Make(new BoxMesh { Size = size }, mat, position));
	}

	private static void AddCylinder(Node3D parent, Material mat, float radius, float height, Vector3 position, Vector3? rotation = null)
	{
		parent.AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 16 },
			mat,
			position,
			rotation ?? Vector3.Zero));
	}

	private static void AddWheel(Node3D parent, Material mat, Vector3 position, float scale)
	{
		parent.AddChild(MeshMat.Make(
			new CylinderMesh
			{
				TopRadius = 0.45f * scale,
				BottomRadius = 0.45f * scale,
				Height = 0.35f * scale
			},
			mat,
			position,
			Vector3.Forward * Mathf.Tau * 0.25f));
	}
}
