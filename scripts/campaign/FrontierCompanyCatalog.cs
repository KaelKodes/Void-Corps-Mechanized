using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

public sealed class FrontierCompanyData
{
	public required string Id { get; init; }
	public required string DisplayName { get; init; }
	public required string ShortName { get; init; }
	public required string LiaisonName { get; init; }
	public required string LiaisonTitle { get; init; }
	public required string Motive { get; init; }
	public required string PublicPitch { get; init; }
	public required string PrivateTruth { get; init; }
	public required string SettlementVision { get; init; }
	public required Color AccentColor { get; init; }
	/// <summary>Presentation/loadout template only; not a corporate relationship.</summary>
	public required string TrialTemplateId { get; init; }

	public ManufacturerTrialDef TrialTemplate => ConventionCatalog.Get(TrialTemplateId);
	public string VoiceProfile => TextVoiceService.ProfileForManufacturer(TrialTemplateId);

	public IEnumerable<string> PitchLines()
	{
		yield return $"{LiaisonName}, {LiaisonTitle}. I represent {DisplayName}. We are hiring a corp, not selling hardware.";
		yield return PublicPitch;
		yield return $"Our claim objective is simple: {Motive}";
		yield return $"Your evaluation is {MissionCatalog.Get(TrialTemplate.TrialMission).Title}. The MAP is leased from ordinary licensed stock.";
		yield return "Qualify, sign an employment charter, and your corp deploys under our frontier mandate.";
	}

	public IEnumerable<string> QualifiedLines()
	{
		yield return "The objective was completed and the company board accepted the telemetry.";
		yield return $"You demonstrated the judgment {ShortName} needs beyond supported space.";
		yield return "The employment charter is ready. Sign it when you are prepared to leave.";
	}

	public IEnumerable<string> FailedLines()
	{
		yield return "The evaluation objective was not completed.";
		yield return $"We are building {SettlementVision.ToLowerInvariant()}; improvisation cannot replace delivery.";
		yield return "You still have an evaluation slot. Use it when you can finish the assigned job.";
	}

	public IEnumerable<string> WithdrawnLines()
	{
		yield return "The evaluation budget is exhausted.";
		yield return $"{DisplayName} has withdrawn its employment offer.";
	}

	public IEnumerable<string> DepartureLines()
	{
		yield return $"Your corp now holds a frontier charter from {DisplayName}.";
		yield return $"Our stated purpose: {Motive}";
		yield return $"First establish {SettlementVision.ToLowerInvariant()}. Then secure the system around it.";
	}
}

/// <summary>
/// Employer companies are independent of the Big Four manufacturers. Four are
/// selected per persistent campaign from a larger pool.
/// </summary>
public static class FrontierCompanyCatalog
{
	private sealed record Archetype(
		string Id,
		string Name,
		string Short,
		string Liaison,
		string Title,
		string Motive,
		string Pitch,
		string Truth,
		string Settlement,
		Color Color);

	private static readonly Archetype[] Pool =
	[
		new("helios_homestead", "Helios Homestead Cooperative", "Helios", "Anika Vale", "Settlement Director",
			"build permanent homes for families priced out of the core systems.",
			"We offer resident shares, local elections, and land grants to the crews who make the first winter possible.",
			"The cooperative is sincere, but badly undercapitalized and one failed harvest from collapse.",
			"a self-sustaining civilian township", new Color(0.92f, 0.68f, 0.3f)),
		new("meridian_extractives", "Meridian Extractives Group", "Meridian", "Cass Orlov", "Acquisitions Executive",
			"secure mineral rights before competitors can register them.",
			"Meridian has the rigs, refineries, and buyers to turn dead rock into a functioning economy.",
			"Executive bonuses reward output regardless of labor conditions or ecological damage.",
			"a fortified mining settlement", new Color(0.85f, 0.32f, 0.22f)),
		new("farfield_foundation", "Farfield Refuge Foundation", "Farfield", "Dr. Nia Okafor", "Resettlement Chair",
			"create a protected refuge for displaced frontier populations.",
			"Our charter is humanitarian: secure water, shelter, medicine, and defensible ground.",
			"Donor governments expect political loyalty from anyone the foundation saves.",
			"a refuge city with clinics and protected housing", new Color(0.35f, 0.78f, 0.88f)),
		new("kepler_agronomics", "Kepler Agronomics Combine", "Kepler", "Tomas Rhee", "Frontier Cultivation Lead",
			"prove that hostile worlds can feed themselves without core-system imports.",
			"We build soil, water, and seed infrastructure before we build monuments.",
			"Kepler intends to patent every successful frontier crop and control its seed supply.",
			"an agricultural settlement and seed vault", new Color(0.45f, 0.82f, 0.38f)),
		new("northstar_transit", "Northstar Transit Union", "Northstar", "Mara Dax", "Route Steward",
			"open a worker-owned freight corridor through the system.",
			"Safe routes make every other settlement possible. The crews who fly them should own them.",
			"The union is honorable, but its militant wing settles labor disputes with blockades and guns.",
			"a freight town, repair yard, and orbital port", new Color(0.38f, 0.62f, 0.95f)),
		new("pale_blue_survey", "Pale Blue Survey Concern", "Pale Blue", "Elias Thorne", "Chief Cartographer",
			"catalog the system's anomalies and preserve discoveries before extraction destroys them.",
			"We pay for verified science, intact sites, and maps that keep future crews alive.",
			"The board quietly auctions sensitive discoveries to the highest strategic bidder.",
			"a research settlement and deep-range observatory", new Color(0.55f, 0.72f, 0.98f)),
		new("redwood_mutual", "Redwood Frontier Mutual", "Redwood", "June Serrano", "Claims Underwriter",
			"make frontier settlement insurable, survivable, and profitable.",
			"We pool risk across miners, haulers, farmers, and pilots so one disaster does not erase a town.",
			"Redwood will abandon any district whose actuarial value drops below its rescue cost.",
			"a resilient company town with emergency reserves", new Color(0.75f, 0.42f, 0.32f)),
		new("crown_security", "Crownward Security Charter", "Crownward", "Major Idris Kane", "Charter Marshal",
			"establish a lawful safe zone against raiders and predatory corps.",
			"Trade and settlement require enforceable rules, defended routes, and consequences.",
			"Crownward defines dissent as instability and intends to govern indefinitely.",
			"a walled charter city and security garrison", new Color(0.62f, 0.56f, 0.82f)),
		new("morrow_salvage", "Morrow Salvage & Reclamation", "Morrow", "Beck Morrow", "Owner-Operator",
			"recover abandoned infrastructure and give dead machinery a second life.",
			"We waste nothing. Every wreck becomes shelter, power, tools, or a way home.",
			"Morrow's crews do not always wait for ownership disputes to be settled.",
			"a reclamation yard grown into a working settlement", new Color(0.78f, 0.58f, 0.35f)),
		new("aurora_civic", "Aurora Civic Ventures", "Aurora", "Priya Sen", "Civic Development Partner",
			"build a model frontier city that can attract independent commerce.",
			"Transparent utilities, public transit, and mixed ownership can outgrow company-town dependency.",
			"Aurora's investors plan to privatize the successful infrastructure after settlement.",
			"a planned open-trade city", new Color(0.88f, 0.48f, 0.72f)),
		new("vesper_energy", "Vesper Independent Energy", "Vesper", "Sol Varga", "Grid Commissioner",
			"construct an energy grid that frees the frontier from imported reactor fuel.",
			"Power is sovereignty. A settlement that owns its grid can negotiate with anyone.",
			"Vesper intends to become the sole grid operator once competitors depend on it.",
			"a reactor settlement and system power exchange", new Color(0.72f, 0.45f, 0.95f)),
		new("open_hand", "Open Hand Development Trust", "Open Hand", "Leonie Ward", "Trust Advocate",
			"hold land in trust so settlers cannot be dispossessed by distant creditors.",
			"The land stays with the community. Industry leases access; it does not own the people.",
			"The trust's unelected founders retain emergency powers with no expiration date.",
			"a land-trust settlement governed by resident councils", new Color(0.42f, 0.86f, 0.68f))
	];

	private static readonly string[] Templates = ["brimforge", "ourotech", "trinova", "lumina"];

	public static List<FrontierCompanyData> Generate(int seed, IReadOnlyList<string>? fixedIds = null)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
		var candidates = Pool.ToList();
		for (var i = candidates.Count - 1; i > 0; i--)
		{
			var j = rng.RandiRange(0, i);
			(candidates[i], candidates[j]) = (candidates[j], candidates[i]);
		}

		var selected = fixedIds is { Count: > 0 }
			? fixedIds.Select(id => Pool.FirstOrDefault(a => a.Id == id)).Where(a => a != null).Cast<Archetype>().ToList()
			: candidates.Take(4).ToList();
		while (selected.Count < 4)
		{
			var next = candidates.First(a => selected.All(s => s.Id != a.Id));
			selected.Add(next);
		}

		var templates = Templates.ToList();
		for (var i = templates.Count - 1; i > 0; i--)
		{
			var j = rng.RandiRange(0, i);
			(templates[i], templates[j]) = (templates[j], templates[i]);
		}

		return selected.Take(4).Select((a, i) => new FrontierCompanyData
		{
			Id = a.Id,
			DisplayName = a.Name,
			ShortName = a.Short,
			LiaisonName = a.Liaison,
			LiaisonTitle = a.Title,
			Motive = a.Motive,
			PublicPitch = a.Pitch,
			PrivateTruth = a.Truth,
			SettlementVision = a.Settlement,
			AccentColor = a.Color,
			TrialTemplateId = templates[i]
		}).ToList();
	}
}
