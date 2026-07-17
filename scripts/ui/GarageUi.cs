using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Mechanize;

public partial class GarageUi : Control
{
	[Signal] public delegate void LoadoutAppliedEventHandler(LoadoutData loadout);

	private LoadoutData _draft = null!;
	private bool _prepMode = true;
	private bool _gspCollapsed;
	private PartSlot? _selected;

	private PanelContainer? _gspPanel;
	private Label? _gspBody;
	private Button? _gspToggle;
	private PanelContainer? _detailsPanel;
	private TextureRect? _detailsPortrait;
	private Label? _detailsTitle;
	private Label? _detailsBody;
	private Label? _titleLabel;
	private Label? _subtitleLabel;
	private Button? _readyButton;
	private Control? _dollCanvas;
	private readonly Dictionary<PartSlot, SlotChip> _chips = new();

	public override void _Ready()
	{
		GameCatalog.EnsureBuilt();
		_draft = GetNodeOrNull<GameSession>("/root/GameSession")?.CurrentLoadout.Clone()
			?? GameCatalog.CreateStarterLoadout();
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
			_titleLabel.Text = prep ? "PREP SCREEN" : "FIELD GARAGE";
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
			_readyButton.Text = prep ? "READY" : "Deploy Loadout";
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
		_selected = null;
		RefreshAll();
	}

	private void BuildUi()
	{
		AddChild(MechUiTheme.MakeDimOverlay());

		var margins = new MarginContainer { Name = "Margins" };
		margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margins.AddThemeConstantOverride("margin_left", 28);
		margins.AddThemeConstantOverride("margin_top", 28);
		margins.AddThemeConstantOverride("margin_right", 28);
		margins.AddThemeConstantOverride("margin_bottom", 28);
		AddChild(margins);

		var root = new HBoxContainer
		{
			Name = "Root",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 14);
		margins.AddChild(root);

		BuildGspColumn(root);
		BuildCenterColumn(root);
		BuildDetailsColumn(root);
	}

	private void BuildGspColumn(HBoxContainer root)
	{
		var col = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		col.AddThemeConstantOverride("separation", 6);
		root.AddChild(col);

		_gspPanel = MechUiTheme.MakePanel("GSP", 248);
		_gspPanel.CustomMinimumSize = new Vector2(248, 0);
		_gspPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		col.AddChild(_gspPanel);

		var gspInner = new VBoxContainer();
		gspInner.AddThemeConstantOverride("separation", 10);
		_gspPanel.AddChild(gspInner);

		gspInner.AddChild(MechUiTheme.MakeSectionLabel("CHASSIS TELEMETRY"));

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		gspInner.AddChild(scroll);

		_gspBody = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.Text
		};
		_gspBody.AddThemeFontSizeOverride("font_size", 14);
		scroll.AddChild(_gspBody);

		_gspToggle = new Button
		{
			Text = "◀",
			CustomMinimumSize = new Vector2(28, 56),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		MechUiTheme.StyleGhostButton(_gspToggle);
		_gspToggle.Pressed += ToggleGsp;
		col.AddChild(_gspToggle);
	}

	private void BuildCenterColumn(HBoxContainer root)
	{
		var center = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		center.AddThemeConstantOverride("separation", 10);
		root.AddChild(center);

		var headerPanel = MechUiTheme.MakePanel("HeaderStrip", deep: true);
		headerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		center.AddChild(headerPanel);

		var headerInner = MechUiTheme.MakeHeaderStrip("PREP SCREEN", "");
		headerPanel.AddChild(headerInner);
		_titleLabel = headerInner.GetNodeOrNull<Label>("Title");
		_subtitleLabel = headerInner.GetNodeOrNull<Label>("Subtitle");

		var dollFrame = MechUiTheme.MakePanel("Paperdoll");
		dollFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		dollFrame.SizeFlagsVertical = SizeFlags.ExpandFill;
		dollFrame.CustomMinimumSize = new Vector2(480, 300);
		center.AddChild(dollFrame);

		var dollInner = new VBoxContainer();
		dollInner.AddThemeConstantOverride("separation", 8);
		dollFrame.AddChild(dollInner);
		dollInner.AddChild(MechUiTheme.MakeSectionLabel("HARDPOINTS"));

		_dollCanvas = new Control
		{
			Name = "DollCanvas",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = false
		};
		_dollCanvas.Resized += LayoutPaperdoll;
		dollInner.AddChild(_dollCanvas);

		CreateChip(PartSlot.Head, "Head", new Vector2(108, 96));
		CreateChip(PartSlot.PowerCore, "Core", new Vector2(88, 78));
		CreateChip(PartSlot.Systems, "Systems", new Vector2(92, 80));
		CreateChip(PartSlot.WeaponL, "Arm L", new Vector2(100, 148));
		CreateChip(PartSlot.Torso, "Torso", new Vector2(148, 148));
		CreateChip(PartSlot.WeaponR, "Arm R", new Vector2(100, 148));
		CreateChip(PartSlot.Legs, "Legs", new Vector2(180, 120));
		CreateChip(PartSlot.ShoulderL, "Shoulder L", new Vector2(96, 80));
		CreateChip(PartSlot.ShoulderR, "Shoulder R", new Vector2(96, 80));
		CreateChip(PartSlot.Backpack, "Back", new Vector2(100, 76));

		var footer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkEnd
		};
		footer.AddThemeConstantOverride("separation", 8);
		center.AddChild(footer);

		_readyButton = new Button
		{
			Text = "READY",
			CustomMinimumSize = new Vector2(280, 50),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		_readyButton.AddThemeFontSizeOverride("font_size", 20);
		MechUiTheme.StylePrimaryButton(_readyButton);
		_readyButton.Pressed += () =>
		{
			GameCatalog.SanitizeMounts(_draft);
			SfxService.Confirm();
			EmitSignal(SignalName.LoadoutApplied, _draft.Clone());
		};
		footer.AddChild(_readyButton);

		var reset = new Button
		{
			Text = "Reset Corps Starter Kit",
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(240, 36)
		};
		MechUiTheme.StyleGhostButton(reset);
		reset.Pressed += () =>
		{
			_draft = GameCatalog.CreateStarterLoadout();
			_selected = null;
			RefreshAll();
		};
		footer.AddChild(reset);

		var tip = new Label
		{
			Text = "Click a hardpoint to inspect  ·  click again to close",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		tip.AddThemeFontSizeOverride("font_size", 12);
		footer.AddChild(tip);
	}

	private void BuildDetailsColumn(HBoxContainer root)
	{
		_detailsPanel = MechUiTheme.MakePanel("Details", 300);
		_detailsPanel.CustomMinimumSize = new Vector2(300, 0);
		_detailsPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		_detailsPanel.Visible = false;
		root.AddChild(_detailsPanel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		_detailsPanel.AddChild(inner);

		inner.AddChild(MechUiTheme.MakeSectionLabel("PART BAY"));

		_detailsPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(140, 140),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		inner.AddChild(_detailsPortrait);

		_detailsTitle = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.AccentHot
		};
		_detailsTitle.AddThemeFontSizeOverride("font_size", 18);
		inner.AddChild(_detailsTitle);

		var cycle = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		cycle.AddThemeConstantOverride("separation", 10);
		inner.AddChild(cycle);

		var prev = new Button { Text = "<  Prev", CustomMinimumSize = new Vector2(96, 34) };
		MechUiTheme.StyleGhostButton(prev);
		prev.Pressed += () => CycleSelected(-1);
		cycle.AddChild(prev);

		var next = new Button { Text = "Next  >", CustomMinimumSize = new Vector2(96, 34) };
		MechUiTheme.StyleGhostButton(next);
		next.Pressed += () => CycleSelected(1);
		cycle.AddChild(next);

		var bodyScroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		inner.AddChild(bodyScroll);

		_detailsBody = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.Text
		};
		_detailsBody.AddThemeFontSizeOverride("font_size", 14);
		bodyScroll.AddChild(_detailsBody);
	}

	private void CreateChip(PartSlot slot, string caption, Vector2 size)
	{
		var chip = new SlotChip(slot, caption, size);
		chip.Pressed += () => OnChipPressed(slot);
		_dollCanvas!.AddChild(chip);
		_chips[slot] = chip;
	}

	private void OnChipPressed(PartSlot slot)
	{
		if (!GameCatalog.IsMountAvailable(_draft, slot) && slot is PartSlot.ShoulderL or PartSlot.ShoulderR or PartSlot.Backpack)
			return;

		_selected = _selected == slot ? null : slot;
		RefreshAll();
	}

	private void CycleSelected(int direction)
	{
		if (_selected == null)
			return;

		var slot = _selected.Value;
		if (!GameCatalog.IsMountAvailable(_draft, slot))
			return;

		var options = GetOwnedOptionsForSlot(slot);
		if (options.Count == 0)
			return;

		var currentId = _draft.GetPartId(slot);
		var index = options.FindIndex(p => p.Id == currentId);
		if (index < 0)
			index = 0;

		index = (index + direction + options.Count) % options.Count;
		_draft.SetPartId(slot, options[index].Id);
		if (slot is PartSlot.Torso or PartSlot.PowerCore)
			GameCatalog.SanitizeMounts(_draft);
		RefreshAll();
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

		return list;
	}

	private void ToggleGsp()
	{
		_gspCollapsed = !_gspCollapsed;
		if (_gspPanel != null)
			_gspPanel.Visible = !_gspCollapsed;
		if (_gspToggle != null)
			_gspToggle.Text = _gspCollapsed ? "▶" : "◀";
	}

	private void RefreshAll()
	{
		GameCatalog.SanitizeMounts(_draft);
		RefreshGsp();
		RefreshPaperdoll();
		RefreshDetails();
		CallDeferred(MethodName.LayoutPaperdoll);
	}

	private void RefreshGsp()
	{
		if (_gspBody == null)
			return;

		GameCatalog.SanitizeMounts(_draft);
		var stats = DeriveDraftStats();
		var legs = GameCatalog.GetPart(_draft.LegsId);
		var drive = legs?.LegType switch
		{
			LegType.Hexapod => "Hexapod strafe",
			LegType.Tracks => "Tracked tank",
			_ => "Bipedal tank"
		};

		_gspBody.Text =
			$"HULL\n  {stats.HullHp:0}\n\n" +
			$"POWER\n  Class {stats.PowerCoreClass}/{stats.PowerCoreHousing}\n  Cap {stats.PowerCapacity:0}  Out {stats.PowerOutput:0}\n\n" +
			$"HEAT\n  Cap {stats.HeatCap:0}\n  Dissipate {stats.HeatDissipation:0.0}/s\n\n" +
			$"SENSORS\n  Vision {stats.VisionRange:0}m / {stats.VisionAngleDeg:0}°\n  Close ID {stats.CloseTargeting:0.00}\n  Scan {stats.ScannerRange:0}m\n\n" +
			$"MOBILITY\n  {drive}\n  Walk {stats.WalkSpeed:0.0}\n  Turn {stats.TurnRateDegrees:0}°/s\n  Sprint {(stats.CanSprint ? $"Yes ×{stats.SprintMultiplier:0.00}" : "No")}\n\n" +
			$"MOUNTS\n  {stats.ShoulderMounts} shoulder / {stats.BackMounts} back";
	}

	private MechStats DeriveDraftStats()
	{
		float hull = 40f, speed = 8f, turn = 70f, fire = 1f;
		float heatCap = 40f, dissipate = 6f, idle = 0.5f, moveHeat = 0f;
		float powerCap = 40f, powerOut = 10f;
		int coreClass = 0, housing = 1, shoulders = 0, backs = 0;
		float vision = 12f, angle = 50f, close = 0.15f, scan = 20f, scanRes = 0.1f;
		bool canSprint = false;
		float sprintMult = 1.45f, sprintHeat = 18f, sprintLoad = 25f;
		var legMode = LegMode.Locked;
		var legType = LegType.Bipedal;

		foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
		{
			if (!GameCatalog.IsMountAvailable(_draft, slot))
				continue;
			var p = GameCatalog.GetPart(_draft.GetPartId(slot));
			if (p == null)
				continue;

			hull += p.Armor + p.HullBonus;
			speed += p.MaxSpeed;
			turn += p.TurnRateDegrees;
			fire += p.FireRateBonus;
			heatCap += p.HeatCapBonus;
			dissipate += p.HeatDissipation;
			idle += p.IdleHeatPerSec;
			moveHeat += p.MoveHeatPerSec;

			switch (slot)
			{
				case PartSlot.Torso:
					housing = Math.Max(1, p.PowerCoreHousing);
					shoulders = p.ShoulderMountCount;
					backs = p.BackpackMountCount;
					break;
				case PartSlot.PowerCore:
					coreClass = p.PowerCoreClass;
					powerCap += p.PowerCapacity;
					powerOut += p.PowerOutput;
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
					break;
			}
		}

		return new MechStats
		{
			HullHp = Math.Max(40f, hull),
			ShoulderMounts = shoulders,
			BackMounts = backs,
			PowerCoreClass = coreClass,
			PowerCoreHousing = housing,
			PowerCapacity = Math.Max(40f, powerCap),
			PowerOutput = Math.Max(5f, powerOut),
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
			LegType = legType
		};
	}

	private void RefreshPaperdoll()
	{
		var torsoExpanded = _selected is PartSlot.Torso or PartSlot.ShoulderL or PartSlot.ShoulderR or PartSlot.Backpack;

		foreach (var (slot, chip) in _chips)
		{
			var available = GameCatalog.IsMountAvailable(_draft, slot);
			var isMount = slot is PartSlot.ShoulderL or PartSlot.ShoulderR or PartSlot.Backpack;
			chip.Visible = available && (!isMount || torsoExpanded);

			var part = available ? GameCatalog.GetPart(_draft.GetPartId(slot)) : null;
			chip.SetPart(part, _selected == slot);

			// When mount cluster is open: fade the rest of the doll and keep mounts on top.
			var inFocusCluster = torsoExpanded && (slot == PartSlot.Torso || isMount);
			var dimmed = torsoExpanded && !inFocusCluster && chip.Visible;
			chip.SetDimmed(dimmed);

			if (!chip.Visible)
			{
				chip.ZIndex = 0;
				continue;
			}

			if (torsoExpanded && isMount)
				chip.ZIndex = 20;
			else if (torsoExpanded && slot == PartSlot.Torso)
				chip.ZIndex = 15;
			else if (dimmed)
				chip.ZIndex = 0;
			else
				chip.ZIndex = 5;
		}

		if (torsoExpanded)
		{
			// Ensure draw order among siblings matches ZIndex intent.
			_chips[PartSlot.Torso].MoveToFront();
			if (_chips[PartSlot.ShoulderL].Visible)
				_chips[PartSlot.ShoulderL].MoveToFront();
			if (_chips[PartSlot.ShoulderR].Visible)
				_chips[PartSlot.ShoulderR].MoveToFront();
			if (_chips[PartSlot.Backpack].Visible)
				_chips[PartSlot.Backpack].MoveToFront();
		}
	}

	private void RefreshDetails()
	{
		if (_detailsPanel == null)
			return;

		if (_selected == null)
		{
			_detailsPanel.Visible = false;
			return;
		}

		var slot = _selected.Value;
		_detailsPanel.Visible = true;

		var part = GameCatalog.GetPart(_draft.GetPartId(slot));
		if (_detailsPortrait != null)
			_detailsPortrait.Texture = PartPortrait.Get(part, 192);

		if (_detailsTitle != null)
			_detailsTitle.Text = part?.DisplayName ?? $"Empty {SlotLabel(slot)}";

		if (_detailsBody == null)
			return;

		if (part == null)
		{
			_detailsBody.Text = $"{SlotLabel(slot)}\n\nNo part equipped.\nUse Prev / Next to install one.";
			_detailsBody.Modulate = Colors.White;
			return;
		}

		var mfg = GameCatalog.GetManufacturer(part.ManufacturerId);
		var sb = new StringBuilder();
		sb.AppendLine(SlotLabel(slot));
		sb.AppendLine();
		sb.AppendLine($"{mfg.DisplayName}");
		sb.AppendLine(mfg.Niche);
		sb.AppendLine();
		sb.AppendLine(mfg.Blurb);
		sb.AppendLine();

		if (part.Armor != 0) sb.AppendLine($"Armor  {part.Armor:+0;-0}");
		if (part.HullBonus != 0) sb.AppendLine($"Hull  {part.HullBonus:+0;-0}");
		if (part.MaxSpeed != 0) sb.AppendLine($"Speed  {part.MaxSpeed:+0.0;-0.0}");
		if (part.TurnRateDegrees != 0) sb.AppendLine($"Turn  {part.TurnRateDegrees:+0;-0}°");
		if (part.WeaponFamily != WeaponFamily.None)
			sb.AppendLine($"Family  {part.WeaponFamily}");

		if (part.Damage > 0)
		{
			sb.AppendLine($"Damage  {part.Damage:0}");
			sb.AppendLine($"Fire rate  {part.FireRate:0.0}/s");
			sb.AppendLine($"Range  {part.Range:0}");
			sb.AppendLine($"Aim  {part.AimMode}");
			sb.AppendLine($"Heat/shot  {part.HeatPerShot:0.0}");
			sb.AppendLine($"Power draw  {part.PowerLoadWhileFiring:0}");
			if (part.TargetingMode == TargetingMode.AimedComponent)
				sb.AppendLine("Sharpshooter targeting");
		}

		if (slot == PartSlot.Legs)
		{
			sb.AppendLine($"Locomotion  {part.LegType} / {part.LegMode}");
			sb.AppendLine(part.CanSprint
				? $"Sprint  x{part.SprintMultiplier:0.00}  (heat {part.SprintHeatPerSec:0}/s, load {part.SprintPowerLoad:0})"
				: "Sprint  not supported");
		}
		if (slot == PartSlot.Torso)
		{
			sb.AppendLine($"Power housing  Class {part.PowerCoreHousing}");
			sb.AppendLine($"Mounts  {part.ShoulderMountCount} shoulder, {part.BackpackMountCount} backpack");
		}
		if (slot == PartSlot.PowerCore)
		{
			sb.AppendLine($"Core class  {part.PowerCoreClass}");
			sb.AppendLine($"Power capacity  {part.PowerCapacity:0}");
			sb.AppendLine($"Power output  {part.PowerOutput:0}");
		}
		if (slot == PartSlot.Head)
		{
			sb.AppendLine($"Vision  {part.VisionRange:0}m / {part.VisionAngleDeg:0}°");
			sb.AppendLine($"Close targeting  {part.CloseTargeting:0.00}");
			sb.AppendLine($"Scanner  {part.ScannerRange:0}m (res {part.ScannerResolution:0.00})");
		}
		if (part.HeatCapBonus != 0) sb.AppendLine($"Heat cap  +{part.HeatCapBonus:0}");
		if (part.HeatDissipation > 0) sb.AppendLine($"Heat sink  +{part.HeatDissipation:0.0}/s");
		if (part.AbilityKind == AbilityKind.Active)
		{
			sb.AppendLine($"Active  {part.AbilityId} ({part.AbilityCooldown:0.0}s)");
			sb.AppendLine($"Ability load  {part.AbilityPowerLoad:0}  heat {part.AbilityHeatBurst:0}");
		}
		if (part.AbilityKind == AbilityKind.Passive)
			sb.AppendLine($"Passive  {part.AbilityId}" + (part.FireRateBonus > 0 ? $" (+{part.FireRateBonus * 100f:0}% fire)" : ""));

		_detailsBody.Text = sb.ToString().TrimEnd();
		_detailsBody.Modulate = Colors.White;
	}

	private void LayoutPaperdoll()
	{
		if (_dollCanvas == null)
			return;

		var size = _dollCanvas.Size;
		if (size.X < 8 || size.Y < 8)
			return;

		var cx = size.X * 0.5f;
		var cy = size.Y * 0.5f;
		var torsoExpanded = _selected is PartSlot.Torso or PartSlot.ShoulderL or PartSlot.ShoulderR or PartSlot.Backpack;
		var scale = Mathf.Clamp(Mathf.Min(size.X / 520f, size.Y / 420f), 0.68f, 1.05f);

		Place(_chips[PartSlot.Head], cx, cy - 155f * scale, scale);
		Place(_chips[PartSlot.WeaponL], cx - 138f * scale, cy + 8f * scale, scale);
		Place(_chips[PartSlot.WeaponR], cx + 138f * scale, cy + 8f * scale, scale);
		Place(_chips[PartSlot.Torso], cx, cy - 4f * scale, scale * (torsoExpanded ? 1.08f : 1f));
		Place(_chips[PartSlot.Legs], cx, cy + 138f * scale, scale);

		// Core / Systems sit on the outer flanks so they clear shoulders, torso, and arms.
		Place(_chips[PartSlot.PowerCore], cx - 210f * scale, cy + 18f * scale, scale * 0.9f);
		Place(_chips[PartSlot.Systems], cx + 210f * scale, cy + 18f * scale, scale * 0.9f);

		if (_chips[PartSlot.ShoulderL].Visible)
			Place(_chips[PartSlot.ShoulderL], cx - 86f * scale, cy - 72f * scale, scale);
		if (_chips[PartSlot.ShoulderR].Visible)
			Place(_chips[PartSlot.ShoulderR], cx + 86f * scale, cy - 72f * scale, scale);
		// Back sits on the torso body (was too low, floating between torso and legs).
		if (_chips[PartSlot.Backpack].Visible)
			Place(_chips[PartSlot.Backpack], cx, cy + 36f * scale, scale);
	}

	private static void Place(SlotChip chip, float x, float y, float scale = 1f)
	{
		var size = chip.ChipSize * scale;
		chip.CustomMinimumSize = size;
		chip.Size = size;
		chip.Position = new Vector2(x - size.X * 0.5f, y - size.Y * 0.5f);
	}

	private static string SlotLabel(PartSlot slot) => slot switch
	{
		PartSlot.Head => "Head",
		PartSlot.PowerCore => "Power Core",
		PartSlot.Systems => "Systems",
		PartSlot.WeaponL => "Left Arm",
		PartSlot.WeaponR => "Right Arm",
		PartSlot.ShoulderL => "Left Shoulder",
		PartSlot.ShoulderR => "Right Shoulder",
		PartSlot.Backpack => "Backpack",
		_ => slot.ToString()
	};

	private partial class SlotChip : Button
	{
		public PartSlot Slot { get; }
		public Vector2 ChipSize { get; }

		private readonly TextureRect _portrait;
		private readonly Label _caption;
		private readonly Label _name;

		public SlotChip(PartSlot slot, string caption, Vector2 size)
		{
			Slot = slot;
			ChipSize = size;
			CustomMinimumSize = size;
			FocusMode = FocusModeEnum.None;
			Flat = true;
			ClipText = false;

			var root = new VBoxContainer
			{
				MouseFilter = MouseFilterEnum.Ignore
			};
			root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			root.AddThemeConstantOverride("separation", 1);
			AddChild(root);

			_portrait = new TextureRect
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			root.AddChild(_portrait);

			_caption = new Label
			{
				Text = caption,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
				Modulate = MechUiTheme.Muted
			};
			_caption.AddThemeFontSizeOverride("font_size", 11);
			root.AddChild(_caption);

			_name = new Label
			{
				Text = "",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				CustomMinimumSize = new Vector2(0, 16)
			};
			_name.AddThemeFontSizeOverride("font_size", 12);
			root.AddChild(_name);

			ApplyChrome(false, null);
		}

		public void SetPart(PartData? part, bool selected)
		{
			_portrait.Texture = PartPortrait.Get(part, 128);
			var label = ChipLabel(part);
			_name.Text = label;
			TooltipText = part == null
				? $"{_caption.Text}: Empty"
				: $"{_caption.Text}: {part.DisplayName}";
			_name.Modulate = part == null || part.VisualKind == "empty"
				? MechUiTheme.Muted
				: part.Tint.Lerp(Colors.White, 0.45f);
			Color? tint = part == null || part.VisualKind == "empty"
				? null
				: part.Tint.Lerp(MechUiTheme.Border, 0.55f);
			ApplyChrome(selected, tint);
		}

		public void SetDimmed(bool dimmed)
		{
			Modulate = dimmed
				? new Color(1f, 1f, 1f, 0.28f)
				: Colors.White;
			// Still clickable so you can jump to another hardpoint, but quieter.
			MouseDefaultCursorShape = dimmed
				? CursorShape.Arrow
				: CursorShape.PointingHand;
		}

		private static string ChipLabel(PartData? part)
		{
			if (part == null || part.VisualKind == "empty")
				return "Empty";
			return part.DisplayName;
		}

		private void ApplyChrome(bool selected, Color? tint)
		{
			var style = MechUiTheme.MakeChipStyle(selected, tint);
			AddThemeStyleboxOverride("normal", style);
			AddThemeStyleboxOverride("hover", style);
			AddThemeStyleboxOverride("pressed", style);
		}
	}
}
