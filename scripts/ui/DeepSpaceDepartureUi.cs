using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mechanize;

/// <summary>
/// Post-signing departure cinematic. A drifting starfield under fading story cards:
/// the sponsoring manufacturer's send-off (with house chrome), then the shared
/// "you leave as a mercenary" spine, ending at a lonely relay station.
/// Hands off to the sector map.
/// </summary>
public partial class DeepSpaceDepartureUi : Control
{
	private const string DepartureTrack = "Derelict Vessel.mp3";
	private const int StarCount = 190;
	private const float EmblemSize = 220f;

	private static readonly Color Spine = new(0.88f, 0.9f, 0.95f);
	private static readonly Color Gold = new(0.85f, 0.7f, 0.38f);
	private static readonly Color RelayCyan = new(0.5f, 0.78f, 0.85f);

	private sealed class Card
	{
		public required string Text { get; init; }
		public Color Color { get; init; } = Spine;
		public int FontSize { get; init; } = 22;
		/// <summary>Extra beats to linger; longer/heavier lines hold on screen.</summary>
		public float HoldBonus { get; init; }
		/// <summary>Manufacturer send-off beat — drives house chrome visibility.</summary>
		public bool IsHouse { get; init; }
		/// <summary>Title card for the house name (larger mark, no quote body).</summary>
		public bool IsHouseTitle { get; init; }
		/// <summary>Anonymous sector-relay coms — shows a generic oscilloscope.</summary>
		public bool IsRelayComs { get; init; }
		/// <summary>Spoken line for TextVoice when this card uses the relay scope.</summary>
		public string SpokenText { get; init; } = "";
	}

	private readonly RandomNumberGenerator _rng = new();
	private Vector2[] _starDir = System.Array.Empty<Vector2>();
	private float[] _starRadius = System.Array.Empty<float>();
	private float[] _starSpeed = System.Array.Empty<float>();
	private float _warp = 1f;
	private float _time;

	private string _mfgId = "trinova";
	private FrontierCompanyData? _company;
	private Color _accent = new(0.35f, 0.72f, 0.42f);
	private float _houseChrome; // 0..1 — fades with manufacturer opening
	private float _housePulse;

	private Label? _card;
	private Label? _speaker;
	private Label? _skipHint;
	private ColorRect? _wash;
	private ColorRect? _veil;
	private Control? _emblemRoot;
	private VoiceOscilloscope? _relayScope;

	private bool _advance;
	private bool _skipAll;
	private bool _finished;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_rng.Randomize();
		ResolveManufacturer();
		SeedStars();
		Build();
		TextVoiceService.TokenSpoken -= OnRelayTokenSpoken;
		TextVoiceService.TokenSpoken += OnRelayTokenSpoken;
		MusicService.CueTrack(DepartureTrack);
		_ = RunSequenceAsync();
	}

	public override void _ExitTree()
	{
		TextVoiceService.TokenSpoken -= OnRelayTokenSpoken;
		TextVoiceService.Stop();
		base._ExitTree();
	}

	private void OnRelayTokenSpoken(bool vowel, float pitchScale)
	{
		if (_relayScope is not { Visible: true })
			return;
		_relayScope.NotifyToken(vowel, pitchScale);
	}

	private void ResolveManufacturer()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		_company = session?.SolarCampaign.SelectedCompany;
		if (_company != null)
		{
			_mfgId = _company.Id;
			_accent = _company.AccentColor;
			return;
		}
		_mfgId = session?.Profile.AffiliatedManufacturerId ?? "";
		if (string.IsNullOrEmpty(_mfgId))
			_mfgId = "trinova";
		var mfg = GameCatalog.GetManufacturer(_mfgId);
		_accent = mfg.AccentColor;
	}

	private void SeedStars()
	{
		_starDir = new Vector2[StarCount];
		_starRadius = new float[StarCount];
		_starSpeed = new float[StarCount];
		for (var i = 0; i < StarCount; i++)
			ResetStar(i, initial: true);
	}

	private void ResetStar(int i, bool initial)
	{
		var angle = _rng.RandfRange(0f, Mathf.Tau);
		_starDir[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		_starRadius[i] = initial ? _rng.RandfRange(4f, MaxRadius()) : _rng.RandfRange(2f, 16f);
		_starSpeed[i] = _rng.RandfRange(0.55f, 1.5f);
	}

	private float MaxRadius()
	{
		var s = GetViewportRect().Size;
		return s.Length() * 0.5f + 40f;
	}

	private void Build()
	{
		var bg = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.05f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		_wash = new ColorRect
		{
			Color = _accent with { A = 0f },
			MouseFilter = MouseFilterEnum.Ignore
		};
		_wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_wash);

		_emblemRoot = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f),
			PivotOffset = new Vector2(EmblemSize * 0.5f, EmblemSize * 0.5f)
		};
		_emblemRoot.AnchorLeft = 0.5f;
		_emblemRoot.AnchorTop = 0.5f;
		_emblemRoot.AnchorRight = 0.5f;
		_emblemRoot.AnchorBottom = 0.5f;
		_emblemRoot.OffsetLeft = -EmblemSize * 0.5f;
		_emblemRoot.OffsetTop = -EmblemSize * 0.5f - 210f;
		_emblemRoot.OffsetRight = EmblemSize * 0.5f;
		_emblemRoot.OffsetBottom = EmblemSize * 0.5f - 210f;
		AddChild(_emblemRoot);

		var houseEmblem = ManufacturerBrand.MakeEmblemOrFallback(_mfgId, _accent, EmblemSize, alpha: 0.92f);
		houseEmblem.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		if (houseEmblem is TextureRect tex)
		{
			tex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			// Slight cool lift for OuroTech's authored serpent; others stay neutral.
			if (_mfgId == "ourotech")
				tex.Modulate = new Color(1.05f, 1.1f, 1.2f, 0.92f);
		}

		_emblemRoot.AddChild(houseEmblem);

		_speaker = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(_accent.R, _accent.G, _accent.B, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_speaker.AddThemeFontSizeOverride("font_size", 14);
		_speaker.AnchorLeft = 0.5f;
		_speaker.AnchorTop = 0.5f;
		_speaker.AnchorRight = 0.5f;
		_speaker.AnchorBottom = 0.5f;
		_speaker.OffsetLeft = -420f;
		_speaker.OffsetRight = 420f;
		_speaker.OffsetTop = -48f;
		_speaker.OffsetBottom = -18f;
		AddChild(_speaker);

		_relayScope = new VoiceOscilloscope
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f)
		};
		_relayScope.Style = VoiceOscilloscopeStyle.Relay;
		_relayScope.AnchorLeft = 0.5f;
		_relayScope.AnchorTop = 0.5f;
		_relayScope.AnchorRight = 0.5f;
		_relayScope.AnchorBottom = 0.5f;
		_relayScope.OffsetLeft = -120f;
		_relayScope.OffsetRight = 120f;
		_relayScope.OffsetTop = -268f;
		_relayScope.OffsetBottom = -48f;
		AddChild(_relayScope);

		_card = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(1f, 1f, 1f, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_card.AnchorLeft = 0.5f;
		_card.AnchorTop = 0.5f;
		_card.AnchorRight = 0.5f;
		_card.AnchorBottom = 0.5f;
		_card.OffsetLeft = -560f;
		_card.OffsetRight = 560f;
		_card.OffsetTop = -40f;
		_card.OffsetBottom = 200f;
		_card.AddThemeFontSizeOverride("font_size", 22);
		_card.AddThemeConstantOverride("line_spacing", 10);
		AddChild(_card);

		_veil = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_veil);

		_skipHint = new Label
		{
			Text = "click or space — advance   ·   esc — skip",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.5f, 0.55f, 0.6f, 0.5f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_skipHint.AddThemeFontSizeOverride("font_size", 12);
		_skipHint.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		_skipHint.OffsetTop = -44;
		_skipHint.OffsetBottom = -22;
		AddChild(_skipHint);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_finished)
			return;

		if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause"))
		{
			_skipAll = true;
			TextVoiceService.Stop();
			GetViewport().SetInputAsHandled();
			return;
		}

		var advance =
			@event is InputEventMouseButton { Pressed: true }
			|| @event is InputEventKey { Pressed: true, Echo: false };
		if (advance)
		{
			_advance = true;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (_finished)
			return;

		var dt = (float)delta;
		_time += dt;
		_housePulse = 0.55f + 0.45f * Mathf.Sin(_time * 1.6f);

		var max = MaxRadius();
		for (var i = 0; i < _starRadius.Length; i++)
		{
			_starRadius[i] += dt * _warp * (14f + _starRadius[i] * 0.9f) * _starSpeed[i];
			if (_starRadius[i] > max)
				ResetStar(i, initial: false);
		}

		if (_emblemRoot != null && _houseChrome > 0.01f)
		{
			// Authored marks get a slow turn; keep it restrained so logos stay readable.
			var spin = ManufacturerBrand.TryGetTexture(_mfgId, out _) ? 8f : 4f;
			_emblemRoot.RotationDegrees += dt * spin * _houseChrome;
		}

		ApplyHouseChromeVisuals();
		QueueRedraw();
	}

	private void ApplyHouseChromeVisuals()
	{
		if (_wash != null)
		{
			// Soft ambient wash — stronger near the bottom for forge heat, even for others.
			var a = _houseChrome * (0.08f + 0.04f * _housePulse);
			_wash.Color = _accent with { A = a };
		}

		if (_emblemRoot != null)
			_emblemRoot.Modulate = new Color(1f, 1f, 1f, _houseChrome * 0.95f);

		if (_speaker != null)
			_speaker.Modulate = _accent with { A = _houseChrome * 0.85f };
	}

	public override void _Draw()
	{
		var size = GetViewportRect().Size;
		var center = size * 0.5f;

		DrawStars(center);
		if (_houseChrome > 0.01f)
			DrawHouseFrame(size, center);
	}

	private void DrawStars(Vector2 center)
	{
		for (var i = 0; i < _starRadius.Length; i++)
		{
			var r = _starRadius[i];
			var head = center + _starDir[i] * r;
			var tailLen = Mathf.Min(r * 0.12f * _warp, 46f) + 1.5f;
			var tail = center + _starDir[i] * Mathf.Max(0f, r - tailLen);

			var depth = Mathf.Clamp(r / 520f, 0.08f, 1f);
			var baseCol = new Color(0.7f + depth * 0.3f, 0.78f + depth * 0.22f, 0.95f, 0.25f + depth * 0.7f);
			// During house opening, bleed manufacturer accent into the star streaks.
			var col = _houseChrome > 0.01f
				? baseCol.Lerp(_accent with { A = baseCol.A }, _houseChrome * 0.55f)
				: baseCol;
			DrawLine(tail, head, col, 1f + depth * 1.4f, true);
		}
	}

	private void DrawHouseFrame(Vector2 size, Vector2 center)
	{
		var a = _houseChrome;
		var accent = _accent with { A = a * (0.35f + 0.2f * _housePulse) };
		var accentHot = _accent.Lightened(0.25f) with { A = a * 0.55f };
		var dim = _accent.Darkened(0.45f) with { A = a * 0.22f };

		// Edge rails.
		DrawRect(new Rect2(0f, 0f, size.X, 3f), accentHot);
		DrawRect(new Rect2(0f, size.Y - 3f, size.X, 3f), accentHot);
		DrawRect(new Rect2(0f, 0f, 2f, size.Y), dim);
		DrawRect(new Rect2(size.X - 2f, 0f, 2f, size.Y), dim);

		// Corner brackets.
		const float arm = 36f;
		const float inset = 22f;
		DrawPolyline([new Vector2(inset, inset + arm), new Vector2(inset, inset), new Vector2(inset + arm, inset)],
			accentHot, 2.2f);
		DrawPolyline(
			[new Vector2(size.X - inset - arm, inset), new Vector2(size.X - inset, inset),
				new Vector2(size.X - inset, inset + arm)],
			accentHot, 2.2f);
		DrawPolyline(
			[new Vector2(inset, size.Y - inset - arm), new Vector2(inset, size.Y - inset),
				new Vector2(inset + arm, size.Y - inset)],
			accentHot, 2.2f);
		DrawPolyline(
			[new Vector2(size.X - inset - arm, size.Y - inset), new Vector2(size.X - inset, size.Y - inset),
				new Vector2(size.X - inset, size.Y - inset - arm)],
			accentHot, 2.2f);

		// Soft radial well behind the emblem / text.
		DrawCircle(center + new Vector2(0f, -40f), 210f, _accent with { A = a * 0.07f });
		DrawCircle(center + new Vector2(0f, -40f), 120f, _accent with { A = a * 0.05f });

		// House-specific atmosphere flourishes.
		switch (_mfgId)
		{
			case "brimforge":
				DrawBrimforgeAtmosphere(size, a);
				break;
			case "ourotech":
				DrawOuroAtmosphere(center, a);
				break;
			case "trinova":
				DrawTrinovaAtmosphere(center, a);
				break;
			case "lumina":
				DrawLuminaAtmosphere(center, a);
				break;
		}
	}

	private void DrawBrimforgeAtmosphere(Vector2 size, float a)
	{
		// Forge heat rising from the floor.
		DrawRect(new Rect2(0f, size.Y * 0.72f, size.X, size.Y * 0.28f),
			new Color(0.55f, 0.18f, 0.05f, a * 0.18f * _housePulse));
		DrawRect(new Rect2(0f, size.Y * 0.86f, size.X, size.Y * 0.14f),
			new Color(0.85f, 0.35f, 0.08f, a * 0.12f));
		// Heavy slab ticks.
		for (var i = 0; i < 5; i++)
		{
			var y = size.Y * 0.78f + i * 14f;
			DrawLine(new Vector2(size.X * 0.28f, y), new Vector2(size.X * 0.72f, y),
				_accent with { A = a * (0.12f - i * 0.015f) }, 3f);
		}
	}

	private void DrawOuroAtmosphere(Vector2 center, float a)
	{
		// Precision reticle rings.
		DrawArc(center + new Vector2(0f, -40f), 150f, 0f, Mathf.Tau, 64,
			_accent with { A = a * 0.18f }, 1.2f, true);
		DrawArc(center + new Vector2(0f, -40f), 180f, _time * 0.4f, _time * 0.4f + 1.8f, 24,
			_accent.Lightened(0.2f) with { A = a * 0.35f }, 2f, true);
		DrawArc(center + new Vector2(0f, -40f), 180f, _time * 0.4f + Mathf.Pi, _time * 0.4f + Mathf.Pi + 1.8f, 24,
			_accent.Lightened(0.2f) with { A = a * 0.35f }, 2f, true);
	}

	private void DrawTrinovaAtmosphere(Vector2 center, float a)
	{
		// Logistics lane ticks — three parallel routes.
		for (var lane = -1; lane <= 1; lane++)
		{
			var y = center.Y + 210f + lane * 18f;
			DrawLine(new Vector2(center.X - 280f, y), new Vector2(center.X + 280f, y),
				_accent with { A = a * 0.2f }, 1.5f);
			var dashX = Mathf.PosMod(_time * 40f + lane * 30f, 48f);
			for (var x = center.X - 280f + dashX; x < center.X + 280f; x += 48f)
				DrawLine(new Vector2(x, y), new Vector2(x + 16f, y),
					_accent.Lightened(0.25f) with { A = a * 0.45f }, 2.2f);
		}
	}

	private void DrawLuminaAtmosphere(Vector2 center, float a)
	{
		// Vault hex ghost + classified scan sweep.
		var c = center + new Vector2(0f, -40f);
		DrawHex(c, 165f, _accent with { A = a * 0.14f }, 1.4f);
		DrawHex(c, 130f, _accent.Lightened(0.2f) with { A = a * 0.1f }, 1f);
		var sweep = Mathf.PosMod(_time * 70f, 360f) - 180f;
		DrawRect(new Rect2(c.X - 200f, c.Y + sweep, 400f, 18f),
			new Color(0.7f, 0.45f, 0.95f, a * 0.08f));
	}

	private void DrawHex(Vector2 center, float radius, Color color, float width)
	{
		var pts = new Vector2[7];
		for (var i = 0; i < 6; i++)
		{
			var ang = -Mathf.Pi / 2f + i * Mathf.Tau / 6f;
			pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
		}

		pts[6] = pts[0];
		DrawPolyline(pts, color, width, true);
	}

	private List<Card> BuildScript()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var mfg = _company == null ? GameCatalog.GetManufacturer(_mfgId) : null;
		var liaison = _company?.TrialTemplate ?? ConventionCatalog.Get(_mfgId);
		var clearance = _company?.ShortName
			?? session?.Profile.MercCorpName
			?? VoidCorpsIdentity.PlayerCorpCodename;

		var cards = new List<Card>();

		void House(string line) => cards.Add(new Card
		{
			Text = line,
			Color = _accent.Lightened(0.15f),
			FontSize = 24,
			HoldBonus = 1.4f,
			IsHouse = true
		});

		cards.Add(new Card
		{
			Text = (_company?.DisplayName ?? mfg!.DisplayName).ToUpperInvariant(),
			Color = _accent.Lightened(0.2f),
			FontSize = 36,
			HoldBonus = 0.8f,
			IsHouse = true,
			IsHouseTitle = true
		});

		var departureLines = _company?.DepartureLines() ?? liaison.DepartureLines;
		foreach (var line in departureLines)
			House($"\"{line}\"");

		cards.Add(new Card { Text = "YOU GRADUATED AS A PILOT.\nYOU LEAVE AS A MERCENARY.", Color = Gold, FontSize = 30, HoldBonus = 1.6f });
		cards.Add(new Card { Text = "Your MAP is locked into a freight cradle.\nThe convention lights fall away behind the ship.", HoldBonus = 0.8f });
		cards.Add(new Card { Text = "Beyond the core systems lie thousands of unregistered worlds.\nNew routes. New resources. New claims.", HoldBonus = 1f });
		cards.Add(new Card
		{
			Text = _company == null
				? "The Big Four cannot move openly.\nSo they fund those who can."
				: $"{_company.DisplayName} is not a manufacturer.\nIts frontier claim will stand or fall on the people it hired.",
			FontSize = 26,
			HoldBonus = 1.2f
		});
		cards.Add(new Card
		{
			Text = _company == null
				? "You are independent.\nYour equipment is licensed.\nYour orders are deniable."
				: $"You are under contract.\nYour equipment comes from open licenses.\nYour first obligation is {_company.SettlementVision.ToLowerInvariant()}.",
			Color = Gold,
			FontSize = 25,
			HoldBonus = 1.4f
		});
		cards.Add(new Card { Text = "The jumps blur together. Frost creeps across the hull. The lanes empty out.", HoldBonus = 0.8f });
		cards.Add(new Card
		{
			Text = "SECTOR RELAY\n\"Unregistered transport. Transmit identification.\"",
			Color = RelayCyan,
			FontSize = 22,
			HoldBonus = 0.6f,
			IsRelayComs = true,
			SpokenText = "Unregistered transport. Transmit identification."
		});
		cards.Add(new Card
		{
			Text = $"CLEARANCE  ·  {clearance.ToUpperInvariant()}\n\"Identification accepted. Proceed to the marked approach.\"",
			Color = RelayCyan,
			FontSize = 22,
			HoldBonus = 0.8f,
			IsRelayComs = true,
			SpokenText = "Identification accepted. Proceed to the marked approach."
		});
		cards.Add(new Card
		{
			Text = "FEW HAVE HEARD OF THIS SECTOR.\nFEWER HAVE COME BACK.\n\nTIME TO EARN THAT PAY.",
			Color = Gold,
			FontSize = 28,
			HoldBonus = 2f
		});

		return cards;
	}

	private string SpeakerLine(string mfgId)
	{
		if (_company != null)
			return $"{_company.DisplayName.ToUpperInvariant()}  ·  {_company.LiaisonName.ToUpperInvariant()}  ·  {_company.LiaisonTitle.ToUpperInvariant()}";
		var mfg = GameCatalog.GetManufacturer(mfgId);
		var liaison = ConventionCatalog.Get(mfgId);
		return $"{mfg.DisplayName.ToUpperInvariant()}  ·  {liaison.LiaisonName.ToUpperInvariant()}  ·  {liaison.LiaisonTitle.ToUpperInvariant()}";
	}

	private async Task RunSequenceAsync()
	{
		var cards = BuildScript();

		var warmup = CreateTween();
		warmup.TweenMethod(Callable.From<float>(w => _warp = w), 0.35f, 1f, 3.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		var houseActive = false;
		foreach (var card in cards)
		{
			if (_skipAll)
				break;

			if (card.IsHouse && !houseActive)
			{
				houseActive = true;
				if (_speaker != null)
					_speaker.Text = SpeakerLine(_mfgId);
				await TweenHouseChrome(1f, 0.9f);
			}
			else if (!card.IsHouse && houseActive)
			{
				houseActive = false;
				await TweenHouseChrome(0f, 1.1f);
				if (_speaker != null)
					_speaker.Text = "";
			}

			await ShowCardAsync(card);
		}

		if (houseActive)
			await TweenHouseChrome(0f, 0.5f);

		await FinishAsync();
	}

	private async Task TweenHouseChrome(float target, double seconds)
	{
		var start = _houseChrome;
		var elapsed = 0.0;
		while (elapsed < seconds && !_skipAll && GodotObject.IsInstanceValid(this))
		{
			elapsed += 0.04;
			var t = Mathf.Clamp((float)(elapsed / seconds), 0f, 1f);
			t = t * t * (3f - 2f * t); // smoothstep
			_houseChrome = Mathf.Lerp(start, target, t);
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
		}

		_houseChrome = target;
	}

	private async Task ShowCardAsync(Card card)
	{
		if (_card == null)
			return;

		_advance = false;
		_card.Text = card.Text;
		_card.Modulate = card.Color with { A = 0f };
		_card.AddThemeFontSizeOverride("font_size", card.FontSize);

		// Title card sits higher so the emblem owns the center.
		if (card.IsHouseTitle)
		{
			_card.OffsetTop = 40f;
			_card.OffsetBottom = 140f;
		}
		else if (card.IsHouse)
		{
			_card.OffsetTop = -20f;
			_card.OffsetBottom = 200f;
		}
		else if (card.IsRelayComs)
		{
			_card.OffsetTop = -20f;
			_card.OffsetBottom = 180f;
		}
		else
		{
			_card.OffsetTop = -140f;
			_card.OffsetBottom = 140f;
		}

		if (card.IsRelayComs)
			await ShowRelayScopeAsync(true);
		else
			await ShowRelayScopeAsync(false);

		await FadeCard(card.Color, 0.9f);
		if (_skipAll)
		{
			TextVoiceService.Stop();
			return;
		}

		if (card.IsRelayComs && !string.IsNullOrEmpty(card.SpokenText))
		{
			TextVoiceService.Speak(card.SpokenText, TextVoiceService.CorpOpsProfile);
			while (TextVoiceService.IsSpeaking && !_skipAll && !_advance && GodotObject.IsInstanceValid(this))
				await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
			if (_advance || _skipAll)
			{
				TextVoiceService.Stop();
				_relayScope?.NotifySilence();
				_advance = false;
			}
			if (_skipAll)
				return;
		}

		var hold = card.IsRelayComs
			? 0.85f + card.HoldBonus
			: 1.9f + card.Text.Length * 0.018f + card.HoldBonus;
		await WaitCard(hold);
		if (_skipAll)
		{
			TextVoiceService.Stop();
			return;
		}

		TextVoiceService.Stop();
		await FadeCard(card.Color with { A = 0f }, 0.6f);
	}

	private async Task ShowRelayScopeAsync(bool visible)
	{
		if (_relayScope == null)
			return;

		if (visible)
		{
			_relayScope.Visible = true;
			_relayScope.Style = VoiceOscilloscopeStyle.Relay;
			_relayScope.NotifySilence();
		}

		var start = _relayScope.Modulate.A;
		var target = visible ? 1f : 0f;
		if (Mathf.IsEqualApprox(start, target))
		{
			if (!visible)
				_relayScope.Visible = false;
			return;
		}

		var elapsed = 0.0;
		const double seconds = 0.35;
		while (elapsed < seconds && !_skipAll && GodotObject.IsInstanceValid(this))
		{
			elapsed += 0.04;
			var t = Mathf.Clamp((float)(elapsed / seconds), 0f, 1f);
			t = t * t * (3f - 2f * t);
			_relayScope.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(start, target, t));
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
		}

		_relayScope.Modulate = new Color(1f, 1f, 1f, target);
		if (!visible)
			_relayScope.Visible = false;
	}

	private async Task FadeCard(Color target, double seconds)
	{
		if (_card == null)
			return;
		var tween = CreateTween();
		tween.TweenProperty(_card, "modulate", target, seconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		var elapsed = 0.0;
		while (elapsed < seconds && !_skipAll && GodotObject.IsInstanceValid(this))
		{
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
			elapsed += 0.04;
			if (_advance)
			{
				_advance = false;
				tween.Kill();
				_card.Modulate = target;
				return;
			}
		}
	}

	private async Task WaitCard(double seconds)
	{
		var remaining = seconds;
		while (remaining > 0.0 && !_skipAll && GodotObject.IsInstanceValid(this))
		{
			await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
			remaining -= 0.05;
			if (_advance)
			{
				_advance = false;
				return;
			}
		}
	}

	private async Task FinishAsync()
	{
		if (_finished)
			return;
		_finished = true;

		TextVoiceService.Stop();
		if (_relayScope != null)
		{
			_relayScope.Visible = false;
			_relayScope.Modulate = new Color(1f, 1f, 1f, 0f);
		}

		if (_skipHint != null)
			_skipHint.Visible = false;

		if (_veil != null)
		{
			var fade = CreateTween();
			fade.TweenProperty(_veil, "color", new Color(0f, 0f, 0f, 1f), 0.8f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.In);
			await ToSignal(fade, Tween.SignalName.Finished);
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		GetTree().ChangeSceneToFile(session?.SolarCampaign.OnboardingComplete == true
			? "res://scenes/solar_system_map.tscn"
			: "res://scenes/campaign_map.tscn");
	}
}

/// <summary>Procedural house mark for manufacturers without dedicated art assets.</summary>
public partial class ManufacturerMarkDrawer : Control
{
	public string ManufacturerId { get; set; } = "trinova";
	public Color Accent { get; set; } = Colors.Gray;
	public float Pulse { get; set; } = 1f;

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		var c = Size * 0.5f;
		var r = Mathf.Min(Size.X, Size.Y) * 0.42f;
		var a = Modulate.A;
		if (a < 0.01f)
			return;

		switch (ManufacturerId)
		{
			case "brimforge":
				DrawBrimforge(c, r, a);
				break;
			case "trinova":
				DrawTrinova(c, r, a);
				break;
			case "lumina":
				DrawLumina(c, r, a);
				break;
			case "ashwhisk":
				DrawAshwhisk(c, r, a);
				break;
			case "velhound":
				DrawVelhound(c, r, a);
				break;
			default:
				DrawFallback(c, r, a);
				break;
		}
	}

	private void DrawBrimforge(Vector2 c, float r, float a)
	{
		// Anvil / forge slab: thick horizontal body, stubby legs, rising heat notch.
		var body = new Rect2(c.X - r * 0.7f, c.Y - r * 0.15f, r * 1.4f, r * 0.38f);
		DrawRect(body, Accent with { A = a * 0.85f });
		DrawRect(new Rect2(c.X - r * 0.55f, c.Y - r * 0.42f, r * 0.35f, r * 0.28f),
			Accent.Lightened(0.15f) with { A = a * 0.75f });
		DrawRect(new Rect2(c.X - r * 0.45f, c.Y + r * 0.22f, r * 0.22f, r * 0.35f),
			Accent.Darkened(0.2f) with { A = a * 0.8f });
		DrawRect(new Rect2(c.X + r * 0.2f, c.Y + r * 0.22f, r * 0.22f, r * 0.35f),
			Accent.Darkened(0.2f) with { A = a * 0.8f });
		// Heat glow.
		DrawCircle(c + new Vector2(0f, -r * 0.55f), r * 0.18f * Pulse,
			new Color(1f, 0.45f, 0.12f, a * 0.35f * Pulse));
		DrawArc(c, r * 0.95f, 0f, Mathf.Tau, 48, Accent with { A = a * 0.35f }, 3f, true);
	}

	private void DrawTrinova(Vector2 c, float r, float a)
	{
		// Triple chevron / wing — fleet logistics.
		for (var i = 0; i < 3; i++)
		{
			var y = c.Y - r * 0.35f + i * r * 0.32f;
			var spread = r * (0.55f + i * 0.12f);
			var col = Accent.Lightened(0.1f * i) with { A = a * (0.9f - i * 0.15f) };
			DrawPolyline(
				[new Vector2(c.X - spread, y + r * 0.18f), new Vector2(c.X, y - r * 0.08f),
					new Vector2(c.X + spread, y + r * 0.18f)],
				col, 4.5f - i, true);
		}

		DrawArc(c, r * 0.95f, 0f, Mathf.Tau, 48, Accent with { A = a * 0.3f }, 2f, true);
	}

	private void DrawLumina(Vector2 c, float r, float a)
	{
		// Vault hex + inner oracle ring.
		var outer = new Vector2[7];
		var inner = new Vector2[7];
		for (var i = 0; i < 6; i++)
		{
			var ang = -Mathf.Pi / 2f + i * Mathf.Tau / 6f;
			outer[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * 0.9f;
			inner[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * 0.55f;
		}

		outer[6] = outer[0];
		inner[6] = inner[0];
		DrawPolyline(outer, Accent with { A = a * 0.85f }, 3f, true);
		DrawPolyline(inner, Accent.Lightened(0.25f) with { A = a * 0.55f }, 1.8f, true);
		DrawCircle(c, r * 0.18f * Pulse, Accent.Lightened(0.3f) with { A = a * 0.5f * Pulse });
		DrawArc(c, r * 0.35f, 0f, Mathf.Tau, 40, Accent with { A = a * 0.4f }, 1.5f, true);
	}

	private void DrawAshwhisk(Vector2 c, float r, float a)
	{
		// AWSK compass-X monogram: geometric cross + stubs at A/W/S/K points.
		var col = Accent with { A = a * 0.9f };
		var arm = r * 0.72f;
		DrawLine(c + new Vector2(-arm, -arm), c + new Vector2(arm, arm), col, 3.2f, true);
		DrawLine(c + new Vector2(arm, -arm), c + new Vector2(-arm, arm), col, 3.2f, true);
		DrawCircle(c + new Vector2(0f, -r * 0.78f), r * 0.1f, Accent.Lightened(0.2f) with { A = a * 0.85f }); // A
		DrawCircle(c + new Vector2(-r * 0.78f, 0f), r * 0.1f, Accent.Lightened(0.2f) with { A = a * 0.85f }); // W
		DrawCircle(c + new Vector2(r * 0.78f, 0f), r * 0.1f, Accent.Lightened(0.2f) with { A = a * 0.85f }); // S
		DrawCircle(c + new Vector2(0f, r * 0.78f), r * 0.1f, Accent.Lightened(0.2f) with { A = a * 0.85f }); // K
		DrawArc(c, r * 0.95f, 0f, Mathf.Tau, 48, Accent with { A = a * 0.3f }, 2f, true);
	}

	private void DrawVelhound(Vector2 c, float r, float a)
	{
		// Industrial hound silhouette: snout wedge + ear notch + thick neck bar.
		var body = new Rect2(c.X - r * 0.55f, c.Y - r * 0.12f, r * 0.95f, r * 0.42f);
		DrawRect(body, Accent with { A = a * 0.85f });
		var snout = new[]
		{
			c + new Vector2(r * 0.35f, -r * 0.05f),
			c + new Vector2(r * 0.95f, r * 0.08f),
			c + new Vector2(r * 0.35f, r * 0.28f)
		};
		DrawColoredPolygon(snout, Accent.Lightened(0.1f) with { A = a * 0.8f });
		DrawRect(new Rect2(c.X - r * 0.35f, c.Y - r * 0.55f, r * 0.28f, r * 0.35f),
			Accent.Darkened(0.15f) with { A = a * 0.8f }); // ear
		DrawCircle(c + new Vector2(r * 0.05f, -r * 0.02f), r * 0.08f,
			new Color(1f, 0.85f, 0.55f, a * 0.45f * Pulse)); // optic
		DrawArc(c, r * 0.95f, 0f, Mathf.Tau, 48, Accent with { A = a * 0.32f }, 2.5f, true);
	}

	private void DrawFallback(Vector2 c, float r, float a)
	{
		DrawArc(c, r * 0.85f, 0f, Mathf.Tau, 48, Accent with { A = a * 0.7f }, 3f, true);
		DrawCircle(c, r * 0.25f, Accent with { A = a * 0.5f });
	}
}
