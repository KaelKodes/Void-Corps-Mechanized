using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Mechanize;

/// <summary>
/// Offline WAV analyzer: onset detection, BPM estimate, intensity curve, section splits.
/// Results are cached under user:// so combat only reads the beat map.
/// </summary>
public static class WavMusicAnalyzer
{
	private const string CacheDir = "user://cache/bullethell";
	private const int AnalysisHop = 512;
	private const float IntensityStep = 0.05f;
	private const float MinBpm = 70f;
	private const float MaxBpm = 180f;

	public static BulletHellBeatMap AnalyzeOrLoad(string resPath, bool forceReanalyze = false)
	{
		if (string.IsNullOrWhiteSpace(resPath))
			throw new ArgumentException("WAV path required.", nameof(resPath));

		if (!TryReadPcm(resPath, out var fileBytes, out var sourceLabel))
			throw new FileNotFoundException($"Bullet-hell track missing: {resPath}");

		if (fileBytes == null || fileBytes.Length < 44)
			throw new InvalidDataException($"Invalid WAV: {resPath}");

		var cachePath = CachePathFor(resPath, fileBytes.LongLength);
		if (!forceReanalyze && TryLoadCache(cachePath, resPath, fileBytes.LongLength, out var cached))
		{
			GD.Print($"WavMusicAnalyzer: cache hit ({cached.Onsets.Count} onsets, {cached.Bpm:0.0} BPM) — {resPath}");
			return cached;
		}

		GD.Print($"WavMusicAnalyzer: analyzing {sourceLabel} ({fileBytes.Length / (1024f * 1024f):0.0} MB)...");
		var map = AnalyzeBytes(resPath, fileBytes);
		SaveCache(cachePath, map);
		GD.Print(
			$"WavMusicAnalyzer: done — {map.DurationSeconds:0.0}s, {map.Bpm:0.0} BPM " +
			$"(conf {map.Confidence:0.00}), {map.Onsets.Count} onsets, {map.Sections.Count} sections.");
		return map;
	}

	/// <summary>
	/// Exported builds only ship the imported AudioStreamWAV — not the raw source file.
	/// Prefer ResourceLoader PCM; fall back to filesystem bytes in the editor.
	/// </summary>
	private static bool TryReadPcm(string resPath, out byte[] fileBytes, out string sourceLabel)
	{
		fileBytes = Array.Empty<byte>();
		sourceLabel = resPath;

		if (ResourceLoader.Exists(resPath))
		{
			var stream = ResourceLoader.Load<AudioStreamWav>(resPath);
			if (stream != null)
			{
				if (stream.Format != AudioStreamWav.FormatEnum.Format16Bits)
				{
					GD.PushWarning(
						$"WavMusicAnalyzer: {resPath} is imported as {stream.Format}. " +
						"Reimport with Compress Mode = Disabled for beat analysis.");
				}
				else if (stream.Data is { Length: > 0 } pcm)
				{
					fileBytes = WrapPcmAsWav(pcm, stream.MixRate, stream.Stereo ? 2 : 1);
					sourceLabel = $"{resPath} (imported)";
					return true;
				}
			}
		}

		var abs = ProjectSettings.GlobalizePath(resPath);
		if (File.Exists(abs))
		{
			fileBytes = File.ReadAllBytes(abs);
			sourceLabel = abs;
			return true;
		}

		if (FileAccess.FileExists(resPath))
		{
			fileBytes = FileAccess.GetFileAsBytes(resPath);
			return fileBytes is { Length: > 0 };
		}

		return false;
	}

	private static byte[] WrapPcmAsWav(byte[] pcm, int sampleRate, int channels)
	{
		// AnalyzeBytes expects a full RIFF/WAVE container.
		var dataSize = pcm.Length;
		var file = new byte[44 + dataSize];
		Encoding.ASCII.GetBytes("RIFF").CopyTo(file, 0);
		BitConverter.GetBytes(36 + dataSize).CopyTo(file, 4);
		Encoding.ASCII.GetBytes("WAVE").CopyTo(file, 8);
		Encoding.ASCII.GetBytes("fmt ").CopyTo(file, 12);
		BitConverter.GetBytes(16).CopyTo(file, 16); // PCM fmt chunk size
		BitConverter.GetBytes((short)1).CopyTo(file, 20); // PCM
		BitConverter.GetBytes((short)channels).CopyTo(file, 22);
		BitConverter.GetBytes(sampleRate).CopyTo(file, 24);
		var byteRate = sampleRate * channels * 2;
		BitConverter.GetBytes(byteRate).CopyTo(file, 28);
		BitConverter.GetBytes((short)(channels * 2)).CopyTo(file, 32);
		BitConverter.GetBytes((short)16).CopyTo(file, 34);
		Encoding.ASCII.GetBytes("data").CopyTo(file, 36);
		BitConverter.GetBytes(dataSize).CopyTo(file, 40);
		Buffer.BlockCopy(pcm, 0, file, 44, dataSize);
		return file;
	}

	public static BulletHellBeatMap AnalyzeBytes(string sourcePath, byte[] fileBytes)
	{
		ParseWav(fileBytes, out var sampleRate, out var channels, out var bits, out var pcmOffset, out var dataBytes);
		if (bits != 16)
			throw new InvalidDataException($"Only 16-bit PCM WAV supported (got {bits}-bit).");
		if (channels is < 1 or > 2)
			throw new InvalidDataException($"Unsupported channel count: {channels}");

		var sampleCount = dataBytes / (bits / 8) / channels;
		var mono = new float[sampleCount];
		var cursor = pcmOffset;
		for (var i = 0; i < sampleCount; i++)
		{
			float sample;
			if (channels == 1)
			{
				sample = BitConverter.ToInt16(fileBytes, cursor) / 32768f;
				cursor += 2;
			}
			else
			{
				var l = BitConverter.ToInt16(fileBytes, cursor) / 32768f;
				var r = BitConverter.ToInt16(fileBytes, cursor + 2) / 32768f;
				sample = 0.5f * (l + r);
				cursor += 4;
			}

			mono[i] = sample;
		}

		var hop = AnalysisHop;
		var frameCount = Math.Max(1, (mono.Length - hop) / hop);
		var lowEnergy = new float[frameCount];
		var midEnergy = new float[frameCount];
		var highEnergy = new float[frameCount];
		var flux = new float[frameCount];

		float prevL = 0f, prevM = 0f, prevH = 0f;
		for (var f = 0; f < frameCount; f++)
		{
			var start = f * hop;
			double eL = 0, eM = 0, eH = 0;
			// Cheap band split via differencing / smoothing proxies (no full FFT).
			float lp = 0f, bp = 0f;
			for (var i = 0; i < hop && start + i < mono.Length; i++)
			{
				var x = mono[start + i];
				lp += 0.08f * (x - lp);
				var hp = x - lp;
				bp += 0.22f * (hp - bp);
				var treble = hp - bp;
				eL += lp * lp;
				eM += bp * bp;
				eH += treble * treble;
			}

			var inv = 1.0 / hop;
			var l = (float)Math.Sqrt(eL * inv);
			var m = (float)Math.Sqrt(eM * inv);
			var h = (float)Math.Sqrt(eH * inv);
			lowEnergy[f] = l;
			midEnergy[f] = m;
			highEnergy[f] = h;

			var dL = Math.Max(0f, l - prevL);
			var dM = Math.Max(0f, m - prevM);
			var dH = Math.Max(0f, h - prevH);
			flux[f] = dL * 1.35f + dM * 1.0f + dH * 0.75f;
			prevL = l;
			prevM = m;
			prevH = h;
		}

		SmoothInPlace(flux, 2);
		var onsets = DetectOnsets(flux, lowEnergy, midEnergy, highEnergy, sampleRate, hop);
		EstimateBpm(flux, sampleRate, hop, out var bpm, out var confidence, out var phaseFrames);
		var beatPeriod = 60f / Math.Max(1f, bpm);
		var firstBeat = phaseFrames * hop / (float)sampleRate;
		var beatTimes = BuildBeatGrid(firstBeat, beatPeriod, sampleCount / (float)sampleRate);
		MarkDownbeats(onsets, beatTimes, beatPeriod);

		var duration = sampleCount / (float)sampleRate;
		BuildIntensity(mono, sampleRate, duration, out var intensityTimes, out var intensityValues);
		var sections = BuildSections(intensityTimes, intensityValues, duration);

		return new BulletHellBeatMap
		{
			SourcePath = sourcePath,
			SourceBytes = fileBytes.LongLength,
			SampleRate = sampleRate,
			DurationSeconds = duration,
			Bpm = bpm,
			BeatPeriodSeconds = beatPeriod,
			FirstBeatSeconds = firstBeat,
			Confidence = confidence,
			BeatTimes = beatTimes,
			Onsets = onsets,
			IntensityTimes = intensityTimes,
			IntensityValues = intensityValues,
			Sections = sections
		};
	}

	private static List<BulletHellOnset> DetectOnsets(
		float[] flux,
		float[] lowEnergy,
		float[] midEnergy,
		float[] highEnergy,
		int sampleRate,
		int hop)
	{
		var mean = 0f;
		for (var i = 0; i < flux.Length; i++)
			mean += flux[i];
		mean /= Math.Max(1, flux.Length);

		var variance = 0f;
		for (var i = 0; i < flux.Length; i++)
		{
			var d = flux[i] - mean;
			variance += d * d;
		}
		variance /= Math.Max(1, flux.Length);
		var std = MathF.Sqrt(Math.Max(1e-8f, variance));

		var threshold = mean + std * 1.15f;
		var minGapFrames = Math.Max(2, (int)(0.08f * sampleRate / hop));
		var onsets = new List<BulletHellOnset>(flux.Length / 8);
		var last = -minGapFrames;

		for (var f = 1; f < flux.Length - 1; f++)
		{
			if (flux[f] < threshold)
				continue;
			if (flux[f] < flux[f - 1] || flux[f] < flux[f + 1])
				continue;
			if (f - last < minGapFrames)
				continue;

			last = f;
			var time = f * hop / (float)sampleRate;
			var strength = Math.Clamp((flux[f] - mean) / (std * 3f + 1e-5f), 0.05f, 1f);
			var l = lowEnergy[f];
			var m = midEnergy[f];
			var h = highEnergy[f];
			var band = PickBand(l, m, h);
			onsets.Add(new BulletHellOnset(time, strength, band));
		}

		// Keep the densest map manageable for combat scheduling.
		if (onsets.Count > 900)
		{
			onsets.Sort((a, b) => b.Strength.CompareTo(a.Strength));
			onsets.RemoveRange(900, onsets.Count - 900);
			onsets.Sort((a, b) => a.Time.CompareTo(b.Time));
		}

		return onsets;
	}

	private static BulletHellBand PickBand(float l, float m, float h)
	{
		var max = Math.Max(l, Math.Max(m, h));
		if (max < 1e-5f)
			return BulletHellBand.Broadband;
		if (l >= m && l >= h && l > max * 0.72f)
			return BulletHellBand.Low;
		if (h >= m && h >= l && h > max * 0.72f)
			return BulletHellBand.High;
		if (m >= l && m >= h)
			return BulletHellBand.Mid;
		return BulletHellBand.Broadband;
	}

	private static void EstimateBpm(
		float[] flux,
		int sampleRate,
		int hop,
		out float bpm,
		out float confidence,
		out int phaseFrames)
	{
		var frameRate = sampleRate / (float)hop;
		var minLag = (int)(frameRate * 60f / MaxBpm);
		var maxLag = (int)(frameRate * 60f / MinBpm);
		minLag = Math.Clamp(minLag, 2, flux.Length / 3);
		maxLag = Math.Clamp(maxLag, minLag + 2, flux.Length / 2);

		var bestLag = minLag;
		var bestScore = float.MinValue;
		var scores = new float[maxLag + 1];

		for (var lag = minLag; lag <= maxLag; lag++)
		{
			double sum = 0;
			var count = flux.Length - lag;
			for (var i = 0; i < count; i++)
				sum += flux[i] * flux[i + lag];
			var score = (float)(sum / Math.Max(1, count));
			scores[lag] = score;
			if (score > bestScore)
			{
				bestScore = score;
				bestLag = lag;
			}
		}

		// Prefer a lag whose double/half also correlate (tempo octave guard).
		var guardedLag = bestLag;
		var guardedScore = bestScore;
		for (var lag = minLag; lag <= maxLag; lag++)
		{
			var half = lag / 2;
			var dbl = lag * 2;
			var bonus = scores[lag];
			if (half >= minLag && half <= maxLag)
				bonus += scores[half] * 0.35f;
			if (dbl >= minLag && dbl <= maxLag)
				bonus += scores[dbl] * 0.35f;
			if (bonus > guardedScore)
			{
				guardedScore = bonus;
				guardedLag = lag;
			}
		}

		bestLag = guardedLag;
		bpm = 60f * frameRate / bestLag;
		bpm = Math.Clamp(bpm, MinBpm, MaxBpm);

		var mean = 0f;
		var n = maxLag - minLag + 1;
		for (var lag = minLag; lag <= maxLag; lag++)
			mean += scores[lag];
		mean /= Math.Max(1, n);
		confidence = Math.Clamp((bestScore - mean) / (mean + 1e-5f), 0f, 1f);

		// Phase: lag where aligning flux peaks to the beat grid maximizes energy.
		phaseFrames = 0;
		var bestPhase = float.MinValue;
		for (var phase = 0; phase < bestLag; phase++)
		{
			double sum = 0;
			var hits = 0;
			for (var f = phase; f < flux.Length; f += bestLag)
			{
				sum += flux[f];
				hits++;
			}

			var score = (float)(sum / Math.Max(1, hits));
			if (score > bestPhase)
			{
				bestPhase = score;
				phaseFrames = phase;
			}
		}
	}

	private static List<float> BuildBeatGrid(float firstBeat, float period, float duration)
	{
		var beats = new List<float>(Math.Max(16, (int)(duration / period) + 4));
		var t = firstBeat;
		if (t > period)
			t -= period * MathF.Floor(t / period);

		while (t < 0f)
			t += period;

		for (; t <= duration + 0.001f; t += period)
			beats.Add(t);
		return beats;
	}

	private static void MarkDownbeats(List<BulletHellOnset> onsets, List<float> beatTimes, float period)
	{
		if (onsets.Count == 0 || beatTimes.Count == 0)
			return;

		var window = period * 0.28f;
		for (var i = 0; i < onsets.Count; i++)
		{
			var o = onsets[i];
			var nearestBeat = FindNearest(beatTimes, o.Time);
			var onGrid = Math.Abs(nearestBeat - o.Time) <= window;
			var beatIndex = FindNearestIndex(beatTimes, nearestBeat);
			var downbeat = onGrid && beatIndex >= 0 && beatIndex % 4 == 0 && o.Strength >= 0.35f;
			if (!downbeat)
				continue;
			onsets[i] = new BulletHellOnset(o.Time, o.Strength, o.Band, isDownbeat: true);
		}
	}

	private static void BuildIntensity(
		float[] mono,
		int sampleRate,
		float duration,
		out List<float> times,
		out List<float> values)
	{
		var stepSamples = Math.Max(1, (int)(sampleRate * IntensityStep));
		times = new List<float>((int)(duration / IntensityStep) + 2);
		values = new List<float>(times.Capacity);

		float peak = 1e-6f;
		for (var start = 0; start + stepSamples <= mono.Length; start += stepSamples)
		{
			double sum = 0;
			for (var i = 0; i < stepSamples; i++)
			{
				var s = mono[start + i];
				sum += s * s;
			}

			var rms = (float)Math.Sqrt(sum / stepSamples);
			peak = Math.Max(peak, rms);
			times.Add(start / (float)sampleRate);
			values.Add(rms);
		}

		for (var i = 0; i < values.Count; i++)
			values[i] = Math.Clamp(values[i] / peak, 0f, 1f);

		SmoothList(values, 3);
	}

	private static List<BulletHellSection> BuildSections(List<float> times, List<float> values, float duration)
	{
		var sections = new List<BulletHellSection>();
		if (times.Count == 0)
		{
			sections.Add(new BulletHellSection(0f, duration, 0.35f, 0));
			return sections;
		}

		var window = Math.Max(4, (int)(2.5f / IntensityStep));
		var startIdx = 0;
		var sectionIndex = 0;
		var running = values[0];

		for (var i = window; i < values.Count; i++)
		{
			var local = 0f;
			for (var k = i - window; k <= i; k++)
				local += values[k];
			local /= window + 1;

			if (Math.Abs(local - running) < 0.18f && i < values.Count - 1)
				continue;

			var endIdx = i;
			var avg = 0f;
			for (var k = startIdx; k <= endIdx; k++)
				avg += values[k];
			avg /= Math.Max(1, endIdx - startIdx + 1);

			var startT = times[startIdx];
			var endT = times[Math.Min(endIdx, times.Count - 1)];
			if (endT - startT >= 4f)
			{
				sections.Add(new BulletHellSection(startT, endT, avg, sectionIndex++));
				startIdx = endIdx;
				running = local;
			}
		}

		if (startIdx < times.Count)
		{
			var avg = 0f;
			for (var k = startIdx; k < values.Count; k++)
				avg += values[k];
			avg /= Math.Max(1, values.Count - startIdx);
			sections.Add(new BulletHellSection(times[startIdx], duration, avg, sectionIndex));
		}

		if (sections.Count == 0)
			sections.Add(new BulletHellSection(0f, duration, 0.35f, 0));

		return sections;
	}

	private static float FindNearest(List<float> sorted, float value)
	{
		var idx = FindNearestIndex(sorted, value);
		return idx >= 0 ? sorted[idx] : value;
	}

	private static int FindNearestIndex(List<float> sorted, float value)
	{
		if (sorted.Count == 0)
			return -1;
		var lo = 0;
		var hi = sorted.Count - 1;
		while (hi - lo > 1)
		{
			var mid = (lo + hi) >> 1;
			if (sorted[mid] < value)
				lo = mid;
			else
				hi = mid;
		}

		return Math.Abs(sorted[lo] - value) <= Math.Abs(sorted[hi] - value) ? lo : hi;
	}

	private static void SmoothInPlace(float[] data, int passes)
	{
		if (data.Length < 3)
			return;
		var tmp = new float[data.Length];
		for (var p = 0; p < passes; p++)
		{
			tmp[0] = data[0];
			tmp[^1] = data[^1];
			for (var i = 1; i < data.Length - 1; i++)
				tmp[i] = (data[i - 1] + data[i] * 2f + data[i + 1]) * 0.25f;
			Array.Copy(tmp, data, data.Length);
		}
	}

	private static void SmoothList(List<float> data, int passes)
	{
		if (data.Count < 3)
			return;
		var tmp = new float[data.Count];
		for (var p = 0; p < passes; p++)
		{
			tmp[0] = data[0];
			tmp[^1] = data[^1];
			for (var i = 1; i < data.Count - 1; i++)
				tmp[i] = (data[i - 1] + data[i] * 2f + data[i + 1]) * 0.25f;
			for (var i = 0; i < data.Count; i++)
				data[i] = tmp[i];
		}
	}

	private static void ParseWav(
		byte[] bytes,
		out int sampleRate,
		out int channels,
		out int bitsPerSample,
		out int dataOffset,
		out int dataSize)
	{
		if (Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF"
		    || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
			throw new InvalidDataException("Not a RIFF/WAVE file.");

		sampleRate = 0;
		channels = 0;
		bitsPerSample = 0;
		dataOffset = 0;
		dataSize = 0;

		var pos = 12;
		while (pos + 8 <= bytes.Length)
		{
			var id = Encoding.ASCII.GetString(bytes, pos, 4);
			var size = BitConverter.ToInt32(bytes, pos + 4);
			var chunkData = pos + 8;
			if (id == "fmt " && size >= 16)
			{
				channels = BitConverter.ToInt16(bytes, chunkData + 2);
				sampleRate = BitConverter.ToInt32(bytes, chunkData + 4);
				bitsPerSample = BitConverter.ToInt16(bytes, chunkData + 14);
			}
			else if (id == "data")
			{
				dataOffset = chunkData;
				dataSize = size;
				break;
			}

			pos = chunkData + size + (size & 1);
		}

		if (sampleRate <= 0 || dataOffset <= 0 || dataSize <= 0)
			throw new InvalidDataException("WAV missing fmt/data chunks.");
	}

	private static string CachePathFor(string resPath, long byteLength)
	{
		var safe = resPath.Replace("res://", "").Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
		return $"{CacheDir}/{safe}.{byteLength}.json";
	}

	private static bool TryLoadCache(string cachePath, string resPath, long byteLength, out BulletHellBeatMap map)
	{
		map = null!;
		if (!FileAccess.FileExists(cachePath))
			return false;

		try
		{
			var json = FileAccess.GetFileAsString(cachePath);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.GetProperty("sourceBytes").GetInt64() != byteLength)
				return false;
			if (!string.Equals(root.GetProperty("sourcePath").GetString(), resPath, StringComparison.Ordinal))
				return false;

			map = Deserialize(root);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushWarning($"WavMusicAnalyzer: cache read failed ({ex.Message})");
			return false;
		}
	}

	private static void SaveCache(string cachePath, BulletHellBeatMap map)
	{
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(CacheDir));
		var json = Serialize(map);
		using var file = FileAccess.Open(cachePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushWarning($"WavMusicAnalyzer: could not write cache {cachePath}");
			return;
		}

		file.StoreString(json);
	}

	private static string Serialize(BulletHellBeatMap map)
	{
		var sb = new StringBuilder(map.Onsets.Count * 48 + 1024);
		sb.Append('{');
		AppendProp(sb, "sourcePath", map.SourcePath, first: true);
		sb.Append(",\"sourceBytes\":").Append(map.SourceBytes.ToString(CultureInfo.InvariantCulture));
		sb.Append(",\"sampleRate\":").Append(map.SampleRate.ToString(CultureInfo.InvariantCulture));
		sb.Append(",\"durationSeconds\":").Append(map.DurationSeconds.ToString("R", CultureInfo.InvariantCulture));
		sb.Append(",\"bpm\":").Append(map.Bpm.ToString("R", CultureInfo.InvariantCulture));
		sb.Append(",\"beatPeriodSeconds\":").Append(map.BeatPeriodSeconds.ToString("R", CultureInfo.InvariantCulture));
		sb.Append(",\"firstBeatSeconds\":").Append(map.FirstBeatSeconds.ToString("R", CultureInfo.InvariantCulture));
		sb.Append(",\"confidence\":").Append(map.Confidence.ToString("R", CultureInfo.InvariantCulture));

		sb.Append(",\"beatTimes\":[");
		for (var i = 0; i < map.BeatTimes.Count; i++)
		{
			if (i > 0) sb.Append(',');
			sb.Append(map.BeatTimes[i].ToString("R", CultureInfo.InvariantCulture));
		}

		sb.Append("],\"onsets\":[");
		for (var i = 0; i < map.Onsets.Count; i++)
		{
			if (i > 0) sb.Append(',');
			var o = map.Onsets[i];
			sb.Append("{\"t\":").Append(o.Time.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(",\"s\":").Append(o.Strength.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(",\"b\":").Append(((int)o.Band).ToString(CultureInfo.InvariantCulture));
			sb.Append(",\"d\":").Append(o.IsDownbeat ? "true" : "false");
			sb.Append('}');
		}

		sb.Append("],\"intensityTimes\":[");
		for (var i = 0; i < map.IntensityTimes.Count; i++)
		{
			if (i > 0) sb.Append(',');
			sb.Append(map.IntensityTimes[i].ToString("R", CultureInfo.InvariantCulture));
		}

		sb.Append("],\"intensityValues\":[");
		for (var i = 0; i < map.IntensityValues.Count; i++)
		{
			if (i > 0) sb.Append(',');
			sb.Append(map.IntensityValues[i].ToString("R", CultureInfo.InvariantCulture));
		}

		sb.Append("],\"sections\":[");
		for (var i = 0; i < map.Sections.Count; i++)
		{
			if (i > 0) sb.Append(',');
			var s = map.Sections[i];
			sb.Append("{\"a\":").Append(s.StartTime.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(",\"b\":").Append(s.EndTime.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(",\"i\":").Append(s.AvgIntensity.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(",\"n\":").Append(s.Index.ToString(CultureInfo.InvariantCulture));
			sb.Append('}');
		}

		sb.Append("]}");
		return sb.ToString();
	}

	private static void AppendProp(StringBuilder sb, string key, string value, bool first)
	{
		if (!first) sb.Append(',');
		sb.Append('"').Append(key).Append("\":\"");
		foreach (var ch in value)
		{
			if (ch is '"' or '\\')
				sb.Append('\\');
			sb.Append(ch);
		}

		sb.Append('"');
	}

	private static BulletHellBeatMap Deserialize(JsonElement root)
	{
		var beatTimes = new List<float>();
		foreach (var v in root.GetProperty("beatTimes").EnumerateArray())
			beatTimes.Add(v.GetSingle());

		var onsets = new List<BulletHellOnset>();
		foreach (var o in root.GetProperty("onsets").EnumerateArray())
		{
			onsets.Add(new BulletHellOnset(
				o.GetProperty("t").GetSingle(),
				o.GetProperty("s").GetSingle(),
				(BulletHellBand)o.GetProperty("b").GetInt32(),
				o.GetProperty("d").GetBoolean()));
		}

		var intensityTimes = new List<float>();
		foreach (var v in root.GetProperty("intensityTimes").EnumerateArray())
			intensityTimes.Add(v.GetSingle());

		var intensityValues = new List<float>();
		foreach (var v in root.GetProperty("intensityValues").EnumerateArray())
			intensityValues.Add(v.GetSingle());

		var sections = new List<BulletHellSection>();
		foreach (var s in root.GetProperty("sections").EnumerateArray())
		{
			sections.Add(new BulletHellSection(
				s.GetProperty("a").GetSingle(),
				s.GetProperty("b").GetSingle(),
				s.GetProperty("i").GetSingle(),
				s.GetProperty("n").GetInt32()));
		}

		return new BulletHellBeatMap
		{
			SourcePath = root.GetProperty("sourcePath").GetString() ?? "",
			SourceBytes = root.GetProperty("sourceBytes").GetInt64(),
			SampleRate = root.GetProperty("sampleRate").GetInt32(),
			DurationSeconds = root.GetProperty("durationSeconds").GetSingle(),
			Bpm = root.GetProperty("bpm").GetSingle(),
			BeatPeriodSeconds = root.GetProperty("beatPeriodSeconds").GetSingle(),
			FirstBeatSeconds = root.GetProperty("firstBeatSeconds").GetSingle(),
			Confidence = root.GetProperty("confidence").GetSingle(),
			BeatTimes = beatTimes,
			Onsets = onsets,
			IntensityTimes = intensityTimes,
			IntensityValues = intensityValues,
			Sections = sections
		};
	}
}
