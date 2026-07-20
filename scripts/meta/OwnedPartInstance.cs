using System;
using Godot;

namespace Mechanize;

/// <summary>One physical owned part copy with independent integrity.</summary>
public sealed class OwnedPartInstance
{
	public string InstanceId { get; set; } = "";
	public string PartId { get; set; } = "";
	public PartCondition Condition { get; set; } = PartCondition.Full();

	/// <summary>Reserved for an in-mission delivery / field crate (still owned, not free to equip elsewhere).</summary>
	public bool Reserved { get; set; }

	public static OwnedPartInstance Create(string partId, PartCondition? condition = null)
	{
		var part = GameCatalog.GetPart(partId);
		var segs = PartCondition.SegmentCountFor(part);
		return new OwnedPartInstance
		{
			InstanceId = Guid.NewGuid().ToString("N"),
			PartId = partId,
			Condition = condition?.Clone() ?? PartCondition.Full(segs),
			Reserved = false
		};
	}

	public OwnedPartInstance Clone() => new()
	{
		InstanceId = InstanceId,
		PartId = PartId,
		Condition = Condition.Clone(),
		Reserved = Reserved
	};

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["id"] = InstanceId,
		["part"] = PartId,
		["reserved"] = Reserved,
		["condition"] = Condition.ToDict()
	};

	public static OwnedPartInstance FromDict(Godot.Collections.Dictionary dict)
	{
		var partId = dict.ContainsKey("part") ? dict["part"].AsString() : "";
		var instance = Create(partId);
		if (dict.ContainsKey("id"))
			instance.InstanceId = dict["id"].AsString();
		if (dict.ContainsKey("reserved"))
			instance.Reserved = dict["reserved"].AsBool();
		if (dict.ContainsKey("condition"))
			instance.Condition = PartCondition.FromDict(dict["condition"].AsGodotDictionary());
		instance.Condition.EnsureSegmentCount(PartCondition.SegmentCountFor(GameCatalog.GetPart(partId)));
		return instance;
	}
}
