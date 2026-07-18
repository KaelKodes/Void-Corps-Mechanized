using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Autoload SFX player. File samples from res://audio/sfx override procedural synth clips.
/// </summary>
public partial class SfxService : Node
{
	private const string SfxDir = "res://audio/sfx";

	private readonly Dictionary<string, AudioStream> _clips = new();
	private readonly Dictionary<string, VoiceSlice> _voiceSlices = new();
	private AudioStream? _voicePack;
	private AudioStreamPlayer? _ui;
	private AudioStreamPlayer? _voice;
	private readonly List<AudioStreamPlayer> _pool = new();
	private int _poolIndex;
	private float _damageVoCooldown;
	private Tween? _voiceStopTween;

	private readonly struct VoiceSlice
	{
		public readonly float Start;
		public readonly float End;
		public VoiceSlice(float start, float end)
		{
			Start = start;
			End = end;
		}

		public float Duration => Mathf.Max(0.05f, End - Start);
	}

	public static SfxService? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		BakeAll();
		LoadFileSamples();
		RegisterVoicePack();

		_ui = new AudioStreamPlayer { Name = "UiPlayer", Bus = "Sfx", VolumeDb = -4f };
		AddChild(_ui);

		_voice = new AudioStreamPlayer { Name = "VoicePlayer", Bus = "Sfx", VolumeDb = -2f };
		AddChild(_voice);

		for (var i = 0; i < 12; i++)
		{
			var p = new AudioStreamPlayer { Name = $"Sfx_{i}", Bus = "Sfx", VolumeDb = -2f };
			AddChild(p);
			_pool.Add(p);
		}
	}

	public override void _Process(double delta)
	{
		if (_damageVoCooldown > 0f)
			_damageVoCooldown = Mathf.Max(0f, _damageVoCooldown - (float)delta);
	}

	private void BakeAll()
	{
		foreach (var id in new[]
		         {
			         "weapon_fire", "weapon_hit", "explosion", "ui_click", "ui_confirm",
			         "countdown", "fight", "victory", "defeat", "capture", "scrap", "alarm", "disk"
		         })
		{
			_clips[id] = SfxSynth.Bake(id);
		}
	}

	private void LoadFileSamples()
	{
		// Explicit mappings so renamed FreeSound dumps stay stable in code.
		TryOverride("ui_click", "freesound_community-mech-keyboard-02-102918.mp3");
		TryOverride("damage_sustained", "phatphrogstudio-robot-voice-damage-sustained-487076.mp3");
		TryOverride("drop_impact", "freesound_community-mech_step_001-87175.mp3");
	}

	private void RegisterVoicePack()
	{
		_voicePack = LoadAudioFile($"{SfxDir}/freesound_community-futuristic-robotic-voice-sentences-31272.mp3");
		if (_voicePack == null)
			return;

		// Timestamps from audio/sfx/futuristic robotic voices breakdown.txt
		_voiceSlices["standingby1"] = new VoiceSlice(0.0f, 1.40f);
		_voiceSlices["standingby2"] = new VoiceSlice(1.40f, 2.9f);
		_voiceSlices["access_denied1"] = new VoiceSlice(2.9f, 5.0f);
		_voiceSlices["access_denied2"] = new VoiceSlice(5.0f, 7.15f);
		_voiceSlices["access_granted1"] = new VoiceSlice(7.15f, 9.75f);
		_voiceSlices["access_granted2"] = new VoiceSlice(9.75f, 12.15f);
		_voiceSlices["keycard_required"] = new VoiceSlice(12.15f, 15.0f);
		_voiceSlices["warning1"] = new VoiceSlice(15.0f, 16.9f);
		_voiceSlices["warning2"] = new VoiceSlice(16.9f, 18.8f);
		_voiceSlices["core_overheat"] = new VoiceSlice(18.8f, 22.3f);
		_voiceSlices["core_meltdown"] = new VoiceSlice(22.3f, 25.45f);
		_voiceSlices["welcome"] = new VoiceSlice(25.45f, 26.95f);
	}

	private void TryOverride(string id, string fileName)
	{
		var stream = LoadAudioFile($"{SfxDir}/{fileName}");
		if (stream != null)
			_clips[id] = stream;
	}

	private static AudioStream? LoadAudioFile(string path)
	{
		if (ResourceLoader.Exists(path))
		{
			var res = ResourceLoader.Load<AudioStream>(path);
			if (res != null)
				return res;
		}

		if (!Godot.FileAccess.FileExists(path))
			return null;

		var bytes = Godot.FileAccess.GetFileAsBytes(path);
		if (bytes == null || bytes.Length == 0)
			return null;

		if (path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase))
			return new AudioStreamMP3 { Data = bytes };

		return null;
	}

	public static void Play(string id, float pitch = 1f, float volumeDb = 0f)
	{
		Instance?.PlayInternal(id, pitch, volumeDb);
	}

	public static void PlayUi(string id) => Play(id, 1f, -2f);

	public static void Play3D(string id, Vector3 worldPosition, float pitch = 1f, float volumeDb = 0f)
	{
		Instance?.PlayAt(id, worldPosition, pitch, volumeDb);
	}

	/// <summary>Play a sliced line from the robotic voice pack (e.g. warning1, access_granted1).</summary>
	public static void PlayVoice(string sliceId, float volumeDb = -1f)
	{
		Instance?.PlayVoiceInternal(sliceId, volumeDb);
	}

	/// <summary>VO when the local MAP takes damage. Cooldown prevents spam.</summary>
	public static void PlayDamageSustained()
	{
		Instance?.PlayDamageSustainedInternal();
	}

	private void PlayDamageSustainedInternal()
	{
		if (_damageVoCooldown > 0f)
			return;
		_damageVoCooldown = 2.4f;
		PlayInternal("damage_sustained", 1f, -1f);
	}

	private void PlayVoiceInternal(string sliceId, float volumeDb)
	{
		if (_voice == null || _voicePack == null)
			return;
		if (!_voiceSlices.TryGetValue(sliceId, out var slice))
			return;

		_voiceStopTween?.Kill();
		_voice.Stop();
		_voice.Stream = _voicePack;
		_voice.VolumeDb = volumeDb;
		_voice.PitchScale = 1f;
		_voice.Play(slice.Start);

		_voiceStopTween = CreateTween();
		_voiceStopTween.TweenInterval(slice.Duration);
		_voiceStopTween.TweenCallback(Callable.From(() =>
		{
			if (_voice != null && _voice.Playing)
				_voice.Stop();
		}));
	}

	private void PlayInternal(string id, float pitch, float volumeDb)
	{
		if (!_clips.TryGetValue(id, out var clip))
			return;

		// UI clicks stay on the dedicated player so pool one-shots don't cut them off.
		if (id is "ui_click" or "ui_confirm" && _ui != null)
		{
			_ui.Stream = clip;
			_ui.PitchScale = Mathf.Clamp(pitch, 0.85f, 1.15f);
			_ui.VolumeDb = volumeDb;
			_ui.Play();
			return;
		}

		var player = NextPoolPlayer();
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(pitch, 0.6f, 1.6f);
		player.VolumeDb = volumeDb;
		player.Play();
	}

	private void PlayAt(string id, Vector3 worldPosition, float pitch, float volumeDb)
	{
		if (!_clips.TryGetValue(id, out var clip))
			return;

		var player = NextPoolPlayer();
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(pitch, 0.7f, 1.4f);
		player.VolumeDb = volumeDb;
		player.Play();
		_ = worldPosition;
	}

	private AudioStreamPlayer NextPoolPlayer()
	{
		_poolIndex = (_poolIndex + 1) % _pool.Count;
		return _pool[_poolIndex];
	}

	public static void Click() => PlayUi("ui_click");
	public static void Confirm() => PlayUi("ui_confirm");
}
