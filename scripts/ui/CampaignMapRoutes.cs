using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Draws glowing routed paths between campaign map nodes.</summary>
public partial class CampaignMapRoutes : Control
{
	private readonly List<(Vector2 From, Vector2 To, Color Color, float Width)> _segments = new();
	private float _time;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		if (_segments.Count > 0)
			QueueRedraw();
	}

	public void SetSegments(IEnumerable<(Vector2 From, Vector2 To, Color Color, float Width)> segments)
	{
		_segments.Clear();
		_segments.AddRange(segments);
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var (from, to, color, width) in _segments)
		{
			// Soft underglow.
			DrawLine(from, to, color with { A = color.A * 0.25f }, width + 6f, true);
			DrawLine(from, to, color, width, true);

			// Moving dash highlight along reachable / hot routes.
			if (color.A > 0.6f)
			{
				var dir = to - from;
				var len = dir.Length();
				if (len < 1f)
					continue;
				dir /= len;
				var t = Mathf.PosMod(_time * 40f, len);
				var p = from + dir * t;
				DrawCircle(p, width * 0.9f, color.Lightened(0.35f) with { A = 0.9f });
			}
		}
	}
}
