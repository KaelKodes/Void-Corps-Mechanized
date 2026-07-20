using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Bottom combat HUD: integrity schematic, weapons + MAP modules.
/// Power/heat meters live in the corner column or flank the player MAP (see GameSettings).
/// </summary>
public partial class MechHud : Control
{
	private IntegritySchematic? _schematic;
	private EnemyTargetSchematic? _enemySchematic;

	private Control? _rootRow;
	private Control? _powerColumn;
	private Control? _heatColumn;
	private Control? _speedColumn;
	private ProgressBar? _powerBar;
	private ProgressBar? _heatBar;
	private ProgressBar? _speedBar;
	private Label? _powerLabel;
	private Label? _heatLabel;
	private Label? _speedLabel;

	private Control? _flankRoot;
	private ProgressBar? _flankPowerBar;
	private ProgressBar? _flankHeatBar;
	private ProgressBar? _flankSpeedBar;
	private Label? _flankPowerLabel;
	private Label? _flankHeatLabel;
	private Label? _flankSpeedLabel;
	private MechController? _trackedMech;

	private readonly List<ModuleRow> _weaponRows = new();
	private readonly List<ModuleRow> _abilityRows = new();
	private CockpitDiegeticHud? _cockpitHud;
	private Control? _weaponModulesColumn;

	private static readonly Color EmptyColor = new(0.82f, 0.18f, 0.16f);
	private static readonly Color DeadColor = new(0.22f, 0.2f, 0.2f);
	private static readonly Color PowerFill = new(0.35f, 0.7f, 1f);
	private static readonly Color HeatFill = new(0.95f, 0.45f, 0.2f);
	private static readonly Color SpeedFill = new(0.45f, 0.9f, 0.55f);

	private const float MeterColumnWidth = 36f;
	private const float CornerMeterBarHeight = 200f;
	private const float FlankBarWidth = 16f;
	private const float FlankBarHeight = 100f;
	private const float FlankSideOffset = 64f;
	private const float FlankAlpha = 0.62f;

	private sealed class ModuleRow
	{
		public Control Root = null!;
		public PanelContainer KeyPanel = null!;
		public Label KeyLabel = null!;
		public PanelContainer BodyPanel = null!;
		public Label BodyLabel = null!;
	}

	private const float BaseWidth = 840f;
	private const float BaseHeight = 310f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = false;
		Build();
		ApplyLayout();
		GameSettings.Changed += ApplyLayout;
	}

	public override void _ExitTree()
	{
		GameSettings.Changed -= ApplyLayout;
		if (_flankRoot != null && GodotObject.IsInstanceValid(_flankRoot))
			_flankRoot.QueueFree();
		_flankRoot = null;
	}

	public override void _Notification(int what)
	{
		if (what != NotificationVisibilityChanged)
			return;

		SyncFlankVisibility();
		if (Visible)
			CallDeferred(MethodName.ApplyLayout);
	}

	public override void _Process(double delta)
	{
		if (!ShouldShowFlankMeters())
		{
			if (_flankRoot != null)
				_flankRoot.Visible = false;
			return;
		}

		UpdateFlankPositions(_trackedMech!);
	}

	/// <summary>Applies scale + screen position from <see cref="GameSettings"/>.</summary>
	public void ApplyLayout()
	{
		if (_rootRow == null)
		{
			if (!IsInsideTree())
				return;
			Build();
		}

		if (_rootRow == null)
			return;

		var beside = GameSettings.MetersBesideMech;
		SetCornerMeterVisible(_powerColumn, !beside);
		SetCornerMeterVisible(_heatColumn, !beside);
		// SPD only appears while the speed governor is below full — refreshed each frame.

		var width = BaseWidth;
		var scale = Mathf.Clamp(GameSettings.HudScale, 0.5f, 1.5f);

		Scale = new Vector2(scale, scale);
		PivotOffset = new Vector2(width * 0.5f, BaseHeight);

		// Bottom-anchored under CanvasLayer — same model as UI/Hint.
		AnchorLeft = 0f;
		AnchorRight = 0f;
		AnchorTop = 1f;
		AnchorBottom = 1f;

		var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
		if (vp.X < 32f || vp.Y < 32f)
			vp = new Vector2(1920, 1080);

		var scaledW = width * scale;
		var halfTravel = Mathf.Max(0f, (vp.X - scaledW) * 0.5f - 12f);
		var maxLift = Mathf.Max(0f, vp.Y * 0.4f);
		var centerLeft = vp.X * 0.5f - width * 0.5f;
		var left = centerLeft + (GameSettings.HudOffsetX - 0.5f) * 2f * halfTravel;
		var bottom = 14f + GameSettings.HudOffsetY * maxLift;

		OffsetLeft = left;
		OffsetRight = left + width;
		OffsetBottom = -bottom;
		OffsetTop = -bottom - BaseHeight;
		CustomMinimumSize = new Vector2(width, BaseHeight);
		Size = CustomMinimumSize;

		// FullRect AFTER parent size is known — ordering was why Reset "fixed" a dead layout.
		_rootRow.Scale = Vector2.One;
		_rootRow.PivotOffset = Vector2.Zero;
		_rootRow.CustomMinimumSize = new Vector2(width, BaseHeight);
		_rootRow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		if (_schematic != null)
		{
			_schematic.Visible = true;
			_schematic.CustomMinimumSize = new Vector2(210, 304);
		}

		if (_enemySchematic != null)
		{
			_enemySchematic.Visible = true;
			_enemySchematic.CustomMinimumSize = new Vector2(210, 304);
		}

		EnsureFlankOverlay();
		SyncFlankVisibility();
	}

	public void QueueApplyLayout() => CallDeferred(MethodName.ApplyLayout);

	private bool ShouldShowFlankMeters() =>
		Visible
		&& GameSettings.MetersBesideMech
		&& _trackedMech != null
		&& GetTree() is { Paused: false };

	private void SyncFlankVisibility()
	{
		if (_flankRoot == null || !GodotObject.IsInstanceValid(_flankRoot))
			return;
		_flankRoot.Visible = ShouldShowFlankMeters();
	}

	private void Build()
	{
		if (_rootRow != null)
			return;

		var root = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(BaseWidth, BaseHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("separation", 10);
		AddChild(root);
		_rootRow = root;
		CustomMinimumSize = root.CustomMinimumSize;
		Size = root.CustomMinimumSize;

		_powerColumn = BuildMeterColumn("PWR", PowerFill, out _powerBar, out _powerLabel);
		root.AddChild(_powerColumn);
		root.AddChild(BuildEnemySchematicColumn());
		root.AddChild(BuildSchematicColumn());
		_weaponModulesColumn = BuildModulesColumn();
		root.AddChild(_weaponModulesColumn);
		_heatColumn = BuildMeterColumn("HEAT", HeatFill, out _heatBar, out _heatLabel);
		root.AddChild(_heatColumn);
		_speedColumn = BuildMeterColumn("SPD", SpeedFill, out _speedBar, out _speedLabel);
		root.AddChild(_speedColumn);
		SetCornerMeterVisible(_speedColumn, false);
		EnsureFlankOverlay();
	}

	private Control BuildEnemySchematicColumn()
	{
		_enemySchematic = new EnemyTargetSchematic
		{
			CustomMinimumSize = new Vector2(210, 304),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			MouseFilter = MouseFilterEnum.Stop
		};
		return _enemySchematic;
	}
	private static void SetCornerMeterVisible(Control? column, bool visible)
	{
		if (column == null)
			return;
		column.Visible = visible;
		column.CustomMinimumSize = visible ? new Vector2(MeterColumnWidth, 0) : Vector2.Zero;
	}

	private static Control BuildMeterColumn(
		string caption,
		Color fill,
		out ProgressBar bar,
		out Label label)
	{
		var col = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(MeterColumnWidth, 0),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			MouseFilter = MouseFilterEnum.Ignore
		};

		(bar, label) = MakeVerticalMeter(
			col, caption, fill, new Vector2(28, CornerMeterBarHeight));
		return col;
	}

	private void EnsureFlankOverlay()
	{
		var parent = GetParent();
		if (parent == null)
			return;

		if (_flankRoot != null && GodotObject.IsInstanceValid(_flankRoot))
			return;

		_flankRoot = new Control
		{
			Name = "MechFlankMeters",
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
			// Stay with combat HUD; pause menu sits above via its own ZIndex.
			ZIndex = 0
		};
		_flankRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		parent.AddChild(_flankRoot);

		(_flankPowerBar, _flankPowerLabel) = MakeVerticalMeter(
			_flankRoot, "PWR", PowerFill, new Vector2(FlankBarWidth, FlankBarHeight), freePosition: true);
		(_flankHeatBar, _flankHeatLabel) = MakeVerticalMeter(
			_flankRoot, "HEAT", HeatFill, new Vector2(FlankBarWidth, FlankBarHeight), freePosition: true);
		(_flankSpeedBar, _flankSpeedLabel) = MakeVerticalMeter(
			_flankRoot, "SPD", SpeedFill, new Vector2(FlankBarWidth, FlankBarHeight), freePosition: true);
		if (_flankPowerBar?.GetParent() is Control powerWrap)
			powerWrap.Modulate = new Color(1f, 1f, 1f, FlankAlpha);
		if (_flankHeatBar?.GetParent() is Control heatWrap)
			heatWrap.Modulate = new Color(1f, 1f, 1f, FlankAlpha);
		if (_flankSpeedBar?.GetParent() is Control speedWrap)
		{
			speedWrap.Modulate = new Color(1f, 1f, 1f, FlankAlpha);
			speedWrap.Visible = false;
		}
	}

	private void UpdateFlankPositions(MechController mech)
	{
		if (_flankRoot == null || _flankPowerBar == null || _flankHeatBar == null)
			return;

		var cam = mech.GetViewport()?.GetCamera3D();
		if (cam == null || !GodotObject.IsInstanceValid(mech) || !mech.IsInsideTree())
		{
			_flankRoot.Visible = false;
			return;
		}

		var anchor = mech.GlobalPosition + Vector3.Up * 1.35f;
		if (cam.IsPositionBehind(anchor))
		{
			_flankRoot.Visible = false;
			return;
		}

		var screen = cam.UnprojectPosition(anchor);
		var vp = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
		if (screen.X < -80f || screen.Y < -80f || screen.X > vp.X + 80f || screen.Y > vp.Y + 80f)
		{
			_flankRoot.Visible = false;
			return;
		}

		_flankRoot.Visible = ShouldShowFlankMeters();
		PlaceFlankMeter(_flankPowerBar, _flankPowerLabel, screen + new Vector2(-FlankSideOffset, 0f));
		PlaceFlankMeter(_flankHeatBar, _flankHeatLabel, screen + new Vector2(FlankSideOffset, 0f));
		if (_flankSpeedBar != null && mech.IsSpeedGovernorActive)
		{
			if (_flankSpeedBar.GetParent() is Control speedWrap)
				speedWrap.Visible = true;
			PlaceFlankMeter(_flankSpeedBar, _flankSpeedLabel, screen + new Vector2(0f, FlankBarHeight * 0.65f));
		}
		else if (_flankSpeedBar?.GetParent() is Control hiddenWrap)
		{
			hiddenWrap.Visible = false;
		}
	}

	private static void PlaceFlankMeter(ProgressBar bar, Label? label, Vector2 center)
	{
		var wrap = bar.GetParent() as Control;
		if (wrap == null)
			return;

		var size = wrap.Size;
		if (size.X < 1f || size.Y < 1f)
			size = wrap.CustomMinimumSize;

		wrap.Position = center - size * 0.5f;
		if (label != null)
			label.Visible = true;
	}

	private static (ProgressBar bar, Label caption) MakeVerticalMeter(
		Control parent,
		string caption,
		Color fill,
		Vector2 barSize,
		bool freePosition = false)
	{
		var wrap = new VBoxContainer
		{
			SizeFlagsVertical = freePosition ? SizeFlags.ShrinkCenter : SizeFlags.ShrinkEnd,
			SizeFlagsHorizontal = freePosition ? SizeFlags.ShrinkCenter : SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = freePosition
				? new Vector2(barSize.X + 8f, barSize.Y + 28f)
				: new Vector2(barSize.X + 4f, barSize.Y + 28f)
		};
		wrap.AddThemeConstantOverride("separation", 4);
		parent.AddChild(wrap);

		var bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 1,
			Value = 0,
			ShowPercentage = false,
			CustomMinimumSize = barSize,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bar.FillMode = (int)ProgressBar.FillModeEnum.BottomToTop;
		bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.1f, 0.82f),
			BorderColor = new Color(0.55f, 0.45f, 0.25f, 0.75f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ContentMarginLeft = 2,
			ContentMarginRight = 2,
			ContentMarginTop = 2,
			ContentMarginBottom = 2
		});
		bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = fill,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		});
		wrap.AddChild(bar);

		var label = new Label
		{
			Text = caption,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.85f, 0.88f, 0.92f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", freePosition ? 12 : 11);
		wrap.AddChild(label);
		return (bar, label);
	}

	private Control BuildSchematicColumn()
	{
		_schematic = new IntegritySchematic
		{
			CustomMinimumSize = new Vector2(210, 304),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			MouseFilter = MouseFilterEnum.Ignore
		};
		return _schematic;
	}

	private Control BuildModulesColumn()
	{
		var frame = new PanelContainer
		{
			Name = "WeaponModules",
			CustomMinimumSize = new Vector2(330, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			MouseFilter = MouseFilterEnum.Ignore
		};
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.055f, 0.07f, 0.92f),
			BorderColor = new Color(0.55f, 0.45f, 0.28f, 0.85f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 8,
			ContentMarginTop = 8,
			ContentMarginRight = 8,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		});

		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 4);
		frame.AddChild(col);

		var header = new Label
		{
			Text = "// WEAPONS / MODULES",
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		header.AddThemeFontSizeOverride("font_size", 12);
		col.AddChild(header);

		_weaponRows.Add(MakeModuleRow(col));
		_weaponRows.Add(MakeModuleRow(col));
		for (var i = 0; i < AbilityController.MaxAbilitySlots; i++)
			_abilityRows.Add(MakeModuleRow(col));

		return frame;
	}

	private static ModuleRow MakeModuleRow(Control parent)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0, 52),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 4);
		parent.AddChild(row);

		var keyPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(44, 52),
			MouseFilter = MouseFilterEnum.Ignore
		};
		keyPanel.AddThemeStyleboxOverride("panel", MakeStyle(new Color(0.14f, 0.16f, 0.2f)));
		var keyLabel = new Label
		{
			Name = "Key",
			Text = "—",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		keyLabel.AddThemeFontSizeOverride("font_size", 11);
		keyPanel.AddChild(keyLabel);
		row.AddChild(keyPanel);

		var bodyPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(220, 52),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bodyPanel.AddThemeStyleboxOverride("panel", MakeStyle(new Color(0.12f, 0.14f, 0.17f)));
		var bodyLabel = new Label
		{
			Name = "Body",
			Text = "Empty",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bodyLabel.AddThemeFontSizeOverride("font_size", 10);
		bodyPanel.AddChild(bodyLabel);
		row.AddChild(bodyPanel);

		return new ModuleRow
		{
			Root = row,
			KeyPanel = keyPanel,
			KeyLabel = keyLabel,
			BodyPanel = bodyPanel,
			BodyLabel = bodyLabel
		};
	}

	private static StyleBoxFlat MakeStyle(Color color) => new()
	{
		BgColor = color,
		BorderColor = new Color(0.45f, 0.38f, 0.24f, 0.7f),
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomRight = 3,
		CornerRadiusBottomLeft = 3,
		ContentMarginLeft = 6,
		ContentMarginTop = 4,
		ContentMarginRight = 6,
		ContentMarginBottom = 4
	};

	public void Refresh(MechController? mech)
	{
		_trackedMech = mech;
		if (mech == null)
		{
			_cockpitHud?.ApplyActive(false);
			SyncDiegeticLayout(false);
			if (_flankRoot != null)
				_flankRoot.Visible = false;
			return;
		}

		var useDiegetic = IsFirstPersonHud(mech) && CockpitDiegeticHud.MechHasCockpitScreens(mech);
		EnsureCockpitHud(mech);
		_cockpitHud?.Refresh(mech, useDiegetic);
		SyncDiegeticLayout(useDiegetic);

		RefreshMeters(mech);
		_schematic?.Refresh(mech);
		_enemySchematic?.Refresh(mech);
		RefreshWeapons(mech);
		RefreshAbilities(mech);

		if (GameSettings.MetersBesideMech)
		{
			if (ShouldShowFlankMeters())
				UpdateFlankPositions(mech);
			else
				SyncFlankVisibility();
		}
	}

	private void RefreshMeters(MechController mech)
	{
		var power = mech.PowerHeat;
		var stats = mech.Assembler?.Stats;
		var beside = GameSettings.MetersBesideMech;

		ApplyPowerMeter(
			beside ? _flankPowerBar : _powerBar,
			beside ? _flankPowerLabel : _powerLabel,
			power);
		ApplyHeatMeter(
			beside ? _flankHeatBar : _heatBar,
			beside ? _flankHeatLabel : _heatLabel,
			power,
			stats?.HeatCap ?? 0f);
		ApplySpeedMeter(mech, beside);
	}

	private void ApplySpeedMeter(MechController mech, bool beside)
	{
		var show = mech.IsSpeedGovernorActive;
		var bar = beside ? _flankSpeedBar : _speedBar;
		var label = beside ? _flankSpeedLabel : _speedLabel;

		if (!beside)
			SetCornerMeterVisible(_speedColumn, show);
		else if (_flankSpeedBar?.GetParent() is Control wrap)
			wrap.Visible = show && ShouldShowFlankMeters();

		if (bar == null)
			return;

		bar.Value = mech.SpeedGovernor;
		if (label == null)
			return;

		label.Text = show
			? $"SPD\n{mech.SpeedGovernor * 100f:0}%"
			: "SPD";
		label.Modulate = new Color(0.65f, 0.95f, 0.7f);
	}

	private static void ApplyPowerMeter(ProgressBar? bar, Label? label, MechPowerHeat? power)
	{
		if (bar == null)
			return;

		bar.Value = power?.PowerRatio ?? 0f;
		if (label == null)
			return;

		label.Text = power == null
			? "PWR"
			: $"PWR\n{power.CurrentPower:0}/{power.EffectiveOperationalMax:0}";
		label.Modulate = power?.IsOverheated == true
			? new Color(1f, 0.45f, 0.35f)
			: power is { CurrentPower: <= 0.5f }
				? new Color(1f, 0.7f, 0.35f)
				: new Color(0.75f, 0.85f, 1f);
	}

	private void ApplyHeatMeter(ProgressBar? bar, Label? label, MechPowerHeat? power, float heatCap)
	{
		if (bar == null)
			return;

		bar.Value = power?.HeatRatio ?? 0f;
		var heatColor = MixHeat(power?.HeatRatio ?? 0f);
		bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = heatColor,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		});
		if (label == null)
			return;

		label.Text = power == null
			? "HEAT"
			: $"HEAT\n{power.CurrentHeat:0}/{heatCap:0}";
		label.Modulate = power?.IsOverheated == true
			? new Color(1f, 0.4f, 0.3f)
			: new Color(1f, 0.75f, 0.55f);
	}

	private static Color MixHeat(float ratio)
	{
		if (ratio < 0.55f)
			return HeatFill.Lerp(new Color(0.95f, 0.75f, 0.25f), ratio / 0.55f);
		return new Color(0.95f, 0.75f, 0.25f).Lerp(EmptyColor, (ratio - 0.55f) / 0.45f);
	}

	private static bool IsFirstPersonHud(MechController mech) =>
		mech.GetViewport()?.GetCamera3D() is TopDownCamera { IsFirstPerson: true };

	private void EnsureCockpitHud(MechController mech)
	{
		if (_cockpitHud != null && GodotObject.IsInstanceValid(_cockpitHud))
			return;

		var host = GetParent();
		if (host == null)
			return;

		_cockpitHud = new CockpitDiegeticHud { Name = "CockpitDiegeticHud" };
		host.AddChild(_cockpitHud);
	}

	private void SyncDiegeticLayout(bool useDiegetic)
	{
		var hidePanels = useDiegetic && _cockpitHud is { HasScreens: true };
		if (_enemySchematic != null)
			_enemySchematic.Visible = !hidePanels;
		if (_schematic != null)
			_schematic.Visible = !hidePanels;
		if (_weaponModulesColumn != null)
			_weaponModulesColumn.Visible = !hidePanels;

		if (_rootRow == null)
			return;

		_rootRow.Visible = !(hidePanels && GameSettings.MetersBesideMech);
	}

	private void RefreshWeapons(MechController mech)
	{
		RefreshWeaponRow(0, mech, PartSlot.WeaponL, "fire_primary");
		RefreshWeaponRow(1, mech, PartSlot.WeaponR, "fire_secondary");
	}

	private void RefreshWeaponRow(int index, MechController mech, PartSlot slot, string action)
	{
		if (index >= _weaponRows.Count)
			return;
		var row = _weaponRows[index];
		row.KeyLabel.Text = ShortBind(action);
		var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
		if (hp?.EquippedPart == null || hp.EquippedPart.VisualKind == "empty")
		{
			row.Root.Visible = true;
			SetModuleRow(row, "Empty mount", new Color(0.1f, 0.11f, 0.13f));
			return;
		}

		row.Root.Visible = true;
		var part = hp.EquippedPart;
		string status;
		string details;
		if (part.IsHeldShield)
		{
			if (hp.IsDestroyed)
				status = "DOWN";
			else if (mech.IsHeldShieldBroken(slot))
				status = "BROKEN";
			else if (mech.IsHeldShieldRaised(slot))
				status = "RAISED";
			else
				status = "READY";
			details =
				$"{part.DisplayName}\nSHIELD  ARC {part.ShieldArcDegrees:0}°\nP {part.ShieldPowerPerSec:0}/s | {status}";
		}
		else if (part.WeaponFamily == WeaponFamily.Melee)
		{
			status = hp.IsDestroyed ? "DOWN" : "READY";
			details =
				$"{part.DisplayName}\nDMG {part.Damage:0}  REACH {part.Range:0.0}\nCONTACT H {part.HeatPerShot:0} | {status}";
		}
		else
		{
			status = hp.IsDestroyed ? "DOWN" : "READY";
			var elev = !IsFirstPersonHud(mech)
			           && part.AllowsFireElevation
			           && Mathf.Abs(mech.FireElevationNormalized) > 0.02f
				? $"  ELV {(mech.FireElevationPitchRadians >= 0f ? "+" : "")}{Mathf.RadToDeg(mech.FireElevationPitchRadians):0}°"
				: "";
			details =
				$"{part.DisplayName}\nDMG {part.Damage:0}  RNG {part.Range:0}{elev}\nH {part.HeatPerShot:0} | P {part.PowerPerShot:0} | {status}";
		}

		var bg = hp.IsDestroyed || (part.IsHeldShield && mech.IsHeldShieldBroken(slot))
			? DeadColor
			: part.IsHeldShield && mech.IsHeldShieldRaised(slot)
				? new Color(0.16f, 0.28f, 0.34f)
				: new Color(0.14f, 0.18f, 0.22f);
		SetModuleRow(row, details, bg);
	}

	private void RefreshAbilities(MechController mech)
	{
		var abilities = mech.Abilities;
		for (var i = 0; i < _abilityRows.Count; i++)
		{
			var row = _abilityRows[i];
			if (abilities == null || i >= abilities.BoundAbilities.Count)
			{
				row.Root.Visible = false;
				continue;
			}

			row.Root.Visible = true;
			row.KeyLabel.Text = ShortBind($"ability_{i + 1}");
			var part = abilities.BoundAbilities[i];
			var cd = abilities.GetCooldownRemaining(i);
			var ready = cd <= 0.05f ? "READY" : $"{cd:0.0}s";
			if (abilities.IsPulseRepairing && abilities.IsPulseRepairAbility(i))
				ready = "PULSE";
			var details =
				$"{part.DisplayName}\n{AbilityBlurb(part)}\nH {part.AbilityHeatBurst:0} | P {part.AbilityPowerLoad:0} | {ready}";
			SetModuleRow(row, details, cd <= 0.05f
				? new Color(0.14f, 0.22f, 0.18f)
				: new Color(0.2f, 0.16f, 0.14f));
		}
	}

	private static string AbilityBlurb(PartData part) => part.AbilityId switch
	{
		AbilityId.MissileSalvo => part.MissileGuidance switch
		{
			MissileGuidanceMode.SensorVision => "TAB lock · needs vision",
			MissileGuidanceMode.SensorContact => "TAB lock · scanner track",
			_ => "Paint lock → salvo"
		},
		AbilityId.MendPulse => "Paint beacon · Ctrl self",
		AbilityId.PulseRepair => "Hold channel repair",
		AbilityId.Shroud => "Cloak pulse",
		_ => "Module"
	};

	private static string ShortBind(string action)
	{
		var raw = InputBindings.FormatAction(action);
		if (string.IsNullOrEmpty(raw) || raw == "—" || raw == "Unbound")
			return "—";
		// Prefer first binding, strip Mouse prefix noise.
		var first = raw.Split(',')[0].Trim();
		if (first.StartsWith("Mouse "))
			return first.Replace("Mouse ", "M");
		if (first.Length > 5)
			return first[..5];
		return first;
	}

	private static void SetModuleRow(ModuleRow row, string text, Color bodyColor)
	{
		row.BodyPanel.AddThemeStyleboxOverride("panel", MakeStyle(bodyColor));
		row.BodyLabel.Text = text;
		row.BodyLabel.AddThemeColorOverride("font_color", Colors.White);
	}
}
