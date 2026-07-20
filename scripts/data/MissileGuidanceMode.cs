namespace Mechanize;

/// <summary>
/// How a missile ability acquires its aim. Paint stays manual; sensor modes use TAB lock.
/// Vision vs contact is a per-weapon requirement, not a global rule.
/// </summary>
public enum MissileGuidanceMode
{
	/// <summary>Hold ability, paint a world point, release. No sensor lock required.</summary>
	Paint = 0,

	/// <summary>
	/// Requires a maintained sensor lock that is currently in vision (optical / direct track).
	/// </summary>
	SensorVision = 1,

	/// <summary>
	/// Requires a maintained sensor lock within acquire range; vision cone optional
	/// (scanner / GPS-style track).
	/// </summary>
	SensorContact = 2
}
