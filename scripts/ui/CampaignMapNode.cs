using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum CampaignMapNodeVisualState
{
	Here,
	Reachable,
	Locked,
	Cleared,
	Warning
}

/// <summary>Interactive holographic tactical marker for a campaign location.</summary>
public partial class CampaignMapNode : Control
{
	public string NodeId { get; private set; } = "";
	public CampaignNodeKind Kind { get; private set; }
	public CampaignMapNodeVisualState VisualState { get; private set; }
	public bool IsConvention { get; private set; }
	public bool Interactable { get; private set; }

	private string _title = "";
	private string _code = "";
	private readonly List<Color> _pips = new();
	private bool _hovered;
	private bool _selected;
	private float _pulse;

	public event Action<string>? Selected;
	public event Action<string, bool>? HoverChanged;

	public static CampaignMapNode Create(
		string nodeId,
		CampaignNode node,
		CampaignMapNodeVisualState state,
		bool interactable,
		IReadOnlyList<Color>? manufacturerPips = null)
	{
		var marker = new CampaignMapNode
		{
			NodeId = nodeId,
			Kind = node.Kind,
			VisualState = state,
			Interactable = interactable,
			IsConvention = node.LocationClaimCode == "VC-CONVENTION"
				|| node.LocationDisplayName.Contains("Convention", StringComparison.OrdinalIgnoreCase),
			CustomMinimumSize = new Vector2(118, 118),
			Size = new Vector2(118, 118),
			FocusMode = interactable ? FocusModeEnum.All : FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop
		};
		marker._title = ResolveTitle(node, state);
		marker._code = string.IsNullOrEmpty(node.LocationClaimCode)
			? (node.Kind == CampaignNodeKind.Start ? "STAGING" : "LOC")
			: node.LocationClaimCode;
		if (manufacturerPips != null)
			marker._pips.AddRange(manufacturerPips);
		marker.MouseEntered += marker.OnMouseEntered;
		marker.MouseExited += marker.OnMouseExited;
		marker.GuiInput += marker.OnGuiInput;
		marker.FocusEntered += () =>
		{
			marker._hovered = true;
			marker.HoverChanged?.Invoke(marker.NodeId, true);
			marker.QueueRedraw();
		};
		marker.FocusExited += () =>
		{
			marker._hovered = false;
			marker.HoverChanged?.Invoke(marker.NodeId, false);
			marker.QueueRedraw();
		};
		return marker;
	}

	public void SetSelected(bool selected)
	{
		_selected = selected;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		_pulse += (float)delta;
		if (VisualState is CampaignMapNodeVisualState.Here or CampaignMapNodeVisualState.Warning
		    or CampaignMapNodeVisualState.Reachable || _hovered || _selected)
			QueueRedraw();
	}

	private void OnMouseEntered()
	{
		_hovered = true;
		HoverChanged?.Invoke(NodeId, true);
		QueueRedraw();
	}

	private void OnMouseExited()
	{
		_hovered = false;
		HoverChanged?.Invoke(NodeId, false);
		QueueRedraw();
	}

	private void OnGuiInput(InputEvent @event)
	{
		if (!Interactable)
			return;
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
		    || (@event.IsActionPressed("ui_accept") && HasFocus()))
		{
			Selected?.Invoke(NodeId);
			AcceptEvent();
		}
	}

	public override void _Draw()
	{
		var center = Size * 0.5f;
		var accent = StateColor();
		var glow = (_hovered || _selected) ? 1f : 0.65f;
		if (VisualState == CampaignMapNodeVisualState.Here)
			glow = 0.85f + 0.15f * Mathf.Sin(_pulse * 3.2f);
		if (VisualState == CampaignMapNodeVisualState.Warning)
			glow = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(_pulse * 4.5f));

		DrawCircle(center, 48f, accent with { A = 0.08f * glow });
		DrawCircle(center, 40f, new Color(0.04f, 0.06f, 0.08f, 0.85f));

		DrawArc(center, 38f, 0f, Mathf.Tau, 48, accent with { A = 0.85f * glow }, 2.2f, true);
		DrawArc(center, 32f, 0f, Mathf.Tau, 40, accent with { A = 0.35f * glow }, 1.2f, true);

		if (VisualState == CampaignMapNodeVisualState.Here)
		{
			var reticle = MechUiTheme.AccentHot with { A = 0.9f };
			var tick = 10f + 3f * Mathf.Sin(_pulse * 3f);
			DrawLine(center + new Vector2(-tick - 18f, 0f), center + new Vector2(-18f, 0f), reticle, 2f);
			DrawLine(center + new Vector2(18f, 0f), center + new Vector2(tick + 18f, 0f), reticle, 2f);
			DrawLine(center + new Vector2(0f, -tick - 18f), center + new Vector2(0f, -18f), reticle, 2f);
			DrawLine(center + new Vector2(0f, 18f), center + new Vector2(0f, tick + 18f), reticle, 2f);
			DrawArc(center, 22f + 2f * Mathf.Sin(_pulse * 2.4f), 0f, Mathf.Tau, 36, reticle, 1.5f, true);
		}

		if (VisualState == CampaignMapNodeVisualState.Warning)
			DrawWarningChevrons(center, accent);

		DrawSigil(center, accent);

		var font = ThemeDB.FallbackFont;
		if (font != null)
		{
			var titleSize = 12;
			var titleWidth = font.GetStringSize(_title, HorizontalAlignment.Left, -1, titleSize).X;
			DrawString(font, center + new Vector2(-titleWidth * 0.5f, 54f), _title,
				HorizontalAlignment.Left, -1, titleSize, MechUiTheme.Text with { A = 0.95f });

			var codeSize = 10;
			var codeWidth = font.GetStringSize(_code, HorizontalAlignment.Left, -1, codeSize).X;
			DrawString(font, center + new Vector2(-codeWidth * 0.5f, 68f), _code,
				HorizontalAlignment.Left, -1, codeSize, MechUiTheme.Muted);
		}

		if (_pips.Count > 0 && VisualState != CampaignMapNodeVisualState.Cleared)
		{
			var startX = center.X - (_pips.Count - 1) * 7f;
			for (var i = 0; i < _pips.Count; i++)
			{
				var p = new Vector2(startX + i * 14f, center.Y + 28f);
				DrawCircle(p, 3.5f, _pips[i]);
				DrawArc(p, 4.2f, 0f, Mathf.Tau, 16, Colors.Black with { A = 0.5f }, 1f, true);
			}
		}

		if (_selected || _hovered)
			DrawArc(center, 44f, 0f, Mathf.Tau, 48, MechUiTheme.AccentHot with { A = 0.55f }, 1.5f, true);
	}

	private void DrawSigil(Vector2 center, Color accent)
	{
		if (IsConvention)
		{
			var pts = new[]
			{
				center + new Vector2(0f, -14f),
				center + new Vector2(14f, 0f),
				center + new Vector2(0f, 14f),
				center + new Vector2(-14f, 0f),
				center + new Vector2(0f, -14f)
			};
			DrawPolyline(pts, accent, 2f, true);
			DrawCircle(center, 4f, accent);
			return;
		}

		switch (Kind)
		{
			case CampaignNodeKind.Start:
				DrawCircle(center, 8f, accent);
				DrawArc(center, 14f, 0f, Mathf.Tau, 24, accent, 2f, true);
				break;
			case CampaignNodeKind.Warning:
				var tri = new[]
				{
					center + new Vector2(0f, -13f),
					center + new Vector2(12f, 10f),
					center + new Vector2(-12f, 10f),
					center + new Vector2(0f, -13f)
				};
				DrawPolyline(tri, accent, 2.2f, true);
				DrawLine(center + new Vector2(0f, -4f), center + new Vector2(0f, 4f), accent, 2f);
				DrawCircle(center + new Vector2(0f, 8f), 1.5f, accent);
				break;
			default:
				var hex = new Vector2[7];
				for (var i = 0; i < 6; i++)
				{
					var a = i * Mathf.Tau / 6f - Mathf.Pi * 0.5f;
					hex[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f;
				}

				hex[6] = hex[0];
				DrawPolyline(hex, accent, 2f, true);
				DrawCircle(center, 3f, accent);
				break;
		}
	}

	private void DrawWarningChevrons(Vector2 center, Color accent)
	{
		var flash = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(_pulse * 5f));
		var c = accent with { A = flash };
		for (var i = 0; i < 3; i++)
		{
			var y = -46f + i * 7f;
			DrawPolyline(
				[center + new Vector2(-16f, y), center + new Vector2(0f, y + 6f), center + new Vector2(16f, y)],
				c, 1.6f);
		}
	}

	private Color StateColor() => VisualState switch
	{
		CampaignMapNodeVisualState.Here => MechUiTheme.MapNodeHere,
		CampaignMapNodeVisualState.Reachable => MechUiTheme.MapNodeReachable,
		CampaignMapNodeVisualState.Cleared => MechUiTheme.MapNodeCleared,
		CampaignMapNodeVisualState.Warning => MechUiTheme.MapNodeWarning,
		_ => MechUiTheme.MapNodeLocked
	};

	private static string ResolveTitle(CampaignNode node, CampaignMapNodeVisualState state)
	{
		if (state == CampaignMapNodeVisualState.Here)
			return "YOU ARE HERE";
		if (node.Cleared)
			return "CLEARED";
		if (node.Kind == CampaignNodeKind.Warning)
			return "WARNING";
		if (node.Kind == CampaignNodeKind.Start)
			return "STAGING";
		if (node.LocationClaimCode == "VC-CONVENTION"
		    || node.LocationDisplayName.Contains("Convention", StringComparison.OrdinalIgnoreCase))
			return "CONVENTION";

		var name = node.LocationDisplayName;
		if (string.IsNullOrEmpty(name))
			return "LOCATION";
		return name.Length > 16 ? name[..16].ToUpperInvariant() : name.ToUpperInvariant();
	}
}
