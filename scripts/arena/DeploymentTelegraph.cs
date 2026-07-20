using Godot;

namespace Mechanize;

/// <summary>
/// Locked landing preview: a translucent silhouette of whatever is about to arrive.
/// Target position is fixed at creation so warnings stay actionable.
/// </summary>
public partial class DeploymentTelegraph : Node3D
{
	public const float GhostAlpha = 0.6f;

	public TeamId Team { get; private set; } = TeamId.Enemy;
	public float WarningDuration { get; private set; } = 2f;

	private float _age;
	private Node3D? _ghostRoot;
	private StandardMaterial3D? _pulseMat;

	public static DeploymentTelegraph Create(
		string name,
		Vector3 position,
		TeamId team,
		float warningDuration,
		Node3D? previewSource = null,
		float radius = 3.2f)
	{
		var node = new DeploymentTelegraph
		{
			Name = name,
			Team = team,
			WarningDuration = Mathf.Clamp(warningDuration, 1f, 3f)
		};
		node.Build(previewSource, radius);
		node.Position = new Vector3(position.X, 0.06f, position.Z);
		return node;
	}

	public override void _ExitTree()
	{
		MeshMat.DetachBeforeFree(this);
		base._ExitTree();
	}

	private void Build(Node3D? previewSource, float radius)
	{
		_ghostRoot = new Node3D { Name = "Ghost" };
		AddChild(_ghostRoot);

		if (previewSource != null && GodotObject.IsInstanceValid(previewSource) && previewSource.IsInsideTree())
			CloneGhostFrom(previewSource);
		else
			BuildFallbackBlob(radius);

		// Soft contact disc so the landing point still reads without a beacon pad.
		var tint = Team == TeamId.Player
			? new Color(0.35f, 0.78f, 0.95f, 0.18f)
			: new Color(0.95f, 0.42f, 0.28f, 0.18f);
		_pulseMat = new StandardMaterial3D
		{
			AlbedoColor = tint,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			EmissionEnabled = true,
			Emission = new Color(tint.R, tint.G, tint.B),
			EmissionEnergyMultiplier = 0.35f
		};
		AddChild(new MeshInstance3D
		{
			Name = "Contact",
			Mesh = new CylinderMesh
			{
				TopRadius = radius * 0.55f,
				BottomRadius = radius * 0.55f,
				Height = 0.03f,
				RadialSegments = 20
			},
			Position = new Vector3(0f, 0.02f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = _pulseMat
		});
	}

	private void CloneGhostFrom(Node3D source)
	{
		if (_ghostRoot == null)
			return;

		var sourceXform = source.GlobalTransform;
		foreach (var child in source.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
		{
			if (child is not MeshInstance3D mi || mi.Mesh == null)
				continue;
			if (mi.Name == OcclusionSilhouette.GhostName)
				continue;

			var ghost = new MeshInstance3D
			{
				Mesh = mi.Mesh,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = MakeGhostMaterial(mi),
				// Source may be elsewhere in the world — bake pose relative to telegraph origin.
				Transform = sourceXform.AffineInverse() * mi.GlobalTransform
			};
			_ghostRoot.AddChild(ghost);
		}
	}

	private static StandardMaterial3D MakeGhostMaterial(MeshInstance3D source)
	{
		var color = new Color(0.55f, 0.58f, 0.62f, GhostAlpha);
		if (source.MaterialOverride is StandardMaterial3D src)
		{
			color = src.AlbedoColor;
			color.A = GhostAlpha;
		}
		else if (source.Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D surface)
		{
			color = surface.AlbedoColor;
			color.A = GhostAlpha;
		}

		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Metallic = 0.15f,
			Roughness = 0.75f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			DisableReceiveShadows = true
		};
	}

	private void BuildFallbackBlob(float radius)
	{
		if (_ghostRoot == null)
			return;

		var color = Team == TeamId.Player
			? new Color(0.4f, 0.8f, 0.95f, GhostAlpha)
			: new Color(0.95f, 0.45f, 0.32f, GhostAlpha);
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		_ghostRoot.AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(radius * 0.7f, radius * 1.1f, radius * 0.7f) },
			Position = new Vector3(0f, radius * 0.55f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = mat
		});
	}

	public void MarkInbound()
	{
		// Ghost stays until the real unit takes over / telegraph is freed.
		if (_ghostRoot != null)
			_ghostRoot.Visible = false;
	}

	public void MarkImpact()
	{
		if (_ghostRoot != null)
			_ghostRoot.Visible = false;
		if (_pulseMat != null)
		{
			var c = _pulseMat.AlbedoColor;
			_pulseMat.AlbedoColor = new Color(c.R, c.G, c.B, 0.28f);
		}
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (_pulseMat == null)
			return;
		var pulse = 0.55f + 0.45f * Mathf.Sin(_age * 6.5f);
		_pulseMat.EmissionEnergyMultiplier = 0.2f + pulse * 0.35f;
	}
}
