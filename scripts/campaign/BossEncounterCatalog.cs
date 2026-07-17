namespace Mechanize;

public sealed class BossEncounterDef
{
	public BossEncounterId Id { get; init; }
	public BossEncounterTemplate Template { get; init; }
	public string BossName { get; init; } = "";
	public string SectorClaimCode { get; init; } = "";
	public string ArrivalLine { get; init; } = "";
	public string Brief { get; init; } = "";
	public int LoadoutVariant { get; init; }
}

public static class BossEncounterCatalog
{
	public static readonly BossEncounterDef[] All =
	[
		new BossEncounterDef
		{
			Id = BossEncounterId.OrbitalDuelist,
			Template = BossEncounterTemplate.Showdown,
			BossName = "ASH-RAKE",
			SectorClaimCode = "VC-CLAIM 7-ORBITAL",
			ArrivalLine = "ASH-RAKE is waiting on the claim.",
			Brief = "Showdown. No gimmicks — silence the rival MAP.",
			LoadoutVariant = 2
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.GridAshSwarmLord,
			Template = BossEncounterTemplate.SwarmToBoss,
			BossName = "GRID-HOWL",
			SectorClaimCode = "VC-CLAIM GRID-ASH",
			ArrivalLine = "GRID-HOWL has arrived…",
			Brief = "Hold the swarm. When the quiet breaks, the drop comes.",
			LoadoutVariant = 1
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.WharfHiddenWarden,
			Template = BossEncounterTemplate.HiddenBoss,
			BossName = "WHARF-WARDEN",
			SectorClaimCode = "VC-CLAIM BLACK-WHARF",
			ArrivalLine = "The structure ruptures — WHARF-WARDEN stands.",
			Brief = "Seek and destroy the marked structure. Something nests inside.",
			LoadoutVariant = 2
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.SlagFoundryDuelist,
			Template = BossEncounterTemplate.Showdown,
			BossName = "SLAG-CROWN",
			SectorClaimCode = "VC-CLAIM SLAG-FOUNDRY",
			ArrivalLine = "SLAG-CROWN walks the pour floor.",
			Brief = "Foundry duel. Deny the furnace rights.",
			LoadoutVariant = 2
		},
		new BossEncounterDef
		{
			Id = BossEncounterId.SpireNullWarden,
			Template = BossEncounterTemplate.HiddenBoss,
			BossName = "SPIRE-NULL",
			SectorClaimCode = "VC-CLAIM SPIRE-NULL",
			ArrivalLine = "The plaza monument cracks — SPIRE-NULL rises.",
			Brief = "Crack the plaza nest. The skyline claim answers.",
			LoadoutVariant = 1
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
