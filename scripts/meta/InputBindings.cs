using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Remappable InputMap actions with defaults, overlap detection, and save/load.
/// </summary>
public static class InputBindings
{
	public readonly struct BindingInfo
	{
		public BindingInfo(string action, string label, string group)
		{
			Action = action;
			Label = label;
			Group = group;
		}

		public string Action { get; }
		public string Label { get; }
		public string Group { get; }
	}

	public static readonly BindingInfo[] All =
	[
		new("move_forward", "Move Forward", "Movement"),
		new("move_back", "Move Back", "Movement"),
		new("turn_left", "Turn / Strafe Left", "Movement"),
		new("turn_right", "Turn / Strafe Right", "Movement"),
		new("sprint", "Sprint", "Movement"),
		new("fire_primary", "Fire Primary", "Combat"),
		new("fire_secondary", "Fire Secondary", "Combat"),
		new("target_next", "Sensor Target Next", "Combat"),
		new("target_clear", "Sensor Target Clear", "Combat"),
		new("target_focus_cycle", "Sensor Focus Cycle", "Combat"),
		new("ability_1", "Ability 1", "Combat"),
		new("ability_2", "Ability 2", "Combat"),
		new("ability_3", "Ability 3", "Combat"),
		new("ability_4", "Ability 4", "Combat"),
		new("ability_5", "Ability 5", "Combat"),
		new("ability_6", "Ability 6", "Combat"),
		new("buy_life", "Buy Life", "Combat"),
		new("interact", "Interact / Extract", "Combat"),
		new("toggle_garage", "Field Garage", "System"),
		new("self_destruct", "Deny Asset", "System"),
		new("pause", "Pause Menu", "System")
	];

	private static readonly Dictionary<string, Godot.Collections.Array<InputEvent>> Defaults = new();
	private static bool _defaultsCaptured;

	public static void EnsureDefaultsCaptured()
	{
		if (_defaultsCaptured)
			return;
		_defaultsCaptured = true;
		EnsureBuiltinActions();
		foreach (var info in All)
		{
			if (!InputMap.HasAction(info.Action))
				continue;
			Defaults[info.Action] = InputMap.ActionGetEvents(info.Action).Duplicate();
		}
	}

	public static void EnsureBuiltinActions()
	{
		EnsureAction("pause", Key.Escape);
		EnsureAction("buy_life", Key.B);
		EnsureAction("interact", Key.F);
		EnsureAction("target_next", Key.Tab);
		EnsureAction("target_clear", Key.X);
		EnsureAction("target_focus_cycle", Key.C);
		MigrateInteractDefaultToF();
	}

	/// <summary>One-shot: old builds bound Interact to E (now used for FP strafe).</summary>
	public static void MigrateInteractDefaultToF()
	{
		if (!InputMap.HasAction("interact"))
		{
			EnsureAction("interact", Key.F);
			return;
		}

		var events = InputMap.ActionGetEvents("interact");
		if (events.Count != 1 || events[0] is not InputEventKey { PhysicalKeycode: Key.E })
			return;

		InputMap.ActionEraseEvents("interact");
		InputMap.ActionAddEvent("interact", new InputEventKey { PhysicalKeycode = Key.F });
		if (_defaultsCaptured)
			Defaults["interact"] = InputMap.ActionGetEvents("interact").Duplicate();
	}

	private static void EnsureAction(string action, Key key)
	{
		if (InputMap.HasAction(action))
			return;
		InputMap.AddAction(action);
		var ev = new InputEventKey { PhysicalKeycode = key };
		InputMap.ActionAddEvent(action, ev);
	}

	public static string FormatAction(string action)
	{
		if (!InputMap.HasAction(action))
			return "—";
		var events = InputMap.ActionGetEvents(action);
		if (events.Count == 0)
			return "Unbound";
		return string.Join(", ", events.Select(FormatEvent));
	}

	public static string FormatEvent(InputEvent ev)
	{
		return ev switch
		{
			InputEventKey key => key.PhysicalKeycode == Key.None
				? key.Keycode.ToString()
				: key.PhysicalKeycode.ToString(),
			InputEventMouseButton mouse => $"Mouse {mouse.ButtonIndex}",
			_ => ev.AsText()
		};
	}

	public static void Rebind(string action, InputEvent ev)
	{
		if (!InputMap.HasAction(action))
			InputMap.AddAction(action);
		InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, ev);
	}

	public static void ResetAction(string action)
	{
		EnsureDefaultsCaptured();
		if (!Defaults.TryGetValue(action, out var events))
			return;
		if (!InputMap.HasAction(action))
			InputMap.AddAction(action);
		InputMap.ActionEraseEvents(action);
		foreach (var ev in events)
			InputMap.ActionAddEvent(action, (InputEvent)ev);
	}

	public static void ResetAll()
	{
		EnsureDefaultsCaptured();
		foreach (var info in All)
			ResetAction(info.Action);
	}

	/// <summary>Actions that share the same physical binding fingerprint.</summary>
	public static List<string> FindOverlaps()
	{
		var map = new Dictionary<string, List<string>>();
		foreach (var info in All)
		{
			if (!InputMap.HasAction(info.Action))
				continue;
			foreach (var ev in InputMap.ActionGetEvents(info.Action))
			{
				var key = Fingerprint(ev);
				if (string.IsNullOrEmpty(key))
					continue;
				if (!map.TryGetValue(key, out var list))
				{
					list = new List<string>();
					map[key] = list;
				}
				if (!list.Contains(info.Action))
					list.Add(info.Action);
			}
		}

		return map.Values.Where(v => v.Count > 1).SelectMany(v => v).Distinct().ToList();
	}

	public static string? OverlapSummary()
	{
		var overlaps = FindOverlaps();
		if (overlaps.Count == 0)
			return null;
		var labels = overlaps
			.Select(a => All.FirstOrDefault(i => i.Action == a).Label ?? a)
			.Distinct();
		return "Overlapping binds: " + string.Join(", ", labels);
	}

	private static string Fingerprint(InputEvent ev)
	{
		return ev switch
		{
			InputEventKey key => $"key:{(int)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode)}",
			InputEventMouseButton mouse => $"mouse:{(int)mouse.ButtonIndex}",
			_ => ""
		};
	}

	public static Godot.Collections.Dictionary ToDict()
	{
		var dict = new Godot.Collections.Dictionary();
		foreach (var info in All)
		{
			if (!InputMap.HasAction(info.Action))
				continue;
			var arr = new Godot.Collections.Array();
			foreach (var ev in InputMap.ActionGetEvents(info.Action))
				arr.Add(SerializeEvent(ev));
			dict[info.Action] = arr;
		}
		return dict;
	}

	public static void ApplyFromDict(Godot.Collections.Dictionary? dict)
	{
		EnsureDefaultsCaptured();
		if (dict == null || dict.Count == 0)
			return;

		foreach (var info in All)
		{
			if (!dict.ContainsKey(info.Action))
				continue;
			if (!InputMap.HasAction(info.Action))
				InputMap.AddAction(info.Action);
			InputMap.ActionEraseEvents(info.Action);
			foreach (var v in dict[info.Action].AsGodotArray())
			{
				var ev = DeserializeEvent(v.AsGodotDictionary());
				if (ev != null)
					InputMap.ActionAddEvent(info.Action, ev);
			}
		}

		MigrateInteractDefaultToF();
	}

	private static Godot.Collections.Dictionary SerializeEvent(InputEvent ev)
	{
		return ev switch
		{
			InputEventKey key => new Godot.Collections.Dictionary
			{
				["type"] = "key",
				["physical"] = (int)key.PhysicalKeycode,
				["keycode"] = (int)key.Keycode
			},
			InputEventMouseButton mouse => new Godot.Collections.Dictionary
			{
				["type"] = "mouse",
				["button"] = (int)mouse.ButtonIndex
			},
			_ => new Godot.Collections.Dictionary { ["type"] = "unknown" }
		};
	}

	private static InputEvent? DeserializeEvent(Godot.Collections.Dictionary dict)
	{
		if (!dict.ContainsKey("type"))
			return null;
		return dict["type"].AsString() switch
		{
			"key" => new InputEventKey
			{
				PhysicalKeycode = (Key)dict["physical"].AsInt32(),
				Keycode = dict.ContainsKey("keycode") ? (Key)dict["keycode"].AsInt32() : Key.None
			},
			"mouse" => new InputEventMouseButton
			{
				ButtonIndex = (MouseButton)dict["button"].AsInt32()
			},
			_ => null
		};
	}
}
