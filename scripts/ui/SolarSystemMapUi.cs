using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Persistent system campaign: revisit-able operation zones, known drop tables,
/// free-trader markets, manufacturer research, and fabrication.
/// </summary>
public partial class SolarSystemMapUi : Control
{
	private GameSession _session = null!;
	private SolarCampaignRun _run = null!;
	private Control? _map;
	private VBoxContainer? _dossier;
	private Label? _title;
	private Label? _body;
	private VBoxContainer? _actions;
	private Label? _status;
	private readonly Dictionary<string, Button> _markers = new();
	private readonly Dictionary<string, List<ShopOffer>> _merchantStock = new();
	private readonly HashSet<string> _researchExpandedManufacturers = new();
	private readonly HashSet<string> _researchExpandedCategories = new();
	private bool _researchExpandDefaultsSeeded;

	public override void _Ready()
	{
		_session = GetNode<GameSession>("/root/GameSession");
		_run = _session.SolarCampaign;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Campaign);
		Build();
		CallDeferred(nameof(RebuildMarkers));
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_markers.Clear();
		AddChild(new CampaignMapBackdrop());

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		var header = MechUiTheme.MakePanel("SystemHeader", deep: true);
		root.AddChild(header);
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 10);
		header.AddChild(headerRow);
		var headerText = new Label
		{
			Text = $"VOID CORPS  ·  SYSTEM CLAIM CAMPAIGN\n" +
				$"{_run.SelectedCompany?.DisplayName ?? "Independent charter"} · {_run.SettlementStageName} · unlocked locations remain available",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.AccentHot
		};
		headerText.AddThemeFontSizeOverride("font_size", 16);
		headerRow.AddChild(headerText);
		headerRow.AddChild(NavButton("RESEARCH", ShowResearch));
		headerRow.AddChild(NavButton("FABRICATE", ShowFabrication));
		headerRow.AddChild(NavButton("MAIN MENU", () =>
		{
			_session.ReturnToSolarMap = false;
			_session.MatchFromSolarCampaign = false;
			_session.SaveProfile();
			GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
		}));

		var columns = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);

		var mapPanel = MechUiTheme.MakePanel("SystemMap", minWidth: 760f, deep: true);
		mapPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		mapPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddChild(mapPanel);
		_map = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(760, 560)
		};
		mapPanel.AddChild(_map);

		var dossierPanel = MechUiTheme.MakePanel("SystemDossier", minWidth: 390f, deep: true);
		dossierPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddChild(dossierPanel);
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		dossierPanel.AddChild(scroll);
		_dossier = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_dossier.AddThemeConstantOverride("separation", 9);
		scroll.AddChild(_dossier);

		_title = new Label { Text = "SYSTEM OVERVIEW", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_title.AddThemeFontSizeOverride("font_size", 24);
		_title.AddThemeColorOverride("font_color", MechUiTheme.AccentHot);
		_dossier.AddChild(_title);
		_body = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_body.AddThemeFontSizeOverride("font_size", 14);
		_dossier.AddChild(_body);
		_actions = new VBoxContainer();
		_actions.AddThemeConstantOverride("separation", 7);
		_dossier.AddChild(_actions);
		_status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = MechUiTheme.Accent };
		_dossier.AddChild(_status);
		ShowOverview();
	}

	private static Button NavButton(string text, System.Action action)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(130, 40) };
		MechUiTheme.StyleGhostButton(button);
		button.Pressed += action;
		return button;
	}

	private void RebuildMarkers()
	{
		if (_map == null || _map.Size.X < 100f)
			return;
		foreach (var marker in _markers.Values)
			marker.QueueFree();
		_markers.Clear();

		var maxColumn = SolarSystemCatalog.Locations.Max(l => l.Column);
		foreach (var location in SolarSystemCatalog.Locations)
		{
			var unlocked = _run.IsUnlocked(location.Id);
			var marker = new Button
			{
				Text = location.Kind switch
				{
					SolarLocationKind.Merchant => $"◇ {location.DisplayName}",
					SolarLocationKind.Command => $"◆ {location.DisplayName}",
					_ => $"{(unlocked ? "●" : "○")} {location.DisplayName}"
				},
				CustomMinimumSize = new Vector2(155, 44),
				Disabled = !unlocked
			};
			var x = 30f + location.Column / Mathf.Max(1f, maxColumn) * (_map.Size.X - 220f);
			var y = location.Row switch
			{
				0 => 90f,
				1 => _map.Size.Y * 0.48f,
				_ => _map.Size.Y - 140f
			};
			marker.Position = new Vector2(x, y);
			if (location.Kind == SolarLocationKind.Merchant)
				marker.AddThemeColorOverride("font_color", new Color(0.95f, 0.72f, 0.35f));
			else if (unlocked)
				marker.AddThemeColorOverride("font_color", MechUiTheme.Cyan);
			MechUiTheme.StyleGhostButton(marker);
			marker.Pressed += () => ShowLocation(location);
			_map.AddChild(marker);
			_markers[location.Id] = marker;
		}
	}

	private void ShowOverview()
	{
		ClearActions();
		if (_title != null)
			_title.Text = "SYSTEM OVERVIEW";
		if (_body != null)
			_body.Text =
				$"EMPLOYER  {_run.SelectedCompany?.DisplayName ?? "Independent"}\n" +
				$"{_run.SelectedCompany?.Motive}\n\n" +
				$"SETTLEMENT  {_run.SettlementStageName}\n" +
				$"Company mining convoys secured  {_run.MiningConvoysCompleted}\n\n" +
				$"Claim brief\n{VoidCorpsIdentity.CampaignPremise}\n\n" +
				$"Unlocked sites  {_run.UnlockedLocations.Count}/{SolarSystemCatalog.Locations.Count}\n" +
				$"Scrap  {_session.Profile.Scrap}\n" +
				$"Blueprints  {_session.Profile.UnlockedBlueprints.Count}\n\n" +
				"Operation dossiers publish common salvage and rare part sightings. " +
				"Cleared locations remain open for repeat farming.";
	}

	private void ShowLocation(SolarLocationData location)
	{
		ClearActions();
		_run.SelectedLocationId = location.Id;
		_run.Save();
		if (_title != null)
			_title.Text = location.DisplayName;
		if (_status != null)
			_status.Text = "";

		if (location.Kind == SolarLocationKind.Merchant)
		{
			ShowMerchant(location);
			return;
		}
		if (location.Kind == SolarLocationKind.Command)
		{
			if (_body != null)
			{
				var company = _run.SelectedCompany;
				var cost = _run.NextSettlementCost();
				var costText = cost.Count == 0
					? "Charter city established."
					: string.Join(", ", cost.Select(kv => $"{MaterialCatalog.All[kv.Key].DisplayName} {kv.Value}"));
				_body.Text =
					$"{company?.DisplayName ?? "Company"} expedition anchor.\n\n" +
					$"PUBLIC MANDATE\n{company?.PublicPitch}\n\n" +
					$"INTERNAL RISK\n{company?.PrivateTruth}\n\n" +
					$"ESTABLISHMENT ARC\nCurrent: {_run.SettlementStageName}\n" +
					$"Vision: {company?.SettlementVision}\n" +
					$"Mining escorts: {_run.MiningConvoysCompleted}/{_run.NextSettlementConvoyRequirement()}\n" +
					$"Next construction: {costText}\n\n" +
					"Manufacturer names below refer only to licensed technology. They do not own or operate this company.";
			}
			if (_run.SettlementStage < 3)
				_actions?.AddChild(ActionButton("BUILD NEXT SETTLEMENT PHASE", TryAdvanceSettlement, primary: true));
			_actions?.AddChild(ActionButton("OPEN RESEARCH", ShowResearch));
			_actions?.AddChild(ActionButton("OPEN FABRICATION", ShowFabrication));
			return;
		}

		var sb = new StringBuilder();
		sb.AppendLine($"{location.RegionName}  ·  Threat {location.ThreatTier}");
		sb.AppendLine($"Clears: {_run.CompletionCount(location.Id)}");
		sb.AppendLine();
		sb.AppendLine("KNOWN MATERIALS");
		foreach (var id in location.MaterialDrops)
			sb.AppendLine($"  · {MaterialCatalog.All[id].DisplayName}");
		sb.AppendLine();
		sb.AppendLine("COMMON PART SIGHTINGS");
		foreach (var id in location.CommonPartDrops)
			sb.AppendLine($"  · {GameCatalog.GetPart(id)?.DisplayName ?? id}");
		sb.AppendLine();
		sb.AppendLine("RARE SIGHTINGS");
		foreach (var id in location.RarePartDrops)
			sb.AppendLine($"  · {GameCatalog.GetPart(id)?.DisplayName ?? id}");
		if (_body != null)
			_body.Text = sb.ToString();

		for (var i = 0; i < location.Missions.Count; i++)
		{
			var index = i;
			var mission = location.Missions[i];
			_actions?.AddChild(ActionButton($"DEPLOY · {MissionCatalog.Get(mission).Title}", () =>
			{
				if (!_session.LaunchSolarLocation(location.Id, index))
				{
					SetStatus("Unable to deploy with the current MAP.");
					return;
				}
				GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
			}, primary: true));
		}
	}

	private void TryAdvanceSettlement()
	{
		if (!_run.TryAdvanceSettlement(_session.Profile))
		{
			SetStatus("Construction requirements not met. Secure company mining convoys and recover the listed materials.");
			return;
		}
		_session.SaveProfile();
		SfxService.Confirm();
		SetStatus($"Construction complete: {_run.SettlementStageName}.");
		var anchor = SolarSystemCatalog.Get("company_anchor");
		if (anchor != null)
			ShowLocation(anchor);
	}

	private void ShowMerchant(SolarLocationData location)
	{
		if (_body != null)
			_body.Text =
				$"{location.MerchantName}, independent claim-runner and self-made trader.\n\n" +
				"Stock is mixed-brand salvage. This market is not operated by, or affiliated with, any manufacturer.";
		if (!_merchantStock.TryGetValue(location.Id, out var stock))
		{
			stock = ShopService.GenerateStock(
				_session.Profile,
				count: 7,
				manufacturerId: null,
				maxTier: location.ThreatTier);
			_merchantStock[location.Id] = stock;
		}
		foreach (var offer in stock)
		{
			var part = GameCatalog.GetPart(offer.PartId);
			if (part == null)
				continue;
			_actions?.AddChild(ActionButton(
				$"BUY  {part.DisplayName}  ·  {offer.Price} scrap",
				() =>
				{
					if (!ShopService.TryBuy(_session.Profile, offer))
					{
						SetStatus("Not enough scrap.");
						return;
					}
					_session.SaveProfile();
					stock.Remove(offer);
					ShowLocation(location);
					SetStatus($"Purchased {part.DisplayName}. Scrap {_session.Profile.Scrap}.");
				}));
		}

		var sellHeading = new Label
		{
			Text = "SELL SPARES",
			Modulate = MechUiTheme.AccentHot
		};
		sellHeading.AddThemeFontSizeOverride("font_size", 16);
		_actions?.AddChild(sellHeading);
		var equipped = _session.Profile.EquippedInstanceIds.Values.ToHashSet();
		foreach (var instance in _session.Profile.OwnedInstances
			         .Where(i => !i.Reserved && !equipped.Contains(i.InstanceId))
			         .OrderBy(i => GameCatalog.GetPart(i.PartId)?.DisplayName)
			         .ToList())
		{
			var part = GameCatalog.GetPart(instance.PartId);
			if (part == null || part.VisualKind == "empty")
				continue;
			var value = ShopService.SellValue(part, instance.Condition);
			_actions?.AddChild(ActionButton(
				$"SELL  {part.DisplayName}  ·  {instance.Condition.AverageRatio * 100f:0}%  ·  {value} scrap",
				() =>
				{
					if (!ShopService.TrySellInstance(_session.Profile, instance.InstanceId))
						return;
					_session.SaveProfile();
					ShowLocation(location);
					SetStatus($"Sold {part.DisplayName}. Scrap {_session.Profile.Scrap}.");
				}));
		}
	}

	private void ShowResearch()
	{
		ClearActions();
		if (_title != null)
			_title.Text = "MANUFACTURER TECH TREES";
		if (_body != null)
			_body.Text =
				$"Spend scrap to license blueprints. Manufacturers provide technology—not mission shops.\n\nAvailable scrap: {_session.Profile.Scrap}";

		SeedResearchExpandDefaults();

		var toolbar = new HBoxContainer();
		toolbar.AddThemeConstantOverride("separation", 8);
		toolbar.AddChild(ActionButton("EXPAND ALL", ExpandAllResearch));
		toolbar.AddChild(ActionButton("COLLAPSE ALL", CollapseAllResearch));
		_actions?.AddChild(toolbar);

		foreach (var manufacturerId in GameCatalog.Manufacturers.Keys)
		{
			var manufacturer = GameCatalog.GetManufacturer(manufacturerId);
			var nodes = TechTreeService.TreeFor(manufacturerId);
			if (nodes.Count == 0)
				continue;

			var unlockedCount = nodes.Count(n => _session.Profile.HasBlueprint(n.PartId));
			var companyExpanded = _researchExpandedManufacturers.Contains(manufacturerId);
			_actions?.AddChild(MakeCollapseHeader(
				$"{manufacturer.DisplayName.ToUpperInvariant()}  ·  {unlockedCount}/{nodes.Count}",
				manufacturer.AccentColor,
				companyExpanded,
				17,
				() =>
				{
					var expanding = !_researchExpandedManufacturers.Contains(manufacturerId);
					ToggleResearchExpand(_researchExpandedManufacturers, manufacturerId);
					if (expanding)
						EnsureManufacturerCategoriesExpanded(manufacturerId, nodes);
					ShowResearch();
				}));

			if (!companyExpanded)
				continue;

			var companyBody = new VBoxContainer();
			companyBody.AddThemeConstantOverride("separation", 5);
			_actions?.AddChild(WrapIndented(companyBody, 10));

			foreach (var group in nodes
				         .GroupBy(n => GameCatalog.GetPart(n.PartId)!.Slot)
				         .OrderBy(g => ResearchCategoryOrder(g.Key)))
			{
				var categoryKey = $"{manufacturerId}:{group.Key}";
				var categoryExpanded = _researchExpandedCategories.Contains(categoryKey);
				var categoryUnlocked = group.Count(n => _session.Profile.HasBlueprint(n.PartId));
				companyBody.AddChild(MakeCollapseHeader(
					$"{ResearchCategoryLabel(group.Key)}  ·  {categoryUnlocked}/{group.Count()}",
					MechUiTheme.Cyan.Lightened(0.1f),
					categoryExpanded,
					14,
					() =>
					{
						ToggleResearchExpand(_researchExpandedCategories, categoryKey);
						ShowResearch();
					}));

				if (!categoryExpanded)
					continue;

				var categoryBody = new VBoxContainer();
				categoryBody.AddThemeConstantOverride("separation", 5);
				companyBody.AddChild(WrapIndented(categoryBody, 12));

				foreach (var node in group.OrderBy(n => n.Tier).ThenBy(n => GameCatalog.GetPart(n.PartId)?.DisplayName))
				{
					var part = GameCatalog.GetPart(node.PartId)!;
					var unlocked = _session.Profile.HasBlueprint(node.PartId);
					var prerequisite = string.IsNullOrEmpty(node.PrerequisitePartId)
						? ""
						: $" · requires {GameCatalog.GetPart(node.PrerequisitePartId)?.DisplayName}";
					var button = ActionButton(
						unlocked
							? $"✓ T{part.Tier}  {part.DisplayName}"
							: $"UNLOCK T{part.Tier}  {part.DisplayName}  ·  {node.ScrapCost} scrap{prerequisite}",
						() =>
						{
							if (!TechTreeService.TryUnlock(_session.Profile, node))
							{
								SetStatus("Blueprint locked by prerequisite or insufficient scrap.");
								return;
							}
							_session.SaveProfile();
							SetStatus($"Licensed {part.DisplayName}.");
							ShowResearch();
						});
					button.Disabled = unlocked;
					categoryBody.AddChild(button);
				}
			}
		}
	}

	private void ExpandAllResearch()
	{
		_researchExpandDefaultsSeeded = true;
		_researchExpandedManufacturers.Clear();
		_researchExpandedCategories.Clear();
		foreach (var manufacturerId in GameCatalog.Manufacturers.Keys)
		{
			var nodes = TechTreeService.TreeFor(manufacturerId);
			if (nodes.Count == 0)
				continue;
			_researchExpandedManufacturers.Add(manufacturerId);
			EnsureManufacturerCategoriesExpanded(manufacturerId, nodes);
		}
		ShowResearch();
	}

	private void CollapseAllResearch()
	{
		_researchExpandDefaultsSeeded = true;
		_researchExpandedManufacturers.Clear();
		_researchExpandedCategories.Clear();
		ShowResearch();
	}

	private void SeedResearchExpandDefaults()
	{
		if (_researchExpandDefaultsSeeded)
			return;
		_researchExpandDefaultsSeeded = true;

		var firstManufacturer = GameCatalog.Manufacturers.Keys.FirstOrDefault();
		if (string.IsNullOrEmpty(firstManufacturer))
			return;

		_researchExpandedManufacturers.Add(firstManufacturer);
		foreach (var slot in TechTreeService.TreeFor(firstManufacturer)
			         .Select(n => GameCatalog.GetPart(n.PartId)!.Slot)
			         .Distinct())
		{
			_researchExpandedCategories.Add($"{firstManufacturer}:{slot}");
		}
	}

	private void EnsureManufacturerCategoriesExpanded(string manufacturerId, List<TechNode> nodes)
	{
		var anyOpen = nodes.Any(n =>
		{
			var part = GameCatalog.GetPart(n.PartId);
			return part != null && _researchExpandedCategories.Contains($"{manufacturerId}:{part.Slot}");
		});
		if (anyOpen)
			return;

		foreach (var slot in nodes
			         .Select(n => GameCatalog.GetPart(n.PartId)?.Slot)
			         .Where(s => s.HasValue)
			         .Select(s => s!.Value)
			         .Distinct())
		{
			_researchExpandedCategories.Add($"{manufacturerId}:{slot}");
		}
	}

	private static void ToggleResearchExpand(HashSet<string> set, string key)
	{
		if (!set.Add(key))
			set.Remove(key);
	}

	private static Button MakeCollapseHeader(
		string title, Color accent, bool expanded, int fontSize, System.Action onToggle)
	{
		var button = new Button
		{
			Text = $"{(expanded ? "▼" : "▶")}  {title}",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0, 34),
			Modulate = accent
		};
		button.AddThemeFontSizeOverride("font_size", fontSize);
		MechUiTheme.StyleGhostButton(button);
		button.Pressed += () =>
		{
			SfxService.Click();
			onToggle();
		};
		return button;
	}

	private static MarginContainer WrapIndented(Control child, int left)
	{
		var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		margin.AddThemeConstantOverride("margin_left", left);
		margin.AddChild(child);
		return margin;
	}

	private static int ResearchCategoryOrder(PartSlot slot) => slot switch
	{
		PartSlot.Legs => 0,
		PartSlot.Torso => 1,
		PartSlot.Head => 2,
		PartSlot.PowerCore => 3,
		PartSlot.WeaponL => 4,
		PartSlot.WeaponR => 5,
		PartSlot.ShoulderL => 6,
		PartSlot.ShoulderR => 7,
		PartSlot.Backpack => 8,
		PartSlot.Systems => 9,
		_ => 99
	};

	private static string ResearchCategoryLabel(PartSlot slot) => slot switch
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

	private void ShowFabrication()
	{
		ClearActions();
		if (_title != null)
			_title.Text = "COMPONENT FABRICATION";
		var inventory = string.Join("  ·  ", MaterialCatalog.All.Values.Select(m =>
			$"{m.DisplayName} {_session.Profile.MaterialCount(m.Id)}"));
		if (_body != null)
			_body.Text = $"Licensed designs must be built as physical copies.\n\n{inventory}";

		foreach (var partId in _session.Profile.UnlockedBlueprints.OrderBy(id => GameCatalog.GetPart(id)?.Tier).ThenBy(id => id))
		{
			var part = GameCatalog.GetPart(partId);
			if (part == null || part.VisualKind == "empty")
				continue;
			var recipe = string.Join(", ", MaterialCatalog.RecipeFor(part).Select(kv =>
				$"{MaterialCatalog.All[kv.Key].DisplayName} ×{kv.Value}"));
			var button = ActionButton($"BUILD  {part.DisplayName}\n{recipe}", () =>
			{
				if (!FabricationService.TryBuild(_session.Profile, part.Id))
				{
					SetStatus("Insufficient fabrication materials.");
					return;
				}
				_session.SaveProfile();
				SetStatus($"Fabricated {part.DisplayName}. Physical copy added to inventory.");
				ShowFabrication();
			});
			button.Disabled = !FabricationService.CanBuild(_session.Profile, part.Id);
			_actions?.AddChild(button);
		}
	}

	private static Button ActionButton(string text, System.Action action, bool primary = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 42),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		if (primary)
			MechUiTheme.StylePrimaryButton(button);
		else
			MechUiTheme.StyleGhostButton(button);
		button.Pressed += () =>
		{
			SfxService.Click();
			action();
		};
		return button;
	}

	private void ClearActions()
	{
		if (_actions != null)
		{
			foreach (var child in _actions.GetChildren())
				child.QueueFree();
		}
		if (_status != null)
			_status.Text = "";
	}

	private void SetStatus(string text)
	{
		if (_status != null)
			_status.Text = text;
	}
}
