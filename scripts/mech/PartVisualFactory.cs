using Godot;

namespace Mechanize;

public static class PartVisualFactory
{
	public static Node3D Create(PartData part, MechChassisClass chassisClass = MechChassisClass.Standard,
		bool encasedPowerCore = false)
	{
		if (chassisClass == MechChassisClass.Titan)
			return TitanPartVisualFactory.Create(part);

		var root = new Node3D { Name = $"Visual_{part.Id}" };
		// Hollow torsos tint via CockpitTorsoVisual.ApplyPart; these roles cover legs / head / weapons / core.
		// Hull = clean steel color + painted-metal scratches (no rust mottling).
		var mat = SurfaceLibrary.GetMechPlate(part.Tint);
		var dark = SurfaceLibrary.GetMech(SurfaceLibrary.Kind.SteelDark, part.Tint.Darkened(0.32f));
		var light = SurfaceLibrary.GetMech(SurfaceLibrary.Kind.Steel, part.Tint.Lightened(0.18f));
		var glow = SurfaceLibrary.Flat(
			part.Tint.Lightened(0.35f),
			metallic: 0.2f,
			roughness: 0.35f,
			emission: part.Tint.Lightened(0.2f),
			emissionEnergy: 1.1f);

		switch (part.VisualKind)
		{
			case "legs":
			case "legs_biped":
				BuildBipedLegs(root, mat, dark, light);
				break;

			case "legs_ash_coil":
				BuildAshCoilLegs(root, mat, dark, light, glow);
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

			case "torso_fleet":
			case "torso_brin_anvil":
			case "torso_ouro_thin":
			case "torso_lum_oracle":
			case "torso_ash_ashrib":
			case "torso_vel_ruff":
				CockpitHullRegistry.Attach(root, part);
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

			case "head_ash_whisker":
				BuildWhiskerHead(root, part, mat, dark, light, glow);
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
				if (encasedPowerCore)
					BuildEncasedCore(root, mat, dark, glow);
				else
				{
					AddCylinder(root, mat, 0.28f, 0.5f, new Vector3(0f, 0.28f, 0f), Vector3.Zero);
					AddCylinder(root, dark, 0.32f, 0.1f, new Vector3(0f, 0.08f, 0f), Vector3.Zero);
					AddSphere(root, glow, 0.16f, new Vector3(0f, 0.52f, 0f));
					AddBox(root, dark, new Vector3(0.55f, 0.08f, 0.55f), new Vector3(0f, 0.04f, 0f));
				}
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

			case "ash_stabilizer":
				BuildAshStabilizerTail(root, mat, dark, light, glow);
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

	/// <summary>Loads a Mech 2.0 hollow hull via <see cref="CockpitHullRegistry"/>.</summary>
	public static void AttachFleetTorsoScene(Node3D root, PartData part) =>
		CockpitHullRegistry.Attach(root, part);

	private static void BuildEncasedCore(Node3D root, Material mat, Material dark, Material glow)
	{
		// Recessed in the aft cavity — no overhead glow sphere in the viewport.
		AddCylinder(root, mat, 0.2f, 0.28f, new Vector3(0f, 0.12f, 0.04f), Vector3.Zero);
		AddBox(root, dark, new Vector3(0.42f, 0.07f, 0.42f), new Vector3(0f, 0.02f, 0.02f));
		AddSphere(root, glow, 0.06f, new Vector3(0f, 0.16f, 0.06f));
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
	/// Ashwhisk Coilstriders — digitigrade lean, claw-splay pads. Same Leg_L/R → Knee → Foot
	/// hierarchy so <see cref="MechLegAnimator"/> can drive the gait.
	/// </summary>
	private static void BuildAshCoilLegs(Node3D root, Material mat, Material dark, Material light, Material glow)
	{
		root.SetMeta("LegRig", "biped");
		AddBox(root, mat, new Vector3(0.95f, 0.18f, 0.58f), new Vector3(0f, 1.12f, 0.02f));
		AddBox(root, dark, new Vector3(0.72f, 0.1f, 0.42f), new Vector3(0f, 1.04f, 0.06f));
		AddBox(root, light, new Vector3(0.22f, 0.28f, 0.22f), new Vector3(-0.28f, 1.18f, 0.08f));
		AddBox(root, light, new Vector3(0.22f, 0.28f, 0.22f), new Vector3(0.28f, 1.18f, 0.08f));
		AddAshCoilLeg(root, mat, dark, light, glow, "Leg_L", -0.34f);
		AddAshCoilLeg(root, mat, dark, light, glow, "Leg_R", 0.34f);
	}

	/// <summary>
	/// Ashwhisk balance-fin stabilizer — backpack-slot tail with a soft Verlet chain.
	/// Socket is placed low on the hull by <see cref="MechAssembler"/> (tail dock, not dorsal pack).
	/// </summary>
	private static void BuildAshStabilizerTail(
		Node3D root, Material mat, Material dark, Material light, Material glow)
	{
		// Rigid dock plate against the hull.
		AddBox(root, dark, new Vector3(0.42f, 0.28f, 0.14f), new Vector3(0f, 0.06f, 0.05f));
		AddBox(root, light, new Vector3(0.3f, 0.18f, 0.07f), new Vector3(0f, 0.08f, 0.01f));
		AddCylinder(root, dark, 0.045f, 0.18f, new Vector3(-0.12f, 0.04f, 0.12f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(root, dark, 0.045f, 0.18f, new Vector3(0.12f, 0.04f, 0.12f), Vector3.Right * Mathf.Tau * 0.25f);

		var chain = new SoftTailChain
		{
			Name = "SoftTail",
			Position = new Vector3(0f, 0.04f, 0.16f),
			SegmentLength = 0.26f,
			Damping = 0.9f,
			StiffnessIterations = 4,
			MaxSag = 0.42f,
			Gravity = 5f,
			InertiaFromParent = 0.65f
		};
		AddTailSeg(chain, "Seg_0", mat, new Vector3(0.26f, 0.2f, 0.24f));
		AddTailSeg(chain, "Seg_1", dark, new Vector3(0.22f, 0.16f, 0.24f));
		AddTailSeg(chain, "Seg_2", mat, new Vector3(0.18f, 0.14f, 0.22f));
		AddTailSeg(chain, "Seg_3", light, new Vector3(0.14f, 0.12f, 0.2f));
		var tip = AddTailSeg(chain, "Seg_4", dark, new Vector3(0.12f, 0.1f, 0.16f));
		tip.AddChild(MeshMat.Make(new SphereMesh { Radius = 0.045f, Height = 0.09f }, glow,
			new Vector3(0f, 0f, 0.1f)));
		root.AddChild(chain);
	}

	private static Node3D AddTailSeg(Node3D chain, string name, Material mat, Vector3 size)
	{
		var seg = new Node3D { Name = name };
		chain.AddChild(seg);
		seg.AddChild(MeshMat.Make(new BoxMesh { Size = size }, mat));
		return seg;
	}

	private static void AddAshCoilLeg(
		Node3D root, Material mat, Material dark, Material light, Material glow,
		string name, float hipX)
	{
		const float thighLen = 0.52f;
		const float shinLen = 0.46f;

		var hip = new Node3D { Name = name, Position = new Vector3(hipX, 1.15f, 0.02f) };
		// Mild digigrade silhouette via mesh offsets — keep rest near-flat so pads stay level.
		hip.SetMeta("RestRotation", new Vector3(0.12f, 0f, hipX < 0f ? 0.03f : -0.03f));
		hip.Rotation = hip.GetMeta("RestRotation").AsVector3();
		root.AddChild(hip);

		var thigh = MeshMat.Make(new BoxMesh { Size = new Vector3(0.26f, thighLen, 0.34f) }, mat,
			new Vector3(0f, -thighLen * 0.5f, 0.06f));
		thigh.Name = "Thigh";
		hip.AddChild(thigh);
		hip.AddChild(MeshMat.Make(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = thighLen * 0.7f },
			light, new Vector3(0.12f, -thighLen * 0.45f, 0.03f), Vector3.Forward * Mathf.Tau * 0.25f));

		var knee = new Node3D { Name = "Knee", Position = new Vector3(0f, -thighLen, 0.1f) };
		knee.SetMeta("RestRotation", new Vector3(-0.28f, 0f, 0f));
		knee.Rotation = knee.GetMeta("RestRotation").AsVector3();
		hip.AddChild(knee);

		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.32f, 0.16f, 0.36f) }, light,
			new Vector3(0f, 0.02f, 0.02f)));
		knee.AddChild(MeshMat.Make(new SphereMesh { Radius = 0.08f, Height = 0.16f }, glow,
			new Vector3(0f, 0f, 0.1f)));

		var shin = MeshMat.Make(new BoxMesh { Size = new Vector3(0.24f, shinLen, 0.28f) }, dark,
			new Vector3(0f, -shinLen * 0.5f, -0.05f));
		shin.Name = "Shin";
		knee.AddChild(shin);
		knee.AddChild(MeshMat.Make(new BoxMesh { Size = new Vector3(0.12f, shinLen * 0.55f, 0.1f) }, light,
			new Vector3(0.1f, -shinLen * 0.4f, -0.01f)));

		// Pad stays locally unpitched; cancel the mild knee rest so soles read flat to the ground.
		const float kneeRestX = -0.28f;
		var footFlat = new Vector3(-kneeRestX - 0.12f, 0f, 0f); // undo knee + hip rest pitch
		var footY = -shinLen - 0.02f;
		var footZ = -0.1f;
		var foot = MeshMat.Make(new BoxMesh { Size = new Vector3(0.42f, 0.12f, 0.48f) }, mat,
			new Vector3(0f, footY, footZ));
		foot.Name = "Foot";
		foot.Rotation = footFlat;
		knee.AddChild(foot);

		var toeZ = footZ - 0.22f;
		var toe = new Node3D { Name = "Toes", Position = new Vector3(0f, footY, 0f), Rotation = footFlat };
		knee.AddChild(toe);
		AddBox(toe, dark, new Vector3(0.1f, 0.08f, 0.26f), new Vector3(-0.14f, -0.01f, toeZ));
		AddBox(toe, dark, new Vector3(0.11f, 0.08f, 0.3f), new Vector3(0f, -0.01f, toeZ - 0.02f));
		AddBox(toe, dark, new Vector3(0.1f, 0.08f, 0.26f), new Vector3(0.14f, -0.01f, toeZ));
		AddBox(toe, light, new Vector3(0.06f, 0.05f, 0.1f), new Vector3(-0.14f, -0.03f, toeZ - 0.14f));
		AddBox(toe, light, new Vector3(0.06f, 0.05f, 0.12f), new Vector3(0f, -0.03f, toeZ - 0.16f));
		AddBox(toe, light, new Vector3(0.06f, 0.05f, 0.1f), new Vector3(0.14f, -0.03f, toeZ - 0.14f));
	}

	/// <summary>Whisker Array — sensor dome with ear-fin plates and a single primary optic.</summary>
	private static void BuildWhiskerHead(
		Node3D root, PartData part, Material mat, Material dark, Material light, Material glow)
	{
		var s = part.VisualScale;
		AddSphere(root, mat, 0.28f * Mathf.Max(s.X, s.Y), new Vector3(0f, 0.22f, 0f));
		AddBox(root, mat, new Vector3(0.48f, 0.36f, 0.46f) * s, new Vector3(0f, 0.18f, 0.02f));
		AddBox(root, dark, new Vector3(0.36f, 0.12f, 0.2f), new Vector3(0f, 0.36f, -0.02f));
		// Primary mono-optic (inspiration: single glowing sensor eye).
		AddSphere(root, glow, 0.1f, new Vector3(0f, 0.2f, -0.26f * s.Z));
		AddBox(root, dark, new Vector3(0.22f, 0.16f, 0.1f), new Vector3(0f, 0.2f, -0.22f * s.Z));
		// Ear-fin antenna plates.
		AddBox(root, light, new Vector3(0.08f, 0.32f, 0.18f), new Vector3(-0.22f, 0.42f, 0.06f));
		AddBox(root, light, new Vector3(0.08f, 0.32f, 0.18f), new Vector3(0.22f, 0.42f, 0.06f));
		AddBox(root, dark, new Vector3(0.05f, 0.22f, 0.12f), new Vector3(-0.26f, 0.52f, 0.02f));
		AddBox(root, dark, new Vector3(0.05f, 0.22f, 0.12f), new Vector3(0.26f, 0.52f, 0.02f));
		// Fine whisker sensor rods.
		AddCylinder(root, dark, 0.015f, 0.28f, new Vector3(-0.18f, 0.12f, -0.2f),
			new Vector3(0.4f, 0f, 0.55f));
		AddCylinder(root, dark, 0.015f, 0.28f, new Vector3(0.18f, 0.12f, -0.2f),
			new Vector3(0.4f, 0f, -0.55f));
		AddCylinder(root, glow, 0.012f, 0.18f, new Vector3(-0.12f, 0.28f, -0.12f),
			new Vector3(-0.3f, 0f, 0.4f));
		AddCylinder(root, glow, 0.012f, 0.18f, new Vector3(0.12f, 0.28f, -0.12f),
			new Vector3(-0.3f, 0f, -0.4f));
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
