using Godot;

namespace Mechanize;

/// <summary>
/// Mesh material helpers. Godot RD spam (material_casts_shadows null) happens when
/// MeshInstance3Ds are freed while still in the draw list with null materials, or when
/// PrimitiveMesh.Material (a shareable resource) is cleared while other instances still use it.
/// </summary>
public static class MeshMat
{
	public static void Bind(MeshInstance3D mi, Material mat)
	{
		// Instance override only — never write PrimitiveMesh.Material (shared resource).
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

	/// <summary>Pull geometry out of the renderer before QueueFree to avoid RD null-material spam.</summary>
	public static void DetachBeforeFree(Node? root)
	{
		if (root == null || !GodotObject.IsInstanceValid(root))
			return;

		foreach (var child in root.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
		{
			if (child is MeshInstance3D mi)
				StripMeshInstance(mi);
		}

		if (root is MeshInstance3D self)
			StripMeshInstance(self);
	}

	private static void StripMeshInstance(MeshInstance3D mi)
	{
		if (!GodotObject.IsInstanceValid(mi) || mi.IsQueuedForDeletion())
			return;

		mi.Visible = false;
		mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		// Leave MaterialOverride alone — nulling a live C# StandardMaterial3D can race Godot's
		// weak→strong GCHandle swap (SwapGCHandleForType / Handle is not initialized).
		// Drop Mesh so RD isn't left drawing geometry we're about to QueueFree.
		mi.Mesh = null;
	}

	public static void QueueFreeSafe(Node? node)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
			return;
		DetachBeforeFree(node);
		node.QueueFree();
	}
}
