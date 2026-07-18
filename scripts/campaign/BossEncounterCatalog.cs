namespace Mechanize;

public sealed class BossEncounterDef
{
	public BossEncounterId Id { get; init; }
	public BossEncounterTemplate Template { get; init; }
	public string RivalPilotId { get; init; } = "";
	public string SectorClaimCode { get; init; } = "";
	public string Brief { get; init; } = "";
	public MechChassisClass ChassisClass { get; init; } = MechChassisClass.Titan;

	public RivalPilotDef Pilot => RivalRosterCatalog.GetPilot(RivalPilotId);
	public RivalCorpDef Corp => RivalRosterCatalog.GetCorp(Pilot.CorpId);
	public string BossName => Pilot.Callsign.ToUpperInvariant();
	public string ArrivalLine =>
		$"{Corp.ShortName} Titan {Pilot.Callsign} is on the claim — commanded by {Pilot.Name}.";
	public int LoadoutVariant => Pilot.LoadoutVariant;
}

public static class BossEncounterCatalog
{
	public static readonly BossEncounterDef[] All =
	[
		new BossEncounterDef
		{
			Id = BossEncounterId.OrbitalDuelist,
			Template = BossEncounterTemplate.Showdown,
			RivalPilotId = "nadi_kess",
			SectorClaimCode = "VC-CLAIM 7-ORBITAL",
			ChassisClass = MechChassisClass.Titan,
			Brief = "Wayfarer brought a Titan-class MAP to hold the orbital filing. Nadi Kess is commanding it until their survey crew arrives."
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.GridAshSwarmLord,
			Template = BossEncounterTemplate.SwarmToBoss,
			RivalPilotId = "anja_serrin",
			SectorClaimCode = "VC-CLAIM GRID-ASH",
			ChassisClass = MechChassisClass.Titan,
			Brief = "Grey Banner support units are locking the relay. Clear them before Anja Serrin's Titan reinforces the pad."
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.WharfHiddenWarden,
			Template = BossEncounterTemplate.HiddenBoss,
			RivalPilotId = "yara_quill",
			SectorClaimCode = "VC-CLAIM BLACK-WHARF",
			ChassisClass = MechChassisClass.Titan,
			Brief = "Ninth Meridian hid a Titan cradle inside the wharf structure. Breach it before Yara Quill finishes startup."
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.SlagFoundryDuelist,
			Template = BossEncounterTemplate.Showdown,
			RivalPilotId = "elias_rowe",
			SectorClaimCode = "VC-CLAIM SLAG-FOUNDRY",
			ChassisClass = MechChassisClass.Titan,
			Brief = "Grey Banner wants the furnace rights. Elias Rowe is enforcing them from a Titan on the pour floor."
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.SpireNullWarden,
			Template = BossEncounterTemplate.HiddenBoss,
			RivalPilotId = "jules_orra",
			SectorClaimCode = "VC-CLAIM SPIRE-NULL",
			ChassisClass = MechChassisClass.Titan,
			Brief = "Wayfarer concealed a Titan firing position beneath the plaza. Jules Orra is using it to control the skyline claim."
		}
	];

	public static BossEncounterDef Get(BossEncounterId id)
	{
		foreach (var def in All)
		{
			if (def.Id == id)
				return def;
		}

		return All[0];
	}

	public static BossEncounterId ForSectorClaim(string claimCode)
	{
		foreach (var def in All)
		{
			if (def.SectorClaimCode == claimCode)
				return def.Id;
		}

		return BossEncounterId.OrbitalDuelist;
	}
}
