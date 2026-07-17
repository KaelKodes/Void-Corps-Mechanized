using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Post-skirmish results + shop. BUY / SELL modes with a shared part details bay.
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
	private List<ShopOffer> _stock = new();
	private ExchangeMode _mode = ExchangeMode.Buy;
	private string? _selectedPartId;
	private ShopOffer? _selectedOffer;
	private bool _committed;
	private string _shopManufacturerId = "";

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

	public void Open(GameSession session)
	{
		_profile = session.Profile;
		_match = session.Match;
		_shopManufacturerId = session.MatchFromCampaign
			? session.LastMissionManufacturerId
			: "";
		if (!_committed)
		{
			_match.ApplyToProfile(_profile);
			session.SetLoadout(_profile.Loadout);
			session.SaveProfile();
			_committed = true;
		}

		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_stock = ShopService.GenerateStock(_profile, manufacturerId: _shopManufacturerId);
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
		BuildExchangeColumn(columns);

		BuildDetailsBay(root);
		BuildNav(root);

		RefreshTelemetry();
		RefreshManifest();
		RefreshModeChrome();
		RebuildList();
		RefreshDetails();
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

		var shopLine = string.IsNullOrEmpty(_shopManufacturerId)
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
			Text = victory ? "CLAIM SECURED" : "DETACHMENT LOST",
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
		var panel = MechUiTheme.MakePanel("Exchange");
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddChild(panel);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 10);
		panel.AddChild(inner);

		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 12);
		inner.AddChild(headerRow);

		var sectionTitle = string.IsNullOrEmpty(_shopManufacturerId)
			? "FIELD EXCHANGE"
			: $"{GameCatalog.GetManufacturer(_shopManufacturerId).DisplayName.ToUpperInvariant()} STOCK";
		_listSection = MechUiTheme.MakeSectionLabel(sectionTitle);
		_listSection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		headerRow.AddChild(_listSection);

		_buyToggle = new Button { Text = "BUY", CustomMinimumSize = new Vector2(88, 32) };
		_buyToggle.Pressed += () => SetMode(ExchangeMode.Buy);
		headerRow.AddChild(_buyToggle);

		_sellToggle = new Button { Text = "SELL", CustomMinimumSize = new Vector2(88, 32) };
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
		_list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_list);
	}

	private void BuildDetailsBay(VBoxContainer root)
	{
		_detailsPanel = MechUiTheme.MakePanel("PartBay");
		_detailsPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_detailsPanel.CustomMinimumSize = new Vector2(0, 168);
		root.AddChild(_detailsPanel);

		var inner = new HBoxContainer();
		inner.AddThemeConstantOverride("separation", 16);
		_detailsPanel.AddChild(inner);

		var left = new VBoxContainer();
		left.AddThemeConstantOverride("separation", 6);
		inner.AddChild(left);
		left.AddChild(MechUiTheme.MakeSectionLabel("PART BAY"));

		_detailsPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(96, 96),
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
			Text = "Click an exchange or salvage listing to inspect it here.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.Muted
		};
		_detailsBody.AddThemeFontSizeOverride("font_size", 13);
		mid.AddChild(_detailsBody);

		var right = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		right.AddThemeConstantOverride("separation", 8);
		inner.AddChild(right);

		_detailsAction = new Button
		{
			Text = "BUY",
			CustomMinimumSize = new Vector2(140, 44),
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
		var cont = new Button
		{
			Text = session is { InCampaign: true, ReturnToCampaignMap: true }
				? "Continue Campaign"
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
			if (s is { InCampaign: true, ReturnToCampaignMap: true })
			{
				GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
				return;
			}

			if (s is { MatchFromCampaign: true })
			{
				s.MatchFromCampaign = false;
				GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
				return;
			}

			if (s != null)
				s.OpenSkirmishSetupOnMenu = true;
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

		var shopNote = string.IsNullOrEmpty(_shopManufacturerId)
			? "Open field exchange"
			: $"{GameCatalog.GetManufacturer(_shopManufacturerId).DisplayName} resupply";
		_manifestBody.Text =
			$"Outcome\n  {_match.Outcome}\n\n" +
			$"Run scrap banked\n  {_match.RunScrap}\n\n" +
			$"Merc corps\n  {_profile.MercCorpName}\n\n" +
			$"Post-mission shop\n  {shopNote}\n\n" +
			$"Recovered parts\n{drops}";
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
				meta: $"{part.Slot}   ·   {offer.Price} scrap" + (copies > 0 ? $"   ·   owned ×{copies}" : ""),
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
				meta: $"{part.Slot}   ·   spare ×{spare}   ·   sell {value} scrap",
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
			CustomMinimumSize = new Vector2(64, 64),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = PartPortrait.Get(part, 96),
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
		var niche = new Label
		{
			Text = mfg.DisplayName,
			Modulate = MechUiTheme.Cyan,
			MouseFilter = MouseFilterEnum.Ignore
		};
		niche.AddThemeFontSizeOverride("font_size", 12);
		info.AddChild(niche);

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
			return;
		}

		_detailsPortrait.Texture = PartPortrait.Get(part, 128);
		_detailsTitle.Text = part.DisplayName;
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
	}

	private static string BuildPartBlurb(PartData part)
	{
		var mfg = GameCatalog.GetManufacturer(part.ManufacturerId);
		var sb = new StringBuilder();
		sb.AppendLine($"{part.Slot}  ·  {mfg.DisplayName}");
		sb.AppendLine(mfg.Niche);
		sb.AppendLine();
		if (part.WeaponFamily != WeaponFamily.None)
			sb.Append($"Family {part.WeaponFamily}  ");
		if (part.Armor != 0) sb.Append($"Armor {part.Armor:+0;-0}  ");
		if (part.HullBonus != 0) sb.Append($"Hull {part.HullBonus:+0;-0}  ");
		if (part.HeatCapBonus != 0) sb.Append($"Heat cap +{part.HeatCapBonus:0}  ");
		if (part.HeatDissipation > 0) sb.Append($"Sink +{part.HeatDissipation:0.0}/s  ");
		if (part.MaxSpeed != 0) sb.Append($"Spd {part.MaxSpeed:+0.0;-0.0}  ");
		if (part.Damage > 0)
			sb.Append($"DMG {part.Damage:0}  RNG {part.Range:0}  ");
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
