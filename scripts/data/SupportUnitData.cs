using Godot;

namespace Mechanize;

public enum SupportUnitKind
{
	LightTank,
	GunTower,
	ScoutBuggy
}

[GlobalClass]
public partial class SupportUnitData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public SupportUnitKind Kind { get; set; }
	[Export] public float MaxHealth { get; set; } = 40f;
	[Export] public float Armor { get; set; }
	[Export] public float Speed { get; set; } = 8f;
	[Export] public float TurnRateDegrees { get; set; } = 90f;
	[Export] public float Damage { get; set; } = 4f;
	[Export] public float FireRate { get; set; } = 2f;
	[Export] public float Range { get; set; } = 28f;
	[Export] public float ProjectileSpeed { get; set; } = 40f;
	[Export] public float VisionRange { get; set; } = 36f;
	[Export] public bool PreferStatic { get; set; }
	[Export] public Color Tint { get; set; } = Colors.Gray;
	[Export] public Vector3 VisualScale { get; set; } = Vector3.One;
}
