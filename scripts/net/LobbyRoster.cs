using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum LobbyPhase
{
	Offline = 0,
	Entry = 1,
	MatchSetup = 2
}

public enum MultiplayerGameMode
{
	CoopCampaign = 0,
	CoopRogueLike = 1,
	TeamSkirmish = 2,
	FfaSkirmish = 3
}

public enum LobbySlotKind
{
	Empty = 0,
	Human = 1,
	Bot = 2
}

public enum LobbyTeam
{
	None = 0,
	Alpha = 1,
	Bravo = 2,
	Ffa = 3
}

/// <summary>One seat in the multiplayer lobby (human, bot, or empty).</summary>
public sealed class LobbySlot
{
	public LobbySlotKind Kind = LobbySlotKind.Empty;
	public int PeerId;
	public int BotId;
	public LobbyTeam Team = LobbyTeam.None;
	public int FfaIndex;
	public bool Ready;
	public string DisplayName = "";

	public bool IsOccupied => Kind != LobbySlotKind.Empty;
	public bool IsHuman => Kind == LobbySlotKind.Human;
	public bool IsBot => Kind == LobbySlotKind.Bot;

	public int OwningId => Kind switch
	{
		LobbySlotKind.Human => PeerId,
		LobbySlotKind.Bot => BotId,
		_ => 0
	};

	public LobbySlot Clone() => new()
	{
		Kind = Kind,
		PeerId = PeerId,
		BotId = BotId,
		Team = Team,
		FfaIndex = FfaIndex,
		Ready = Ready,
		DisplayName = DisplayName
	};

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["kind"] = (int)Kind,
		["peer"] = PeerId,
		["bot"] = BotId,
		["team"] = (int)Team,
		["ffa"] = FfaIndex,
		["ready"] = Ready,
		["name"] = DisplayName
	};

	public static LobbySlot FromDict(Godot.Collections.Dictionary dict)
	{
		return new LobbySlot
		{
			Kind = dict.ContainsKey("kind") ? (LobbySlotKind)dict["kind"].AsInt32() : LobbySlotKind.Empty,
			PeerId = dict.ContainsKey("peer") ? dict["peer"].AsInt32() : 0,
			BotId = dict.ContainsKey("bot") ? dict["bot"].AsInt32() : 0,
			Team = dict.ContainsKey("team") ? (LobbyTeam)dict["team"].AsInt32() : LobbyTeam.None,
			FfaIndex = dict.ContainsKey("ffa") ? dict["ffa"].AsInt32() : 0,
			Ready = dict.ContainsKey("ready") && dict["ready"].AsBool(),
			DisplayName = dict.ContainsKey("name") ? dict["name"].AsString() : ""
		};
	}
}

/// <summary>Helper sizing and display for multiplayer modes.</summary>
public static class LobbyModeRules
{
	public const int MaxCoopSlots = 4;
	public const int MaxTeamPerSide = 4;
	public const int MaxFfaSlots = 8;
	public const int MaxHumans = 8;
	public const int BotIdBase = -100;

	public static string ModeLabel(MultiplayerGameMode mode) => mode switch
	{
		MultiplayerGameMode.CoopCampaign => "Co-op Campaign",
		MultiplayerGameMode.CoopRogueLike => "Co-Op Rogue-Like",
		MultiplayerGameMode.TeamSkirmish => "Team Skirmish",
		MultiplayerGameMode.FfaSkirmish => "FFA Skirmish",
		_ => "Multiplayer"
	};

	public static int SlotCount(MultiplayerGameMode mode) => mode switch
	{
		MultiplayerGameMode.CoopCampaign or MultiplayerGameMode.CoopRogueLike => MaxCoopSlots,
		MultiplayerGameMode.TeamSkirmish => MaxTeamPerSide * 2,
		MultiplayerGameMode.FfaSkirmish => MaxFfaSlots,
		_ => MaxCoopSlots
	};

	public static int MaxHumanPeers(MultiplayerGameMode mode) => mode switch
	{
		MultiplayerGameMode.CoopCampaign or MultiplayerGameMode.CoopRogueLike => MaxCoopSlots,
		MultiplayerGameMode.TeamSkirmish => MaxTeamPerSide * 2,
		MultiplayerGameMode.FfaSkirmish => MaxFfaSlots,
		_ => MaxCoopSlots
	};

	public static bool SupportsBots(MultiplayerGameMode mode) => mode is
		MultiplayerGameMode.TeamSkirmish or MultiplayerGameMode.FfaSkirmish
		or MultiplayerGameMode.CoopCampaign or MultiplayerGameMode.CoopRogueLike;

	public static bool IsCoop(MultiplayerGameMode mode) =>
		mode is MultiplayerGameMode.CoopCampaign or MultiplayerGameMode.CoopRogueLike;

	public static bool IsPvp(MultiplayerGameMode mode) =>
		mode is MultiplayerGameMode.TeamSkirmish or MultiplayerGameMode.FfaSkirmish;

	public static TeamId ToCombatTeam(LobbySlot slot)
	{
		if (slot.Team == LobbyTeam.Alpha)
			return TeamId.Alpha;
		if (slot.Team == LobbyTeam.Bravo)
			return TeamId.Bravo;
		if (slot.Team == LobbyTeam.Ffa)
			return TeamId.Ffa1 + Math.Clamp(slot.FfaIndex, 0, 7);
		return TeamId.Player;
	}
}
