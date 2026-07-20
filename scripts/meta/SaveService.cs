using Godot;

namespace Mechanize;

public static class SaveService
{
	public const string SavePath = "user://mechanize_save.json";
	public const string RoguelikeSavePath = "user://mechanize_roguelike_profile.json";

	public static bool HasSave() => Godot.FileAccess.FileExists(SavePath);

	public static void Save(PlayerProfile profile)
		=> Save(profile, SavePath);

	public static void Save(PlayerProfile profile, string path)
	{
		var dict = new Godot.Collections.Dictionary();
		foreach (var (key, value) in profile.ToDict())
			dict[key] = value;

		var json = Json.Stringify(dict, "\t");
		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(json);
		GD.Print($"Profile saved (schema {PlayerProfile.SchemaVersion}, {profile.OwnedCopyCount} part copies, {profile.Scrap} scrap).");
	}

	public static PlayerProfile LoadOrNew()
		=> LoadOrNew(SavePath);

	public static PlayerProfile LoadOrNew(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
			return PlayerProfile.CreateNew();

		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
			return PlayerProfile.CreateNew();

		var text = file.GetAsText();
		var parsed = Json.ParseString(text);
		if (parsed.VariantType != Variant.Type.Dictionary)
			return PlayerProfile.CreateNew();

		return PlayerProfile.FromDict(parsed.AsGodotDictionary());
	}
}
