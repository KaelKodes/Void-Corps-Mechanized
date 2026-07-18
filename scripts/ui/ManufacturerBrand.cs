using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Authored manufacturer house marks. Falls back to procedural geometry when an asset is missing.
/// Manufacturers are kit brands — not corps.
/// </summary>
public static class ManufacturerBrand
{
	private static readonly Dictionary<string, string> EmblemPaths = new()
	{
		["brimforge"] = "res://art/ui/brimforge_mark.png",
		["ourotech"] = "res://art/ui/ourotech_ouroboros.png",
		["trinova"] = "res://art/ui/trinova_mark.png",
		["lumina"] = "res://art/ui/lumina_mark.png"
	};

	public static bool TryGetTexture(string manufacturerId, out Texture2D texture)
	{
		texture = null!;
		if (string.IsNullOrEmpty(manufacturerId)
		    || !EmblemPaths.TryGetValue(manufacturerId, out var path)
		    || !ResourceLoader.Exists(path))
			return false;

		texture = GD.Load<Texture2D>(path);
		return texture != null;
	}

	/// <summary>Centered emblem TextureRect, or null if no authored mark exists.</summary>
	public static TextureRect? MakeEmblem(string manufacturerId, float size, float alpha = 1f)
	{
		if (!TryGetTexture(manufacturerId, out var texture))
			return null;

		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, alpha)
		};
	}

	/// <summary>
	/// Emblem control sized to <paramref name="size"/>. Prefers authored art;
	/// otherwise a procedural ManufacturerMarkDrawer.
	/// </summary>
	public static Control MakeEmblemOrFallback(string manufacturerId, Color accent, float size, float alpha = 1f)
	{
		var authored = MakeEmblem(manufacturerId, size, alpha);
		if (authored != null)
			return authored;

		var fallback = new ManufacturerMarkDrawer
		{
			ManufacturerId = manufacturerId,
			Accent = accent,
			CustomMinimumSize = new Vector2(size, size),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, alpha)
		};
		return fallback;
	}
}
