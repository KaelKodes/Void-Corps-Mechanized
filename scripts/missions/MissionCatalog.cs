namespace Mechanize;

public enum MissionType
{
	DestroyAllEnemies,
	SearchAndDestroy,
	CaptureArea,
	CaptureMultipleAreas,
	DataRetrieval,
	SwarmDefend,
	Escort,
	BossEncounter
}

public static class MissionCatalog
{
	public readonly struct MissionInfo
	{
		public MissionInfo(MissionType type, string title, string brief)
		{
			Type = type;
			Title = title;
			Brief = brief;
		}

		public MissionType Type { get; }
		public string Title { get; }
		public string Brief { get; }
	}

	public static readonly MissionInfo[] All =
	[
		new(MissionType.DestroyAllEnemies, "Destroy All Enemies",
			"Silence every opposing MAP detachment. Hold the claim by force."),
		new(MissionType.SearchAndDestroy, "Search & Destroy",
			"Locate and demolish the marked corporate structure. Guards optional — the building is not."),
		new(MissionType.CaptureArea, "Capture Area",
			"Seize and hold the contested zone until claim beacons lock."),
		new(MissionType.CaptureMultipleAreas, "Capture Multiple Areas",
			"Secure each marked zone. Sticky captures — finish the grid."),
		new(MissionType.DataRetrieval, "Data Retrieval",
			"Crack the archive, recover the data disk, and return it to the allied pad."),
		new(MissionType.SwarmDefend, "Swarm Defend",
			"Hold the pad against fodder waves, then surviving rival MAPs."),
		new(MissionType.Escort, "Escort",
			"Shepherd the salvage crawler to the extraction gate. Stay close — it only rolls with cover."),
		new(MissionType.BossEncounter, "Warning — Boss",
			"Sector Warning. A named MAP threat ends this claim path.")
	];

	public static MissionInfo Get(MissionType type)
	{
		foreach (var info in All)
		{
			if (info.Type == type)
				return info;
		}

		return All[0];
	}
}
