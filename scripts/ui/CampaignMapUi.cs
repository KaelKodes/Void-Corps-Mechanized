using System.Collections.Generic;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>Sector board of claim-locations; full visibility, adjacent deploy with manufacturer offers.</summary>
public partial class CampaignMapUi : Control
{
	private CampaignRun _run = null!;
	private readonly Dictionary<string, Control> _nodeButtons = new();
	private Label? _status;
	private PanelContainer? _detailPanel;
	private Label? _detailBody;
	private VBoxContainer? _offerList;
	private string? _pendingCommitId;
	private int _pendingOfferIndex = -1;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		_run = session?.Campaign ?? CampaignRun.Load() ?? CampaignRun.StartNew();
		if (session != null)
			session.Campaign = _run;
		Build();
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_nodeButtons.Clear();
		_pendingCommitId = null;
		_pendingOfferIndex = -1;
		var session = GetNodeOrNull<GameSession>("/root/GameSession");

		var dim = new ColorRect
		{
			Color = new Color(0.05f, 0.07f, 0.09f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var title = new Label
		{
			Text = $"{session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename}  ·  {_run.Graph.SectorTitle}",
			Position = new Vector2(40, 28),
			Size = new Vector2(1200, 40),
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		AddChild(title);

		_status = new Label
		{
			Text = BuildStatusLine(session),
			Position = new Vector2(40, 68),
			Size = new Vector2(1700, 48),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.65f, 0.72f, 0.78f)
		};
		AddChild(_status);

		var map = new Control
		{
			Position = new Vector2(40, 100),
			Size = new Vector2(1840, 560),
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(map);

		var layout = BuildLayout(map.Size);
		DrawEdges(map, layout);
		PlaceNodes(map, layout);

		_detailPanel = new PanelContainer
		{
			Visible = false,
			Position = new Vector2(280, 680),
			CustomMinimumSize = new Vector2(1360, 280)
		};
		_detailPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.09f, 0.11f, 0.96f),
			BorderColor = new Color(0.62f, 0.5f, 0.28f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 14,
			ContentMarginTop = 12,
			ContentMarginRight = 14,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		});
		AddChild(_detailPanel);

		var detailRoot = new VBoxContainer();
		detailRoot.AddThemeConstantOverride("separation", 8);
		_detailPanel.AddChild(detailRoot);
		_detailBody = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_detailBody.AddThemeFontSizeOverride("font_size", 15);
		detailRoot.AddChild(_detailBody);

		_offerList = new VBoxContainer();
		_offerList.AddThemeConstantOverride("separation", 6);
		detailRoot.AddChild(_offerList);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		detailRoot.AddChild(row);
		var deploy = new Button { Text = "DEPLOY", CustomMinimumSize = new Vector2(180, 40) };
		deploy.Pressed += ConfirmDeploy;
		row.AddChild(deploy);
		var cancel = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(140, 40) };
		cancel.Pressed += () =>
		{
			_pendingCommitId = null;
			_pendingOfferIndex = -1;
			_detailPanel.Visible = false;
		};
		row.AddChild(cancel);

		var back = new Button
		{
			Text = "Main Menu",
			Position = new Vector2(1700, 28),
			CustomMinimumSize = new Vector2(160, 40)
		};
		back.Pressed += () =>
		{
			_run.Save();
			GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
		};
		AddChild(back);
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

	private void PlaceNodes(Control map, MapLayout layout)
	{
		const float nodeSize = 96f;
		foreach (var node in _run.Graph.Nodes)
		{
			var pos = layout.Positions[node.Id];
			var isHere = node.Id == _run.CurrentNodeId;
			var canCommit = _run.Graph.IsAdjacent(_run.CurrentNodeId, node.Id) && !node.Cleared
				&& node.Kind != CampaignNodeKind.Start;

			var btn = new Button
			{
				Position = pos - new Vector2(nodeSize * 0.5f, nodeSize * 0.5f),
				CustomMinimumSize = new Vector2(nodeSize, nodeSize),
				Size = new Vector2(nodeSize, nodeSize),
				Text = NodeLabel(node, isHere),
				Disabled = !canCommit && !isHere,
				ClipText = true
			};
			btn.AddThemeFontSizeOverride("font_size", isHere || node.Kind == CampaignNodeKind.Warning ? 12 : 11);
			btn.Modulate = NodeColor(node, isHere, canCommit);
			if (canCommit)
			{
				var id = node.Id;
				btn.Pressed += () => OpenCommit(id);
			}

			map.AddChild(btn);
			_nodeButtons[node.Id] = btn;
		}
	}

	private void DrawEdges(Control map, MapLayout layout)
	{
		foreach (var (fromId, toId) in _run.Graph.Edges)
		{
			if (!layout.Positions.TryGetValue(fromId, out var a)
				|| !layout.Positions.TryGetValue(toId, out var b))
				continue;

			var line = new Line2D
			{
				Width = 3.5f,
				DefaultColor = new Color(0.78f, 0.82f, 0.88f, 0.7f),
				Antialiased = true,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round
			};
			line.AddPoint(a);
			line.AddPoint(b);
			map.AddChild(line);
		}
	}

	private static Vector2 NodeScreenPos(CampaignNode node, int maxCol, int columnHeight, Vector2 mapSize)
	{
		var x = 100f + node.Column / Mathf.Max(1f, maxCol) * (mapSize.X - 200f);
		var usableY = mapSize.Y - 120f;
		var step = usableY / (columnHeight + 1);
		var y = 60f + step * (node.Row + 1);
		return new Vector2(x, y);
	}

	private static string NodeLabel(CampaignNode node, bool isHere)
	{
		if (isHere)
			return "YOU";
		if (node.Cleared)
			return "DONE";
		if (node.Kind == CampaignNodeKind.Warning)
			return "WARNING!";
		if (node.Kind == CampaignNodeKind.Start)
			return "START";

		var name = node.LocationDisplayName;
		if (string.IsNullOrEmpty(name))
			return "LOC";
		var parts = name.Split(' ');
		return parts.Length > 0 ? parts[^1].ToUpperInvariant() : name.ToUpperInvariant();
	}

	private static Color NodeColor(CampaignNode node, bool isHere, bool canCommit)
	{
		if (isHere)
			return new Color(0.95f, 0.82f, 0.4f);
		if (node.Cleared)
			return new Color(0.4f, 0.55f, 0.45f);
		if (node.Kind == CampaignNodeKind.Warning)
			return new Color(0.95f, 0.4f, 0.3f);
		if (canCommit)
			return new Color(0.55f, 0.78f, 0.95f);
		return new Color(0.7f, 0.74f, 0.78f);
	}

	private void OpenCommit(string nodeId)
	{
		var node = _run.Graph.Get(nodeId);
		if (node == null || _detailPanel == null || _detailBody == null || _offerList == null)
			return;

		_pendingCommitId = nodeId;
		_pendingOfferIndex = node.Offers.Count > 0 ? 0 : -1;
		_detailPanel.Visible = true;

		foreach (var child in _offerList.GetChildren())
			child.QueueFree();

		var claimBrief = "";
		foreach (var claim in VoidCorpsIdentity.ClaimSites)
		{
			if (claim.Code == node.LocationClaimCode)
			{
				claimBrief = claim.Brief;
				break;
			}
		}

		if (node.Kind == CampaignNodeKind.Warning && node.Offers.Count > 0)
		{
			var boss = BossEncounterCatalog.Get(node.Offers[0].BossEncounterId);
			_detailBody.Text =
				$"{node.LocationDisplayName}\nWARNING — {boss.BossName}\n{boss.Brief}\n{claimBrief}";
		}
		else
		{
			_detailBody.Text =
				$"{node.LocationDisplayName}\n{claimBrief}\n\nPick a manufacturer contract. Completing one clears this location.";
		}

		var offerGroup = new ButtonGroup();
		for (var i = 0; i < node.Offers.Count; i++)
		{
			var offer = node.Offers[i];
			var index = i;
			var mfg = GameCatalog.GetManufacturer(offer.ManufacturerId).DisplayName;
			var rival = GameCatalog.GetManufacturer(offer.RivalManufacturerId).DisplayName;
			var mission = MissionCatalog.Get(offer.MissionType);
			var label =
				$"{mfg} · {mission.Title} · {offer.Difficulty}  |  +{offer.RepGain} {mfg}  /  -{offer.RepLoss} {rival}";

			var btn = new Button
			{
				Text = label,
				CustomMinimumSize = new Vector2(0, 36),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				ToggleMode = true,
				ButtonGroup = offerGroup,
				ButtonPressed = index == _pendingOfferIndex
			};
			btn.Pressed += () => SelectOffer(index);
			_offerList.AddChild(btn);
		}
	}

	private void SelectOffer(int index)
	{
		_pendingOfferIndex = index;
	}

	private string BuildStatusLine(GameSession? session)
	{
		var profile = session?.Profile;
		var sb = new StringBuilder();
		sb.Append("All locations visible. Deploy to an adjacent site, then pick a manufacturer contract.  ");
		sb.Append("Claims secured ");
		sb.Append(_run.ClaimsSecured);
		if (profile != null)
		{
			sb.Append("  ·  Rep");
			foreach (var id in GameCatalog.Manufacturers.Keys)
			{
				var m = GameCatalog.GetManufacturer(id);
				sb.Append("  ");
				sb.Append(ShortMfg(m.DisplayName));
				sb.Append(' ');
				sb.Append(profile.ReputationWith(id).ToString("+0;-0;0"));
			}
		}

		return sb.ToString();
	}

	private static string ShortMfg(string displayName)
	{
		if (displayName.StartsWith("Lumina"))
			return "Lumina";
		return displayName;
	}

	private void ConfirmDeploy()
	{
		if (_pendingCommitId == null || _pendingOfferIndex < 0)
		{
			if (_status != null)
				_status.Text = "Select a manufacturer contract first.";
			return;
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return;
		if (!session.DeployCampaignNode(_pendingCommitId, _pendingOfferIndex))
		{
			_status!.Text = "Cannot deploy that location.";
			return;
		}

		SfxService.Confirm();
		GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
	}
}
