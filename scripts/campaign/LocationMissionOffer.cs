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
	/// <summary>Recurring merc pilot attached to this offer as a boss or mini-boss.</summary>
	public string RivalPilotId { get; set; } = "";
	public string RivalCorpId { get; set; } = "";

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["mfg"] = ManufacturerId,
		["rival"] = RivalManufacturerId,
		["mission"] = (int)MissionType,
		["diff"] = (int)Difficulty,
		["rep_gain"] = RepGain,
		["rep_loss"] = RepLoss,
		["seed"] = Seed,
		["boss"] = (int)BossEncounterId,
		["rival_pilot"] = RivalPilotId,
		["rival_corp"] = RivalCorpId
	};

	public static LocationMissionOffer FromDict(Godot.Collections.Dictionary d)
	{
		var offer = new LocationMissionOffer
		{
			ManufacturerId = d.ContainsKey("mfg") ? d["mfg"].AsString() : "",
			RivalManufacturerId = d.ContainsKey("rival") ? d["rival"].AsString() : "",
			MissionType = d.ContainsKey("mission") ? (MissionType)d["mission"].AsInt32() : MissionType.DestroyAllEnemies,
			Difficulty = d.ContainsKey("diff") ? (PilotDifficulty)d["diff"].AsInt32() : PilotDifficulty.Easy,
			RepGain = d.ContainsKey("rep_gain") ? d["rep_gain"].AsInt32() : 3,
			RepLoss = d.ContainsKey("rep_loss") ? d["rep_loss"].AsInt32() : 2,
			Seed = d.ContainsKey("seed") ? d["seed"].AsInt32() : 0,
			BossEncounterId = d.ContainsKey("boss") ? (BossEncounterId)d["boss"].AsInt32() : BossEncounterId.None,
			RivalPilotId = d.ContainsKey("rival_pilot") ? d["rival_pilot"].AsString() : "",
			RivalCorpId = d.ContainsKey("rival_corp") ? d["rival_corp"].AsString() : ""
		};

		// Migrate old Warning offers away from manufacturer reputation framing.
		if (offer.MissionType == MissionType.BossEncounter)
		{
			var encounter = BossEncounterCatalog.Get(offer.BossEncounterId);
			offer.ManufacturerId = "";
			offer.RivalManufacturerId = "";
			offer.RepGain = 0;
			offer.RepLoss = 0;
			offer.RivalPilotId = encounter.RivalPilotId;
			offer.RivalCorpId = encounter.Corp.Id;
		}

		return offer;
	}
}
