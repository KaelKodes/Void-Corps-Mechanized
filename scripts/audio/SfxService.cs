using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Autoload SFX player. Procedural clips baked once at boot.
/// </summary>
public partial class SfxService : Node
{
	private readonly Dictionary<string, AudioStreamWav> _clips = new();
	private AudioStreamPlayer? _ui;
	private readonly List<AudioStreamPlayer> _pool = new();
	private int _poolIndex;

	public static SfxService? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		BakeAll();
		_ui = new AudioStreamPlayer { Name = "UiPlayer", Bus = "Master", VolumeDb = -4f };
		AddChild(_ui);

		for (var i = 0; i < 10; i++)
		{
			var p = new AudioStreamPlayer { Name = $"Sfx_{i}", Bus = "Master", VolumeDb = -2f };
			AddChild(p);
			_pool.Add(p);
		}
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

	public static void Play(string id, float pitch = 1f, float volumeDb = 0f)
	{
		Instance?.PlayInternal(id, pitch, volumeDb);
	}

	public static void PlayUi(string id) => Play(id, 1f, -2f);

	public static void Play3D(string id, Vector3 worldPosition, float pitch = 1f, float volumeDb = 0f)
	{
		Instance?.PlayAt(id, worldPosition, pitch, volumeDb);
	}

	private void PlayInternal(string id, float pitch, float volumeDb)
	{
		if (!_clips.TryGetValue(id, out var clip))
			return;

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

		// Prototype: still 2D bus, lightly pitch-varied by distance feel later.
		var player = NextPoolPlayer();
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(pitch, 0.7f, 1.4f);
		player.VolumeDb = volumeDb;
		player.Play();
		_ = worldPosition; // reserved for AudioStreamPlayer3D upgrade
	}

	private AudioStreamPlayer NextPoolPlayer()
	{
		_poolIndex = (_poolIndex + 1) % _pool.Count;
		return _pool[_poolIndex];
	}

	public static void Click() => PlayUi("ui_click");
	public static void Confirm() => PlayUi("ui_confirm");
}
