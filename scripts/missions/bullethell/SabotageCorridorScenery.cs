using Godot;

namespace Mechanize;

/// <summary>
/// Visual-only props outside the Sabotage arena shell so forward travel stays readable.
/// </summary>
public static class SabotageCorridorScenery
{
	public static void Build(Node3D parent, ClaimArenaLayout layout)
	{
		var existing = parent.GetNodeOrNull<Node3D>("CorridorScenery");
		if (existing != null)
		{
			parent.RemoveChild(existing);
			existing.Free();
		}

		var root = new Node3D { Name = "CorridorScenery" };
		parent.AddChild(root);

		var halfX = layout.HalfExtentX;
		var halfZ = layout.HalfExtentZ;
		var ambience = layout.WallColor;
		var outside = halfX + 14f;
		var rng = new RandomNumberGenerator();
		rng.Seed = 0xEC4E10u;

		// East / west industrial spine — landmarks tick by as you push north.
		for (var z = -halfZ + 30f; z <= halfZ - 30f; z += 48f)
		{
			var eastKind = PickKind(rng);
			var westKind = PickKind(rng);
			Place(root, eastKind, new Vector3(outside + rng.RandfRange(0f, 8f), 0f, z + rng.RandfRange(-6f, 6f)),
				rng.RandfRange(-20f, 20f), ambience, rng.RandfRange(0.85f, 1.25f));
			Place(root, westKind, new Vector3(-outside - rng.RandfRange(0f, 8f), 0f, z + rng.RandfRange(-6f, 6f)),
				rng.RandfRange(160f, 200f), ambience, rng.RandfRange(0.85f, 1.25f));
		}

		// North uplink approach — denser skyline behind Point B.
		for (var i = 0; i < 5; i++)
		{
			var x = Mathf.Lerp(-outside - 4f, outside + 4f, i / 4f);
			Place(root, CoverKind.Skyscraper,
				new Vector3(x, 0f, -halfZ - 18f - (i % 2) * 10f),
				rng.RandfRange(-12f, 12f), ambience, rng.RandfRange(0.9f, 1.35f));
		}

		// South ingress — reminds you where you dropped in.
		Place(root, CoverKind.Warehouse, new Vector3(-outside - 2f, 0f, halfZ + 16f), 15f, ambience, 1.1f);
		Place(root, CoverKind.Warehouse, new Vector3(outside + 2f, 0f, halfZ + 16f), -15f, ambience, 1.1f);
		Place(root, CoverKind.OilTankCluster, new Vector3(0f, 0f, halfZ + 22f), 0f, ambience, 0.95f);
	}

	private static CoverKind PickKind(RandomNumberGenerator rng)
	{
		var roll = rng.Randi() % 6;
		return roll switch
		{
			0 => CoverKind.Skyscraper,
			1 => CoverKind.Warehouse,
			2 => CoverKind.IndustrialShed,
			3 => CoverKind.PipeRack,
			4 => CoverKind.OilTank,
			_ => CoverKind.SemiTrailer
		};
	}

	private static void Place(
		Node3D root,
		CoverKind kind,
		Vector3 position,
		float yawDegrees,
		Color ambience,
		float scale)
	{
		var seed = (int)(position.X * 5f) + (int)(position.Z * 11f) + (int)kind * 19;
		var built = CoverVisualFactory.Build(kind, ambience, scale, seed);
		var holder = new Node3D
		{
			Name = $"Scenery_{kind}",
			Position = position,
			RotationDegrees = new Vector3(0f, yawDegrees, 0f)
		};
		holder.AddChild(built.Visual);
		root.AddChild(holder);
	}
}
