using Godot;

namespace Mechanize;

/// <summary>
/// Boss-tier part silhouettes for Titan-class chassis. Same local proportions as
/// <see cref="PartVisualFactory"/> so the MAP socket rig still fits after 4× scale,
/// but with denser armor, bracing, and threat lighting so they read as Titans —
/// not just enlarged field MAPs.
/// </summary>
public static class TitanPartVisualFactory
{
	public static Node3D Create(PartData part)
	{
		var root = new Node3D { Name = $"TitanVisual_{part.Id}" };
		var mat = MakeMat(part.Tint, metallic: 0.42f, roughness: 0.48f);
		var dark = MakeMat(part.Tint.Darkened(0.38f), metallic: 0.55f, roughness: 0.42f);
		var light = MakeMat(part.Tint.Lightened(0.16f), metallic: 0.32f, roughness: 0.4f);
		var plate = MakeMat(part.Tint.Darkened(0.18f), metallic: 0.5f, roughness: 0.55f);
		var glow = MakeMat(
			part.Tint.Lightened(0.4f),
			metallic: 0.15f,
			roughness: 0.28f,
			emission: part.Tint.Lightened(0.25f),
			emissionEnergy: 1.55f);
		var threat = MakeMat(
			new Color(1f, 0.35f, 0.12f),
			metallic: 0.1f,
			roughness: 0.3f,
			emission: new Color(1f, 0.4f, 0.1f),
			emissionEnergy: 1.8f);

		switch (part.VisualKind)
		{
			case "legs":
			case "legs_biped":
				BuildBipedLegs(root, mat, dark, light, plate, glow);
				break;
			case "legs_hex":
				BuildHexLegs(root, mat, dark, light, plate, glow);
				break;
			case "legs_tracks":
				BuildTracks(root, mat, dark, light, plate, glow, threat);
				break;
			case "torso":
				BuildTorso(root, part, mat, dark, light, plate, glow, threat);
				break;
			case "head":
				BuildHead(root, part, mat, dark, light, plate, glow, threat);
				break;
			case "core":
				BuildCore(root, mat, dark, light, glow, threat);
				break;
			case "cannon":
				BuildCannon(root, mat, dark, light, plate, glow);
				break;
			case "rifle":
				BuildRifle(root, mat, dark, light, plate, glow);
				break;
			case "energy":
				BuildEnergy(root, mat, dark, light, glow, threat);
				break;
			case "cleaver":
				BuildCleaver(root, mat, dark, light, glow);
				break;
			case "held_shield":
				BuildHeldShield(root, mat, dark, light, plate, glow);
				break;
			case "missile":
				BuildMissile(root, mat, dark, light, plate, glow);
				break;
			case "backpack":
				BuildBackpack(root, mat, dark, light, plate, glow);
				break;
			case "shroud":
				BuildShroud(root, mat, dark, light, glow, threat);
				break;
			case "heatsink":
				BuildHeatsink(root, mat, dark, light, plate, glow);
				break;
			case "gimbal":
				BuildGimbal(root, mat, dark, light, glow);
				break;
			case "shield":
				BuildShield(root, mat, dark, light, plate, glow);
				break;
			default:
				AddBox(root, mat, new Vector3(0.55f, 0.55f, 0.55f), Vector3.Zero);
				AddBox(root, dark, new Vector3(0.42f, 0.12f, 0.42f), new Vector3(0f, 0.28f, 0f));
				AddSphere(root, glow, 0.12f, new Vector3(0f, 0.05f, 0.2f));
				break;
		}

		return root;
	}

	private static void BuildBipedLegs(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		root.SetMeta("LegRig", "biped");
		AddBox(root, mat, new Vector3(1.25f, 0.28f, 0.82f), new Vector3(0f, 0.92f, 0f));
		AddBox(root, dark, new Vector3(1.05f, 0.12f, 0.62f), new Vector3(0f, 0.82f, 0.04f));
		AddBox(root, plate, new Vector3(1.35f, 0.1f, 0.55f), new Vector3(0f, 0.78f, -0.08f));
		AddBox(root, glow, new Vector3(0.35f, 0.06f, 0.08f), new Vector3(0f, 0.88f, -0.38f));
		AddTitanBipedLeg(root, mat, dark, light, plate, "Leg_L", -0.42f);
		AddTitanBipedLeg(root, mat, dark, light, plate, "Leg_R", 0.42f);
	}

	private static void AddTitanBipedLeg(
		Node3D root, Material mat, Material dark, Material light, Material plate, string name, float hipX)
	{
		const float thighLen = 0.5f;
		const float shinLen = 0.42f;
		var side = Mathf.Sign(hipX);

		var hip = new Node3D { Name = name, Position = new Vector3(hipX, 0.88f, 0f) };
		hip.SetMeta("RestRotation", Vector3.Zero);
		root.AddChild(hip);

		hip.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.38f, thighLen, 0.38f) }, mat,
			new Vector3(0f, -thighLen * 0.5f, 0f)));
		hip.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.44f, 0.18f, 0.22f) }, plate,
			new Vector3(0f, -0.12f, -0.14f)));
		hip.AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.42f },
			dark,
			new Vector3(0f, -0.28f, 0.16f),
			Vector3.Right * 0.35f));

		var knee = new Node3D { Name = "Knee", Position = new Vector3(0f, -thighLen, 0f) };
		knee.SetMeta("RestRotation", Vector3.Zero);
		hip.AddChild(knee);

		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.42f, 0.2f, 0.42f) }, light,
			new Vector3(0f, 0f, 0.03f)));
		knee.AddChild(MeshMat.Make(
			new SphereMesh { Radius = 0.1f, Height = 0.2f },
			dark,
			new Vector3(0f, 0f, 0.18f)));

		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.34f, shinLen, 0.34f) }, dark,
			new Vector3(0f, -shinLen * 0.5f, 0f)));
		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.4f, 0.28f, 0.12f) }, plate,
			new Vector3(0f, -shinLen * 0.45f, -0.18f)));

		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.52f, 0.16f, 0.68f) }, mat,
			new Vector3(0f, -shinLen - 0.06f, 0.08f)));
		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.38f, 0.08f, 0.22f) }, light,
			new Vector3(0f, -shinLen - 0.04f, 0.32f)));
		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.18f, 0.1f, 0.28f) }, dark,
			new Vector3(-side * 0.2f, -shinLen - 0.07f, -0.18f)));
	}

	private static void BuildHexLegs(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		root.SetMeta("LegRig", "hex");
		AddBox(root, mat, new Vector3(1.05f, 0.28f, 1.05f), new Vector3(0f, 0.58f, 0f));
		AddBox(root, dark, new Vector3(0.78f, 0.12f, 0.78f), new Vector3(0f, 0.68f, 0f));
		AddCylinder(root, plate, 0.34f, 0.2f, new Vector3(0f, 0.72f, 0f), Vector3.Zero);
		AddSphere(root, glow, 0.14f, new Vector3(0f, 0.82f, 0f));

		AddTitanSpiderLeg(root, mat, dark, light, 0, new Vector3(-0.48f, 0.62f, -0.4f), new Vector3(-1.22f, 0.12f, -0.9f));
		AddTitanSpiderLeg(root, mat, dark, light, 1, new Vector3(0.48f, 0.62f, -0.4f), new Vector3(1.22f, 0.12f, -0.9f));
		AddTitanSpiderLeg(root, mat, dark, light, 2, new Vector3(-0.52f, 0.62f, 0f), new Vector3(-1.38f, 0.12f, 0f));
		AddTitanSpiderLeg(root, mat, dark, light, 3, new Vector3(0.52f, 0.62f, 0f), new Vector3(1.38f, 0.12f, 0f));
		AddTitanSpiderLeg(root, mat, dark, light, 4, new Vector3(-0.48f, 0.62f, 0.4f), new Vector3(-1.22f, 0.12f, 0.9f));
		AddTitanSpiderLeg(root, mat, dark, light, 5, new Vector3(0.48f, 0.62f, 0.4f), new Vector3(1.22f, 0.12f, 0.9f));
	}

	private static void AddTitanSpiderLeg(
		Node3D parent, Material mat, Material dark, Material light, int index, Vector3 hip, Vector3 foot)
	{
		var leg = new Node3D { Name = $"HexLeg_{index}" };
		leg.SetMeta("LegIndex", index);
		leg.SetMeta("Hip", hip);
		leg.SetMeta("RestFoot", foot);
		parent.AddChild(leg);

		var upper = MeshMat.Make(
			new CylinderMesh { TopRadius = 0.095f, BottomRadius = 0.115f, Height = 1f },
			mat);
		upper.Name = "Upper";
		leg.AddChild(upper);

		var lower = MeshMat.Make(
			new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.095f, Height = 1f },
			dark);
		lower.Name = "Lower";
		leg.AddChild(lower);

		var knee = MeshMat.Make(
			new SphereMesh { Radius = 0.13f, Height = 0.26f },
			light);
		knee.Name = "Knee";
		leg.AddChild(knee);

		var footBall = MeshMat.Make(
			new SphereMesh { Radius = 0.12f, Height = 0.24f },
			light);
		footBall.Name = "Foot";
		leg.AddChild(footBall);

		PoseTitanSpiderLeg(leg, hip, foot);
	}

	private static void PoseTitanSpiderLeg(Node3D leg, Vector3 hip, Vector3 foot)
	{
		var outward = new Vector3(foot.X - hip.X, 0f, foot.Z - hip.Z).Normalized();
		var knee = hip.Lerp(foot, 0.52f) + outward * 0.2f + Vector3.Up * 0.28f;
		PoseTitanStrut(leg.GetNode<MeshInstance3D>("Upper"), hip, knee);
		PoseTitanStrut(leg.GetNode<MeshInstance3D>("Lower"), knee, foot);
		leg.GetNode<MeshInstance3D>("Knee").Position = knee;
		leg.GetNode<MeshInstance3D>("Foot").Position = foot;
	}

	private static void PoseTitanStrut(MeshInstance3D strut, Vector3 from, Vector3 to)
	{
		var delta = to - from;
		var length = Mathf.Max(0.001f, delta.Length());
		strut.Position = (from + to) * 0.5f;
		strut.Quaternion = new Quaternion(Vector3.Up, delta / length);
		strut.Scale = new Vector3(1f, length, 1f);
	}

	private static void BuildTracks(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow, Material threat)
	{
		AddBox(root, mat, new Vector3(1.55f, 0.38f, 1.15f), new Vector3(0f, 0.45f, 0f));
		AddBox(root, dark, new Vector3(1.2f, 0.14f, 0.85f), new Vector3(0f, 0.62f, 0f));
		AddBox(root, plate, new Vector3(1.1f, 0.22f, 0.35f), new Vector3(0f, 0.52f, -0.45f));
		AddBox(root, glow, new Vector3(0.55f, 0.08f, 0.06f), new Vector3(0f, 0.58f, -0.58f));
		AddBox(root, threat, new Vector3(0.12f, 0.08f, 0.08f), new Vector3(-0.55f, 0.7f, 0.35f));
		AddBox(root, threat, new Vector3(0.12f, 0.08f, 0.08f), new Vector3(0.55f, 0.7f, 0.35f));

		foreach (var side in new[] { -1f, 1f })
		{
			var x = side * 0.72f;
			AddBox(root, dark, new Vector3(0.38f, 0.55f, 1.45f), new Vector3(x, 0.3f, 0f));
			AddBox(root, plate, new Vector3(0.12f, 0.42f, 1.2f), new Vector3(x + side * 0.18f, 0.32f, 0f));
			AddWheel(root, light, new Vector3(x, 0.16f, -0.52f));
			AddWheel(root, light, new Vector3(x, 0.16f, -0.18f));
			AddWheel(root, light, new Vector3(x, 0.16f, 0.18f));
			AddWheel(root, light, new Vector3(x, 0.16f, 0.52f));
			AddBox(root, mat, new Vector3(0.22f, 0.1f, 1.5f), new Vector3(x, 0.05f, 0f));
		}
	}

	private static void BuildTorso(
		Node3D root, PartData part, Material mat, Material dark, Material light, Material plate, Material glow, Material threat)
	{
		var s = part.VisualScale;
		// Main hull — keep same footprint as MAP torso for socket fit.
		AddBox(root, mat, new Vector3(1.4f, 1.05f, 1.0f) * s, new Vector3(0f, 0.55f, 0f));
		// Collar / neck ring
		AddBox(root, dark, new Vector3(1.2f, 0.2f, 0.9f) * new Vector3(s.X, 1f, s.Z), new Vector3(0f, 1.0f, 0.04f));
		AddBox(root, plate, new Vector3(0.95f, 0.1f, 0.7f), new Vector3(0f, 1.12f, 0.02f));
		// Layered chest armor
		AddBox(root, plate, new Vector3(1.05f, 0.55f, 0.22f), new Vector3(0f, 0.58f, -0.48f));
		AddBox(root, dark, new Vector3(0.7f, 0.4f, 0.14f), new Vector3(0f, 0.55f, -0.58f));
		AddBox(root, glow, new Vector3(0.45f, 0.1f, 0.06f), new Vector3(0f, 0.62f, -0.66f));
		AddBox(root, glow, new Vector3(0.28f, 0.06f, 0.05f), new Vector3(0f, 0.48f, -0.66f));
		// Heavy pauldrons
		AddBox(root, light, new Vector3(0.42f, 0.42f, 0.62f), new Vector3(-0.82f, 0.88f, 0.02f));
		AddBox(root, light, new Vector3(0.42f, 0.42f, 0.62f), new Vector3(0.82f, 0.88f, 0.02f));
		AddBox(root, plate, new Vector3(0.18f, 0.35f, 0.5f), new Vector3(-1.0f, 0.9f, 0f));
		AddBox(root, plate, new Vector3(0.18f, 0.35f, 0.5f), new Vector3(1.0f, 0.9f, 0f));
		// Cockpit tower
		AddBox(root, mat, new Vector3(0.72f, 0.38f, 0.72f), new Vector3(0f, 1.22f, 0f));
		AddBox(root, dark, new Vector3(0.5f, 0.12f, 0.35f), new Vector3(0f, 1.38f, -0.12f));
		AddBox(root, threat, new Vector3(0.1f, 0.08f, 0.08f), new Vector3(-0.28f, 1.4f, -0.28f));
		AddBox(root, threat, new Vector3(0.1f, 0.08f, 0.08f), new Vector3(0.28f, 1.4f, -0.28f));
		// Side radiator gills
		for (var i = 0; i < 3; i++)
		{
			var y = 0.35f + i * 0.18f;
			AddBox(root, dark, new Vector3(0.08f, 0.12f, 0.55f), new Vector3(-0.72f, y, 0.1f));
			AddBox(root, dark, new Vector3(0.08f, 0.12f, 0.55f), new Vector3(0.72f, y, 0.1f));
		}
	}

	private static void BuildHead(
		Node3D root, PartData part, Material mat, Material dark, Material light, Material plate, Material glow, Material threat)
	{
		var s = part.VisualScale;
		AddBox(root, mat, new Vector3(0.62f, 0.48f, 0.58f) * s, new Vector3(0f, 0.22f, 0f));
		AddBox(root, plate, new Vector3(0.7f, 0.14f, 0.4f), new Vector3(0f, 0.42f, -0.02f));
		AddBox(root, dark, new Vector3(0.48f, 0.16f, 0.22f), new Vector3(0f, 0.26f, -0.3f));
		// Sensor array
		AddSphere(root, glow, 0.09f, new Vector3(-0.14f, 0.24f, -0.28f));
		AddSphere(root, glow, 0.09f, new Vector3(0.14f, 0.24f, -0.28f));
		AddBox(root, threat, new Vector3(0.08f, 0.06f, 0.06f), new Vector3(0f, 0.34f, -0.32f));
		// Antenna mast + side dish
		AddCylinder(root, dark, 0.035f, 0.32f, new Vector3(0.24f, 0.48f, 0.05f), Vector3.Zero);
		AddCylinder(root, light, 0.08f, 0.04f, new Vector3(0.24f, 0.62f, 0.05f), Vector3.Zero);
		AddBox(root, plate, new Vector3(0.18f, 0.22f, 0.08f), new Vector3(-0.32f, 0.28f, 0.05f));
		AddBox(root, dark, new Vector3(0.12f, 0.08f, 0.2f), new Vector3(0f, 0.12f, 0.22f));
	}

	private static void BuildCore(
		Node3D root, Material mat, Material dark, Material light, Material glow, Material threat)
	{
		AddCylinder(root, mat, 0.32f, 0.55f, new Vector3(0f, 0.3f, 0f), Vector3.Zero);
		AddCylinder(root, dark, 0.38f, 0.12f, new Vector3(0f, 0.08f, 0f), Vector3.Zero);
		AddCylinder(root, dark, 0.36f, 0.1f, new Vector3(0f, 0.52f, 0f), Vector3.Zero);
		AddSphere(root, glow, 0.2f, new Vector3(0f, 0.55f, 0f));
		AddSphere(root, threat, 0.08f, new Vector3(0f, 0.55f, 0f));
		AddBox(root, dark, new Vector3(0.65f, 0.1f, 0.65f), new Vector3(0f, 0.04f, 0f));
		AddBox(root, light, new Vector3(0.2f, 0.35f, 0.08f), new Vector3(-0.28f, 0.3f, 0.28f));
		AddBox(root, light, new Vector3(0.2f, 0.35f, 0.08f), new Vector3(0.28f, 0.3f, 0.28f));
		AddBox(root, glow, new Vector3(0.08f, 0.2f, 0.04f), new Vector3(0f, 0.3f, 0.34f));
	}

	private static void BuildCannon(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(0.48f, 0.48f, 0.95f), new Vector3(0f, 0f, -0.12f));
		AddBox(root, plate, new Vector3(0.55f, 0.2f, 0.55f), new Vector3(0f, 0.28f, 0f));
		AddBox(root, dark, new Vector3(0.35f, 0.22f, 0.4f), new Vector3(0f, 0.22f, 0.12f));
		AddCylinder(root, dark, 0.16f, 1.2f, new Vector3(0f, 0f, -0.9f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(root, light, 0.2f, 0.16f, new Vector3(0f, 0f, -1.45f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(root, mat, 0.14f, 0.2f, new Vector3(0f, 0f, -1.58f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(root, glow, 0.07f, 0.1f, new Vector3(0f, 0f, -1.68f), Vector3.Right * Mathf.Tau * 0.25f);
		// Recoil braces
		AddBox(root, dark, new Vector3(0.1f, 0.12f, 0.55f), new Vector3(-0.28f, 0.05f, -0.35f));
		AddBox(root, dark, new Vector3(0.1f, 0.12f, 0.55f), new Vector3(0.28f, 0.05f, -0.35f));
		AddBox(root, plate, new Vector3(0.22f, 0.35f, 0.18f), new Vector3(0f, -0.28f, 0.15f));
	}

	private static void BuildRifle(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(0.24f, 0.24f, 1.25f), new Vector3(0f, 0f, -0.28f));
		AddBox(root, plate, new Vector3(0.3f, 0.14f, 0.55f), new Vector3(0f, 0.14f, -0.1f));
		AddBox(root, dark, new Vector3(0.18f, 0.28f, 0.35f), new Vector3(0f, 0.14f, 0.28f));
		AddBox(root, light, new Vector3(0.12f, 0.1f, 0.25f), new Vector3(0f, 0.18f, -0.2f));
		AddBox(root, dark, new Vector3(0.16f, 0.16f, 0.18f), new Vector3(0f, 0f, -0.95f));
		AddCylinder(root, dark, 0.06f, 0.35f, new Vector3(0f, 0f, -1.15f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(root, glow, 0.035f, 0.08f, new Vector3(0f, 0f, -1.32f), Vector3.Right * Mathf.Tau * 0.25f);
		AddBox(root, plate, new Vector3(0.08f, 0.22f, 0.35f), new Vector3(0.16f, 0f, -0.4f));
		AddBox(root, dark, new Vector3(0.1f, 0.2f, 0.15f), new Vector3(0f, -0.18f, 0.2f));
	}

	private static void BuildEnergy(
		Node3D root, Material mat, Material dark, Material light, Material glow, Material threat)
	{
		AddBox(root, mat, new Vector3(0.32f, 0.32f, 0.85f), new Vector3(0f, 0f, -0.12f));
		AddBox(root, dark, new Vector3(0.26f, 0.18f, 0.35f), new Vector3(0f, 0.2f, 0.12f));
		AddCylinder(root, light, 0.12f, 0.42f, new Vector3(0f, 0f, -0.6f), Vector3.Right * Mathf.Tau * 0.25f);
		AddSphere(root, glow, 0.22f, new Vector3(0f, 0f, -0.9f));
		AddSphere(root, threat, 0.1f, new Vector3(0f, 0f, -0.9f));
		AddBox(root, dark, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(-0.2f, 0.05f, -0.2f));
		AddBox(root, dark, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(0.2f, 0.05f, -0.2f));
		AddBox(root, glow, new Vector3(0.08f, 0.08f, 0.35f), new Vector3(0f, 0.22f, -0.35f));
	}

	private static void BuildCleaver(
		Node3D root, Material mat, Material dark, Material light, Material glow)
	{
		AddBox(root, mat, new Vector3(0.28f, 0.34f, 0.62f), new Vector3(0f, 0f, 0.08f));
		AddBox(root, dark, new Vector3(0.16f, 0.22f, 0.4f), new Vector3(0f, 0.1f, 0.25f));
		AddBox(root, light, new Vector3(0.1f, 0.65f, 1.2f), new Vector3(0.03f, 0.06f, -0.58f));
		AddBox(root, glow, new Vector3(0.05f, 0.5f, 1.05f), new Vector3(0.08f, 0.06f, -0.62f));
		AddBox(root, dark, new Vector3(0.14f, 0.14f, 0.25f), new Vector3(0f, -0.18f, 0.15f));
		AddBox(root, mat, new Vector3(0.18f, 0.08f, 0.35f), new Vector3(0f, 0.22f, -0.2f));
	}

	private static void BuildHeldShield(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(1.05f, 1.25f, 0.14f), new Vector3(0f, 0.15f, -0.35f));
		AddBox(root, dark, new Vector3(0.88f, 1.05f, 0.08f), new Vector3(0f, 0.15f, -0.44f));
		AddBox(root, plate, new Vector3(0.7f, 0.14f, 0.06f), new Vector3(0f, 0.6f, -0.5f));
		AddBox(root, plate, new Vector3(0.7f, 0.14f, 0.06f), new Vector3(0f, -0.25f, -0.5f));
		AddBox(root, light, new Vector3(0.55f, 0.1f, 0.05f), new Vector3(0f, 0.55f, -0.5f));
		AddBox(root, glow, new Vector3(0.4f, 0.1f, 0.05f), new Vector3(0f, -0.15f, -0.5f));
		AddBox(root, dark, new Vector3(0.2f, 0.35f, 0.25f), new Vector3(0f, 0.15f, -0.15f));
		AddSphere(root, glow, 0.1f, new Vector3(0f, 0.15f, -0.52f));
	}

	private static void BuildMissile(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(0.68f, 0.35f, 0.82f), new Vector3(0f, 0.1f, 0f));
		AddBox(root, plate, new Vector3(0.58f, 0.12f, 0.65f), new Vector3(0f, 0.28f, 0.05f));
		AddBox(root, dark, new Vector3(0.5f, 0.08f, 0.55f), new Vector3(0f, 0.34f, 0.05f));
		foreach (var x in new[] { -0.2f, 0f, 0.2f })
		{
			AddCylinder(root, dark, 0.09f, 0.78f, new Vector3(x, 0.26f, -0.1f), Vector3.Right * Mathf.Tau * 0.25f);
			AddCylinder(root, light, 0.1f, 0.1f, new Vector3(x, 0.26f, -0.5f), Vector3.Right * Mathf.Tau * 0.25f);
			AddCylinder(root, glow, 0.04f, 0.06f, new Vector3(x, 0.26f, -0.56f), Vector3.Right * Mathf.Tau * 0.25f);
		}
		AddBox(root, mat, new Vector3(0.25f, 0.25f, 0.3f), new Vector3(0f, -0.05f, 0.25f));
	}

	private static void BuildBackpack(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(0.85f, 0.9f, 0.45f), new Vector3(0f, 0.28f, 0.22f));
		AddBox(root, dark, new Vector3(0.65f, 0.3f, 0.3f), new Vector3(0f, 0.58f, 0.3f));
		AddBox(root, plate, new Vector3(0.55f, 0.5f, 0.12f), new Vector3(0f, 0.25f, 0.42f));
		AddSphere(root, glow, 0.16f, new Vector3(0f, 0.75f, 0.24f));
		AddCylinder(root, dark, 0.08f, 0.42f, new Vector3(-0.26f, 0.15f, 0.4f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(root, dark, 0.08f, 0.42f, new Vector3(0.26f, 0.15f, 0.4f), Vector3.Right * Mathf.Tau * 0.25f);
		AddBox(root, light, new Vector3(0.15f, 0.55f, 0.2f), new Vector3(-0.4f, 0.28f, 0.18f));
		AddBox(root, light, new Vector3(0.15f, 0.55f, 0.2f), new Vector3(0.4f, 0.28f, 0.18f));
		AddBox(root, glow, new Vector3(0.2f, 0.08f, 0.08f), new Vector3(0f, 0.45f, 0.48f));
	}

	private static void BuildShroud(
		Node3D root, Material mat, Material dark, Material light, Material glow, Material threat)
	{
		AddBox(root, mat, new Vector3(0.72f, 0.55f, 0.38f), new Vector3(0f, 0.22f, 0.2f));
		AddCylinder(root, dark, 0.22f, 0.4f, new Vector3(0f, 0.58f, 0.2f), Vector3.Zero);
		AddCylinder(root, glow, 0.12f, 0.14f, new Vector3(0f, 0.78f, 0.2f), Vector3.Zero);
		AddSphere(root, threat, 0.07f, new Vector3(0f, 0.78f, 0.2f));
		AddBox(root, light, new Vector3(0.5f, 0.08f, 0.25f), new Vector3(0f, 0.08f, 0.28f));
		AddBox(root, dark, new Vector3(0.15f, 0.35f, 0.15f), new Vector3(-0.3f, 0.3f, 0.32f));
		AddBox(root, dark, new Vector3(0.15f, 0.35f, 0.15f), new Vector3(0.3f, 0.3f, 0.32f));
	}

	private static void BuildHeatsink(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(0.95f, 0.22f, 0.35f), new Vector3(0f, 0.1f, 0.16f));
		AddBox(root, plate, new Vector3(0.85f, 0.08f, 0.28f), new Vector3(0f, 0.2f, 0.18f));
		for (var i = -3; i <= 3; i++)
		{
			AddBox(root, dark, new Vector3(0.08f, 0.55f, 0.32f), new Vector3(i * 0.13f, 0.4f, 0.2f));
			AddBox(root, light, new Vector3(0.03f, 0.48f, 0.26f), new Vector3(i * 0.13f, 0.42f, 0.24f));
		}
		AddBox(root, glow, new Vector3(0.55f, 0.06f, 0.08f), new Vector3(0f, 0.12f, 0.34f));
	}

	private static void BuildGimbal(
		Node3D root, Material mat, Material dark, Material light, Material glow)
	{
		AddCylinder(root, mat, 0.3f, 0.38f, new Vector3(0f, 0.2f, 0f), Vector3.Zero);
		AddBox(root, dark, new Vector3(0.65f, 0.12f, 0.65f), new Vector3(0f, 0.45f, 0f));
		AddCylinder(root, light, 0.14f, 0.14f, new Vector3(0f, 0.55f, 0f), Vector3.Zero);
		AddSphere(root, glow, 0.1f, new Vector3(0f, 0.62f, 0f));
		AddBox(root, dark, new Vector3(0.12f, 0.25f, 0.4f), new Vector3(-0.28f, 0.25f, 0f));
		AddBox(root, dark, new Vector3(0.12f, 0.25f, 0.4f), new Vector3(0.28f, 0.25f, 0f));
	}

	private static void BuildShield(
		Node3D root, Material mat, Material dark, Material light, Material plate, Material glow)
	{
		AddBox(root, mat, new Vector3(1.1f, 1.2f, 0.14f), new Vector3(0f, 0.38f, 0.08f));
		AddBox(root, dark, new Vector3(0.9f, 1.0f, 0.08f), new Vector3(0f, 0.38f, 0.16f));
		AddBox(root, plate, new Vector3(0.7f, 0.14f, 0.06f), new Vector3(0f, 0.75f, 0.2f));
		AddBox(root, light, new Vector3(0.6f, 0.12f, 0.05f), new Vector3(0f, 0.7f, 0.22f));
		AddSphere(root, glow, 0.12f, new Vector3(0f, 0.38f, 0.22f));
		AddBox(root, dark, new Vector3(0.2f, 0.45f, 0.12f), new Vector3(-0.45f, 0.38f, 0.12f));
		AddBox(root, dark, new Vector3(0.2f, 0.45f, 0.12f), new Vector3(0.45f, 0.38f, 0.12f));
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

	private static void AddWheel(Node3D parent, Material mat, Vector3 position)
	{
		AddCylinder(parent, mat, 0.18f, 0.2f, position, Vector3.Forward * Mathf.Tau * 0.25f);
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
