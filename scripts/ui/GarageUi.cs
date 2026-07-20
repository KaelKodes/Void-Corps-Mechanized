using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Hangar loadout screen: category rail, owned component list, selection details
/// with direct compare, 3D MAP preview, preview-before-equip, and live chassis stats.
/// Shared by prep and field garage.
/// </summary>
public partial class GarageUi : Control
{
	[Signal] public delegate void LoadoutAppliedEventHandler(LoadoutData loadout);
	[Signal] public delegate void FieldDeliveryRequestedEventHandler(int slot, string instanceId);

	private enum HangarCategory
	{
		Head,
		Torso,
		Legs,
		ArmL,
		ArmR,
		Utilities
	}

	private LoadoutData _draft = null!;
	private LoadoutData _baseline = null!;
	private bool _prepMode = true;

	private HangarCategory? _openCategory;
	private PartSlot? _activeSlot;
	private string? _previewPartId;
	/// <summary>Part shown in Selection Display (preview or focused row, including equipped).</summary>
	private string? _selectedPartId;

	private PanelContainer? _listPanel;
	private VBoxContainer? _railBox;
	private HBoxContainer? _subSlotRow;
	private VBoxContainer? _listBox;
	private Label? _listHint;
	private RichTextLabel? _selectionBody;
	private HangarMechPreview? _mechPreview;
	private RichTextLabel? _statsBody;
	private Label? _titleLabel;
	private Label? _subtitleLabel;
	private Button? _equipButton;
	private Button? _readyButton;
	private Label? _equipHint;

	private readonly Dictionary<HangarCategory, Button> _railButtons = new();

	public override void _Ready()
	{
		GameCatalog.EnsureBuilt();
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		_draft = session?.CurrentLoadout.Clone() ?? GameCatalog.CreateStarterLoadout();
		_baseline = _draft.Clone();
		GameCatalog.SanitizeMounts(_draft);

		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildUi();
		ConfigurePrepMode(true);
		RefreshAll();
	}

	public void ConfigurePrepMode(bool prep)
	{
		_prepMode = prep;
		if (_titleLabel != null)
			_titleLabel.Text = prep ? "HANGAR" : "FIELD HANGAR";
		if (_subtitleLabel != null)
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			var claim = session?.CurrentClaim.DisplayName ?? "Unlisted Claim";
			var mission = MissionCatalog.Get(session?.Match.MissionType ?? session?.PendingMission ?? MissionType.DestroyAllEnemies).Title;
			_subtitleLabel.Text = prep
				? $"Staging — {claim}  ·  {mission}"
				: $"Mid-claim refit — {claim}";
		}

		if (_readyButton != null)
			_readyButton.Text = prep ? "READY" : "REQUEST DELIVERY";
	}

	public void RefreshFromSession()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session != null)
		{
			session.Profile.EnforceOwnedEquipLimits(session.Profile.Loadout);
			_draft = session.CurrentLoadout.Clone();
		}

		GameCatalog.SanitizeMounts(_draft);
		_baseline = _draft.Clone();
		ClearPreviewSelection(closeCategory: false);
		RefreshAll();
	}

	private void BuildUi()
	{
		AddChild(MechUiTheme.MakeDimOverlay());

		var margins = new MarginContainer { Name = "Margins" };
		margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margins.AddThemeConstantOverride("margin_left", 24);
		margins.AddThemeConstantOverride("margin_top", 24);
		margins.AddThemeConstantOverride("margin_right", 24);
		margins.AddThemeConstantOverride("margin_bottom", 24);
		AddChild(margins);

		var root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 12);
		margins.AddChild(root);

		var headerPanel = MechUiTheme.MakePanel("HeaderStrip", deep: true);
		headerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.AddChild(headerPanel);
		var headerInner = MechUiTheme.MakeHeaderStrip("HANGAR", "");
		headerPanel.AddChild(headerInner);
		_titleLabel = headerInner.GetNodeOrNull<Label>("Title");
		_subtitleLabel = headerInner.GetNodeOrNull<Label>("Subtitle");

		var columns = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		columns.AddThemeConstantOverride("separation", 12);
		root.AddChild(columns);

		BuildLeftColumn(columns);
		BuildPreviewColumn(columns);
		BuildStatsColumn(columns);
	}

	private void BuildLeftColumn(HBoxContainer columns)
	{
		var left = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(480, 0)
		};
		left.AddThemeConstantOverride("separation", 10);
		columns.AddChild(left);

		var top = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f
		};
		top.AddThemeConstantOverride("separation", 10);
		left.AddChild(top);

		BuildRail(top);
		BuildComponentList(top);
		BuildSelectionDisplay(left);
	}

	private void BuildRail(HBoxContainer parent)
	{
		var panel = MechUiTheme.MakePanel("HangarRail", 168);
		panel.CustomMinimumSize = new Vector2(168, 0);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		parent.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		panel.AddChild(inner);
		inner.AddChild(MechUiTheme.MakeSectionLabel("HANGAR"));

		_railBox = new VBoxContainer();
		_railBox.AddThemeConstantOverride("separation", 6);
		inner.AddChild(_railBox);

		AddRailButton(HangarCategory.Head, "Head");
		AddRailButton(HangarCategory.Torso, "Torso");
		AddRailButton(HangarCategory.Legs, "Legs");
		AddRailButton(HangarCategory.ArmL, "L. Arm");
		AddRailButton(HangarCategory.ArmR, "R. Arm");
		AddRailButton(HangarCategory.Utilities, "Utilities");
	}

	private void AddRailButton(HangarCategory category, string label)
	{
		var button = new Button
		{
			Text = label,
			CustomMinimumSize = new Vector2(0, 42),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ToggleMode = true
		};
		MechUiTheme.StyleGhostButton(button);
		button.Pressed += () => OnRailPressed(category);
		_railBox!.AddChild(button);
		_railButtons[category] = button;
	}

	private void BuildComponentList(HBoxContainer parent)
	{
		_listPanel = MechUiTheme.MakePanel("ComponentList", 300);
		_listPanel.CustomMinimumSize = new Vector2(300, 0);
		_listPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_listPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		_listPanel.Visible = false;
		parent.AddChild(_listPanel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		_listPanel.AddChild(inner);
		inner.AddChild(MechUiTheme.MakeSectionLabel("COMPONENT LIST"));

		_subSlotRow = new HBoxContainer();
		_subSlotRow.AddThemeConstantOverride("separation", 6);
		inner.AddChild(_subSlotRow);

		_listHint = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		_listHint.AddThemeFontSizeOverride("font_size", 12);
		inner.AddChild(_listHint);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		inner.AddChild(scroll);

		_listBox = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_listBox.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_listBox);
	}

	private void BuildSelectionDisplay(VBoxContainer left)
	{
		var panel = MechUiTheme.MakePanel("SelectionDisplay", deep: true);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.SizeFlagsStretchRatio = 1f;
		panel.CustomMinimumSize = new Vector2(0, 220);
		left.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		panel.AddChild(inner);
		inner.AddChild(MechUiTheme.MakeSectionLabel("SELECTION DISPLAY"));

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		inner.AddChild(scroll);

		_selectionBody = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 40)
		};
		_selectionBody.AddThemeFontSizeOverride("normal_font_size", 13);
		_selectionBody.AddThemeColorOverride("default_color", MechUiTheme.Text);
		scroll.AddChild(_selectionBody);
	}

	private void BuildPreviewColumn(HBoxContainer columns)
	{
		var center = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		center.AddThemeConstantOverride("separation", 10);
		columns.AddChild(center);

		var previewPanel = MechUiTheme.MakePanel("MechPreview");
		previewPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		previewPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		previewPanel.CustomMinimumSize = new Vector2(420, 320);
		center.AddChild(previewPanel);

		var previewInner = new VBoxContainer();
		previewInner.AddThemeConstantOverride("separation", 8);
		previewPanel.AddChild(previewInner);
		previewInner.AddChild(MechUiTheme.MakeSectionLabel("MECH PREVIEW"));

		_mechPreview = new HangarMechPreview
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(360, 260)
		};
		previewInner.AddChild(_mechPreview);

		var tip = new Label
		{
			Text = "Drag to orbit  ·  scroll to zoom",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		tip.AddThemeFontSizeOverride("font_size", 11);
		previewInner.AddChild(tip);

		var actions = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		actions.AddThemeConstantOverride("separation", 10);
		center.AddChild(actions);

		_equipButton = new Button { Text = "EQUIP", CustomMinimumSize = new Vector2(120, 42) };
		MechUiTheme.StylePrimaryButton(_equipButton);
		_equipButton.Pressed += OnEquipPressed;
		actions.AddChild(_equipButton);

		var reset = new Button { Text = "RESET", CustomMinimumSize = new Vector2(120, 42) };
		MechUiTheme.StyleGhostButton(reset);
		reset.Pressed += OnResetPressed;
		actions.AddChild(reset);

		var exit = new Button { Text = "EXIT", CustomMinimumSize = new Vector2(120, 42) };
		MechUiTheme.StyleGhostButton(exit);
		exit.Pressed += OnExitPressed;
		actions.AddChild(exit);

		_equipHint = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_equipHint.AddThemeFontSizeOverride("font_size", 12);
		center.AddChild(_equipHint);

		_readyButton = new Button
		{
			Text = "READY",
			CustomMinimumSize = new Vector2(280, 50),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		_readyButton.AddThemeFontSizeOverride("font_size", 20);
		MechUiTheme.StylePrimaryButton(_readyButton);
		_readyButton.Pressed += OnReadyPressed;
		center.AddChild(_readyButton);
	}

	private void BuildStatsColumn(HBoxContainer columns)
	{
		var panel = MechUiTheme.MakePanel("LiveStats", 280);
		panel.CustomMinimumSize = new Vector2(280, 0);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		panel.AddChild(inner);
		inner.AddChild(MechUiTheme.MakeSectionLabel("LIVE STAT DISPLAY"));

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		inner.AddChild(scroll);

		_statsBody = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 40)
		};
		_statsBody.AddThemeFontSizeOverride("normal_font_size", 13);
		_statsBody.AddThemeColorOverride("default_color", MechUiTheme.Text);
		scroll.AddChild(_statsBody);
	}

	private void OnRailPressed(HangarCategory category)
	{
		SfxService.Click();
		if (_openCategory == category)
		{
			ClearPreviewSelection(closeCategory: true);
			RefreshAll();
			return;
		}

		_openCategory = category;
		_previewPartId = null;
		_activeSlot = DefaultSlotForCategory(category);
		_selectedPartId = _activeSlot == null ? null : _draft.GetPartId(_activeSlot.Value);
		RefreshAll();
	}

	private PartSlot? DefaultSlotForCategory(HangarCategory category) => category switch
	{
		HangarCategory.Head => PartSlot.Head,
		HangarCategory.Torso => PartSlot.Torso,
		HangarCategory.Legs => PartSlot.Legs,
		HangarCategory.ArmL => PartSlot.WeaponL,
		HangarCategory.ArmR => PartSlot.WeaponR,
		HangarCategory.Utilities => FirstAvailableUtilitySlot(),
		_ => null
	};

	private PartSlot? FirstAvailableUtilitySlot()
	{
		foreach (var slot in new[] { PartSlot.ShoulderL, PartSlot.ShoulderR, PartSlot.Backpack, PartSlot.Systems })
		{
			if (GameCatalog.IsMountAvailable(_draft, slot))
				return slot;
		}

		return PartSlot.Systems;
	}

	private void OnSubSlotPressed(PartSlot slot)
	{
		SfxService.Click();
		_activeSlot = slot;
		_previewPartId = null;
		_selectedPartId = _draft.GetPartId(slot);
		RefreshAll();
	}

	private void OnPartRowPressed(string partId)
	{
		SfxService.Click();
		if (_activeSlot == null)
			return;

		_selectedPartId = partId;
		_previewPartId = partId == _draft.GetPartId(_activeSlot.Value) ? null : partId;
		RefreshAll();
	}

	private void OnEquipPressed()
	{
		if (_activeSlot == null || string.IsNullOrEmpty(_previewPartId))
			return;

		var slot = _activeSlot.Value;
		if (!GameCatalog.CanEquipPart(_draft, slot, _previewPartId))
		{
			SfxService.Play("alarm", 1.05f, -6f);
			RefreshActions();
			return;
		}

		SfxService.Confirm();
		_draft.SetPartId(slot, _previewPartId);
		if (slot is PartSlot.Torso or PartSlot.PowerCore)
			GameCatalog.SanitizeMounts(_draft);
		_selectedPartId = _previewPartId;
		_previewPartId = null;
		RefreshAll();
	}

	private void OnResetPressed()
	{
		SfxService.Click();
		_draft = _baseline.Clone();
		GameCatalog.SanitizeMounts(_draft);
		ClearPreviewSelection(closeCategory: true);
		RefreshAll();
	}

	private void OnExitPressed()
	{
		SfxService.Click();
		if (_prepMode)
		{
			ClearPreviewSelection(closeCategory: true);
			RefreshAll();
			return;
		}

		Visible = false;
		ClearPreviewSelection(closeCategory: true);
	}

	private void OnReadyPressed()
	{
		if (!_prepMode)
		{
			TryRequestFieldDelivery();
			return;
		}

		GameCatalog.SanitizeMounts(_draft);
		if (!GameCatalog.IsPowerLegal(_draft))
		{
			SfxService.Play("alarm", 1.05f, -6f);
			RefreshStats();
			RefreshActions();
			return;
		}

		SfxService.Confirm();
		_baseline = _draft.Clone();
		EmitSignal(SignalName.LoadoutApplied, _draft.Clone());
	}

	private void TryRequestFieldDelivery()
	{
		if (_activeSlot == null || string.IsNullOrEmpty(_previewPartId))
		{
			SfxService.Play("alarm", 1.05f, -6f);
			if (_equipHint != null)
				_equipHint.Text = "Select a spare part to request a field delivery.";
			return;
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return;

		var slot = _activeSlot.Value;
		var spare = session.Profile.GetSpareInstances(_previewPartId)
			.FirstOrDefault(i => GameCatalog.GetPart(i.PartId)?.Slot == slot);
		if (spare == null)
		{
			SfxService.Play("alarm", 1.05f, -6f);
			if (_equipHint != null)
				_equipHint.Text = "No free copy available for delivery.";
			return;
		}

		SfxService.Confirm();
		EmitSignal(SignalName.FieldDeliveryRequested, (int)slot, spare.InstanceId);
		Visible = false;
	}

	private void ClearPreviewSelection(bool closeCategory)
	{
		_previewPartId = null;
		_selectedPartId = null;
		_activeSlot = null;
		if (closeCategory)
			_openCategory = null;
	}

	private void RefreshAll()
	{
		GameCatalog.SanitizeMounts(_draft);
		RefreshRail();
		RefreshComponentList();
		RefreshPreview();
		RefreshSelectionDisplay();
		RefreshStats();
		RefreshActions();
	}

	private void RefreshRail()
	{
		foreach (var (category, button) in _railButtons)
		{
			var selected = _openCategory == category;
			button.ButtonPressed = selected;
			button.AddThemeStyleboxOverride("normal", MechUiTheme.MakeChipStyle(selected));
			button.AddThemeStyleboxOverride("hover", MechUiTheme.MakeChipStyle(true));
			button.AddThemeStyleboxOverride("pressed", MechUiTheme.MakeChipStyle(true));
			button.AddThemeColorOverride("font_color", selected ? MechUiTheme.AccentHot : MechUiTheme.Muted);
		}
	}

	private void RefreshComponentList()
	{
		if (_listPanel == null || _listBox == null || _subSlotRow == null || _listHint == null)
			return;

		foreach (var child in _listBox.GetChildren())
			child.QueueFree();
		foreach (var child in _subSlotRow.GetChildren())
			child.QueueFree();

		if (_openCategory == null || _activeSlot == null)
		{
			_listPanel.Visible = false;
			return;
		}

		_listPanel.Visible = true;
		var subSlots = SubSlotsForCategory(_openCategory.Value).ToList();
		if (subSlots.Count > 1)
		{
			foreach (var slot in subSlots)
			{
				var selected = _activeSlot == slot;
				var btn = new Button
				{
					Text = SlotLabel(slot),
					ToggleMode = true,
					ButtonPressed = selected,
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					CustomMinimumSize = new Vector2(0, 32)
				};
				MechUiTheme.StyleGhostButton(btn);
				if (selected)
				{
					btn.AddThemeStyleboxOverride("normal", MechUiTheme.MakeChipStyle(true));
					btn.AddThemeColorOverride("font_color", MechUiTheme.AccentHot);
				}

				var captured = slot;
				btn.Pressed += () => OnSubSlotPressed(captured);
				_subSlotRow.AddChild(btn);
			}
		}

		_listHint.Text = $"{SlotLabel(_activeSlot.Value)}  ·  owned kit  ·  select to preview";

		var options = GetOwnedOptionsForSlot(_activeSlot.Value);
		var equippedId = _draft.GetPartId(_activeSlot.Value);
		if (options.Count == 0)
		{
			_listBox.AddChild(new Label
			{
				Text = "No owned parts for this slot.",
				Modulate = MechUiTheme.Muted
			});
			return;
		}

		foreach (var part in options)
		{
			var isEquipped = part.Id == equippedId;
			var isPreview = _previewPartId == part.Id;
			var isSelected = _selectedPartId == part.Id || (string.IsNullOrEmpty(_selectedPartId) && isEquipped);
			_listBox.AddChild(MakePartRow(part, isEquipped, isPreview || (isSelected && !isEquipped)));
		}
	}

	private IEnumerable<PartSlot> SubSlotsForCategory(HangarCategory category)
	{
		switch (category)
		{
			case HangarCategory.Head:
				yield return PartSlot.Head;
				break;
			case HangarCategory.Torso:
				yield return PartSlot.Torso;
				yield return PartSlot.PowerCore;
				break;
			case HangarCategory.Legs:
				yield return PartSlot.Legs;
				break;
			case HangarCategory.ArmL:
				yield return PartSlot.WeaponL;
				break;
			case HangarCategory.ArmR:
				yield return PartSlot.WeaponR;
				break;
			case HangarCategory.Utilities:
				foreach (var slot in new[] { PartSlot.ShoulderL, PartSlot.ShoulderR, PartSlot.Backpack, PartSlot.Systems })
				{
					if (GameCatalog.IsMountAvailable(_draft, slot))
						yield return slot;
				}
				break;
		}
	}

	private Control MakePartRow(PartData part, bool equipped, bool preview)
	{
		var row = new Button
		{
			CustomMinimumSize = new Vector2(0, 72),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = HorizontalAlignment.Left,
			ClipText = true
		};
		var tint = equipped || preview ? part.Tint.Lerp(MechUiTheme.Border, 0.45f) : (Color?)null;
		row.AddThemeStyleboxOverride("normal", MechUiTheme.MakeChipStyle(equipped || preview, tint));
		row.AddThemeStyleboxOverride("hover", MechUiTheme.MakeChipStyle(true, part.Tint));
		row.AddThemeStyleboxOverride("pressed", MechUiTheme.MakeChipStyle(true, part.Tint));

		var body = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		body.AddThemeConstantOverride("separation", 10);
		row.AddChild(body);

		var portrait = new TextureRect
		{
			Texture = PartThumbnail.Get(part, 96),
			CustomMinimumSize = new Vector2(56, 56),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		body.AddChild(portrait);

		var textCol = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		textCol.AddThemeConstantOverride("separation", 2);
		body.AddChild(textCol);

		var name = new Label
		{
			Text = part.DisplayName,
			Modulate = MechUiTheme.AccentHot,
			MouseFilter = MouseFilterEnum.Ignore
		};
		name.AddThemeFontSizeOverride("font_size", 14);
		textCol.AddChild(name);

		var mfg = GameCatalog.GetManufacturer(part.ManufacturerId);
		var meta = new Label
		{
			Text = $"{CatalogTiers.ShortLabel(part.Tier)}  ·  {mfg.DisplayName}" +
			       (equipped ? "  ·  EQUIPPED" : preview ? "  ·  PREVIEW" : ""),
			Modulate = equipped ? MechUiTheme.Success : preview ? MechUiTheme.Cyan : MechUiTheme.Muted,
			MouseFilter = MouseFilterEnum.Ignore
		};
		meta.AddThemeFontSizeOverride("font_size", 11);
		textCol.AddChild(meta);

		if (ManufacturerBrand.TryGetTexture(part.ManufacturerId, out var mark))
		{
			var markRect = new TextureRect
			{
				Texture = mark,
				CustomMinimumSize = new Vector2(28, 28),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			body.AddChild(markRect);
		}

		var partId = part.Id;
		row.Pressed += () => OnPartRowPressed(partId);
		return row;
	}

	private void RefreshPreview()
	{
		_mechPreview?.ShowLoadout(BuildDisplayLoadout());
	}

	private void RefreshSelectionDisplay()
	{
		if (_selectionBody == null)
			return;

		if (_activeSlot == null)
		{
			_selectionBody.Text =
				"// NO SLOT SELECTED\n\n" +
				"Open a hangar category and pick a component.\n" +
				"Weapon, mount, and systems details appear here with a direct compare against what they replace.";
			return;
		}

		var slot = _activeSlot.Value;
		var equippedId = _draft.GetPartId(slot);
		var selectedId = string.IsNullOrEmpty(_selectedPartId)
			? (string.IsNullOrEmpty(_previewPartId) ? equippedId : _previewPartId)
			: _selectedPartId;
		var selected = GameCatalog.GetPart(selectedId);
		if (selected == null || selected.VisualKind == "empty")
		{
			_selectionBody.Text =
				$"// {SlotLabel(slot).ToUpperInvariant()}\n\n" +
				"Empty mount — select an owned part from the component list.";
			return;
		}

		var equipped = GameCatalog.GetPart(equippedId);
		var comparing = equipped != null
			&& equipped.VisualKind != "empty"
			&& equipped.Id != selected.Id;
		var mfg = GameCatalog.GetManufacturer(selected.ManufacturerId);

		var sb = new StringBuilder();
		sb.AppendLine(comparing ? "// SELECTED vs EQUIPPED" : "// EQUIPPED / SELECTED");
		sb.AppendLine($"[color=#D4B56A]{selected.DisplayName}[/color]");
		sb.AppendLine(
			$"{CatalogTiers.Label(selected.Tier)}  ·  {SlotLabel(slot)}  ·  {mfg.DisplayName}");
		if (comparing && equipped != null)
			sb.AppendLine($"Replaces  {equipped.DisplayName}");
		sb.AppendLine();

		AppendPartDetailBlock(sb, selected, comparing ? equipped : null);
		_selectionBody.Text = sb.ToString().TrimEnd();
	}

	private static void AppendPartDetailBlock(StringBuilder sb, PartData selected, PartData? equipped)
	{
		var compare = equipped != null;
		sb.AppendLine("CONTRIBUTION");
		AppendPartStat(sb, "Structure", selected.StructureHp, equipped?.StructureHp, compare, "0");
		AppendPartStat(sb, "Armor", selected.Armor, equipped?.Armor, compare, "0");
		AppendPartStat(sb, "Weight", selected.Weight, equipped?.Weight, compare, "0", invertGood: true);
		AppendPartStat(sb, "Power req", selected.PowerRequirement, equipped?.PowerRequirement, compare, "0",
			invertGood: true);
		AppendPartStat(sb, "Heat cap", selected.HeatCapBonus, equipped?.HeatCapBonus, compare, "0");
		AppendPartStat(sb, "Sink /s", selected.HeatDissipation, equipped?.HeatDissipation, compare, "0.0");

		if (selected.Slot is PartSlot.WeaponL or PartSlot.WeaponR
		    || selected.IsHeldShield
		    || selected.WeaponFamily != WeaponFamily.None)
		{
			sb.AppendLine();
			sb.AppendLine("WEAPON");
			if (selected.WeaponFamily != WeaponFamily.None || (equipped?.WeaponFamily ?? WeaponFamily.None) != WeaponFamily.None)
			{
				if (compare && equipped != null && selected.WeaponFamily != equipped.WeaponFamily)
					sb.AppendLine($"  Family  {selected.WeaponFamily}  ←  {equipped.WeaponFamily}");
				else
					sb.AppendLine($"  Family  {selected.WeaponFamily}");
			}

			if (selected.IsHeldShield || equipped is { IsHeldShield: true })
			{
				AppendPartStat(sb, "Shield arc", selected.ShieldArcDegrees, equipped?.ShieldArcDegrees, compare, "0");
				AppendPartStat(sb, "Raise /s", selected.ShieldPowerPerSec, equipped?.ShieldPowerPerSec, compare, "0",
					invertGood: true);
				AppendPartStat(sb, "Heat / dmg", selected.ShieldHeatPerDamage, equipped?.ShieldHeatPerDamage, compare,
					"0.0", invertGood: true);
			}
			else
			{
				var melee = selected.WeaponFamily == WeaponFamily.Melee
				            || equipped?.WeaponFamily == WeaponFamily.Melee;
				AppendPartStat(sb, "Damage", selected.Damage, equipped?.Damage, compare, "0");
				AppendPartStat(sb, melee ? "Reach" : "Range", selected.Range, equipped?.Range, compare,
					melee ? "0.0" : "0");
				AppendPartStat(sb, melee ? "Contact rate" : "Fire rate", selected.FireRate, equipped?.FireRate,
					compare, "0.0");
				AppendPartStat(sb, melee ? "Heat / contact" : "Heat / shot", selected.HeatPerShot,
					equipped?.HeatPerShot, compare, "0", invertGood: true);
				AppendPartStat(sb, melee ? "Power / contact" : "Power / shot", selected.PowerPerShot,
					equipped?.PowerPerShot, compare, "0", invertGood: true);
				if (!melee)
					AppendPartStat(sb, "Proj speed", selected.ProjectileSpeed, equipped?.ProjectileSpeed, compare, "0");
				if (selected.AimMode != AimMode.Fixed || (equipped != null && equipped.AimMode != selected.AimMode))
					sb.AppendLine(compare && equipped != null && equipped.AimMode != selected.AimMode
						? $"  Aim  {selected.AimMode}  ←  {equipped.AimMode}"
						: $"  Aim  {selected.AimMode}");
			}
		}

		if (selected.Slot == PartSlot.PowerCore)
		{
			sb.AppendLine();
			sb.AppendLine("POWER CORE");
			if (compare && equipped != null && selected.PowerCoreClass != equipped.PowerCoreClass)
				sb.AppendLine($"  Class  {selected.PowerCoreClass}  ←  {equipped.PowerCoreClass}");
			else
				sb.AppendLine($"  Class  {selected.PowerCoreClass}");
			AppendPartStat(sb, "Capacity", selected.PowerCapacity, equipped?.PowerCapacity, compare, "0");
			AppendPartStat(sb, "Gen /s", selected.PowerOutput, equipped?.PowerOutput, compare, "0.0");
		}

		if (selected.Slot == PartSlot.Torso)
		{
			sb.AppendLine();
			sb.AppendLine("TORSO");
			AppendPartStat(sb, "Shoulder mounts", selected.ShoulderMountCount, equipped?.ShoulderMountCount, compare,
				"0");
			AppendPartStat(sb, "Back mounts", selected.BackpackMountCount, equipped?.BackpackMountCount, compare, "0");
			AppendPartStat(sb, "Core housing", selected.PowerCoreHousing, equipped?.PowerCoreHousing, compare, "0");
		}

		if (selected.Slot == PartSlot.Legs)
		{
			sb.AppendLine();
			sb.AppendLine("MOBILITY");
			sb.AppendLine(compare && equipped != null && selected.LegType != equipped.LegType
				? $"  Drive  {selected.LegType}  ←  {equipped.LegType}"
				: $"  Drive  {selected.LegType}");
			AppendPartStat(sb, "Walk", selected.MaxSpeed, equipped?.MaxSpeed, compare, "0.0");
			AppendPartStat(sb, "Turn", selected.TurnRateDegrees, equipped?.TurnRateDegrees, compare, "0");
			AppendPartStat(sb, "Load rating", selected.LoadRating, equipped?.LoadRating, compare, "0");
			sb.AppendLine($"  Sprint {(selected.CanSprint ? $"Yes ×{selected.SprintMultiplier:0.00}" : "No")}");
			if (selected.MobilityModule == MobilityModuleKind.Booster || equipped?.MobilityModule == MobilityModuleKind.Booster)
			{
				sb.AppendLine($"  Boosters  {(selected.MobilityModule == MobilityModuleKind.Booster ? "Yes" : "No")}");
				if (selected.MobilityModule == MobilityModuleKind.Booster)
				{
					AppendPartStat(sb, "Jump impulse", selected.JumpImpulse, equipped?.JumpImpulse, compare, "0.0");
					AppendPartStat(sb, "Jump power", selected.JumpPowerCost, equipped?.JumpPowerCost, compare, "0",
						invertGood: true);
					AppendPartStat(sb, "Jump heat", selected.JumpHeat, equipped?.JumpHeat, compare, "0",
						invertGood: true);
				}
			}
			if (selected.MobilityModule == MobilityModuleKind.Thruster || equipped?.MobilityModule == MobilityModuleKind.Thruster)
			{
				sb.AppendLine($"  Thrusters  {(selected.MobilityModule == MobilityModuleKind.Thruster ? "Yes" : "No")}");
				if (selected.MobilityModule == MobilityModuleKind.Thruster)
				{
					AppendPartStat(sb, "Dash speed", selected.DashSpeed, equipped?.DashSpeed, compare, "0.0");
					AppendPartStat(sb, "Dash dur", selected.DashDuration, equipped?.DashDuration, compare, "0.00");
					AppendPartStat(sb, "Dash CD", selected.DashCooldown, equipped?.DashCooldown, compare, "0.00",
						invertGood: true);
					AppendPartStat(sb, "Dash power", selected.DashPowerCost, equipped?.DashPowerCost, compare, "0",
						invertGood: true);
					AppendPartStat(sb, "Dash heat", selected.DashHeat, equipped?.DashHeat, compare, "0",
						invertGood: true);
				}
			}
		}

		if (selected.Slot == PartSlot.Head)
		{
			sb.AppendLine();
			sb.AppendLine("SENSORS");
			AppendPartStat(sb, "Vision m", selected.VisionRange, equipped?.VisionRange, compare, "0");
			AppendPartStat(sb, "Vision deg", selected.VisionAngleDeg, equipped?.VisionAngleDeg, compare, "0");
			AppendPartStat(sb, "Close ID", selected.CloseTargeting, equipped?.CloseTargeting, compare, "0.00");
			AppendPartStat(sb, "Scan m", selected.ScannerRange, equipped?.ScannerRange, compare, "0");
		}

		if (selected.AbilityKind != AbilityKind.None || equipped is { AbilityKind: not AbilityKind.None })
		{
			sb.AppendLine();
			sb.AppendLine("SYSTEMS");
			if (selected.AbilityKind == AbilityKind.Active)
			{
				sb.AppendLine($"  Ability  {selected.AbilityId}");
				AppendPartStat(sb, "Cooldown", selected.AbilityCooldown, equipped?.AbilityCooldown, compare, "0.0",
					invertGood: true);
				AppendPartStat(sb, "Duration", selected.AbilityDuration, equipped?.AbilityDuration, compare, "0.0");
				AppendPartStat(sb, "Radius", selected.AbilityRadius, equipped?.AbilityRadius, compare, "0.0");
				AppendPartStat(sb, "Heat burst", selected.AbilityHeatBurst, equipped?.AbilityHeatBurst, compare, "0",
					invertGood: true);
				AppendPartStat(sb, "Power load", selected.AbilityPowerLoad, equipped?.AbilityPowerLoad, compare, "0",
					invertGood: true);
			}
			else if (selected.AbilityKind == AbilityKind.Passive)
			{
				sb.AppendLine($"  Passive  {selected.AbilityId}");
				if (selected.FireRateBonus > 0f || (equipped?.FireRateBonus ?? 0f) > 0f)
					AppendPartStat(sb, "Fire rate bonus", selected.FireRateBonus, equipped?.FireRateBonus, compare,
						"0.00");
			}
		}

		if (compare && equipped != null && selected.Tier != equipped.Tier)
		{
			sb.AppendLine();
			sb.AppendLine(
				$"  Tier  {CatalogTiers.ShortLabel(selected.Tier)}  ←  {CatalogTiers.ShortLabel(equipped.Tier)}");
		}
	}

	private static void AppendPartStat(
		StringBuilder sb,
		string label,
		float selected,
		float? equipped,
		bool compare,
		string format,
		bool invertGood = false)
	{
		if (!compare || equipped == null || Mathf.IsEqualApprox(selected, equipped.Value))
		{
			if (Mathf.IsZeroApprox(selected) && (equipped == null || Mathf.IsZeroApprox(equipped.Value)))
				return;
			sb.AppendLine($"  {label}  {selected.ToString(format)}");
			return;
		}

		var delta = selected - equipped.Value;
		var better = invertGood ? delta < 0f : delta > 0f;
		var sign = delta > 0f ? "+" : "";
		var color = better ? "#73C7EB" : "#E66152";
		sb.AppendLine(
			$"  {label}  {selected.ToString(format)}  [color={color}]({sign}{delta.ToString(format)})[/color]");
	}

	private LoadoutData BuildDisplayLoadout()
	{
		var display = _draft.Clone();
		if (_activeSlot != null && !string.IsNullOrEmpty(_previewPartId))
		{
			display.SetPartId(_activeSlot.Value, _previewPartId);
			if (_activeSlot is PartSlot.Torso or PartSlot.PowerCore)
				GameCatalog.SanitizeMounts(display);
		}

		GameCatalog.SanitizeMounts(display);
		return display;
	}

	private void RefreshActions()
	{
		if (_equipButton == null || _equipHint == null || _readyButton == null)
			return;

		var previewing = _activeSlot != null && !string.IsNullOrEmpty(_previewPartId);

		if (!_prepMode)
		{
			_equipButton.Disabled = true;
			_equipButton.Modulate = new Color(1f, 1f, 1f, 0.45f);
			_readyButton.Disabled = !previewing;
			_readyButton.Modulate = previewing ? Colors.White : new Color(1f, 0.55f, 0.45f);
			_equipHint.Text = previewing
				? "REQUEST DELIVERY drops this spare near you — hold Interact on the crate to install."
				: "Field Hangar: select a spare owned/loot part, then REQUEST DELIVERY.";
			_equipHint.Modulate = MechUiTheme.Muted;
			return;
		}

		var canEquip = false;
		if (previewing)
			canEquip = GameCatalog.CanEquipPart(_draft, _activeSlot!.Value, _previewPartId!);

		_equipButton.Disabled = !previewing || !canEquip;
		_equipButton.Modulate = _equipButton.Disabled
			? new Color(1f, 1f, 1f, 0.55f)
			: Colors.White;

		if (!previewing)
			_equipHint.Text = "Select a part to preview  ·  EQUIP commits it to the draft";
		else if (!canEquip)
			_equipHint.Text = "Cannot equip — power budget or housing conflict";
		else
			_equipHint.Text = "Preview active — EQUIP to commit, or pick another part";

		_equipHint.Modulate = previewing && !canEquip ? MechUiTheme.Danger : MechUiTheme.Muted;

		var powerLegal = GameCatalog.IsPowerLegal(_draft);
		_readyButton.Disabled = !powerLegal;
		_readyButton.Modulate = powerLegal ? Colors.White : new Color(1f, 0.55f, 0.45f);
	}

	private void RefreshStats()
	{
		if (_statsBody == null)
			return;

		var baseline = DeriveDraftStats(_draft);
		var displayLoadout = BuildDisplayLoadout();
		var preview = DeriveDraftStats(displayLoadout);
		var previewing = _activeSlot != null && !string.IsNullOrEmpty(_previewPartId);

		var reserved = GameCatalog.SumPowerRequirements(displayLoadout);
		var capacity = GameCatalog.GetCoreCapacity(displayLoadout);
		var operational = Mathf.Max(0f, capacity - reserved);
		var powerLegal = GameCatalog.IsPowerLegal(displayLoadout);
		var legs = GameCatalog.GetPart(displayLoadout.LegsId);
		var drive = legs?.LegType switch
		{
			LegType.Hexapod => "Hexapod strafe",
			LegType.Tracks => "Tracked tank",
			_ => "Bipedal tank"
		};

		var sb = new StringBuilder();
		sb.AppendLine(previewing ? "// PREVIEW vs DRAFT" : "// DRAFT LOADOUT");
		sb.AppendLine();
		AppendStat(sb, "Torso integrity", baseline.TorsoHp, preview.TorsoHp, previewing, "0");
		sb.AppendLine();
		sb.AppendLine("POWER");
		sb.AppendLine($"  Class {preview.PowerCoreClass}/{preview.PowerCoreHousing}");
		AppendStat(sb, "Capacity", baseline.PowerCapacity, preview.PowerCapacity, previewing, "0");
		AppendStat(sb, "Generation", baseline.PowerGeneration, preview.PowerGeneration, previewing, "0");
		AppendStat(sb, "Reserved", GameCatalog.SumPowerRequirements(_draft), reserved, previewing, "0");
		AppendStat(sb, "Pool",
			Mathf.Max(0f, GameCatalog.GetCoreCapacity(_draft) - GameCatalog.SumPowerRequirements(_draft)),
			operational, previewing, "0");
		if (!powerLegal)
			sb.AppendLine("  [color=#E66152]!! OVERBUDGET — strip kit or upgrade core[/color]");
		sb.AppendLine();
		sb.AppendLine("HEAT");
		AppendStat(sb, "Cap", baseline.HeatCap, preview.HeatCap, previewing, "0");
		AppendStat(sb, "Dissipate", baseline.HeatDissipation, preview.HeatDissipation, previewing, "0.0");
		sb.AppendLine();
		sb.AppendLine("SENSORS");
		AppendStat(sb, "Vision m", baseline.VisionRange, preview.VisionRange, previewing, "0");
		AppendStat(sb, "Vision deg", baseline.VisionAngleDeg, preview.VisionAngleDeg, previewing, "0");
		AppendStat(sb, "Close ID", baseline.CloseTargeting, preview.CloseTargeting, previewing, "0.00");
		AppendStat(sb, "Scan m", baseline.ScannerRange, preview.ScannerRange, previewing, "0");
		sb.AppendLine();
		sb.AppendLine("MOBILITY");
		sb.AppendLine($"  {drive}");
		var draftWeight = GameCatalog.SumWeight(_draft);
		var draftRating = GameCatalog.GetLoadRating(_draft);
		var previewWeight = GameCatalog.SumWeight(displayLoadout);
		var previewRating = GameCatalog.GetLoadRating(displayLoadout);
		AppendStat(sb, "Weight", draftWeight, previewWeight, previewing, "0");
		AppendStat(sb, "Load rating", draftRating, previewRating, previewing, "0");
		var loadPct = previewRating > 0.01f ? previewWeight / previewRating * 100f : 0f;
		sb.AppendLine($"  Load  {loadPct:0}%");
		if (GameCatalog.IsOverLoadRating(displayLoadout))
			sb.AppendLine("  [color=#E8A24A]!! OVER RATING — walk/turn will suffer[/color]");
		AppendStat(sb, "Walk",
			baseline.WalkSpeed * baseline.WeightMoveMultiplier,
			preview.WalkSpeed * preview.WeightMoveMultiplier,
			previewing, "0.0");
		AppendStat(sb, "Turn",
			baseline.TurnRateDegrees * baseline.WeightTurnMultiplier,
			preview.TurnRateDegrees * preview.WeightTurnMultiplier,
			previewing, "0");
		sb.AppendLine($"  Sprint {(preview.CanSprint ? $"Yes ×{preview.SprintMultiplier:0.00}" : "No")}");
		if (preview.HasBooster)
			sb.AppendLine($"  Boosters  jump {preview.JumpImpulse:0.0}  (P {preview.JumpPowerCost:0} / H {preview.JumpHeat:0})");
		if (preview.HasThruster)
			sb.AppendLine($"  Thrusters  dash {preview.DashSpeed:0.0}  ({preview.DashDuration:0.00}s / CD {preview.DashCooldown:0.00}s)");
		sb.AppendLine();
		sb.AppendLine("MOUNTS");
		sb.AppendLine($"  {preview.ShoulderMounts} shoulder / {preview.BackMounts} back");

		_statsBody.Text = sb.ToString().TrimEnd();
	}

	private static void AppendStat(
		StringBuilder sb, string label, float baseline, float preview, bool previewing, string format)
	{
		if (!previewing || Mathf.IsEqualApprox(baseline, preview))
		{
			sb.AppendLine($"  {label}  {preview.ToString(format)}");
			return;
		}

		var delta = preview - baseline;
		var sign = delta > 0f ? "+" : "";
		var color = delta > 0f ? "#73C7EB" : "#E66152";
		sb.AppendLine(
			$"  {label}  {preview.ToString(format)}  [color={color}]({sign}{delta.ToString(format)})[/color]");
	}

	private List<PartData> GetOwnedOptionsForSlot(PartSlot slot)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		IEnumerable<PartData> options = slot == PartSlot.PowerCore
			? GameCatalog.GetLegalPowerCores(_draft)
			: GameCatalog.GetPartsForSlot(slot);

		if (session != null)
			options = options.Where(p => session.Profile.AvailableForSlot(p.Id, _draft, slot) > 0);

		var list = options.ToList();
		var currentId = _draft.GetPartId(slot);
		if (!string.IsNullOrEmpty(currentId) && list.TrueForAll(p => p.Id != currentId))
		{
			var current = GameCatalog.GetPart(currentId);
			if (current != null)
				list.Insert(0, current);
		}

		// Equipped always first.
		list = list
			.OrderByDescending(p => p.Id == currentId)
			.ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();
		return list;
	}

	private static MechStats DeriveDraftStats(LoadoutData loadout)
	{
		float torsoHp = 40f, speed = 8f, turn = 70f, fire = 1f;
		float heatCap = 40f, dissipate = 6f, idle = 0.5f, moveHeat = 0f;
		float powerCap = 0f, powerGen = 0f, reserved = 0f;
		var hasCore = false;
		int coreClass = 0, housing = 1, shoulders = 0, backs = 0;
		float vision = 12f, angle = 50f, close = 0.15f, scan = 20f, scanRes = 0.1f;
		bool canSprint = false;
		float sprintMult = 1.45f, sprintHeat = 18f, sprintLoad = 25f;
		var legMode = LegMode.Locked;
		var legType = LegType.Bipedal;
		float totalWeight = 0f, loadRating = 0f;
		var hasThruster = false;
		float dashSpeed = 0f, dashDuration = 0.18f, dashCooldown = 1.2f, dashPower = 0f, dashHeat = 0f;
		var hasBooster = false;
		float jumpImpulse = 0f, jumpPower = 0f, jumpHeat = 0f;

		foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
		{
			if (!GameCatalog.IsMountAvailable(loadout, slot))
				continue;
			var p = GameCatalog.GetPart(loadout.GetPartId(slot));
			if (p == null)
				continue;

			if (p.VisualKind != "empty")
			{
				reserved += Math.Max(0f, p.PowerRequirement);
				totalWeight += Math.Max(0f, p.Weight);
			}

			speed += p.MaxSpeed;
			turn += p.TurnRateDegrees;
			fire += p.FireRateBonus;
			heatCap += p.HeatCapBonus;
			dissipate += p.HeatDissipation;
			idle += p.IdleHeatPerSec;
			moveHeat += p.MoveHeatPerSec;

			if (p.MobilityModule == MobilityModuleKind.Thruster && p.DashSpeed > 0.1f)
			{
				hasThruster = true;
				if (p.DashSpeed >= dashSpeed)
				{
					dashSpeed = p.DashSpeed;
					dashDuration = Math.Max(0.08f, p.DashDuration);
					dashCooldown = Math.Max(0.2f, p.DashCooldown);
					dashPower = Math.Max(0f, p.DashPowerCost);
					dashHeat = Math.Max(0f, p.DashHeat);
				}
			}
			else if (p.MobilityModule == MobilityModuleKind.Booster && p.JumpImpulse > 0.1f)
			{
				hasBooster = true;
				if (p.JumpImpulse >= jumpImpulse)
				{
					jumpImpulse = p.JumpImpulse;
					jumpPower = Math.Max(0f, p.JumpPowerCost);
					jumpHeat = Math.Max(0f, p.JumpHeat);
				}
			}

			switch (slot)
			{
				case PartSlot.Torso:
					torsoHp = Math.Max(1f, p.StructureHp);
					housing = Math.Max(1, p.PowerCoreHousing);
					shoulders = p.ShoulderMountCount;
					backs = p.BackpackMountCount;
					break;
				case PartSlot.PowerCore:
					hasCore = true;
					coreClass = p.PowerCoreClass;
					powerCap = Math.Max(0f, p.PowerCapacity);
					powerGen = Math.Max(0f, p.PowerOutput);
					break;
				case PartSlot.Head:
					vision = p.VisionRange;
					angle = p.VisionAngleDeg;
					close = p.CloseTargeting;
					scan = p.ScannerRange;
					scanRes = p.ScannerResolution;
					break;
				case PartSlot.Legs:
					legMode = p.LegMode;
					legType = p.LegType;
					canSprint = p.CanSprint;
					sprintMult = p.SprintMultiplier > 0.1f ? p.SprintMultiplier : 1.45f;
					sprintHeat = p.SprintHeatPerSec;
					sprintLoad = p.SprintPowerLoad;
					loadRating = Math.Max(0f, p.LoadRating);
					break;
			}
		}

		if (!hasCore)
		{
			powerCap = 0f;
			powerGen = 0f;
		}

		reserved = Math.Max(0f, reserved);
		var operational = Math.Max(0f, powerCap - reserved);
		var (weightMove, weightTurn, loadRatio) =
			CatalogWeight.ComputeOverloadMultipliers(totalWeight, loadRating);

		return new MechStats
		{
			TorsoHp = torsoHp,
			ShoulderMounts = shoulders,
			BackMounts = backs,
			PowerCoreClass = coreClass,
			PowerCoreHousing = housing,
			PowerCapacity = powerCap,
			PowerGeneration = powerGen,
			PowerReserved = reserved,
			OperationalMax = operational,
			HeatCap = Math.Max(40f, heatCap),
			HeatDissipation = Math.Max(2f, dissipate),
			IdleHeatPerSec = idle,
			MoveHeatPerSec = moveHeat,
			VisionRange = vision,
			VisionAngleDeg = angle,
			CloseTargeting = close,
			ScannerRange = scan,
			ScannerResolution = scanRes,
			WalkSpeed = Math.Max(2.2f, speed * 0.72f),
			TurnRateDegrees = Math.Max(18f, turn * 0.9f),
			FireRateMultiplier = Math.Max(0.25f, fire),
			CanSprint = canSprint,
			SprintMultiplier = sprintMult,
			SprintHeatPerSec = sprintHeat,
			SprintPowerLoad = sprintLoad,
			LegMode = legMode,
			LegType = legType,
			HasThruster = hasThruster,
			DashSpeed = dashSpeed,
			DashDuration = dashDuration,
			DashCooldown = dashCooldown,
			DashPowerCost = dashPower,
			DashHeat = dashHeat,
			HasBooster = hasBooster,
			JumpImpulse = jumpImpulse,
			JumpPowerCost = jumpPower,
			JumpHeat = jumpHeat,
			TotalWeight = totalWeight,
			LoadRating = loadRating,
			LoadRatio = loadRatio,
			WeightMoveMultiplier = weightMove,
			WeightTurnMultiplier = weightTurn
		};
	}

	private static string SlotLabel(PartSlot slot) => slot switch
	{
		PartSlot.Head => "Head",
		PartSlot.Torso => "Torso",
		PartSlot.PowerCore => "Power Core",
		PartSlot.Legs => "Legs",
		PartSlot.WeaponL => "Left Arm",
		PartSlot.WeaponR => "Right Arm",
		PartSlot.ShoulderL => "L Shoulder",
		PartSlot.ShoulderR => "R Shoulder",
		PartSlot.Backpack => "Backpack",
		PartSlot.Systems => "Systems",
		_ => slot.ToString()
	};
}
