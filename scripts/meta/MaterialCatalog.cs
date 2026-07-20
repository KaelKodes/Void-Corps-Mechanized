using System.Collections.Generic;
using Godot;

namespace Mechanize;

public sealed record CraftingMaterialData(
	string Id,
	string DisplayName,
	string Description,
	Color Color,
	int Tier);

/// <summary>Universal salvage pool used to fabricate every licensed part.</summary>
public static class MaterialCatalog
{
	public const string Alloy = "mat_alloy";
	public const string Servo = "mat_servo";
	public const string Circuit = "mat_circuit";
	public const string Optics = "mat_optics";
	public const string Reactor = "mat_reactor";
	public const string Exotic = "mat_exotic";

	public static readonly IReadOnlyDictionary<string, CraftingMaterialData> All =
		new Dictionary<string, CraftingMaterialData>
		{
			[Alloy] = new(Alloy, "Claim Alloy", "Structural plate, barrels, and chassis bracing.", new Color(0.65f, 0.68f, 0.72f), 1),
			[Servo] = new(Servo, "Servo Bundles", "Actuators, recoil assemblies, and traverse motors.", new Color(0.82f, 0.58f, 0.3f), 1),
			[Circuit] = new(Circuit, "Control Circuits", "Weapon logic, regulators, and systems boards.", new Color(0.35f, 0.8f, 0.55f), 1),
			[Optics] = new(Optics, "Optic Glass", "Sensors, targeting arrays, and precision emitters.", new Color(0.4f, 0.78f, 0.95f), 2),
			[Reactor] = new(Reactor, "Reactor Matrix", "Power-dense core and energy-weapon substrate.", new Color(0.75f, 0.55f, 0.95f), 2),
			[Exotic] = new(Exotic, "Anomaly Catalysts", "Rare claim matter used in Threat-grade fabrication.", new Color(0.95f, 0.45f, 0.75f), 3)
		};

	public static Dictionary<string, int> RecipeFor(PartData part)
	{
		var tier = Mathf.Clamp(part.Tier, 1, 3);
		var recipe = new Dictionary<string, int>
		{
			[Alloy] = 2 + tier * 2
		};

		switch (part.Slot)
		{
			case PartSlot.Legs:
			case PartSlot.Torso:
				Add(recipe, Servo, 2 + tier);
				break;
			case PartSlot.Head:
				Add(recipe, Optics, 1 + tier);
				Add(recipe, Circuit, 1 + tier);
				break;
			case PartSlot.PowerCore:
				Add(recipe, Reactor, 2 + tier * 2);
				Add(recipe, Circuit, tier);
				break;
			case PartSlot.WeaponL:
			case PartSlot.WeaponR:
			case PartSlot.ShoulderL:
			case PartSlot.ShoulderR:
				Add(recipe, Servo, 1 + tier);
				Add(recipe, part.Damage > 0f && part.PowerPerShot > 0f ? Reactor : Circuit, 1 + tier);
				break;
			default:
				Add(recipe, Circuit, 2 + tier);
				break;
		}

		if (tier >= 2)
			Add(recipe, Optics, tier - 1);
		if (tier >= 3)
			Add(recipe, Exotic, 1 + (part.GrantsActiveAbility ? 1 : 0));
		return recipe;
	}

	private static void Add(Dictionary<string, int> recipe, string id, int amount) =>
		recipe[id] = recipe.GetValueOrDefault(id) + amount;
}
