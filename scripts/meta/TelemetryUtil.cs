using Godot;

namespace Mechanize;

public static class TelemetryUtil
{
	public static MatchSession? Match(Node? node) =>
		node?.GetNodeOrNull<GameSession>("/root/GameSession")?.Match;

	public static bool IsPlayerSource(Node? node) => node switch
	{
		MechController mech => mech.IsPlayerControlled,
		EscortAsset => false,
		SupportUnit unit => unit.Team == TeamId.Player,
		HealBeacon beacon => beacon.Source != null && IsPlayerSource(beacon.Source),
		_ => false
	};

	public static TelemetryTargetKind Classify(Node? node) => node switch
	{
		MissionBuilding => TelemetryTargetKind.Building,
		EscortAsset => TelemetryTargetKind.Escort,
		SupportUnit => TelemetryTargetKind.Mad,
		DummyTarget => TelemetryTargetKind.Fodder,
		MechController => TelemetryTargetKind.Map,
		_ => TelemetryTargetKind.Unknown
	};
}
