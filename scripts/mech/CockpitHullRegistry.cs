using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Mech 2.0 hollow hull registry: VisualKind → authored cockpit scene.
/// All scenes must expose CockpitAnchor and follow Fleet Intermediate node naming for tint binders.
/// SeatLever is ensured at ApplyPart time under CockpitInterior/Dashboard.
/// </summary>
public static class CockpitHullRegistry
{
	private static readonly Dictionary<string, string> SceneByVisualKind = new()
	{
		["torso_fleet"] = "res://scenes/parts/torso_tri_fleet.tscn",
		["torso_brin_anvil"] = "res://scenes/parts/torso_brin_anvil.tscn",
		["torso_ouro_thin"] = "res://scenes/parts/torso_ouro_thin.tscn",
		["torso_lum_oracle"] = "res://scenes/parts/torso_lum_oracle.tscn",
		["torso_ash_ashrib"] = "res://scenes/parts/torso_ash_ashrib.tscn",
		["torso_vel_ruff"] = "res://scenes/parts/torso_vel_ruff.tscn"
	};

	public static bool IsCockpitHull(string? visualKind) =>
		!string.IsNullOrEmpty(visualKind) && SceneByVisualKind.ContainsKey(visualKind);

	public static bool TryGetScenePath(string visualKind, out string path) =>
		SceneByVisualKind.TryGetValue(visualKind, out path!);

	/// <summary>Instantiate the house cockpit scene and ApplyPart tint.</summary>
	public static void Attach(Node3D root, PartData part)
	{
		if (!TryGetScenePath(part.VisualKind, out var path))
			return;

		root.SetMeta("CockpitTorso", true);
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			GD.PushError($"CockpitHullRegistry: missing scene for {part.VisualKind} at {path}");
			return;
		}

		var hull = scene.Instantiate<CockpitTorsoVisual>();
		hull.ApplyPart(part);
		root.AddChild(hull);
	}
}
