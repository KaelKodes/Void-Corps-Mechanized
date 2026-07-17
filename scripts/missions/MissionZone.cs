using Godot;

namespace Mechanize;

/// <summary>Visual + radius helper for capture / defend / base pads.</summary>
public partial class MissionZone : Node3D
{
	public float Radius { get; private set; } = 10f;
	public string ZoneId { get; private set; } = "zone";

	private MeshInstance3D? _ring;
	private Label3D? _label;
	private Color _baseColor = new(0.35f, 0.75f, 0.95f);

	public static MissionZone Create(string id, Vector3 position, float radius, Color color, string label)
	{
		var zone = new MissionZone
		{
			Name = id,
			ZoneId = id,
			Radius = radius,
			_baseColor = color,
			Position = position
		};
		zone.Build(label);
		return zone;
	}

	private void Build(string label)
	{
		_ring = new MeshInstance3D
		{
			Mesh = new TorusMesh
			{
				InnerRadius = Mathf.Max(0.5f, Radius - 0.45f),
				OuterRadius = Radius,
				Rings = 24,
				RingSegments = 48
			},
			Position = new Vector3(0f, 0.08f, 0f),
			MaterialOverride = MakeRingMat()
		};
		AddChild(_ring);

		var disc = new MeshInstance3D
		{
			Mesh = new CylinderMesh
			{
				TopRadius = Radius * 0.98f,
				BottomRadius = Radius * 0.98f,
				Height = 0.05f,
				RadialSegments = 48
			},
			Position = new Vector3(0f, 0.03f, 0f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(_baseColor.R, _baseColor.G, _baseColor.B, 0.18f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				Roughness = 1f
			}
		};
		AddChild(disc);

		_label = new Label3D
		{
			Text = label,
			Position = new Vector3(0f, 3.2f, 0f),
			FontSize = 48,
			Modulate = _baseColor,
			OutlineSize = 10,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
		};
		AddChild(_label);
	}

	private StandardMaterial3D MakeRingMat() => new()
	{
		AlbedoColor = _baseColor,
		EmissionEnabled = true,
		Emission = _baseColor,
		EmissionEnergyMultiplier = 0.8f,
		Roughness = 0.45f
	};

	public void SetLabel(string text)
	{
		if (_label != null)
			_label.Text = text;
	}

	public void SetColor(Color color)
	{
		_baseColor = color;
		if (_ring?.MaterialOverride is StandardMaterial3D mat)
		{
			mat.AlbedoColor = color;
			mat.Emission = color;
		}

		if (_label != null)
			_label.Modulate = color;
	}

	public bool Contains(Vector3 worldPoint)
	{
		var delta = worldPoint - GlobalPosition;
		delta.Y = 0f;
		return delta.LengthSquared() <= Radius * Radius;
	}
}
