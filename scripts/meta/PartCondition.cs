using System;
using Godot;

namespace Mechanize;

/// <summary>
/// Normalized integrity for one owned part copy. Ratios survive catalog HP rebalances.
/// One segment per part (including legs — shared package pool).
/// Legacy multi-limb save arrays still load via <see cref="AverageRatio"/>.
/// </summary>
public sealed class PartCondition
{
	public const int MaxLimbSegments = 6;

	public float[] Segments { get; set; } = [1f];
	public bool Destroyed { get; set; }

	public static PartCondition Full(int segmentCount = 1)
	{
		var count = Mathf.Clamp(segmentCount, 1, MaxLimbSegments);
		var segments = new float[count];
		Array.Fill(segments, 1f);
		return new PartCondition { Segments = segments, Destroyed = false };
	}

	public static PartCondition DestroyedState(int segmentCount = 1)
	{
		var count = Mathf.Clamp(segmentCount, 1, MaxLimbSegments);
		return new PartCondition
		{
			Segments = new float[count],
			Destroyed = true
		};
	}

	public float AverageRatio
	{
		get
		{
			if (Segments.Length == 0)
				return Destroyed ? 0f : 1f;
			var sum = 0f;
			foreach (var s in Segments)
				sum += s;
			return Mathf.Clamp(sum / Segments.Length, 0f, 1f);
		}
	}

	public float MissingRatio => Mathf.Clamp(1f - AverageRatio, 0f, 1f);

	public bool IsFull => !Destroyed && AverageRatio >= 0.999f;

	public bool NeedsRepair => Destroyed || AverageRatio < 0.999f;

	public PartCondition Clone()
	{
		var copy = new float[Segments.Length];
		Array.Copy(Segments, copy, Segments.Length);
		return new PartCondition
		{
			Segments = copy,
			Destroyed = Destroyed
		};
	}

	public void SetFull()
	{
		for (var i = 0; i < Segments.Length; i++)
			Segments[i] = 1f;
		Destroyed = false;
	}

	public void EnsureSegmentCount(int count)
	{
		count = Mathf.Clamp(count, 1, MaxLimbSegments);
		if (Segments.Length == count)
			return;
		var next = new float[count];
		for (var i = 0; i < count; i++)
			next[i] = i < Segments.Length ? Segments[i] : AverageRatio;
		Segments = next;
	}

	public Godot.Collections.Dictionary ToDict()
	{
		var segs = new Godot.Collections.Array();
		foreach (var s in Segments)
			segs.Add(s);
		return new Godot.Collections.Dictionary
		{
			["destroyed"] = Destroyed,
			["segments"] = segs
		};
	}

	public static PartCondition FromDict(Godot.Collections.Dictionary dict)
	{
		var condition = Full();
		if (dict.ContainsKey("destroyed"))
			condition.Destroyed = dict["destroyed"].AsBool();
		if (dict.ContainsKey("segments"))
		{
			var arr = dict["segments"].AsGodotArray();
			if (arr.Count > 0)
			{
				condition.Segments = new float[Mathf.Min(arr.Count, MaxLimbSegments)];
				for (var i = 0; i < condition.Segments.Length; i++)
					condition.Segments[i] = Mathf.Clamp(arr[i].AsSingle(), 0f, 1f);
			}
		}

		if (condition.Destroyed)
		{
			for (var i = 0; i < condition.Segments.Length; i++)
				condition.Segments[i] = 0f;
		}

		return condition;
	}

	public static int SegmentCountFor(PartData? part) => 1;
}
