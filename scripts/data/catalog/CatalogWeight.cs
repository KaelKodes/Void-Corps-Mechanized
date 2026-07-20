using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Central weight / load-rating calibration.
/// Soft mobility constraint: overweight slows movement and turning; never blocks deploy.
/// </summary>
public static class CatalogWeight
{
	public static void Apply(Dictionary<string, PartData> parts)
	{
		foreach (var part in parts.Values)
		{
			if (part.VisualKind == "empty")
			{
				part.Weight = 0f;
				part.LoadRating = 0f;
				continue;
			}

			if (part.Weight <= 0f)
				part.Weight = DefaultWeight(part);

			if (part.Slot == PartSlot.Legs && part.LoadRating <= 0f)
				part.LoadRating = DefaultLoadRating(part.Id, part);
		}
	}

	private static float DefaultWeight(PartData part)
	{
		var tier = Mathf.Clamp(part.Tier, 1, 3);
		var center = part.Slot switch
		{
			PartSlot.Legs => 16f + tier * 2f,           // 18 / 20 / 22
			PartSlot.Torso => 19f + tier * 3f,          // 22 / 25 / 28
			PartSlot.Head => 3f + tier,                 // 4 / 5 / 6
			PartSlot.PowerCore => 7f + tier * 2f,       // 9 / 11 / 13
			PartSlot.WeaponL or PartSlot.WeaponR => 6f + tier * 2f, // 8 / 10 / 12
			PartSlot.ShoulderL or PartSlot.ShoulderR => 3f + tier,  // 4 / 5 / 6
			PartSlot.Backpack => 4f + tier * 2f,        // 6 / 8 / 10
			PartSlot.Systems => 2f + tier,              // 3 / 4 / 5
			_ => 5f
		};

		// Held shields and heavy kinetic kit carry a bit more mass.
		if (part.IsHeldShield)
			center += 3f;
		else if (part.Slot is PartSlot.WeaponL or PartSlot.WeaponR)
		{
			if (part.WeaponFamily == WeaponFamily.Melee || part.Damage >= 28f)
				center += 2f;
			else if (part.Damage >= 20f)
				center += 1f;
		}
		else if (part.Slot == PartSlot.Backpack && part.Armor >= 40f)
			center += 2f; // ballast / plate packs

		var lean = part.ManufacturerId switch
		{
			"brimforge" => 1.20f,
			"lumina" => 0.90f,
			"ourotech" => 0.85f,
			_ => 1.00f // Trinova
		};

		return Mathf.Max(1f, Mathf.Round(center * lean));
	}

	private static float DefaultLoadRating(string id, PartData part)
	{
		// Authored targets so stock kits sit near 85–95% utilization.
		return id switch
		{
			"legs_lum_glasswalk" => 90f,
			"legs_ouro_duelist" => 95f,
			"legs_ouro_ascender" => 95f,
			"legs_lum_vector" => 95f,
			"legs_tri_biped" => 95f,
			"legs_lum_boosters" => 98f,
			"legs_ouro_thrusters" => 100f,
			"legs_ouro_razorhex" => 100f,
			"legs_ouro_hex" => 105f,
			"legs_lum_phasehex" => 105f,
			"legs_lum_hex" => 105f,
			"legs_tri_courier" => 105f,
			"legs_tri_jumpjack" => 108f,
			"legs_tri_slide" => 110f,
			"legs_ouro_rail" => 115f,
			"legs_lum_magbelt" => 115f,
			"legs_tri_packhex" => 120f,
			"legs_brin_biped" => 120f,
			"legs_tri_tracks" => 130f,
			"legs_brin_bulwark" => 135f,
			"legs_brin_pilejack" => 138f,
			"legs_brin_charge" => 140f,
			"legs_brin_siegehex" => 145f,
			"legs_brin_tracks" => 145f,
			"legs_tri_hauler" => 145f,
			"legs_brin_fortress" => 150f,
			_ => FallbackLoadRating(part)
		};
	}

	private static float FallbackLoadRating(PartData part)
	{
		// Rough identity: heavier/slower legs carry more.
		var fromArmor = part.Armor * 1.6f;
		var fromSpeed = Mathf.Max(0f, 18f - part.MaxSpeed) * 4f;
		var typeBias = part.LegType switch
		{
			LegType.Tracks => 25f,
			LegType.Hexapod => 10f,
			_ => 0f
		};
		return Mathf.Clamp(Mathf.Round(80f + fromArmor * 0.35f + fromSpeed + typeBias), 85f, 160f);
	}

	/// <summary>
	/// Locked overload curve: linear move, squared turn, immobile at 200% load rating.
	/// </summary>
	public static (float Move, float Turn, float Ratio) ComputeOverloadMultipliers(float totalWeight, float loadRating)
	{
		var rating = Mathf.Max(1f, loadRating);
		var ratio = Mathf.Max(0f, totalWeight) / rating;
		var overload = Mathf.Clamp(ratio - 1f, 0f, 1f);
		var move = 1f - overload;
		var turn = move * move;
		return (move, turn, ratio);
	}
}
