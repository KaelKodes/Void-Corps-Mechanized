using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

public sealed record TechNode(
	string PartId,
	string ManufacturerId,
	int Tier,
	int ScrapCost,
	string PrerequisitePartId);

/// <summary>Manufacturer licensing trees. Scrap unlocks blueprints; materials build copies.</summary>
public static class TechTreeService
{
	public static List<TechNode> TreeFor(string manufacturerId)
	{
		GameCatalog.EnsureBuilt();
		var parts = GameCatalog.Parts.Values
			.Where(p => p.VisualKind != "empty" && p.ManufacturerId == manufacturerId)
			.OrderBy(p => p.Tier)
			.ThenBy(p => (int)p.Slot)
			.ThenBy(p => p.DisplayName)
			.ToList();

		var nodes = new List<TechNode>();
		foreach (var part in parts)
		{
			var prerequisite = parts
				.Where(p => p.Slot == part.Slot && p.Tier < part.Tier)
				.OrderByDescending(p => p.Tier)
				.Select(p => p.Id)
				.FirstOrDefault() ?? "";
			nodes.Add(new TechNode(
				part.Id,
				manufacturerId,
				part.Tier,
				UnlockCost(part),
				prerequisite));
		}

		return nodes;
	}

	public static int UnlockCost(PartData part) =>
		Mathf.Clamp(20 + part.Tier * 30 + ShopService.PriceFor(part) / 3, 40, 240);

	public static bool CanUnlock(PlayerProfile profile, TechNode node)
	{
		if (profile.HasBlueprint(node.PartId) || profile.Scrap < node.ScrapCost)
			return false;
		return string.IsNullOrEmpty(node.PrerequisitePartId)
		       || profile.HasBlueprint(node.PrerequisitePartId);
	}

	public static bool TryUnlock(PlayerProfile profile, TechNode node)
	{
		if (!CanUnlock(profile, node))
			return false;
		profile.Scrap -= node.ScrapCost;
		profile.UnlockedBlueprints.Add(node.PartId);
		return true;
	}
}

public static class FabricationService
{
	public static bool CanBuild(PlayerProfile profile, string partId)
	{
		var part = GameCatalog.GetPart(partId);
		if (part == null || !profile.HasBlueprint(partId))
			return false;
		foreach (var (materialId, amount) in MaterialCatalog.RecipeFor(part))
		{
			if (profile.MaterialCount(materialId) < amount)
				return false;
		}

		return true;
	}

	public static bool TryBuild(PlayerProfile profile, string partId)
	{
		var part = GameCatalog.GetPart(partId);
		if (part == null || !CanBuild(profile, partId))
			return false;
		foreach (var (materialId, amount) in MaterialCatalog.RecipeFor(part))
			profile.AddMaterial(materialId, -amount);
		profile.OwnInstance(partId);
		return true;
	}
}
