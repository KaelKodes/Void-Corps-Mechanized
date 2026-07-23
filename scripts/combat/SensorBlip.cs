using Godot;

namespace Mechanize;

/// <summary>
/// Frozen last-known contact marker. Does not track the unit after spawn.
/// </summary>
public partial class SensorBlip : Node3D
{
	private float _life;
	private float _maxLife = 1.6f;
	private ScanBlipStyle _style = ScanBlipStyle.WorldPip;
	private Color _color = Colors.White;
	private MeshInstance3D? _mesh;
	private StandardMaterial3D? _mat;

	public void Configure(Vector3 worldPos, Color color, float duration, ScanBlipStyle style)
	{
		_color = color;
		_maxLife = Mathf.Max(0.35f, duration);
		_life = _maxLife;
		_style = style == ScanBlipStyle.Inherit ? ScanBlipStyle.WorldPip : style;
		GlobalPosition = _style == ScanBlipStyle.GroundRing
			? new Vector3(worldPos.X, 0.06f, worldPos.Z)
			: worldPos + Vector3.Up * 1.35f;
		BuildVisual();
	}

	public override void _Process(double delta)
	{
		_life -= (float)delta;
		if (_life <= 0f)
		{
			QueueFree();
			return;
		}

		if (_mat == null)
			return;

		var t = Mathf.Clamp(_life / _maxLife, 0f, 1f);
		// Hold opaque most of the life, then fade out in the last third.
		var alpha = t > 0.35f ? 0.92f : Mathf.Lerp(0f, 0.92f, t / 0.35f);
		_mat.AlbedoColor = new Color(_color.R, _color.G, _color.B, alpha);
		_mat.Emission = _color;
		_mat.EmissionEnergyMultiplier = Mathf.Lerp(0.4f, 1.6f, t);
	}

	private void BuildVisual()
	{
		_mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(_color.R, _color.G, _color.B, 0.92f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			EmissionEnabled = true,
			Emission = _color,
			EmissionEnergyMultiplier = 1.4f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true
		};

		Mesh mesh = _style == ScanBlipStyle.GroundRing
			? new TorusMesh
			{
				InnerRadius = 0.85f,
				OuterRadius = 1.15f,
				Rings = 8,
				RingSegments = 24
			}
			: new SphereMesh
			{
				Radius = 0.55f,
				Height = 1.1f,
				RadialSegments = 12,
				Rings = 8
			};

		_mesh = new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = _mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_mesh);
	}
}
