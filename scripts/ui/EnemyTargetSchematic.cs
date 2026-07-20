using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Sensor target dossier — enemy integrity silhouette with clickable focus bands
/// for TAB-lock aim assist (Legs / Torso / Head / arms).
/// </summary>
public partial class EnemyTargetSchematic : Control
{
	private sealed class Zone
	{
		public required PartSlot Slot;
		public required PanelContainer Plate;
		public required ColorRect Fill;
		public required Label TitleLabel;
		public required Label ValueLabel;
		public required PanelContainer FocusFrame;
		public required float PlateHeight;
		public required Button Hit;
	}

	private const float ContentWidth = 190f;
	private const float ContentHeight = 268f;

	private static readonly Color PlateBg = new(0.05f, 0.07f, 0.09f, 0.94f);
	private static readonly Color PlateBorder = new(0.35f, 0.55f, 0.62f, 0.9f);
	private static readonly Color FillHealthy = new(0.38f, 0.72f, 0.42f, 0.88f);
	private static readonly Color FillWarn = new(0.9f, 0.72f, 0.28f, 0.9f);
	private static readonly Color FillCritical = new(0.88f, 0.32f, 0.26f, 0.92f);
	private static readonly Color FillDead = new(0.18f, 0.16f, 0.16f, 0.95f);
	private static readonly Color FillMissing = new(0.14f, 0.16f, 0.18f, 0.7f);
	private static readonly Color FocusColor = new(0.95f, 0.72f, 0.28f, 0.95f);
	private static readonly Color LockColor = new(0.45f, 0.82f, 0.95f, 0.95f);

	private readonly Dictionary<PartSlot, Zone> _zones = new();
	private Label? _header;
	private Label? _sub;
	private Label? _hint;
	private float _pulse;
	private MechController? _pilot;

	[Signal] public delegate void FocusRequestedEventHandler(int slot);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = new Vector2(ContentWidth + 20f, ContentHeight + 18f);
		Size = CustomMinimumSize;
		Build();
	}

	public override void _Process(double delta)
	{
		_pulse += (float)delta * 4.2f;
		var pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_pulse));
		foreach (var zone in _zones.Values)
		{
			if (!zone.FocusFrame.Visible)
				continue;
			zone.FocusFrame.Modulate = new Color(1f, 1f, 1f, pulse);
		}
	}

	public void BindPilot(MechController? pilot) => _pilot = pilot;

	public void Refresh(MechController? pilot)
	{
		_pilot = pilot;
		var target = pilot?.SensorLockTarget;
		var focus = pilot?.SensorFocusSlot;

		if (_header != null)
		{
			_header.Text = target == null ? "// SENSORS" : $"LOCK  ·  {Callsign(target)}";
			_header.Modulate = target == null ? MechUiTheme.Accent : LockColor;
		}

		if (_sub != null)
		{
			_sub.Text = target == null
				? "TAB acquire contact"
				: pilot!.SensorLockInVision
					? "TRACKING  ·  click band to focus AI"
					: "CONTACT  ·  outside vision cone";
			_sub.Modulate = target != null && pilot!.SensorLockInVision
				? LockColor
				: MechUiTheme.Muted;
		}

		if (_hint != null)
		{
			_hint.Text = target == null
				? $"{InputBindings.FormatAction("target_next")} cycle  ·  {InputBindings.FormatAction("target_clear")} clear"
				: focus.HasValue
					? $"FOCUS  {ShortSlot(focus.Value)}  ·  {InputBindings.FormatAction("target_focus_cycle")} cycle band"
					: $"Select a band  ·  {InputBindings.FormatAction("target_focus_cycle")}";
		}

		foreach (var slot in _zones.Keys)
			RefreshZone(target, slot, focus);
	}

	private void Build()
	{
		var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		frame.CustomMinimumSize = new Vector2(ContentWidth + 20f, ContentHeight + 18f);
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.055f, 0.07f, 0.92f),
			BorderColor = PlateBorder,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 10,
			ContentMarginTop = 8,
			ContentMarginRight = 10,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		});
		AddChild(frame);

		var inner = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(ContentWidth, ContentHeight)
		};
		frame.AddChild(inner);

		_header = new Label
		{
			Text = "// SENSORS",
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_header.AddThemeFontSizeOverride("font_size", 12);
		_header.Position = Vector2.Zero;
		_header.Size = new Vector2(ContentWidth, 16);
		inner.AddChild(_header);

		_sub = new Label
		{
			Text = "TAB acquire contact",
			Modulate = MechUiTheme.Muted,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_sub.AddThemeFontSizeOverride("font_size", 10);
		_sub.Position = new Vector2(0, 16);
		_sub.Size = new Vector2(ContentWidth, 14);
		inner.AddChild(_sub);

		const float torsoW = 78f;
		const float torsoX = 56f;
		const float centerX = torsoX + torsoW * 0.5f;
		const float headW = 62f;
		const float legsW = 78f;
		const float y0 = 36f;

		AddZone(inner, PartSlot.Head, "HEAD", new Vector2(headW, 36), new Vector2(centerX - headW * 0.5f, y0));
		AddZone(inner, PartSlot.WeaponL, "L ARM", new Vector2(42, 88), new Vector2(6, y0 + 44));
		AddZone(inner, PartSlot.Torso, "TORSO", new Vector2(torsoW, 78), new Vector2(torsoX, y0 + 44));
		AddZone(inner, PartSlot.WeaponR, "R ARM", new Vector2(42, 88), new Vector2(142, y0 + 44));
		AddZone(inner, PartSlot.Legs, "LEGS", new Vector2(legsW, 64), new Vector2(centerX - legsW * 0.5f, y0 + 132));

		_hint = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_hint.AddThemeFontSizeOverride("font_size", 10);
		_hint.Position = new Vector2(0, ContentHeight - 28);
		_hint.Size = new Vector2(ContentWidth, 28);
		inner.AddChild(_hint);
	}

	private void AddZone(Control parent, PartSlot slot, string title, Vector2 size, Vector2 position)
	{
		var plate = new PanelContainer
		{
			Position = position,
			CustomMinimumSize = size,
			Size = size,
			MouseFilter = MouseFilterEnum.Stop,
			ClipContents = true
		};
		plate.AddThemeStyleboxOverride("panel", MakePlateStyle(PlateBorder));
		parent.AddChild(plate);

		var stack = new Control { MouseFilter = MouseFilterEnum.Ignore };
		stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		plate.AddChild(stack);

		var fill = new ColorRect
		{
			Color = FillMissing,
			MouseFilter = MouseFilterEnum.Ignore
		};
		fill.AnchorLeft = 0f;
		fill.AnchorRight = 1f;
		fill.AnchorTop = 1f;
		fill.AnchorBottom = 1f;
		fill.OffsetLeft = 2;
		fill.OffsetRight = -2;
		fill.OffsetTop = -(size.Y - 4);
		fill.OffsetBottom = -2;
		stack.AddChild(fill);

		var titleLabel = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ApplyOutlinedLabel(titleLabel, 10);
		titleLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		titleLabel.OffsetTop = 4;
		titleLabel.OffsetBottom = 18;
		stack.AddChild(titleLabel);

		var valueLabel = new Label
		{
			Text = "—",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ApplyOutlinedLabel(valueLabel, 11);
		valueLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		valueLabel.OffsetTop = -22;
		valueLabel.OffsetBottom = -4;
		stack.AddChild(valueLabel);

		var focusFrame = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		focusFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		focusFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = FocusColor,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		});
		stack.AddChild(focusFrame);

		var hit = new Button
		{
			Flat = true,
			MouseFilter = MouseFilterEnum.Stop,
			FocusMode = FocusModeEnum.None
		};
		hit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		hit.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		hit.AddThemeStyleboxOverride("hover", new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.06f)
		});
		hit.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.1f)
		});
		var captured = slot;
		hit.Pressed += () =>
		{
			SfxService.Click();
			EmitSignal(SignalName.FocusRequested, (int)captured);
			_pilot?.SetSensorFocus(captured);
		};
		stack.AddChild(hit);

		_zones[slot] = new Zone
		{
			Slot = slot,
			Plate = plate,
			Fill = fill,
			TitleLabel = titleLabel,
			ValueLabel = valueLabel,
			FocusFrame = focusFrame,
			PlateHeight = size.Y,
			Hit = hit
		};
	}

	private void RefreshZone(MechController? target, PartSlot slot, PartSlot? focus)
	{
		if (!_zones.TryGetValue(slot, out var zone))
			return;

		zone.Hit.Disabled = target == null;
		zone.FocusFrame.Visible = target != null && focus == slot;

		if (target == null)
		{
			zone.Fill.Color = FillMissing;
			SetFillHeight(zone, 1f);
			zone.ValueLabel.Text = "—";
			zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(new Color(0.3f, 0.35f, 0.4f, 0.7f)));
			return;
		}

		var hp = target.Assembler?.Hardpoints.GetValueOrDefault(slot);
		if (hp == null || hp.EquippedPart == null || hp.EquippedPart.VisualKind == "empty" || hp.MaxHp <= 0f)
		{
			zone.Fill.Color = FillMissing;
			SetFillHeight(zone, 1f);
			zone.ValueLabel.Text = "—";
			zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(new Color(0.35f, 0.38f, 0.42f, 0.7f)));
			return;
		}

		if (hp.IsDestroyed)
		{
			zone.Fill.Color = FillDead;
			SetFillHeight(zone, 1f);
			zone.ValueLabel.Text = "DOWN";
			zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(FillCritical));
			return;
		}

		var cur = Mathf.Max(0f, hp.CurrentHp);
		var max = Mathf.Max(1f, hp.MaxHp);
		var ratio = Mathf.Clamp(cur / max, 0f, 1f);
		var focused = focus == slot;
		zone.Fill.Color = MixFill(ratio);
		SetFillHeight(zone, ratio);
		zone.ValueLabel.Text = $"{Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}";
		zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(
			focused ? FocusColor : ratio < 0.35f ? FillCritical : PlateBorder));
	}

	private static string Callsign(MechController mech)
	{
		if (mech.ChassisClass == MechChassisClass.Titan)
			return "TITAN";
		var name = mech.Name.ToString();
		if (name.StartsWith("Enemy", System.StringComparison.Ordinal))
			return name.ToUpperInvariant();
		return name.Length <= 14 ? name.ToUpperInvariant() : name[..14].ToUpperInvariant();
	}

	private static void SetFillHeight(Zone zone, float ratio)
	{
		var h = Mathf.Max(4f, (zone.PlateHeight - 4f) * Mathf.Clamp(ratio, 0f, 1f));
		zone.Fill.OffsetTop = -h;
		zone.Fill.OffsetBottom = -2;
	}

	private static void ApplyOutlinedLabel(Label label, int fontSize)
	{
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Colors.Black);
		label.AddThemeColorOverride("font_outline_color", Colors.White);
		label.AddThemeConstantOverride("outline_size", 3);
		label.Modulate = Colors.White;
	}

	private static Color MixFill(float ratio)
	{
		if (ratio >= 0.55f)
			return FillHealthy.Lerp(FillWarn, 1f - (ratio - 0.55f) / 0.45f);
		return FillWarn.Lerp(FillCritical, 1f - ratio / 0.55f);
	}

	private static StyleBoxFlat MakePlateStyle(Color border) => new()
	{
		BgColor = PlateBg,
		BorderColor = border,
		BorderWidthLeft = 2,
		BorderWidthTop = 2,
		BorderWidthRight = 2,
		BorderWidthBottom = 2,
		CornerRadiusTopLeft = 2,
		CornerRadiusTopRight = 2,
		CornerRadiusBottomRight = 2,
		CornerRadiusBottomLeft = 2
	};

	private static string ShortSlot(PartSlot slot) => slot switch
	{
		PartSlot.Head => "HEAD",
		PartSlot.WeaponL => "L ARM",
		PartSlot.WeaponR => "R ARM",
		PartSlot.Torso => "TORSO",
		PartSlot.Legs => "LEGS",
		_ => slot.ToString().ToUpperInvariant()
	};
}
