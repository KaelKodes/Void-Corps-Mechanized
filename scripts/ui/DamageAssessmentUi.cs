using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Post-mission damage board — repair equipped instances to new before the exchange floor.
/// </summary>
public partial class DamageAssessmentUi : Control
{
	private PlayerProfile _profile = null!;
	private MatchSession _match = null!;
	private GameSession _session = null!;
	private Action? _onContinue;
	private readonly HashSet<PartSlot> _selected = new();
	private Label? _scrapLabel;
	private VBoxContainer? _list;
	private Label? _hint;

	public void Open(GameSession session, Action onContinue)
	{
		_session = session;
		_profile = session.Profile;
		_match = session.Match;
		_onContinue = onContinue;
		_selected.Clear();

		// Seed selection with every damaged / destroyed slot.
		foreach (var (slot, condition) in _match.FinalConditionBySlot)
		{
			if (condition.NeedsRepair)
				_selected.Add(slot);
		}

		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Results);
		Build();
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		AddChild(MechUiTheme.MakeDimOverlay());

		var margins = new MarginContainer();
		margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margins.AddThemeConstantOverride("margin_left", 40);
		margins.AddThemeConstantOverride("margin_top", 32);
		margins.AddThemeConstantOverride("margin_right", 40);
		margins.AddThemeConstantOverride("margin_bottom", 32);
		AddChild(margins);

		var root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 12);
		margins.AddChild(root);

		var title = new Label
		{
			Text = "DAMAGE ASSESSMENT",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", MechUiTheme.Accent);
		root.AddChild(title);

		var sub = new Label
		{
			Text = "Repairs restore parts to factory-new. Destroyed systems carry a reconstruction premium.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		sub.AddThemeFontSizeOverride("font_size", 14);
		sub.Modulate = new Color(0.75f, 0.8f, 0.85f);
		root.AddChild(sub);

		_scrapLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_scrapLabel.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(_scrapLabel);

		var panel = MechUiTheme.MakePanel("DamageList", deep: true);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(panel);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panel.AddChild(scroll);

		_list = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_list.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_list);

		_hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_hint.AddThemeFontSizeOverride("font_size", 13);
		root.AddChild(_hint);

		var nav = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		nav.AddThemeConstantOverride("separation", 12);
		root.AddChild(nav);

		nav.AddChild(MakeNavButton("REPAIR SELECTED", OnRepairSelected));
		nav.AddChild(MakeNavButton("REPAIR ALL AFFORDABLE", OnRepairAllAffordable));
		nav.AddChild(MakeNavButton("SKIP / CONTINUE", OnContinue));

		Refresh();
	}

	private static Button MakeNavButton(string text, Action pressed)
	{
		var btn = new Button { Text = text, CustomMinimumSize = new Vector2(220, 40) };
		MechUiTheme.StylePrimaryButton(btn);
		btn.Pressed += pressed;
		return btn;
	}

	private void Refresh()
	{
		if (_list == null || _scrapLabel == null)
			return;

		foreach (var child in _list.GetChildren())
			child.QueueFree();

		var availableScrap = _profile.Scrap + _match.RunScrap;
		_scrapLabel.Text = $"Available scrap (bank + run): {availableScrap}   ·   Bank {_profile.Scrap}  ·  Run {_match.RunScrap}";

		if (_match.FinalConditionBySlot.Count == 0)
		{
			_list.AddChild(new Label { Text = "No equipped combat parts recorded." });
			if (_hint != null)
				_hint.Text = "Continue to the field exchange.";
			return;
		}

		foreach (var slot in _match.FinalConditionBySlot.Keys.OrderBy(s => (int)s))
		{
			var condition = _match.FinalConditionBySlot[slot];
			var instance = _profile.GetEquippedInstance(slot);
			var partId = instance?.PartId ?? _profile.Loadout.GetPartId(slot);
			var part = GameCatalog.GetPart(partId);
			if (part == null)
				continue;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 10);
			_list.AddChild(row);

			var check = new CheckBox
			{
				ButtonPressed = _selected.Contains(slot),
				Disabled = !condition.NeedsRepair,
				Text = slot.ToString()
			};
			check.Toggled += on =>
			{
				if (on)
					_selected.Add(slot);
				else
					_selected.Remove(slot);
				RefreshHint();
			};
			row.AddChild(check);

			var name = new Label
			{
				Text = part.DisplayName,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			row.AddChild(name);

			var status = condition.Destroyed
				? "DESTROYED"
				: condition.IsFull
					? "NOMINAL"
					: $"{condition.AverageRatio * 100f:0}%";
			var statusLabel = new Label { Text = status, CustomMinimumSize = new Vector2(110, 0) };
			statusLabel.Modulate = condition.Destroyed
				? MechUiTheme.Danger
				: condition.IsFull
					? MechUiTheme.Success
					: MechUiTheme.AccentHot;
			row.AddChild(statusLabel);

			var repairCost = RepairService.RepairCost(part, condition);
			var replace = RepairService.ReplacementPrice(part);
			var costLabel = new Label
			{
				Text = condition.NeedsRepair
					? $"Repair {repairCost}  ·  Replace {replace}"
					: "—",
				CustomMinimumSize = new Vector2(220, 0),
				HorizontalAlignment = HorizontalAlignment.Right
			};
			if (condition.Destroyed && repairCost > replace)
				costLabel.Modulate = MechUiTheme.AccentHot;
			row.AddChild(costLabel);
		}

		RefreshHint();
	}

	private void RefreshHint()
	{
		if (_hint == null)
			return;
		var selectedCost = 0;
		foreach (var slot in _selected)
		{
			if (!_match.FinalConditionBySlot.TryGetValue(slot, out var condition) || !condition.NeedsRepair)
				continue;
			var instance = _profile.GetEquippedInstance(slot);
			var part = GameCatalog.GetPart(instance?.PartId ?? "");
			if (part != null)
				selectedCost += RepairService.RepairCost(part, condition);
		}

		var sb = new StringBuilder();
		sb.Append($"Selected repair cost: {selectedCost}. ");
		sb.Append("You may continue with damaged or destroyed gear — offline systems stay equipped.");
		_hint.Text = sb.ToString();
	}

	private int SpendFromPools(int cost)
	{
		if (cost <= 0)
			return 0;
		var fromRun = Mathf.Min(_match.RunScrap, cost);
		_match.RunScrap -= fromRun;
		var remain = cost - fromRun;
		if (remain > 0)
			_profile.Scrap = Mathf.Max(0, _profile.Scrap - remain);
		return cost;
	}

	private bool TryRepairSlot(PartSlot slot)
	{
		if (!_match.FinalConditionBySlot.TryGetValue(slot, out var condition) || !condition.NeedsRepair)
			return false;

		var instance = _profile.GetEquippedInstance(slot);
		var part = GameCatalog.GetPart(instance?.PartId ?? "");
		if (part == null || instance == null)
			return false;

		var cost = RepairService.RepairCost(part, condition);
		var pool = _profile.Scrap + _match.RunScrap;
		if (pool < cost)
			return false;

		SpendFromPools(cost);
		instance.Condition.SetFull();
		condition.SetFull();
		_selected.Remove(slot);
		return true;
	}

	private void OnRepairSelected()
	{
		var any = false;
		foreach (var slot in _selected.ToList())
			any |= TryRepairSlot(slot);
		if (any)
			SfxService.Confirm();
		else
			SfxService.Play("alarm", 1.1f, -6f);
		Refresh();
	}

	private void OnRepairAllAffordable()
	{
		var repaired = 0;
		foreach (var slot in _match.FinalConditionBySlot.Keys.OrderBy(s => (int)s).ToList())
		{
			if (TryRepairSlot(slot))
				repaired++;
		}

		if (repaired > 0)
			SfxService.Confirm();
		else
			SfxService.Play("alarm", 1.1f, -6f);
		Refresh();
	}

	private void OnContinue()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Ignore;
		_session.CommitMatchRewards();
		_onContinue?.Invoke();
	}
}
