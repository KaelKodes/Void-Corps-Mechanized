using Godot;

namespace Mechanize;

public static class PartVisualFactory
{
	public static Node3D Create(PartData part, MechChassisClass chassisClass = MechChassisClass.Standard)
	{
		if (chassisClass == MechChassisClass.Titan)
			return TitanPartVisualFactory.Create(part);

		var root = new Node3D { Name = $"Visual_{part.Id}" };
		var mat = MakeMat(part.Tint, metallic: 0.38f, roughness: 0.52f);
		var dark = MakeMat(part.Tint.Darkened(0.32f), metallic: 0.45f, roughness: 0.48f);
		var light = MakeMat(part.Tint.Lightened(0.18f), metallic: 0.3f, roughness: 0.45f);
		var glow = MakeMat(part.Tint.Lightened(0.35f), metallic: 0.2f, roughness: 0.35f, emission: part.Tint.Lightened(0.2f), emissionEnergy: 1.1f);

		switch (part.VisualKind)
		{
			case "legs":
			case "legs_biped":
				BuildBipedLegs(root, mat, dark, light);
				break;

			case "legs_hex":
				BuildHexLegs(root, mat, dark);
				break;

			case "legs_tracks":
				AddBox(root, mat, new Vector3(1.45f, 0.32f, 1.05f), new Vector3(0f, 0.42f, 0f));
				AddBox(root, dark, new Vector3(1.1f, 0.12f, 0.75f), new Vector3(0f, 0.58f, 0f));
				AddBox(root, dark, new Vector3(0.32f, 0.5f, 1.35f), new Vector3(-0.7f, 0.28f, 0f));
				AddBox(root, dark, new Vector3(0.32f, 0.5f, 1.35f), new Vector3(0.7f, 0.28f, 0f));
				AddWheel(root, light, new Vector3(-0.7f, 0.18f, -0.5f));
				AddWheel(root, light, new Vector3(-0.7f, 0.18f, 0f));
				AddWheel(root, light, new Vector3(-0.7f, 0.18f, 0.5f));
				AddWheel(root, light, new Vector3(0.7f, 0.18f, -0.5f));
				AddWheel(root, light, new Vector3(0.7f, 0.18f, 0f));
				AddWheel(root, light, new Vector3(0.7f, 0.18f, 0.5f));
				break;

			case "torso":
				AddBox(root, mat, new Vector3(1.4f, 1.05f, 1.0f) * part.VisualScale, new Vector3(0f, 0.55f, 0f));
				AddBox(root, dark, new Vector3(1.15f, 0.18f, 0.85f), new Vector3(0f, 0.95f, 0.05f));
				// Pauldrons
				AddBox(root, light, new Vector3(0.35f, 0.35f, 0.55f), new Vector3(-0.75f, 0.85f, 0f));
				AddBox(root, light, new Vector3(0.35f, 0.35f, 0.55f), new Vector3(0.75f, 0.85f, 0f));
				// Chest vent
				AddBox(root, dark, new Vector3(0.55f, 0.35f, 0.2f), new Vector3(0f, 0.55f, -0.45f));
				AddBox(root, glow, new Vector3(0.35f, 0.08f, 0.05f), new Vector3(0f, 0.58f, -0.52f));
				AddBox(root, mat, new Vector3(0.65f, 0.32f, 0.65f), new Vector3(0f, 1.2f, 0f));
				break;

			case "head":
				AddBox(root, mat, new Vector3(0.55f, 0.42f, 0.5f) * part.VisualScale, new Vector3(0f, 0.2f, 0f));
				AddBox(root, dark, new Vector3(0.4f, 0.1f, 0.22f), new Vector3(0f, 0.38f, -0.05f));
				AddBox(root, dark, new Vector3(0.38f, 0.14f, 0.18f), new Vector3(0f, 0.24f, -0.28f));
				AddSphere(root, glow, 0.07f, new Vector3(-0.12f, 0.22f, -0.24f));
				AddSphere(root, glow, 0.07f, new Vector3(0.12f, 0.22f, -0.24f));
				AddCylinder(root, dark, 0.03f, 0.22f, new Vector3(0.2f, 0.42f, 0.05f), Vector3.Zero);
				break;

			case "core":
				AddCylinder(root, mat, 0.28f, 0.5f, new Vector3(0f, 0.28f, 0f), Vector3.Zero);
				AddCylinder(root, dark, 0.32f, 0.1f, new Vector3(0f, 0.08f, 0f), Vector3.Zero);
				AddSphere(root, glow, 0.16f, new Vector3(0f, 0.52f, 0f));
				AddBox(root, dark, new Vector3(0.55f, 0.08f, 0.55f), new Vector3(0f, 0.04f, 0f));
				break;

			case "cannon":
				AddBox(root, mat, new Vector3(0.38f, 0.38f, 0.85f), new Vector3(0f, 0f, -0.15f));
				AddBox(root, dark, new Vector3(0.28f, 0.18f, 0.35f), new Vector3(0f, 0.2f, 0.05f));
				AddCylinder(root, dark, 0.13f, 1.05f, new Vector3(0f, 0f, -0.85f), Vector3.Right * Mathf.Tau * 0.25f);
				AddCylinder(root, light, 0.16f, 0.12f, new Vector3(0f, 0f, -1.35f), Vector3.Right * Mathf.Tau * 0.25f);
				AddCylinder(root, glow, 0.06f, 0.08f, new Vector3(0f, 0f, -1.42f), Vector3.Right * Mathf.Tau * 0.25f);
				break;

			case "rifle":
				AddBox(root, mat, new Vector3(0.18f, 0.18f, 1.15f), new Vector3(0f, 0f, -0.3f));
				AddBox(root, dark, new Vector3(0.14f, 0.22f, 0.28f), new Vector3(0f, 0.12f, 0.22f));
				AddBox(root, light, new Vector3(0.1f, 0.08f, 0.18f), new Vector3(0f, 0.14f, -0.15f));
				AddBox(root, dark, new Vector3(0.12f, 0.12f, 0.12f), new Vector3(0f, 0f, -0.9f));
				break;

			case "energy":
				AddBox(root, mat, new Vector3(0.26f, 0.26f, 0.75f), new Vector3(0f, 0f, -0.15f));
				AddBox(root, dark, new Vector3(0.2f, 0.14f, 0.3f), new Vector3(0f, 0.16f, 0.1f));
				AddCylinder(root, light, 0.1f, 0.35f, new Vector3(0f, 0f, -0.55f), Vector3.Right * Mathf.Tau * 0.25f);
				AddSphere(root, glow, 0.18f, new Vector3(0f, 0f, -0.82f));
				break;

			case "cleaver":
				AddBox(root, mat, new Vector3(0.22f, 0.28f, 0.55f), new Vector3(0f, 0f, 0.05f));
				AddBox(root, dark, new Vector3(0.12f, 0.18f, 0.35f), new Vector3(0f, 0.08f, 0.2f));
				// Forge Cleaver blade: 15% longer, extending forward from the same guard.
				AddBox(root, light, new Vector3(0.08f, 0.55f, 1.2075f), new Vector3(0.02f, 0.05f, -0.62875f));
				AddBox(root, glow, new Vector3(0.04f, 0.42f, 1.035f), new Vector3(0.06f, 0.05f, -0.6675f));
				break;

			case "held_shield":
				AddBox(root, mat, new Vector3(0.95f, 1.15f, 0.12f), new Vector3(0f, 0.15f, -0.35f));
				AddBox(root, dark, new Vector3(0.78f, 0.95f, 0.06f), new Vector3(0f, 0.15f, -0.42f));
				AddBox(root, light, new Vector3(0.55f, 0.1f, 0.05f), new Vector3(0f, 0.55f, -0.46f));
				AddBox(root, glow, new Vector3(0.35f, 0.08f, 0.04f), new Vector3(0f, -0.15f, -0.46f));
				break;

			case "missile":
				AddBox(root, mat, new Vector3(0.58f, 0.28f, 0.72f), new Vector3(0f, 0.08f, 0f));
				AddBox(root, dark, new Vector3(0.5f, 0.08f, 0.55f), new Vector3(0f, 0.24f, 0.05f));
				AddCylinder(root, dark, 0.08f, 0.72f, new Vector3(-0.16f, 0.22f, -0.12f), Vector3.Right * Mathf.Tau * 0.25f);
				AddCylinder(root, dark, 0.08f, 0.72f, new Vector3(0.16f, 0.22f, -0.12f), Vector3.Right * Mathf.Tau * 0.25f);
				AddCylinder(root, light, 0.09f, 0.08f, new Vector3(-0.16f, 0.22f, -0.48f), Vector3.Right * Mathf.Tau * 0.25f);
				AddCylinder(root, light, 0.09f, 0.08f, new Vector3(0.16f, 0.22f, -0.48f), Vector3.Right * Mathf.Tau * 0.25f);
				break;

			case "backpack":
				// Mount face at z≈0 (against torso); pack extends +Z (aft).
				AddBox(root, mat, new Vector3(0.72f, 0.78f, 0.38f), new Vector3(0f, 0.25f, 0.20f));
				AddBox(root, dark, new Vector3(0.55f, 0.25f, 0.25f), new Vector3(0f, 0.5f, 0.28f));
				AddSphere(root, glow, 0.14f, new Vector3(0f, 0.68f, 0.22f));
				AddCylinder(root, dark, 0.06f, 0.35f, new Vector3(-0.22f, 0.15f, 0.36f), Vector3.Right * Mathf.Tau * 0.25f);
				AddCylinder(root, dark, 0.06f, 0.35f, new Vector3(0.22f, 0.15f, 0.36f), Vector3.Right * Mathf.Tau * 0.25f);
				break;

			case "shroud":
				AddBox(root, mat, new Vector3(0.62f, 0.48f, 0.32f), new Vector3(0f, 0.2f, 0.18f));
				AddCylinder(root, dark, 0.18f, 0.35f, new Vector3(0f, 0.55f, 0.18f), Vector3.Zero);
				AddCylinder(root, glow, 0.1f, 0.12f, new Vector3(0f, 0.72f, 0.18f), Vector3.Zero);
				break;

			case "heatsink":
				AddBox(root, mat, new Vector3(0.85f, 0.18f, 0.28f), new Vector3(0f, 0.08f, 0.14f));
				for (var i = -2; i <= 2; i++)
				{
					AddBox(root, dark, new Vector3(0.08f, 0.48f, 0.3f), new Vector3(i * 0.16f, 0.34f, 0.18f));
					AddBox(root, light, new Vector3(0.03f, 0.42f, 0.24f), new Vector3(i * 0.16f, 0.36f, 0.22f));
				}
				break;

			case "gimbal":
				AddCylinder(root, mat, 0.26f, 0.32f, new Vector3(0f, 0.18f, 0f), Vector3.Zero);
				AddBox(root, dark, new Vector3(0.55f, 0.1f, 0.55f), new Vector3(0f, 0.42f, 0f));
				AddCylinder(root, light, 0.12f, 0.12f, new Vector3(0f, 0.5f, 0f), Vector3.Zero);
				break;

			case "shield":
				// Flat ballast plate flush to the back mount (was floating ~0.5m aft).
				AddBox(root, mat, new Vector3(0.95f, 1.05f, 0.12f), new Vector3(0f, 0.35f, 0.08f));
				AddBox(root, dark, new Vector3(0.75f, 0.85f, 0.06f), new Vector3(0f, 0.35f, 0.15f));
				AddBox(root, light, new Vector3(0.55f, 0.12f, 0.05f), new Vector3(0f, 0.62f, 0.19f));
				AddSphere(root, glow, 0.1f, new Vector3(0f, 0.35f, 0.2f));
				break;

			default:
				AddBox(root, mat, new Vector3(0.4f, 0.4f, 0.4f), Vector3.Zero);
				break;
		}

		return root;
	}

	private static StandardMaterial3D MakeMat(
		Color albedo,
		float metallic = 0.35f,
		float roughness = 0.55f,
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

	/// <summary>
	/// Articulated biped: Hip → Knee → Foot pivots the walk animator can drive.
	/// </summary>
	private static void BuildBipedLegs(Node3D root, Material mat, Material dark, Material light)
	{
		root.SetMeta("LegRig", "biped");
		AddBox(root, mat, new Vector3(1.15f, 0.22f, 0.7f), new Vector3(0f, 0.9f, 0f));
		AddBox(root, dark, new Vector3(0.9f, 0.1f, 0.5f), new Vector3(0f, 0.82f, 0.05f));
		AddBipedLeg(root, mat, dark, light, "Leg_L", -0.4f);
		AddBipedLeg(root, mat, dark, light, "Leg_R", 0.4f);
	}

	private static void AddBipedLeg(
		Node3D root, Material mat, Material dark, Material light, string name, float hipX)
	{
		const float thighLen = 0.45f;
		const float shinLen = 0.38f;

		var hip = new Node3D { Name = name, Position = new Vector3(hipX, 0.85f, 0f) };
		hip.SetMeta("RestRotation", Vector3.Zero);
		root.AddChild(hip);

		var thigh = MeshMat.Make(new BoxMesh { Size = new Vector3(0.3f, thighLen, 0.3f) }, mat,
			new Vector3(0f, -thighLen * 0.5f, 0f));
		thigh.Name = "Thigh";
		hip.AddChild(thigh);

		var knee = new Node3D { Name = "Knee", Position = new Vector3(0f, -thighLen, 0f) };
		knee.SetMeta("RestRotation", Vector3.Zero);
		hip.AddChild(knee);

		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.34f, 0.16f, 0.34f) }, light,
			new Vector3(0f, 0f, 0.02f)));

		var shin = MeshMat.Make(new BoxMesh { Size = new Vector3(0.28f, shinLen, 0.28f) }, dark,
			new Vector3(0f, -shinLen * 0.5f, 0f));
		shin.Name = "Shin";
		knee.AddChild(shin);

		var foot = MeshMat.Make(new BoxMesh { Size = new Vector3(0.45f, 0.14f, 0.58f) }, mat,
			new Vector3(0f, -shinLen - 0.07f, 0.06f));
		foot.Name = "Foot";
		knee.AddChild(foot);
	}

	/// <summary>
	/// Grounded two-link spider rig. Feet are authored on the ground plane first;
	/// upper/lower struts are then fitted between hull, knee, and foot.
	/// </summary>
	private static void BuildHexLegs(Node3D root, Material mat, Material dark)
	{
		root.SetMeta("LegRig", "hex");
		AddBox(root, mat, new Vector3(0.9f, 0.22f, 0.9f), new Vector3(0f, 0.55f, 0f));
		AddCylinder(root, dark, 0.28f, 0.18f, new Vector3(0f, 0.62f, 0f), Vector3.Zero);

		AddSpiderLeg(root, mat, dark, 0, new Vector3(-0.42f, 0.58f, -0.34f), new Vector3(-1.12f, 0.1f, -0.82f));
		AddSpiderLeg(root, mat, dark, 1, new Vector3(0.42f, 0.58f, -0.34f), new Vector3(1.12f, 0.1f, -0.82f));
		AddSpiderLeg(root, mat, dark, 2, new Vector3(-0.46f, 0.58f, 0f), new Vector3(-1.28f, 0.1f, 0f));
		AddSpiderLeg(root, mat, dark, 3, new Vector3(0.46f, 0.58f, 0f), new Vector3(1.28f, 0.1f, 0f));
		AddSpiderLeg(root, mat, dark, 4, new Vector3(-0.42f, 0.58f, 0.34f), new Vector3(-1.12f, 0.1f, 0.82f));
		AddSpiderLeg(root, mat, dark, 5, new Vector3(0.42f, 0.58f, 0.34f), new Vector3(1.12f, 0.1f, 0.82f));
	}

	private static void AddSpiderLeg(
		Node3D parent, Material mat, Material dark, int index, Vector3 hip, Vector3 foot)
	{
		var leg = new Node3D { Name = $"HexLeg_{index}" };
		leg.SetMeta("LegIndex", index);
		leg.SetMeta("Hip", hip);
		leg.SetMeta("RestFoot", foot);
		parent.AddChild(leg);

		var upper = MeshMat.Make(
			new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.09f, Height = 1f },
			mat);
		upper.Name = "Upper";
		leg.AddChild(upper);

		var lower = MeshMat.Make(
			new CylinderMesh { TopRadius = 0.065f, BottomRadius = 0.08f, Height = 1f },
			dark);
		lower.Name = "Lower";
		leg.AddChild(lower);

		var knee = MeshMat.Make(
			new SphereMesh { Radius = 0.1f, Height = 0.2f },
			dark);
		knee.Name = "Knee";
		leg.AddChild(knee);

		var footBall = MeshMat.Make(
			new SphereMesh { Radius = 0.1f, Height = 0.2f },
			dark);
		footBall.Name = "Foot";
		leg.AddChild(footBall);

		PoseSpiderLeg(leg, hip, foot);
	}

	private static void PoseSpiderLeg(Node3D leg, Vector3 hip, Vector3 foot)
	{
		var outward = new Vector3(foot.X - hip.X, 0f, foot.Z - hip.Z).Normalized();
		var knee = hip.Lerp(foot, 0.52f) + outward * 0.18f + Vector3.Up * 0.24f;
		PoseStrut(leg.GetNode<MeshInstance3D>("Upper"), hip, knee);
		PoseStrut(leg.GetNode<MeshInstance3D>("Lower"), knee, foot);
		leg.GetNode<MeshInstance3D>("Knee").Position = knee;
		leg.GetNode<MeshInstance3D>("Foot").Position = foot;
	}

	private static void PoseStrut(MeshInstance3D strut, Vector3 from, Vector3 to)
	{
		var delta = to - from;
		var length = Mathf.Max(0.001f, delta.Length());
		strut.Position = (from + to) * 0.5f;
		strut.Quaternion = new Quaternion(Vector3.Up, delta / length);
		strut.Scale = new Vector3(1f, length, 1f);
	}

	private static void AddWheel(Node3D parent, Material mat, Vector3 position)
	{
		AddCylinder(parent, mat, 0.16f, 0.18f, position, Vector3.Forward * Mathf.Tau * 0.25f);
	}

	private static void AddBox(Node3D parent, Material mat, Vector3 size, Vector3 position)
	{
		parent.AddChild(MeshMat.Make(new BoxMesh { Size = size }, mat, position));
	}

	private static void AddCylinder(Node3D parent, Material mat, float radius, float height, Vector3 position, Vector3 rotation)
	{
		parent.AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
			mat,
			position,
			rotation));
	}

	private static void AddSphere(Node3D parent, Material mat, float radius, Vector3 position)
	{
		parent.AddChild(MeshMat.Make(
			new SphereMesh { Radius = radius, Height = radius * 2f },
			mat,
			position));
	}
}
