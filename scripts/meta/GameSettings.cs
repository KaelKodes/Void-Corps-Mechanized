using System;
using Godot;

namespace Mechanize;

/// <summary>Where combat HUD bars (integrity, weapons, sensors) are drawn.</summary>
public enum HudBarsMode
{
	/// <summary>Cockpit panels in first person; floating overlay in third person.</summary>
	Auto = 0,
	/// <summary>Prefer cockpit dashboard panels while in first person.</summary>
	Panels = 1,
	/// <summary>Always use the floating screen HUD overlay.</summary>
	Overlay = 2
}

/// <summary>Client preferences (HUD layout, audio buses, etc.) stored separately from campaign/profile saves.</summary>
public static class GameSettings
{
	public const string Path = "user://mechanize_settings.cfg";

	/// <summary>Default ~15% smaller than the authored MechHUD layout.</summary>
	public const float DefaultHudScale = 0.85f;
	/// <summary>Default horizontal placement: mid-screen (0 = left, 1 = right).</summary>
	public const float DefaultHudOffsetX = 0.5f;
	/// <summary>Default lift off the bottom edge (~10% of available travel).</summary>
	public const float DefaultHudOffsetY = 0.1f;

	/// <summary>Non-music channels cannot go fully silent.</summary>
	public const float MinAudibleLinear = 0.05f;
	public const float DefaultMasterVolume = 1f;
	public const float DefaultMusicVolume = 1f;
	public const float DefaultSfxVolume = 1f;
	public const float DefaultUiVolume = 1f;
	public const float DefaultVoiceVolume = 1f;
	public const float DefaultMechVolume = 1f;

	public static float HudScale { get; private set; } = DefaultHudScale;
	/// <summary>0 = left edge, 0.5 = centered, 1 = as far right as the HUD still fits.</summary>
	public static float HudOffsetX { get; private set; } = DefaultHudOffsetX;
	/// <summary>0 = bottom edge, 1 = lifted toward mid-screen.</summary>
	public static float HudOffsetY { get; private set; } = DefaultHudOffsetY;
	/// <summary>Legacy: PWR/SPD flanking the MAP. Unused while meters live on Integrity.</summary>
	public static bool MetersBesideMech { get; private set; }
	/// <summary>Where integrity / weapon / sensor bars are drawn. Default Auto.</summary>
	public static HudBarsMode HudBarsMode { get; private set; } = HudBarsMode.Auto;
	/// <summary>FP cockpit seat — meters forward toward the glass from CockpitAnchor.</summary>
	public static float SeatForward { get; private set; }
	/// <summary>FP cockpit seat — meters up from CockpitAnchor.</summary>
	public static float SeatUp { get; private set; }
	/// <summary>When true, FP window objective panel is collapsed (toggle with O).</summary>
	public static bool ObjectiveHudMinimized { get; private set; }

	public const float SeatForwardMin = -0.12f;
	public const float SeatForwardMax = 0.45f;
	public const float SeatUpMin = -0.18f;
	public const float SeatUpMax = 0.4f;

	/// <summary>0..1 linear gain for Master bus.</summary>
	public static float MasterVolume { get; private set; } = DefaultMasterVolume;
	/// <summary>0..1 linear gain for Music bus. May be fully muted.</summary>
	public static float MusicVolume { get; private set; } = DefaultMusicVolume;
	/// <summary>MinAudibleLinear..1 combat SFX bus.</summary>
	public static float SfxVolume { get; private set; } = DefaultSfxVolume;
	/// <summary>MinAudibleLinear..1 UI click/confirm bus.</summary>
	public static float UiVolume { get; private set; } = DefaultUiVolume;
	/// <summary>MinAudibleLinear..1 dialogue / robot VO bus.</summary>
	public static float VoiceVolume { get; private set; } = DefaultVoiceVolume;
	/// <summary>MinAudibleLinear..1 mech steps / chassis bus.</summary>
	public static float MechVolume { get; private set; } = DefaultMechVolume;

	public static event Action? Changed;

	static GameSettings() => Load();

	public static void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) != Error.Ok)
		{
			HudScale = DefaultHudScale;
			HudOffsetX = DefaultHudOffsetX;
			HudOffsetY = DefaultHudOffsetY;
			MetersBesideMech = false;
			HudBarsMode = HudBarsMode.Auto;
			SeatForward = 0f;
			SeatUp = 0f;
			ObjectiveHudMinimized = false;
			ResetAudioVolumes(persist: false);
			return;
		}

		var version = (int)cfg.GetValue("meta", "version", 1);
		HudScale = Mathf.Clamp((float)cfg.GetValue("hud", "scale", DefaultHudScale), 0.5f, 1.5f);
		HudOffsetX = Mathf.Clamp((float)cfg.GetValue("hud", "offset_x", DefaultHudOffsetX), 0f, 1f);
		HudOffsetY = Mathf.Clamp((float)cfg.GetValue("hud", "offset_y", DefaultHudOffsetY), 0f, 1f);
		MetersBesideMech = (bool)cfg.GetValue("hud", "meters_beside_mech", false);
		HudBarsMode = LoadHudBarsMode(cfg);
		SeatForward = Mathf.Clamp(
			(float)cfg.GetValue("cockpit", "seat_forward", 0f), SeatForwardMin, SeatForwardMax);
		SeatUp = Mathf.Clamp(
			(float)cfg.GetValue("cockpit", "seat_up", 0f), SeatUpMin, SeatUpMax);
		ObjectiveHudMinimized = (bool)cfg.GetValue("cockpit", "objective_minimized", false);

		MasterVolume = ClampMaster((float)cfg.GetValue("audio", "master", DefaultMasterVolume));
		MusicVolume = ClampMusic((float)cfg.GetValue("audio", "music", DefaultMusicVolume));
		SfxVolume = ClampAudible((float)cfg.GetValue("audio", "sfx", DefaultSfxVolume));
		UiVolume = ClampAudible((float)cfg.GetValue("audio", "ui", DefaultUiVolume));
		VoiceVolume = ClampAudible((float)cfg.GetValue("audio", "voice", DefaultVoiceVolume));
		MechVolume = ClampAudible((float)cfg.GetValue("audio", "mech", DefaultMechVolume));

		// v1 shipped with 0 lift; adopt the new 10% default once.
		if (version < 2)
			HudOffsetY = DefaultHudOffsetY;
		// v2 shipped bottom-left; adopt bottom-center once.
		if (version < 3)
			HudOffsetX = DefaultHudOffsetX;
		// v3 shipped meters beside the MAP; adopt corner HUD once.
		if (version < 4)
			MetersBesideMech = false;

		if (version < 9)
			Save();

		ApplyAudioBuses();
	}

	public static void Save()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("meta", "version", 9);
		cfg.SetValue("hud", "scale", HudScale);
		cfg.SetValue("hud", "offset_x", HudOffsetX);
		cfg.SetValue("hud", "offset_y", HudOffsetY);
		cfg.SetValue("hud", "meters_beside_mech", MetersBesideMech);
		cfg.SetValue("hud", "bars_mode", (int)HudBarsMode);
		cfg.SetValue("cockpit", "seat_forward", SeatForward);
		cfg.SetValue("cockpit", "seat_up", SeatUp);
		cfg.SetValue("cockpit", "objective_minimized", ObjectiveHudMinimized);
		cfg.SetValue("audio", "master", MasterVolume);
		cfg.SetValue("audio", "music", MusicVolume);
		cfg.SetValue("audio", "sfx", SfxVolume);
		cfg.SetValue("audio", "ui", UiVolume);
		cfg.SetValue("audio", "voice", VoiceVolume);
		cfg.SetValue("audio", "mech", MechVolume);
		cfg.Save(Path);
	}

	public static void SetHudScale(float value)
	{
		HudScale = Mathf.Clamp(value, 0.5f, 1.5f);
		PersistAndNotify();
	}

	public static void SetHudOffsetX(float value)
	{
		HudOffsetX = Mathf.Clamp(value, 0f, 1f);
		PersistAndNotify();
	}

	public static void SetHudOffsetY(float value)
	{
		HudOffsetY = Mathf.Clamp(value, 0f, 1f);
		PersistAndNotify();
	}

	public static void SetMetersBesideMech(bool value)
	{
		MetersBesideMech = value;
		PersistAndNotify();
	}

	public static void SetHudBarsMode(HudBarsMode value)
	{
		HudBarsMode = value;
		PersistAndNotify();
	}

	public static void SetSeatOffset(float forward, float up)
	{
		SeatForward = Mathf.Clamp(forward, SeatForwardMin, SeatForwardMax);
		SeatUp = Mathf.Clamp(up, SeatUpMin, SeatUpMax);
		PersistAndNotify();
	}

	public static void ResetSeatOffset()
	{
		SeatForward = 0f;
		SeatUp = 0f;
		PersistAndNotify();
	}

	public static void SetObjectiveHudMinimized(bool minimized)
	{
		ObjectiveHudMinimized = minimized;
		PersistAndNotify();
	}

	public static void CycleHudBarsMode()
	{
		HudBarsMode = HudBarsMode switch
		{
			HudBarsMode.Auto => HudBarsMode.Panels,
			HudBarsMode.Panels => HudBarsMode.Overlay,
			_ => HudBarsMode.Auto
		};
		PersistAndNotify();
	}

	/// <summary>
	/// Whether combat readouts should bind to cockpit dashboard panels for this camera / chassis.
	/// Auto and Panels both use panels in first person; Overlay never does.
	/// </summary>
	public static bool ShouldUseDiegeticHudBars(bool isFirstPerson, bool hasCockpitScreens) =>
		HudBarsMode != HudBarsMode.Overlay
		&& isFirstPerson
		&& hasCockpitScreens;

	public static string HudBarsModeLabel() => HudBarsMode switch
	{
		HudBarsMode.Auto => "HUD bars: Auto",
		HudBarsMode.Panels => "HUD bars: First Person (panels)",
		_ => "HUD bars: Overlay"
	};

	public static void SetMasterVolume(float value)
	{
		MasterVolume = ClampMaster(value);
		ApplyAudioBuses();
		PersistAndNotify();
	}

	public static void SetMusicVolume(float value)
	{
		MusicVolume = ClampMusic(value);
		ApplyAudioBuses();
		PersistAndNotify();
	}

	public static void SetSfxVolume(float value)
	{
		SfxVolume = ClampAudible(value);
		ApplyAudioBuses();
		PersistAndNotify();
	}

	public static void SetUiVolume(float value)
	{
		UiVolume = ClampAudible(value);
		ApplyAudioBuses();
		PersistAndNotify();
	}

	public static void SetVoiceVolume(float value)
	{
		VoiceVolume = ClampAudible(value);
		ApplyAudioBuses();
		PersistAndNotify();
	}

	public static void SetMechVolume(float value)
	{
		MechVolume = ClampAudible(value);
		ApplyAudioBuses();
		PersistAndNotify();
	}

	public static void ResetHudLayout()
	{
		HudScale = DefaultHudScale;
		HudOffsetX = DefaultHudOffsetX;
		HudOffsetY = DefaultHudOffsetY;
		MetersBesideMech = false;
		HudBarsMode = HudBarsMode.Auto;
		SeatForward = 0f;
		SeatUp = 0f;
		PersistAndNotify();
	}

	public static void ResetAudioVolumes(bool persist = true)
	{
		MasterVolume = DefaultMasterVolume;
		MusicVolume = DefaultMusicVolume;
		SfxVolume = DefaultSfxVolume;
		UiVolume = DefaultUiVolume;
		VoiceVolume = DefaultVoiceVolume;
		MechVolume = DefaultMechVolume;
		ApplyAudioBuses();
		if (persist)
			PersistAndNotify();
	}

	/// <summary>Push linear gains onto Godot buses. Safe to call before buses exist.</summary>
	public static void ApplyAudioBuses()
	{
		SetBusLinear("Master", MasterVolume, allowMute: false);
		SetBusLinear("Music", MusicVolume, allowMute: true);
		SetBusLinear("Sfx", SfxVolume, allowMute: false);
		SetBusLinear("Ui", UiVolume, allowMute: false);
		SetBusLinear("Voice", VoiceVolume, allowMute: false);
		SetBusLinear("Mech", MechVolume, allowMute: false);
	}

	private static HudBarsMode LoadHudBarsMode(ConfigFile cfg)
	{
		if (cfg.HasSectionKey("hud", "bars_mode"))
		{
			var raw = (int)cfg.GetValue("hud", "bars_mode", (int)HudBarsMode.Auto);
			return raw switch
			{
				(int)HudBarsMode.Panels => HudBarsMode.Panels,
				(int)HudBarsMode.Overlay => HudBarsMode.Overlay,
				_ => HudBarsMode.Auto
			};
		}

		// Pre-v7 boolean: true meant cockpit panels in FP (hybrid) → Auto; false → Overlay.
		if (cfg.HasSectionKey("hud", "first_person_hud"))
			return (bool)cfg.GetValue("hud", "first_person_hud", true)
				? HudBarsMode.Auto
				: HudBarsMode.Overlay;

		return HudBarsMode.Auto;
	}

	private static void SetBusLinear(string busName, float linear, bool allowMute)
	{
		var idx = AudioServer.GetBusIndex(busName);
		if (idx < 0)
			return;

		var gain = allowMute
			? Mathf.Clamp(linear, 0f, 1f)
			: Mathf.Clamp(linear, MinAudibleLinear, 1f);

		if (gain <= 0.0001f)
		{
			AudioServer.SetBusMute(idx, true);
			AudioServer.SetBusVolumeDb(idx, -80f);
			return;
		}

		AudioServer.SetBusMute(idx, false);
		AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(gain));
	}

	private static float ClampMaster(float value) => Mathf.Clamp(value, MinAudibleLinear, 1f);
	private static float ClampMusic(float value) => Mathf.Clamp(value, 0f, 1f);
	private static float ClampAudible(float value) => Mathf.Clamp(value, MinAudibleLinear, 1f);

	private static void PersistAndNotify()
	{
		Save();
		Changed?.Invoke();
	}
}
