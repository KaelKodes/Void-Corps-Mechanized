using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Shared Void Corps universe framing for Mechanize surface ops.
/// Mechanize = product title. In-world chassis classes are MAP / MAD.
/// Manufacturers (Brimforge, OuroTech, Trinova, Lumina) are NOT Corps —
/// Corps are player/NPC organizations (guild-to-business scale).
/// </summary>
public static class VoidCorpsIdentity
{
	public const string ProductTitle = "Void Corps: Mechanize";
	public const string ShortTitle = "MECHANIZE";
	/// <summary>Player-facing build label (semver).</summary>
	public const string GameVersion = "0.2.1";

	/// <summary>Manned combat chassis — Mechanized Armor Pilot.</summary>
	public const string MapAcronym = "MAP";
	public const string MapFull = "Mechanized Armor Pilot";
	public const string MapPlural = "MAPs";

	/// <summary>Unmanned combat chassis — Mechanized Armor Drone.</summary>
	public const string MadAcronym = "MAD";
	public const string MadFull = "Mechanized Armor Drone";
	public const string MadPlural = "MADs";

	/// <summary>
	/// Size tier above a field MAP. Titans remain MAPs/MADs by operation class;
	/// they are simply superheavy frames used to break contested claims.
	/// </summary>
	public const string TitanClass = "Titan-class";
	public const string TitanMap = "Titan-class MAP";

	/// <summary>
	/// Placeholder upstart the player joins in campaign.
	/// Corps succeed → members succeed. Manufacturers license kit; they are not the player's org.
	/// </summary>
	public const string PlayerCorpCodename = "Upstart Detachment";
	public const string PlayerCorpBlurb =
		"A young surface corp. Rise through its ranks; if the corp takes ground, its pilots take the spoils.";
	public const string CampaignPremise =
		"Publicly you are an independent merc corps. Privately you are a manufacturer's shadow arm claiming ground they can quietly exploit.";
	public const string CadetPremise =
		"The MAP Cadet Program issues a military-grade training chassis for tutorial certification only.";
	public const string ConventionPremise =
		"After graduation, the Big Four court new pilots with mock missions, sparse part pools, and trial scoring.";

	public const string Tagline =
		"When the claim isn't settled in orbit, MAPs settle it on the surface.";
	public const string OpsBrief =
		"Surface warfare for unclaimed territory. Pilot a licensed MAP for your corp, deny rival assets, hold the claim.";

	/// <summary>
	/// Design north star (not yet systems): campaign is linear path choice;
	/// long-term fantasy is Foxhole-like territory control, with a later
	/// persistent seasonal MMO layer of player-run corps at war.
	/// </summary>
	public const string DesignPillar =
		"Territory control facilitated by mechanized battles.";

	public static readonly ClaimSite[] ClaimSites =
	[
		// --- Map gen 1.0 / Small ---
		new ClaimSite(
			"VC-CLAIM 7-ORBITAL",
			"Dust Claim 7-Orbital",
			"Unregistered rock in a disputed belt. First corp to silence opposing MAP detachments files the claim.",
			ArenaSize.Small,
			1.0f),
		new ClaimSite(
			"VC-CLAIM GRID-ASH",
			"Grid Ash Outpost",
			"Abandoned relay pad. Salvage rights transfer with site control — expect rival MAP pilots.",
			ArenaSize.Small,
			1.0f),
		new ClaimSite(
			"VC-CLAIM BLACK-WHARF",
			"Black Wharf Shelf",
			"Cold industrial shelf under a dead docking spine. Hold the pad; deny the asset if you can't.",
			ArenaSize.Small,
			1.0f),

		// --- Map gen 2.0 ---
		new ClaimSite(
			"VC-CLAIM SLAG-FOUNDRY",
			"Slag Foundry Yard",
			"Brimforge pour-floor gone dark. Lane the slag pits, deny the rival detachment, claim the furnace rights.",
			ArenaSize.Medium,
			2.0f),
		new ClaimSite(
			"VC-CLAIM SPIRE-NULL",
			"Spire-Null Plaza",
			"A dead corporate megacity core. Fight the plaza under glass towers — whoever holds the pad owns the skyline claim.",
			ArenaSize.Large,
			2.0f),

		// --- Sabotage corridor (mission-exclusive — never random/skirmish/campaign pool) ---
		new ClaimSite(
			"VC-CLAIM ECHELON-RUN",
			"Echelon Approach",
			"Long industrial approach under automated fire lattices. Reach the uplink. Plant. Call for pickup.",
			ArenaSize.Medium,
			2.3f,
			sabotageOnly: true),

		// --- Escort haul-line (mission-exclusive — long drill run) ---
		new ClaimSite(
			"VC-CLAIM DRIFT-HAUL",
			"Drift Haul-Line",
			"A deep ore drift with a single haul-line to the surface pad. Walk the rig down, guard the dig, haul it home.",
			ArenaSize.Medium,
			2.3f,
			missionOnly: true),
	];

	/// <summary>Claim sites legal for general skirmish / campaign map gen (excludes mission-exclusive corridors).</summary>
	public static IEnumerable<ClaimSite> StandardClaimSites =>
		ClaimSites.Where(c => !c.SabotageOnly && !c.MissionOnly);

	public static ClaimSite PickClaimSite(int seed = -1)
	{
		var pool = StandardClaimSites.ToArray();
		if (pool.Length == 0)
			return ClaimSites[0];
		if (seed < 0)
			seed = (int)Time.GetTicksMsec();
		return pool[Mathf.Abs(seed) % pool.Length];
	}

	public static ClaimSite? FindClaim(string code)
	{
		foreach (var site in ClaimSites)
		{
			if (site.Code == code)
				return site;
		}

		return null;
	}

	public readonly struct ClaimSite(
		string code,
		string displayName,
		string brief,
		ArenaSize size = ArenaSize.Small,
		float mapVersion = 1.0f,
		bool sabotageOnly = false,
		bool missionOnly = false)
	{
		public string Code { get; } = code;
		public string DisplayName { get; } = displayName;
		public string Brief { get; } = brief;
		public ArenaSize Size { get; } = size;
		/// <summary>1.0 = original trio; 2.0 = identity-era layouts.</summary>
		public float MapVersion { get; } = mapVersion;
		/// <summary>True for the Sabotage Run corridor — not a general claim site.</summary>
		public bool SabotageOnly { get; } = sabotageOnly;
		/// <summary>True for other mission-exclusive layouts (e.g. the Escort haul-line).</summary>
		public bool MissionOnly { get; } = missionOnly;
	}
}
