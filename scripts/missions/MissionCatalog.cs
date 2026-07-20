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
	BossEncounter,
	/// <summary>MAP Cadet Program — scripted range (unfailable).</summary>
	CadetRange,
	/// <summary>Bullet-hell sabotage run — reach Point B, plant, extract.</summary>
	Sabotage = 9
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
			"Seize the contested zone until claim beacons lock. Expect a mid-capture push — and sometimes a MAP counter-drop."),
		new(MissionType.CaptureMultipleAreas, "Capture Multiple Areas",
			"Secure each marked zone. Sticky captures. Each site can call a mid-hold wave and a late MAP counter."),
		new(MissionType.DataRetrieval, "Data Retrieval",
			"Crack the archive, recover the data disk, and return it to the allied pad."),
		new(MissionType.SwarmDefend, "Swarm Defend",
			"Hold the pad against fodder waves, then surviving rival MAPs."),
		new(MissionType.Escort, "Mining Escort",
			"Escort the company mining rig to the vein, guard the dig, then haul cargo home. Vein work draws reinforcements."),
		new(MissionType.BossEncounter, "Warning — Titan Claim",
			"Sector Warning. A rival corp has fielded a Titan-class MAP to contest the claim."),
		new(MissionType.CadetRange, "Cadet Range",
			"MAP Cadet Program certification range. Learn the sticks before live fire."),
		new(MissionType.Sabotage, "Sabotage Run",
			"Infiltrate the corridor under patterned fire. Reach the uplink, plant the package, and call for exfil.")
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
