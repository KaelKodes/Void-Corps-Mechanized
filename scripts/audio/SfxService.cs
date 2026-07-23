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
	private const string MechAirMovePool = "mech_air_move";
	private const string DryFirePool = "dry_fire";
	private const string OverheatPool = "overheat";
	private const string SteamReleasePool = "steam_release";
	private const string AirPuffPool = "air_puff";
	private const float SteamLoopVolumeDb = -10f;
	private const float AmbientVolumeDb = -26f;
	private const float AmbientGapMin = 7f;
	private const float AmbientGapMax = 18f;
	private const float BoosterHoldVolumeDb = 5.4f;
	private const float ThrusterDashVolumeDb = 4.5f;
	private const float SprintLoopVolumeDb = 3.5f;
	/// <summary>~gunfire / impact hear distance before silence.</summary>
	public const float CombatHearRange = 42f;
	public const float DestructionHearRange = 55f;
	private const string BoosterHoldDir = "mech/boost/hold";

	private const string UiErrorQuick = "ui_error_quick";
	private const string UiErrorIncorrect = "ui_error_incorrect";
	private const string UiErrorDeeDoo = "ui_error_deedoo";
	private const string UiErrorBuzzBuzz = "ui_error_buzzbuzz";
	private const string ImpactArmorPool = "impact_armor";
	private const string ImpactCoverPool = "impact_cover";
	private const string ImpactShieldPool = "impact_shield";
	private const string ImpactLightPool = "impact_light";
	private const string DestroySmallPool = "destroy_small";
	private const string DestroyMediumPool = "destroy_medium";
	private const string DestroyHeavyPool = "destroy_heavy";
	private const float LightImpactDamage = 5f;

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
	private readonly List<AudioStreamPlayer> _uiSfxPool = new();
	private readonly List<AudioStreamPlayer> _energyPool = new();
	private readonly Dictionary<AudioStreamPlayer, Tween> _sliceStopTweens = new();
	private int _combatPoolIndex;
	private int _stepPoolIndex;
	private int _uiSfxPoolIndex;
	private int _energyPoolIndex;
	private float _damageVoCooldown;
	private Tween? _voiceStopTween;
	private AudioStreamPlayer? _steamLoop;
	private AudioStream? _steamLoopStream;
	private Tween? _steamFade;
	private bool _overheatFxActive;
	private float _exertionVentCooldown;
	private AudioStreamPlayer? _ambient;
	private readonly List<AmbientClip> _ambientClips = new();
	private bool _ambientEnabled;
	private float _ambientGapLeft;
	private int _ambientLastIndex = -1;

	private readonly struct AmbientClip
	{
		public readonly AudioStream Stream;
		public readonly float VolumeDb;
		public readonly float Weight;

		public AmbientClip(AudioStream stream, float volumeDb, float weight)
		{
			Stream = stream;
			VolumeDb = volumeDb;
			Weight = Mathf.Max(0.01f, weight);
		}
	}

	private enum AmbientPhase
	{
		Idle,
		Waiting,
		Playing
	}

	private AmbientPhase _ambientPhase = AmbientPhase.Idle;
	private AudioStreamPlayer? _boosterHold;
	private Tween? _boosterFade;
	private bool _boosterHoldActive;
	private readonly Dictionary<string, AudioStream> _boosterHoldLoops = new();
	private AudioStreamPlayer? _sprintLoop;
	private AudioStream? _sprintLoopStream;
	private Tween? _sprintFade;
	private bool _sprintLoopActive;

	public enum BoosterHoldVoice
	{
		Quick,
		Soft,
		Heavy
	}

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

	/// <summary>Hexapods may use multi-stomp files; everything else in the gait list is biped-plant.</summary>
	private static readonly HashSet<string> HexGaitLegsPartIds = new()
	{
		"legs_ouro_hex", "legs_ouro_razorhex", "legs_lum_hex", "legs_lum_phasehex",
		"legs_tri_packhex", "legs_brin_siegehex"
	};

	private enum StepKind
	{
		Single,
		Multi
	}

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
		public readonly StepKind Kind;

		public PackSlice(string packKey, float start, float end, StepKind kind = StepKind.Single)
		{
			PackKey = packKey;
			Start = start;
			End = end;
			WholeFile = false;
			Kind = kind;
		}

		public static PackSlice Whole(string packKey, StepKind kind = StepKind.Single) =>
			new(packKey, kind);

		private PackSlice(string packKey, StepKind kind)
		{
			PackKey = packKey;
			Start = 0f;
			End = 0f;
			WholeFile = true;
			Kind = kind;
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
		RegisterMechAirMovePack();
		RegisterDryFirePacks();
		RegisterOverheatPacks();
		RegisterAmbientPacks();
		RegisterBallisticPacks();
		RegisterBoosterHoldLoops();
		RegisterUiErrorPacks();
		RegisterImpactPacks();
		RegisterDestructionPacks();
		RegisterEnergyFirePacks();

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

		_boosterHold = new AudioStreamPlayer
		{
			Name = "BoosterHold",
			Bus = "Mech",
			VolumeDb = BoosterHoldVolumeDb
		};
		AddChild(_boosterHold);

		_sprintLoop = new AudioStreamPlayer
		{
			Name = "SprintLoop",
			Bus = "Mech",
			VolumeDb = SprintLoopVolumeDb
		};
		AddChild(_sprintLoop);

		// Separate banks so weapon spam never steals footfall voices (and vice versa).
		FillPlayerPool(_combatPool, "Combat", "Sfx", 20);
		FillPlayerPool(_stepPool, "Step", "Mech", 12);
		FillPlayerPool(_uiSfxPool, "UiSfx", "Ui", 6);
		// Energy lasers are ~4s and layer — need a deep dedicated bank so shots never cut each other.
		FillPlayerPool(_energyPool, "Energy", "Sfx", 48);

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
		if (_exertionVentCooldown > 0f)
			_exertionVentCooldown = Mathf.Max(0f, _exertionVentCooldown - (float)delta);

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
		TryOverride("ui_click", "ui/freesound_community-mech-keyboard-02-102918.mp3");
		TryOverride("damage_sustained", "voice/phatphrogstudio-robot-voice-damage-sustained-487076.mp3");
		TryOverride("drop_impact", "mech/steps/freesound_community-mech_step_001-87175.mp3");
		TryOverride("steam_hissing", "mech/heat/steam-hissing.mp3");
	}

	private void RegisterOverheatPacks()
	{
		// One-shot overheat stings — add more files here as they land.
		AddWholeFilesToPool(OverheatPool, "mech/heat/minigun_overheat.mp3");

		if (!_clips.TryGetValue("steam_hissing", out var steam) || steam == null)
			steam = LoadAudioFile($"{SfxDir}/mech/heat/steam-hissing.mp3");
		if (steam != null)
		{
			_clips["steam_hissing"] = steam;
			_steamLoopStream = (AudioStream)steam.Duplicate();
			if (_steamLoopStream is AudioStreamMP3 mp3)
				mp3.Loop = true;
		}

		RegisterHeatVentPacks();
	}

	private void RegisterHeatVentPacks()
	{
		RegisterClip("air_vent_fan", "mech/heat/air-vent-fan.mp3");
		RegisterClip("air_out", "mech/heat/air-out.mp3");
		RegisterClip("air_release", "mech/heat/air-release.mp3");

		AddWholeFilesToPool(AirPuffPool,
			"mech/heat/air-out.mp3",
			"mech/heat/air-release.mp3");

		var longPath = $"{SfxDir}/mech/heat/SteamReleaseLong.mp3";
		var longStream = LoadAudioFile(longPath);
		if (longStream == null)
			return;

		_packs["steam_release_long"] = longStream;
		var end = Mathf.Max(0.55f, (float)longStream.GetLength());
		var pool = GetOrCreatePool(SteamReleasePool);
		// Kyle timestamps: soft puff, then louder longer dump.
		pool.Add(new PackSlice("steam_release_long", 0.0f, 0.5f));
		pool.Add(new PackSlice("steam_release_long", 0.5f, end));
	}

	private void RegisterAmbientPacks()
	{
		// Hangar / menu bed. Louder sources get lower gain + lower pick weight.
		TryAddAmbientClip("ambient/steam-radiator.mp3", AmbientVolumeDb, weight: 1f);
		TryAddAmbientClip("ambient/steam-ambience.mp3", AmbientVolumeDb, weight: 1f);
		TryAddAmbientClip("ambient/air-vent-ambience.mp3", -28f, weight: 1.15f);
		TryAddAmbientClip("ambient/retro-fridge.mp3", -24f, weight: 1f);
		TryAddAmbientClip("ambient/AmbientHangerAir.mp3", -23f, weight: 0.85f);
		TryAddAmbientClip("ambient/industrial-cleaning.mp3", -34f, weight: 0.3f);
	}

	private void TryAddAmbientClip(string relativePath, float volumeDb, float weight)
	{
		var stream = LoadAudioFile($"{SfxDir}/{relativePath}");
		if (stream == null)
			return;
		_clips[System.IO.Path.GetFileNameWithoutExtension(relativePath)] = stream;
		_ambientClips.Add(new AmbientClip(stream, volumeDb, weight));
	}

	private void RegisterBallisticPacks()
	{
		RegisterClip("ballistic_lmg_single", "weapons/ballistic/LMGSingleFire.mp3");
		RegisterClip("ballistic_rifle_1", "weapons/ballistic/1ShotRifle.mp3");
		RegisterClip("reload_light", "weapons/ballistic/reload1.mp3");
		RegisterClip("reload_heavy", "weapons/ballistic/reload2.mp3");
	}

	private void RegisterUiErrorPacks()
	{
		RegisterClip(UiErrorQuick, "ui/errors/QuickErrorBloop.mp3");
		RegisterClip(UiErrorIncorrect, "ui/errors/IncorrectError.mp3");
		RegisterClip(UiErrorDeeDoo, "ui/errors/DeeDooError.mp3");
		RegisterClip(UiErrorBuzzBuzz, "ui/errors/BuzzBuzzError.mp3");
	}

	private void RegisterImpactPacks()
	{
		AddImpactPrefixToPool(ImpactArmorPool, "MetalimpactHit");
		AddImpactPrefixToPool(ImpactCoverPool, "MetalimpactCrunch");
		AddImpactPrefixToPool(ImpactShieldPool, "MetalimpactClang");
		AddImpactPrefixToPool(ImpactLightPool, "MetalimpactLightHit");

		// Prefer a real armor hit over the synth clang if the pool loaded.
		if (_slicePools.TryGetValue(ImpactArmorPool, out var armor) && armor.Count > 0
		    && _packs.TryGetValue(armor[0].PackKey, out var armorStream))
			_clips["weapon_hit"] = armorStream;
	}

	private void AddImpactPrefixToPool(string poolId, string filePrefix)
	{
		const string relDir = "combat/impacts";
		var absDir = ProjectSettings.GlobalizePath($"{SfxDir}/{relDir}");
		if (!System.IO.Directory.Exists(absDir))
		{
			AddWholeFilesToPool(poolId, $"{relDir}/{filePrefix}.mp3");
			return;
		}

		foreach (var path in System.IO.Directory.GetFiles(absDir))
		{
			var name = System.IO.Path.GetFileName(path);
			if (name.EndsWith(".import", System.StringComparison.OrdinalIgnoreCase))
				continue;
			if (!name.StartsWith(filePrefix, System.StringComparison.OrdinalIgnoreCase))
				continue;
			// "MetalimpactHit" must not steal "MetalimpactHitSomethingElse" letter suffixes.
			var rest = name[filePrefix.Length..];
			if (rest.Length > 0 && char.IsLetter(rest[0]))
				continue;
			if (!name.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
			    && !name.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
			    && !name.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase))
				continue;
			AddWholeFilesToPool(poolId, $"{relDir}/{name}");
		}
	}

	private void RegisterDestructionPacks()
	{
		// Small props / crates — ceramic, wood, universal crunch.
		AddWholeFilesToPool(DestroySmallPool,
			"combat/explosions/destroy_ceramic.mp3",
			"combat/explosions/destroy_wood_crate.mp3",
			"combat/explosions/destroy_crunch_universal.mp3");

		// Mid structure — concrete bust + mixed crunch + concrete/glass blast.
		AddWholeFilesToPool(DestroyMediumPool,
			"combat/explosions/destroy_concrete.mp3",
			"combat/explosions/destroy_crunch_universal.mp3",
			"combat/explosions/explosion_concrete_glass.mp3");

		// Heavy demolitions / MAP self-destruct / buildings.
		AddWholeFilesToPool(DestroyHeavyPool,
			"combat/explosions/explosion_mortar.mp3",
			"combat/explosions/explosion_bomb_close.mp3",
			"combat/explosions/explosion_medium_distant.mp3",
			"combat/explosions/explosion_concrete_glass.mp3");

		// Legacy Play("explosion") call sites → mortar as default.
		RegisterClip("explosion", "combat/explosions/explosion_mortar.mp3");
	}

	private void RegisterEnergyFirePacks()
	{
		const string relDir = "weapons/energy";
		var absDir = ProjectSettings.GlobalizePath($"{SfxDir}/{relDir}");
		var files = new List<string>();
		if (System.IO.Directory.Exists(absDir))
		{
			foreach (var path in System.IO.Directory.GetFiles(absDir))
			{
				var name = System.IO.Path.GetFileName(path);
				if (name.EndsWith(".import", System.StringComparison.OrdinalIgnoreCase))
					continue;
				if (!name.StartsWith("energy_laser_", System.StringComparison.OrdinalIgnoreCase))
					continue;
				if (!name.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
				    && !name.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
				    && !name.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase))
					continue;
				files.Add(name);
			}

			files.Sort(System.StringComparer.OrdinalIgnoreCase);
		}

		if (files.Count == 0)
		{
			for (var i = 1; i <= 11; i++)
				files.Add($"energy_laser_{i:00}.wav");
		}

		foreach (var name in files)
		{
			var stream = LoadAudioFile($"{SfxDir}/{relDir}/{name}");
			if (stream == null)
				continue;
			var packKey = System.IO.Path.GetFileNameWithoutExtension(name);
			_packs[packKey] = stream;
			_clips[packKey] = stream;
		}
	}

	/// <summary>One fixed laser take per energy gun — do not random-pool these.</summary>
	private static readonly Dictionary<string, string> EnergyFireClipByPart =
		new()
		{
			["wep_tri_hybrid"] = "energy_laser_01", // Hybrid Projector
			["wep_lum_arc"] = "energy_laser_02", // Arc Lance
			["wep_lum_volt"] = "energy_laser_03", // Volt Needle
			["wep_lum_prism"] = "energy_laser_04", // Prism Beam
			["wep_lum_surge"] = "energy_laser_05", // Surge Coil
			["wep_lum_ghost"] = "energy_laser_06", // Ghost Arc
			["wep_lum_oracle"] = "energy_laser_07", // Oracle Ray
			["wep_lum_spark"] = "energy_laser_08", // Spark Cascade
			["wep_lum_well"] = "energy_laser_09", // Well Emitter
		};

	private const string EnergyFireSupportClip = "energy_laser_10";
	private const string EnergyFireFallbackClip = "energy_laser_11";

	private void RegisterClip(string id, string relativePath)
	{
		var stream = LoadAudioFile($"{SfxDir}/{relativePath}");
		if (stream != null)
			_clips[id] = stream;
	}

	/// <summary>Ballistic muzzle reports — single-shot clips only.</summary>
	private static readonly System.Collections.Generic.Dictionary<string, string> BallisticFireClipByPart =
		new()
		{
			// Clean heavy rifle report
			["wep_brin_slug"] = "ballistic_rifle_1",
			["wep_brin_anvil"] = "ballistic_rifle_1",
			["wep_brin_deny"] = "ballistic_rifle_1",
			["wep_ouro_rifle"] = "ballistic_rifle_1",
			["wep_ouro_marksman"] = "ballistic_rifle_1",
			["wep_ouro_longneedle"] = "ballistic_rifle_1",
			["wep_ouro_scalpel"] = "ballistic_rifle_1",
			["wep_ouro_duelist"] = "ballistic_rifle_1",
			["wep_ouro_whisper"] = "ballistic_rifle_1",
			["wep_tri_patrol"] = "ballistic_rifle_1",
			["wep_tri_fleet"] = "ballistic_rifle_1",

			// Thud with casing character
			["wep_brin_maul"] = "ballistic_lmg_single",
			["wep_brin_rivet"] = "ballistic_lmg_single",
			["wep_brin_pile"] = "ballistic_lmg_single",
			["wep_brin_scatter"] = "ballistic_lmg_single",
			["wep_brin_chain"] = "ballistic_lmg_single",
			["wep_ouro_stitch"] = "ballistic_lmg_single",
			["wep_ouro_pulse"] = "ballistic_lmg_single",
			["wep_tri_burst"] = "ballistic_lmg_single",
			["wep_tri_convoy"] = "ballistic_lmg_single",
			["wep_tri_workhorse"] = "ballistic_lmg_single",
			["wep_tri_anchor"] = "ballistic_lmg_single",
		};

	private void RegisterBoosterHoldLoops()
	{
		TryRegisterBoosterLoop(BoosterHoldVoice.Heavy, "HeavyBoosterRoar.mp3");
		TryRegisterBoosterLoop(BoosterHoldVoice.Soft, "SoftBooster.mp3");
		TryRegisterBoosterLoop(BoosterHoldVoice.Quick, "QuickBoost.mp3");

		// Same QuickBoost take: one-shot for dash, looped bed for sprint.
		var quick = LoadAudioFile($"{SfxDir}/{BoosterHoldDir}/QuickBoost.mp3");
		if (quick != null)
		{
			_clips["thruster_dash"] = quick;
			_sprintLoopStream = (AudioStream)quick.Duplicate();
			if (_sprintLoopStream is AudioStreamMP3 mp3)
				mp3.Loop = true;
		}
	}

	private void TryRegisterBoosterLoop(BoosterHoldVoice voice, string fileName)
	{
		var stream = LoadAudioFile($"{SfxDir}/{BoosterHoldDir}/{fileName}");
		if (stream == null)
			return;

		var looped = (AudioStream)stream.Duplicate();
		if (looped is AudioStreamMP3 mp3)
			mp3.Loop = true;
		_boosterHoldLoops[VoiceKey(voice)] = looped;
	}

	private static string VoiceKey(BoosterHoldVoice voice) => voice.ToString();

	/// <summary>Pick a hold loop from legs character: heavy roar, soft burn, or quick taps.</summary>
	public static BoosterHoldVoice ResolveBoosterHoldVoice(PartData? legs, MechStats stats)
	{
		if (legs != null)
		{
			switch (legs.Id)
			{
				case "legs_brin_pilejack":
				case "legs_brin_charge":
				case "legs_brin_fortress":
				case "legs_brin_bulwark":
				case "legs_brin_siegehex":
				case "legs_vel_bracehounds":
					return BoosterHoldVoice.Heavy;
				case "legs_lum_boosters":
				case "legs_ouro_ascender":
				case "legs_lum_vector":
				case "legs_lum_phasehex":
				case "legs_lum_hex":
				case "legs_ouro_razorhex":
				case "legs_ouro_thrusters":
					return BoosterHoldVoice.Soft;
				case "legs_tri_jumpjack":
				case "legs_ash_coilstriders":
				case "legs_tri_courier":
				case "legs_tri_slide":
				case "legs_ouro_duelist":
					return BoosterHoldVoice.Quick;
			}
		}

		// Fallback from assembled jump stats when the kit isn't specially mapped.
		if (stats.JumpHeat >= 11f || stats.JumpImpulse >= 11f)
			return BoosterHoldVoice.Heavy;
		if (stats.JumpDuration <= 1.2f)
			return BoosterHoldVoice.Quick;
		return BoosterHoldVoice.Soft;
	}

	/// <summary>Start/refresh the sustain loop while Space thrust is held.</summary>
	public static void BeginBoosterHold(BoosterHoldVoice voice, float volumeDb = BoosterHoldVolumeDb)
	{
		Instance?.BeginBoosterHoldInternal(voice, volumeDb);
	}

	/// <summary>Fade out the sustain loop on release / fuel empty.</summary>
	public static void EndBoosterHold(bool fade = true)
	{
		Instance?.EndBoosterHoldInternal(fade);
	}

	/// <summary>Thruster dash — single QuickBoost report.</summary>
	public static void PlayThrusterDash(float volumeDb = ThrusterDashVolumeDb)
	{
		Play("thruster_dash", 1f + (float)GD.RandRange(-0.03, 0.03), volumeDb);
	}

	/// <summary>Sprint sustain — looped QuickBoost while the gait is opened up.</summary>
	public static void BeginSprintThruster(float volumeDb = SprintLoopVolumeDb)
	{
		Instance?.BeginSprintThrusterInternal(volumeDb);
	}

	public static void EndSprintThruster(bool fade = true)
	{
		Instance?.EndSprintThrusterInternal(fade);
	}

	private void BeginBoosterHoldInternal(BoosterHoldVoice voice, float volumeDb)
	{
		if (_boosterHold == null)
			return;
		if (!_boosterHoldLoops.TryGetValue(VoiceKey(voice), out var stream))
			return;

		// Same voice already burning — leave it alone.
		if (_boosterHoldActive
		    && _boosterHold.Playing
		    && _boosterHold.Stream == stream)
			return;

		_boosterFade?.Kill();
		_boosterHold.Stream = stream;
		_boosterHold.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.02f, 0.02f), 0.95f, 1.05f);
		_boosterHold.VolumeDb = -40f;
		_boosterHold.Play();
		_boosterHoldActive = true;

		_boosterFade = CreateTween();
		_boosterFade.TweenProperty(_boosterHold, "volume_db", volumeDb, 0.18f);
	}

	private void EndBoosterHoldInternal(bool fade)
	{
		if (_boosterHold == null)
			return;

		_boosterHoldActive = false;
		_boosterFade?.Kill();
		if (!fade || !_boosterHold.Playing)
		{
			_boosterHold.Stop();
			return;
		}

		var player = _boosterHold;
		_boosterFade = CreateTween();
		_boosterFade.TweenProperty(player, "volume_db", -40f, 0.35f);
		_boosterFade.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(player))
				player.Stop();
		}));
	}

	private void BeginSprintThrusterInternal(float volumeDb)
	{
		if (_sprintLoop == null || _sprintLoopStream == null)
			return;
		if (_sprintLoopActive && _sprintLoop.Playing)
			return;

		_sprintFade?.Kill();
		_sprintLoop.Stream = _sprintLoopStream;
		_sprintLoop.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.02f, 0.02f), 0.95f, 1.05f);
		_sprintLoop.VolumeDb = -40f;
		_sprintLoop.Play();
		_sprintLoopActive = true;

		_sprintFade = CreateTween();
		_sprintFade.TweenProperty(_sprintLoop, "volume_db", volumeDb, 0.2f);
	}

	private void EndSprintThrusterInternal(bool fade)
	{
		if (_sprintLoop == null)
			return;

		_sprintLoopActive = false;
		_sprintFade?.Kill();
		if (!fade || !_sprintLoop.Playing)
		{
			_sprintLoop.Stop();
			return;
		}

		var player = _sprintLoop;
		_sprintFade = CreateTween();
		_sprintFade.TweenProperty(player, "volume_db", -40f, 0.3f);
		_sprintFade.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(player))
				player.Stop();
		}));
	}

	private void RegisterVoicePack()
	{
		_voicePack = LoadAudioFile($"{SfxDir}/voice/freesound_community-futuristic-robotic-voice-sentences-31272.mp3");
		if (_voicePack == null)
			return;

		// Timestamps from audio/sfx/voice/futuristic robotic voices breakdown.txt
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
		var kinds = LoadStepKindLabels();

		// Multi-stomp packs — hex only (see step_kind.txt).
		RegisterNamedWhole("m2", "mech/steps/mechanized-2step.mp3", KindOf(kinds, "mechanized-2step.mp3", StepKind.Multi));
		RegisterNamedWhole("m3", "mech/steps/mechanized-3step.mp3", KindOf(kinds, "mechanized-3step.mp3", StepKind.Multi));
		RegisterNamedSlice("m3_a", "mechanized-3step", "mech/steps/mechanized-3step.mp3", 0.0f, 1.05f, StepKind.Multi);
		RegisterNamedSlice("m3_b", "mechanized-3step", "mech/steps/mechanized-3step.mp3", 1.05f, 1.95f, StepKind.Multi);
		RegisterNamedSlice("m3_c", "mechanized-3step", "mech/steps/mechanized-3step.mp3", 1.95f, 2.70f, StepKind.Multi);

		RegisterNamedWhole("ap_005", "mech/steps/audiopapkin-big-robot-footstep-005-370113.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-005-370113.mp3", StepKind.Single));
		RegisterNamedWhole("ap_008", "mech/steps/audiopapkin-big-robot-footstep-008-425967.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-008-425967.mp3", StepKind.Single));
		RegisterNamedWhole("ap_010", "mech/steps/audiopapkin-big-robot-footstep-010-426482.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-010-426482.mp3", StepKind.Single));
		RegisterNamedWhole("ap_013", "mech/steps/audiopapkin-big-robot-footstep-013-445101.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-013-445101.mp3", StepKind.Single));
		RegisterNamedWhole("ap_014", "mech/steps/audiopapkin-big-robot-footstep-014-445104.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-014-445104.mp3", StepKind.Single));
		RegisterNamedWhole("ap_015", "mech/steps/audiopapkin-big-robot-footstep-015-445103.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-015-445103.mp3", StepKind.Single));
		RegisterNamedWhole("ap_016", "mech/steps/audiopapkin-big-robot-footstep-016-445102.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-016-445102.mp3", StepKind.Single));
		RegisterNamedWhole("ap_017", "mech/steps/audiopapkin-big-robot-footstep-017-499567.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-017-499567.mp3", StepKind.Single));
		RegisterNamedWhole("ap_020", "mech/steps/audiopapkin-big-robot-footstep-020-500843.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-020-500843.mp3", StepKind.Single));
		RegisterNamedWhole("ap_330", "mech/steps/audiopapkin-big-robot-footstep-330678.mp3",
			KindOf(kinds, "audiopapkin-big-robot-footstep-330678.mp3", StepKind.Single));
		RegisterNamedWhole("fs_001", "mech/steps/freesound_community-mech_step_001-87175.mp3",
			KindOf(kinds, "freesound_community-mech_step_001-87175.mp3", StepKind.Single));

		// Shared fallback = biped-safe singles only.
		var master = GetOrCreatePool(MechStepPool);
		foreach (var id in _allStepIds)
		{
			if (_namedSteps[id].Kind == StepKind.Single)
				master.Add(_namedSteps[id]);
		}

		AssignLegsStepPools();
	}

	private void RegisterMechAirMovePack()
	{
		AddWholeFilesToPool(MechAirMovePool, "mech/ambient/movements.mp3");
	}

	private void RegisterDryFirePacks()
	{
		AddWholeFilesToPool(DryFirePool,
			"weapons/dry_fire/empty-machine-gun-firing-clicks.mp3",
			"weapons/dry_fire/empty-rifle-clip.mp3",
			"weapons/dry_fire/empty-gun-shot.mp3");
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

	private void RegisterNamedSlice(
		string stepId, string packKey, string fileName, float start, float end, StepKind kind)
	{
		if (!_packs.ContainsKey(packKey))
		{
			var stream = LoadAudioFile($"{SfxDir}/{fileName}");
			if (stream == null)
				return;
			_packs[packKey] = stream;
		}

		_namedSteps[stepId] = new PackSlice(packKey, start, end, kind);
		_allStepIds.Add(stepId);
	}

	private void RegisterNamedWhole(string stepId, string fileName, StepKind kind = StepKind.Single)
	{
		var stream = LoadAudioFile($"{SfxDir}/{fileName}");
		if (stream == null)
			return;

		var packKey = System.IO.Path.GetFileNameWithoutExtension(fileName);
		_packs[packKey] = stream;
		_namedSteps[stepId] = PackSlice.Whole(packKey, kind);
		_allStepIds.Add(stepId);
	}

	/// <summary>
	/// Labels from res://audio/sfx/mech/steps/step_kind.txt.
	/// Flip a file to multi if it has several hits — bipeds will stop using it.
	/// </summary>
	private static Dictionary<string, StepKind> LoadStepKindLabels()
	{
		var map = new Dictionary<string, StepKind>(System.StringComparer.OrdinalIgnoreCase);
		const string path = $"{SfxDir}/mech/steps/step_kind.txt";
		if (!Godot.FileAccess.FileExists(path))
			return map;

		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return map;

		while (!file.EofReached())
		{
			var line = (file.GetLine() ?? "").Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;
			var hash = line.IndexOf('#');
			if (hash >= 0)
				line = line[..hash].Trim();
			var parts = line.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				continue;

			var fileName = parts[0].Trim();
			var kindToken = parts[1].Trim().ToLowerInvariant();
			var kind = kindToken switch
			{
				"multi" or "multistep" or "multi-step" or "multistomp" => StepKind.Multi,
				"single" or "one" or "1" => StepKind.Single,
				_ => StepKind.Single
			};
			map[fileName] = kind;
			map[System.IO.Path.GetFileNameWithoutExtension(fileName)] = kind;
		}

		return map;
	}

	private static StepKind KindOf(Dictionary<string, StepKind> labels, string fileName, StepKind fallback)
	{
		if (labels.TryGetValue(fileName, out var byFile))
			return byFile;
		var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
		return labels.TryGetValue(stem, out var byStem) ? byStem : fallback;
	}

	/// <summary>
	/// One fixed step clip per legs kit (see legs_step.txt). No random bank deal.
	/// </summary>
	private void AssignLegsStepPools()
	{
		var map = LoadLegsStepAssignments();
		foreach (var legsId in GaitLegsPartIds)
		{
			var pool = GetOrCreatePool(LegsPoolId(legsId));
			pool.Clear();

			if (!map.TryGetValue(legsId, out var stepId)
			    || !_namedSteps.TryGetValue(stepId, out var slice))
			{
				GD.PushWarning($"SfxService: no step assignment for legs '{legsId}'.");
				continue;
			}

			// Bipeds must not get multi packs even if the txt is wrong.
			if (!HexGaitLegsPartIds.Contains(legsId) && slice.Kind == StepKind.Multi)
			{
				GD.PushWarning(
					$"SfxService: legs '{legsId}' mapped to multi step '{stepId}' — bipeds need a single.");
				continue;
			}

			pool.Add(slice);
		}
	}

	/// <summary>legs_part_id → step_id from res://audio/sfx/mech/steps/legs_step.txt.</summary>
	private static Dictionary<string, string> LoadLegsStepAssignments()
	{
		var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
		const string path = $"{SfxDir}/mech/steps/legs_step.txt";
		if (!Godot.FileAccess.FileExists(path))
			return map;

		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return map;

		while (!file.EofReached())
		{
			var line = (file.GetLine() ?? "").Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;
			var hash = line.IndexOf('#');
			if (hash >= 0)
				line = line[..hash].Trim();
			var parts = line.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				continue;
			map[parts[0].Trim()] = parts[1].Trim();
		}

		return map;
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

		if (path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
			return ParseWavPcm(bytes);

		return null;
	}

	/// <summary>Minimal PCM WAV loader for packs that have not been Godot-imported yet.</summary>
	private static AudioStreamWav? ParseWavPcm(byte[] bytes)
	{
		if (bytes.Length < 44)
			return null;
		if (bytes[0] != (byte)'R' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F' || bytes[3] != (byte)'F')
			return null;

		var channels = BitConverter.ToUInt16(bytes, 22);
		var sampleRate = BitConverter.ToInt32(bytes, 24);
		var bitsPerSample = BitConverter.ToUInt16(bytes, 34);
		var dataOffset = -1;
		var dataSize = 0;
		var offset = 12;
		while (offset + 8 <= bytes.Length)
		{
			var id0 = (char)bytes[offset];
			var id1 = (char)bytes[offset + 1];
			var id2 = (char)bytes[offset + 2];
			var id3 = (char)bytes[offset + 3];
			var chunkSize = BitConverter.ToInt32(bytes, offset + 4);
			offset += 8;
			if (id0 == 'd' && id1 == 'a' && id2 == 't' && id3 == 'a')
			{
				dataOffset = offset;
				dataSize = Mathf.Max(0, Mathf.Min(chunkSize, bytes.Length - offset));
				break;
			}

			offset += Mathf.Max(0, chunkSize);
		}

		if (dataOffset < 0 || dataSize <= 0)
			return null;

		var pcm = new byte[dataSize];
		System.Buffer.BlockCopy(bytes, dataOffset, pcm, 0, dataSize);

		var format = bitsPerSample switch
		{
			8 => AudioStreamWav.FormatEnum.Format8Bits,
			16 => AudioStreamWav.FormatEnum.Format16Bits,
			_ => AudioStreamWav.FormatEnum.Format16Bits
		};

		return new AudioStreamWav
		{
			Data = pcm,
			Format = format,
			MixRate = sampleRate > 0 ? sampleRate : 44100,
			Stereo = channels >= 2
		};
	}

	public static void Play(string id, float pitch = 1f, float volumeDb = 0f)
	{
		Instance?.PlayInternal(id, pitch, volumeDb);
	}

	public static void PlayUi(string id) => Play(id, 1f, -2f);

	/// <summary>UI deny / error tones from <c>audio/sfx/ui/errors</c>.</summary>
	public static void PlayUiError(UiErrorTone tone = UiErrorTone.Quick, float volumeDb = -5f)
	{
		Instance?.PlayUiErrorInternal(tone, volumeDb);
	}

	/// <summary>
	/// World one-shot faded by distance to the local listener (mech or camera).
	/// Silent beyond <paramref name="hearRange"/>.
	/// </summary>
	public static void PlayWorld(
		string id,
		Vector3 worldPosition,
		float pitch = 1f,
		float volumeDb = 0f,
		float hearRange = CombatHearRange)
	{
		Instance?.PlayWorldInternal(id, worldPosition, pitch, volumeDb, hearRange);
	}

	public static void Play3D(string id, Vector3 worldPosition, float pitch = 1f, float volumeDb = 0f)
	{
		PlayWorld(id, worldPosition, pitch, volumeDb);
	}

	/// <summary>Projectile / melee strike on MAP armor.</summary>
	public static void PlayImpactArmor(Vector3 worldPosition, float volumeDb = -2f, float hearRange = CombatHearRange)
	{
		Instance?.PlayImpactInternal(ImpactArmorPool, worldPosition, volumeDb, hearRange, "weapon_hit");
	}

	/// <summary>Projectile strike on cover / world geometry.</summary>
	public static void PlayImpactCover(Vector3 worldPosition, float volumeDb = -4f, float hearRange = CombatHearRange)
	{
		Instance?.PlayImpactInternal(ImpactCoverPool, worldPosition, volumeDb, hearRange, "weapon_hit");
	}

	/// <summary>Held shield deflect — clang on the face.</summary>
	public static void PlayImpactShield(Vector3 worldPosition, float volumeDb = -2f, float hearRange = CombatHearRange)
	{
		Instance?.PlayImpactInternal(ImpactShieldPool, worldPosition, volumeDb, hearRange, "weapon_hit");
	}

	/// <summary>Graze / very low damage tick on armor.</summary>
	public static void PlayImpactLight(Vector3 worldPosition, float volumeDb = -5f, float hearRange = CombatHearRange)
	{
		Instance?.PlayImpactInternal(ImpactLightPool, worldPosition, volumeDb, hearRange, "weapon_hit");
	}

	/// <summary>Pick light vs full armor impact from damage dealt through to structure.</summary>
	public static void PlayImpactArmorOrLight(
		Vector3 worldPosition,
		float damageThrough,
		float volumeDbArmor = -2f,
		float volumeDbLight = -5f,
		float hearRange = CombatHearRange)
	{
		if (damageThrough <= LightImpactDamage)
			PlayImpactLight(worldPosition, volumeDbLight, hearRange);
		else
			PlayImpactArmor(worldPosition, volumeDbArmor, hearRange);
	}

	/// <summary>
	/// Shatter / demolition one-shot scaled by burst size.
	/// Small = props; medium = cover chunks; heavy = buildings / self-destruct.
	/// </summary>
	public static void PlayDestruction(
		Vector3 worldPosition,
		Vector3 sourceSize,
		int pieceCount,
		float hearRange = DestructionHearRange)
	{
		Instance?.PlayDestructionInternal(worldPosition, sourceSize, pieceCount, hearRange);
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

	/// <summary>Airborne gait clank — legs cycling with no ground plant (Mech bus).</summary>
	public static void PlayMechAirMove(float volumeDb = 0f)
	{
		Instance?.PlayPoolSlice(MechAirMovePool, volumeDb, pitchJitter: 0.05f, stepBank: true);
	}

	/// <summary>Footfall for this legs kit — fixed clip from legs_step.txt (no random pick).</summary>
	public static void PlayMechStepForLegs(string legsPartId, float volumeDb = -2f)
	{
		if (Instance == null)
			return;
		if (!string.IsNullOrEmpty(legsPartId)
		    && Instance._slicePools.TryGetValue(LegsPoolId(legsPartId), out var pool)
		    && pool.Count > 0)
		{
			Instance.PlayPoolSlice(LegsPoolId(legsPartId), volumeDb, pitchJitter: 0.02f);
			return;
		}

		Instance.PlayPoolSlice(MechStepPool, volumeDb, pitchJitter: 0.02f);
	}

	/// <summary>Click/clack when a weapon lacks power to discharge.</summary>
	public static void PlayDryFire(float volumeDb = -4f)
	{
		Instance?.PlayPoolSlice(DryFirePool, volumeDb, pitchJitter: 0.06f, stepBank: false);
	}

	/// <summary>Weapon muzzle report (ballistic clips or per-gun energy laser).</summary>
	public static void PlayBallisticFire(
		PartData part,
		float volumeDb = -3f,
		Vector3? origin = null,
		bool fullVolume = false,
		float hearRange = CombatHearRange)
	{
		Instance?.PlayWeaponFireInternal(part, volumeDb, origin, fullVolume, hearRange);
	}

	/// <summary>Energy / laser fire for a specific gun (fixed clip per part id).</summary>
	public static void PlayEnergyFire(
		PartData part,
		float volumeDb = -4f,
		Vector3? origin = null,
		bool fullVolume = false,
		float hearRange = CombatHearRange)
	{
		Instance?.PlayEnergyFireInternal(part.Id, volumeDb, origin, fullVolume, hearRange);
	}

	/// <summary>Support tower / non-part energy muzzle (shared spare clip).</summary>
	public static void PlayEnergyFireSupport(
		float volumeDb = -6f,
		Vector3? origin = null,
		bool fullVolume = false,
		float hearRange = CombatHearRange)
	{
		Instance?.PlayEnergyFireInternal(null, volumeDb, origin, fullVolume, hearRange, support: true);
	}

	/// <summary>Magazine reload — light for rifles / fast mags, heavy for cannons.</summary>
	public static void PlayBallisticReload(PartData part, float volumeDb = -5f)
	{
		if (Instance == null || part.WeaponFamily != WeaponFamily.Ballistic)
			return;

		var heavy = part.VisualKind == "cannon" || part.ReloadTime >= 2.7f;
		Instance.PlayInternal(heavy ? "reload_heavy" : "reload_light", 1f, volumeDb);
	}

	private void PlayWeaponFireInternal(
		PartData part,
		float volumeDb,
		Vector3? origin,
		bool fullVolume,
		float hearRange)
	{
		if (part.WeaponFamily == WeaponFamily.Energy)
		{
			PlayEnergyFireInternal(part.Id, volumeDb, origin, fullVolume, hearRange);
			return;
		}

		string clipId;
		float pitch = 1f;
		if (part.WeaponFamily != WeaponFamily.Ballistic)
		{
			clipId = "weapon_fire";
		}
		else if (BallisticFireClipByPart.TryGetValue(part.Id, out var mapped)
		         && _clips.ContainsKey(mapped))
		{
			clipId = mapped;
			pitch = 1f + _rng.RandfRange(-0.05f, 0.05f);
		}
		else
		{
			clipId = part.VisualKind == "cannon" || part.FireRate >= 5f
				? "ballistic_lmg_single"
				: "ballistic_rifle_1";
			if (!_clips.ContainsKey(clipId))
				clipId = "weapon_fire";
			else
				pitch = 1f + _rng.RandfRange(-0.05f, 0.05f);
		}

		if (fullVolume || origin == null)
		{
			PlayInternal(clipId, pitch, volumeDb);
			return;
		}

		PlayWorldInternal(clipId, origin.Value, pitch, volumeDb, hearRange);
	}

	private void PlayEnergyFireInternal(
		string? partId,
		float volumeDb,
		Vector3? origin,
		bool fullVolume,
		float hearRange,
		bool support = false)
	{
		var clipId = EnergyFireFallbackClip;
		if (support)
			clipId = EnergyFireSupportClip;
		else if (!string.IsNullOrEmpty(partId)
		         && EnergyFireClipByPart.TryGetValue(partId, out var mapped))
			clipId = mapped;

		if (!_packs.TryGetValue(clipId, out var stream) && !_clips.TryGetValue(clipId, out stream))
		{
			if (fullVolume || origin == null)
				PlayInternal("weapon_fire", 1f, volumeDb);
			else
				PlayWorldInternal("weapon_fire", origin.Value, 1f, volumeDb, hearRange);
			return;
		}

		if (!fullVolume && origin != null)
		{
			var db = volumeDb;
			if (!TryAttenuateWorld(origin.Value, hearRange, ref db))
				return;
			volumeDb = db;
		}

		// Never steal a playing laser voice — grow the bank if every channel is busy.
		var player = ClaimIdlePlayer(_energyPool, ref _energyPoolIndex)
			?? GrowPoolPlayer(_energyPool, "Energy", "Sfx", ref _energyPoolIndex);
		player.Stream = stream;
		player.PitchScale = 1f;
		player.VolumeDb = volumeDb;
		player.Play();
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

	/// <summary>Random soft/hard dump from SteamReleaseLong, or a short air puff.</summary>
	public static void PlayAirRelease(float volumeDb = -8f)
	{
		Instance?.PlayAirReleaseInternal(volumeDb);
	}

	/// <summary>Internal cooling fan — idle settle / overheat recovery flavor.</summary>
	public static void PlayHeatVentFan(float volumeDb = -10f)
	{
		Instance?.PlayHeatVentFanInternal(volumeDb);
	}

	/// <summary>
	/// Post-sprint / power-empty breath: air-out, air-release, and optionally a steam hiss.
	/// </summary>
	public static void PlayExertionVents(bool heavy = false)
	{
		Instance?.PlayExertionVentsInternal(heavy);
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

		var index = PickWeightedAmbientIndex();
		_ambientLastIndex = index;
		var clip = _ambientClips[index];

		_ambient.Stream = clip.Stream;
		_ambient.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.04f, 0.04f), 0.9f, 1.1f);
		_ambient.VolumeDb = clip.VolumeDb + _rng.RandfRange(-1.5f, 1f);
		_ambient.Play();
		_ambientPhase = AmbientPhase.Playing;
	}

	private int PickWeightedAmbientIndex()
	{
		float total = 0f;
		for (var i = 0; i < _ambientClips.Count; i++)
		{
			if (i == _ambientLastIndex && _ambientClips.Count > 1)
				continue;
			total += _ambientClips[i].Weight;
		}

		var roll = _rng.Randf() * total;
		float acc = 0f;
		var fallback = 0;
		for (var i = 0; i < _ambientClips.Count; i++)
		{
			if (i == _ambientLastIndex && _ambientClips.Count > 1)
				continue;
			fallback = i;
			acc += _ambientClips[i].Weight;
			if (roll <= acc)
				return i;
		}

		return fallback;
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
		// Fans spool as the dump clears — recovery beat after the hiss fades in.
		PlayHeatVentFan(-13f);
		PlaySteamReleaseHard(-7f);
	}

	private void PlayAirReleaseInternal(float volumeDb)
	{
		// Prefer the sliced long take; fall back to short balloon/generic puffs.
		if (_slicePools.TryGetValue(SteamReleasePool, out var steam) && steam.Count > 0)
		{
			PlayPoolSlice(SteamReleasePool, volumeDb, pitchJitter: 0.05f, stepBank: false);
			return;
		}

		PlayPoolSlice(AirPuffPool, volumeDb, pitchJitter: 0.06f, stepBank: false);
	}

	private void PlayHeatVentFanInternal(float volumeDb)
	{
		if (!_clips.TryGetValue("air_vent_fan", out var clip))
			return;

		var player = ClaimPlayer(_stepPool, ref _stepPoolIndex);
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.03f, 0.03f), 0.92f, 1.08f);
		player.VolumeDb = volumeDb;
		player.Play();
	}

	private void PlayExertionVentsInternal(bool heavy)
	{
		if (_exertionVentCooldown > 0f || _overheatFxActive)
			return;
		_exertionVentCooldown = heavy ? 2.4f : 1.8f;

		// Soft jog stop: one puff. Hard sprint / empty power: both puffs + hiss.
		if (heavy)
		{
			PlayClipOnMechBus("air_out", -6f, 0.04f);
			PlayClipOnMechBus("air_release", -5f, 0.04f);
			PlayClipOnMechBus("steam_hissing", -11f, 0.03f);
		}
		else
		{
			PlayClipOnMechBus(PickExertionPuff(), -8f, 0.04f);
		}
	}

	private string PickExertionPuff()
	{
		if (_rng.Randf() < 0.5f && _clips.ContainsKey("air_out"))
			return "air_out";
		if (_clips.ContainsKey("air_release"))
			return "air_release";
		return _clips.ContainsKey("air_out") ? "air_out" : "steam_hissing";
	}

	private void PlayClipOnMechBus(string id, float volumeDb, float pitchJitter)
	{
		if (!_clips.TryGetValue(id, out var clip))
			return;

		var player = ClaimPlayer(_stepPool, ref _stepPoolIndex);
		player.Stream = clip;
		player.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-pitchJitter, pitchJitter), 0.9f, 1.1f);
		player.VolumeDb = volumeDb;
		player.Play();
	}

	private void PlaySteamReleaseHard(float volumeDb)
	{
		if (!_slicePools.TryGetValue(SteamReleasePool, out var pool) || pool.Count < 2)
		{
			PlayAirReleaseInternal(volumeDb);
			return;
		}

		// Index 1 is the louder 0.5→end dump.
		var slice = pool[1];
		if (!_packs.TryGetValue(slice.PackKey, out var stream))
			return;

		var player = ClaimPlayer(_combatPool, ref _combatPoolIndex);
		player.Stream = stream;
		player.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-0.04f, 0.04f), 0.9f, 1.1f);
		player.VolumeDb = volumeDb;
		player.Play(slice.Start);

		var stopPlayer = player;
		var tween = CreateTween();
		_sliceStopTweens[player] = tween;
		tween.TweenInterval(slice.Duration);
		tween.TweenCallback(Callable.From(() =>
		{
			_sliceStopTweens.Remove(stopPlayer);
			if (GodotObject.IsInstanceValid(stopPlayer) && stopPlayer.Playing)
				stopPlayer.Stop();
		}));
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

		// Stop any prior slice-stop tween on this voice so we don't cut the new plant short.
		if (_sliceStopTweens.TryGetValue(player, out var priorTween))
		{
			priorTween.Kill();
			_sliceStopTweens.Remove(player);
		}

		player.Stream = stream;
		player.PitchScale = Mathf.Clamp(1f + _rng.RandfRange(-pitchJitter, pitchJitter), 0.85f, 1.15f);
		player.VolumeDb = volumeDb;

		if (slice.WholeFile)
		{
			// Let the full footfall play — no early Stop().
			player.Play();
			return;
		}

		player.Play(slice.Start);

		var duration = slice.Duration;
		var stopSliced = player;
		var sliceTween = CreateTween();
		_sliceStopTweens[player] = sliceTween;
		sliceTween.TweenInterval(duration);
		sliceTween.TweenCallback(Callable.From(() =>
		{
			_sliceStopTweens.Remove(stopSliced);
			if (GodotObject.IsInstanceValid(stopSliced) && stopSliced.Playing)
				stopSliced.Stop();
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

	private void PlayUiErrorInternal(UiErrorTone tone, float volumeDb)
	{
		var id = tone switch
		{
			UiErrorTone.Incorrect => UiErrorIncorrect,
			UiErrorTone.DeeDoo => UiErrorDeeDoo,
			UiErrorTone.BuzzBuzz => UiErrorBuzzBuzz,
			_ => UiErrorQuick
		};
		if (!_clips.TryGetValue(id, out var clip))
			return;

		var player = ClaimPlayer(_uiSfxPool, ref _uiSfxPoolIndex);
		player.Stream = clip;
		player.PitchScale = 1f;
		player.VolumeDb = volumeDb;
		player.Play();
	}

	private void PlayWorldInternal(
		string id,
		Vector3 worldPosition,
		float pitch,
		float volumeDb,
		float hearRange)
	{
		if (!_clips.TryGetValue(id, out _))
			return;
		if (!TryAttenuateWorld(worldPosition, hearRange, ref volumeDb))
			return;
		PlayInternal(id, pitch, volumeDb);
	}

	private void PlayImpactInternal(
		string poolId,
		Vector3 worldPosition,
		float volumeDb,
		float hearRange,
		string fallbackClipId)
	{
		if (!TryAttenuateWorld(worldPosition, hearRange, ref volumeDb))
			return;

		if (_slicePools.TryGetValue(poolId, out var pool) && pool.Count > 0)
		{
			PlayPoolSlice(poolId, volumeDb, pitchJitter: 0.06f, stepBank: false);
			return;
		}

		PlayInternal(fallbackClipId, 1f + _rng.RandfRange(-0.06f, 0.06f), volumeDb);
	}

	private void PlayDestructionInternal(
		Vector3 worldPosition,
		Vector3 sourceSize,
		int pieceCount,
		float hearRange)
	{
		var volume = Mathf.Abs(sourceSize.X * sourceSize.Y * sourceSize.Z);
		string poolId;
		float volumeDb;
		if (pieceCount >= 28 || volume >= 40f)
		{
			poolId = DestroyHeavyPool;
			volumeDb = -1f;
			hearRange = Mathf.Max(hearRange, 70f);
		}
		else if (pieceCount >= 16 || volume >= 6f)
		{
			poolId = DestroyMediumPool;
			volumeDb = -2f;
		}
		else
		{
			poolId = DestroySmallPool;
			volumeDb = -3f;
			hearRange = Mathf.Min(hearRange, 40f);
		}

		PlayImpactInternal(poolId, worldPosition, volumeDb, hearRange, "explosion");
	}

	private static bool TryAttenuateWorld(Vector3 worldPosition, float hearRange, ref float volumeDb)
	{
		if (!TryGetListenerPosition(out var listener))
			return false;

		hearRange = Mathf.Max(1f, hearRange);
		var dist = listener.DistanceTo(worldPosition);
		if (dist >= hearRange)
			return false;

		var t = dist / hearRange;
		volumeDb += Mathf.Lerp(0f, -48f, t * t);
		return true;
	}

	/// <summary>Local pilot chassis, else active camera — used for world SFX falloff.</summary>
	public static bool TryGetListenerPosition(out Vector3 position)
	{
		position = default;
		var tree = Instance?.GetTree();
		if (tree == null)
			return false;

		foreach (var node in tree.GetNodesInGroup("mechs"))
		{
			if (node is MechController { IsLocalPilot: true } local
			    && GodotObject.IsInstanceValid(local))
			{
				position = local.GlobalPosition;
				return true;
			}
		}

		var cam = Instance?.GetViewport()?.GetCamera3D();
		if (cam != null && GodotObject.IsInstanceValid(cam))
		{
			position = cam.GlobalPosition;
			return true;
		}

		return false;
	}

	private void PlayAt(string id, Vector3 worldPosition, float pitch, float volumeDb)
	{
		PlayWorldInternal(id, worldPosition, pitch, volumeDb, CombatHearRange);
	}

	/// <summary>Prefer an idle voice; only overwrite a playing one when the bank is saturated.</summary>
	private AudioStreamPlayer ClaimPlayer(List<AudioStreamPlayer> pool, ref int roundRobin)
	{
		var idle = ClaimIdlePlayer(pool, ref roundRobin);
		if (idle != null)
			return idle;

		roundRobin = (roundRobin + 1) % pool.Count;
		var forced = pool[roundRobin];
		ClearSliceStop(forced);
		return forced;
	}

	/// <summary>Idle voice only — returns null when every channel is still playing (for layered SFX).</summary>
	private AudioStreamPlayer? ClaimIdlePlayer(List<AudioStreamPlayer> pool, ref int roundRobin)
	{
		if (pool.Count == 0)
			return null;

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

		return null;
	}

	private AudioStreamPlayer GrowPoolPlayer(
		List<AudioStreamPlayer> pool,
		string prefix,
		string bus,
		ref int roundRobin)
	{
		var p = new AudioStreamPlayer
		{
			Name = $"{prefix}_{pool.Count}",
			Bus = bus,
			VolumeDb = -2f
		};
		AddChild(p);
		pool.Add(p);
		roundRobin = 0;
		return p;
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
