namespace Mechanize;

/// <summary>Presentation for last-known contact markers. Per-part.</summary>
public enum ScanBlipStyle
{
	/// <summary>Do not change assembled blip style (enhancer default).</summary>
	Inherit = 0,
	/// <summary>Floating pip at torso height.</summary>
	WorldPip = 1,
	/// <summary>Ground ring under the contact (missile-lock family).</summary>
	GroundRing = 2
}
