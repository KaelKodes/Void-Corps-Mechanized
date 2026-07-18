using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum MusicCue
{
	None,
	Menu,
	Hangar,
	Combat,
	Results,
	Campaign
}

/// <summary>
/// Soundtrack player. Most cues pick a random track; menu is locked to Black Nova.
/// </summary>
public partial class MusicService : Node
{
	private const string SoundtrackDir = "res://audio/soundtrack";
	private const string MenuTrackPath = "res://audio/soundtrack/Black Nova.mp3";
	private const float TargetVolumeDb = -10f;
	private const float SilentDb = -40f;
	private const float CrossfadeSeconds = 1.6f;

	private readonly List<string> _tracks = new();
	private readonly RandomNumberGenerator _rng = new();

	private AudioStreamPlayer? _a;
	private AudioStreamPlayer? _b;
	private bool _usingA = true;
	private Tween? _fade;
	private string _currentPath = "";
	private MusicCue _currentCue = MusicCue.None;

	public static MusicService? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		_rng.Randomize();
		DiscoverTracks();

		_a = MakePlayer("MusicA");
		_b = MakePlayer("MusicB");
		AddChild(_a);
		AddChild(_b);
	}

	private AudioStreamPlayer MakePlayer(string name) => new()
	{
		Name = name,
		Bus = "Music",
		VolumeDb = SilentDb,
		Autoplay = false
	};

	private void DiscoverTracks()
	{
		_tracks.Clear();
		using var dir = DirAccess.Open(SoundtrackDir);
		if (dir == null)
		{
			GD.PushWarning($"MusicService: missing {SoundtrackDir}");
			return;
		}

		dir.ListDirBegin();
		while (true)
		{
			var file = dir.GetNext();
			if (string.IsNullOrEmpty(file))
				break;
			if (dir.CurrentIsDir())
				continue;
			if (!file.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
			    && !file.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase)
			    && !file.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
				continue;
			_tracks.Add($"{SoundtrackDir}/{file}");
		}

		dir.ListDirEnd();
		_tracks.Sort();
		GD.Print($"MusicService: {_tracks.Count} soundtrack track(s).");
	}

	public static void Cue(MusicCue cue) => Instance?.CueInternal(cue);

	/// <summary>Play a specific soundtrack file (by name, e.g. "Derelict Vessel.mp3"), bypassing random cue selection.</summary>
	public static void CueTrack(string fileName) => Instance?.CueTrackInternal(fileName);

	public static void Stop(float fadeSeconds = 0.8f) => Instance?.StopInternal(fadeSeconds);

	private void CueInternal(MusicCue cue)
	{
		if (_tracks.Count == 0)
			return;

		// Same surface already playing — leave it alone.
		if (cue == _currentCue && IsAnythingPlaying())
			return;

		_currentCue = cue;
		var path = PickPathForCue(cue, avoid: _currentPath);
		CrossfadeTo(path);
	}

	private void CueTrackInternal(string fileName)
	{
		var path = fileName.StartsWith(SoundtrackDir, System.StringComparison.OrdinalIgnoreCase)
			? fileName
			: $"{SoundtrackDir}/{fileName}";

		if (_currentPath == path && IsAnythingPlaying())
			return;

		// Explicit track choice: clear the surface cue so the next Cue() still crossfades.
		_currentCue = MusicCue.None;
		CrossfadeTo(path);
	}

	private string PickPathForCue(MusicCue cue, string avoid)
	{
		if (cue == MusicCue.Menu)
		{
			if (_tracks.Contains(MenuTrackPath) || ResourceLoader.Exists(MenuTrackPath)
			    || Godot.FileAccess.FileExists(MenuTrackPath))
				return MenuTrackPath;

			GD.PushWarning($"MusicService: menu track missing ({MenuTrackPath}), falling back.");
		}

		return PickRandomPath(avoid);
	}

	private void StopInternal(float fadeSeconds)
	{
		_currentCue = MusicCue.None;
		_fade?.Kill();
		_fade = CreateTween();
		_fade.SetParallel(true);
		if (_a != null)
			_fade.TweenProperty(_a, "volume_db", SilentDb, fadeSeconds);
		if (_b != null)
			_fade.TweenProperty(_b, "volume_db", SilentDb, fadeSeconds);
		_fade.Finished += () =>
		{
			_a?.Stop();
			_b?.Stop();
			_currentPath = "";
		};
	}

	private bool IsAnythingPlaying() =>
		(_a?.Playing ?? false) || (_b?.Playing ?? false);

	private string PickRandomPath(string avoid)
	{
		if (_tracks.Count == 1)
			return _tracks[0];

		string pick;
		var guard = 0;
		do
		{
			pick = _tracks[(int)(_rng.Randi() % (uint)_tracks.Count)];
			guard++;
		} while (pick == avoid && guard < 12);

		return pick;
	}

	private void CrossfadeTo(string path)
	{
		var stream = LoadStream(path);
		if (stream == null)
			return;

		var incoming = _usingA ? _b : _a;
		var outgoing = _usingA ? _a : _b;
		if (incoming == null || outgoing == null)
			return;

		_usingA = !_usingA;
		_currentPath = path;

		incoming.Stream = stream;
		incoming.VolumeDb = SilentDb;
		incoming.Play();

		_fade?.Kill();
		_fade = CreateTween();
		_fade.SetParallel(true);
		_fade.TweenProperty(incoming, "volume_db", TargetVolumeDb, CrossfadeSeconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		if (outgoing.Playing)
		{
			_fade.TweenProperty(outgoing, "volume_db", SilentDb, CrossfadeSeconds)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.In);
			_fade.Chain().TweenCallback(Callable.From(() =>
			{
				if (outgoing != incoming)
					outgoing.Stop();
			}));
		}
	}

	private static AudioStream? LoadStream(string path)
	{
		// Prefer imported resource when Godot has generated one.
		if (ResourceLoader.Exists(path))
		{
			var res = ResourceLoader.Load<AudioStream>(path);
			if (res != null)
			{
				ApplyLoop(res);
				return res;
			}
		}

		if (!Godot.FileAccess.FileExists(path))
		{
			GD.PushWarning($"MusicService: file missing {path}");
			return null;
		}

		var bytes = Godot.FileAccess.GetFileAsBytes(path);
		if (bytes == null || bytes.Length == 0)
			return null;

		if (path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase))
		{
			var mp3 = new AudioStreamMP3 { Data = bytes, Loop = true };
			return mp3;
		}

		if (path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase))
		{
			var ogg = new AudioStreamOggVorbis();
			// OGG typically needs import; fall through if unavailable.
			return ResourceLoader.Load<AudioStream>(path);
		}

		return null;
	}

	private static void ApplyLoop(AudioStream stream)
	{
		switch (stream)
		{
			case AudioStreamMP3 mp3:
				mp3.Loop = true;
				break;
			case AudioStreamOggVorbis ogg:
				ogg.Loop = true;
				break;
			case AudioStreamWav wav:
				wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
				break;
		}
	}
}
