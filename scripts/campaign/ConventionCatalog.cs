using System.Collections.Generic;

namespace Mechanize;

public sealed class ManufacturerTrialDef
{
	public required string ManufacturerId { get; init; }
	public required string BannerSnippet { get; init; }
	public required string[] PitchLines { get; init; }
	public required string[] QualifiedReturnLines { get; init; }
	public required string[] FailedReturnLines { get; init; }
	public required string[] WithdrawnReturnLines { get; init; }
	public required MissionType TrialMission { get; init; }
	public required LoadoutData DemoLoaner { get; init; }
	public required LoadoutData SigningLoadout { get; init; }
	public required string[] SigningBonusPartIds { get; init; }
	public int SigningBonusScrap { get; init; }
	public int SigningBonusLives { get; init; }
	public required string SigningBonusBlurb { get; init; }
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
				BannerSnippet = "Forge-slab armor. Kinetic iron. The claim goes to whoever is still standing.",
				PitchLines =
				[
					"RECRUITER  ·  Brimforge doesn't court poets. We court survivors.",
					"RECRUITER  ·  Your academy toy got recalled. Cute. Out here the scrap is real and the rival doesn't clap when you extract.",
					"RECRUITER  ·  Sign with us and you leave with iron that laughs at needles and vault toys.",
					"RECRUITER  ·  Fail the trial and you waste a floor-model chassis. Three strikes — we stop writing checks.",
					"RECRUITER  ·  Hold the pad. Deny the asset. Prove the iron trusts you."
				],
				QualifiedReturnLines =
				[
					"RECRUITER  ·  There you are. Pad held, asset denied, and the floor model came back earning its dents.",
					"RECRUITER  ·  You didn't dance around the work. You stood where we put you and made the other side move.",
					"RECRUITER  ·  Brimforge has a chassis with your name on the crate. Sign, and we'll put real iron under you."
				],
				FailedReturnLines =
				[
					"RECRUITER  ·  Back already? The pad report says somebody else wanted that ground more than you did.",
					"RECRUITER  ·  Iron doesn't make a wall by itself, pilot. Plant your feet, control the lane, and finish the hold.",
					"RECRUITER  ·  You've still got a demo slot. Use it better."
				],
				WithdrawnReturnLines =
				[
					"RECRUITER  ·  Three floor models. Three empty pads.",
					"RECRUITER  ·  Brimforge pays for survivors, not practice. Our offer is off the table."
				],
				TrialMission = MissionType.CaptureArea,
				DemoLoaner = GameCatalog.SanitizeMounts(new LoadoutData
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
				SigningLoadout = GameCatalog.SanitizeMounts(new LoadoutData
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
				BannerSnippet = "Servo gimbals. Seekers. Surgical fire — waste is amateur.",
				PitchLines =
				[
					"RECRUITER  ·  OuroTech licenses precision. Corps that spray and pray don't get a second brochure.",
					"RECRUITER  ·  You look like someone who can put a needle through a rivet head. Or you look expensive.",
					"RECRUITER  ·  Our trial is simple: find the priority asset and unmake it. Clean. No theatrics.",
					"RECRUITER  ·  Three demos. That's the budget. After that, the calipers close.",
					"RECRUITER  ·  Sign, and you walk with a marksman's frame — and the spare glass to keep it honest."
				],
				QualifiedReturnLines =
				[
					"RECRUITER  ·  Your telemetry has finished processing. Priority asset eliminated; expenditure remained within tolerance.",
					"RECRUITER  ·  More importantly, you distinguished the target from the noise. That skill is considerably rarer than marksmanship.",
					"RECRUITER  ·  Your licensing packet is ready. Sign it, and the marksman's frame leaves with you."
				],
				FailedReturnLines =
				[
					"RECRUITER  ·  We reviewed the recording. Abundant motion, abundant ammunition, no completed objective.",
					"RECRUITER  ·  Precision begins with deciding what not to shoot.",
					"RECRUITER  ·  You retain another evaluation slot. Try to make the data less embarrassing."
				],
				WithdrawnReturnLines =
				[
					"RECRUITER  ·  The evaluation sample is now statistically sufficient.",
					"RECRUITER  ·  OuroTech declines to extend a license. There is no appeal process."
				],
				TrialMission = MissionType.SearchAndDestroy,
				DemoLoaner = GameCatalog.SanitizeMounts(new LoadoutData
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
				SigningLoadout = GameCatalog.SanitizeMounts(new LoadoutData
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
				BannerSnippet = "Fleet logistics gone surface-side. Balanced frames. Mend when it matters.",
				PitchLines =
				[
					"RECRUITER  ·  Trinova keeps wings flying. Heroes make good obituaries; logistics make payroll.",
					"RECRUITER  ·  Your corps is young. Young corps die when the mend beacon is empty and the crawler is smoking.",
					"RECRUITER  ·  Escort the mining rig. Guard the dig. Bring the ore home. That's the job behind the gun.",
					"RECRUITER  ·  Three trial slots. After that we redirect the demo budget to someone who reads the brief.",
					"RECRUITER  ·  Sign with us — hybrid kit, field mend, and scrap that actually restocks."
				],
				QualifiedReturnLines =
				[
					"RECRUITER  ·  Rig made it home, ore aboard, crew accounted for. That's a clean manifest.",
					"RECRUITER  ·  You kept the job moving when shooting would've been easier. Trinova notices that.",
					"RECRUITER  ·  The contract is ready. Sign, and we'll make sure your corp has what it needs to keep moving."
				],
				FailedReturnLines =
				[
					"RECRUITER  ·  The rig didn't make delivery. Doesn't matter how many hostiles you dropped if the cargo never arrives.",
					"RECRUITER  ·  Stay near the asset, watch its condition, and solve the problem that's actually on the invoice.",
					"RECRUITER  ·  We can spare another slot. Bring the rig back next time."
				],
				WithdrawnReturnLines =
				[
					"RECRUITER  ·  We've written off the last demo slot and reassigned the equipment.",
					"RECRUITER  ·  Logistics only works when we stop feeding resources into a failed route. This one is closed."
				],
				TrialMission = MissionType.Escort,
				DemoLoaner = GameCatalog.SanitizeMounts(new LoadoutData
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
				SigningLoadout = GameCatalog.SanitizeMounts(new LoadoutData
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
				BannerSnippet = "Vault-lab experimental wing. Energy arcs. Shrouds. Don't ask what's classified.",
				PitchLines =
				[
					"RECRUITER  ·  Lumina Vaultworks doesn't recruit. We curate.",
					"RECRUITER  ·  The brief is sealed. The kit is provisional. The people who wash out forget the hallway.",
					"RECRUITER  ·  Retrieve the package. Don't open it. Don't discuss it. Prove the shroud trusts your hands.",
					"RECRUITER  ·  Three demos. After that the vault door remembers your face as a rejected key.",
					"RECRUITER  ·  Sign — and you leave with arcs, a veil, and questions you will learn not to ask."
				],
				QualifiedReturnLines =
				[
					"RECRUITER  ·  The package is intact. Its seals indicate you resisted curiosity.",
					"RECRUITER  ·  The shroud accepted your inputs. The vault has elected not to reject you.",
					"RECRUITER  ·  Sign the sealed page. Do not concern yourself with the pages you cannot see."
				],
				FailedReturnLines =
				[
					"RECRUITER  ·  The package did not arrive. The vault anticipated this outcome, though it hoped to be surprised.",
					"RECRUITER  ·  Follow the signal. Trust the shroud. Leave unopened things unopened.",
					"RECRUITER  ·  The corridor remains accessible—for now."
				],
				WithdrawnReturnLines =
				[
					"RECRUITER  ·  The corridor no longer recognizes your clearance.",
					"RECRUITER  ·  You should leave before the rest of the building reaches the same conclusion."
				],
				TrialMission = MissionType.DataRetrieval,
				DemoLoaner = GameCatalog.SanitizeMounts(new LoadoutData
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
				SigningLoadout = GameCatalog.SanitizeMounts(new LoadoutData
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
		var loadout = GameCatalog.SanitizeMounts(new LoadoutData
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
		profile.OwnedCounts.Clear();
		profile.Loadout = loadout;
		profile.GrantLoadoutOwnership(loadout);
		profile.SetAffiliation("trinova");
		profile.Scrap = 20;
		profile.LivesBank = PlayerProfile.StartingLives;
		profile.AddReputation("trinova", 1);
	}
}
