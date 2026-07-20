using Godot;

namespace Mechanize;

/// <summary>
/// PWR / total HEAT / SPD gauges for the cockpit wing-L systems screen.
/// </summary>
public partial class CockpitSystemsPanel : Control
{
	public enum LayoutMode
	{
		Vertical,
		Horizontal
	}

	private static readonly Color PowerFill = new(0.35f, 0.7f, 1f);
	private static readonly Color HeatFill = new(0.95f, 0.45f, 0.2f);
	private static readonly Color SpeedFill = new(0.45f, 0.9f, 0.55f);
	private static readonly Color EmptyColor = new(0.82f, 0.18f, 0.16f);

	private Control? _meterHost;
	private ProgressBar? _powerBar;
	private ProgressBar? _heatBar;
	private ProgressBar? _speedBar;
	private Label? _powerLabel;
	private Label? _heatLabel;
	private Label? _speedLabel;
	private LayoutMode _layout = LayoutMode.Vertical;

	public LayoutMode Layout
	{
		get => _layout;
		set
		{
			if (_layout == value)
				return;
			_layout = value;
			RebuildMeters();
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(280, 220);
		BuildChrome();
		RebuildMeters();
	}

	public void Refresh(MechController mech)
	{
		var power = mech.PowerHeat;
		var stats = mech.Assembler?.Stats;
		var heatCap = stats?.HeatCap ?? 0f;

		ApplyPowerMeter(power);
		ApplyHeatMeter(power, heatCap);
		ApplySpeedMeter(mech);
	}

	private void BuildChrome()
	{
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 4);
		col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(col);

		var header = new Label
		{
			Text = "// SYSTEMS",
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		header.AddThemeFontSizeOverride("font_size", 9);
		col.AddChild(header);

		_meterHost = new Control
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		col.AddChild(_meterHost);
	}

	private void RebuildMeters()
	{
		if (_meterHost == null)
			return;

		foreach (var child in _meterHost.GetChildren())
			child.QueueFree();

		_powerBar = null;
		_heatBar = null;
		_speedBar = null;
		_powerLabel = null;
		_heatLabel = null;
		_speedLabel = null;

		if (_layout == LayoutMode.Horizontal)
			BuildHorizontalMeters(_meterHost);
		else
			BuildVerticalMeters(_meterHost);
	}

	private void BuildVerticalMeters(Control host)
	{
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 6);
		col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		host.AddChild(col);

		(_powerBar, _powerLabel) = MakeMeter(col, "PWR", PowerFill, true, new Vector2(0, 52));
		(_heatBar, _heatLabel) = MakeMeter(col, "HEAT", HeatFill, true, new Vector2(0, 52));
		(_speedBar, _speedLabel) = MakeMeter(col, "SPD", SpeedFill, true, new Vector2(0, 52));
	}

	private void BuildHorizontalMeters(Control host)
	{
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		host.AddChild(row);

		(_powerBar, _powerLabel) = MakeMeter(row, "PWR", PowerFill, false, new Vector2(72, 0));
		(_heatBar, _heatLabel) = MakeMeter(row, "HEAT", HeatFill, false, new Vector2(72, 0));
		(_speedBar, _speedLabel) = MakeMeter(row, "SPD", SpeedFill, false, new Vector2(72, 0));
	}

	private static (ProgressBar bar, Label caption) MakeMeter(
		Control parent,
		string caption,
		Color fill,
		bool vertical,
		Vector2 barSize)
	{
		var wrap = new VBoxContainer
		{
			SizeFlagsHorizontal = vertical ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		wrap.AddThemeConstantOverride("separation", 2);
		parent.AddChild(wrap);

		var bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 1,
			Value = 0,
			ShowPercentage = false,
			CustomMinimumSize = barSize,
			SizeFlagsHorizontal = vertical ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bar.FillMode = vertical
			? (int)ProgressBar.FillModeEnum.BottomToTop
			: (int)ProgressBar.FillModeEnum.LeftToRight;
		bar.AddThemeStyleboxOverride("background", MakeBarBackground());
		bar.AddThemeStyleboxOverride("fill", MakeBarFill(fill));
		wrap.AddChild(bar);

		var label = new Label
		{
			Text = caption,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 8);
		wrap.AddChild(label);
		return (bar, label);
	}

	private static StyleBoxFlat MakeBarBackground() => new()
	{
		BgColor = new Color(0.06f, 0.08f, 0.1f, 0.82f),
		BorderColor = new Color(0.45f, 0.38f, 0.24f, 0.75f),
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		CornerRadiusTopLeft = 4,
		CornerRadiusTopRight = 4,
		CornerRadiusBottomRight = 4,
		CornerRadiusBottomLeft = 4,
		ContentMarginLeft = 2,
		ContentMarginRight = 2,
		ContentMarginTop = 2,
		ContentMarginBottom = 2
	};

	private static StyleBoxFlat MakeBarFill(Color fill) => new()
	{
		BgColor = fill,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomRight = 3,
		CornerRadiusBottomLeft = 3
	};

	private void ApplyPowerMeter(MechPowerHeat? power)
	{
		if (_powerBar == null)
			return;

		_powerBar.Value = power?.PowerRatio ?? 0f;
		if (_powerLabel == null)
			return;

		_powerLabel.Text = power == null
			? "PWR"
			: $"PWR {power.CurrentPower:0}/{power.EffectiveOperationalMax:0}";
		_powerLabel.Modulate = power?.IsOverheated == true
			? new Color(1f, 0.45f, 0.35f)
			: power is { CurrentPower: <= 0.5f }
				? new Color(1f, 0.7f, 0.35f)
				: new Color(0.75f, 0.85f, 1f);
	}

	private void ApplyHeatMeter(MechPowerHeat? power, float heatCap)
	{
		if (_heatBar == null)
			return;

		_heatBar.Value = power?.HeatRatio ?? 0f;
		_heatBar.AddThemeStyleboxOverride("fill", MakeBarFill(MixHeat(power?.HeatRatio ?? 0f)));
		if (_heatLabel == null)
			return;

		_heatLabel.Text = power == null
			? "HEAT"
			: $"HEAT {power.CurrentHeat:0}/{heatCap:0}";
		_heatLabel.Modulate = power?.IsOverheated == true
			? new Color(1f, 0.4f, 0.3f)
			: new Color(1f, 0.75f, 0.55f);
	}

	private void ApplySpeedMeter(MechController mech)
	{
		if (_speedBar == null || _speedLabel == null)
			return;

		var show = mech.IsSpeedGovernorActive;
		_speedBar.GetParent<Control>().Visible = show;
		if (!show)
			return;

		_speedBar.Value = mech.SpeedGovernor;
		_speedLabel.Text = $"SPD {mech.SpeedGovernor * 100f:0}%";
		_speedLabel.Modulate = new Color(0.65f, 0.95f, 0.7f);
	}

	private static Color MixHeat(float ratio)
	{
		if (ratio < 0.55f)
			return HeatFill.Lerp(new Color(0.95f, 0.75f, 0.25f), ratio / 0.55f);
		return new Color(0.95f, 0.75f, 0.25f).Lerp(EmptyColor, (ratio - 0.55f) / 0.45f);
	}
}
