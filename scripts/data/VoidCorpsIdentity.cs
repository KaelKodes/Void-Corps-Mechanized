using Godot;

namespace Mechanize;

/// <summary>
/// Shared Void Corps universe framing for Mechanize surface ops.
/// Mechanize = product title. In-world chassis classes are MAP / MAD.
/// </summary>
public static class VoidCorpsIdentity
{
	public const string ProductTitle = "Void Corps: Mechanize";
	public const string ShortTitle = "MECHANIZE";

	/// <summary>Manned combat chassis — Mechanized Armor Pilot.</summary>
	public const string MapAcronym = "MAP";
	public const string MapFull = "Mechanized Armor Pilot";
	public const string MapPlural = "MAPs";

	/// <summary>Unmanned combat chassis — Mechanized Armor Drone.</summary>
	public const string MadAcronym = "MAD";
	public const string MadFull = "Mechanized Armor Drone";
	public const string MadPlural = "MADs";

	public const string Tagline =
		"When the claim isn't settled in orbit, MAPs settle it on the surface.";
	public const string OpsBrief =
		"Corporate surface warfare for unclaimed territory. Assemble a licensed MAP, deny enemy assets, hold the claim.";

	public static readonly ClaimSite[] ClaimSites =
	[
		new ClaimSite(
			"VC-CLAIM 7-ORBITAL",
			"Dust Claim 7-Orbital",
			"Unregistered rock in a disputed belt. First corp to silence opposing MAP detachments files the claim."),
		new ClaimSite(
			"VC-CLAIM GRID-ASH",
			"Grid Ash Outpost",
			"Abandoned relay pad. Salvage rights transfer with site control — expect rival MAP pilots."),
		new ClaimSite(
			"VC-CLAIM BLACK-WHARF",
			"Black Wharf Shelf",
			"Cold industrial shelf under a dead docking spine. Hold the pad; deny the asset if you can't."),
	];

	public static ClaimSite PickClaimSite(int seed = -1)
	{
		if (seed < 0)
			seed = (int)Time.GetTicksMsec();
		return ClaimSites[Mathf.Abs(seed) % ClaimSites.Length];
	}

	public readonly struct ClaimSite(string code, string displayName, string brief)
	{
		public string Code { get; } = code;
		public string DisplayName { get; } = displayName;
		public string Brief { get; } = brief;
	}
}
