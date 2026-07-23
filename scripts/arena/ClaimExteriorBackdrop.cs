using Godot;

namespace Mechanize;

/// <summary>
/// Non-playable set dressing past the pad barrier — silhouette + depth.
/// Near belt can poke through the rim (collision on); far belt is visual-only.
/// </summary>
public static class ClaimExteriorBackdrop
{
	/// <summary>How far the playable floor continues past the barrier walls (meters per side).</summary>
	public const float FloorApron = 28f;

	public static void Rebuild(Node3D world, ClaimArenaLayout layout)
	{
		var existing = world.GetNodeOrNull("ExteriorBackdrop");
		if (existing != null)
			MeshMat.QueueFreeSafe(existing);

		var root = new Node3D { Name = "ExteriorBackdrop" };
		world.AddChild(root);

		var halfX = layout.HalfExtentX;
		var halfZ = layout.HalfExtentZ;
		var half = Mathf.Max(halfX, halfZ);
		var isSpire = layout.ClaimCode.Contains("SPIRE-NULL", System.StringComparison.OrdinalIgnoreCase);

		// Bright enough to read against night sky when looking past the barrier.
		var nearAmbience = isSpire
			? new Color(0.48f, 0.54f, 0.64f)
			: new Color(0.40f, 0.44f, 0.50f);
		var farAmbience = new Color(0.28f, 0.32f, 0.40f);

		// Hug the outside of the barrier — FP at the rim should see massing immediately.
		var near = half + 2.5f;
		var step = isSpire ? 9f : 14f;
		var scale = isSpire ? 1.55f : 1.25f;

		// Low industrial blocks (doors face the pad). Towers rise behind them on purpose.
		// Skip skyline slots that fall inside these footprints so we don't get random pierces.
		var clusters = new[]
		{
			// West — warehouse long-axis along wall; doors toward +X (pad).
			new RimCluster(new Vector3(-near - 10f, 0f, -14f), -90f, CoverKind.Warehouse, 1.45f, alongX: false),
			// East
			new RimCluster(new Vector3(near + 10f, 0f, 10f), 90f, CoverKind.Warehouse, 1.5f, alongX: false),
			// North — shed doors toward +Z (pad).
			new RimCluster(new Vector3(8f, 0f, -near - 9f), 0f, CoverKind.IndustrialShed, 1.55f, alongX: true),
			// South — tank farm (no tower stack).
			new RimCluster(new Vector3(-6f, 0f, near + 10f), 0f, CoverKind.OilTankCluster, 1.45f, alongX: true, withTowers: false)
		};

		PlaceSide(root, CoverKind.Skyscraper, alongX: true, fixedCoord: -near,
			from: -half + 1f, to: half - 1f, step: step,
			yaw: 0f, scaleBase: scale, ambience: nearAmbience, depthJitter: 2f, maxScale: 2.2f,
			withCollision: true, halfX: halfX, halfZ: halfZ, skip: clusters);
		PlaceSide(root, CoverKind.Skyscraper, alongX: true, fixedCoord: near,
			from: -half + 2f, to: half - 2f, step: step + 1f,
			yaw: 180f, scaleBase: scale + 0.05f, ambience: nearAmbience, depthJitter: 2.5f, maxScale: 2.2f,
			withCollision: true, halfX: halfX, halfZ: halfZ, skip: clusters);
		PlaceSide(root, CoverKind.Skyscraper, alongX: false, fixedCoord: -near,
			from: -half + 1f, to: half - 1f, step: step,
			yaw: 90f, scaleBase: scale - 0.05f, ambience: nearAmbience, depthJitter: 2f, maxScale: 2.2f,
			withCollision: true, halfX: halfX, halfZ: halfZ, skip: clusters);
		PlaceSide(root, CoverKind.Skyscraper, alongX: false, fixedCoord: near,
			from: -half + 3f, to: half - 3f, step: step + 1f,
			yaw: -90f, scaleBase: scale + 0.1f, ambience: nearAmbience, depthJitter: 2.5f, maxScale: 2.2f,
			withCollision: true, halfX: halfX, halfZ: halfZ, skip: clusters);

		foreach (var c in clusters)
			PlaceRimCluster(root, c, nearAmbience, scale, halfX, halfZ);

		if (!isSpire)
			return;

		// Far belt — oversized forced-perspective massing (visual only).
		var far = half + 32f;
		PlaceSide(root, CoverKind.Skyscraper, alongX: true, fixedCoord: -far,
			from: -half - 24f, to: half + 24f, step: 16f,
			yaw: 5f, scaleBase: 2.6f, ambience: farAmbience, depthJitter: 7f, maxScale: 3.4f,
			withCollision: false, halfX: halfX, halfZ: halfZ);
		PlaceSide(root, CoverKind.Skyscraper, alongX: true, fixedCoord: far,
			from: -half - 20f, to: half + 20f, step: 18f,
			yaw: 175f, scaleBase: 2.8f, ambience: farAmbience, depthJitter: 8f, maxScale: 3.4f,
			withCollision: false, halfX: halfX, halfZ: halfZ);
		PlaceSide(root, CoverKind.Skyscraper, alongX: false, fixedCoord: -far,
			from: -half - 22f, to: half + 22f, step: 16f,
			yaw: 85f, scaleBase: 2.7f, ambience: farAmbience, depthJitter: 7f, maxScale: 3.4f,
			withCollision: false, halfX: halfX, halfZ: halfZ);
		PlaceSide(root, CoverKind.Skyscraper, alongX: false, fixedCoord: far,
			from: -half - 16f, to: half + 16f, step: 18f,
			yaw: -95f, scaleBase: 2.9f, ambience: farAmbience, depthJitter: 8f, maxScale: 3.4f,
			withCollision: false, halfX: halfX, halfZ: halfZ);
	}

	/// <summary>
	/// Real sky dome from claim colors — pad used flat BGMode.Color (near-black void, no horizon).
	/// </summary>
	public static void ApplySky(Godot.Environment env, ClaimArenaLayout layout, ArenaPeriod period = ArenaPeriod.Night)
	{
		var baseSky = layout.SkyColor;
		Color top;
		Color horizon;
		float skyEnergy;
		float groundEnergy;

		if (period == ArenaPeriod.Day)
		{
			top = baseSky.Lightened(0.55f).Lerp(new Color(0.35f, 0.55f, 0.85f), 0.7f);
			horizon = baseSky.Lightened(0.65f).Lerp(new Color(0.75f, 0.82f, 0.92f), 0.55f);
			skyEnergy = 1.15f;
			groundEnergy = 0.55f;
		}
		else
		{
			// Lift authored near-blacks so the dome reads as night sky, not crushed void.
			top = baseSky.Lightened(0.08f).Lerp(new Color(0.06f, 0.09f, 0.16f), 0.55f);
			horizon = baseSky.Lightened(0.22f).Lerp(layout.AmbientColor, 0.35f);
			horizon = new Color(
				Mathf.Clamp(horizon.R * 1.35f, 0f, 1f),
				Mathf.Clamp(horizon.G * 1.25f, 0f, 1f),
				Mathf.Clamp(horizon.B * 1.15f, 0f, 1f));
			skyEnergy = 0.85f;
			groundEnergy = 0.35f;
		}

		// Ground band stays dark so it doesn't compete with the extended pad apron.
		var groundHorizon = period == ArenaPeriod.Day
			? layout.FloorColor.Darkened(0.15f)
			: layout.FloorColor.Darkened(0.35f);
		var groundBottom = period == ArenaPeriod.Day
			? layout.FloorColor.Darkened(0.3f)
			: layout.FloorColor.Darkened(0.55f);

		var skyMat = new ProceduralSkyMaterial
		{
			SkyTopColor = top,
			SkyHorizonColor = horizon,
			SkyEnergyMultiplier = skyEnergy,
			SkyCurve = period == ArenaPeriod.Day ? 0.08f : 0.12f,
			GroundBottomColor = groundBottom,
			GroundHorizonColor = groundHorizon,
			GroundEnergyMultiplier = groundEnergy,
			GroundCurve = 0.08f,
			SunAngleMax = period == ArenaPeriod.Day ? 30f : 22f,
			SunCurve = 0.12f
		};

		env.BackgroundMode = Godot.Environment.BGMode.Sky;
		env.Sky = new Sky { SkyMaterial = skyMat };
		env.BackgroundColor = period == ArenaPeriod.Day ? horizon : baseSky;
		env.AmbientLightSkyContribution = 0f; // lighting stays on AmbientSource.Color
	}

	public static void ApplyFog(Godot.Environment env, ClaimArenaLayout layout, ArenaPeriod period = ArenaPeriod.Night)
	{
		if (!layout.ClaimCode.Contains("SPIRE-NULL", System.StringComparison.OrdinalIgnoreCase))
		{
			env.FogEnabled = false;
			return;
		}

		env.FogEnabled = true;
		env.FogMode = Godot.Environment.FogModeEnum.Depth;
		if (period == ArenaPeriod.Day)
		{
			env.FogLightColor = new Color(0.55f, 0.62f, 0.72f);
			env.FogLightEnergy = 0.35f;
			env.FogDensity = 0.0f;
			env.FogAerialPerspective = 0.12f;
			env.FogSkyAffect = 0.08f;
			env.FogDepthCurve = 1.0f;
			env.FogDepthBegin = 70f;
			env.FogDepthEnd = 160f;
		}
		else
		{
			env.FogLightColor = new Color(0.10f, 0.12f, 0.16f);
			env.FogLightEnergy = 0.55f;
			env.FogDensity = 0.0f;
			env.FogAerialPerspective = 0.2f;
			env.FogSkyAffect = 0.15f;
			env.FogDepthCurve = 1.05f;
			env.FogDepthBegin = 55f;
			env.FogDepthEnd = 140f;
		}
	}

	readonly struct RimCluster(
		Vector3 position,
		float yawDegrees,
		CoverKind kind,
		float scale,
		bool alongX,
		bool withTowers = true)
	{
		public Vector3 Position { get; } = position;
		public float YawDegrees { get; } = yawDegrees;
		public CoverKind Kind { get; } = kind;
		public float Scale { get; } = scale;
		/// <summary>True if this cluster sits on a north/south rim (wall along X).</summary>
		public bool AlongX { get; } = alongX;
		public bool WithTowers { get; } = withTowers;
		/// <summary>Clearance along the wall so PlaceSide doesn't stab through the block.</summary>
		public float ClearRadius => Kind == CoverKind.Warehouse ? 13f : 9f;
	}

	/// <param name="alongX">True = walk along X at fixed Z; false = walk along Z at fixed X.</param>
	static void PlaceSide(
		Node3D root,
		CoverKind kind,
		bool alongX,
		float fixedCoord,
		float from,
		float to,
		float step,
		float yaw,
		float scaleBase,
		Color ambience,
		float depthJitter,
		float maxScale,
		bool withCollision,
		float halfX,
		float halfZ,
		RimCluster[]? skip = null)
	{
		for (var t = from; t <= to + 0.01f; t += step)
		{
			if (skip != null && HitsCluster(alongX, t, fixedCoord, skip))
				continue;

			var depth = fixedCoord + (float)GD.RandRange(-depthJitter * 0.35f, depthJitter);
			var sc = Mathf.Clamp(scaleBase * (float)GD.RandRange(0.88f, 1.14f), 0.9f, maxScale);
			var yawJ = yaw + (float)GD.RandRange(-8f, 8f);
			var jitter = (float)GD.RandRange(-1.2f, 1.2f);
			var pos = alongX
				? new Vector3(t + jitter, 0f, depth)
				: new Vector3(depth, 0f, t + jitter);
			AddProp(root, kind, pos, yawJ, sc, ambience, maxScale, withCollision, halfX, halfZ);
		}
	}

	static bool HitsCluster(bool alongX, float t, float fixedCoord, RimCluster[] clusters)
	{
		foreach (var c in clusters)
		{
			// Only skip on the same rim the cluster sits on.
			if (c.AlongX != alongX)
				continue;
			var along = alongX ? c.Position.X : c.Position.Z;
			var depth = alongX ? c.Position.Z : c.Position.X;
			if (Mathf.Abs(t - along) > c.ClearRadius)
				continue;
			// Same outward side (don't clear the opposite wall).
			if (Mathf.Sign(fixedCoord) != Mathf.Sign(depth) && Mathf.Abs(fixedCoord) > 1f)
				continue;
			return true;
		}

		return false;
	}

	static void PlaceRimCluster(
		Node3D root,
		RimCluster cluster,
		Color ambience,
		float towerScale,
		float halfX,
		float halfZ)
	{
		AddProp(root, cluster.Kind, cluster.Position, cluster.YawDegrees, cluster.Scale, ambience, 2.0f,
			withCollision: true, halfX: halfX, halfZ: halfZ);

		if (!cluster.WithTowers)
			return;

		// Towers rise behind the block (further from pad) — intentional "coming out of" silhouette.
		var outward = cluster.AlongX
			? new Vector3(0f, 0f, Mathf.Sign(cluster.Position.Z) * 5.5f)
			: new Vector3(Mathf.Sign(cluster.Position.X) * 5.5f, 0f, 0f);
		var along = cluster.AlongX ? Vector3.Right : Vector3.Back;
		var towerYaw = cluster.AlongX
			? (cluster.Position.Z < 0f ? 0f : 180f)
			: (cluster.Position.X < 0f ? 90f : -90f);

		AddProp(root, CoverKind.Skyscraper, cluster.Position + outward - along * 4.5f,
			towerYaw + 6f, towerScale * 1.05f, ambience, 2.2f,
			withCollision: true, halfX: halfX, halfZ: halfZ);
		AddProp(root, CoverKind.Skyscraper, cluster.Position + outward * 1.15f + along * 5f,
			towerYaw - 4f, towerScale * 0.92f, ambience, 2.2f,
			withCollision: true, halfX: halfX, halfZ: halfZ);
	}

	static void AddProp(
		Node3D root,
		CoverKind kind,
		Vector3 pos,
		float yawDeg,
		float scale,
		Color ambience,
		float maxScale,
		bool withCollision,
		float halfX,
		float halfZ)
	{
		var built = CoverVisualFactory.Build(kind, ambience, scale, variationSeed: pos.GetHashCode(), maxScale: maxScale);
		var visual = built.Visual;
		TintSubtree(visual, ambience);

		var needsCollision = withCollision && OverlapsPad(pos, built.CollisionSize, yawDeg, halfX, halfZ);
		if (!needsCollision)
		{
			visual.Position = pos;
			visual.RotationDegrees = new Vector3(0f, yawDeg, 0f);
			root.AddChild(visual);
			return;
		}

		// Same collision contract as field cover — solid, non-destructible backdrop.
		var body = new StaticBody3D
		{
			Name = $"Exterior_{kind}",
			Position = pos,
			RotationDegrees = new Vector3(0f, yawDeg, 0f),
			CollisionLayer = PhysicsLayers.World,
			CollisionMask = 0
		};
		body.AddChild(visual);
		body.AddChild(new CollisionShape3D
		{
			Name = "Collision",
			Shape = new BoxShape3D { Size = built.CollisionSize },
			Position = built.CollisionCenter,
			RotationDegrees = built.CollisionRotationDegrees
		});
		for (var e = 0; e < built.ExtraCollisions.Length; e++)
		{
			var vol = built.ExtraCollisions[e];
			body.AddChild(new CollisionShape3D
			{
				Name = $"CollisionExtra_{e}",
				Shape = new BoxShape3D { Size = vol.Size },
				Position = vol.Center,
				RotationDegrees = vol.RotationDegrees
			});
		}

		root.AddChild(body);
	}

	/// <summary>
	/// Rough AABB vs pad rectangle — if the prop footprint crosses inside the barrier, it needs collision.
	/// </summary>
	static bool OverlapsPad(Vector3 pos, Vector3 collisionSize, float yawDeg, float halfX, float halfZ)
	{
		var yaw = Mathf.DegToRad(yawDeg);
		var c = Mathf.Abs(Mathf.Cos(yaw));
		var s = Mathf.Abs(Mathf.Sin(yaw));
		// 2D footprint half-extents after yaw (XZ plane).
		var halfW = 0.5f * (collisionSize.X * c + collisionSize.Z * s);
		var halfD = 0.5f * (collisionSize.X * s + collisionSize.Z * c);
		var minX = pos.X - halfW;
		var maxX = pos.X + halfW;
		var minZ = pos.Z - halfD;
		var maxZ = pos.Z + halfD;
		return minX < halfX && maxX > -halfX && minZ < halfZ && maxZ > -halfZ;
	}

	static void TintSubtree(Node node, Color ambience)
	{
		if (node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D mat)
		{
			var tinted = (StandardMaterial3D)mat.Duplicate();
			tinted.AlbedoColor = ambience;
			mi.MaterialOverride = tinted;
		}

		foreach (var child in node.GetChildren())
			TintSubtree(child, ambience);
	}
}
