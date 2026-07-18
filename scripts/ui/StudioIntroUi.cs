using System.Threading.Tasks;
using Godot;

namespace Mechanize;

/// <summary>
/// Boot bumper: studio typewriter + ouroboros that glitches into the OuroTech mark,
/// then hands off to the hangar menu.
/// </summary>
public partial class StudioIntroUi : Control
{
	private const string OuroborosPath = "res://art/ui/ouroboros.png";
	private const string OuroTechPath = "res://art/ui/ourotech_ouroboros.png";
	private const string LineStudio = "Celtic Trinity Studios Presents...";
	private const string LineAuthor = "a Game by Kael...";
	private const float EmblemSize = 400f;

	private static readonly Color OuroTechCyan = new(0.25f, 0.55f, 0.78f);

	private ColorRect? _veil;
	private Control? _emblem;
	private TextureRect? _ouroClassic;
	private TextureRect? _ouroTech;
	private ColorRect? _glitchFlash;
	private Label? _studioLabel;
	private Label? _authorLabel;
	private Label? _corpTag;
	private bool _skipRequested;
	private bool _finished;
	private bool _finishing;
	private bool _spinning = true;
	private System.Action? _onFinished;

	public static StudioIntroUi Create(System.Action onFinished)
	{
		var intro = new StudioIntroUi { Name = "StudioIntroUi" };
		intro._onFinished = onFinished;
		return intro;
	}

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Build();
		_ = RunSequenceAsync();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_finished)
			return;

		var skip =
			(@event is InputEventMouseButton { Pressed: true })
			|| (@event is InputEventKey { Pressed: true, Echo: false })
			|| (@event.IsActionPressed("ui_accept"))
			|| (@event.IsActionPressed("ui_cancel"))
			|| (@event.IsActionPressed("pause"));

		if (!skip)
			return;

		_skipRequested = true;
		GetViewport().SetInputAsHandled();
	}

	public override void _Process(double delta)
	{
		if (_emblem == null || !_spinning || _finished)
			return;

		_emblem.RotationDegrees += (float)delta * 18f;
	}

	private void Build()
	{
		_veil = new ColorRect
		{
			Color = Colors.Black,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_veil);

		_emblem = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			PivotOffset = new Vector2(EmblemSize * 0.5f, EmblemSize * 0.5f)
		};
		_emblem.AnchorLeft = 0.5f;
		_emblem.AnchorTop = 0.5f;
		_emblem.AnchorRight = 0.5f;
		_emblem.AnchorBottom = 0.5f;
		_emblem.OffsetLeft = -EmblemSize * 0.5f;
		_emblem.OffsetTop = -EmblemSize * 0.5f - 36f;
		_emblem.OffsetRight = EmblemSize * 0.5f;
		_emblem.OffsetBottom = EmblemSize * 0.5f - 36f;
		AddChild(_emblem);

		_ouroClassic = MakeEmblemRect(GD.Load<Texture2D>(OuroborosPath), visibleAlpha: 0f);
		_emblem.AddChild(_ouroClassic);

		_ouroTech = MakeEmblemRect(GD.Load<Texture2D>(OuroTechPath), visibleAlpha: 0f);
		_emblem.AddChild(_ouroTech);

		_glitchFlash = new ColorRect
		{
			Color = new Color(OuroTechCyan.R, OuroTechCyan.G, OuroTechCyan.B, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_glitchFlash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_glitchFlash);

		var credits = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		credits.AddThemeConstantOverride("separation", 18);
		credits.AnchorLeft = 0.5f;
		credits.AnchorTop = 0.5f;
		credits.AnchorRight = 0.5f;
		credits.AnchorBottom = 0.5f;
		credits.OffsetLeft = -480f;
		credits.OffsetRight = 480f;
		credits.OffsetTop = 190f;
		credits.OffsetBottom = 340f;
		AddChild(credits);

		_studioLabel = MakeCreditLabel(42);
		credits.AddChild(_studioLabel);

		_authorLabel = MakeCreditLabel(28);
		_authorLabel.Modulate = MechUiTheme.Muted;
		credits.AddChild(_authorLabel);

		_corpTag = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(OuroTechCyan.R, OuroTechCyan.G, OuroTechCyan.B, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_corpTag.AddThemeFontSizeOverride("font_size", 22);
		credits.AddChild(_corpTag);

		var skipHint = new Label
		{
			Text = "click or any key to skip",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.45f, 0.5f, 0.55f, 0.55f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		skipHint.AddThemeFontSizeOverride("font_size", 12);
		skipHint.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		skipHint.OffsetTop = -48;
		skipHint.OffsetBottom = -24;
		AddChild(skipHint);
	}

	private static TextureRect MakeEmblemRect(Texture2D? tex, float visibleAlpha)
	{
		var rect = new TextureRect
		{
			Texture = tex,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = new Color(1f, 1f, 1f, visibleAlpha),
			MouseFilter = MouseFilterEnum.Ignore
		};
		rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return rect;
	}

	private static Label MakeCreditLabel(int fontSize)
	{
		var label = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private async Task RunSequenceAsync()
	{
		var emblemFade = CreateTween();
		emblemFade.TweenProperty(_ouroClassic, "modulate:a", 0.92f, 2.4f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);

		await TypeLineAsync(_studioLabel!, LineStudio, 0.055);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		await WaitOrSkip(0.4);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		var revealHangar = CreateTween();
		revealHangar.TweenProperty(_veil, "modulate:a", 0.42f, 2.2f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);

		await TypeLineAsync(_authorLabel!, LineAuthor, 0.07);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		await WaitOrSkip(0.9);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		var textFade = CreateTween();
		textFade.SetParallel(true);
		textFade.TweenProperty(_studioLabel, "modulate:a", 0f, 0.9f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		textFade.TweenProperty(_authorLabel, "modulate:a", 0f, 0.9f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		await ToSignal(textFade, Tween.SignalName.Finished);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		await WaitOrSkip(0.2);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		await GlitchToOuroTechAsync();
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		await WaitOrSkip(1.15);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		var serpentFade = CreateTween();
		serpentFade.SetParallel(true);
		serpentFade.TweenProperty(_ouroTech, "modulate:a", 0f, 1.6f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		serpentFade.TweenProperty(_corpTag, "modulate:a", 0f, 1.2f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		serpentFade.TweenProperty(_veil, "modulate:a", 0f, 1.6f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		await ToSignal(serpentFade, Tween.SignalName.Finished);
		if (_skipRequested)
		{
			FinishImmediately();
			return;
		}

		await FadeVeilAndFinish(0.4f);
	}

	private async Task GlitchToOuroTechAsync()
	{
		if (_emblem == null || _ouroClassic == null || _ouroTech == null)
			return;

		var baseL = _emblem.OffsetLeft;
		var baseT = _emblem.OffsetTop;
		var baseR = _emblem.OffsetRight;
		var baseB = _emblem.OffsetBottom;
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		void Jitter(float x, float y)
		{
			_emblem.OffsetLeft = baseL + x;
			_emblem.OffsetRight = baseR + x;
			_emblem.OffsetTop = baseT + y;
			_emblem.OffsetBottom = baseB + y;
		}

		void ResetPos() => Jitter(0f, 0f);

		// Stutter / tear while the classic mark is still showing.
		for (var i = 0; i < 10; i++)
		{
			if (_skipRequested)
				return;

			Jitter(rng.RandfRange(-18f, 18f), rng.RandfRange(-10f, 10f));
			_ouroClassic.Modulate = i % 2 == 0
				? new Color(1.4f, 0.2f, 0.85f, 0.85f)
				: new Color(0.2f, 1.2f, 1.4f, 0.9f);

			if (_glitchFlash != null)
				_glitchFlash.Color = new Color(OuroTechCyan.R, OuroTechCyan.G, OuroTechCyan.B, rng.RandfRange(0.04f, 0.14f));

			await WaitOrSkip(0.035 + rng.RandfRange(0f, 0.03f));
		}

		// Hard cut into the corporate mark.
		_ouroClassic.Modulate = new Color(1f, 1f, 1f, 0f);
		_ouroTech.Modulate = new Color(1f, 1f, 1f, 0f);
		Jitter(rng.RandfRange(-22f, 22f), 0f);
		await WaitOrSkip(0.05);
		if (_skipRequested)
			return;

		_ouroTech.Modulate = new Color(1.15f, 1.25f, 1.4f, 1f);
		ResetPos();
		if (_glitchFlash != null)
			_glitchFlash.Color = new Color(OuroTechCyan.R, OuroTechCyan.G, OuroTechCyan.B, 0.22f);

		for (var i = 0; i < 4; i++)
		{
			if (_skipRequested)
				return;
			_ouroTech.Modulate = new Color(1f, 1f, 1f, i % 2 == 0 ? 1f : 0.35f);
			Jitter(rng.RandfRange(-8f, 8f), rng.RandfRange(-4f, 4f));
			await WaitOrSkip(0.045);
		}

		ResetPos();
		_ouroTech.Modulate = Colors.White;
		if (_glitchFlash != null)
		{
			var flashOut = CreateTween();
			flashOut.TweenProperty(_glitchFlash, "color:a", 0f, 0.35f);
		}

		if (_corpTag != null)
		{
			_corpTag.Text = "OUROTECH";
			var tagIn = CreateTween();
			tagIn.TweenProperty(_corpTag, "modulate:a", 0.95f, 0.55f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
		}

		SfxService.Confirm();
	}

	private async Task TypeLineAsync(Label label, string fullText, double secondsPerChar)
	{
		label.Text = "";
		for (var i = 1; i <= fullText.Length; i++)
		{
			if (_skipRequested)
			{
				label.Text = fullText;
				return;
			}

			label.Text = fullText[..i];
			if (i % 3 == 0)
				SfxService.Click();
			await WaitOrSkip(secondsPerChar);
		}
	}

	private async Task WaitOrSkip(double seconds)
	{
		var remaining = seconds;
		while (remaining > 0.0 && !_skipRequested && GodotObject.IsInstanceValid(this))
		{
			var step = Mathf.Min(0.05f, (float)remaining);
			await ToSignal(GetTree().CreateTimer(step), SceneTreeTimer.SignalName.Timeout);
			remaining -= step;
		}
	}

	private void FinishImmediately()
	{
		if (_finished || _finishing)
			return;

		if (_studioLabel != null)
			_studioLabel.Text = LineStudio;
		if (_authorLabel != null)
			_authorLabel.Text = LineAuthor;

		_ = FadeVeilAndFinish(0.55f);
	}

	private async Task FadeVeilAndFinish(float seconds)
	{
		if (_finished || _finishing)
			return;
		_finishing = true;
		_finished = true;
		_spinning = false;

		var fade = CreateTween();
		fade.SetParallel(true);
		if (_veil != null)
			fade.TweenProperty(_veil, "modulate:a", 0f, seconds);
		if (_ouroClassic != null)
			fade.TweenProperty(_ouroClassic, "modulate:a", 0f, seconds * 0.7f);
		if (_ouroTech != null)
			fade.TweenProperty(_ouroTech, "modulate:a", 0f, seconds * 0.7f);
		if (_corpTag != null)
			fade.TweenProperty(_corpTag, "modulate:a", 0f, seconds * 0.5f);
		if (_studioLabel != null)
			fade.TweenProperty(_studioLabel, "modulate:a", 0f, seconds * 0.5f);
		if (_authorLabel != null)
			fade.TweenProperty(_authorLabel, "modulate:a", 0f, seconds * 0.5f);
		if (_glitchFlash != null)
			fade.TweenProperty(_glitchFlash, "color:a", 0f, seconds * 0.4f);

		await ToSignal(fade, Tween.SignalName.Finished);
		_onFinished?.Invoke();
		QueueFree();
	}
}
