using System.Collections.Generic;

namespace Mechanize;

public static class SupportCatalog
{
	public static IReadOnlyDictionary<string, SupportUnitData> Units { get; private set; } = null!;

	private static bool _built;

	public static void EnsureBuilt()
	{
		if (_built)
			return;
		_built = true;

		Units = new Dictionary<string, SupportUnitData>
		{
			["light_tank"] = new SupportUnitData
			{
				Id = "light_tank",
				DisplayName = "Patrol Slab",
				Kind = SupportUnitKind.LightTank,
				MaxHealth = 55f,
				Armor = 8f,
				Speed = 5.0f,
				TurnRateDegrees = 55f,
				Damage = 6f,
				FireRate = 1.6f,
				Range = 32f,
				ProjectileSpeed = 38f,
				VisionRange = 40f,
				PreferStatic = false,
				Tint = new Godot.Color(0.62f, 0.4f, 0.22f),
				VisualScale = new Godot.Vector3(1.1f, 0.7f, 1.3f)
			},
			["gun_tower"] = new SupportUnitData
			{
				Id = "gun_tower",
				DisplayName = "Claim Pike",
				Kind = SupportUnitKind.GunTower,
				MaxHealth = 70f,
				Armor = 14f,
				Speed = 0f,
				TurnRateDegrees = 55f,
				Damage = 8f,
				FireRate = 1.2f,
				Range = 38f,
				ProjectileSpeed = 50f,
				VisionRange = 45f,
				PreferStatic = true,
				Tint = new Godot.Color(0.45f, 0.55f, 0.48f),
				VisualScale = new Godot.Vector3(0.9f, 1.6f, 0.9f)
			},
			["scout_buggy"] = new SupportUnitData
			{
				Id = "scout_buggy",
				DisplayName = "Skitter Cart",
				Kind = SupportUnitKind.ScoutBuggy,
				MaxHealth = 28f,
				Armor = 2f,
				Speed = 9.0f,
				TurnRateDegrees = 110f,
				Damage = 3.5f,
				FireRate = 3.5f,
				Range = 22f,
				ProjectileSpeed = 55f,
				VisionRange = 34f,
				PreferStatic = false,
				Tint = new Godot.Color(0.3f, 0.55f, 0.75f),
				VisualScale = new Godot.Vector3(0.85f, 0.45f, 1.1f)
			}
		};
	}

	public static SupportUnitData? Get(string id)
	{
		EnsureBuilt();
		return Units.TryGetValue(id, out var u) ? u : null;
	}
}
