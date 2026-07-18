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

	/// <summary>Absolute playback seconds on the active (incoming) player.</summary>
	public static float GetPlaybackPosition()
	{
		var player = Instance?.ActivePlayer();
		return player?.Playing == true ? player.GetPlaybackPosition() : 0f;
	}

	public static bool IsPlaying => Instance?.IsAnythingPlaying() ?? false;

	public static string CurrentPath => Instance?._currentPath ?? "";

	public static bool IsPlayingPath(string path)
	{
		if (Instance == null || string.IsNullOrEmpty(path))
			return false;
		return Instance.IsAnythingPlaying()
		       && string.Equals(Instance._currentPath, path, System.StringComparison.OrdinalIgnoreCase);
	}

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

	/// <summary>Play any res:// audio path (bullet-hell tracks, one-shots, etc.).</summary>
	public static void CueAbsolute(string resPath, bool loop = true) =>
		Instance?.CueAbsoluteInternal(resPath, loop);

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
		CrossfadeTo(path, loop: true);
	}

	private void CueTrackInternal(string fileName)
	{
		var path = fileName.StartsWith("res://", System.StringComparison.OrdinalIgnoreCase)
			? fileName
			: fileName.StartsWith(SoundtrackDir, System.StringComparison.OrdinalIgnoreCase)
				? fileName
				: $"{SoundtrackDir}/{fileName}";

		if (_currentPath == path && IsAnythingPlaying())
			return;

		// Explicit track choice: clear the surface cue so the next Cue() still crossfades.
		_currentCue = MusicCue.None;
		CrossfadeTo(path, loop: true);
	}

	private void CueAbsoluteInternal(string resPath, bool loop)
	{
		if (string.IsNullOrWhiteSpace(resPath))
			return;

		if (_currentPath == resPath && IsAnythingPlaying())
			return;

		_currentCue = MusicCue.None;
		CrossfadeTo(resPath, loop);
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

	private AudioStreamPlayer? ActivePlayer()
	{
		var incoming = _usingA ? _a : _b;
		if (incoming?.Playing == true)
			return incoming;
		var other = _usingA ? _b : _a;
		return other?.Playing == true ? other : incoming;
	}

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

	private void CrossfadeTo(string path, bool loop)
	{
		var stream = LoadStream(path, loop);
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

	private static AudioStream? LoadStream(string path, bool loop)
	{
		// Prefer imported resource when Godot has generated one.
		if (ResourceLoader.Exists(path))
		{
			var res = ResourceLoader.Load<AudioStream>(path);
			if (res != null)
			{
				ApplyLoop(res, loop);
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
			var mp3 = new AudioStreamMP3 { Data = bytes, Loop = loop };
			return mp3;
		}

		if (path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase))
		{
			var ogg = ResourceLoader.Load<AudioStream>(path);
			if (ogg != null)
				ApplyLoop(ogg, loop);
			return ogg;
		}

		if (path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
		{
			var wav = LoadWavFromBytes(bytes, loop);
			if (wav != null)
				return wav;
		}

		return null;
	}

	private static AudioStreamWav? LoadWavFromBytes(byte[] bytes, bool loop)
	{
		try
		{
			if (bytes.Length < 44)
				return null;

			var channels = System.BitConverter.ToInt16(bytes, 22);
			var sampleRate = System.BitConverter.ToInt32(bytes, 24);
			var bits = System.BitConverter.ToInt16(bytes, 34);
			if (bits != 16)
				return null;

			var pos = 12;
			var dataOffset = -1;
			var dataSize = 0;
			while (pos + 8 <= bytes.Length)
			{
				var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
				var size = System.BitConverter.ToInt32(bytes, pos + 4);
				var chunk = pos + 8;
				if (id == "data")
				{
					dataOffset = chunk;
					dataSize = size;
					break;
				}

				pos = chunk + size + (size & 1);
			}

			if (dataOffset < 0 || dataSize <= 0 || dataOffset + dataSize > bytes.Length)
				return null;

			var pcm = new byte[dataSize];
			System.Buffer.BlockCopy(bytes, dataOffset, pcm, 0, dataSize);

			var wav = new AudioStreamWav
			{
				Data = pcm,
				Format = AudioStreamWav.FormatEnum.Format16Bits,
				MixRate = sampleRate,
				Stereo = channels > 1,
				LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled
			};
			return wav;
		}
		catch (System.Exception ex)
		{
			GD.PushWarning($"MusicService: WAV load failed ({ex.Message})");
			return null;
		}
	}

	private static void ApplyLoop(AudioStream stream, bool loop)
	{
		switch (stream)
		{
			case AudioStreamMP3 mp3:
				mp3.Loop = loop;
				break;
			case AudioStreamOggVorbis ogg:
				ogg.Loop = loop;
				break;
			case AudioStreamWav wav:
				wav.LoopMode = loop
					? AudioStreamWav.LoopModeEnum.Forward
					: AudioStreamWav.LoopModeEnum.Disabled;
				break;
		}
	}
}
