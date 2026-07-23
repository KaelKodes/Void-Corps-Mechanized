using Godot;

namespace Mechanize;

/// <summary>
/// Ensures a diegetic SeatLever under CockpitInterior/Dashboard on hollow hulls.
/// Gaze-ray Area3D lives on collision layer <see cref="CollisionLayerBit"/>.
/// </summary>
public static class CockpitSeatLever
{
	public const string NodeName = "SeatLever";
	/// <summary>Physics layer bit 5 (value 16) — seat interact only.</summary>
	public const uint CollisionLayerBit = 16;

	/// <summary>Dashboard-local placement: left of the center stick, toward the pilot.</summary>
	private static readonly Vector3 LocalPos = new(-0.22f, -0.02f, -0.12f);

	public static Area3D? Find(Node mechRoot)
	{
		var node = mechRoot.FindChild(NodeName, recursive: true, owned: false);
		return node as Area3D;
	}

	public static Area3D? EnsureOn(CockpitTorsoVisual hull)
	{
		if (!GodotObject.IsInstanceValid(hull))
			return null;

		var existing = hull.FindChild(NodeName, recursive: true, owned: false) as Area3D;
		if (existing != null)
			return existing;

		var dashboard = hull.GetNodeOrNull<Node3D>("CockpitInterior/Dashboard");
		if (dashboard == null)
			return null;

		var lever = new Area3D
		{
			Name = NodeName,
			CollisionLayer = CollisionLayerBit,
			CollisionMask = 0,
			Monitoring = false,
			Monitorable = true,
			Position = LocalPos
		};

		var mesh = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.12f, 0.035f) },
			Position = new Vector3(0f, 0.06f, 0f)
		};
		mesh.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.42f, 0.22f),
			Metallic = 0.55f,
			Roughness = 0.4f,
			EmissionEnabled = true,
			Emission = new Color(0.7f, 0.45f, 0.15f),
			EmissionEnergyMultiplier = 0.15f
		};
		lever.AddChild(mesh);

		var knob = new MeshInstance3D
		{
			Name = "Knob",
			Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.04f, 0.05f) },
			Position = new Vector3(0f, 0.13f, 0f)
		};
		knob.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.75f, 0.55f, 0.25f),
			Metallic = 0.4f,
			Roughness = 0.35f
		};
		lever.AddChild(knob);

		var shape = new CollisionShape3D
		{
			Name = "Hit",
			Shape = new BoxShape3D { Size = new Vector3(0.1f, 0.2f, 0.1f) },
			Position = new Vector3(0f, 0.08f, 0f)
		};
		lever.AddChild(shape);

		dashboard.AddChild(lever);
		return lever;
	}

	public static void SetHighlight(Area3D? lever, bool on)
	{
		if (lever == null || !GodotObject.IsInstanceValid(lever))
			return;
		if (lever.GetNodeOrNull<MeshInstance3D>("Mesh")?.MaterialOverride is StandardMaterial3D mat)
			mat.EmissionEnergyMultiplier = on ? 0.85f : 0.15f;
	}
}
