using System;
using Godot;

namespace Mechanize;

/// <summary>Client preferences (HUD layout, etc.) stored separately from campaign/profile saves.</summary>
public static class GameSettings
{
	public const string Path = "user://mechanize_settings.cfg";

	/// <summary>Default ~15% smaller than the authored MechHUD layout.</summary>
	public const float DefaultHudScale = 0.85f;
	/// <summary>Default lift off the bottom edge (~10% of available travel).</summary>
	public const float DefaultHudOffsetY = 0.1f;

	public static float HudScale { get; private set; } = DefaultHudScale;
	/// <summary>0 = left edge, 1 = as far right as the HUD still fits.</summary>
	public static float HudOffsetX { get; private set; }
	/// <summary>0 = bottom edge, 1 = lifted toward mid-screen.</summary>
	public static float HudOffsetY { get; private set; } = DefaultHudOffsetY;

	public static event Action? Changed;

	static GameSettings() => Load();

	public static void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) != Error.Ok)
		{
			HudScale = DefaultHudScale;
			HudOffsetX = 0f;
			HudOffsetY = DefaultHudOffsetY;
			return;
		}

		var version = (int)cfg.GetValue("meta", "version", 1);
		HudScale = Mathf.Clamp((float)cfg.GetValue("hud", "scale", DefaultHudScale), 0.5f, 1.5f);
		HudOffsetX = Mathf.Clamp((float)cfg.GetValue("hud", "offset_x", 0f), 0f, 1f);
		HudOffsetY = Mathf.Clamp((float)cfg.GetValue("hud", "offset_y", DefaultHudOffsetY), 0f, 1f);

		// v1 shipped with 0 lift; adopt the new 10% default once.
		if (version < 2)
			HudOffsetY = DefaultHudOffsetY;
	}

	public static void Save()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("meta", "version", 2);
		cfg.SetValue("hud", "scale", HudScale);
		cfg.SetValue("hud", "offset_x", HudOffsetX);
		cfg.SetValue("hud", "offset_y", HudOffsetY);
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

	public static void ResetHudLayout()
	{
		HudScale = DefaultHudScale;
		HudOffsetX = 0f;
		HudOffsetY = DefaultHudOffsetY;
		PersistAndNotify();
	}

	private static void PersistAndNotify()
	{
		Save();
		Changed?.Invoke();
	}
}
