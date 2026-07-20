using System.Collections.Generic;

namespace Mechanize;

public sealed class ManufacturerTrialDef
{
	public required string ManufacturerId { get; init; }
	public required string LiaisonName { get; init; }
	public required string LiaisonShortName { get; init; }
	public required string LiaisonTitle { get; init; }
	public required string BannerSnippet { get; init; }
	public required string[] PitchLines { get; init; }
	public required string[] QualifiedReturnLines { get; init; }
	public required string[] FailedReturnLines { get; init; }
	public required string[] WithdrawnReturnLines { get; init; }
	/// <summary>Use {0} for the rival manufacturer's display name.</summary>
	public required string ForgivenessLine { get; init; }
	public required string[] DepartureLines { get; init; }
	public required MissionType TrialMission { get; init; }
	public required LoadoutData DemoLoaner { get; init; }
	public required LoadoutData SigningLoadout { get; init; }
	public required string[] SigningBonusPartIds { get; init; }
	public int SigningBonusScrap { get; init; }
	public int SigningBonusLives { get; init; }
	public required string SigningBonusBlurb { get; init; }

	public string SpeakerTag => LiaisonShortName.ToUpperInvariant();

	public string FormatLine(string body) => $"{SpeakerTag}  ·  {body}";

	public string FormatForgiveness(string rivalDisplayName) =>
		FormatLine(string.Format(ForgivenessLine, rivalDisplayName));

	public IEnumerable<string> FormatLines(IEnumerable<string> bodies)
	{
		foreach (var body in bodies)
			yield return FormatLine(body);
	}
}

/// <summary>Convention pitches, demo loaners, trial missions, and signing packages.</summary>
public static class ConventionCatalog
{
	private static Dictionary<string, ManufacturerTrialDef>? _defs;

	public static IReadOnlyDictionary<string, ManufacturerTrialDef> All
	{
		get
		{
			EnsureBuilt();
			return _defs!;
		}
	}

	public static void EnsureBuilt()
	{
		if (_defs != null)
			return;

		GameCatalog.EnsureBuilt();
		_defs = new Dictionary<string, ManufacturerTrialDef>
		{
			["brimforge"] = new ManufacturerTrialDef
			{
				ManufacturerId = "brimforge",
				LiaisonName = "Garrick Holt",
				LiaisonShortName = "Holt",
				LiaisonTitle = "Field Contracts",
				BannerSnippet = "Heavy frames, kinetic weapons, and armor that stays on when the lane gets ugly.",
				PitchLines =
				[
					"Garrick Holt, field contracts. I used to keep these frames running after people like you brought them home in pieces.",
					"Your academy loaner got pulled. That's normal. Out here you're on a demo chassis, and the bill for wrecking it is real.",
					"Trial's a hold. Keep the pad. Stop the other detachment from walking off with the site. Three attempts.",
					"Pass, and Brimforge puts a starter MAP under you with scrap and spare plate. Fail all three and I close the offer.",
					"If you're ready, take the slot. If you're not, don't burn a demo proving it."
				],
				QualifiedReturnLines =
				[
					"Pad held, opposing unit pushed off, and the demo still walks. That's the report I needed.",
					"You didn't chase kills for the cameras. You stayed on the objective until it was finished.",
					"Contract's ready. Sign it and the starter frame leaves with you."
				],
				FailedReturnLines =
				[
					"You lost the pad. Doesn't matter how hard you hit if you leave without the ground.",
					"Next run: hold the lane, protect the chassis, and finish the hold before the clock or the rival does.",
					"You've still got attempts. Use one when you're ready to do the job as written."
				],
				WithdrawnReturnLines =
				[
					"That's three demos and no secured pad.",
					"I'm closing Brimforge's offer. I won't keep writing off chassis for practice."
				],
				ForgivenessLine =
					"I saw what you did to {0}'s floor model. That bought you one more attempt with us. Don't spend it showing off.",
				DepartureLines =
				[
					"There are unsettled sites past the core lanes. Other detachments will contest them.",
					"Your corp files the claims. Brimforge keeps you supplied. Publicly, we aren't attached to the work.",
					"Take the ground you can hold. Don't make me explain a burned frame to accounting."
				],
				TrialMission = MissionType.CaptureArea,
				DemoLoaner = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_brin_bulwark",
					TorsoId = "torso_brin_anvil",
					HeadId = "head_brin_warface",
					PowerCoreId = "core_brin_forge",
					WeaponLId = "wep_brin_slug",
					WeaponRId = "wep_brin_rivet",
					ShoulderLId = "shoulder_brin_pods",
					ShoulderRId = "",
					BackpackId = "backpack_brin_plate",
					SystemsId = "systems_brin_vent"
				}),
				SigningLoadout = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_brin_biped",
					TorsoId = "torso_brin_anvil",
					HeadId = "head_brin_warface",
					PowerCoreId = "core_brin_forge",
					WeaponLId = "wep_brin_slug",
					WeaponRId = "wep_brin_rivet",
					ShoulderLId = "shoulder_brin_pods",
					ShoulderRId = "",
					BackpackId = "backpack_brin_plate",
					SystemsId = "systems_brin_vent"
				}),
				SigningBonusPartIds = ["backpack_brin_mend", "systems_brin_armor"],
				SigningBonusScrap = 40,
				SigningBonusLives = 1,
				SigningBonusBlurb = "Heavy starter MAP · +40 scrap · +1 life · Forge Patch + Reactive Plate unlocked"
			},
			["ourotech"] = new ManufacturerTrialDef
			{
				ManufacturerId = "ourotech",
				LiaisonName = "Selene Vey",
				LiaisonShortName = "Vey",
				LiaisonTitle = "Licensing Desk",
				BannerSnippet = "Precision frames, seekers, and fire control built for clean engagements.",
				PitchLines =
				[
					"Selene Vey, licensing. I review the trial recordings myself, so please don't waste either of our time.",
					"OuroTech isn't hiring a mascot. We need someone who can identify a priority target and finish it without emptying the magazine into the scenery.",
					"Your evaluation is search and destroy. Locate the marked asset, eliminate it, extract. Three attempts on the demo budget.",
					"If you qualify, you leave with a precision starter MAP and the spare systems to keep it honest.",
					"When you're ready, take the trial. I'll be watching the telemetry, not the theatrics."
				],
				QualifiedReturnLines =
				[
					"Target down, ammunition within tolerance, and you didn't invent a second objective halfway through.",
					"That kind of judgment is harder to hire than good aiming. I can work with it.",
					"Licensing packet is ready. Sign, and the frame is yours."
				],
				FailedReturnLines =
				[
					"I watched the recording. Plenty of motion. The priority asset is still intact.",
					"Next attempt: ignore the distractions, confirm the target, and finish the assignment as briefed.",
					"You still have evaluation slots. Come back when you intend to use one properly."
				],
				WithdrawnReturnLines =
				[
					"Three attempts. No completed objective.",
					"OuroTech will not extend a license. I'm not going to debate the sample."
				],
				ForgivenessLine =
					"Destroying {0}'s floor model was messy, but it cleared a political obstruction. One more attempt. Make the recording useful.",
				DepartureLines =
				[
					"Your corp will operate as an independent acquisition concern. That is not a joke. Keep the paperwork clean.",
					"When a claim is verified, OuroTech may license development rights. We are not obligated to, and you should not advertise otherwise.",
					"Do the work carefully. I dislike revising projections after the fact."
				],
				TrialMission = MissionType.SearchAndDestroy,
				DemoLoaner = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_ouro_duelist",
					TorsoId = "torso_ouro_thin",
					HeadId = "head_ouro_reticle",
					PowerCoreId = "core_ouro_pulse",
					WeaponLId = "wep_ouro_rifle",
					WeaponRId = "wep_ouro_marksman",
					ShoulderLId = "shoulder_ouro_tracker",
					ShoulderRId = "",
					BackpackId = "backpack_ouro_cooler",
					SystemsId = "systems_ouro_heatsink"
				}),
				SigningLoadout = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_ouro_duelist",
					TorsoId = "torso_ouro_thin",
					HeadId = "head_ouro_scope",
					PowerCoreId = "core_ouro_pulse",
					WeaponLId = "wep_ouro_rifle",
					WeaponRId = "wep_ouro_duelist",
					ShoulderLId = "shoulder_ouro_tracker",
					ShoulderRId = "",
					BackpackId = "backpack_ouro_cooler",
					SystemsId = "systems_ouro_heatsink"
				}),
				SigningBonusPartIds = ["wep_ouro_marksman", "systems_ouro_radiator", "backpack_ouro_pulse"],
				SigningBonusScrap = 25,
				SigningBonusLives = 0,
				SigningBonusBlurb = "Precision starter MAP · +25 scrap · Marksman Caliper + Needle Radiator + Pulse Repair unlocked"
			},
			["trinova"] = new ManufacturerTrialDef
			{
				ManufacturerId = "trinova",
				LiaisonName = "Mara Keel",
				LiaisonShortName = "Keel",
				LiaisonTitle = "Logistics Desk",
				BannerSnippet = "Balanced frames, field repair systems, and support kit for newly formed corps.",
				PitchLines =
				[
					"Mara Keel, logistics. I handle trial slots, demo budgets, and the ugly part where a route fails and someone still expects delivery.",
					"You're out of academy gear. If you sign with us, you'll be escorting work that pays for fuel and repairs, not collecting medals.",
					"Trial is an escort. Keep the mining rig intact, get it through the site, and bring the load home. Three attempts.",
					"Qualify and Trinova issues a hybrid starter MAP, field mend, and enough scrap to restock once you're out of the hall.",
					"If that sounds like the job you want, take a slot. If you want glory work, try another booth."
				],
				QualifiedReturnLines =
				[
					"Rig arrived, cargo accounted for, crew still breathing. That's a clean delivery.",
					"You stayed with the asset when peeling off for a fight would've been easier. I notice that.",
					"Contract's ready. Sign and we'll get your corp supplied for the next leg."
				],
				FailedReturnLines =
				[
					"The rig didn't make delivery. Hostiles down don't count if the cargo never arrives.",
					"Stay on the asset, watch its condition, and solve the problem on the invoice first.",
					"You've got attempts left. Bring the load home next time."
				],
				WithdrawnReturnLines =
				[
					"I've written off the last demo slot and reassigned the equipment.",
					"I'm not feeding more resources into a failed route. Trinova's offer is closed."
				],
				ForgivenessLine =
					"You cost {0} a floor model. That makes my life easier for about five minutes. One more slot. Finish the escort.",
				DepartureLines =
				[
					"Past the supported lanes, fuel and repairs stop being someone else's problem. They're yours.",
					"Keep the corp solvent, file claims that hold, and the accounts stay open.",
					"That's the arrangement. No speeches. Just keep the route alive."
				],
				TrialMission = MissionType.Escort,
				DemoLoaner = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_tri_courier",
					TorsoId = "torso_tri_fleet",
					HeadId = "head_tri_optic",
					PowerCoreId = "core_tri_cell",
					WeaponLId = "wep_tri_patrol",
					WeaponRId = "wep_tri_burst",
					ShoulderLId = "shoulder_tri_patrol",
					ShoulderRId = "",
					BackpackId = "backpack_tri_mend",
					SystemsId = "systems_tri_coolant"
				}),
				SigningLoadout = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_tri_biped",
					TorsoId = "torso_tri_fleet",
					HeadId = "head_tri_optic",
					PowerCoreId = "core_tri_cell",
					WeaponLId = "wep_tri_patrol",
					WeaponRId = "wep_tri_hybrid",
					ShoulderLId = "shoulder_tri_patrol",
					ShoulderRId = "",
					BackpackId = "backpack_tri_mend",
					SystemsId = "systems_tri_coolant"
				}),
				SigningBonusPartIds = ["backpack_tri_field", "shoulder_tri_fleet", "wep_tri_workhorse"],
				SigningBonusScrap = 55,
				SigningBonusLives = 0,
				SigningBonusBlurb = "Hybrid starter MAP · +55 scrap · Field Mend + Fleet Rockets + Workhorse unlocked"
			},
			["lumina"] = new ManufacturerTrialDef
			{
				ManufacturerId = "lumina",
				LiaisonName = "Ilyra Senn",
				LiaisonShortName = "Senn",
				LiaisonTitle = "Special Projects",
				BannerSnippet = "Experimental energy systems and classified field kits. Details stay provisional.",
				PitchLines =
				[
					"Ilyra Senn, special projects. I can tell you what the trial requires. I cannot tell you what the package is.",
					"Lumina issues provisional kit for evaluation. If you wash out, you leave without it and without a useful explanation.",
					"Retrieve the sealed package. Do not open it. Do not discuss the contents. Three demo attempts.",
					"If you qualify, you leave with an experimental starter MAP and systems you're not cleared to ask about yet.",
					"Take the trial when you're prepared to follow the brief as written."
				],
				QualifiedReturnLines =
				[
					"The package is intact and the seals are unbroken. That was the assignment.",
					"You followed the brief without inventing exceptions. That matters more here than most people admit.",
					"Sign the packet. You'll receive the frame and the clearance level that goes with it."
				],
				FailedReturnLines =
				[
					"The package did not arrive. Whatever else happened on site is secondary.",
					"Follow the signal, protect the seal, and leave sealed material sealed.",
					"You still have attempts. Use the next one carefully."
				],
				WithdrawnReturnLines =
				[
					"Your evaluation clearance is revoked.",
					"I'm ending the offer. Please leave the booth before security has to."
				],
				ForgivenessLine =
					"{0} lost a floor model. That created a scheduling gap I can use. One more attempt. Do not make me regret the paperwork.",
				DepartureLines =
				[
					"The sector you're entering is not on public charts, and this assignment will not appear in them either.",
					"Your corp will still file claims under its own name. Lumina's involvement stays off the record.",
					"Acquire the sites. Report through the channels I give you. Do not improvise beyond that."
				],
				TrialMission = MissionType.DataRetrieval,
				DemoLoaner = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_lum_phasehex",
					TorsoId = "torso_lum_oracle",
					HeadId = "head_lum_oracle",
					PowerCoreId = "core_lum_arc",
					WeaponLId = "wep_lum_arc",
					WeaponRId = "wep_lum_volt",
					ShoulderLId = "shoulder_lum_arc",
					ShoulderRId = "",
					BackpackId = "backpack_lum_shroud",
					SystemsId = "systems_lum_cryo"
				}),
				SigningLoadout = GameCatalog.SanitizeLoadout(new LoadoutData
				{
					LegsId = "legs_lum_phasehex",
					TorsoId = "torso_lum_oracle",
					HeadId = "head_lum_oracle",
					PowerCoreId = "core_lum_arc",
					WeaponLId = "wep_lum_arc",
					WeaponRId = "wep_lum_spark",
					ShoulderLId = "shoulder_lum_arc",
					ShoulderRId = "",
					BackpackId = "backpack_lum_shroud",
					SystemsId = "systems_lum_cryo"
				}),
				SigningBonusPartIds = ["backpack_lum_veil", "wep_lum_ghost", "systems_lum_phase"],
				SigningBonusScrap = 30,
				SigningBonusLives = 0,
				SigningBonusBlurb = "Experimental starter MAP · +30 scrap · Veil Spool + Ghost Arc + Phase Sink unlocked"
			}
		};
	}

	public static ManufacturerTrialDef Get(string manufacturerId)
	{
		EnsureBuilt();
		return _defs!.TryGetValue(manufacturerId, out var def) ? def : _defs["trinova"];
	}

	/// <summary>Scrap-heap pity package when every house withdraws.</summary>
	public static void ApplyPityPackage(PlayerProfile profile)
	{
		EnsureBuilt();
		var loadout = GameCatalog.SanitizeLoadout(new LoadoutData
		{
			LegsId = "legs_tri_biped",
			TorsoId = "torso_tri_frame",
			HeadId = "head_tri_optic",
			PowerCoreId = "core_tri_cell",
			WeaponLId = "wep_brin_slug",
			WeaponRId = "wep_tri_burst",
			ShoulderLId = "",
			ShoulderRId = "",
			BackpackId = "backpack_tri_mend",
			SystemsId = "systems_tri_coolant"
		});
		profile.OwnedInstances.Clear();
		profile.EquippedInstanceIds.Clear();
		profile.Loadout = loadout;
		profile.GrantLoadoutOwnership(loadout);
		profile.SetAffiliation("trinova");
		profile.Scrap = 20;
		profile.LivesBank = PlayerProfile.StartingLives;
		profile.UnlockOwnedBlueprints();
	}
}
