namespace Mechanize;

/// <summary>
/// Shared arena footprint class. Base shell is 80×80 (half-extent 40).
/// Medium = +20%, Large = +40%.
/// </summary>
public enum ArenaSize
{
	Small = 0,
	Medium = 1,
	Large = 2
}

public static class ArenaSizeUtil
{
	public const float BaseExtent = 80f;
	public const float BaseHalfExtent = 40f;

	public static float Scale(ArenaSize size) => size switch
	{
		ArenaSize.Medium => 1.2f,
		ArenaSize.Large => 1.4f,
		_ => 1f
	};

	public static float Extent(ArenaSize size) => BaseExtent * Scale(size);
	public static float HalfExtent(ArenaSize size) => BaseHalfExtent * Scale(size);
	/// <summary>Drop-pad clamp inset from the wall.</summary>
	public static float PadLimit(ArenaSize size) => HalfExtent(size) - 4.5f;

	public static string Label(ArenaSize size) => size switch
	{
		ArenaSize.Medium => "MEDIUM",
		ArenaSize.Large => "LARGE",
		_ => "SMALL"
	};
}
