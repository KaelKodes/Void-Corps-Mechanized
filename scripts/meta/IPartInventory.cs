namespace Mechanize;

/// <summary>
/// Run kit bag. Today the profile holds a single bag (<c>pilot</c>);
/// later this becomes per-character with trade between bags.
/// </summary>
public interface IPartInventory
{
	string InventoryId { get; }
	int Scrap { get; set; }
	int LivesBank { get; set; }
	LoadoutData Loadout { get; set; }
	int OwnedCount(string partId);
	bool Owns(string partId);
	void Own(string partId, int amount = 1);
	bool TryRemoveOwned(string partId, int amount = 1);
	void GrantLoadoutOwnership(LoadoutData loadout);
	void EnforceOwnedEquipLimits(LoadoutData loadout);
	void WipeRunInventory();
}
