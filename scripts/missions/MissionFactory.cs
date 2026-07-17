namespace Mechanize;

public static class MissionFactory
{
	public static MissionBase Create(MissionType type, BossEncounterId bossEncounter = BossEncounterId.None) =>
		type switch
		{
			MissionType.SearchAndDestroy => new SearchDestroyMission(),
			MissionType.CaptureArea => new CaptureMission(zoneCount: 1),
			MissionType.CaptureMultipleAreas => new CaptureMission(zoneCount: 3),
			MissionType.DataRetrieval => new DataRetrievalMission(),
			MissionType.SwarmDefend => new SwarmDefendMission(),
			MissionType.Escort => new EscortMission(),
			MissionType.BossEncounter => new BossMission(
				BossEncounterCatalog.Get(bossEncounter == BossEncounterId.None
					? BossEncounterId.OrbitalDuelist
					: bossEncounter)),
			_ => new EliminateMission()
		};
}
