using Godot;

namespace Mechanize;

public static class SaveService
{
	public const string SavePath = "user://mechanize_save.json";

	public static bool HasSave() => Godot.FileAccess.FileExists(SavePath);

	public static void Save(PlayerProfile profile)
	{
		var dict = new Godot.Collections.Dictionary();
		foreach (var (key, value) in profile.ToDict())
			dict[key] = value;

		var json = Json.Stringify(dict, "\t");
		using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(json);
		GD.Print($"Profile saved ({profile.OwnedCopyCount} part copies, {profile.Scrap} scrap).");
	}

	public static PlayerProfile LoadOrNew()
	{
		if (!HasSave())
			return PlayerProfile.CreateNew();

		using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return PlayerProfile.CreateNew();

		var text = file.GetAsText();
		var parsed = Json.ParseString(text);
		if (parsed.VariantType != Variant.Type.Dictionary)
			return PlayerProfile.CreateNew();

		return PlayerProfile.FromDict(parsed.AsGodotDictionary());
	}
}
