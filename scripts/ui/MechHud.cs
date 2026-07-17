using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Bottom combat HUD: power/heat meters, integrity schematic, weapons + corps modules.
/// </summary>
public partial class MechHud : Control
{
	private readonly Dictionary<PartSlot, PanelContainer> _panels = new();
	private readonly Dictionary<PartSlot, Label> _labels = new();
	private PanelContainer? _hullPanel;
	private Label? _hullLabel;

	private ProgressBar? _powerBar;
	private ProgressBar? _heatBar;
	private Label? _powerLabel;
	private Label? _heatLabel;

	private readonly List<ModuleRow> _weaponRows = new();
	private readonly List<ModuleRow> _abilityRows = new();

	private static readonly Color FullColor = new(0.42f, 0.78f, 0.28f);
	private static readonly Color EmptyColor = new(0.82f, 0.18f, 0.16f);
	private static readonly Color DeadColor = new(0.22f, 0.2f, 0.2f);
	private static readonly Color MissingColor = new(0.28f, 0.3f, 0.32f);
	private static readonly Color PanelBg = new(0.06f, 0.08f, 0.11f, 0.92f);
	private static readonly Color PowerFill = new(0.35f, 0.7f, 1f);
	private static readonly Color HeatFill = new(0.95f, 0.45f, 0.2f);

	private sealed class ModuleRow
	{
		public Control Root = null!;
		public PanelContainer KeyPanel = null!;
		public Label KeyLabel = null!;
		public PanelContainer BodyPanel = null!;
		public Label BodyLabel = null!;
	}

	private const float BaseWidth = 580f;
	private const float BaseHeight = 360f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		ApplyLayout();
		GameSettings.Changed += ApplyLayout;
	}

	public override void _ExitTree()
	{
		GameSettings.Changed -= ApplyLayout;
	}

	/// <summary>Applies scale + screen position from <see cref="GameSettings"/>.</summary>
	public void ApplyLayout()
	{
		var scale = GameSettings.HudScale;
		Scale = new Vector2(scale, scale);
		PivotOffset = new Vector2(0f, BaseHeight);

		AnchorLeft = 0f;
		AnchorRight = 0f;
		AnchorTop = 1f;
		AnchorBottom = 1f;

		var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
		var scaledW = BaseWidth * scale;
		var maxX = Mathf.Max(0f, vp.X - scaledW - 16f);
		var maxLift = Mathf.Max(0f, vp.Y * 0.4f);
		var left = 12f + GameSettings.HudOffsetX * maxX;
		var bottom = 14f + GameSettings.HudOffsetY * maxLift;

		OffsetLeft = left;
		OffsetRight = left + BaseWidth;
		OffsetBottom = -bottom;
		OffsetTop = -bottom - BaseHeight;
		CustomMinimumSize = new Vector2(BaseWidth, BaseHeight);
		Size = CustomMinimumSize;
	}

	private void Build()
	{
		var root = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(BaseWidth, BaseHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};
		root.AddThemeConstantOverride("separation", 10);
		AddChild(root);
		CustomMinimumSize = root.CustomMinimumSize;
		Size = root.CustomMinimumSize;

		root.AddChild(BuildMetersColumn());
		root.AddChild(BuildSchematicColumn());
		root.AddChild(BuildModulesColumn());
	}

	private Control BuildMetersColumn()
	{
		var col = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(72, BaseHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};
		col.AddThemeConstantOverride("separation", 8);

		var meters = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		meters.AddThemeConstantOverride("separation", 8);
		col.AddChild(meters);

		(_powerBar, _powerLabel) = MakeVerticalMeter(meters, "PWR", PowerFill);
		(_heatBar, _heatLabel) = MakeVerticalMeter(meters, "HEAT", HeatFill);
		return col;
	}

	private static (ProgressBar bar, Label caption) MakeVerticalMeter(Control parent, string caption, Color fill)
	{
		var wrap = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		wrap.AddThemeConstantOverride("separation", 4);
		parent.AddChild(wrap);

		var bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 1,
			Value = 0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(28, 280),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bar.FillMode = (int)ProgressBar.FillModeEnum.BottomToTop;
		bar.CustomMinimumSize = new Vector2(28, 280);
		bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.14f, 0.16f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			ContentMarginLeft = 2,
			ContentMarginRight = 2,
			ContentMarginTop = 2,
			ContentMarginBottom = 2
		});
		bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = fill,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		});
		wrap.AddChild(bar);

		var label = new Label
		{
			Text = caption,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.75f, 0.8f, 0.85f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		wrap.AddChild(label);
		return (bar, label);
	}

	private Control BuildSchematicColumn()
	{
		var root = new Control
		{
			CustomMinimumSize = new Vector2(210, BaseHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};

		MakeSlot(root, PartSlot.Head, "Head", new Vector2(64, 48), new Vector2(73, 4));
		MakeSlot(root, PartSlot.WeaponL, "Arm L", new Vector2(52, 96), new Vector2(8, 60));
		MakeSlot(root, PartSlot.Torso, "Torso", new Vector2(88, 88), new Vector2(61, 60));
		MakeSlot(root, PartSlot.WeaponR, "Arm R", new Vector2(52, 96), new Vector2(150, 60));
		MakeSlot(root, PartSlot.Legs, "Legs", new Vector2(120, 72), new Vector2(45, 166));

		_hullPanel = MakeBlock(root, "Hull", new Vector2(210, 36), new Vector2(0, 300));
		_hullLabel = _hullPanel.GetNode<Label>("Label");
		return root;
	}

	private Control BuildModulesColumn()
	{
		var col = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(260, 0),
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			MouseFilter = MouseFilterEnum.Ignore
		};
		col.AddThemeConstantOverride("separation", 4);

		_weaponRows.Add(MakeModuleRow(col));
		_weaponRows.Add(MakeModuleRow(col));
		for (var i = 0; i < AbilityController.MaxAbilitySlots; i++)
			_abilityRows.Add(MakeModuleRow(col));

		return col;
	}

	private static ModuleRow MakeModuleRow(Control parent)
	{
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 4);
		parent.AddChild(row);

		var keyPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(40, 38),
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
			CustomMinimumSize = new Vector2(210, 38),
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

	private void MakeSlot(Control parent, PartSlot slot, string title, Vector2 size, Vector2 position)
	{
		var panel = MakeBlock(parent, title, size, position);
		_panels[slot] = panel;
		_labels[slot] = panel.GetNode<Label>("Label");
	}

	private static PanelContainer MakeBlock(Control parent, string title, Vector2 size, Vector2 position)
	{
		var panel = new PanelContainer
		{
			Position = position,
			CustomMinimumSize = size,
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", MakeStyle(FullColor));

		var label = new Label
		{
			Name = "Label",
			Text = $"{title}\n—/—",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", Colors.Black);
		panel.AddChild(label);
		parent.AddChild(panel);
		return panel;
	}

	private static StyleBoxFlat MakeStyle(Color color) => new()
	{
		BgColor = color,
		CornerRadiusTopLeft = 4,
		CornerRadiusTopRight = 4,
		CornerRadiusBottomRight = 4,
		CornerRadiusBottomLeft = 4,
		ContentMarginLeft = 4,
		ContentMarginTop = 3,
		ContentMarginRight = 4,
		ContentMarginBottom = 3
	};

	public void Refresh(MechController? mech)
	{
		if (mech == null)
			return;

		RefreshMeters(mech);
		RefreshSlot(mech, PartSlot.Head, "Head");
		RefreshSlot(mech, PartSlot.WeaponL, "Arm L");
		RefreshSlot(mech, PartSlot.Torso, "Torso");
		RefreshSlot(mech, PartSlot.WeaponR, "Arm R");
		RefreshSlot(mech, PartSlot.Legs, "Legs");
		RefreshHull(mech.Health);
		RefreshWeapons(mech);
		RefreshAbilities(mech);
	}

	private void RefreshMeters(MechController mech)
	{
		var power = mech.PowerHeat;
		var stats = mech.Assembler?.Stats;
		if (_powerBar != null)
		{
			_powerBar.Value = power?.LoadRatio ?? 0f;
			if (_powerLabel != null)
			{
				_powerLabel.Text = power == null
					? "PWR"
					: $"PWR\n{power.CurrentLoad:0}/{stats?.PowerCapacity ?? 0:0}";
				_powerLabel.Modulate = power?.IsOverheated == true
					? new Color(1f, 0.45f, 0.35f)
					: new Color(0.75f, 0.85f, 1f);
			}
		}

		if (_heatBar != null)
		{
			_heatBar.Value = power?.HeatRatio ?? 0f;
			var heatColor = MixHeat(power?.HeatRatio ?? 0f);
			_heatBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
			{
				BgColor = heatColor,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2,
				CornerRadiusBottomRight = 2,
				CornerRadiusBottomLeft = 2
			});
			if (_heatLabel != null)
			{
				_heatLabel.Text = power == null
					? "HEAT"
					: $"HEAT\n{power.CurrentHeat:0}/{stats?.HeatCap ?? 0:0}";
				_heatLabel.Modulate = power?.IsOverheated == true
					? new Color(1f, 0.4f, 0.3f)
					: new Color(1f, 0.75f, 0.55f);
			}
		}
	}

	private static Color MixHeat(float ratio)
	{
		if (ratio < 0.55f)
			return HeatFill.Lerp(new Color(0.95f, 0.75f, 0.25f), ratio / 0.55f);
		return new Color(0.95f, 0.75f, 0.25f).Lerp(EmptyColor, (ratio - 0.55f) / 0.45f);
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
			row.Root.Visible = false;
			return;
		}

		row.Root.Visible = true;
		var part = hp.EquippedPart;
		var status = hp.IsDestroyed ? "DOWN" : "READY";
		var details = $"{part.DisplayName}\nDMG {part.Damage:0}  RNG {part.Range:0}\nH {part.HeatPerShot:0} | P {part.PowerLoadWhileFiring:0} | {status}";
		SetModuleRow(row, details, hp.IsDestroyed ? DeadColor : new Color(0.14f, 0.18f, 0.22f));
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
		AbilityId.MissileSalvo => "Paint lock → salvo",
		AbilityId.MendPulse => "Paint → heal beacon",
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

	private void RefreshHull(Damageable? hull)
	{
		if (_hullPanel == null || _hullLabel == null)
			return;

		if (hull == null || hull.MaxHealth <= 0f)
		{
			SetBlock(_hullPanel, _hullLabel, "Hull\n—/—", MissingColor);
			return;
		}

		var cur = Mathf.Max(0f, hull.CurrentHealth);
		var max = hull.MaxHealth;
		var ratio = Mathf.Clamp(cur / max, 0f, 1f);
		var color = hull.IsDead ? DeadColor : MixHealth(ratio);
		SetBlock(_hullPanel, _hullLabel, $"Hull\n{Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}", color);
	}

	private void RefreshSlot(MechController mech, PartSlot slot, string title)
	{
		if (!_panels.TryGetValue(slot, out var panel) || !_labels.TryGetValue(slot, out var label))
			return;

		var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
		if (hp == null || hp.EquippedPart == null || hp.EquippedPart.VisualKind == "empty" || hp.MaxHp <= 0f)
		{
			SetBlock(panel, label, $"{title}\n—", MissingColor);
			return;
		}

		if (hp.IsDestroyed)
		{
			SetBlock(panel, label, $"{title}\nDOWN", DeadColor);
			return;
		}

		var cur = Mathf.Max(0f, hp.CurrentHp);
		var max = hp.MaxHp;
		var ratio = Mathf.Clamp(cur / max, 0f, 1f);
		var text = slot == PartSlot.Legs && hp.LimbCount > 1
			? $"{title}\n{Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}\n{hp.LimbsAlive}/{hp.LimbCount}"
			: $"{title}\n{Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}";
		SetBlock(panel, label, text, MixHealth(ratio));
	}

	private static Color MixHealth(float ratio)
	{
		if (ratio >= 0.55f)
			return FullColor.Lerp(new Color(0.88f, 0.78f, 0.2f), 1f - (ratio - 0.55f) / 0.45f);
		return new Color(0.88f, 0.78f, 0.2f).Lerp(EmptyColor, 1f - ratio / 0.55f);
	}

	private static void SetBlock(PanelContainer panel, Label label, string text, Color color)
	{
		panel.AddThemeStyleboxOverride("panel", MakeStyle(color));
		label.Text = text;
		label.AddThemeColorOverride("font_color", color.Luminance < 0.45f ? Colors.White : Colors.Black);
	}
}
