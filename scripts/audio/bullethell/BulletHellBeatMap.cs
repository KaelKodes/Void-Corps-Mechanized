using System;
using System.Collections.Generic;

namespace Mechanize;

public enum BulletHellBand
{
	Low = 0,
	Mid = 1,
	High = 2,
	Broadband = 3
}

public readonly struct BulletHellOnset
{
	public BulletHellOnset(float time, float strength, BulletHellBand band, bool isDownbeat = false)
	{
		Time = time;
		Strength = strength;
		Band = band;
		IsDownbeat = isDownbeat;
	}

	public float Time { get; }
	public float Strength { get; }
	public BulletHellBand Band { get; }
	public bool IsDownbeat { get; }
}

public readonly struct BulletHellSection
{
	public BulletHellSection(float startTime, float endTime, float avgIntensity, int index)
	{
		StartTime = startTime;
		EndTime = endTime;
		AvgIntensity = avgIntensity;
		Index = index;
	}

	public float StartTime { get; }
	public float EndTime { get; }
	public float AvgIntensity { get; }
	public int Index { get; }
}

/// <summary>
/// Precomputed musical event map for bullet-hell pattern scheduling.
/// Built offline from WAV PCM — not live spectrum analysis during combat.
/// </summary>
public sealed class BulletHellBeatMap
{
	public string SourcePath { get; init; } = "";
	public long SourceBytes { get; init; }
	public int SampleRate { get; init; }
	public float DurationSeconds { get; init; }
	public float Bpm { get; init; }
	public float BeatPeriodSeconds { get; init; }
	public float FirstBeatSeconds { get; init; }
	public float Confidence { get; init; }
	public IReadOnlyList<float> BeatTimes { get; init; } = Array.Empty<float>();
	public IReadOnlyList<BulletHellOnset> Onsets { get; init; } = Array.Empty<BulletHellOnset>();
	public IReadOnlyList<float> IntensityTimes { get; init; } = Array.Empty<float>();
	public IReadOnlyList<float> IntensityValues { get; init; } = Array.Empty<float>();
	public IReadOnlyList<BulletHellSection> Sections { get; init; } = Array.Empty<BulletHellSection>();

	public float IntensityAt(float timeSeconds)
	{
		if (IntensityTimes.Count == 0)
			return 0.35f;

		if (timeSeconds <= IntensityTimes[0])
			return IntensityValues[0];
		if (timeSeconds >= IntensityTimes[^1])
			return IntensityValues[^1];

		var lo = 0;
		var hi = IntensityTimes.Count - 1;
		while (hi - lo > 1)
		{
			var mid = (lo + hi) >> 1;
			if (IntensityTimes[mid] <= timeSeconds)
				lo = mid;
			else
				hi = mid;
		}

		var t0 = IntensityTimes[lo];
		var t1 = IntensityTimes[hi];
		var u = t1 > t0 ? (timeSeconds - t0) / (t1 - t0) : 0f;
		return IntensityValues[lo] + (IntensityValues[hi] - IntensityValues[lo]) * u;
	}

	public BulletHellSection SectionAt(float timeSeconds)
	{
		foreach (var section in Sections)
		{
			if (timeSeconds >= section.StartTime && timeSeconds < section.EndTime)
				return section;
		}

		return Sections.Count > 0
			? Sections[^1]
			: new BulletHellSection(0f, DurationSeconds, 0.35f, 0);
	}
}
