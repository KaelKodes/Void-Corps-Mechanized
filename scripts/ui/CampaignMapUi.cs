using System.Collections.Generic;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Holographic sector operations table — claim locations, adjacent deploy, manufacturer offers.
/// </summary>
public partial class CampaignMapUi : Control
{
	private CampaignRun _run = null!;
	private readonly Dictionary<string, CampaignMapNode> _nodes = new();
	private readonly Dictionary<string, Vector2> _positions = new();

	private CampaignMapRoutes? _routes;
	private Control? _opsTable;
	private PanelContainer? _dossier;
	private Label? _dossierTitle;
	private Label? _dossierBody;
	private VBoxContainer? _offerList;
	private Button? _deployButton;
	private Label? _statusToast;
	private Label? _headerMeta;
	private Label? _tooltip;
	private string? _pendingCommitId;
	private int _pendingOfferIndex = -1;
	private string? _hoveredNodeId;
	private Tween? _dossierTween;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Campaign);
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		_run = session?.Campaign ?? CampaignRun.Load() ?? CampaignRun.StartNew();
		if (session != null)
			session.Campaign = _run;

		if (_run.Phase == CampaignPhase.CadetProgram)
		{
			if (_run.AcademyStep == AcademyStep.Graduation || session is { OpenAcademyGraduation: true })
			{
				if (session != null)
					session.OpenAcademyGraduation = true;
				GetTree().ChangeSceneToFile("res://scenes/academy_graduation.tscn");
				return;
			}

			session?.ResumeCadetIfNeeded();
			GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
			return;
		}

		Build();
		CallDeferred(nameof(DeferredReveal));
	}

	private void DeferredReveal()
	{
		RebuildGraph();
		Modulate = new Color(1f, 1f, 1f, 0f);
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 1f, 0.45f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_nodes.Clear();
		_positions.Clear();
		_pendingCommitId = null;
		_pendingOfferIndex = -1;
		_hoveredNodeId = null;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");

		var backdrop = new CampaignMapBackdrop();
		AddChild(backdrop);

		var root = new MarginContainer();
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("margin_left", 20);
		root.AddThemeConstantOverride("margin_top", 16);
		root.AddThemeConstantOverride("margin_right", 20);
		root.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(root);

		var columns = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);

		var mainCol = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		mainCol.AddThemeConstantOverride("separation", 10);
		columns.AddChild(mainCol);

		mainCol.AddChild(BuildHeader(session));
		mainCol.AddChild(BuildOpsTable());
		mainCol.AddChild(BuildLegendRow());

		_statusToast = new Label
		{
			Text = "",
			Modulate = MechUiTheme.AccentHot,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_statusToast.AddThemeFontSizeOverride("font_size", 13);
		mainCol.AddChild(_statusToast);

		columns.AddChild(BuildDossier());
		HideDossierImmediate();
	}

	private Control BuildHeader(GameSession? session)
	{
		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panel.AddThemeStyleboxOverride("panel", MechUiTheme.MakeMapHeaderStyle());

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 18);
		panel.AddChild(row);

		var left = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		left.AddThemeConstantOverride("separation", 2);
		row.AddChild(left);

		var brand = new Label
		{
			Text = "VOID CORPS  ·  OPERATIONS TABLE",
			Modulate = MechUiTheme.Accent.Darkened(0.1f)
		};
		brand.AddThemeFontSizeOverride("font_size", 11);
		left.AddChild(brand);

		var title = new Label
		{
			Text = $"{session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename}  ·  {_run.Graph.SectorTitle}",
			Modulate = MechUiTheme.AccentHot
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		left.AddChild(title);

		_headerMeta = new Label
		{
			Text = BuildHeaderMeta(session),
			Modulate = MechUiTheme.Muted,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_headerMeta.AddThemeFontSizeOverride("font_size", 13);
		left.AddChild(_headerMeta);

		var back = new Button
		{
			Text = "Main Menu",
			CustomMinimumSize = new Vector2(140, 40),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		MechUiTheme.StyleGhostButton(back);
		back.Pressed += () =>
		{
			SfxService.Click();
			_run.Save();
			session?.SaveProfile();
			session?.ActivateMainProfile();
			GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
		};
		row.AddChild(back);

		return panel;
	}

	private Control BuildOpsTable()
	{
		var frame = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 420)
		};
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.035f, 0.05f, 0.55f),
			BorderColor = MechUiTheme.Cyan.Darkened(0.45f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 8,
			ContentMarginRight = 8,
			ContentMarginBottom = 8
		});

		_opsTable = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true
		};
		_opsTable.Resized += RebuildGraph;
		frame.AddChild(_opsTable);

		_routes = new CampaignMapRoutes();
		_opsTable.AddChild(_routes);

		_tooltip = new Label
		{
			Visible = false,
			Modulate = MechUiTheme.Text,
			ZIndex = 20
		};
		_tooltip.AddThemeFontSizeOverride("font_size", 12);
		_tooltip.AddThemeStyleboxOverride("normal", new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.92f),
			BorderColor = MechUiTheme.Cyan,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 4,
			ContentMarginRight = 8,
			ContentMarginBottom = 4
		});
		_opsTable.AddChild(_tooltip);

		return frame;
	}

	private Control BuildLegendRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		row.AddChild(LegendChip("HERE", MechUiTheme.MapNodeHere));
		row.AddChild(LegendChip("REACHABLE", MechUiTheme.MapNodeReachable));
		row.AddChild(LegendChip("LOCKED", MechUiTheme.MapNodeLocked));
		row.AddChild(LegendChip("CLEARED", MechUiTheme.MapNodeCleared));
		row.AddChild(LegendChip("WARNING", MechUiTheme.MapNodeWarning));
		var hint = new Label
		{
			Text = "Hover highlights routes from your position  ·  Select a reachable site for the dossier",
			Modulate = MechUiTheme.Muted,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		hint.AddThemeFontSizeOverride("font_size", 12);
		row.AddChild(hint);
		return row;
	}

	private static Control LegendChip(string text, Color color)
	{
		var box = new HBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		var swatch = new ColorRect
		{
			Color = color,
			CustomMinimumSize = new Vector2(10, 10),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		box.AddChild(swatch);
		var label = new Label { Text = text, Modulate = MechUiTheme.Muted };
		label.AddThemeFontSizeOverride("font_size", 11);
		box.AddChild(label);
		return box;
	}

	private Control BuildDossier()
	{
		_dossier = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_dossier.AddThemeStyleboxOverride("panel", MechUiTheme.MakeMapDossierStyle());

		var col = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		col.AddThemeConstantOverride("separation", 10);
		_dossier.AddChild(col);

		var section = MechUiTheme.MakeSectionLabel("LOCATION DOSSIER");
		col.AddChild(section);

		_dossierTitle = new Label
		{
			Text = "",
			Modulate = MechUiTheme.AccentHot,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_dossierTitle.AddThemeFontSizeOverride("font_size", 20);
		col.AddChild(_dossierTitle);

		_dossierBody = new Label
		{
			Text = "Select an adjacent claim to open contracts.",
			Modulate = MechUiTheme.Text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_dossierBody.AddThemeFontSizeOverride("font_size", 14);
		col.AddChild(_dossierBody);

		_offerList = new VBoxContainer();
		_offerList.AddThemeConstantOverride("separation", 8);
		col.AddChild(_offerList);

		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		actions.AddThemeConstantOverride("separation", 10);
		col.AddChild(actions);

		var cancel = new Button
		{
			Text = "Close",
			CustomMinimumSize = new Vector2(110, 40)
		};
		MechUiTheme.StyleGhostButton(cancel);
		cancel.Pressed += () =>
		{
			SfxService.Click();
			CloseDossier();
		};
		actions.AddChild(cancel);

		_deployButton = new Button
		{
			Text = "DEPLOY",
			CustomMinimumSize = new Vector2(160, 40)
		};
		MechUiTheme.StylePrimaryButton(_deployButton);
		_deployButton.Pressed += ConfirmDeploy;
		actions.AddChild(_deployButton);

		return _dossier;
	}

	private void RebuildGraph()
	{
		if (_opsTable == null || _routes == null)
			return;

		foreach (var node in _nodes.Values)
			node.QueueFree();
		_nodes.Clear();
		_positions.Clear();

		var size = _opsTable.Size;
		if (size.X < 80f || size.Y < 80f)
			return;

		var layout = BuildLayout(size);
		foreach (var (id, pos) in layout.Positions)
			_positions[id] = pos;

		RefreshRoutes();

		foreach (var node in _run.Graph.Nodes)
		{
			if (!_positions.TryGetValue(node.Id, out var pos))
				continue;

			var isHere = node.Id == _run.CurrentNodeId;
			var canCommit = _run.Graph.IsAdjacent(_run.CurrentNodeId, node.Id) && !node.Cleared
				&& node.Kind != CampaignNodeKind.Start;
			var canReenterHall = _run.AtConventionGate && isHere
				&& node.LocationClaimCode == "VC-CONVENTION";
			var interactable = canCommit || canReenterHall || (isHere && _run.AtConventionGate);

			var state = ResolveVisualState(node, isHere, canCommit || canReenterHall);
			var pips = BuildManufacturerPips(node);
			var marker = CampaignMapNode.Create(node.Id, node, state, interactable, pips);
			marker.Position = pos - marker.Size * 0.5f;
			marker.Selected += OpenCommit;
			marker.HoverChanged += OnNodeHover;
			_opsTable.AddChild(marker);
			_nodes[node.Id] = marker;

			if (_pendingCommitId == node.Id)
				marker.SetSelected(true);
		}

		if (_tooltip != null)
			_tooltip.MoveToFront();
	}

	private void RefreshRoutes()
	{
		if (_routes == null)
			return;

		var here = _run.CurrentNodeId;
		var focusId = _hoveredNodeId ?? _pendingCommitId;
		var segments = new List<(Vector2 From, Vector2 To, Color Color, float Width)>();
		foreach (var (fromId, toId) in _run.Graph.Edges)
		{
			if (!_positions.TryGetValue(fromId, out var a) || !_positions.TryGetValue(toId, out var b))
				continue;

			var from = _run.Graph.Get(fromId);
			var to = _run.Graph.Get(toId);
			if (from == null || to == null)
				continue;

			// Only light lanes that leave your current site for the focused destination.
			var hot = focusId != null
				&& fromId == here
				&& toId == focusId;
			var color = RouteColor(from, to, hot);
			var width = hot ? 4.5f : 3.2f;
			segments.Add((a, b, color, width));
		}

		_routes.SetSegments(segments);
	}

	private Color RouteColor(CampaignNode from, CampaignNode to, bool hot)
	{
		if (hot)
			return MechUiTheme.MapRouteHot;
		// Available move from where you stand.
		if (from.Id == _run.CurrentNodeId && !to.Cleared)
			return MechUiTheme.MapRouteReachable;
		// Completed path segments only — never paint a locked branch as "done".
		if (from.Cleared && to.Cleared)
			return MechUiTheme.MapRouteDone;
		return MechUiTheme.MapRouteLocked;
	}

	private static CampaignMapNodeVisualState ResolveVisualState(CampaignNode node, bool isHere, bool reachable)
	{
		if (isHere)
			return CampaignMapNodeVisualState.Here;
		if (node.Cleared)
			return CampaignMapNodeVisualState.Cleared;
		if (node.Kind == CampaignNodeKind.Warning)
			return reachable ? CampaignMapNodeVisualState.Warning : CampaignMapNodeVisualState.Locked;
		if (reachable)
			return CampaignMapNodeVisualState.Reachable;
		return CampaignMapNodeVisualState.Locked;
	}

	private static List<Color> BuildManufacturerPips(CampaignNode node)
	{
		var pips = new List<Color>();
		foreach (var offer in node.Offers)
		{
			if (string.IsNullOrEmpty(offer.ManufacturerId))
				continue;
			pips.Add(GameCatalog.GetManufacturer(offer.ManufacturerId).AccentColor);
			if (pips.Count >= 3)
				break;
		}

		return pips;
	}

	private void OnNodeHover(string nodeId, bool hovered)
	{
		_hoveredNodeId = hovered ? nodeId : null;
		RefreshRoutes();

		if (_tooltip == null || !_positions.TryGetValue(nodeId, out var pos))
			return;

		if (!hovered)
		{
			_tooltip.Visible = false;
			return;
		}

		var node = _run.Graph.Get(nodeId);
		if (node == null)
			return;

		_tooltip.Text = $"{node.LocationDisplayName}\n{node.LocationClaimCode}";
		_tooltip.Visible = true;
		_tooltip.Position = pos + new Vector2(40f, -50f);
		_tooltip.ResetSize();
	}

	private sealed class MapLayout
	{
		public required Dictionary<string, Vector2> Positions;
	}

	private MapLayout BuildLayout(Vector2 mapSize)
	{
		var maxCol = 0;
		var colHeights = new Dictionary<int, int>();
		foreach (var n in _run.Graph.Nodes)
		{
			maxCol = Mathf.Max(maxCol, n.Column);
			colHeights.TryGetValue(n.Column, out var h);
			colHeights[n.Column] = Mathf.Max(h, n.Row + 1);
		}

		var positions = new Dictionary<string, Vector2>();
		foreach (var node in _run.Graph.Nodes)
		{
			var height = colHeights.GetValueOrDefault(node.Column, 1);
			positions[node.Id] = NodeScreenPos(node, maxCol, height, mapSize);
		}

		return new MapLayout { Positions = positions };
	}

	private static Vector2 NodeScreenPos(CampaignNode node, int maxCol, int columnHeight, Vector2 mapSize)
	{
		var padX = Mathf.Clamp(mapSize.X * 0.08f, 70f, 140f);
		var padY = Mathf.Clamp(mapSize.Y * 0.12f, 70f, 120f);
		var x = padX + node.Column / Mathf.Max(1f, maxCol) * (mapSize.X - padX * 2f);
		var usableY = mapSize.Y - padY * 2f;
		var step = usableY / (columnHeight + 1);
		var y = padY + step * (node.Row + 1);
		return new Vector2(x, y);
	}

	private void OpenCommit(string nodeId)
	{
		var node = _run.Graph.Get(nodeId);
		if (node == null || _dossier == null || _dossierTitle == null || _dossierBody == null || _offerList == null
		    || _deployButton == null)
			return;

		var isHere = node.Id == _run.CurrentNodeId;
		var canCommit = _run.Graph.IsAdjacent(_run.CurrentNodeId, node.Id) && !node.Cleared
			&& node.Kind != CampaignNodeKind.Start;
		var canReenterHall = _run.AtConventionGate && isHere
			&& node.LocationClaimCode == "VC-CONVENTION";
		if (!canCommit && !canReenterHall)
		{
			Toast(isHere ? "Current staging position." : "Only adjacent uncleared sites can be selected.");
			return;
		}

		SfxService.Click();
		_pendingCommitId = nodeId;
		_pendingOfferIndex = node.Offers.Count > 0 ? 0 : -1;

		foreach (var (id, marker) in _nodes)
			marker.SetSelected(id == nodeId);
		RefreshRoutes();

		foreach (var child in _offerList.GetChildren())
			child.QueueFree();

		_deployButton.Text = _run.AtConventionGate ? "ENTER HALL" : "DEPLOY";
		_deployButton.Disabled = false;

		var claimBrief = "";
		foreach (var claim in VoidCorpsIdentity.ClaimSites)
		{
			if (claim.Code == node.LocationClaimCode)
			{
				claimBrief = claim.Brief;
				break;
			}
		}

		_dossierTitle.Text = node.LocationDisplayName;

		if (_run.AtConventionGate)
		{
			_dossierBody.Text =
				"Big Four Convention\n\n" +
				"Manufacturer recruiters, demo trials, and affiliation signing.\n" +
				"Enter the hall to hear pitches and run trials.";
			_pendingOfferIndex = 0;
			ShowDossier();
			return;
		}

		if (node.Kind == CampaignNodeKind.Warning && node.Offers.Count > 0)
		{
			var boss = BossEncounterCatalog.Get(node.Offers[0].BossEncounterId);
			_dossierBody.Text =
				$"TITAN CLAIM — {boss.Pilot.DisplayName}\n" +
				$"{boss.Corp.DisplayName} · {MechChassisClassUtil.Label(boss.ChassisClass)}\n\n" +
				$"{boss.Brief}\n\n{claimBrief}\n\n" +
				"No manufacturer reputation is attached to this fight. Both corps want the same ground.";
		}
		else
		{
			_dossierBody.Text =
				$"{claimBrief}\n\nPick a manufacturer contract. Completing one clears this location.";
		}

		var offerGroup = new ButtonGroup();
		for (var i = 0; i < node.Offers.Count; i++)
		{
			var offer = node.Offers[i];
			var index = i;
			var mission = MissionCatalog.Get(offer.MissionType);
			var isBoss = offer.MissionType == MissionType.BossEncounter;
			var accent = MechUiTheme.MapRouteWarning;
			string cardText;

			if (isBoss)
			{
				var pilot = RivalRosterCatalog.GetPilot(offer.RivalPilotId);
				var corp = RivalRosterCatalog.GetCorp(pilot.CorpId);
				accent = corp.AccentColor;
				cardText =
					$"{corp.DisplayName}  ·  {mission.Title}\n" +
					$"{MechChassisClassUtil.Label(MechChassisClass.Titan)}  ·  {pilot.DisplayName}  ·  No manufacturer reputation";
			}
			else
			{
				var mfg = GameCatalog.GetManufacturer(offer.ManufacturerId);
				var rival = GameCatalog.GetManufacturer(offer.RivalManufacturerId);
				accent = mfg.AccentColor;
				cardText =
					$"{mfg.DisplayName}  ·  {mission.Title}\n" +
					$"{offer.Difficulty}   |   +{offer.RepGain} {ShortMfg(mfg.DisplayName)}   /   -{offer.RepLoss} {ShortMfg(rival.DisplayName)}";
				if (!string.IsNullOrEmpty(offer.RivalPilotId))
				{
					var pilot = RivalRosterCatalog.GetPilot(offer.RivalPilotId);
					var corp = RivalRosterCatalog.GetCorp(pilot.CorpId);
					cardText += $"\nRival pilot reported: {pilot.Callsign} · {corp.ShortName}";
				}
			}

			var card = new Button
			{
				ToggleMode = true,
				ButtonGroup = offerGroup,
				ButtonPressed = index == _pendingOfferIndex,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, string.IsNullOrEmpty(offer.RivalPilotId) ? 64 : 82),
				Alignment = HorizontalAlignment.Left,
				Text = cardText
			};
			card.AddThemeFontSizeOverride("font_size", 13);
			card.AddThemeStyleboxOverride("normal", MechUiTheme.MakeOfferCardStyle(false, accent));
			card.AddThemeStyleboxOverride("hover", MechUiTheme.MakeOfferCardStyle(true, accent));
			card.AddThemeStyleboxOverride("pressed", MechUiTheme.MakeOfferCardStyle(true, accent));
			card.AddThemeColorOverride("font_color", MechUiTheme.Text);
			if (!isBoss && ManufacturerBrand.TryGetTexture(offer.ManufacturerId, out var mark))
			{
				card.Icon = mark;
				card.ExpandIcon = true;
				card.AddThemeConstantOverride("icon_max_width", 40);
			}
			card.Pressed += () =>
			{
				SfxService.Click();
				SelectOffer(index);
				RefreshOfferStyles(node);
			};
			_offerList.AddChild(card);
		}

		RefreshOfferStyles(node);
		ShowDossier();
	}

	private void RefreshOfferStyles(CampaignNode node)
	{
		if (_offerList == null)
			return;
		var i = 0;
		foreach (var child in _offerList.GetChildren())
		{
			if (child is not Button btn || i >= node.Offers.Count)
				continue;
			var offer = node.Offers[i];
			var accent = offer.MissionType == MissionType.BossEncounter
				? RivalRosterCatalog.GetCorp(offer.RivalCorpId).AccentColor
				: GameCatalog.GetManufacturer(offer.ManufacturerId).AccentColor;
			var selected = i == _pendingOfferIndex;
			btn.AddThemeStyleboxOverride("normal", MechUiTheme.MakeOfferCardStyle(selected, accent));
			btn.ButtonPressed = selected;
			i++;
		}
	}

	private void SelectOffer(int index) => _pendingOfferIndex = index;

	private void ShowDossier()
	{
		if (_dossier == null)
			return;
		_dossier.Visible = true;
		_dossier.CustomMinimumSize = new Vector2(420, 0);
		_dossier.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		_dossierTween?.Kill();
		_dossier.Modulate = new Color(1f, 1f, 1f, 0f);
		_dossierTween = CreateTween();
		_dossierTween.TweenProperty(_dossier, "modulate:a", 1f, 0.2f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
	}

	private void HideDossierImmediate()
	{
		if (_dossier == null)
			return;
		_dossierTween?.Kill();
		_dossier.Visible = false;
		_dossier.CustomMinimumSize = new Vector2(0, 0);
		_dossier.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		_dossier.Modulate = Colors.White;
	}

	private void CloseDossier()
	{
		_pendingCommitId = null;
		_pendingOfferIndex = -1;
		foreach (var marker in _nodes.Values)
			marker.SetSelected(false);
		RefreshRoutes();
		HideDossierImmediate();
	}

	private string BuildHeaderMeta(GameSession? session)
	{
		var profile = session?.Profile;
		var sb = new StringBuilder();
		if (_run.AtConventionGate)
			sb.Append("Convention gate  ·  Enter the Big Four hall  ·  ");
		else
			sb.Append("Adjacent deploy only  ·  One contract clears a location  ·  ");

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { Mode: NetSession.NetMode.Client })
			sb.Append("CO-OP GUEST  ·  ");
		else if (net is { Mode: NetSession.NetMode.Hosting })
			sb.Append($"CO-OP HOST ({net.PeerCount})  ·  ");

		sb.Append($"Sector {_run.SectorIndex + 1}/{CampaignRun.MaxSectors}");
		if (_run.Phase == CampaignPhase.ActiveOperations)
			sb.Append($"  ·  loot ≤{CatalogTiers.ShortLabel(_run.MaxLootTier)}");
		sb.Append($"  ·  Claims {_run.ClaimsSecured}");

		if (profile != null)
			sb.Append($"  ·  Scrap {profile.Scrap}  ·  Licensed blueprints {profile.UnlockedBlueprints.Count}");

		return sb.ToString();
	}

	private static string ShortMfg(string displayName)
	{
		if (displayName.StartsWith("Lumina"))
			return "Lumina";
		return displayName;
	}

	private void Toast(string message)
	{
		if (_statusToast != null)
			_statusToast.Text = message;
	}

	private void ConfirmDeploy()
	{
		if (_run.AtConventionGate)
		{
			if (_pendingCommitId == null)
			{
				Toast("Select the convention node first.");
				return;
			}

			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			if (session == null)
				return;
			if (!session.EnterConventionHallFromMap(_pendingCommitId))
			{
				Toast("Cannot enter the convention hall.");
				return;
			}

			SfxService.Confirm();
			GetTree().ChangeSceneToFile("res://scenes/convention_hall.tscn");
			return;
		}

		if (_pendingCommitId == null || _pendingOfferIndex < 0)
		{
			Toast("Select a manufacturer contract first.");
			return;
		}

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { Mode: NetSession.NetMode.Client })
		{
			Toast("Only the host deploys. Wait for the detachment lead.");
			return;
		}

		var sessionOps = GetNodeOrNull<GameSession>("/root/GameSession");
		if (sessionOps == null)
			return;
		if (!sessionOps.DeployCampaignNode(_pendingCommitId, _pendingOfferIndex))
		{
			Toast("Cannot deploy that location.");
			return;
		}

		SfxService.Confirm();
		if (net is { Mode: NetSession.NetMode.Hosting })
		{
			net.HostLaunchMatch(sessionOps.BuildLaunchPayload(true));
			return;
		}

		GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
	}
}
