using Godot;

namespace Mechanize;

/// <summary>3×3 pilot portrait sheets for Cat / Dog faction pick.</summary>
public static class PilotPortraits
{
	public const int GridSize = 3;
	public const int PortraitCount = GridSize * GridSize;

	private const string CatSheetPath = "res://art/ui/portraits/cat_pilot_portraits_3x3.png";
	private const string DogSheetPath = "res://art/ui/portraits/dog_pilot_portraits_3x3.png";

	private static Texture2D? _catSheet;
	private static Texture2D? _dogSheet;

	public static string DisplayName(FactionId faction) => faction switch
	{
		FactionId.Cat => "Cat",
		FactionId.Dog => "Dog",
		_ => "Unset"
	};

	public static Texture2D? GetSheet(FactionId faction)
	{
		if (faction == FactionId.Cat)
			return _catSheet ??= LoadSheet(CatSheetPath);
		if (faction == FactionId.Dog)
			return _dogSheet ??= LoadSheet(DogSheetPath);
		return null;
	}

	public static Texture2D? GetPortrait(FactionId faction, int index)
	{
		var sheet = GetSheet(faction);
		if (sheet == null)
			return null;

		index = Mathf.Clamp(index, 0, PortraitCount - 1);
		var cellW = sheet.GetWidth() / GridSize;
		var cellH = sheet.GetHeight() / GridSize;
		var col = index % GridSize;
		var row = index / GridSize;
		return new AtlasTexture
		{
			Atlas = sheet,
			Region = new Rect2(col * cellW, row * cellH, cellW, cellH)
		};
	}

	private static Texture2D? LoadSheet(string path)
	{
		var loaded = GD.Load<Texture2D>(path);
		if (loaded != null)
			return loaded;

		if (!Godot.FileAccess.FileExists(path))
			return null;

		var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
		if (image == null || image.IsEmpty())
			return null;
		return ImageTexture.CreateFromImage(image);
	}
}
