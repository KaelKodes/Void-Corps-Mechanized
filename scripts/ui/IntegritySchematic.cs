using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Combat integrity schematic — MAP silhouette plates with health fill and aim lock.
/// Sized to content (not stretched) so it does not swallow the weapon column.
/// </summary>
public partial class IntegritySchematic : Control
{
	private sealed class Zone
	{
		public required PartSlot Slot;
		public required PanelContainer Plate;
		public required ColorRect Fill;
		public required ColorRect ArmorPip;
		public required Label TitleLabel;
		public required Label ValueLabel;
		public required PanelContainer LockFrame;
		public required HBoxContainer LimbPips;
		public required float PlateHeight;
	}

	private const float ContentWidth = 190f;
	private const float ContentHeight = 286f;
	private const float UtilSlotSize = 44f;
	private const float UtilRowY = 230f;

	private static readonly PartSlot[] UtilitySlots =
	[
		PartSlot.ShoulderL,
		PartSlot.ShoulderR,
		PartSlot.Backpack,
		PartSlot.Systems
	];

	private static readonly Color PlateBg = new(0.05f, 0.07f, 0.09f, 0.94f);
	private static readonly Color PlateBorder = new(0.55f, 0.45f, 0.28f, 0.85f);
	private static readonly Color FillHealthy = new(0.38f, 0.72f, 0.42f, 0.88f);
	private static readonly Color FillWarn = new(0.9f, 0.72f, 0.28f, 0.9f);
	private static readonly Color FillCritical = new(0.88f, 0.32f, 0.26f, 0.92f);
	private static readonly Color FillDead = new(0.18f, 0.16f, 0.16f, 0.95f);
	private static readonly Color FillMissing = new(0.14f, 0.16f, 0.18f, 0.7f);
	private static readonly Color LockColor = new(0.45f, 0.82f, 0.95f, 0.95f);

	private readonly Dictionary<PartSlot, Zone> _zones = new();
	private Label? _header;
	private float _pulse;
	private readonly List<PartSlot> _visibleUtilities = new();

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
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
			if (!zone.LockFrame.Visible)
				continue;
			zone.LockFrame.Modulate = new Color(1f, 1f, 1f, pulse);
		}
	}

	public void Refresh(MechController? mech)
	{
		if (mech == null)
			return;

		var aimed = mech.AimedComponentSlot;
		RefreshZone(mech, PartSlot.Head, aimed);
		RefreshZone(mech, PartSlot.WeaponL, aimed);
		RefreshZone(mech, PartSlot.Torso, aimed);
		RefreshZone(mech, PartSlot.WeaponR, aimed);
		RefreshZone(mech, PartSlot.Legs, aimed);
		RefreshUtilityRow(mech, aimed);

		if (_header == null)
			return;

		_header.Text = aimed.HasValue
			? $"LOCK  ·  {ShortSlot(aimed.Value)}"
			: "// INTEGRITY";
		_header.Modulate = aimed.HasValue ? LockColor : MechUiTheme.Accent;
	}

	private void Build()
	{
		var frame = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
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
			Text = "// INTEGRITY",
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_header.AddThemeFontSizeOverride("font_size", 12);
		_header.Position = Vector2.Zero;
		_header.Size = new Vector2(ContentWidth, 18);
		inner.AddChild(_header);

		// Silhouette layout inside content box.
		// Torso center X = 95. Head/legs share that center.
		const float torsoW = 78f;
		const float torsoX = 56f;
		const float centerX = torsoX + torsoW * 0.5f; // 95
		const float headW = 62f;
		const float legsW = 78f;

		AddZone(inner, PartSlot.Head, "HEAD", new Vector2(headW, 36), new Vector2(centerX - headW * 0.5f, 26));
		AddZone(inner, PartSlot.WeaponL, "L ARM", new Vector2(42, 88), new Vector2(6, 70));
		AddZone(inner, PartSlot.Torso, "TORSO", new Vector2(torsoW, 78), new Vector2(torsoX, 70));
		AddZone(inner, PartSlot.WeaponR, "R ARM", new Vector2(42, 88), new Vector2(142, 70));
		AddZone(inner, PartSlot.Legs, "LEGS", new Vector2(legsW, 64), new Vector2(centerX - legsW * 0.5f, 158));

		// Utility strip inside the same brass frame — equipped mountables that can take damage.
		const float gap = 6f;
		var utilCount = UtilitySlots.Length;
		var rowW = utilCount * UtilSlotSize + (utilCount - 1) * gap;
		var utilX = (ContentWidth - rowW) * 0.5f;
		for (var i = 0; i < utilCount; i++)
		{
			var slot = UtilitySlots[i];
			AddZone(
				inner,
				slot,
				ShortSlot(slot),
				new Vector2(UtilSlotSize, UtilSlotSize),
				new Vector2(utilX + i * (UtilSlotSize + gap), UtilRowY));
		}
	}

	private void AddZone(Control parent, PartSlot slot, string title, Vector2 size, Vector2 position)
	{
		var plate = new PanelContainer
		{
			Position = position,
			CustomMinimumSize = size,
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore,
			ClipContents = true
		};
		plate.AddThemeStyleboxOverride("panel", MakePlateStyle(PlateBorder));
		parent.AddChild(plate);

		var stack = new Control { MouseFilter = MouseFilterEnum.Ignore };
		stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		plate.AddChild(stack);

		var fill = new ColorRect
		{
			Color = FillHealthy,
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

		var armorPip = new ColorRect
		{
			Color = new Color(0.7f, 0.78f, 0.88f, 0.4f),
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		armorPip.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		armorPip.OffsetLeft = 3;
		armorPip.OffsetRight = -3;
		armorPip.OffsetBottom = 3;
		stack.AddChild(armorPip);

		var titleLabel = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ApplyOutlinedLabel(titleLabel, size.Y <= UtilSlotSize + 0.5f ? 8 : 10);
		titleLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		titleLabel.OffsetTop = size.Y <= UtilSlotSize + 0.5f ? 2 : 4;
		titleLabel.OffsetBottom = size.Y <= UtilSlotSize + 0.5f ? 14 : 18;
		stack.AddChild(titleLabel);

		var valueLabel = new Label
		{
			Text = "—",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ApplyOutlinedLabel(valueLabel, size.Y <= UtilSlotSize + 0.5f ? 9 : 11);
		valueLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		valueLabel.OffsetTop = size.Y <= UtilSlotSize + 0.5f ? -18 : -22;
		valueLabel.OffsetBottom = size.Y <= UtilSlotSize + 0.5f ? -2 : -4;
		stack.AddChild(valueLabel);

		var limbPips = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		limbPips.AddThemeConstantOverride("separation", 3);
		limbPips.AnchorLeft = 0.5f;
		limbPips.AnchorRight = 0.5f;
		limbPips.AnchorTop = 1f;
		limbPips.AnchorBottom = 1f;
		limbPips.OffsetLeft = -36;
		limbPips.OffsetRight = 36;
		limbPips.OffsetTop = -38;
		limbPips.OffsetBottom = -28;
		stack.AddChild(limbPips);

		var lockFrame = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		lockFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		lockFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = LockColor,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		});
		stack.AddChild(lockFrame);

		_zones[slot] = new Zone
		{
			Slot = slot,
			Plate = plate,
			Fill = fill,
			ArmorPip = armorPip,
			TitleLabel = titleLabel,
			ValueLabel = valueLabel,
			LockFrame = lockFrame,
			LimbPips = limbPips,
			PlateHeight = size.Y
		};
	}

	private void RefreshZone(MechController mech, PartSlot slot, PartSlot? aimed)
	{
		if (!_zones.TryGetValue(slot, out var zone))
			return;

		zone.LockFrame.Visible = aimed == slot;

		var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
		if (hp == null || hp.EquippedPart == null || hp.EquippedPart.VisualKind == "empty" || hp.MaxHp <= 0f)
		{
			zone.Fill.Color = FillMissing;
			SetFillHeight(zone, 1f);
			zone.ValueLabel.Text = "—";
			zone.ArmorPip.Visible = false;
			zone.LimbPips.Visible = false;
			zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(new Color(0.35f, 0.38f, 0.42f, 0.7f)));
			return;
		}

		if (hp.IsDestroyed)
		{
			zone.Fill.Color = FillDead;
			SetFillHeight(zone, 1f);
			zone.ValueLabel.Text = "DOWN";
			zone.ArmorPip.Visible = false;
			zone.LimbPips.Visible = false;
			zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(FillCritical));
			return;
		}

		var cur = Mathf.Max(0f, hp.CurrentHp);
		var max = Mathf.Max(1f, hp.MaxHp);
		var ratio = Mathf.Clamp(cur / max, 0f, 1f);
		var locked = aimed == slot;
		zone.Fill.Color = MixFill(ratio);
		SetFillHeight(zone, ratio);
		zone.ValueLabel.Text = $"{Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}";
		zone.ArmorPip.Visible = hp.ComponentArmor > 0.5f;
		zone.ArmorPip.Color = new Color(0.7f, 0.78f, 0.88f, Mathf.Clamp(hp.ComponentArmor / 40f, 0.2f, 0.55f));
		zone.Plate.AddThemeStyleboxOverride("panel", MakePlateStyle(
			locked ? LockColor : ratio < 0.35f ? FillCritical : PlateBorder));

		zone.LimbPips.Visible = false;
	}

	private void RefreshUtilityRow(MechController mech, PartSlot? aimed)
	{
		_visibleUtilities.Clear();
		foreach (var slot in UtilitySlots)
		{
			var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
			var equipped = hp?.EquippedPart != null
				&& hp.EquippedPart.VisualKind != "empty"
				&& hp.MaxHp > 0f;
			if (equipped || SlotMountOpen(mech, slot))
				_visibleUtilities.Add(slot);
		}

		const float gap = 6f;
		var count = _visibleUtilities.Count;
		var rowW = count <= 0 ? 0f : count * UtilSlotSize + (count - 1) * gap;
		var utilX = (ContentWidth - rowW) * 0.5f;

		foreach (var slot in UtilitySlots)
		{
			if (!_zones.TryGetValue(slot, out var zone))
				continue;

			var index = _visibleUtilities.IndexOf(slot);
			zone.Plate.Visible = index >= 0;
			if (index < 0)
				continue;

			zone.Plate.Position = new Vector2(utilX + index * (UtilSlotSize + gap), UtilRowY);
			RefreshZone(mech, slot, aimed);

			var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
			var equipped = hp?.EquippedPart != null
				&& hp.EquippedPart.VisualKind != "empty"
				&& hp.MaxHp > 0f;
			if (!equipped)
			{
				zone.TitleLabel.Text = ShortSlot(slot);
				zone.ValueLabel.Text = "";
			}
		}
	}

	private static bool SlotMountOpen(MechController mech, PartSlot slot)
	{
		var stats = mech.Assembler?.Stats;
		if (stats == null)
			return false;

		return slot switch
		{
			PartSlot.ShoulderL => stats.ShoulderMounts >= 1,
			PartSlot.ShoulderR => stats.ShoulderMounts >= 2,
			PartSlot.Backpack => stats.BackMounts >= 1,
			PartSlot.Systems => true,
			_ => false
		};
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
		PartSlot.ShoulderL => "L SH",
		PartSlot.ShoulderR => "R SH",
		PartSlot.Backpack => "BACK",
		PartSlot.Systems => "SYS",
		PartSlot.PowerCore => "CORE",
		_ => slot.ToString().ToUpperInvariant()
	};
}
