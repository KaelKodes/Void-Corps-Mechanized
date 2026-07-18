using Godot;

namespace Mechanize;

/// <summary>Procedural holographic operations-table backdrop for the campaign map.</summary>
public partial class CampaignMapBackdrop : Control
{
	private float _time;
	private readonly Vector2[] _stars = new Vector2[48];
	private readonly float[] _starBright = new float[48];

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var rng = new RandomNumberGenerator();
		rng.Seed = 0xC0FFEEUL;
		for (var i = 0; i < _stars.Length; i++)
		{
			_stars[i] = new Vector2(rng.Randf(), rng.Randf());
			_starBright[i] = rng.RandfRange(0.25f, 0.9f);
		}
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 8f || size.Y < 8f)
			return;

		DrawRect(new Rect2(Vector2.Zero, size), MechUiTheme.MapVoid);

		// Soft radial wells.
		var center = size * 0.5f;
		DrawCircle(center, Mathf.Min(size.X, size.Y) * 0.42f, new Color(0.08f, 0.16f, 0.22f, 0.35f));
		DrawCircle(center + new Vector2(size.X * 0.18f, -size.Y * 0.1f), size.Y * 0.22f,
			new Color(0.12f, 0.08f, 0.04f, 0.2f));

		// Sparse star field.
		for (var i = 0; i < _stars.Length; i++)
		{
			var p = new Vector2(_stars[i].X * size.X, _stars[i].Y * size.Y);
			var twinkle = 0.55f + 0.45f * Mathf.Sin(_time * (1.2f + i * 0.07f) + i);
			var a = _starBright[i] * twinkle * 0.55f;
			DrawCircle(p, i % 7 == 0 ? 1.6f : 1.1f, new Color(0.7f, 0.85f, 0.95f, a));
		}

		// Sector rings.
		var ringColor = MechUiTheme.MapGridMajor with { A = 0.18f };
		for (var r = 1; r <= 4; r++)
		{
			var radius = Mathf.Min(size.X, size.Y) * (0.12f + r * 0.1f);
			DrawArc(center, radius, 0f, Mathf.Tau, 72, ringColor, 1.2f, true);
		}

		// Major / minor grid.
		const float minor = 48f;
		const float major = 144f;
		for (var x = 0f; x < size.X; x += minor)
		{
			var majorLine = Mathf.Abs(x % major) < 0.5f;
			DrawLine(new Vector2(x, 0f), new Vector2(x, size.Y),
				majorLine ? MechUiTheme.MapGridMajor : MechUiTheme.MapGrid, majorLine ? 1.4f : 1f);
		}

		for (var y = 0f; y < size.Y; y += minor)
		{
			var majorLine = Mathf.Abs(y % major) < 0.5f;
			DrawLine(new Vector2(0f, y), new Vector2(size.X, y),
				majorLine ? MechUiTheme.MapGridMajor : MechUiTheme.MapGrid, majorLine ? 1.4f : 1f);
		}

		// Coordinate ticks along edges.
		var tick = MechUiTheme.Cyan with { A = 0.35f };
		for (var i = 0; i < 12; i++)
		{
			var x = size.X * (i + 1) / 13f;
			DrawLine(new Vector2(x, 0f), new Vector2(x, 10f), tick, 1.5f);
			DrawLine(new Vector2(x, size.Y), new Vector2(x, size.Y - 10f), tick, 1.5f);
		}

		for (var i = 0; i < 7; i++)
		{
			var y = size.Y * (i + 1) / 8f;
			DrawLine(new Vector2(0f, y), new Vector2(10f, y), tick, 1.5f);
			DrawLine(new Vector2(size.X, y), new Vector2(size.X - 10f, y), tick, 1.5f);
		}

		// Horizontal scan sweep.
		var sweepY = Mathf.PosMod(_time * 55f, size.Y + 80f) - 40f;
		DrawRect(new Rect2(0f, sweepY, size.X, 28f), MechUiTheme.MapScan);

		// Fine scanlines.
		var lineA = new Color(0f, 0f, 0f, 0.18f);
		for (var y = 0f; y < size.Y; y += 3f)
			DrawLine(new Vector2(0f, y), new Vector2(size.X, y), lineA, 1f);

		// Vignette bands.
		DrawRect(new Rect2(0f, 0f, size.X, 48f), new Color(0f, 0f, 0f, 0.35f));
		DrawRect(new Rect2(0f, size.Y - 56f, size.X, 56f), new Color(0f, 0f, 0f, 0.4f));
		DrawRect(new Rect2(0f, 0f, 36f, size.Y), new Color(0f, 0f, 0f, 0.25f));
		DrawRect(new Rect2(size.X - 36f, 0f, 36f, size.Y), new Color(0f, 0f, 0f, 0.25f));

		// Corner brackets.
		var bracket = MechUiTheme.Accent with { A = 0.55f };
		const float arm = 28f;
		DrawPolyline([new Vector2(18, 18 + arm), new Vector2(18, 18), new Vector2(18 + arm, 18)], bracket, 2f);
		DrawPolyline([new Vector2(size.X - 18 - arm, 18), new Vector2(size.X - 18, 18), new Vector2(size.X - 18, 18 + arm)],
			bracket, 2f);
		DrawPolyline([new Vector2(18, size.Y - 18 - arm), new Vector2(18, size.Y - 18), new Vector2(18 + arm, size.Y - 18)],
			bracket, 2f);
		DrawPolyline(
			[new Vector2(size.X - 18 - arm, size.Y - 18), new Vector2(size.X - 18, size.Y - 18),
				new Vector2(size.X - 18, size.Y - 18 - arm)],
			bracket, 2f);
	}
}
