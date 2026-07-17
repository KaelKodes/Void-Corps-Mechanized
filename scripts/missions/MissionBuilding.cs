using Godot;

namespace Mechanize;

/// <summary>Large destructible objective building for S&amp;D / data missions.</summary>
public partial class MissionBuilding : StaticBody3D
{
	[Signal] public delegate void DestroyedEventHandler();

	public float MaxHealth { get; private set; } = 420f;

	private Damageable? _health;
	private Label3D? _label;
	private bool _dead;

	public bool IsDestroyed => _dead || (_health?.IsDead ?? false);
	public float HealthRatio => _health == null || _health.MaxHealth <= 0f
		? 0f
		: _health.CurrentHealth / _health.MaxHealth;

	public static MissionBuilding Create(string name, Vector3 position, float maxHealth, Color color)
	{
		var building = new MissionBuilding
		{
			Name = name,
			MaxHealth = maxHealth,
			Position = position,
			CollisionLayer = 1 | 8,
			CollisionMask = 0
		};
		building.BuildVisual(color);
		building.EnsureHealth();
		return building;
	}

	private void BuildVisual(Color color)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.68f,
			Metallic = 0.22f
		};
		var dark = new StandardMaterial3D
		{
			AlbedoColor = color.Darkened(0.35f),
			Roughness = 0.6f,
			Metallic = 0.3f
		};
		var glass = new StandardMaterial3D
		{
			AlbedoColor = color.Lightened(0.25f),
			EmissionEnabled = true,
			Emission = color.Lightened(0.15f),
			EmissionEnergyMultiplier = 0.55f,
			Roughness = 0.35f
		};

		AddBox(mat, new Vector3(8f, 4f, 6f), new Vector3(0f, 2f, 0f));
		AddBox(dark, new Vector3(8.4f, 0.35f, 6.4f), new Vector3(0f, 4.1f, 0f));
		AddBox(mat, new Vector3(3f, 4.2f, 3f), new Vector3(0f, 5.2f, 0f));
		AddBox(dark, new Vector3(3.3f, 0.25f, 3.3f), new Vector3(0f, 7.35f, 0f));
		AddBox(mat, new Vector3(10f, 1.2f, 1.2f), new Vector3(0f, 1.2f, -4f));
		// Window band
		AddBox(glass, new Vector3(6.5f, 0.7f, 0.12f), new Vector3(0f, 2.8f, -3.05f));
		AddBox(glass, new Vector3(0.12f, 0.7f, 4.5f), new Vector3(-4.05f, 2.8f, 0f));
		AddBox(glass, new Vector3(0.12f, 0.7f, 4.5f), new Vector3(4.05f, 2.8f, 0f));
		// Roof antenna
		AddBox(dark, new Vector3(0.15f, 1.4f, 0.15f), new Vector3(0.8f, 8.1f, 0.6f));
		AddBox(glass, new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0.8f, 8.85f, 0.6f));

		var collision = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(8.5f, 5.5f, 7f) },
			Position = new Vector3(0f, 2.75f, 0f)
		};
		AddChild(collision);

		_label = new Label3D
		{
			Position = new Vector3(0f, 9.2f, 0f),
			FontSize = 52,
			OutlineSize = 10,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(1f, 0.55f, 0.35f)
		};
		AddChild(_label);
		RefreshLabel();
	}

	private void AddBox(Material mat, Vector3 size, Vector3 position)
	{
		var mi = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			Position = position,
			MaterialOverride = mat
		};
		AddChild(mi);
	}

	private void EnsureHealth()
	{
		_health = new Damageable { Name = "Damageable", MaxHealth = MaxHealth };
		AddChild(_health);
		_health.ResetHealth(MaxHealth);
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;
	}

	public override void _Process(double delta) => RefreshLabel();

	private void OnDamaged(float amount, float remaining) => RefreshLabel();

	private void RefreshLabel()
	{
		if (_label == null || _health == null)
			return;
		_label.Text = _dead
			? "WRECK"
			: $"TARGET {Mathf.CeilToInt(_health.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";
	}

	private void OnDied()
	{
		if (_dead)
			return;
		_dead = true;
		CollisionLayer = 0;
		Visible = false;
		var parent = GetTree()?.CurrentScene ?? GetParent();
		if (parent != null)
			ShatterBurst.Spawn(parent, GlobalPosition + Vector3.Up * 2f, new Color(1f, 0.45f, 0.2f), new Vector3(6f, 4f, 5f), 36);
		EmitSignal(SignalName.Destroyed);
	}
}
