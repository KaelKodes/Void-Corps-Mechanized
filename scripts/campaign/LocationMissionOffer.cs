using Godot;

namespace Mechanize;

/// <summary>
/// One manufacturer contract at a campaign location. Completing it clears the location
/// and gates the post-mission shop to that manufacturer.
/// </summary>
public sealed class LocationMissionOffer
{
	public string ManufacturerId { get; set; } = "";
	public string RivalManufacturerId { get; set; } = "";
	public MissionType MissionType { get; set; } = MissionType.DestroyAllEnemies;
	public PilotDifficulty Difficulty { get; set; } = PilotDifficulty.Easy;
	public int RepGain { get; set; } = 3;
	public int RepLoss { get; set; } = 2;
	public int Seed { get; set; }
	public BossEncounterId BossEncounterId { get; set; } = BossEncounterId.None;

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["mfg"] = ManufacturerId,
		["rival"] = RivalManufacturerId,
		["mission"] = (int)MissionType,
		["diff"] = (int)Difficulty,
		["rep_gain"] = RepGain,
		["rep_loss"] = RepLoss,
		["seed"] = Seed,
		["boss"] = (int)BossEncounterId
	};

	public static LocationMissionOffer FromDict(Godot.Collections.Dictionary d) => new()
	{
		ManufacturerId = d.ContainsKey("mfg") ? d["mfg"].AsString() : "",
		RivalManufacturerId = d.ContainsKey("rival") ? d["rival"].AsString() : "",
		MissionType = d.ContainsKey("mission") ? (MissionType)d["mission"].AsInt32() : MissionType.DestroyAllEnemies,
		Difficulty = d.ContainsKey("diff") ? (PilotDifficulty)d["diff"].AsInt32() : PilotDifficulty.Easy,
		RepGain = d.ContainsKey("rep_gain") ? d["rep_gain"].AsInt32() : 3,
		RepLoss = d.ContainsKey("rep_loss") ? d["rep_loss"].AsInt32() : 2,
		Seed = d.ContainsKey("seed") ? d["seed"].AsInt32() : 0,
		BossEncounterId = d.ContainsKey("boss") ? (BossEncounterId)d["boss"].AsInt32() : BossEncounterId.None
	};
}
