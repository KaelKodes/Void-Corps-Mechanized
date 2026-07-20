using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Central durability calibration for catalogue parts.
/// Every non-empty component leaves catalogue construction with explicit structure and armor.
/// Torso structure is authored directly by CatalogFrames because it is the MAP's defeat pool.
/// </summary>
public static class CatalogDurability
{
	public static void Apply(Dictionary<string, PartData> parts)
	{
		foreach (var part in parts.Values)
		{
			if (part.VisualKind == "empty")
			{
				part.StructureHp = 0f;
				part.Armor = 0f;
				continue;
			}

			part.Armor = Mathf.Max(0f, part.Armor);

			// Arm weapons previously had no durability identity at all. Give each
			// manufacturer a readable baseline while preserving glassier precision/energy kit.
			if (part.Slot is PartSlot.WeaponL or PartSlot.WeaponR && part.Armor <= 0f)
				part.Armor = WeaponArmor(part.ManufacturerId, part.Tier);

			// Unarmored systems are still physical components. Zero remains valid on
			// exposed shoulder modules, but systems bays receive their maker's casing.
			if (part.Slot == PartSlot.Systems && part.Armor <= 0f)
				part.Armor = SystemsArmor(part.ManufacturerId, part.Tier);

			if (part.StructureHp <= 0f)
				part.StructureHp = DefaultStructure(part);

			// Global playtest pad: every component +20% structure (includes authored torsos).
			part.StructureHp = Mathf.Round(part.StructureHp * 1.2f);
		}
	}

	private static float DefaultStructure(PartData part)
	{
		var baseHp = part.Slot switch
		{
			PartSlot.Legs => 28f, // per-limb budget into shared package pool
			PartSlot.Torso => 60f,
			PartSlot.Head => 40f,
			PartSlot.PowerCore => 44f,
			PartSlot.WeaponL or PartSlot.WeaponR => 52f,
			PartSlot.ShoulderL or PartSlot.ShoulderR => 34f,
			PartSlot.Backpack => 42f,
			PartSlot.Systems => 40f,
			_ => 40f
		};

		var manufacturerBonus = part.ManufacturerId switch
		{
			"brimforge" => 8f,
			"trinova" => 4f,
			"lumina" => 1f,
			_ => 0f // OuroTech
		};

		var tierBonus = Mathf.Max(0, part.Tier - 1) * 4f;
		return baseHp + manufacturerBonus + tierBonus;
	}

	private static float WeaponArmor(string manufacturerId, int tier)
	{
		var baseline = manufacturerId switch
		{
			"brimforge" => 22f,
			"trinova" => 14f,
			"lumina" => 10f,
			_ => 8f // OuroTech
		};
		return baseline + Mathf.Max(0, tier - 1) * 2f;
	}

	private static float SystemsArmor(string manufacturerId, int tier)
	{
		var baseline = manufacturerId switch
		{
			"brimforge" => 16f,
			"trinova" => 10f,
			"lumina" => 8f,
			_ => 6f // OuroTech
		};
		return baseline + Mathf.Max(0, tier - 1) * 2f;
	}
}
