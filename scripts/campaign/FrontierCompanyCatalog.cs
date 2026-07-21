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
	public required string[] PitchBodies { get; init; }
	public required string[] QualifiedBodies { get; init; }
	public required string[] FailedBodies { get; init; }
	public required string[] WithdrawnBodies { get; init; }
	public required string[] DepartureBodies { get; init; }

	public ManufacturerTrialDef TrialTemplate => ConventionCatalog.Get(TrialTemplateId);
	public string VoiceProfile => TextVoiceService.ProfileForManufacturer(TrialTemplateId);

	public IEnumerable<string> PitchLines() => Expand(PitchBodies);
	public IEnumerable<string> QualifiedLines() => Expand(QualifiedBodies);
	public IEnumerable<string> FailedLines() => Expand(FailedBodies);
	public IEnumerable<string> WithdrawnLines() => Expand(WithdrawnBodies);
	public IEnumerable<string> DepartureLines() => Expand(DepartureBodies);

	private IEnumerable<string> Expand(IEnumerable<string> bodies)
	{
		var eval = MissionCatalog.Get(TrialTemplate.TrialMission).Title;
		foreach (var body in bodies)
			yield return body.Replace("{eval}", eval).Replace("{company}", DisplayName).Replace("{short}", ShortName);
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
		Color Color,
		string[] PitchLines,
		string[] Qualified,
		string[] Failed,
		string[] Withdrawn,
		string[] Departure);

	private static readonly Archetype[] Pool =
	[
		new("helios_homestead", "Helios Homestead Cooperative", "Helios", "Anika Vale", "Settlement Director",
			"build permanent homes for families priced out of the core systems.",
			"We offer resident shares, local elections, and land grants to the crews who make the first winter possible.",
			"The cooperative is sincere, but badly undercapitalized and one failed harvest from collapse.",
			"a self-sustaining civilian township", new Color(0.92f, 0.68f, 0.3f),
			[
				"Helios Homestead needs pilots who can hold ground long enough for families to unpack — not glory hires.",
				"We are not selling frames. We are trying to keep roofs standing through the first winter.",
				"Helios builds townships: heat, water, school roofs that do not fold in the first freeze.",
				"Your evaluation is {eval}. Finish it clean and we talk contracts. Fail it and I keep looking."
			],
			[
				"You held the site. That is the whole job out where we work.",
				"I can put your name in front of the housing board without lying.",
				"Contract is ready when you are. Sign it, and Helios puts you on the first push."
			],
			[
				"That run did not secure what we need.",
				"Homesteads do not survive on almost. Come back when you can finish the evaluation.",
				"You still have a slot. Use it if you mean to work with us."
			],
			[
				"I have spent the last evaluation budget I can justify.",
				"Helios is withdrawing the offer. Find another booth."
			],
			[
				"You are on Helios rolls now. The township comes first.",
				"Get {settlement} standing. After that, we hold the surrounding claims so people can stay.",
				"If you treat settlers like scenery, you will not last long with me."
			]),

		new("meridian_extractives", "Meridian Extractives Group", "Meridian", "Cass Orlov", "Acquisitions Executive",
			"secure mineral rights before competitors can register them.",
			"Meridian has the rigs, refineries, and buyers to turn dead rock into a functioning economy.",
			"Executive bonuses reward output regardless of labor conditions or ecological damage.",
			"a fortified mining settlement", new Color(0.85f, 0.32f, 0.22f),
			[
				"Meridian hires pilots who can take a claim and keep competitors off it. Sentiment is optional.",
				"I do acquisitions. That means arrival order and retention, not speeches.",
				"Meridian already has the rigs and buyers. What we lack is someone who can arrive first and stay standing.",
				"Evaluation: {eval}. Three attempts. Bring me a completed objective, not a story."
			],
			[
				"Telemetry checks out. You can follow a brief.",
				"That is enough for Meridian to put you on payroll.",
				"Sign and you leave with a contract. Miss the window and I fill the seat with someone else."
			],
			[
				"You did not secure the objective.",
				"Meridian does not pay for effort. Come back when you can finish the evaluation.",
				"You have attempts left. Do not waste them explaining yourself."
			],
			[
				"Your evaluation budget is closed.",
				"Meridian withdraws. Next."
			],
			[
				"You work for Meridian now. Mineral rights first. Everything else is secondary.",
				"Establish {settlement}. Then expand the claim lattice before another crew registers it.",
				"If you lose ground we already paid for, expect a short conversation."
			]),

		new("farfield_foundation", "Farfield Refuge Foundation", "Farfield", "Dr. Nia Okafor", "Resettlement Chair",
			"create a protected refuge for displaced frontier populations.",
			"Our charter is humanitarian: secure water, shelter, medicine, and defensible ground.",
			"Donor governments expect political loyalty from anyone the foundation saves.",
			"a refuge city with clinics and protected housing", new Color(0.35f, 0.78f, 0.88f),
			[
				"Farfield is hiring a pilot who can keep people alive — not someone looking for a private war.",
				"Our charter is clinics, water, and shelter for displaced crews. The work is slow and often contested.",
				"Farfield builds clinics, water, and shelter for displaced crews. The work is slow, public, and frequently contested.",
				"Your evaluation is {eval}. Complete it without turning the site into a graveyard if you can help it."
			],
			[
				"You finished the evaluation without abandoning the objective. That matters here.",
				"I will recommend your contract to the foundation board.",
				"Sign when ready. We move people as soon as the paperwork clears."
			],
			[
				"The objective failed. Refugees cannot wait on unfinished work.",
				"I need competence under pressure, not excuses.",
				"You still have evaluation time. Use it carefully."
			],
			[
				"I cannot authorize further evaluation spending.",
				"Farfield withdraws its offer. I am sorry it ended this way."
			],
			[
				"You are contracted to Farfield. The people under our charter come first.",
				"Start with {settlement}. Then secure the approaches so the next convoy does not die on the road.",
				"If you make this about yourself, we will terminate the contract."
			]),

		new("kepler_agronomics", "Kepler Agronomics Combine", "Kepler", "Tomas Rhee", "Frontier Cultivation Lead",
			"prove that hostile worlds can feed themselves without core-system imports.",
			"We build soil, water, and seed infrastructure before we build monuments.",
			"Kepler intends to patent every successful frontier crop and control its seed supply.",
			"an agricultural settlement and seed vault", new Color(0.45f, 0.82f, 0.38f),
			[
				"Kepler needs people who can protect soil works, water lines, and seed vaults.",
				"Pretty fighting is useless if the crop fails. Hungry settlements do not grade style.",
				"Kepler is proving hostile worlds can feed themselves. That means dirty sites and long watches.",
				"Evaluation is {eval}. Keep the asset intact. Hungry settlements do not care how stylish the wreckage looked."
			],
			[
				"You finished the job without losing the objective. Good.",
				"Kepler can use that kind of patience.",
				"Contract is ready. Sign it and we put you on the next cultivation site."
			],
			[
				"The evaluation failed. Crops do not wait for redo season forever.",
				"Protect the work. Finish the brief. Come back when you can do both.",
				"You still have an attempt left."
			],
			[
				"I am closing Kepler's evaluation offer.",
				"We will hire someone who can keep a site alive."
			],
			[
				"You are under Kepler contract. Food infrastructure is the mission.",
				"Build {settlement}. Then hold the water and soil claims around it.",
				"If the vault burns because you chased a fight, do not bother calling me."
			]),

		new("northstar_transit", "Northstar Transit Union", "Northstar", "Mara Dax", "Route Steward",
			"open a worker-owned freight corridor through the system.",
			"Safe routes make every other settlement possible. The crews who fly them should own them.",
			"The union is honorable, but its militant wing settles labor disputes with blockades and guns.",
			"a freight town, repair yard, and orbital port", new Color(0.38f, 0.62f, 0.95f),
			[
				"Northstar hires pilots who can keep freight moving. If you only like shooting, talk to Crownward.",
				"No corridor, no towns. That is the whole argument.",
				"Northstar opens worker-owned corridors. No corridor, no towns. It is that simple.",
				"Your eval is {eval}. Bring the work home. Leave the cargo in better shape than you found it."
			],
			[
				"You delivered. That is the only metric that matters on my desk.",
				"Union can put you on the route board.",
				"Sign the contract. We have haulers waiting on clearance."
			],
			[
				"The route failed. Dead freight does not feed anyone.",
				"Stay with the job. Finish the evaluation. Then we talk.",
				"You have attempts remaining. Do not burn them showing off."
			],
			[
				"Evaluation budget is gone.",
				"Northstar withdraws. Find another hall if you still want work."
			],
			[
				"You fly for Northstar now. Keep the corridor open.",
				"First priority: {settlement}. Then lock the lanes that feed it.",
				"Block a route for ego and the union will pull your papers."
			]),

		new("pale_blue_survey", "Pale Blue Survey Concern", "Pale Blue", "Elias Thorne", "Chief Cartographer",
			"catalog the system's anomalies and preserve discoveries before extraction destroys them.",
			"We pay for verified science, intact sites, and maps that keep future crews alive.",
			"The board quietly auctions sensitive discoveries to the highest strategic bidder.",
			"a research settlement and deep-range observatory", new Color(0.55f, 0.72f, 0.98f),
			[
				"Pale Blue needs a pilot who can retrieve material without smashing the site into scrap.",
				"Precision over noise. Bad maps kill people later.",
				"Pale Blue maps anomalies before extractors erase them. Bad maps kill people later.",
				"Evaluation: {eval}. Follow the brief. Do not invent your own science."
			],
			[
				"The recording is clean. Objective complete.",
				"That is sufficient for a Pale Blue contract.",
				"Sign when ready. I prefer not to keep survey windows open longer than necessary."
			],
			[
				"You failed the evaluation. Incomplete data is worse than no data.",
				"Return when you can finish the assignment as written.",
				"Attempts remain. Use one carefully."
			],
			[
				"I am withdrawing Pale Blue's offer.",
				"We cannot spend more evaluation time on unfinished work."
			],
			[
				"You are contracted to Pale Blue. Catalog first. Argue later.",
				"Establish {settlement}. Then secure the survey claims around the anomalies.",
				"If you destroy a site for convenience, your reports will not be trusted again."
			]),

		new("redwood_mutual", "Redwood Frontier Mutual", "Redwood", "June Serrano", "Claims Underwriter",
			"make frontier settlement insurable, survivable, and profitable.",
			"We pool risk across miners, haulers, farmers, and pilots so one disaster does not erase a town.",
			"Redwood will abandon any district whose actuarial value drops below its rescue cost.",
			"a resilient company town with emergency reserves", new Color(0.75f, 0.42f, 0.32f),
			[
				"Redwood hires pilots who reduce loss — people who finish the job without turning every site into a write-off.",
				"I underwrite risk. Broken kit is a claim I have to explain upstairs.",
				"Redwood pools risk so one bad winter does not erase a town. We need people who understand that.",
				"Your evaluation is {eval}. Complete it with the asset intact. Broken kit is a claim I have to explain."
			],
			[
				"Acceptable risk profile. Objective met.",
				"I can underwrite your contract.",
				"Sign and Redwood puts you on the frontier ledger."
			],
			[
				"That evaluation is a loss event.",
				"Come back when you can finish without creating new claims paperwork.",
				"You still have an attempt on the books."
			],
			[
				"Evaluation coverage is exhausted.",
				"Redwood withdraws. I will not keep paying for incomplete work."
			],
			[
				"You are on Redwood contract. Keep settlements survivable.",
				"Build {settlement}. Then hold the surrounding claims that keep the reserve solvent.",
				"If a district becomes uninsurable because of you, the contract ends."
			]),

		new("crown_security", "Crownward Security Charter", "Crownward", "Major Idris Kane", "Charter Marshal",
			"establish a lawful safe zone against raiders and predatory corps.",
			"Trade and settlement require enforceable rules, defended routes, and consequences.",
			"Crownward defines dissent as instability and intends to govern indefinitely.",
			"a walled charter city and security garrison", new Color(0.62f, 0.56f, 0.82f),
			[
				"Crownward is hiring for enforcement work. Hold ground. Deny raiders. Follow lawful orders.",
				"Safe zones keep trade alive. Soft hands do not last on this roster.",
				"Crownward builds safe zones so trade does not die in ambushes. Soft hands do not last here.",
				"Evaluation is {eval}. Three attempts. Complete the objective or leave the booth."
			],
			[
				"Objective secured. That is what I needed to see.",
				"Crownward will issue your contract.",
				"Sign it. You will report for deployment immediately after."
			],
			[
				"You failed the evaluation.",
				"Crownward does not retain people who cannot finish an assigned action.",
				"You have remaining attempts. Use them."
			],
			[
				"Your evaluation clearance is revoked.",
				"Crownward withdraws the offer. Move along."
			],
			[
				"You are under Crownward charter. Maintain order.",
				"Establish {settlement}. Then lock the approaches against raiders and rival crews.",
				"Disobey a lawful order and your contract becomes a discharge."
			]),

		new("morrow_salvage", "Morrow Salvage & Reclamation", "Morrow", "Beck Morrow", "Owner-Operator",
			"recover abandoned infrastructure and give dead machinery a second life.",
			"We waste nothing. Every wreck becomes shelter, power, tools, or a way home.",
			"Morrow's crews do not always wait for ownership disputes to be settled.",
			"a reclamation yard grown into a working settlement", new Color(0.78f, 0.58f, 0.35f),
			[
				"I own Morrow Salvage. I hire pilots who can pull value out of dead sites without turning everything into slag.",
				"Fancy kit talk does not impress me. Finished reclamation does.",
				"We reclaim yards, reactors, hulls — whatever still has use. Fancy kit talk does not impress me.",
				"Eval is {eval}. Finish it. Bring the work back in one piece if you can."
			],
			[
				"You got it done. Good enough for me.",
				"I will put you on the crew list.",
				"Sign the paper and we start hauling."
			],
			[
				"That was a bust.",
				"Come back when you can finish a job without losing the prize.",
				"You still have a try left. Take it or do not."
			],
			[
				"I am done burning eval slots on this.",
				"Offer is pulled. Plenty of other pilots walking the floor."
			],
			[
				"You work for Morrow now. Salvage first.",
				"Get {settlement} running. Then strip and secure the sites around it before someone else does.",
				"Waste good scrap and we are going to have words."
			]),

		new("aurora_civic", "Aurora Civic Ventures", "Aurora", "Priya Sen", "Civic Development Partner",
			"build a model frontier city that can attract independent commerce.",
			"Transparent utilities, public transit, and mixed ownership can outgrow company-town dependency.",
			"Aurora's investors plan to privatize the successful infrastructure after settlement.",
			"a planned open-trade city", new Color(0.88f, 0.48f, 0.72f),
			[
				"Aurora is hiring a pilot who can secure ground for a real city — transit, utilities, mixed commerce.",
				"We are not building a company barracks. Investors notice unfinished sites.",
				"Aurora sells permanence. Investors notice unfinished sites.",
				"Your evaluation is {eval}. Complete it professionally. We are judged by how clean the record looks."
			],
			[
				"Objective complete. That reads well.",
				"Aurora can move forward with your contract.",
				"Sign when ready. The next development window will not wait."
			],
			[
				"The evaluation failed. That is a problem for the schedule.",
				"Return when you can finish the assignment as briefed.",
				"You still have attempts. Please use one soon."
			],
			[
				"Aurora is withdrawing the evaluation offer.",
				"I have to reallocate the development budget."
			],
			[
				"You are contracted to Aurora. Build the city.",
				"Establish {settlement}. Then secure the commercial approaches around it.",
				"If you make this look like a raid instead of development, the investors will pull funding."
			]),

		new("vesper_energy", "Vesper Independent Energy", "Vesper", "Sol Varga", "Grid Commissioner",
			"construct an energy grid that frees the frontier from imported reactor fuel.",
			"Power is sovereignty. A settlement that owns its grid can negotiate with anyone.",
			"Vesper intends to become the sole grid operator once competitors depend on it.",
			"a reactor settlement and system power exchange", new Color(0.72f, 0.45f, 0.95f),
			[
				"Vesper needs pilots who can protect reactor works and keep a grid site from going dark.",
				"Without a grid, every other settlement is a guest. That is the job.",
				"Vesper builds power independence. Without a grid, every other settlement is a guest.",
				"Evaluation: {eval}. Finish it. Do not leave a site in a state that trips half the sector."
			],
			[
				"Objective met. Load profile acceptable.",
				"Vesper will issue your contract.",
				"Sign it. We need bodies on the next grid node immediately."
			],
			[
				"Evaluation failed. That site is still cold.",
				"Come back when you can complete the work without cascading faults.",
				"Attempts remain. Use them."
			],
			[
				"I am cutting the evaluation authorization.",
				"Vesper withdraws. Find another employer."
			],
			[
				"You are under Vesper contract. Keep the grid alive.",
				"Build {settlement}. Then secure the nodes that feed it.",
				"If you black out a district through negligence, your clearance ends."
			]),

		new("open_hand", "Open Hand Development Trust", "Open Hand", "Leonie Ward", "Trust Advocate",
			"hold land in trust so settlers cannot be dispossessed by distant creditors.",
			"The land stays with the community. Industry leases access; it does not own the people.",
			"The trust's unelected founders retain emergency powers with no expiration date.",
			"a land-trust settlement governed by resident councils", new Color(0.42f, 0.86f, 0.68f),
			[
				"Open Hand is hiring a pilot to help secure land that stays with the people who live on it.",
				"Not creditors three systems away. Industry can lease. It does not get to own the residents.",
				"Open Hand holds title in trust. Industry can lease. It does not get to own the residents.",
				"Your evaluation is {eval}. Complete it carefully. Sloppy work becomes someone else's eviction notice."
			],
			[
				"You finished the evaluation. The board will accept that.",
				"I can put a contract in front of you.",
				"Sign when you are ready to leave. The trust moves slowly, but it moves."
			],
			[
				"The evaluation was not completed.",
				"Land disputes punish hesitation and carelessness the same way.",
				"You still have an evaluation slot. Use it if you intend to work with us."
			],
			[
				"The trust cannot fund further evaluation attempts.",
				"Open Hand withdraws its offer."
			],
			[
				"You are under Open Hand contract. Protect the trust land.",
				"Establish {settlement}. Then secure the surrounding claims before creditors file over them.",
				"If you treat settlers as obstacles, this arrangement ends."
			])
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
			TrialTemplateId = templates[i],
			PitchBodies = a.PitchLines,
			QualifiedBodies = a.Qualified,
			FailedBodies = a.Failed,
			WithdrawnBodies = a.Withdrawn,
			DepartureBodies = PatchSettlement(a.Departure, a.Settlement)
		}).ToList();
	}

	private static string[] PatchSettlement(string[] departure, string settlement) =>
		departure.Select(line => line.Replace("{settlement}", settlement)).ToArray();
}
