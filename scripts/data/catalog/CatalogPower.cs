using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Central power calibration: PowerRequirement standby reservations and PowerPerShot costs.
/// Runs after tiers so manufacturer / tier leans are stable.
/// </summary>
public static class CatalogPower
{
	public static void Apply(Dictionary<string, PartData> parts)
	{
		foreach (var part in parts.Values)
		{
			if (part.VisualKind == "empty")
			{
				part.PowerRequirement = 0f;
				part.PowerPerShot = 0f;
				continue;
			}

			if (part.Slot == PartSlot.PowerCore)
			{
				part.PowerRequirement = 0f;
				continue;
			}

			if (part.PowerRequirement <= 0f)
				part.PowerRequirement = DefaultRequirement(part);

			// Weapons: concurrent-load authoring is obsolete — derive per-shot spend.
			// Held shields do not shoot; melee keeps its authored contact cost (often zero).
			if (part.Slot is PartSlot.WeaponL or PartSlot.WeaponR)
			{
				if (part.IsHeldShield)
					part.PowerPerShot = 0f;
				else if (part.WeaponFamily == WeaponFamily.Melee)
				{
					// Keep authored contact cost; air movement never spends it.
				}
				else
					part.PowerPerShot = DefaultPowerPerShot(part);
			}

			if (part.GrantsActiveAbility && part.AbilityPowerLoad > 0f)
			{
				// Legacy ability loads were concurrent-reservation sized; shrink to pool bursts.
				part.AbilityPowerLoad = Mathf.Clamp(
					Mathf.Round(part.AbilityPowerLoad * 0.45f),
					4f,
					22f);
			}
		}
	}

	private static float DefaultRequirement(PartData part)
	{
		var baseline = part.Slot switch
		{
			PartSlot.Legs => 10f,
			PartSlot.Torso => 12f,
			PartSlot.Head => 5f,
			PartSlot.WeaponL or PartSlot.WeaponR => 8f,
			PartSlot.ShoulderL or PartSlot.ShoulderR => 5f,
			PartSlot.Backpack => 6f,
			PartSlot.Systems => 5f,
			_ => 5f
		};

		var mfg = part.ManufacturerId switch
		{
			"brimforge" => 2f,
			"lumina" => 1f,
			_ => 0f // Trinova / OuroTech
		};

		var tier = Mathf.Max(0, part.Tier - 1) * 1.5f;

		// Heavier kinetic / missile kits reserve more standby juice.
		if (part.Slot is PartSlot.WeaponL or PartSlot.WeaponR)
		{
			if (part.IsHeldShield)
				baseline += 1f;
			else if (part.WeaponFamily == WeaponFamily.Melee || part.Damage >= 24f)
				baseline += 3f;
			else if (part.FireRate >= 7f)
				baseline += 1f;
		}

		if (part.GrantsActiveAbility)
			baseline += 1f;

		return Mathf.Max(1f, baseline + mfg + tier);
	}

	private static float DefaultPowerPerShot(PartData part)
	{
		// Prefer readable per-shot costs: heavy slow guns pay more per trigger,
		// high RoF guns chip the pool with smaller bites.
		var fromDamage = part.Damage * 0.35f;
		var fromHeat = part.HeatPerShot * 0.25f;
		var rateBias = part.FireRate >= 6f ? 0.65f : part.FireRate <= 1.2f ? 1.25f : 1f;
		var cost = (fromDamage + fromHeat) * rateBias;
		return Mathf.Clamp(Mathf.Round(cost), 2f, 16f);
	}
}
