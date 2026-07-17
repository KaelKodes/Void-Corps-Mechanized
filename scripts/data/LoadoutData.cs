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
}
