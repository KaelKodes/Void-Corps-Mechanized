using Godot;

namespace Mechanize;

/// <summary>
/// Authored Trinova Fleet Intermediate hull + cockpit interior (see torso_tri_fleet.tscn).
/// Runtime tint only — geometry lives in the scene for editor iteration.
/// </summary>
public partial class FleetTorsoVisual : Node3D
{
	private static readonly string[] ExteriorMeshPaths =
	[
		"Exterior/HullRear",
		"Exterior/SideWallL/FrameRear",
		"Exterior/SideWallL/FrameLowerFwd",
		"Exterior/SideWallR/FrameRear",
		"Exterior/SideWallR/FrameLowerFwd",
		"Exterior/HullFloor",
		"Exterior/HullCeiling",
		"Exterior/Collar",
		"Exterior/NeckPedestal",
		"Exterior/PauldronL",
		"Exterior/PauldronR"
	];

	private static readonly string[] FrameMeshPaths =
	[
		"ViewFrame/PillarL",
		"ViewFrame/PillarR",
		"ViewFrame/LintelTop",
		"ViewFrame/RollBar",
		"ViewFrame/RollMountL",
		"ViewFrame/RollMountR",
		"Exterior/SideWallL/Brace",
		"Exterior/SideWallR/Brace",
		"CockpitInterior/Dashboard/DeckShelf",
		"CockpitInterior/Dashboard/CenterStick/Base",
		"CockpitInterior/Dashboard/CenterStick/Handle",
		"CockpitInterior/SideRailL",
		"CockpitInterior/SideRailR"
	];

	private static readonly string[] GlassMeshPaths =
	[
		"ViewPanel",
		"Exterior/SideWallL/GlassWedge",
		"Exterior/SideWallR/GlassWedge"
	];

	private static readonly string[] AccentMeshPaths =
	[
		"Exterior/AccentL",
		"Exterior/AccentR",
		"ViewFrame/RollMountL",
		"ViewFrame/RollMountR",
		"CockpitInterior/Dashboard/CenterStick/Grip"
	];

	public void ApplyPart(PartData part)
	{
		var mat = MakeMat(part.Tint, 0.38f, 0.52f);
		var dark = MakeMat(part.Tint.Darkened(0.32f), 0.45f, 0.48f);
		var light = MakeMat(part.Tint.Lightened(0.18f), 0.3f, 0.45f);
		var glow = MakeMat(part.Tint.Lightened(0.35f), 0.2f, 0.35f,
			part.Tint.Lightened(0.2f), 1.1f);

		var scale = part.VisualScale;
		Scale = scale;

		BindMeshes(ExteriorMeshPaths, mat);
		BindMeshes(FrameMeshPaths, dark);
		BindMesh("Exterior/PauldronL", light);
		BindMesh("Exterior/PauldronR", light);
		BindMeshes(AccentMeshPaths, glow);

		var glass = MakeViewGlass();
		foreach (var path in GlassMeshPaths)
			BindMesh(path, glass);
	}

	private void BindMeshes(string[] paths, Material mat)
	{
		foreach (var path in paths)
			BindMesh(path, mat);
	}

	private void BindMesh(string path, Material mat)
	{
		if (GetNodeOrNull<MeshInstance3D>(path) is { } mi)
			MeshMat.Bind(mi, mat);
	}

	private static StandardMaterial3D MakeMat(
		Color albedo, float metallic, float roughness, Color? emission = null, float emissionEnergy = 0f)
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

	private static StandardMaterial3D MakeViewGlass() =>
		new()
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(0.55f, 0.82f, 1f, 0.07f),
			Metallic = 0.02f,
			Roughness = 0.08f,
			EmissionEnabled = true,
			Emission = new Color(0.35f, 0.7f, 1f),
			EmissionEnergyMultiplier = 0.12f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled
		};
}
