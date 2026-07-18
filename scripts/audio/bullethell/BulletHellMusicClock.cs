using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Advances through a beat map using MusicService playback position (no dt drift).
/// </summary>
public sealed class BulletHellMusicClock
{
	private readonly BulletHellBeatMap _map;
	private int _onsetCursor;
	private int _beatCursor;
	private float _lastPlayback;
	private bool _started;

	public BulletHellMusicClock(BulletHellBeatMap map)
	{
		_map = map;
	}

	public BulletHellBeatMap Map => _map;
	public float PlaybackSeconds { get; private set; }
	public bool TrackEnded => PlaybackSeconds >= _map.DurationSeconds - 0.05f;
	public float Intensity => _map.IntensityAt(PlaybackSeconds);
	public BulletHellSection Section => _map.SectionAt(PlaybackSeconds);

	public void Reset()
	{
		_onsetCursor = 0;
		_beatCursor = 0;
		_lastPlayback = 0f;
		PlaybackSeconds = 0f;
		_started = false;
	}

	/// <summary>Pull absolute playback time from MusicService and collect due musical events.</summary>
	public void Sync(List<BulletHellOnset> dueOnsets, List<float> dueBeats)
	{
		dueOnsets.Clear();
		dueBeats.Clear();

		if (!MusicService.IsPlayingPath(_map.SourcePath))
		{
			if (_started)
				PlaybackSeconds = Math.Min(_map.DurationSeconds, PlaybackSeconds);
			return;
		}

		var pos = MusicService.GetPlaybackPosition();
		// Guard against crossfade / restart blips.
		if (_started && pos + 0.35f < _lastPlayback)
		{
			_onsetCursor = 0;
			_beatCursor = 0;
		}

		_started = true;
		_lastPlayback = pos;
		PlaybackSeconds = pos;

		while (_onsetCursor < _map.Onsets.Count && _map.Onsets[_onsetCursor].Time <= pos + 0.02f)
		{
			dueOnsets.Add(_map.Onsets[_onsetCursor]);
			_onsetCursor++;
		}

		while (_beatCursor < _map.BeatTimes.Count && _map.BeatTimes[_beatCursor] <= pos + 0.02f)
		{
			dueBeats.Add(_map.BeatTimes[_beatCursor]);
			_beatCursor++;
		}
	}
}
