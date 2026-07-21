using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Autoload SFX player. File samples from res://audio/sfx override procedural synth clips.
/// Multi-hit packs (stomps, VO banks) register as timed slices and play from named pools.
/// </summary>
public partial class SfxService : Node
{
	private const string SfxDir = "res://audio/sfx";
	private const string MechStepPool = "mech_step";
	private const string DryFirePool = "dry_fire";
	private const string OverheatPool = "overheat";
	private const float SteamLoopVolumeDb = -10f;
	private const float AmbientVolumeDb = -26f;
	private const float AmbientGapMin = 7f;
	private const float AmbientGapMax = 18f;

	private readonly Dictionary<string, AudioStream> _clips = new();
	private readonly Dictionary<string, AudioStream> _packs = new();
	private readonly Dictionary<string, PackSlice> _namedSteps = new();
	private readonly List<string> _allStepIds = new();
	private readonly Dictionary<string, List<PackSlice>> _slicePools = new();
	private readonly Dictionary<string, VoiceSlice> _voiceSlices = new();
	private readonly RandomNumberGenerator _rng = new();
	private AudioStream? _voicePack;
	private AudioStreamPlayer? _ui;
	private AudioStreamPlayer? _voice;
	private readonly List<AudioStreamPlayer> _combatPool = new();
	private readonly List<AudioStreamPlayer> _stepPool = new();
	private readonly Dictionary<AudioStreamPlayer, Tween> _sliceStopTweens = new();
	private int _combatPoolIndex;
	private int _stepPoolIndex;
	private float _damageVoCooldown;
	private Tween? _voiceStopTween;
	private AudioStreamPlayer? _steamLoop;
	private AudioStream? _steamLoopStream;
	private Tween? _steamFade;
	private bool _overheatFxActive;
	private AudioStreamPlayer? _ambient;
	private readonly List<AudioStream> _ambientClips = new();
	private bool _ambientEnabled;
	private float _ambientGapLeft;
	private int _ambientLastIndex = -1;
	private enum AmbientPhase
	{
		Idle,
		Waiting,
		Playing
	}

	private AmbientPhase _ambientPhase = AmbientPhase.Idle;

	/// <summary>Leg kits with animated gait (biped / hex / booster / thruster). Tracks skip stomps.</summary>
	private static readonly string[] GaitLegsPartIds =
	{
		"legs_tri_biped", "legs_tri_courier", "legs_brin_biped", "legs_brin_bulwark",
		"legs_ouro_duelist", "legs_lum_glasswalk",
		"legs_ouro_hex", "legs_ouro_razorhex", "legs_lum_hex", "legs_lum_phasehex",
		"legs_tri_packhex", "legs_brin_siegehex",
		"legs_tri_jumpjack", "legs_lum_boosters", "legs_brin_pilejack", "legs_ouro_ascender",
		"legs_ouro_thrusters", "legs_lum_vector", "legs_tri_slide", "legs_brin_charge",
		"legs_ash_coilstriders", "legs_vel_bracehounds"
	};

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

	private readonly struct PackSlice
	{
		public readonly string PackKey;
		public readonly float Start;
		public readonly float End;
		public readonly bool WholeFile;

		public PackSlice(string packKey, float start, float end)
		{
			PackKey = packKey;
			Start = start;
			End = end;
			WholeFile = false;
		}

		public static PackSlice Whole(string packKey) => new(packKey);

		private PackSlice(string packKey)
		{
			PackKey = packKey;
			Start = 0f;
			End = 0f;
			WholeFile = true;
		}

		public float Duration => Mathf.Max(0.05f, End - Start);
	}

	public static SfxService? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		_rng.Randomize();
		BakeAll();
		LoadFileSamples();
		RegisterVoicePack();
		RegisterMechStepPacks();
		RegisterDryFirePacks();
		RegisterOverheatPacks();
		RegisterAmbientPacks();

		_ui = new AudioStreamPlayer { Name = "UiPlayer", Bus = "Ui", VolumeDb = -4f };
		AddChild(_ui);

		_voice = new AudioStreamPlayer { Name = "VoicePlayer", Bus = "Voice", VolumeDb = -2f };
		AddChild(_voice);

		_steamLoop = new AudioStreamPlayer
		{
			Name = "SteamLoop",
			Bus = "Mech",
			VolumeDb = SteamLoopVolumeDb
		};
		AddChild(_steamLoop);

		_ambient = new AudioStreamPlayer
		{
			Name = "AmbientPlayer",
			Bus = "Mech",
			VolumeDb = AmbientVolumeDb
		};
		AddChild(_ambient);

		// Separate banks so weapon spam never steals footfall voices (and vice versa).
		FillPlayerPool(_combatPool, "Combat", "Sfx", 20);
		FillPlayerPool(_stepPool, "Step", "Mech", 12);

		GameSettings.ApplyAudioBuses();
	}

	private void FillPlayerPool(List<AudioStreamPlayer> pool, string prefix, string bus, int count)
	{
		for (var i = 0; i < count; i++)
		{
			var p = new AudioStreamPlayer { Name = $"{prefix}_{i}", Bus = bus, VolumeDb = -2f };
			AddChild(p);
			pool.Add(p);
		}
	}

	public override void _Process(double delta)
	{
		if (_damageVoCooldown > 0f)
			_damageVoCooldown = Mathf.Max(0f, _damageVoCooldown - (float)delta);

		TickAmbient((float)delta);
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
		TryOverride("steam_hissing", "steam-hissing.mp3");
	}

	private void RegisterOverheatPacks()
	{
		// One-shot overheat stings — add more files here as they land.
		AddWholeFilesToPool(OverheatPool, "minigun_overheat.mp3");

		if (!_clips.TryGetValue("steam_hissing", out var steam) || steam == null)
			steam = LoadAudioFile($"{SfxDir}/steam-hissing.mp3");
		if (steam == null)
			return;

		_clips["steam_hissing"] = steam;
		_steamLoopStream = (AudioStream)steam.Duplicate();
		if (_steamLoopStream is AudioStreamMP3 mp3)
			mp3.Loop = true;
	}

	private void RegisterAmbientPacks()
	{
		// Quiet industrial bed for menu / hangar surfaces. Loud sources — keep AmbientVolumeDb low.
		TryAddAmbientClip("steam-radiator.mp3");
		TryAddAmbientClip("steam-ambience.mp3");
	}

	private void TryAddAmbientClip(string fileName)
	{
		var stream = LoadAudioFile($"{SfxDir}/{fileName}");
		if (stream == null)
			return;
		_clips[System.IO.Path.GetFileNameWithoutExtension(fileName)] = stream;
		_ambientClips.Add(stream);
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

	private void RegisterMechStepPacks()
	{
		// Named variants — Kyle timestamps for sliced packs; wholes play end-to-end.
		RegisterNamedSlice("m2_a", "mechanized-2step", "mechanized-2step.mp3", 0.0f, 0.1f);
		RegisterNamedSlice("m2_b", "mechanized-2step", "mechanized-2step.mp3", 0.1f, 0.2f);
		RegisterNamedSlice("m3_a", "mechanized-3step", "mechanized-3step.mp3", 0.0f, 1.05f);
		RegisterNamedSlice("m3_b", "mechanized-3step", "mechanized-3step.mp3", 1.05f, 1.95f);
		RegisterNamedSlice("m3_c", "mechanized-3step", "mechanized-3step.mp3", 1.95f, 2.70f);

		RegisterNamedWhole("ap_005", "audiopapkin-big-robot-footstep-005-370113.mp3");
		RegisterNamedWhole("ap_008", "audiopapkin-big-robot-footstep-008-425967.mp3");
		RegisterNamedWhole("ap_010", "audiopapkin-big-robot-footstep-010-426482.mp3");
		RegisterNamedWhole("ap_013", "audiopapkin-big-robot-footstep-013-445101.mp3");
		RegisterNamedWhole("ap_014", "audiopapkin-big-robot-footstep-014-445104.mp3");
		RegisterNamedWhole("ap_015", "audiopapkin-big-robot-footstep-015-445103.mp3");
		RegisterNamedWhole("ap_016", "audiopapkin-big-robot-footstep-016-445102.mp3");
		RegisterNamedWhole("ap_017", "audiopapkin-big-robot-footstep-017-499567.mp3");
		RegisterNamedWhole("ap_020", "audiopapkin-big-robot-footstep-020-500843.mp3");
		RegisterNamedWhole("ap_330", "audiopapkin-big-robot-footstep-330678.mp3");

		var master = GetOrCreatePool(MechStepPool);
		foreach (var id in _allStepIds)
			master.Add(_namedSteps[id]);

		AssignLegsStepPools();
	}

	private void RegisterDryFirePacks()
	{
		AddWholeFilesToPool(DryFirePool,
			"empty-machine-gun-firing-clicks.mp3",
			"empty-rifle-clip.mp3",
			"empty-gun-shot.mp3");
	}

	private void AddWholeFilesToPool(string poolId, params string[] fileNames)
	{
		var pool = GetOrCreatePool(poolId);
		foreach (var fileName in fileNames)
		{
			var stream = LoadAudioFile($"{SfxDir}/{fileName}");
			if (stream == null)
				continue;
			var packKey = System.IO.Path.GetFileNameWithoutExtension(fileName);
			_packs[packKey] = stream;
			pool.Add(PackSlice.Whole(packKey));
		}
	}

	private void RegisterNamedSlice(string stepId, string packKey, string fileName, float start, float end)
	{
		if (!_packs.ContainsKey(packKey))
		{
			var stream = LoadAudioFile($"{SfxDir}/{fileName}");
			if (stream == null)
				return;
			_packs[packKey] = stream;
		}

		_namedSteps[stepId] = new PackSlice(packKey, start, end);
		_allStepIds.Add(stepId);
	}

	private void RegisterNamedWhole(string stepId, string fileName)
	{
		var stream = LoadAudioFile($"{SfxDir}/{fileName}");
		if (stream == null)
			return;

		var packKey = System.IO.Path.GetFileNameWithoutExtension(fileName);
		_packs[packKey] = stream;
		_namedSteps[stepId] = PackSlice.Whole(packKey);
		_allStepIds.Add(stepId);
	}

	/// <summary>Deal two step variants to each gait legs kit, cycling the bank evenly.</summary>
	private void AssignLegsStepPools()
	{
		if (_allStepIds.Count == 0)
			return;

		const int variantsPerLegs = 2;
		for (var i = 0; i < GaitLegsPartIds.Length; i++)
		{
			var pool = GetOrCreatePool(LegsPoolId(GaitLegsPartIds[i]));
			pool.Clear();
			for (var v = 0; v < variantsPerLegs; v++)
			{
				var stepId = _allStepIds[(i * variantsPerLegs + v) % _allStepIds.Count];
				pool.Add(_namedSteps[stepId]);
			}
		}
	}

	private static string LegsPoolId(string legsPartId) => $"legs:{legsPartId}";

	private List<PackSlice> GetOrCreatePool(string poolId)
	{
		if (!_slicePools.TryGetValue(poolId, out var pool))
		{
			pool = new List<PackSlice>();
			_slicePools[poolId] = pool;
		}

		return pool;
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

	/// <summary>Random mech footfall from the shared bank (fallback).</summary>
	public static void PlayMechStep(float volumeDb = -2f)
	{
		Instance?.PlayPoolSlice(MechStepPool, volumeDb, pitchJitter: 0.08f);
	}

	/// <summary>Footfall from the variants assigned to this legs part id.</summary>
	public static void PlayMechStepForLegs(string legsPartId, float volumeDb = -2f)
	{
		if (Instance == null)
			return;
		if (!string.IsNullOrEmpty(legsPartId)
		    && Instance._slicePools.TryGetValue(LegsPoolId(legsPartId), out var pool)
		    && pool.Count > 0)
		{
			Instance.PlayPoolSlice(LegsPoolId(legsPartId), volumeDb, pitchJitter: 0.08f);
			return;
		}

		Instance.PlayPoolSlice(MechStepPool, volumeDb, pitchJitter: 0.08f);
	}

	/// <summary>Click/clack when a weapon lacks power to discharge.</summary>
	public static void PlayDryFire(float volumeDb = -4f)
	{
		Instance?.PlayPoolSlice(DryFirePool, volumeDb, pitchJitter: 0.06f, stepBank: false);
	}

	/// <summary>Local MAP crossed into full overheat — sting + vent hiss loop.</summary>
	public static void BeginOverheatFx()
	{
		Instance?.BeginOverheatFxInternal();
	}

	/// <summary>Overheat cleared (or pilot out of combat) — stop vent loop.</summary>
	public static void EndOverheatFx()
	{
		Instance?.EndOverheatFxInternal();
	}

	/// <summary>One-shot steam vent for future call sites (crates, doors, coolers, …).</summary>
	public static void PlaySteamHiss(float pitch = 1f, float volumeDb = -6f)
	{
		Play("steam_hissing", pitch, volumeDb);
	}

	/// <summary>
	/// Menu / hangar steam bed. Follows music surface so combat and campaign maps stay quiet.
	/// </summary>
	public static void SyncAmbienceForMusicCue(MusicCue cue)
	{
		Instance?.SyncAmbienceForMusicCueInternal(cue);
	}

	/// <summary>VO when a local MAP component is fully destroyed. Cooldown prevents spam.</summary>
	public static void PlayDamageSustained()
	{
		Instance?.PlayDamageSustainedInternal();
	}

	private void SyncAmbienceForMusicCueInternal(MusicCue cue)
	{
		var want = cue is MusicCue.Menu or MusicCue.Hangar or MusicCue.Results;
		if (want)
			StartAmbientBed();
		else
			StopAmbientBed();
	}

	private void StartAmbientBed()
	{
		if (_ambientClips.Count == 0 || _ambient == null)
			return;
		if (_ambientEnabled)
			return;

		_ambientEnabled = true;
		_ambientPhase = AmbientPhase.Waiting;
		// Short open silence so the bed doesn't slam in with the menu sting.
		_ambientGapLeft = _rng.RandfRange(2.5f, 6f);
	}

	private void StopAmbientBed()
	{
		_ambientEnabled = false;
		_ambientPhase = AmbientPhase.Idle;
		_ambientGapLeft = 0f;
		if (_ambient != null && _ambient.Playing)
			_ambient.Stop();
	}

	private void TickAmbient(float dt)
	{
		if (!_ambientEnabled || _ambient == null || _ambientClips.Count == 0)
			return;

		switch (_ambientPhase)
		{
			case AmbientPhase.Waiting:
				_ambientGapLeft -= dt;
				if (_ambientGapLeft > 0f)
					return;
				PlayNextAmbientClip();
				break;
			case AmbientPhase.Playing:
				if (_ambient.Playing)
					return;
				_ambientPhase = AmbientPhase.Waiting;
				_ambientGapLeft = _rng.RandfRange(AmbientGapMin, AmbientGapMax);
				break;
		}
	}

	private void PlayNextAmbientClip()
	{
		if (_ambient == null || _ambientClips.Count == 0)
			return;

		var index = _rng.RandiRange(0, _ambientClips.Count - 1);
		if (_ambientClips.Count > 1 && index == _ambientLastIndex)
			index = (index + 1) % _ambientClips.Count;
		_ambientLastIndex = index;

		_ambient.Stream = _ambientClips[index];
		_ambient.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.04f, 0.04f), 0.9f, 1.1f);
		_ambient.VolumeDb = AmbientVolumeDb + _rng.RandfRange(-2f, 1f);
		_ambient.Play();
		_ambientPhase = AmbientPhase.Playing;
	}

	private void BeginOverheatFxInternal()
	{
		if (_overheatFxActive)
			return;
		_overheatFxActive = true;

		PlayPoolSlice(OverheatPool, volumeDb: -3f, pitchJitter: 0.04f, stepBank: false);
		StartSteamLoop();
	}

	private void EndOverheatFxInternal()
	{
		if (!_overheatFxActive && _steamLoop is not { Playing: true })
			return;
		_overheatFxActive = false;
		StopSteamLoop(fade: true);
	}

	private void StartSteamLoop()
	{
		if (_steamLoop == null || _steamLoopStream == null)
			return;

		_steamFade?.Kill();
		_steamLoop.Stream = _steamLoopStream;
		_steamLoop.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.03f, 0.03f), 0.9f, 1.1f);
		_steamLoop.VolumeDb = -40f;
		_steamLoop.Play();

		_steamFade = CreateTween();
		_steamFade.TweenProperty(_steamLoop, "volume_db", SteamLoopVolumeDb, 0.35f);
	}

	private void StopSteamLoop(bool fade)
	{
		if (_steamLoop == null)
			return;

		_steamFade?.Kill();
		if (!fade || !_steamLoop.Playing)
		{
			_steamLoop.Stop();
			return;
		}

		var player = _steamLoop;
		_steamFade = CreateTween();
		_steamFade.TweenProperty(player, "volume_db", -40f, 0.55f);
		_steamFade.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(player))
				player.Stop();
		}));
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

	private void PlayPoolSlice(string poolId, float volumeDb, float pitchJitter, bool stepBank = true)
	{
		if (!_slicePools.TryGetValue(poolId, out var pool) || pool.Count == 0)
			return;

		var slice = pool[_rng.RandiRange(0, pool.Count - 1)];
		if (!_packs.TryGetValue(slice.PackKey, out var stream))
			return;

		// Long sliced stomps read hotter / hang longer — keep them from dominating.
		if (!slice.WholeFile && slice.Duration > 0.45f)
			volumeDb -= 4f;

		var player = stepBank
			? ClaimPlayer(_stepPool, ref _stepPoolIndex)
			: ClaimPlayer(_combatPool, ref _combatPoolIndex);
		player.Stream = stream;
		player.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-pitchJitter, pitchJitter), 0.85f, 1.15f);
		player.VolumeDb = volumeDb;

		if (slice.WholeFile)
		{
			player.Play();
			return;
		}

		player.Play(slice.Start);

		var duration = slice.Duration;
		var stopPlayer = player;
		var tween = CreateTween();
		_sliceStopTweens[player] = tween;
		tween.TweenInterval(duration);
		tween.TweenCallback(Callable.From(() =>
		{
			_sliceStopTweens.Remove(stopPlayer);
			if (GodotObject.IsInstanceValid(stopPlayer) && stopPlayer.Playing)
				stopPlayer.Stop();
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

		var player = ClaimPlayer(_combatPool, ref _combatPoolIndex);
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(pitch, 0.6f, 1.6f);
		player.VolumeDb = volumeDb;
		player.Play();
	}

	private void PlayAt(string id, Vector3 worldPosition, float pitch, float volumeDb)
	{
		if (!_clips.TryGetValue(id, out var clip))
			return;

		var player = ClaimPlayer(_combatPool, ref _combatPoolIndex);
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(pitch, 0.7f, 1.4f);
		player.VolumeDb = volumeDb;
		player.Play();
		_ = worldPosition;
	}

	/// <summary>Prefer an idle voice; only overwrite a playing one when the bank is saturated.</summary>
	private AudioStreamPlayer ClaimPlayer(List<AudioStreamPlayer> pool, ref int roundRobin)
	{
		for (var i = 0; i < pool.Count; i++)
		{
			var idx = (roundRobin + i) % pool.Count;
			var candidate = pool[idx];
			if (candidate.Playing)
				continue;
			roundRobin = (idx + 1) % pool.Count;
			ClearSliceStop(candidate);
			return candidate;
		}

		roundRobin = (roundRobin + 1) % pool.Count;
		var forced = pool[roundRobin];
		ClearSliceStop(forced);
		return forced;
	}

	private void ClearSliceStop(AudioStreamPlayer player)
	{
		if (!_sliceStopTweens.TryGetValue(player, out var tween))
			return;
		if (GodotObject.IsInstanceValid(tween))
			tween.Kill();
		_sliceStopTweens.Remove(player);
	}

	public static void Click() => PlayUi("ui_click");
	public static void Confirm() => PlayUi("ui_confirm");
}
