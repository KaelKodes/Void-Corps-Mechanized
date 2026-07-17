using Godot;

namespace Mechanize;

/// <summary>Carryable data disk after archive breach.</summary>
public partial class DataDiskPickup : Area3D
{
	[Signal] public delegate void CollectedEventHandler();

	private bool _taken;

	public static DataDiskPickup Create(Vector3 position)
	{
		var disk = new DataDiskPickup
		{
			Name = "DataDisk",
			Position = position,
			CollisionLayer = 0,
			CollisionMask = 2,
			Monitoring = true,
			Monitorable = false
		};
		disk.Build();
		return disk;
	}

	private void Build()
	{
		var shape = new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = 2.2f },
			Position = new Vector3(0f, 1f, 0f)
		};
		AddChild(shape);

		var mesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.15f, 1.1f) },
			Position = new Vector3(0f, 1.1f, 0f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.3f, 0.95f, 0.75f),
				EmissionEnabled = true,
				Emission = new Color(0.2f, 0.9f, 0.7f),
				EmissionEnergyMultiplier = 1.4f
			}
		};
		AddChild(mesh);

		var label = new Label3D
		{
			Text = "DATA DISK",
			Position = new Vector3(0f, 2.4f, 0f),
			FontSize = 40,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(0.4f, 1f, 0.8f)
		};
		AddChild(label);

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_taken)
			return;
		if (body is not MechController mech || !mech.IsPlayerControlled)
			return;

		_taken = true;
		Visible = false;
		SetDeferred(Area3D.PropertyName.Monitoring, false);
		SfxService.Play("disk");
		EmitSignal(SignalName.Collected);
		CallDeferred(Node.MethodName.QueueFree);
	}
}
