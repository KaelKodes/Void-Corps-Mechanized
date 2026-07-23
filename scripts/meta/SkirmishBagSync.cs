using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// One-way bleed into the skirmish bag: scrap copies + unlock/part mirrors.
/// Skirmish never writes back to campaign or roguelike.
/// </summary>
public static class SkirmishBagSync
{
	public static void AddScrapCopy(int slot, int amount)
	{
		if (amount <= 0)
			return;

		var path = SaveService.SkirmishPath(slot);
		var skirmish = SaveService.LoadOrNew(path);
		skirmish.Scrap += amount;
		SaveService.Save(skirmish, path);
	}

	/// <summary>
	/// Mirror campaign ∪ roguelike blueprints and owned part types into skirmish as sandbox copies.
	/// </summary>
	public static void MirrorUnlocks(int slot)
	{
		var campaign = SaveService.LoadOrNew(SaveService.CampaignProfilePath(slot));
		PlayerProfile? roguelike = null;
		var rlPath = SaveService.RoguelikePath(slot);
		if (Godot.FileAccess.FileExists(rlPath))
			roguelike = SaveService.LoadOrNew(rlPath);

		var skirmishPath = SaveService.SkirmishPath(slot);
		var skirmish = SaveService.LoadOrNew(skirmishPath);
		MirrorInto(skirmish, campaign, roguelike);
		SaveService.Save(skirmish, skirmishPath);
	}

	public static void MirrorInto(PlayerProfile skirmish, PlayerProfile campaign, PlayerProfile? roguelike)
	{
		foreach (var id in campaign.UnlockedBlueprints)
			skirmish.UnlockedBlueprints.Add(id);
		if (roguelike != null)
		{
			foreach (var id in roguelike.UnlockedBlueprints)
				skirmish.UnlockedBlueprints.Add(id);
		}

		var ownedTypes = new HashSet<string>();
		foreach (var inst in campaign.OwnedInstances)
		{
			if (!string.IsNullOrEmpty(inst.PartId))
				ownedTypes.Add(inst.PartId);
		}

		if (roguelike != null)
		{
			foreach (var inst in roguelike.OwnedInstances)
			{
				if (!string.IsNullOrEmpty(inst.PartId))
					ownedTypes.Add(inst.PartId);
			}
		}

		var skirmishTypes = new HashSet<string>(
			skirmish.OwnedInstances.Select(i => i.PartId).Where(id => !string.IsNullOrEmpty(id)));

		foreach (var partId in ownedTypes)
		{
			if (skirmishTypes.Contains(partId))
				continue;
			if (PlayerProfile.IsUnlimited(partId))
				continue;
			skirmish.Own(partId);
			skirmishTypes.Add(partId);
		}
	}

	/// <summary>After campaign/RL rewards: copy scrap and refresh unlock mirror.</summary>
	public static void AfterPersistentEarn(int slot, int scrapGained)
	{
		AddScrapCopy(slot, scrapGained);
		MirrorUnlocks(slot);
	}
}
