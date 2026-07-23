using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Bottom combat HUD: integrity schematic (with on-panel SPD), weapons + MAP modules.
/// Chassis HEAT warning lives under the aim crosshair (60%+); heat + power meters are on the cockpit glass.
/// </summary>
public partial class MechHud : Control
{
	private IntegritySchematic? _schematic;
	private EnemyTargetSchematic? _enemySchematic;

	private Control? _rootRow;
	private readonly List<ModuleRow> _weaponRows = new();
	private readonly List<ModuleRow> _abilityRows = new();
	private CockpitDiegeticHud? _cockpitHud;
	private Control? _weaponModulesColumn;

	private static readonly Color DeadColor = new(0.22f, 0.2f, 0.2f);

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
	}

	public override void _Notification(int what)
	{
		if (what != NotificationVisibilityChanged)
			return;

		if (Visible)
			CallDeferred(MethodName.ApplyLayout);
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

		var width = BaseWidth;
		var scale = Mathf.Clamp(GameSettings.HudScale, 0.5f, 1.5f);

		Scale = new Vector2(scale, scale);
		PivotOffset = new Vector2(width * 0.5f, BaseHeight);

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

		_rootRow.Scale = Vector2.One;
		_rootRow.PivotOffset = Vector2.Zero;
		_rootRow.CustomMinimumSize = new Vector2(width, BaseHeight);
		_rootRow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		if (_schematic != null)
		{
			_schematic.Visible = true;
			_schematic.CustomMinimumSize = new Vector2(280, 304);
		}

		if (_enemySchematic != null)
		{
			_enemySchematic.Visible = true;
			_enemySchematic.CustomMinimumSize = new Vector2(210, 304);
		}
	}

	public void QueueApplyLayout() => CallDeferred(MethodName.ApplyLayout);

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

		root.AddChild(BuildEnemySchematicColumn());
		root.AddChild(BuildSchematicColumn());
		_weaponModulesColumn = BuildModulesColumn();
		root.AddChild(_weaponModulesColumn);
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

	private Control BuildSchematicColumn()
	{
		_schematic = new IntegritySchematic
		{
			CustomMinimumSize = new Vector2(280, 304),
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
		if (mech == null)
		{
			_cockpitHud?.ApplyActive(false);
			SyncDiegeticLayout(false);
			return;
		}

		var useDiegetic = ShouldUseDiegeticPanels(mech);
		EnsureCockpitHud(mech);
		_cockpitHud?.Refresh(mech, useDiegetic);
		SyncDiegeticLayout(useDiegetic);

		_schematic?.Refresh(mech);
		_enemySchematic?.Refresh(mech);
		RefreshWeapons(mech);
		RefreshAbilities(mech);
	}

	/// <summary>Whether floating combat chrome should hide in favor of cockpit panels / glass.</summary>
	public bool IsUsingDiegeticCockpit =>
		_cockpitHud is { IsDiegeticActive: true };

	public void SetMissionChrome(
		string claimLine,
		string contractLine,
		string objective,
		string flavor,
		string status,
		string runStrip) =>
		_cockpitHud?.SetMissionChrome(claimLine, contractLine, objective, flavor, status, runStrip);

	/// <summary>
	/// Auto / First Person: bind readouts to cockpit panels while in FP.
	/// Overlay: keep the floating bottom HUD even in first person.
	/// </summary>
	private static bool ShouldUseDiegeticPanels(MechController mech) =>
		GameSettings.ShouldUseDiegeticHudBars(
			IsFirstPersonHud(mech),
			CockpitDiegeticHud.MechHasCockpitScreens(mech));

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

		_rootRow.Visible = !hidePanels;
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
			if (!hp.IsDestroyed && hp.UsesMagazine)
			{
				if (hp.IsReloading)
					status = $"RELOAD {hp.ReloadRemaining:0.0}s";
				else
					status = $"{hp.AmmoInMag}/{hp.MagazineCapacity}";
			}
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
		AbilityId.ContactReveal => "Radar blips · last known",
		_ => "Module"
	};

	private static string ShortBind(string action)
	{
		var raw = InputBindings.FormatAction(action);
		if (string.IsNullOrEmpty(raw) || raw == "—" || raw == "Unbound")
			return "—";
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
