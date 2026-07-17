using Godot;

namespace Mechanize;

/// <summary>
/// Procedural cover props — shipping containers, barriers, tanks, vehicles, buildings.
/// Collision is a single AABB so AI / projectiles keep working the same way.
/// </summary>
public static class CoverVisualFactory
{
	public readonly struct BuiltCover
	{
		public BuiltCover(
			Node3D visual,
			Vector3 collisionSize,
			float health,
			bool destructible,
			Color shatterColor,
			Vector3? collisionCenter = null)
		{
			Visual = visual;
			CollisionSize = collisionSize;
			CollisionCenter = collisionCenter ?? new Vector3(0f, collisionSize.Y * 0.5f, 0f);
			Health = health;
			Destructible = destructible;
			ShatterColor = shatterColor;
		}

		public Node3D Visual { get; }
		public Vector3 CollisionSize { get; }
		/// <summary>Local center of the blocking volume (matches visual footprint).</summary>
		public Vector3 CollisionCenter { get; }
		public float Health { get; }
		public bool Destructible { get; }
		public Color ShatterColor { get; }
	}

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
			_ => BuildShippingContainer(ambience, scale, stacked: false)
		};
	}

	private static BuiltCover BuildShippingContainer(Color ambience, float scale, bool stacked)
	{
		var root = new Node3D { Name = "ShippingContainer" };
		var paint = PickContainerPaint(ambience);
		var body = MakeMat(paint, 0.45f, 0.5f);
		var rib = MakeMat(paint.Darkened(0.22f), 0.5f, 0.45f);
		var door = MakeMat(paint.Darkened(0.35f), 0.55f, 0.4f);
		var corner = MakeMat(new Color(0.35f, 0.35f, 0.32f), 0.7f, 0.35f);
		var accent = MakeMat(paint.Lightened(0.2f), 0.3f, 0.45f);

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
		var concrete = MakeMat(new Color(0.55f, 0.54f, 0.5f).Lerp(ambience, 0.15f), 0.05f, 0.92f);
		var dark = MakeMat(new Color(0.4f, 0.39f, 0.36f), 0.05f, 0.9f);
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
		var steel = MakeMat(new Color(0.42f, 0.38f, 0.32f).Lerp(ambience, 0.2f), 0.65f, 0.4f);
		var rust = MakeMat(new Color(0.55f, 0.28f, 0.14f), 0.4f, 0.55f);
		var dark = MakeMat(new Color(0.2f, 0.2f, 0.22f), 0.7f, 0.35f);
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
		var cabPaint = MakeMat(new Color(0.7f, 0.18f, 0.14f).Lerp(ambience, 0.15f), 0.35f, 0.45f);
		var trailer = MakeMat(new Color(0.75f, 0.72f, 0.65f), 0.25f, 0.55f);
		var dark = MakeMat(new Color(0.18f, 0.18f, 0.2f), 0.5f, 0.4f);
		var glass = MakeMat(new Color(0.35f, 0.55f, 0.7f), 0.1f, 0.25f, new Color(0.4f, 0.65f, 0.85f), 0.4f);
		var chrome = MakeMat(new Color(0.55f, 0.55f, 0.58f), 0.85f, 0.3f);

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
		var wall = MakeMat(new Color(0.55f, 0.5f, 0.42f).Lerp(ambience, 0.25f), 0.15f, 0.85f);
		var roof = MakeMat(new Color(0.28f, 0.3f, 0.34f), 0.4f, 0.55f);
		var dark = MakeMat(new Color(0.22f, 0.22f, 0.24f), 0.35f, 0.6f);
		var door = MakeMat(new Color(0.35f, 0.38f, 0.4f), 0.45f, 0.5f);

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
		AddBox(root, MakeMat(new Color(0.7f, 0.55f, 0.2f), 0.2f, 0.5f), new Vector3(w * 0.5f, 0.2f, 0.08f),
			new Vector3(0f, h * 0.85f, d * 0.52f));

		var size = new Vector3(w * 1.05f, h + 0.5f, d * 1.05f);
		return Finish(root, size, 320f, true, wall.AlbedoColor);
	}

	private static BuiltCover BuildIndustrialShed(Color ambience, float scale)
	{
		var root = new Node3D { Name = "IndustrialShed" };
		var wall = MakeMat(new Color(0.4f, 0.45f, 0.48f).Lerp(ambience, 0.2f), 0.3f, 0.7f);
		var roof = MakeMat(new Color(0.25f, 0.28f, 0.3f), 0.5f, 0.5f);
		var dark = MakeMat(new Color(0.2f, 0.2f, 0.22f), 0.4f, 0.55f);

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
		var facade = MakeMat(new Color(0.28f, 0.32f, 0.38f).Lerp(ambience, 0.15f), 0.55f, 0.4f);
		var glass = MakeMat(new Color(0.25f, 0.4f, 0.55f), 0.2f, 0.25f, new Color(0.35f, 0.55f, 0.75f), 0.35f);
		var dark = MakeMat(new Color(0.12f, 0.13f, 0.15f), 0.4f, 0.5f);
		var trim = MakeMat(new Color(0.55f, 0.5f, 0.35f).Lerp(ambience, 0.2f), 0.45f, 0.45f);

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
		var frame = MakeMat(new Color(0.35f, 0.35f, 0.32f).Lerp(ambience, 0.15f), 0.6f, 0.4f);
		var pipeA = MakeMat(new Color(0.55f, 0.45f, 0.25f), 0.5f, 0.45f);
		var pipeB = MakeMat(new Color(0.3f, 0.4f, 0.45f), 0.55f, 0.4f);
		var dark = MakeMat(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.5f);

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
		float emissionEnergy = 0f)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = albedo,
			Metallic = metallic,
			Roughness = roughness
		};
		if (emission.HasValue && emissionEnergy > 0f)
		{
			mat.EmissionEnabled = true;
			mat.Emission = emission.Value;
			mat.EmissionEnergyMultiplier = emissionEnergy;
		}
		return mat;
	}

	private static void AddBox(Node3D parent, Material mat, Vector3 size, Vector3 position)
	{
		parent.AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			Position = position,
			MaterialOverride = mat
		});
	}

	private static void AddCylinder(Node3D parent, Material mat, float radius, float height, Vector3 position, Vector3? rotation = null)
	{
		parent.AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 16 },
			Position = position,
			Rotation = rotation ?? Vector3.Zero,
			MaterialOverride = mat
		});
	}

	private static void AddWheel(Node3D parent, Material mat, Vector3 position, float scale)
	{
		parent.AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh
			{
				TopRadius = 0.45f * scale,
				BottomRadius = 0.45f * scale,
				Height = 0.35f * scale
			},
			Position = position,
			Rotation = Vector3.Forward * Mathf.Tau * 0.25f,
			MaterialOverride = mat
		});
	}
}
