using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>FTL-style one-way sector map with fog of war.</summary>
public partial class CampaignMapUi : Control
{
	private CampaignRun _run = null!;
	private readonly Dictionary<string, Control> _nodeButtons = new();
	private Label? _status;
	private PanelContainer? _detailPanel;
	private Label? _detailBody;
	private string? _pendingCommitId;

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

		var dim = new ColorRect
		{
			Color = new Color(0.05f, 0.07f, 0.09f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var title = new Label
		{
			Text = _run.Graph.SectorTitle,
			Position = new Vector2(40, 28),
			Size = new Vector2(1200, 40),
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		AddChild(title);

		_status = new Label
		{
			Text = "Select a revealed adjacent node. Paths only move forward.",
			Position = new Vector2(40, 68),
			Size = new Vector2(1400, 28),
			Modulate = new Color(0.65f, 0.72f, 0.78f)
		};
		AddChild(_status);

		var map = new Control
		{
			Position = new Vector2(40, 110),
			Size = new Vector2(1840, 780),
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(map);

		var layout = BuildLayout(map.Size);
		DrawEdges(map, layout);
		PlaceNodes(map, layout);

		_detailPanel = new PanelContainer
		{
			Visible = false,
			Position = new Vector2(560, 820),
			CustomMinimumSize = new Vector2(800, 160)
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
		const float nodeSize = 84f;
		foreach (var node in _run.Graph.Nodes)
		{
			var pos = layout.Positions[node.Id];
			var revealed = _run.IsRevealed(node);
			var isHere = node.Id == _run.CurrentNodeId;
			var canCommit = revealed && _run.Graph.IsAdjacent(_run.CurrentNodeId, node.Id) && !node.Cleared
				&& node.Kind != CampaignNodeKind.Start;

			var btn = new Button
			{
				Position = pos - new Vector2(nodeSize * 0.5f, nodeSize * 0.5f),
				CustomMinimumSize = new Vector2(nodeSize, nodeSize),
				Size = new Vector2(nodeSize, nodeSize),
				Text = NodeLabel(node, revealed, isHere),
				Disabled = !canCommit && !isHere,
				ClipText = true
			};
			btn.AddThemeFontSizeOverride("font_size", isHere || node.Kind == CampaignNodeKind.Warning ? 13 : 12);
			btn.Modulate = NodeColor(node, revealed, isHere, canCommit);
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

			var fromNode = _run.Graph.Get(fromId);
			var toNode = _run.Graph.Get(toId);
			var lit = fromNode != null && toNode != null
				&& (_run.IsRevealed(fromNode) || fromNode.Id == _run.CurrentNodeId)
				&& (_run.IsRevealed(toNode) || toNode.Id == _run.CurrentNodeId);

			var line = new Line2D
			{
				Width = 3.5f,
				DefaultColor = lit
					? new Color(0.78f, 0.82f, 0.88f, 0.7f)
					: new Color(0.45f, 0.48f, 0.52f, 0.28f),
				Antialiased = true,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round
			};
			line.AddPoint(a);
			line.AddPoint(b);
			map.AddChild(line);
		}
	}

	/// <summary>Column-centered layout so short columns sit mid-band like FTL.</summary>
	private static Vector2 NodeScreenPos(CampaignNode node, int maxCol, int columnHeight, Vector2 mapSize)
	{
		var x = 100f + node.Column / Mathf.Max(1f, maxCol) * (mapSize.X - 200f);
		var usableY = mapSize.Y - 120f;
		var step = usableY / (columnHeight + 1);
		var y = 60f + step * (node.Row + 1);
		return new Vector2(x, y);
	}

	private static string NodeLabel(CampaignNode node, bool revealed, bool isHere)
	{
		if (isHere)
			return "YOU";
		if (node.Cleared)
			return "DONE";
		if (!revealed)
			return "?";
		if (node.Kind == CampaignNodeKind.Warning)
			return "WARNING!";
		if (node.Kind == CampaignNodeKind.Start)
			return "START";
		return MissionCatalog.Get(node.MissionType).Title.Split(' ')[0].ToUpperInvariant();
	}

	private static Color NodeColor(CampaignNode node, bool revealed, bool isHere, bool canCommit)
	{
		if (isHere)
			return new Color(0.95f, 0.82f, 0.4f);
		if (node.Cleared)
			return new Color(0.4f, 0.55f, 0.45f);
		if (!revealed)
			return new Color(0.45f, 0.48f, 0.52f);
		if (node.Kind == CampaignNodeKind.Warning)
			return new Color(0.95f, 0.4f, 0.3f);
		if (canCommit)
			return new Color(0.55f, 0.78f, 0.95f);
		return new Color(0.7f, 0.74f, 0.78f);
	}

	private void OpenCommit(string nodeId)
	{
		var node = _run.Graph.Get(nodeId);
		if (node == null || _detailPanel == null || _detailBody == null)
			return;

		_pendingCommitId = nodeId;
		_detailPanel.Visible = true;

		if (node.Kind == CampaignNodeKind.Warning)
		{
			var boss = BossEncounterCatalog.Get(node.BossEncounterId);
			_detailBody.Text =
				$"WARNING — {boss.BossName}\n{boss.Brief}\nDifficulty: {node.Difficulty}\n\nCommit to this path. No going back.";
		}
		else
		{
			var info = MissionCatalog.Get(node.MissionType);
			_detailBody.Text =
				$"{info.Title}\n{info.Brief}\nDifficulty: {node.Difficulty}\n\nCommit to this path. No going back.";
		}
	}

	private void ConfirmDeploy()
	{
		if (_pendingCommitId == null)
			return;
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return;
		if (!session.DeployCampaignNode(_pendingCommitId))
		{
			_status!.Text = "Cannot deploy that node.";
			return;
		}

		SfxService.Confirm();
		GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
	}
}
