namespace Mechanize;

/// <summary>
/// How a passive/active contact scan treats cover when placing last-known blips.
/// Per-part — heads set the baseline; Systems/Backpack may override.
/// </summary>
public enum ScanPenetrationMode
{
	/// <summary>Do not change assembled scan mode (enhancer default).</summary>
	Inherit = 0,
	/// <summary>Only mark contacts with a clear ray past World/Targets cover.</summary>
	LineOfSight = 1,
	/// <summary>Through-walls radar contact (still a frozen blip, not live X-ray).</summary>
	Contact = 2
}
