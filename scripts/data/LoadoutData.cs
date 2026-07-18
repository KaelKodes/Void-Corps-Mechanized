using Godot;

namespace Mechanize;

[GlobalClass]
public partial class LoadoutData : Resource
{
	[Export] public string LegsId { get; set; } = "";
	[Export] public string TorsoId { get; set; } = "";
	[Export] public string HeadId { get; set; } = "";
	[Export] public string PowerCoreId { get; set; } = "";
	[Export] public string WeaponLId { get; set; } = "";
	[Export] public string WeaponRId { get; set; } = "";
	[Export] public string ShoulderLId { get; set; } = "";
	[Export] public string ShoulderRId { get; set; } = "";
	[Export] public string BackpackId { get; set; } = "";
	[Export] public string SystemsId { get; set; } = "";

	public string GetPartId(PartSlot slot) => slot switch
	{
		PartSlot.Legs => LegsId,
		PartSlot.Torso => TorsoId,
		PartSlot.Head => HeadId,
		PartSlot.PowerCore => PowerCoreId,
		PartSlot.WeaponL => WeaponLId,
		PartSlot.WeaponR => WeaponRId,
		PartSlot.ShoulderL => ShoulderLId,
		PartSlot.ShoulderR => ShoulderRId,
		PartSlot.Backpack => BackpackId,
		PartSlot.Systems => SystemsId,
		_ => ""
	};

	public void SetPartId(PartSlot slot, string id)
	{
		switch (slot)
		{
			case PartSlot.Legs: LegsId = id; break;
			case PartSlot.Torso: TorsoId = id; break;
			case PartSlot.Head: HeadId = id; break;
			case PartSlot.PowerCore: PowerCoreId = id; break;
			case PartSlot.WeaponL: WeaponLId = id; break;
			case PartSlot.WeaponR: WeaponRId = id; break;
			case PartSlot.ShoulderL: ShoulderLId = id; break;
			case PartSlot.ShoulderR: ShoulderRId = id; break;
			case PartSlot.Backpack: BackpackId = id; break;
			case PartSlot.Systems: SystemsId = id; break;
		}
	}

	public LoadoutData Clone()
	{
		return new LoadoutData
		{
			LegsId = LegsId,
			TorsoId = TorsoId,
			HeadId = HeadId,
			PowerCoreId = PowerCoreId,
			WeaponLId = WeaponLId,
			WeaponRId = WeaponRId,
			ShoulderLId = ShoulderLId,
			ShoulderRId = ShoulderRId,
			BackpackId = BackpackId,
			SystemsId = SystemsId
		};
	}

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["legs"] = LegsId,
		["torso"] = TorsoId,
		["head"] = HeadId,
		["core"] = PowerCoreId,
		["weapon_l"] = WeaponLId,
		["weapon_r"] = WeaponRId,
		["shoulder_l"] = ShoulderLId,
		["shoulder_r"] = ShoulderRId,
		["backpack"] = BackpackId,
		["systems"] = SystemsId
	};

	public static LoadoutData FromDict(Godot.Collections.Dictionary dict)
	{
		var loadout = new LoadoutData();
		if (dict.ContainsKey("legs")) loadout.LegsId = dict["legs"].AsString();
		if (dict.ContainsKey("torso")) loadout.TorsoId = dict["torso"].AsString();
		if (dict.ContainsKey("head")) loadout.HeadId = dict["head"].AsString();
		if (dict.ContainsKey("core")) loadout.PowerCoreId = dict["core"].AsString();
		if (dict.ContainsKey("weapon_l")) loadout.WeaponLId = dict["weapon_l"].AsString();
		if (dict.ContainsKey("weapon_r")) loadout.WeaponRId = dict["weapon_r"].AsString();
		if (dict.ContainsKey("shoulder_l")) loadout.ShoulderLId = dict["shoulder_l"].AsString();
		if (dict.ContainsKey("shoulder_r")) loadout.ShoulderRId = dict["shoulder_r"].AsString();
		if (dict.ContainsKey("backpack")) loadout.BackpackId = dict["backpack"].AsString();
		if (dict.ContainsKey("systems")) loadout.SystemsId = dict["systems"].AsString();
		return GameCatalog.SanitizeMounts(loadout);
	}
}
