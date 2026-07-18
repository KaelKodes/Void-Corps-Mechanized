using Godot;

namespace Mechanize;

/// <summary>
/// Mesh material helpers. Godot RD spam (material_casts_shadows null) happens when
/// MeshInstance3Ds with MaterialOverride / surface overrides are freed; clear first.
/// </summary>
public static class MeshMat
{
	public static void Bind(MeshInstance3D mi, Material mat)
	{
		if (mi.Mesh is PrimitiveMesh prim)
			prim.Material = mat;
		mi.MaterialOverride = mat;
	}

	public static MeshInstance3D Make(
		Mesh mesh,
		Material mat,
		Vector3? position = null,
		Vector3? rotation = null,
		GeometryInstance3D.ShadowCastingSetting castShadow = GeometryInstance3D.ShadowCastingSetting.On)
	{
		var mi = new MeshInstance3D
		{
			Mesh = mesh,
			CastShadow = castShadow
		};
		if (position.HasValue)
			mi.Position = position.Value;
		if (rotation.HasValue)
			mi.Rotation = rotation.Value;
		Bind(mi, mat);
		return mi;
	}

	/// <summary>Strip overrides so QueueFree does not trip RD null-material errors.</summary>
	public static void DetachBeforeFree(Node? root)
	{
		if (root == null || !GodotObject.IsInstanceValid(root))
			return;

		foreach (var child in root.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
		{
			if (child is not MeshInstance3D mi)
				continue;
			mi.MaterialOverride = null;
			var count = mi.GetSurfaceOverrideMaterialCount();
			for (var i = 0; i < count; i++)
				mi.SetSurfaceOverrideMaterial(i, null);
			if (mi.Mesh is PrimitiveMesh prim)
				prim.Material = null;
		}
	}

	public static void QueueFreeSafe(Node? node)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
			return;
		DetachBeforeFree(node);
		node.QueueFree();
	}
}
