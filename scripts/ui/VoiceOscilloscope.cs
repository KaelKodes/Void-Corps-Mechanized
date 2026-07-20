using Godot;

namespace Mechanize;

/// <summary>
/// Speaker "portrait" rendered as a reactive CRT oscilloscope.
/// Driven by TextVoiceService speech energy — not a true audio FFT —
/// so the wave stays locked to our gated letter speech.
/// </summary>
public partial class VoiceOscilloscope : Control
{
	private const int SampleCount = 72;

	private readonly float[] _wave = new float[SampleCount];
	private readonly float[] _ghost = new float[SampleCount];
	private VoiceOscilloscopeStyle _style = VoiceOscilloscopeStyle.CorpOps;
	private float _phase;
	private float _energy;
	private float _time;
	private float _tokenKick;
	private bool _vowelBias;
	private Label? _caption;

	public VoiceOscilloscopeStyle Style
	{
		get => _style;
		set
		{
			_style = value;
			if (_caption != null)
			{
				_caption.Text = value.Caption;
				_caption.Modulate = value.Accent;
			}
			QueueRedraw();
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(220, 220);
		BuildChrome();
	}

	private void BuildChrome()
	{
		_caption = new Label
		{
			Text = _style.Caption,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = _style.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_caption.AddThemeFontSizeOverride("font_size", 11);
		_caption.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		_caption.OffsetTop = -22;
		_caption.OffsetBottom = -4;
		AddChild(_caption);
	}

	public void SetManufacturer(string manufacturerId)
	{
		Style = VoiceOscilloscopeStyle.ForManufacturer(manufacturerId);
		var name = ConventionCatalog.Get(manufacturerId).LiaisonName.ToUpperInvariant();
		if (_caption != null)
		{
			_caption.Text = name;
			_caption.Modulate = Style.Accent;
		}
	}

	public void SetCompany(FrontierCompanyData company)
	{
		var basis = VoiceOscilloscopeStyle.ForManufacturer(company.TrialTemplateId);
		Style = new VoiceOscilloscopeStyle
		{
			Id = company.Id,
			Accent = company.AccentColor,
			Caption = company.LiaisonName.ToUpperInvariant(),
			Trace = basis.Trace,
			Ghost = basis.Ghost,
			Grid = basis.Grid,
			Panel = basis.Panel,
			Bezel = company.AccentColor,
			LineWidth = basis.LineWidth,
			GhostWidth = basis.GhostWidth,
			Amplitude = basis.Amplitude,
			Frequency = basis.Frequency,
			Decay = basis.Decay,
			IdleHum = basis.IdleHum,
			GlowStrength = basis.GlowStrength,
			Squareness = basis.Squareness,
			Harmonic = basis.Harmonic,
			GhostLag = basis.GhostLag,
			ShowReticles = basis.ShowReticles,
			ShowHexFrame = basis.ShowHexFrame,
			EmberFloor = basis.EmberFloor,
			ScanSweep = basis.ScanSweep
		};
		if (_caption != null)
		{
			_caption.Text = company.LiaisonName.ToUpperInvariant();
			_caption.Modulate = company.AccentColor;
		}
	}

	public void NotifyToken(bool vowel, float pitchScale)
	{
		_vowelBias = vowel;
		_tokenKick = Mathf.Clamp(0.7f + (vowel ? 0.55f : 0.25f) + (pitchScale - 1f) * 0.3f, 0.45f, 1.4f);
		_energy = Mathf.Max(_energy, _tokenKick);
		// Rebuild immediately so the portrait kicks on the same frame as the spoken token.
		RebuildWave(0f);
		QueueRedraw();
	}

	public void NotifySilence()
	{
		_tokenKick = 0f;
		_energy = _style.IdleHum;
		RebuildWave(0f);
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_time += dt;
		_phase += dt * (11f + _energy * 14f) * _style.Frequency;

		var serviceEnergy = TextVoiceService.VisualizationEnergy;
		if (serviceEnergy > _energy)
			_energy = serviceEnergy;

		// Fall off with the letter gate, not a long CRT afterglow.
		_energy = Mathf.MoveToward(_energy, _style.IdleHum, dt * (_style.Decay + 10f));
		_tokenKick = Mathf.MoveToward(_tokenKick, 0f, dt * 18f);
		if (_tokenKick < 0.08f)
			_vowelBias = false;

		RebuildWave(dt);
		QueueRedraw();
	}

	private void RebuildWave(float dt)
	{
		var amp = _energy * _style.Amplitude * (0.7f + 0.45f * _tokenKick);
		if (_vowelBias)
			amp *= 1.2f;

		// Whole trace uses current amplitude. Spatial phase offset only — no delayed energy.
		var catchUp = Mathf.Clamp(1f - _style.GhostLag * 0.45f + dt * 14f, 0.35f, 0.95f);
		for (var i = 0; i < SampleCount; i++)
		{
			var t = _phase - (SampleCount - 1 - i) * 0.14f;
			var sine = Mathf.Sin(t);
			var square = Mathf.Sign(sine);
			var shape = Mathf.Lerp(sine, square, _style.Squareness);
			var harmonic = Mathf.Sin(t * 2.3f + 0.7f) * _style.Harmonic;
			var chatter = Mathf.Sin(t * 5.1f) * 0.08f * _energy;
			_wave[i] = Mathf.Clamp(shape * amp + harmonic * amp * 0.55f + chatter, -1.35f, 1.35f);
			_ghost[i] = Mathf.Lerp(_ghost[i], _wave[i], catchUp);
		}
	}

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 8f || size.Y < 8f)
			return;

		var pad = 10f;
		var plot = new Rect2(pad, pad, size.X - pad * 2f, size.Y - pad * 2f - 18f);
		DrawRect(new Rect2(Vector2.Zero, size), _style.Panel);
		DrawRect(new Rect2(2, 2, size.X - 4, size.Y - 4), _style.Bezel, false, 2f);

		DrawGrid(plot);
		if (_style.EmberFloor)
			DrawRect(new Rect2(plot.Position.X, plot.End.Y - 18f, plot.Size.X, 18f),
				new Color(0.7f, 0.2f, 0.05f, 0.12f + _energy * 0.2f));

		if (_style.ShowHexFrame)
			DrawHexFrame(plot.GetCenter(), Mathf.Min(plot.Size.X, plot.Size.Y) * 0.42f);

		if (_style.ShowReticles)
			DrawReticles(plot);

		if (_style.ScanSweep)
		{
			var y = plot.Position.Y + Mathf.PosMod(_time * 42f, plot.Size.Y);
			DrawRect(new Rect2(plot.Position.X, y, plot.Size.X, 10f),
				_style.Accent with { A = 0.08f });
		}

		DrawWave(_ghost, plot, _style.Ghost, _style.GhostWidth);
		DrawWave(_wave, plot, _style.Trace, _style.LineWidth);

		if (_style.GlowStrength > 0.01f)
		{
			var glow = _style.Trace with { A = _style.GlowStrength * (0.15f + _energy * 0.35f) };
			DrawCircle(plot.GetCenter(), 14f + _energy * 20f, glow);
		}

		DrawCornerTicks(plot);
	}

	private void DrawGrid(Rect2 plot)
	{
		DrawRect(plot, new Color(0f, 0f, 0f, 0.35f));
		var midY = plot.Position.Y + plot.Size.Y * 0.5f;
		DrawLine(new Vector2(plot.Position.X, midY), new Vector2(plot.End.X, midY),
			_style.Grid.Lightened(0.25f), 1.2f);

		for (var i = 1; i < 4; i++)
		{
			var x = plot.Position.X + plot.Size.X * (i / 4f);
			var y = plot.Position.Y + plot.Size.Y * (i / 4f);
			DrawLine(new Vector2(x, plot.Position.Y), new Vector2(x, plot.End.Y), _style.Grid, 1f);
			DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), _style.Grid, 1f);
		}
	}

	private void DrawWave(float[] samples, Rect2 plot, Color color, float width)
	{
		if (color.A <= 0.01f)
			return;

		var midY = plot.Position.Y + plot.Size.Y * 0.5f;
		var amp = plot.Size.Y * 0.38f;
		var pts = new Vector2[samples.Length];
		for (var i = 0; i < samples.Length; i++)
		{
			var x = plot.Position.X + plot.Size.X * (i / (float)(samples.Length - 1));
			pts[i] = new Vector2(x, midY - samples[i] * amp);
		}

		DrawPolyline(pts, color, width, true);
	}

	private void DrawReticles(Rect2 plot)
	{
		var c = plot.GetCenter();
		var a = _style.Accent with { A = 0.35f + _energy * 0.25f };
		DrawArc(c, 28f, 0f, Mathf.Tau, 40, a, 1f, true);
		DrawArc(c, 46f, _time, _time + 1.4f, 18, a.Lightened(0.2f), 1.5f, true);
	}

	private void DrawHexFrame(Vector2 center, float radius)
	{
		var pts = new Vector2[7];
		for (var i = 0; i < 6; i++)
		{
			var ang = -Mathf.Pi / 2f + i * Mathf.Tau / 6f;
			pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
		}

		pts[6] = pts[0];
		DrawPolyline(pts, _style.Accent with { A = 0.35f + _energy * 0.2f }, 1.4f, true);
	}

	private void DrawCornerTicks(Rect2 plot)
	{
		var a = _style.Accent with { A = 0.55f };
		const float arm = 14f;
		DrawPolyline(
			[new Vector2(plot.Position.X, plot.Position.Y + arm), plot.Position,
				new Vector2(plot.Position.X + arm, plot.Position.Y)], a, 1.6f);
		DrawPolyline(
			[new Vector2(plot.End.X - arm, plot.Position.Y), new Vector2(plot.End.X, plot.Position.Y),
				new Vector2(plot.End.X, plot.Position.Y + arm)], a, 1.6f);
		DrawPolyline(
			[new Vector2(plot.Position.X, plot.End.Y - arm), new Vector2(plot.Position.X, plot.End.Y),
				new Vector2(plot.Position.X + arm, plot.End.Y)], a, 1.6f);
		DrawPolyline(
			[new Vector2(plot.End.X - arm, plot.End.Y), new Vector2(plot.End.X, plot.End.Y),
				new Vector2(plot.End.X, plot.End.Y - arm)], a, 1.6f);
	}
}
