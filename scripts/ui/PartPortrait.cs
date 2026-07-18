using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Procedural part thumbnails until authored art exists. Shape follows VisualKind; color follows Tint.
/// </summary>
public static class PartPortrait
{
	private static readonly Dictionary<string, ImageTexture> Cache = new();

	public static Texture2D Get(PartData? part, int size = 128)
	{
		if (part == null || string.IsNullOrEmpty(part.Id) || part.VisualKind == "empty")
			return GetEmpty(size);

		var key = $"{part.Id}:{size}";
		if (Cache.TryGetValue(key, out var cached))
			return cached;

		var image = BuildImage(part, size);
		var texture = ImageTexture.CreateFromImage(image);
		Cache[key] = texture;
		return texture;
	}

	/// <summary>Fresh procedural plate image (backdrop + silhouette + frame). Caller owns it.</summary>
	public static Image BuildImage(PartData part, int size)
	{
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		FillBackdrop(image);

		var accent = part.Tint;
		var shade = accent.Darkened(0.38f);
		var mid = accent.Darkened(0.18f);
		var light = accent.Lightened(0.28f);
		var edge = accent.Darkened(0.55f);
		var glow = accent.Lightened(0.45f);

		switch (part.VisualKind)
		{
			case "legs_biped":
				// Pelvis / hip block
				FillRect(image, 0.22f, 0.12f, 0.56f, 0.18f, accent);
				FillRect(image, 0.28f, 0.16f, 0.44f, 0.04f, light);
				// Thighs + knees + shins
				FillRect(image, 0.26f, 0.28f, 0.18f, 0.28f, mid);
				FillRect(image, 0.56f, 0.28f, 0.18f, 0.28f, mid);
				FillRect(image, 0.28f, 0.52f, 0.14f, 0.08f, light);
				FillRect(image, 0.58f, 0.52f, 0.14f, 0.08f, light);
				FillRect(image, 0.28f, 0.58f, 0.16f, 0.28f, shade);
				FillRect(image, 0.56f, 0.58f, 0.16f, 0.28f, shade);
				// Feet
				FillRect(image, 0.22f, 0.84f, 0.24f, 0.08f, accent);
				FillRect(image, 0.54f, 0.84f, 0.24f, 0.08f, accent);
				break;
			case "legs_hex":
				FillOval(image, 0.5f, 0.4f, 0.26f, 0.2f, accent);
				FillOval(image, 0.5f, 0.38f, 0.12f, 0.1f, shade);
				for (var i = 0; i < 6; i++)
				{
					var a = i * Mathf.Tau / 6f - Mathf.Pi * 0.5f;
					var cx = 0.5f + Mathf.Cos(a) * 0.34f;
					var cy = 0.5f + Mathf.Sin(a) * 0.34f;
					FillRect(image, cx - 0.045f, cy - 0.14f, 0.09f, 0.3f, mid);
					FillRect(image, cx - 0.06f, cy + 0.12f, 0.12f, 0.06f, light);
				}
				break;
			case "legs_tracks":
				FillRect(image, 0.2f, 0.24f, 0.6f, 0.36f, accent);
				FillRect(image, 0.26f, 0.3f, 0.48f, 0.08f, light);
				FillRect(image, 0.28f, 0.42f, 0.44f, 0.04f, shade);
				FillRect(image, 0.12f, 0.52f, 0.76f, 0.22f, shade);
				FillRect(image, 0.14f, 0.56f, 0.72f, 0.04f, edge);
				FillOval(image, 0.24f, 0.63f, 0.08f, 0.08f, light);
				FillOval(image, 0.42f, 0.63f, 0.08f, 0.08f, light);
				FillOval(image, 0.58f, 0.63f, 0.08f, 0.08f, light);
				FillOval(image, 0.76f, 0.63f, 0.08f, 0.08f, light);
				break;
			case "torso":
				FillRect(image, 0.2f, 0.16f, 0.6f, 0.66f, accent);
				// Shoulder plates
				FillRect(image, 0.12f, 0.2f, 0.14f, 0.22f, mid);
				FillRect(image, 0.74f, 0.2f, 0.14f, 0.22f, mid);
				FillRect(image, 0.28f, 0.24f, 0.44f, 0.14f, shade);
				FillRect(image, 0.32f, 0.28f, 0.36f, 0.04f, light);
				// Cockpit / chest vent
				FillRect(image, 0.36f, 0.48f, 0.28f, 0.2f, mid);
				FillRect(image, 0.4f, 0.52f, 0.2f, 0.04f, glow);
				FillRect(image, 0.4f, 0.6f, 0.2f, 0.04f, glow);
				break;
			case "head":
				FillRect(image, 0.28f, 0.22f, 0.44f, 0.42f, accent);
				FillRect(image, 0.32f, 0.18f, 0.36f, 0.08f, mid);
				FillRect(image, 0.34f, 0.36f, 0.32f, 0.12f, edge);
				FillOval(image, 0.4f, 0.42f, 0.07f, 0.07f, glow);
				FillOval(image, 0.6f, 0.42f, 0.07f, 0.07f, glow);
				FillRect(image, 0.42f, 0.56f, 0.16f, 0.06f, light);
				FillRect(image, 0.7f, 0.26f, 0.06f, 0.18f, shade); // antenna stub
				break;
			case "core":
				FillOval(image, 0.5f, 0.48f, 0.24f, 0.3f, accent);
				FillOval(image, 0.5f, 0.48f, 0.16f, 0.2f, mid);
				FillOval(image, 0.5f, 0.42f, 0.1f, 0.1f, glow);
				FillOval(image, 0.5f, 0.42f, 0.04f, 0.04f, Colors.White);
				FillRect(image, 0.26f, 0.7f, 0.48f, 0.1f, shade);
				FillRect(image, 0.32f, 0.74f, 0.36f, 0.03f, light);
				break;
			case "cannon":
				FillRect(image, 0.14f, 0.34f, 0.3f, 0.32f, accent);
				FillRect(image, 0.18f, 0.38f, 0.22f, 0.06f, light);
				FillRect(image, 0.4f, 0.42f, 0.4f, 0.16f, mid);
				FillRect(image, 0.42f, 0.46f, 0.36f, 0.04f, shade);
				FillRect(image, 0.78f, 0.4f, 0.1f, 0.2f, edge);
				FillOval(image, 0.88f, 0.5f, 0.07f, 0.07f, glow);
				break;
			case "rifle":
				FillRect(image, 0.18f, 0.4f, 0.22f, 0.24f, accent);
				FillRect(image, 0.38f, 0.46f, 0.42f, 0.1f, mid);
				FillRect(image, 0.42f, 0.42f, 0.12f, 0.06f, light); // optic
				FillRect(image, 0.26f, 0.6f, 0.1f, 0.18f, shade);
				FillRect(image, 0.78f, 0.44f, 0.08f, 0.14f, edge);
				break;
			case "energy":
				FillRect(image, 0.2f, 0.28f, 0.22f, 0.44f, accent);
				FillRect(image, 0.24f, 0.34f, 0.14f, 0.08f, shade);
				FillRect(image, 0.4f, 0.38f, 0.36f, 0.12f, light);
				FillOval(image, 0.78f, 0.44f, 0.12f, 0.12f, glow);
				FillOval(image, 0.78f, 0.44f, 0.05f, 0.05f, Colors.White);
				break;
			case "cleaver":
				FillRect(image, 0.16f, 0.36f, 0.22f, 0.28f, accent);
				FillRect(image, 0.36f, 0.3f, 0.12f, 0.42f, mid);
				FillRect(image, 0.46f, 0.22f, 0.1f, 0.56f, light);
				FillRect(image, 0.52f, 0.28f, 0.06f, 0.44f, glow);
				break;
			case "held_shield":
				FillRect(image, 0.28f, 0.14f, 0.44f, 0.72f, accent);
				FillRect(image, 0.34f, 0.2f, 0.32f, 0.6f, mid);
				FillRect(image, 0.38f, 0.26f, 0.24f, 0.08f, light);
				FillRect(image, 0.4f, 0.62f, 0.2f, 0.06f, glow);
				break;
			case "missile":
				FillRect(image, 0.18f, 0.2f, 0.28f, 0.58f, accent);
				FillRect(image, 0.54f, 0.2f, 0.28f, 0.58f, accent);
				FillRect(image, 0.22f, 0.28f, 0.2f, 0.06f, shade);
				FillRect(image, 0.58f, 0.28f, 0.2f, 0.06f, shade);
				FillRect(image, 0.22f, 0.44f, 0.2f, 0.06f, shade);
				FillRect(image, 0.58f, 0.44f, 0.2f, 0.06f, shade);
				FillOval(image, 0.32f, 0.18f, 0.1f, 0.08f, light);
				FillOval(image, 0.68f, 0.18f, 0.1f, 0.08f, light);
				FillRect(image, 0.22f, 0.72f, 0.2f, 0.06f, edge);
				FillRect(image, 0.58f, 0.72f, 0.2f, 0.06f, edge);
				break;
			case "backpack":
				FillRect(image, 0.26f, 0.18f, 0.48f, 0.62f, accent);
				FillRect(image, 0.32f, 0.24f, 0.36f, 0.16f, shade);
				FillOval(image, 0.5f, 0.3f, 0.1f, 0.1f, glow);
				FillRect(image, 0.34f, 0.5f, 0.32f, 0.2f, mid);
				FillRect(image, 0.38f, 0.56f, 0.24f, 0.04f, light);
				break;
			case "shield":
				FillRect(image, 0.24f, 0.16f, 0.52f, 0.68f, accent);
				FillRect(image, 0.3f, 0.22f, 0.4f, 0.56f, mid);
				FillRect(image, 0.36f, 0.3f, 0.28f, 0.08f, light);
				FillOval(image, 0.5f, 0.5f, 0.1f, 0.1f, glow);
				FillRect(image, 0.34f, 0.64f, 0.32f, 0.08f, shade);
				break;
			case "shroud":
				FillRect(image, 0.26f, 0.2f, 0.48f, 0.52f, accent);
				FillRect(image, 0.32f, 0.26f, 0.36f, 0.14f, shade);
				FillOval(image, 0.5f, 0.22f, 0.14f, 0.1f, mid);
				FillRect(image, 0.34f, 0.5f, 0.32f, 0.14f, light);
				break;
			case "heatsink":
				FillRect(image, 0.24f, 0.2f, 0.52f, 0.6f, accent);
				for (var i = 0; i < 5; i++)
				{
					FillRect(image, 0.3f, 0.26f + i * 0.1f, 0.4f, 0.05f, shade);
					FillRect(image, 0.32f, 0.27f + i * 0.1f, 0.36f, 0.015f, light);
				}
				break;
			default:
				FillRect(image, 0.25f, 0.25f, 0.5f, 0.5f, accent);
				FillRect(image, 0.32f, 0.32f, 0.36f, 0.08f, light);
				break;
		}

		DrawFrame(image, new Color(0.28f, 0.32f, 0.38f));
		return image;
	}

	/// <summary>Plate backdrop only (no frame) for compositing a rendered 3D model on top.</summary>
	public static Image CreateBackdrop(int size)
	{
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		FillBackdrop(image);
		return image;
	}

	/// <summary>Standard catalogue plate frame + corner ticks.</summary>
	public static void DrawPlateFrame(Image image) => DrawFrame(image, new Color(0.28f, 0.32f, 0.38f));

	public static Texture2D GetEmpty(int size = 128)
	{
		var key = $"empty:{size}";
		if (Cache.TryGetValue(key, out var cached))
			return cached;

		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(new Color(0.07f, 0.08f, 0.1f, 0.9f));
		var dash = new Color(0.35f, 0.38f, 0.42f, 0.8f);
		DrawFrame(image, dash);
		FillRect(image, 0.3f, 0.46f, 0.4f, 0.08f, dash);
		FillRect(image, 0.46f, 0.3f, 0.08f, 0.4f, dash);
		var texture = ImageTexture.CreateFromImage(image);
		Cache[key] = texture;
		return texture;
	}

	private static void FillBackdrop(Image image)
	{
		var size = image.GetWidth();
		for (var py = 0; py < size; py++)
		{
			var t = py / (float)(size - 1);
			var c = new Color(
				Mathf.Lerp(0.1f, 0.06f, t),
				Mathf.Lerp(0.12f, 0.08f, t),
				Mathf.Lerp(0.15f, 0.1f, t),
				0.95f);
			for (var px = 0; px < size; px++)
				image.SetPixel(px, py, c);
		}
		// Soft top highlight strip
		FillRect(image, 0.08f, 0.06f, 0.84f, 0.03f, new Color(0.16f, 0.19f, 0.22f, 0.9f));
	}

	private static void FillRect(Image image, float x, float y, float w, float h, Color color)
	{
		var size = image.GetWidth();
		var x0 = Mathf.Clamp(Mathf.RoundToInt(x * size), 0, size - 1);
		var y0 = Mathf.Clamp(Mathf.RoundToInt(y * size), 0, size - 1);
		var x1 = Mathf.Clamp(Mathf.RoundToInt((x + w) * size), 0, size);
		var y1 = Mathf.Clamp(Mathf.RoundToInt((y + h) * size), 0, size);
		for (var py = y0; py < y1; py++)
		for (var px = x0; px < x1; px++)
			image.SetPixel(px, py, color);
	}

	private static void FillOval(Image image, float cx, float cy, float rx, float ry, Color color)
	{
		var size = image.GetWidth();
		var x0 = Mathf.Max(0, Mathf.FloorToInt((cx - rx) * size));
		var y0 = Mathf.Max(0, Mathf.FloorToInt((cy - ry) * size));
		var x1 = Mathf.Min(size, Mathf.CeilToInt((cx + rx) * size));
		var y1 = Mathf.Min(size, Mathf.CeilToInt((cy + ry) * size));
		for (var py = y0; py < y1; py++)
		for (var px = x0; px < x1; px++)
		{
			var nx = (px / (float)size - cx) / rx;
			var ny = (py / (float)size - cy) / ry;
			if (nx * nx + ny * ny <= 1f)
				image.SetPixel(px, py, color);
		}
	}

	private static void DrawFrame(Image image, Color color)
	{
		var size = image.GetWidth();
		var corner = color.Lightened(0.25f);
		for (var i = 0; i < size; i++)
		{
			image.SetPixel(i, 0, color);
			image.SetPixel(i, size - 1, color);
			image.SetPixel(0, i, color);
			image.SetPixel(size - 1, i, color);
		}
		// Corner ticks
		var tick = Mathf.Max(3, size / 16);
		for (var i = 0; i < tick; i++)
		{
			image.SetPixel(i, 1, corner);
			image.SetPixel(1, i, corner);
			image.SetPixel(size - 1 - i, 1, corner);
			image.SetPixel(size - 2, i, corner);
			image.SetPixel(i, size - 2, corner);
			image.SetPixel(1, size - 1 - i, corner);
			image.SetPixel(size - 1 - i, size - 2, corner);
			image.SetPixel(size - 2, size - 1 - i, corner);
		}
	}
}
