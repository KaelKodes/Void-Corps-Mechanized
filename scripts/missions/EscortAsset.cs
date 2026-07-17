using Godot;

namespace Mechanize;

/// <summary>Slow allied crawler that advances when the player stays nearby.</summary>
public partial class EscortAsset : CharacterBody3D
{
	public float MaxHealth { get; private set; } = 280f;
	public float MoveSpeed { get; set; } = 2.2f;
	public float EscortRadius { get; set; } = 16f;

	private Damageable? _health;
	private Vector3 _destination;
	private MechController? _escort;
	private bool _arrived;
	private bool _dead;

	public bool HasArrived => _arrived;
	public bool IsDestroyed => _dead || (_health?.IsDead ?? false);
	public float HealthRatio => _health == null || _health.MaxHealth <= 0f
		? 0f
		: _health.CurrentHealth / _health.MaxHealth;

	public static EscortAsset Create(Vector3 start, Vector3 destination)
	{
		var asset = new EscortAsset
		{
			Name = "SalvageCrawler",
			Position = start,
			_destination = destination,
			CollisionLayer = 2,
			CollisionMask = 1
		};
		asset.Build();
		return asset;
	}

	public void SetEscort(MechController? mech) => _escort = mech;

	private void Build()
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.45f, 0.7f, 0.4f),
			Roughness = 0.6f,
			Metallic = 0.25f
		};
		var dark = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.28f, 0.4f, 0.26f),
			Roughness = 0.55f,
			Metallic = 0.35f
		};
		var glass = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.35f, 0.85f, 0.55f),
			EmissionEnabled = true,
			Emission = new Color(0.35f, 0.85f, 0.55f),
			EmissionEnergyMultiplier = 0.7f,
			Roughness = 0.3f
		};

		AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(2.4f, 1.0f, 3.6f) },
			Position = new Vector3(0f, 0.75f, 0f),
			MaterialOverride = mat
		});
		AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(2.1f, 0.2f, 2.0f) },
			Position = new Vector3(0f, 1.3f, -0.5f),
			MaterialOverride = dark
		});
		AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.95f, 1.35f) },
			Position = new Vector3(0f, 1.55f, 0.75f),
			MaterialOverride = mat
		});
		AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(1.15f, 0.35f, 0.08f) },
			Position = new Vector3(0f, 1.7f, 1.4f),
			MaterialOverride = glass
		});
		AddWheel(dark, new Vector3(-1.1f, 0.35f, -1.2f));
		AddWheel(dark, new Vector3(1.1f, 0.35f, -1.2f));
		AddWheel(dark, new Vector3(-1.1f, 0.35f, 1.2f));
		AddWheel(dark, new Vector3(1.1f, 0.35f, 1.2f));

		AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(2.4f, 1.4f, 3.6f) },
			Position = new Vector3(0f, 0.75f, 0f)
		});

		_health = new Damageable { Name = "Damageable", MaxHealth = MaxHealth };
		AddChild(_health);
		_health.ResetHealth(MaxHealth);
		_health.Died += () =>
		{
			_dead = true;
			Velocity = Vector3.Zero;
			ProcessMode = ProcessModeEnum.Disabled;
			Visible = false;
		};

		var label = new Label3D
		{
			Name = "Label",
			Text = "CRAWLER",
			Position = new Vector3(0f, 2.8f, 0f),
			FontSize = 42,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(0.6f, 0.95f, 0.5f)
		};
		AddChild(label);
	}

	private void AddWheel(Material mat, Vector3 position)
	{
		AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 0.28f },
			Position = position,
			Rotation = Vector3.Forward * Mathf.Tau * 0.25f,
			MaterialOverride = mat
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead || _arrived || _health?.IsDead == true)
		{
			Velocity = Vector3.Zero;
			return;
		}

		var dt = (float)delta;
		var label = GetNodeOrNull<Label3D>("Label");
		if (label != null)
			label.Text = $"CRAWLER {Mathf.CeilToInt(_health!.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";

		var toGoal = _destination - GlobalPosition;
		toGoal.Y = 0f;
		if (toGoal.Length() < 3.5f)
		{
			_arrived = true;
			Velocity = Vector3.Zero;
			return;
		}

		var escorted = _escort != null
			&& GodotObject.IsInstanceValid(_escort)
			&& _escort.Integrity?.IsCollapsed != true
			&& _escort.GlobalPosition.DistanceTo(GlobalPosition) <= EscortRadius;

		if (!escorted)
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			if (!IsOnFloor())
				Velocity = new Vector3(0f, Velocity.Y - 28f * dt, 0f);
			MoveAndSlide();
			return;
		}

		var dir = toGoal.Normalized();
		var targetYaw = Mathf.Atan2(-dir.X, -dir.Z);
		Rotation = new Vector3(Rotation.X, Mathf.RotateToward(Rotation.Y, targetYaw, 1.6f * dt), Rotation.Z);
		Velocity = new Vector3(dir.X * MoveSpeed, Velocity.Y, dir.Z * MoveSpeed);
		if (!IsOnFloor())
			Velocity = new Vector3(Velocity.X, Velocity.Y - 28f * dt, Velocity.Z);
		MoveAndSlide();
	}
}
