using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Post-skirmish results + shop. Narrow exchange list, equipped compare panel, centered part bay.
/// </summary>
public partial class ResultsShopUi : Control
{
	private enum ExchangeMode
	{
		Buy,
		Sell
	}

	private PlayerProfile _profile = null!;
	private MatchSession _match = null!;
	private GameSession _session = null!;
	private List<ShopOffer> _stock = new();
	private ExchangeMode _mode = ExchangeMode.Buy;
	private string? _selectedPartId;
	private ShopOffer? _selectedOffer;
	private bool _committed;
	private string _shopManufacturerId = "";
	private bool _conventionDebrief;
	private bool _bossDebrief;
	private bool _shopDisabled;

	private Label? _telemetry;
	private Label? _manifestBody;
	private Label? _listSection;
	private VBoxContainer? _list;
	private Button? _buyToggle;
	private Button? _sellToggle;

	private PanelContainer? _detailsPanel;
	private TextureRect? _detailsPortrait;
	private Label? _detailsTitle;
	private Label? _detailsBody;
	private Button? _detailsAction;
	private Label? _detailsHint;

	private RichTextLabel? _compareBody;
	private TextureRect? _compareSelectedThumb;
	private TextureRect? _compareEquippedThumb;
	private Label? _compareSelectedName;
	private Label? _compareEquippedName;
	private Label? _compareSelectedSlot;
	private Label? _compareEquippedSlot;

	public void Open(GameSession session)
	{
		_session = session;
		_profile = session.Profile;
		_match = session.Match;
		_conventionDebrief = session.MatchFromConvention;
		_bossDebrief = _match.MissionType == MissionType.BossEncounter;
		// Field exchanges no longer appear after every mission. Parts are sold
		// only by independent merchants at dedicated map locations.
		_shopDisabled = true;
		_shopManufacturerId = "";
		if (!_session.MatchRewardsCommitted)
			_session.CommitMatchRewards();
		_committed = true;

		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Results);
		_stock = _shopDisabled
			? new List<ShopOffer>()
			: ShopService.GenerateStock(
				_profile,
				manufacturerId: _shopManufacturerId,
				maxTier: session.CurrentMaxLootTier());
		_mode = ExchangeMode.Buy;
		_selectedPartId = null;
		_selectedOffer = null;
		Build();
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		AddChild(MechUiTheme.MakeDimOverlay());

		var margins = new MarginContainer();
		margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margins.AddThemeConstantOverride("margin_left", 36);
		margins.AddThemeConstantOverride("margin_top", 28);
		margins.AddThemeConstantOverride("margin_right", 36);
		margins.AddThemeConstantOverride("margin_bottom", 28);
		AddChild(margins);

		var root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 10);
		margins.AddChild(root);

		BuildBanner(root);

		var columns = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);

		BuildManifestColumn(columns);
		if (!_shopDisabled)
			BuildExchangeColumn(columns);

		if (!_shopDisabled)
			BuildDetailsBay(root);
		BuildNav(root);

		RefreshTelemetry();
		RefreshManifest();
		if (!_shopDisabled)
		{
			RefreshModeChrome();
			RebuildList();
			RefreshDetails();
		}
	}

	private void BuildBanner(VBoxContainer root)
	{
		var victory = _match.Outcome == MatchOutcome.Victory;
		var banner = MechUiTheme.MakePanel("OutcomeBanner", deep: true);
		banner.AddThemeStyleboxOverride("panel", MechUiTheme.MakePanelStyle(
			margin: 14f,
			deep: true,
			border: victory ? MechUiTheme.Success : MechUiTheme.Danger));
		root.AddChild(banner);

		var bannerInner = new VBoxContainer();
		bannerInner.AddThemeConstantOverride("separation", 4);
		banner.AddChild(bannerInner);

		var shopLine = _bossDebrief
			? "TITAN CLAIM DEBRIEF"
			: _conventionDebrief
				? "CONVENTION TRIAL DEBRIEF"
			: string.IsNullOrEmpty(_shopManufacturerId)
			? "FIELD DEBRIEF"
			: $"{GameCatalog.GetManufacturer(_shopManufacturerId).DisplayName.ToUpperInvariant()} FIELD EXCHANGE";
		var brand = new Label
		{
			Text = $"VOID CORPS  ·  {shopLine}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent.Darkened(0.1f)
		};
		brand.AddThemeFontSizeOverride("font_size", 12);
		bannerInner.AddChild(brand);

		var header = new Label
		{
			Text = _conventionDebrief
				? victory ? "TRIAL PASSED" : "TRIAL FAILED"
				: _bossDebrief
					? victory ? "TITAN DESTROYED" : "CLAIM LOST"
				: victory ? "CLAIM SECURED" : "DETACHMENT LOST",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = victory ? MechUiTheme.Success : MechUiTheme.Danger
		};
		header.AddThemeFontSizeOverride("font_size", 30);
		bannerInner.AddChild(header);

		_telemetry = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Cyan
		};
		_telemetry.AddThemeFontSizeOverride("font_size", 15);
		bannerInner.AddChild(_telemetry);
	}

	private void BuildManifestColumn(HBoxContainer columns)
	{
		var panel = MechUiTheme.MakePanel("Manifest", 300);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		if (_shopDisabled)
			panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.CustomMinimumSize = new Vector2(300, 0);
		columns.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 10);
		panel.AddChild(inner);
		inner.AddChild(MechUiTheme.MakeSectionLabel("MANIFEST"));

		_manifestBody = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.Text
		};
		_manifestBody.AddThemeFontSizeOverride("font_size", 14);
		inner.AddChild(_manifestBody);
	}

	private void BuildExchangeColumn(HBoxContainer columns)
	{
		var exchange = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		exchange.AddThemeConstantOverride("separation", 14);
		columns.AddChild(exchange);

		BuildListingPanel(exchange);
		BuildComparePanel(exchange);
	}

	private void BuildListingPanel(HBoxContainer exchange)
	{
		// Narrow stock list — content-width cards, not full-bleed rows.
		var panel = MechUiTheme.MakePanel("ExchangeList", 420);
		panel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.CustomMinimumSize = new Vector2(420, 0);
		exchange.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 10);
		panel.AddChild(inner);

		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 10);
		inner.AddChild(headerRow);

		if (!string.IsNullOrEmpty(_shopManufacturerId))
		{
			var mfg = GameCatalog.GetManufacturer(_shopManufacturerId);
			var stockMark = ManufacturerBrand.MakeEmblemOrFallback(_shopManufacturerId, mfg.AccentColor, 28f);
			stockMark.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			headerRow.AddChild(stockMark);
		}

		var sectionTitle = string.IsNullOrEmpty(_shopManufacturerId)
			? "FIELD EXCHANGE"
			: $"{GameCatalog.GetManufacturer(_shopManufacturerId).DisplayName.ToUpperInvariant()} STOCK";
		_listSection = MechUiTheme.MakeSectionLabel(sectionTitle);
		_listSection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		headerRow.AddChild(_listSection);

		_buyToggle = new Button { Text = "BUY", CustomMinimumSize = new Vector2(72, 30) };
		_buyToggle.Pressed += () => SetMode(ExchangeMode.Buy);
		headerRow.AddChild(_buyToggle);

		_sellToggle = new Button { Text = "SELL", CustomMinimumSize = new Vector2(72, 30) };
		_sellToggle.Pressed += () => SetMode(ExchangeMode.Sell);
		headerRow.AddChild(_sellToggle);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			CustomMinimumSize = new Vector2(0, 240)
		};
		inner.AddChild(scroll);

		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_list);
	}

	private void BuildComparePanel(HBoxContainer exchange)
	{
		var panel = MechUiTheme.MakePanel("Compare");
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		exchange.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 10);
		panel.AddChild(inner);
		inner.AddChild(MechUiTheme.MakeSectionLabel("COMPARE"));

		var heads = new HBoxContainer();
		heads.AddThemeConstantOverride("separation", 16);
		inner.AddChild(heads);

		heads.AddChild(MakeCompareSide(
			"SELECTED",
			out _compareSelectedThumb,
			out _compareSelectedName,
			out _compareSelectedSlot));

		var vs = new Label
		{
			Text = "VS",
			VerticalAlignment = VerticalAlignment.Center,
			Modulate = MechUiTheme.Accent,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		vs.AddThemeFontSizeOverride("font_size", 14);
		heads.AddChild(vs);

		heads.AddChild(MakeCompareSide(
			"EQUIPPED",
			out _compareEquippedThumb,
			out _compareEquippedName,
			out _compareEquippedSlot));

		_compareBody = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 160),
			Modulate = MechUiTheme.Text
		};
		_compareBody.AddThemeFontSizeOverride("normal_font_size", 13);
		inner.AddChild(_compareBody);
	}

	private static Control MakeCompareSide(
		string caption,
		out TextureRect thumb,
		out Label name,
		out Label slotLabel)
	{
		var col = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		col.AddThemeConstantOverride("separation", 4);

		var cap = new Label
		{
			Text = caption,
			Modulate = MechUiTheme.Muted
		};
		cap.AddThemeFontSizeOverride("font_size", 11);
		col.AddChild(cap);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		col.AddChild(row);

		thumb = new TextureRect
		{
			CustomMinimumSize = new Vector2(56, 56),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = PartPortrait.GetEmpty(64),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddChild(thumb);

		var textCol = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		textCol.AddThemeConstantOverride("separation", 2);
		row.AddChild(textCol);

		name = new Label
		{
			Text = "—",
			Modulate = MechUiTheme.AccentHot,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		name.AddThemeFontSizeOverride("font_size", 14);
		textCol.AddChild(name);

		slotLabel = new Label
		{
			Text = "—",
			Modulate = MechUiTheme.Cyan
		};
		slotLabel.AddThemeFontSizeOverride("font_size", 11);
		textCol.AddChild(slotLabel);

		return col;
	}

	private void BuildDetailsBay(VBoxContainer root)
	{
		// Centered bay — no longer full-bleed across the debrief.
		var wrap = new CenterContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		root.AddChild(wrap);

		_detailsPanel = MechUiTheme.MakePanel("PartBay");
		_detailsPanel.CustomMinimumSize = new Vector2(760, 156);
		_detailsPanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		wrap.AddChild(_detailsPanel);

		var inner = new HBoxContainer();
		inner.AddThemeConstantOverride("separation", 16);
		_detailsPanel.AddChild(inner);

		var left = new VBoxContainer();
		left.AddThemeConstantOverride("separation", 6);
		inner.AddChild(left);
		left.AddChild(MechUiTheme.MakeSectionLabel("PART BAY"));

		_detailsPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(88, 88),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		left.AddChild(_detailsPortrait);

		var mid = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		mid.AddThemeConstantOverride("separation", 4);
		inner.AddChild(mid);

		_detailsTitle = new Label
		{
			Text = "Select a part",
			Modulate = MechUiTheme.AccentHot
		};
		_detailsTitle.AddThemeFontSizeOverride("font_size", 18);
		mid.AddChild(_detailsTitle);

		_detailsBody = new Label
		{
			Text = "Click an exchange listing to inspect specs before buying.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.Muted
		};
		_detailsBody.AddThemeFontSizeOverride("font_size", 13);
		mid.AddChild(_detailsBody);

		var right = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		right.AddThemeConstantOverride("separation", 8);
		inner.AddChild(right);

		_detailsAction = new Button
		{
			Text = "BUY",
			CustomMinimumSize = new Vector2(132, 44),
			Disabled = true
		};
		_detailsAction.AddThemeFontSizeOverride("font_size", 16);
		MechUiTheme.StylePrimaryButton(_detailsAction);
		_detailsAction.Pressed += OnDetailsAction;
		right.AddChild(_detailsAction);

		_detailsHint = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		_detailsHint.AddThemeFontSizeOverride("font_size", 11);
		right.AddChild(_detailsHint);
	}

	private void BuildNav(VBoxContainer root)
	{
		var navRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		navRow.AddThemeConstantOverride("separation", 14);
		root.AddChild(navRow);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var academyGrad = session is { OpenAcademyGraduation: true };
		var academyNext = session is { LaunchAcademyContinue: true };
		var conventionContinue = session is { ReturnToConventionHall: true }
			|| session is { InConvention: true, MatchFromConvention: true };
		var campaignContinue = session is { InCampaign: true, ReturnToCampaignMap: true };
		var solarContinue = session is { MatchFromSolarCampaign: true, ReturnToSolarMap: true };
		var runFinished = session is { MatchFromCampaign: true } && !campaignContinue;
		var cont = new Button
		{
			Text = academyGrad
				? "Graduation"
				: academyNext
					? "Next Academy Beat"
					: conventionContinue
						? "Return to Convention"
						: solarContinue
							? "Return to System Map"
						: campaignContinue
							? "Continue Roguelike"
							: runFinished
								? "Finish Run"
								: "Continue",
			CustomMinimumSize = new Vector2(240, 46)
		};
		cont.AddThemeFontSizeOverride("font_size", 17);
		MechUiTheme.StylePrimaryButton(cont);
		cont.Pressed += () =>
		{
			SfxService.Confirm();
			var s = GetNodeOrNull<GameSession>("/root/GameSession");
			s?.SaveProfile();
			if (s is { OpenAcademyGraduation: true })
			{
				s.OpenAcademyGraduation = false;
				s.MatchFromAcademy = false;
				GetTree().ChangeSceneToFile("res://scenes/academy_graduation.tscn");
				return;
			}

			if (s is { LaunchAcademyContinue: true })
			{
				s.LaunchAcademyContinue = false;
				if (s.Campaign?.AcademyStep == AcademyStep.LiveFire)
					s.BeginAcademyLiveFire();
				else
					s.BeginAcademyRange();
				GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
				return;
			}

			if (s is { ReturnToConventionHall: true } || s is { MatchFromConvention: true })
			{
				s.ReturnToConventionHall = false;
				s.MatchFromConvention = false;
				GetTree().ChangeSceneToFile("res://scenes/convention_hall.tscn");
				return;
			}

			if (s is { MatchFromSolarCampaign: true, ReturnToSolarMap: true })
			{
				s.MatchFromSolarCampaign = false;
				s.ReturnToSolarMap = false;
				GetTree().ChangeSceneToFile("res://scenes/solar_system_map.tscn");
				return;
			}

			if (s is { InCampaign: true, ReturnToCampaignMap: true })
			{
				GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
				return;
			}

			if (s != null)
			{
				// Finale / failed campaign run — campaign already cleared; hangar menu is correct.
				if (s.MatchFromCampaign)
				{
					s.MatchFromCampaign = false;
					s.ReturnToCampaignMap = false;
					s.OpenSkirmishSetupOnMenu = false;
				}
				else
				{
					s.OpenSkirmishSetupOnMenu = true;
				}
				s.ActivateMainProfile();
			}

			GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
		};
		navRow.AddChild(cont);

		var menu = new Button
		{
			Text = "Return to Main Menu",
			CustomMinimumSize = new Vector2(240, 46)
		};
		MechUiTheme.StyleGhostButton(menu);
		menu.Pressed += () =>
		{
			SfxService.Click();
			var s = GetNodeOrNull<GameSession>("/root/GameSession");
			s?.SaveProfile();
			if (s != null)
			{
				s.ReturnToCampaignMap = false;
				s.MatchFromCampaign = false;
				s.MatchFromSolarCampaign = false;
				s.ReturnToSolarMap = false;
				s.MatchFromAcademy = false;
				s.MatchFromConvention = false;
				s.ReturnToConventionHall = false;
				s.LaunchAcademyContinue = false;
				s.OpenAcademyGraduation = false;
				s.ActivateMainProfile();
			}
			GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
		};
		navRow.AddChild(menu);
	}

	private void SetMode(ExchangeMode mode)
	{
		if (_mode == mode)
			return;
		_mode = mode;
		_selectedPartId = null;
		_selectedOffer = null;
		SfxService.Click();
		RefreshModeChrome();
		RebuildList();
		RefreshDetails();
	}

	private void RefreshModeChrome()
	{
		if (_listSection != null)
			_listSection.Text = _mode == ExchangeMode.Buy ? "// FIELD EXCHANGE" : "// SALVAGE BAY";

		if (_buyToggle != null)
		{
			if (_mode == ExchangeMode.Buy)
				MechUiTheme.StylePrimaryButton(_buyToggle);
			else
				MechUiTheme.StyleGhostButton(_buyToggle);
		}

		if (_sellToggle != null)
		{
			if (_mode == ExchangeMode.Sell)
				MechUiTheme.StylePrimaryButton(_sellToggle);
			else
				MechUiTheme.StyleGhostButton(_sellToggle);
		}
	}

	private void RefreshTelemetry()
	{
		if (_telemetry == null)
			return;
		_telemetry.Text =
			$"Run scrap {_match.RunScrap}   ·   Profile scrap {_profile.Scrap}   ·   " +
			$"Lives {_profile.LivesBank}   ·   Parts {_profile.OwnedCopyCount}\n" +
			_match.Telemetry.BuildSummary();
	}

	private void RefreshManifest()
	{
		if (_manifestBody == null)
			return;

		var drops = _match.RunPartDrops.Count == 0
			? "No part drops recovered."
			: string.Join("\n", _match.RunPartDrops.Select(id =>
			{
				var part = GameCatalog.GetPart(id);
				return $"  ·  {part?.DisplayName ?? id}  ({part?.Slot.ToString() ?? "?"})";
			}));

		var materials = _match.RunMaterialDrops.Count == 0
			? "No fabrication materials recovered."
			: string.Join("\n", _match.RunMaterialDrops.Select(kv =>
				$"  ·  {MaterialCatalog.All.GetValueOrDefault(kv.Key)?.DisplayName ?? kv.Key}  ×{kv.Value}"));
		_manifestBody.Text =
			$"Outcome\n  {_match.Outcome}\n\n" +
			$"Run scrap banked\n  {_match.RunScrap}\n\n" +
			$"Merc corps\n  {_profile.MercCorpName}\n\n" +
			"Resupply\n  Independent merchants operate from marked system-map locations.\n\n" +
			$"Recovered parts\n{drops}\n\n" +
			$"Fabrication salvage\n{materials}";
	}

	private void RebuildList()
	{
		if (_list == null)
			return;
		foreach (var child in _list.GetChildren())
			child.QueueFree();

		if (_mode == ExchangeMode.Buy)
			RebuildBuyList();
		else
			RebuildSellList();
	}

	private void RebuildBuyList()
	{
		if (_list == null)
			return;

		if (_stock.Count == 0)
		{
			_list.AddChild(MakeEmptyLabel("No exchange stock this cycle."));
			return;
		}

		foreach (var offer in _stock)
		{
			var part = GameCatalog.GetPart(offer.PartId);
			if (part == null)
				continue;

			var copies = _profile.OwnedCount(offer.PartId);
			var selected = _selectedPartId == offer.PartId;
			var card = MakePartCard(
				part,
				meta: $"{CatalogTiers.ShortLabel(part.Tier)}   ·   {part.Slot}   ·   {offer.Price} scrap"
				      + (copies > 0 ? $"   ·   owned ×{copies}" : ""),
				selected: selected,
				onSelect: () => SelectBuyOffer(offer));
			_list.AddChild(card);
		}
	}

	private void RebuildSellList()
	{
		if (_list == null)
			return;

		var sellables = _profile.OwnedCounts.Keys
			.Where(id => _profile.SpareCount(id) > 0)
			.Select(id => GameCatalog.GetPart(id))
			.Where(part => part != null && part.VisualKind != "empty")
			.Cast<PartData>()
			.OrderBy(p => p.Slot.ToString())
			.ThenBy(p => p.DisplayName)
			.ToList();

		if (sellables.Count == 0)
		{
			_list.AddChild(MakeEmptyLabel("No spare salvage to liquidate."));
			return;
		}

		foreach (var part in sellables)
		{
			var spare = _profile.SpareCount(part.Id);
			var value = ShopService.SellValue(part);
			var selected = _selectedPartId == part.Id;
			var card = MakePartCard(
				part,
				meta: $"{CatalogTiers.ShortLabel(part.Tier)}   ·   {part.Slot}   ·   spare ×{spare}   ·   sell {value} scrap",
				selected: selected,
				onSelect: () => SelectSellPart(part.Id));
			_list.AddChild(card);
		}
	}

	private static Label MakeEmptyLabel(string text)
	{
		var empty = new Label { Text = text, Modulate = MechUiTheme.Muted };
		empty.AddThemeFontSizeOverride("font_size", 14);
		return empty;
	}

	private Control MakePartCard(PartData part, string meta, bool selected, System.Action onSelect)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop
		};
		card.AddThemeStyleboxOverride("panel", MechUiTheme.MakePanelStyle(
			10f,
			deep: true,
			border: selected ? MechUiTheme.AccentHot : MechUiTheme.BorderDim));

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 12);
		card.AddChild(row);

		var portrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(52, 52),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = PartThumbnail.Get(part, 80),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddChild(portrait);

		var info = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		info.AddThemeConstantOverride("separation", 2);
		row.AddChild(info);

		var name = new Label
		{
			Text = part.DisplayName,
			Modulate = MechUiTheme.AccentHot,
			MouseFilter = MouseFilterEnum.Ignore
		};
		name.AddThemeFontSizeOverride("font_size", 16);
		info.AddChild(name);

		var metaLabel = new Label
		{
			Text = meta,
			Modulate = MechUiTheme.Muted,
			MouseFilter = MouseFilterEnum.Ignore
		};
		metaLabel.AddThemeFontSizeOverride("font_size", 12);
		info.AddChild(metaLabel);

		var mfg = GameCatalog.GetManufacturer(part.ManufacturerId);
		var mfgRow = new HBoxContainer();
		mfgRow.AddThemeConstantOverride("separation", 6);
		info.AddChild(mfgRow);

		var mfgMark = ManufacturerBrand.MakeEmblemOrFallback(part.ManufacturerId, mfg.AccentColor, 18f);
		mfgMark.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		mfgRow.AddChild(mfgMark);

		var niche = new Label
		{
			Text = mfg.DisplayName,
			Modulate = MechUiTheme.Cyan,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		niche.AddThemeFontSizeOverride("font_size", 12);
		mfgRow.AddChild(niche);

		card.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				onSelect();
				card.AcceptEvent();
			}
		};

		return card;
	}

	private void SelectBuyOffer(ShopOffer offer)
	{
		_selectedOffer = offer;
		_selectedPartId = offer.PartId;
		SfxService.Click();
		RebuildList();
		RefreshDetails();
	}

	private void SelectSellPart(string partId)
	{
		_selectedOffer = null;
		_selectedPartId = partId;
		SfxService.Click();
		RebuildList();
		RefreshDetails();
	}

	private void RefreshDetails()
	{
		if (_detailsPortrait == null || _detailsTitle == null || _detailsBody == null
		    || _detailsAction == null || _detailsHint == null)
			return;

		var part = string.IsNullOrEmpty(_selectedPartId) ? null : GameCatalog.GetPart(_selectedPartId);
		if (part == null)
		{
			_detailsPortrait.Texture = PartPortrait.GetEmpty(96);
			_detailsTitle.Text = "Select a part";
			_detailsBody.Text = _mode == ExchangeMode.Buy
				? "Click an exchange listing to inspect specs before buying."
				: "Click a salvage listing to inspect it before selling.";
			_detailsBody.Modulate = MechUiTheme.Muted;
			_detailsAction.Disabled = true;
			_detailsAction.Text = _mode == ExchangeMode.Buy ? "BUY" : "SELL";
			_detailsHint.Text = "";
			RefreshCompare(null);
			return;
		}

		_detailsPortrait.Texture = PartThumbnail.Get(part, 128);
		_detailsTitle.Text = $"{part.DisplayName}  ·  {CatalogTiers.ShortLabel(part.Tier)}";
		_detailsBody.Text = BuildPartBlurb(part);
		_detailsBody.Modulate = MechUiTheme.Text;

		if (_mode == ExchangeMode.Buy)
		{
			var offer = _selectedOffer ?? _stock.FirstOrDefault(o => o.PartId == part.Id);
			var price = offer?.Price ?? ShopService.PriceFor(part);
			var canBuy = offer != null && _profile.Scrap >= price;
			_detailsAction.Text = _profile.OwnedCount(part.Id) > 0 ? "BUY +" : "BUY";
			_detailsAction.Disabled = !canBuy;
			_detailsHint.Text = canBuy
				? $"{price} scrap"
				: offer == null ? "Unavailable" : $"Need {price} scrap";
		}
		else
		{
			var spare = _profile.SpareCount(part.Id);
			var value = ShopService.SellValue(part);
			_detailsAction.Text = "SELL";
			_detailsAction.Disabled = spare <= 0;
			_detailsHint.Text = spare > 0 ? $"+{value} scrap  ·  spare ×{spare}" : "No spare copies";
		}

		RefreshCompare(part);
	}

	private void RefreshCompare(PartData? selected)
	{
		if (_compareBody == null
		    || _compareSelectedThumb == null || _compareEquippedThumb == null
		    || _compareSelectedName == null || _compareEquippedName == null
		    || _compareSelectedSlot == null || _compareEquippedSlot == null)
			return;

		if (selected == null)
		{
			_compareSelectedThumb.Texture = PartPortrait.GetEmpty(64);
			_compareEquippedThumb.Texture = PartPortrait.GetEmpty(64);
			_compareSelectedName.Text = "—";
			_compareEquippedName.Text = "—";
			_compareSelectedSlot.Text = "Pick a listing";
			_compareEquippedSlot.Text = "—";
			_compareBody.Text = "[color=#94A3B0]Select a part to compare it against the kit currently mounted in that slot.[/color]";
			return;
		}

		var equipped = GetEquippedPeer(selected, out var equippedSlotLabel);
		_compareSelectedThumb.Texture = PartThumbnail.Get(selected, 96);
		_compareSelectedName.Text = selected.DisplayName;
		_compareSelectedSlot.Text = SlotShort(selected.Slot);

		if (equipped == null || equipped.Id == selected.Id)
		{
			_compareEquippedThumb.Texture = equipped == null
				? PartPortrait.GetEmpty(64)
				: PartThumbnail.Get(equipped, 96);
			_compareEquippedName.Text = equipped == null ? "Empty mount" : equipped.DisplayName;
			_compareEquippedSlot.Text = equippedSlotLabel;
			_compareBody.Text = equipped == null
				? $"[color=#94A3B0]No part equipped in {equippedSlotLabel}. Buying this fills an empty mount.[/color]"
				: "[color=#73C7EB]Same part already mounted — buying adds a spare copy.[/color]";
			return;
		}

		_compareEquippedThumb.Texture = PartThumbnail.Get(equipped, 96);
		_compareEquippedName.Text = equipped.DisplayName;
		_compareEquippedSlot.Text = equippedSlotLabel;
		_compareBody.Text = BuildCompareBlurb(selected, equipped);
	}

	private PartData? GetEquippedPeer(PartData selected, out string slotLabel)
	{
		var loadout = _profile.Loadout;
		if (selected.Slot is PartSlot.WeaponL or PartSlot.WeaponR)
		{
			var left = GameCatalog.GetPart(loadout.WeaponLId);
			var right = GameCatalog.GetPart(loadout.WeaponRId);
			var leftLive = left is { VisualKind: not "empty" };
			var rightLive = right is { VisualKind: not "empty" };
			if (leftLive)
			{
				slotLabel = "L ARM";
				return left;
			}

			if (rightLive)
			{
				slotLabel = "R ARM";
				return right;
			}

			slotLabel = "ARM";
			return null;
		}

		slotLabel = SlotShort(selected.Slot);
		var part = GameCatalog.GetPart(loadout.GetPartId(selected.Slot));
		if (part == null || part.VisualKind == "empty")
			return null;
		return part;
	}

	private static string SlotShort(PartSlot slot) => slot switch
	{
		PartSlot.WeaponL => "L ARM",
		PartSlot.WeaponR => "R ARM",
		PartSlot.PowerCore => "POWER CORE",
		PartSlot.ShoulderL => "L SHOULDER",
		PartSlot.ShoulderR => "R SHOULDER",
		PartSlot.Backpack => "BACK",
		PartSlot.Systems => "SYSTEMS",
		_ => slot.ToString().ToUpperInvariant()
	};

	private static string BuildCompareBlurb(PartData selected, PartData equipped)
	{
		var sb = new StringBuilder();
		sb.AppendLine("[color=#D4B56A]DELTA  (selected − equipped)[/color]");
		AppendDelta(sb, "Structure", selected.StructureHp, equipped.StructureHp, "0");
		AppendDelta(sb, "Armor", selected.Armor, equipped.Armor, "0");
		AppendDelta(sb, "Weight", selected.Weight, equipped.Weight, "0", invertGood: true);
		AppendDelta(sb, "Power req", selected.PowerRequirement, equipped.PowerRequirement, "0", invertGood: true);

		if (selected.Slot is PartSlot.WeaponL or PartSlot.WeaponR
		    || equipped.Slot is PartSlot.WeaponL or PartSlot.WeaponR)
		{
			if (selected.WeaponFamily != WeaponFamily.None || equipped.WeaponFamily != WeaponFamily.None)
				sb.AppendLine($"  Family  {selected.WeaponFamily}  ←  {equipped.WeaponFamily}");
			AppendDelta(sb, "Damage", selected.Damage, equipped.Damage, "0");
			AppendDelta(sb, selected.WeaponFamily == WeaponFamily.Melee ? "Reach" : "Range",
				selected.Range, equipped.Range, "0.0");
			var meleeComparison = selected.WeaponFamily == WeaponFamily.Melee
			                      || equipped.WeaponFamily == WeaponFamily.Melee;
			AppendDelta(sb, meleeComparison ? "Contact rate" : "Fire rate",
				selected.FireRate, equipped.FireRate, "0.0");
			AppendDelta(sb, meleeComparison ? "Heat / contact" : "Heat / shot",
				selected.HeatPerShot, equipped.HeatPerShot, "0", invertGood: true);
			AppendDelta(sb, meleeComparison ? "Power / contact" : "Power / shot",
				selected.PowerPerShot, equipped.PowerPerShot, "0", invertGood: true);
			if (selected.IsHeldShield || equipped.IsHeldShield)
			{
				AppendDelta(sb, "Shield arc", selected.ShieldArcDegrees, equipped.ShieldArcDegrees, "0");
				AppendDelta(sb, "Raise /s", selected.ShieldPowerPerSec, equipped.ShieldPowerPerSec, "0", invertGood: true);
			}
		}

		if (selected.Slot == PartSlot.PowerCore || equipped.Slot == PartSlot.PowerCore)
		{
			AppendDelta(sb, "Capacity", selected.PowerCapacity, equipped.PowerCapacity, "0");
			AppendDelta(sb, "Gen /s", selected.PowerOutput, equipped.PowerOutput, "0.0");
			if (selected.PowerCoreClass != equipped.PowerCoreClass)
				sb.AppendLine($"  Core class  {selected.PowerCoreClass}  ←  {equipped.PowerCoreClass}");
		}

		if (selected.Slot == PartSlot.Legs || equipped.Slot == PartSlot.Legs)
		{
			AppendDelta(sb, "Speed", selected.MaxSpeed, equipped.MaxSpeed, "0.0");
			AppendDelta(sb, "Turn", selected.TurnRateDegrees, equipped.TurnRateDegrees, "0");
			AppendDelta(sb, "Load rating", selected.LoadRating, equipped.LoadRating, "0");
		}

		if (selected.Slot == PartSlot.Head || equipped.Slot == PartSlot.Head)
		{
			AppendDelta(sb, "Vision", selected.VisionRange, equipped.VisionRange, "0.0");
			AppendDelta(sb, "Scan", selected.ScannerRange, equipped.ScannerRange, "0.0");
		}

		AppendDelta(sb, "Heat cap", selected.HeatCapBonus, equipped.HeatCapBonus, "0");
		AppendDelta(sb, "Sink /s", selected.HeatDissipation, equipped.HeatDissipation, "0.0");

		if (selected.Tier != equipped.Tier)
			sb.AppendLine($"  Tier  {CatalogTiers.ShortLabel(selected.Tier)}  ←  {CatalogTiers.ShortLabel(equipped.Tier)}");

		return sb.ToString().TrimEnd();
	}

	private static void AppendDelta(
		StringBuilder sb,
		string label,
		float selected,
		float equipped,
		string format,
		bool invertGood = false)
	{
		if (Mathf.IsEqualApprox(selected, equipped))
		{
			sb.AppendLine($"  {label}  {selected.ToString(format)}");
			return;
		}

		var delta = selected - equipped;
		var better = invertGood ? delta < 0f : delta > 0f;
		var sign = delta > 0f ? "+" : "";
		var color = better ? "#73C7EB" : "#E66152";
		sb.AppendLine(
			$"  {label}  {selected.ToString(format)}  [color={color}]({sign}{delta.ToString(format)})[/color]");
	}

	private static string BuildPartBlurb(PartData part)
	{
		var mfg = GameCatalog.GetManufacturer(part.ManufacturerId);
		var sb = new StringBuilder();
		sb.AppendLine($"{CatalogTiers.Label(part.Tier)}  ·  {part.Slot}  ·  {mfg.DisplayName}");
		sb.AppendLine(mfg.Niche);
		sb.AppendLine();
		if (part.WeaponFamily != WeaponFamily.None)
			sb.Append($"Family {part.WeaponFamily}  ");
		sb.Append($"Structure {part.StructureHp:0}  ");
		sb.Append($"Armor {part.Armor:0}  ");
		if (part.Weight > 0) sb.Append($"Wt {part.Weight:0}  ");
		if (part.LoadRating > 0) sb.Append($"Load {part.LoadRating:0}  ");
		if (part.PowerRequirement > 0) sb.Append($"Req {part.PowerRequirement:0}  ");
		if (part.IsHeldShield)
		{
			sb.Append($"Shield arc {part.ShieldArcDegrees:0}°  ");
			sb.Append($"Raise {part.ShieldPowerPerSec:0}/s  ");
		}
		else if (part.PowerPerShot > 0)
			sb.Append(part.WeaponFamily == WeaponFamily.Melee
				? $"P/swing {part.PowerPerShot:0}  "
				: $"P/shot {part.PowerPerShot:0}  ");
		if (part.PowerCapacity > 0) sb.Append($"Cap {part.PowerCapacity:0}  ");
		if (part.PowerOutput > 0) sb.Append($"Gen {part.PowerOutput:0}/s  ");
		if (part.HeatCapBonus != 0) sb.Append($"Heat cap +{part.HeatCapBonus:0}  ");
		if (part.HeatDissipation > 0) sb.Append($"Sink +{part.HeatDissipation:0.0}/s  ");
		if (part.MaxSpeed != 0) sb.Append($"Spd {part.MaxSpeed:+0.0;-0.0}  ");
		if (part.Damage > 0)
			sb.Append(part.WeaponFamily == WeaponFamily.Melee
				? $"DMG {part.Damage:0}  REACH {part.Range:0.0}  "
				: $"DMG {part.Damage:0}  RNG {part.Range:0}  ");
		if (part.AbilityKind == AbilityKind.Active)
			sb.Append($"{part.AbilityId} ({part.AbilityCooldown:0.0}s CD)  ");
		if (part.AbilityKind == AbilityKind.Passive)
			sb.Append($"Passive {part.AbilityId}  ");
		return sb.ToString().TrimEnd();
	}

	private void OnDetailsAction()
	{
		if (string.IsNullOrEmpty(_selectedPartId))
			return;

		if (_mode == ExchangeMode.Buy)
		{
			var offer = _selectedOffer ?? _stock.FirstOrDefault(o => o.PartId == _selectedPartId);
			if (offer == null || !ShopService.TryBuy(_profile, offer))
			{
				SfxService.Click();
				return;
			}

			SfxService.Confirm();
		}
		else
		{
			if (!ShopService.TrySell(_profile, _selectedPartId))
			{
				SfxService.Click();
				return;
			}

			SfxService.Play("scrap");
			if (_profile.SpareCount(_selectedPartId) <= 0)
				_selectedPartId = null;
		}

		GetNodeOrNull<GameSession>("/root/GameSession")?.SaveProfile();
		RefreshTelemetry();
		RefreshManifest();
		RebuildList();
		RefreshDetails();
	}
}
