using Godot;

namespace Mechanize;

/// <summary>
/// Expensive manufacturer floor-model on the trial grounds. Destroying it is optional spite/sabotage.
/// </summary>
public partial class DemoCradle : DummyTarget
{
	[Export] public string ManufacturerId { get; set; } = "";

	public override void _Ready()
	{
		MaxHealth = 120f;
		BlocksMovement = true;
		var company = GetNodeOrNull<GameSession>("/root/GameSession")?.GetFrontierCompany(ManufacturerId);
		var accent = company?.AccentColor ?? GameCatalog.GetManufacturer(ManufacturerId).AccentColor;
		AliveColor = accent;
		DeadColor = accent.Darkened(0.55f);
		Name = $"DemoCradle_{ManufacturerId}";
		base._Ready();

		var tag = GetNodeOrNull<Label3D>("HpLabel");
		if (tag != null)
			tag.Text = "FLOOR MODEL";

		var health = GetNodeOrNull<Damageable>("Damageable");
		if (health != null)
			health.Died += OnCradleDied;
	}

	private void OnCradleDied()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.NotifyDemoCradleDestroyed(ManufacturerId);
		GD.Print($"Demo cradle sabotaged: {ManufacturerId}");
	}
}
