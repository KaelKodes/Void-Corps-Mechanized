using Godot;

namespace Mechanize;

/// <summary>World-space missile paint marker under the cursor while holding lock.</summary>
public partial class MissileLockDecal : Node3D
{
	private MeshInstance3D? _ring;
	private MeshInstance3D? _fill;
	private Label3D? _label;
	private StandardMaterial3D? _ringMat;
	private StandardMaterial3D? _fillMat;

	public override void _Ready()
	{
		_ringMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.3f, 0.95f, 0.45f, 0.9f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			EmissionEnabled = true,
			Emission = new Color(0.25f, 0.9f, 0.4f),
			EmissionEnergyMultiplier = 1.4f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		_fillMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.3f, 0.95f, 0.45f, 0.18f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		_ring = new MeshInstance3D
		{
			Mesh = new TorusMesh
			{
				InnerRadius = 2.4f,
				OuterRadius = 2.85f,
				Rings = 12,
				RingSegments = 32
			},
			Position = new Vector3(0f, 0.12f, 0f),
			MaterialOverride = _ringMat
		};
		AddChild(_ring);

		_fill = new MeshInstance3D
		{
			Mesh = new CylinderMesh
			{
				TopRadius = 2.35f,
				BottomRadius = 2.35f,
				Height = 0.04f,
				RadialSegments = 32
			},
			Position = new Vector3(0f, 0.08f, 0f),
			MaterialOverride = _fillMat
		};
		AddChild(_fill);

		_label = new Label3D
		{
			Text = "MISSILE LOCK",
			Position = new Vector3(0f, 2.4f, 0f),
			FontSize = 42,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(0.45f, 1f, 0.55f)
		};
		AddChild(_label);

		Visible = false;
	}

	public void SetState(bool active, Vector3 worldPoint, bool valid, bool healPaint = false)
	{
		Visible = active;
		if (!active)
			return;

		GlobalPosition = new Vector3(worldPoint.X, 0.05f, worldPoint.Z);
		var color = valid
			? (healPaint ? new Color(0.35f, 0.95f, 0.65f) : new Color(0.3f, 0.95f, 0.45f))
			: new Color(0.95f, 0.28f, 0.22f);
		if (_ringMat != null)
		{
			_ringMat.AlbedoColor = new Color(color.R, color.G, color.B, 0.9f);
			_ringMat.Emission = color;
		}
		if (_fillMat != null)
			_fillMat.AlbedoColor = new Color(color.R, color.G, color.B, 0.18f);
		if (_label != null)
		{
			_label.Modulate = color;
			_label.Text = valid
				? (healPaint ? "MEND DEPLOY" : "MISSILE LOCK")
				: "OUT OF SENSOR CONE";
		}
	}
}
