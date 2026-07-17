using Godot;

namespace Mechanize;

public partial class DummyTarget : StaticBody3D
{
	[Export] public float MaxHealth { get; set; } = 80f;
	[Export] public Color AliveColor { get; set; } = new(0.75f, 0.35f, 0.2f);
	[Export] public Color DeadColor { get; set; } = new(0.25f, 0.25f, 0.25f);
	[Export] public int ShatterPieces { get; set; } = 14;
	[Export] public bool BlocksMovement { get; set; } = true;

	private Damageable? _health;
	private Node3D? _visualRoot;
	private StandardMaterial3D? _mat;
	private Label3D? _label;
	private Vector3 _visualSize = new(2.4f, 1.6f, 1.8f);
	private bool _destroyed;

	public override void _Ready()
	{
		CollisionLayer = BlocksMovement ? PhysicsLayers.World : PhysicsLayers.Targets;
		CollisionMask = 0;

		EnsureVisual();
		EnsureHealth();
	}

	private void EnsureVisual()
	{
		// Replace flat box with a small shipping-crate silhouette.
		var oldMesh = GetNodeOrNull<MeshInstance3D>("Mesh");
		oldMesh?.QueueFree();

		_visualRoot = GetNodeOrNull<Node3D>("CrateVisual");
		if (_visualRoot == null)
		{
			_visualRoot = new Node3D { Name = "CrateVisual" };
			AddChild(_visualRoot);

			_mat = new StandardMaterial3D
			{
				AlbedoColor = AliveColor,
				Roughness = 0.65f,
				Metallic = 0.25f
			};
			var dark = new StandardMaterial3D
			{
				AlbedoColor = AliveColor.Darkened(0.3f),
				Roughness = 0.6f,
				Metallic = 0.35f
			};
			var strap = new StandardMaterial3D
			{
				AlbedoColor = AliveColor.Lightened(0.15f),
				Roughness = 0.5f,
				Metallic = 0.4f
			};

			var size = _visualSize;
			AddBox(_visualRoot, _mat, size, new Vector3(0f, size.Y * 0.5f, 0f));
			AddBox(_visualRoot, dark, new Vector3(size.X * 1.02f, 0.12f, size.Z * 1.02f), new Vector3(0f, size.Y * 0.08f, 0f));
			AddBox(_visualRoot, dark, new Vector3(size.X * 1.02f, 0.12f, size.Z * 1.02f), new Vector3(0f, size.Y * 0.92f, 0f));
			AddBox(_visualRoot, strap, new Vector3(0.1f, size.Y * 0.9f, size.Z * 1.04f), new Vector3(-size.X * 0.2f, size.Y * 0.5f, 0f));
			AddBox(_visualRoot, strap, new Vector3(0.1f, size.Y * 0.9f, size.Z * 1.04f), new Vector3(size.X * 0.2f, size.Y * 0.5f, 0f));
			AddBox(_visualRoot, dark, new Vector3(size.X * 0.35f, size.Y * 0.45f, 0.08f), new Vector3(0f, size.Y * 0.45f, size.Z * 0.5f));
		}
		else
		{
			_mat = _visualRoot.GetChildOrNull<MeshInstance3D>(0)?.MaterialOverride as StandardMaterial3D;
		}

		var collision = GetNodeOrNull<CollisionShape3D>("Collision");
		if (collision == null)
		{
			collision = new CollisionShape3D
			{
				Name = "Collision",
				Shape = new BoxShape3D { Size = _visualSize },
				Position = new Vector3(0f, _visualSize.Y * 0.5f, 0f)
			};
			AddChild(collision);
		}
		else if (collision.Shape is BoxShape3D existing)
		{
			_visualSize = existing.Size;
			collision.Position = new Vector3(0f, _visualSize.Y * 0.5f, 0f);
		}

		_label = GetNodeOrNull<Label3D>("HpLabel");
		if (_label == null)
		{
			_label = new Label3D
			{
				Name = "HpLabel",
				Position = new Vector3(0f, _visualSize.Y + 0.6f, 0f),
				FontSize = 42,
				Modulate = Colors.White,
				OutlineSize = 8,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
			};
			AddChild(_label);
		}
	}

	private static void AddBox(Node3D parent, Material mat, Vector3 size, Vector3 position)
	{
		parent.AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			Position = position,
			MaterialOverride = mat
		});
	}

	private void EnsureHealth()
	{
		_health = GetNodeOrNull<Damageable>("Damageable");
		if (_health == null)
		{
			_health = new Damageable { Name = "Damageable", MaxHealth = MaxHealth };
			AddChild(_health);
		}
		else
		{
			_health.ResetHealth(MaxHealth);
		}

		_health.Damaged += OnDamaged;
		_health.Died += OnDied;
		UpdateLabel();
	}

	private void OnDamaged(float amount, float remaining)
	{
		if (_destroyed)
			return;

		UpdateLabel();
		if (_mat != null)
		{
			var t = remaining / MaxHealth;
			_mat.AlbedoColor = AliveColor.Lerp(DeadColor, 1f - t);
		}
	}

	private void OnDied()
	{
		if (_destroyed)
			return;
		_destroyed = true;

		var origin = GlobalPosition + Vector3.Up * (_visualSize.Y * 0.5f);
		var parent = GetTree().CurrentScene ?? GetParent();
		if (parent != null)
		{
			ShatterBurst.Spawn(
				parent,
				origin,
				AliveColor,
				_visualSize,
				ShatterPieces);
			LootService.SpawnWorldDrops(parent, origin, LootService.ScrapForCover());
		}

		CollisionLayer = 0;
		if (_visualRoot != null)
			_visualRoot.Visible = false;
		if (_label != null)
			_label.Visible = false;

		GD.Print($"{Name} shattered.");
		QueueFree();
	}

	private void UpdateLabel()
	{
		if (_label == null || _health == null)
			return;
		_label.Text = _health.IsDead ? "WRECK" : $"{Mathf.CeilToInt(_health.CurrentHealth)} HP";
	}
}
