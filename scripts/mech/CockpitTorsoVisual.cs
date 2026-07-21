using Godot;

namespace Mechanize;

/// <summary>
/// Shared Mech 2.0 cockpit binder. Geometry lives in per-house .tscn files under scenes/parts/.
/// Node paths match the Fleet Intermediate contract so diegetic HUD and tinting stay portable.
/// </summary>
public partial class CockpitTorsoVisual : Node3D
{
	protected virtual string[] ExteriorMeshPaths { get; } =
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

	protected virtual string[] FrameMeshPaths { get; } =
	[
		"ViewFrame/PillarL",
		"ViewFrame/PillarR",
		"ViewFrame/LintelTop",
		"ViewFrame/SillBottom",
		"ViewFrame/RollBar",
		"ViewFrame/MidCrossbar",
		"ViewFrame/Mullion",
		"ViewFrame/CornerTL",
		"ViewFrame/CornerTR",
		"ViewFrame/CornerBL",
		"ViewFrame/CornerBR",
		"ViewFrame/RollMountL",
		"ViewFrame/RollMountR",
		"Exterior/SideWallL/Brace",
		"Exterior/SideWallR/Brace",
		"Exterior/AnvilChest",
		"Exterior/JowlPlate",
		"Exterior/EarL",
		"Exterior/EarR",
		"Exterior/FacetL",
		"Exterior/FacetR",
		"Exterior/FacetTop",
		"Exterior/GimbalCage",
		"CockpitInterior/Dashboard/DeckShelf",
		"CockpitInterior/Dashboard/CenterStick/Base",
		"CockpitInterior/Dashboard/CenterStick/Handle",
		"CockpitInterior/SideRailL",
		"CockpitInterior/SideRailR"
	];

	protected virtual string[] GlassMeshPaths { get; } =
	[
		"ViewPanel",
		"Exterior/SideWallL/GlassWedge",
		"Exterior/SideWallR/GlassWedge"
	];

	protected virtual string[] AccentMeshPaths { get; } =
	[
		"Exterior/AccentL",
		"Exterior/AccentR",
		"ViewFrame/RollMountL",
		"ViewFrame/RollMountR",
		"CockpitInterior/Dashboard/CenterStick/Grip"
	];

	public virtual void ApplyPart(PartData part)
	{
		var mat = MakeMat(part.Tint, 0.38f, 0.52f);
		var dark = MakeMat(part.Tint.Darkened(0.32f), 0.45f, 0.48f);
		var light = MakeMat(part.Tint.Lightened(0.18f), 0.3f, 0.45f);
		var glow = MakeMat(part.Tint.Lightened(0.35f), 0.2f, 0.35f,
			part.Tint.Lightened(0.2f), 1.1f);

		Scale = part.VisualScale;

		BindMeshes(ExteriorMeshPaths, mat);
		BindMeshes(FrameMeshPaths, dark);
		BindMesh("Exterior/PauldronL", light);
		BindMesh("Exterior/PauldronR", light);
		BindMeshes(AccentMeshPaths, glow);

		var glass = MakeViewGlass();
		foreach (var path in GlassMeshPaths)
			BindMesh(path, glass);
	}

	protected void BindMeshes(string[] paths, Material mat)
	{
		foreach (var path in paths)
			BindMesh(path, mat);
	}

	protected void BindMesh(string path, Material mat)
	{
		if (GetNodeOrNull<MeshInstance3D>(path) is { } mi)
			MeshMat.Bind(mi, mat);
	}

	public static StandardMaterial3D MakeMat(
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

	/// <summary>
	/// Front view glass — clear for the seated pilot (FP look-out).
	/// Exterior “solid armor vs transparent” paint is a later customization choice;
	/// for now keep this see-through so combat FP stays playable.
	/// </summary>
	public static StandardMaterial3D MakeViewGlass() =>
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
			SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
			RenderPriority = 1,
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
		};
}
