using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Compact weapons + MAP modules readout for cockpit wing screens.</summary>
public partial class CockpitModulesPanel : Control
{
	private sealed class ModuleRow
	{
		public Control Root = null!;
		public Label KeyLabel = null!;
		public Label BodyLabel = null!;
	}

	private readonly List<ModuleRow> _weaponRows = new();
	private readonly List<ModuleRow> _abilityRows = new();

	private static readonly Color EmptyColor = new(0.82f, 0.18f, 0.16f);
	private static readonly Color DeadColor = new(0.22f, 0.2f, 0.2f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(280, 220);
		Build();
	}

	private void Build()
	{
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 2);
		col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(col);

		var header = new Label
		{
			Text = "// WEAPONS / MODULES",
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		header.AddThemeFontSizeOverride("font_size", 9);
		col.AddChild(header);

		_weaponRows.Add(MakeModuleRow(col));
		_weaponRows.Add(MakeModuleRow(col));
		for (var i = 0; i < AbilityController.MaxAbilitySlots; i++)
			_abilityRows.Add(MakeModuleRow(col));
	}

	private static ModuleRow MakeModuleRow(Control parent)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0, 34),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 3);
		parent.AddChild(row);

		var keyLabel = new Label
		{
			Name = "Key",
			Text = "—",
			CustomMinimumSize = new Vector2(28, 34),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		keyLabel.AddThemeFontSizeOverride("font_size", 8);
		row.AddChild(keyLabel);

		var bodyLabel = new Label
		{
			Name = "Body",
			Text = "Empty",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bodyLabel.AddThemeFontSizeOverride("font_size", 8);
		row.AddChild(bodyLabel);

		return new ModuleRow { Root = row, KeyLabel = keyLabel, BodyLabel = bodyLabel };
	}

	public void Refresh(MechController? mech)
	{
		if (mech == null)
			return;

		RefreshWeaponRow(0, mech, PartSlot.WeaponL, "fire_primary");
		RefreshWeaponRow(1, mech, PartSlot.WeaponR, "fire_secondary");
		RefreshAbilities(mech);
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
			row.BodyLabel.Text = "Empty mount";
			row.BodyLabel.Modulate = new Color(0.65f, 0.68f, 0.72f);
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
			details = $"{part.DisplayName} · SH {part.ShieldArcDegrees:0}° · {status}";
		}
		else if (part.WeaponFamily == WeaponFamily.Melee)
		{
			status = hp.IsDestroyed ? "DOWN" : "READY";
			details = $"{part.DisplayName} · DMG {part.Damage:0} · {status}";
		}
		else
		{
			status = hp.IsDestroyed ? "DOWN" : "READY";
			details = $"{part.DisplayName} · DMG {part.Damage:0} · H {part.HeatPerShot:0} · {status}";
		}

		row.BodyLabel.Text = details;
		row.BodyLabel.Modulate = hp.IsDestroyed || (part.IsHeldShield && mech.IsHeldShieldBroken(slot))
			? DeadColor
			: part.IsHeldShield && mech.IsHeldShieldRaised(slot)
				? new Color(0.55f, 0.82f, 0.92f)
				: Colors.White;
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
			row.BodyLabel.Text = $"{part.DisplayName} · {ready}";
			row.BodyLabel.Modulate = cd <= 0.05f
				? new Color(0.65f, 0.95f, 0.75f)
				: new Color(0.95f, 0.75f, 0.55f);
		}
	}

	private static string ShortBind(string action)
	{
		var raw = InputBindings.FormatAction(action);
		if (string.IsNullOrEmpty(raw) || raw == "—" || raw == "Unbound")
			return "—";
		var first = raw.Split(',')[0].Trim();
		if (first.StartsWith("Mouse "))
			return first.Replace("Mouse ", "M");
		if (first.Length > 4)
			return first[..4];
		return first;
	}
}
