using System.Collections.Generic;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>Job convention floor — frontier employers, recruiter pitches, trials, signing.</summary>
public partial class ConventionHallUi : Control
{
	private static readonly string[] HouseOrder = ["brimforge", "ourotech", "trinova", "lumina"];

	private CampaignRun _run = null!;
	private GameSession? _session;
	private Label? _status;
	private PanelContainer? _pitchPanel;
	private Label? _pitchTitle;
	private Label? _pitchBody;
	private HBoxContainer? _pitchButtons;
	private Button? _pitchAdvance;
	private PanelContainer? _pityPanel;
	private VoiceOscilloscope? _scope;

	private string? _openManufacturerId;
	/// <summary>Index into display lines (forgiveness prepended as line 0 when granted).</summary>
	private int _lineIndex;
	private readonly List<string> _lines = new();
	private bool _lineSpeaking;
	private string _currentFullLine = "";

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Campaign);
		ConventionCatalog.EnsureBuilt();
		_session = GetNodeOrNull<GameSession>("/root/GameSession");
		_run = _session?.Campaign ?? CampaignRun.Load() ?? CampaignRun.StartCadet();
		if (_run.Phase != CampaignPhase.ManufacturerConvention)
			_run.EnterConventionGate();
		if (_run.SolarOnboarding && _session != null)
		{
			foreach (var company in _session.SolarCampaign.ConventionCompanies)
				_run.Convention.Get(company.Id);
		}
		else
			_run.Convention.EnsureAllManufacturers();
		if (_session != null)
		{
			_session.Campaign = _run;
			_session.ReturnToConventionHall = false;
			_session.ClearConventionDemoLoaner();
		}

		Build();
		TextVoiceService.TokenSpoken -= OnTokenSpoken;
		TextVoiceService.TokenSpoken += OnTokenSpoken;
	}

	private void OnTokenSpoken(bool vowel, float pitchScale)
	{
		_scope?.NotifyToken(vowel, pitchScale);
	}

	public override void _ExitTree()
	{
		TextVoiceService.TokenSpoken -= OnTokenSpoken;
		TextVoiceService.Stop();
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_pitchPanel = null;
		_pityPanel = null;
		_scope = null;

		var dim = new ColorRect
		{
			Color = new Color(0.04f, 0.05f, 0.07f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var title = new Label
		{
			Text = _run.SolarOnboarding ? "FRONTIER JOB CONVENTION" : "BIG FOUR CONVENTION",
			Position = new Vector2(48, 28),
			Size = new Vector2(900, 40),
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		title.AddThemeFontSizeOverride("font_size", 30);
		AddChild(title);

		_status = new Label
		{
			Text = BuildStatusLine(),
			Position = new Vector2(48, 68),
			Size = new Vector2(1600, 48),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.65f, 0.72f, 0.78f)
		};
		_status.AddThemeFontSizeOverride("font_size", 14);
		AddChild(_status);

		var row = new HBoxContainer
		{
			Position = new Vector2(48, 130),
			Size = new Vector2(1824, 400),
			Alignment = BoxContainer.AlignmentMode.Center
		};
		row.AddThemeConstantOverride("separation", 18);
		AddChild(row);

		var participantIds = _run.SolarOnboarding && _session != null
			? System.Linq.Enumerable.Select(_session.SolarCampaign.ConventionCompanies, c => c.Id)
			: HouseOrder;
		foreach (var id in participantIds)
			row.AddChild(MakeBanner(id));

		BuildPitchPanel();
		BuildPityPanel();

		var back = new Button
		{
			Text = _run.SolarOnboarding ? "Main Menu" : "Sector Map",
			Position = new Vector2(1700, 28),
			CustomMinimumSize = new Vector2(160, 40)
		};
		back.Pressed += () =>
		{
			_run.Save();
			GetTree().ChangeSceneToFile(_run.SolarOnboarding
				? "res://scenes/main_menu.tscn"
				: "res://scenes/campaign_map.tscn");
		};
		AddChild(back);

		if (_run.Convention.AllWithdrawnWithNoQualify())
			ShowPity();
	}

	private Control MakeBanner(string manufacturerId)
	{
		var company = _run.SolarOnboarding ? _session?.GetFrontierCompany(manufacturerId) : null;
		var mfg = company == null ? GameCatalog.GetManufacturer(manufacturerId) : null;
		var def = company?.TrialTemplate ?? ConventionCatalog.Get(manufacturerId);
		var accent = company?.AccentColor ?? mfg!.AccentColor;
		var displayName = company?.DisplayName ?? mfg!.DisplayName;
		var status = _run.Convention.Get(manufacturerId);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420, 380),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.09f, 0.11f, 0.96f),
			BorderColor = accent,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			ContentMarginLeft = 16,
			ContentMarginTop = 16,
			ContentMarginRight = 16,
			ContentMarginBottom = 16,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		});

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 10);
		panel.AddChild(col);

		var emblem = ManufacturerBrand.MakeEmblemOrFallback(manufacturerId, accent, 96f);
		emblem.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		col.AddChild(emblem);

		var name = new Label
		{
			Text = displayName.ToUpperInvariant(),
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = accent
		};
		name.AddThemeFontSizeOverride("font_size", 22);
		col.AddChild(name);

		var focus = new Label
		{
			Text = company?.Motive ?? mfg!.Niche,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.7f, 0.75f, 0.8f)
		};
		focus.AddThemeFontSizeOverride("font_size", 12);
		col.AddChild(focus);

		var snippet = new Label
		{
			Text = company?.PublicPitch ?? def.BannerSnippet,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.78f, 0.82f, 0.86f)
		};
		snippet.AddThemeFontSizeOverride("font_size", 14);
		col.AddChild(snippet);

		var state = new Label
		{
			Text = StatusText(status),
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = StatusColor(status)
		};
		state.AddThemeFontSizeOverride("font_size", 13);
		col.AddChild(state);

		var open = new Button
		{
			Text = status.Withdrawn ? "Withdrawn — speak?" : "Speak with recruiter",
			CustomMinimumSize = new Vector2(0, 44)
		};
		open.Pressed += () =>
		{
			SfxService.Click();
			OpenPitch(manufacturerId);
		};
		col.AddChild(open);

		if (status.Withdrawn && !status.Qualified)
			panel.Modulate = new Color(0.55f, 0.55f, 0.58f);

		return panel;
	}

	private static string StatusText(ManufacturerConventionStatus status)
	{
		if (status.Qualified)
			return "QUALIFIED — ready to sign";
		if (status.Withdrawn)
			return status.Sabotaged
				? "OFFER WITHDRAWN · you torched their floor model"
				: "OFFER WITHDRAWN · 0 attempts";
		var sab = status.Sabotaged ? " · floor model destroyed" : "";
		return $"Attempts left: {status.AttemptsRemaining}/{ConventionState.MaxAttempts}{sab}";
	}

	private static Color StatusColor(ManufacturerConventionStatus status)
	{
		if (status.Qualified)
			return new Color(0.45f, 0.85f, 0.55f);
		if (status.Withdrawn)
			return new Color(0.9f, 0.4f, 0.35f);
		return new Color(0.75f, 0.78f, 0.82f);
	}

	private void BuildPitchPanel()
	{
		_pitchPanel = new PanelContainer
		{
			Visible = false,
			Position = new Vector2(220, 520),
			CustomMinimumSize = new Vector2(1480, 360)
		};
		_pitchPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.1f, 0.98f),
			BorderColor = new Color(0.62f, 0.5f, 0.28f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 16,
			ContentMarginTop = 14,
			ContentMarginRight = 16,
			ContentMarginBottom = 14,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		});
		AddChild(_pitchPanel);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		_pitchPanel.AddChild(row);

		_scope = new VoiceOscilloscope
		{
			CustomMinimumSize = new Vector2(240, 260),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		row.AddChild(_scope);

		var root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 10);
		row.AddChild(root);

		_pitchTitle = new Label { Modulate = new Color(0.85f, 0.7f, 0.38f) };
		_pitchTitle.AddThemeFontSizeOverride("font_size", 20);
		root.AddChild(_pitchTitle);

		_pitchBody = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0, 150),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = new Color(0.8f, 0.85f, 0.9f)
		};
		_pitchBody.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(_pitchBody);

		_pitchButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		_pitchButtons.AddThemeConstantOverride("separation", 12);
		root.AddChild(_pitchButtons);
	}

	private void BuildPityPanel()
	{
		var fallbackCompany = _run.SolarOnboarding ? _session?.SolarCampaign.ConventionCompanies[0] : null;
		var fallbackAccent = fallbackCompany?.AccentColor ?? GameCatalog.GetManufacturer("trinova").AccentColor;
		_pityPanel = new PanelContainer
		{
			Visible = false,
			Position = new Vector2(360, 200),
			CustomMinimumSize = new Vector2(1200, 280)
		};
		_pityPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.1f, 0.09f, 0.98f),
			BorderColor = fallbackAccent,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			ContentMarginLeft = 20,
			ContentMarginTop = 16,
			ContentMarginRight = 20,
			ContentMarginBottom = 16
		});
		AddChild(_pityPanel);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 12);
		_pityPanel.AddChild(col);

		var keel = ConventionCatalog.Get("trinova");
		var t = new Label
		{
			Text = fallbackCompany == null ? "TRINOVA SALVAGE CONTRACT" : "LAST-CHANCE FRONTIER CHARTER",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = fallbackAccent
		};
		t.AddThemeFontSizeOverride("font_size", 22);
		col.AddChild(t);

		var body = new Label
		{
			Text = fallbackCompany == null
				? $"Every other booth closed. {keel.LiaisonName} still has a scrap-heap MAP and a salvage affiliation.\nTake it, or leave the convention without a chassis."
				: $"Every evaluation offer closed. {fallbackCompany.DisplayName} will still issue a basic frontier charter and scrap-heap MAP.\nTake it, or leave without a company contract.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		body.AddThemeFontSizeOverride("font_size", 15);
		col.AddChild(body);

		var take = new Button
		{
			Text = fallbackCompany == null
				? "Accept salvage affiliation (Trinova)"
				: $"Accept last-chance charter ({fallbackCompany.ShortName})",
			CustomMinimumSize = new Vector2(0, 46),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		take.Pressed += () =>
		{
			SfxService.Confirm();
			if (_session?.AcceptPityContract() == true)
				GetTree().ChangeSceneToFile("res://scenes/deep_space_departure.tscn");
		};
		col.AddChild(take);
	}

	private void ShowPity()
	{
		if (_pityPanel != null)
			_pityPanel.Visible = true;
		if (_pitchPanel != null)
			_pitchPanel.Visible = false;
	}

	private void OpenPitch(string manufacturerId)
	{
		_openManufacturerId = manufacturerId;
		_lineIndex = 0;
		_lines.Clear();

		var company = _run.SolarOnboarding ? _session?.GetFrontierCompany(manufacturerId) : null;
		var def = company?.TrialTemplate ?? ConventionCatalog.Get(manufacturerId);
		var rival = _run.Convention.TryGrantForgiveness(manufacturerId);
		if (rival != null)
		{
			_run.Save();
			var rivalName = _session?.GetFrontierCompany(rival)?.DisplayName
				?? GameCatalog.GetManufacturer(rival).DisplayName;
			_lines.Add(company == null
				? def.FormatForgiveness(rivalName)
				: FormatCompanyLine(company, $"I saw what happened to {rivalName}'s evaluation asset. That opened one final slot. Use it well."));
		}

		var status = _run.Convention.Get(manufacturerId);
		if (status.Qualified)
			_lines.AddRange(company == null ? def.FormatLines(def.QualifiedReturnLines) : FormatCompanyLines(company, company.QualifiedLines()));
		else if (status.Withdrawn)
			_lines.AddRange(company == null ? def.FormatLines(def.WithdrawnReturnLines) : FormatCompanyLines(company, company.WithdrawnLines()));
		else if (status.AttemptsRemaining < ConventionState.MaxAttempts)
			_lines.AddRange(company == null ? def.FormatLines(def.FailedReturnLines) : FormatCompanyLines(company, company.FailedLines()));
		else
			_lines.AddRange(company == null ? def.FormatLines(def.PitchLines) : FormatCompanyLines(company, company.PitchLines()));

		if (_pitchPanel != null)
			_pitchPanel.Visible = true;
		RefreshPitch();
	}

	private void RefreshPitch()
	{
		if (_openManufacturerId == null || _pitchTitle == null || _pitchBody == null || _pitchButtons == null)
			return;

		var id = _openManufacturerId;
		var company = _run.SolarOnboarding ? _session?.GetFrontierCompany(id) : null;
		var mfg = company == null ? GameCatalog.GetManufacturer(id) : null;
		var def = company?.TrialTemplate ?? ConventionCatalog.Get(id);
		var accent = company?.AccentColor ?? mfg!.AccentColor;
		var status = _run.Convention.Get(id);

		_pitchTitle.Text = company == null
			? $"{mfg!.DisplayName}  ·  {def.LiaisonName}  ·  {def.LiaisonTitle}"
			: $"{company.DisplayName}  ·  {company.LiaisonName}  ·  {company.LiaisonTitle}";
		_pitchTitle.Modulate = accent;
		if (company == null)
			_scope?.SetManufacturer(id);
		else
			_scope?.SetCompany(company);
		if (_pitchPanel != null)
		{
			var style = _pitchPanel.GetThemeStylebox("panel") as StyleBoxFlat;
			if (style != null)
			{
				style = (StyleBoxFlat)style.Duplicate();
				style.BorderColor = accent;
				_pitchPanel.AddThemeStyleboxOverride("panel", style);
			}
		}

		var atActions = _lineIndex >= _lines.Count;
		if (!atActions)
		{
			var fullLine = _lines[_lineIndex];
			_currentFullLine = fullLine;
			var divider = fullLine.IndexOf('·');
			var speaker = divider >= 0 ? fullLine[..(divider + 1)] + "  " : "";
			var spoken = divider >= 0 ? fullLine[(divider + 1)..].TrimStart() : fullLine;
			var expectedLineIndex = _lineIndex;

			// Full text is the no-audio fallback. An active voice service immediately
			// resets the body to zero characters, then reveals it on each spoken beat.
			_pitchBody.Text = fullLine;
			TextVoiceService.Speak(
				spoken,
				company?.VoiceProfile ?? TextVoiceService.ProfileForManufacturer(id),
				revealed =>
				{
					if (_pitchBody == null || _openManufacturerId != id || _lineIndex != expectedLineIndex)
						return;
					var count = Mathf.Clamp(revealed, 0, spoken.Length);
					_pitchBody.Text = speaker + spoken[..count];
				},
				() =>
				{
					if (_pitchBody == null || _openManufacturerId != id || _lineIndex != expectedLineIndex)
						return;
					_lineSpeaking = false;
					_pitchBody.Text = fullLine;
					if (_pitchAdvance != null)
						_pitchAdvance.Text = "Continue";
				});
			_lineSpeaking = TextVoiceService.IsSpeaking;
		}
		else
		{
			TextVoiceService.Stop();
			_lineSpeaking = false;
			_scope?.NotifySilence();
			_pitchBody.Text =
				$"{_lines[^1]}\n\n" +
				$"Trial: {MissionCatalog.Get(def.TrialMission).Title}  ·  {StatusText(status)}\n" +
				$"{(company == null ? "Signing package" : "Company-issued starter package")}: {def.SigningBonusBlurb}";
		}

		foreach (var child in _pitchButtons.GetChildren())
			child.QueueFree();
		_pitchAdvance = null;

		if (!atActions)
		{
			_pitchAdvance = new Button
			{
				Text = _lineSpeaking ? "Skip" : "Continue",
				CustomMinimumSize = new Vector2(160, 40)
			};
			_pitchAdvance.Pressed += () =>
			{
				SfxService.Click();
				if (_lineSpeaking)
				{
					TextVoiceService.Stop();
					_scope?.NotifySilence();
					_lineSpeaking = false;
					if (_pitchBody != null)
						_pitchBody.Text = _currentFullLine;
					if (_pitchAdvance != null)
						_pitchAdvance.Text = "Continue";
					return;
				}

				_lineIndex++;
				RefreshPitch();
			};
			_pitchButtons.AddChild(_pitchAdvance);
			return;
		}

		var canTrial = !status.Qualified && !status.Withdrawn && status.AttemptsRemaining > 0;
		if (canTrial)
		{
			var trial = new Button
			{
				Text = $"Run trial ({status.AttemptsRemaining} left)",
				CustomMinimumSize = new Vector2(220, 42)
			};
			trial.Pressed += () =>
			{
				SfxService.Confirm();
				if (_session?.BeginManufacturerTrial(id) == true)
					GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
				else if (_status != null)
					_status.Text = "Cannot launch trial — offer closed or no attempts left.";
			};
			_pitchButtons.AddChild(trial);
		}

		if (status.Qualified)
		{
			var sign = new Button
			{
				Text = company == null ? "Sign affiliation" : "Sign company charter",
				CustomMinimumSize = new Vector2(200, 42)
			};
			MechUiTheme.StylePrimaryButton(sign);
			sign.Pressed += () =>
			{
				SfxService.Confirm();
				if (_session?.SignManufacturer(id) == true)
					GetTree().ChangeSceneToFile("res://scenes/deep_space_departure.tscn");
			};
			_pitchButtons.AddChild(sign);
		}

		var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 42) };
		back.Pressed += () =>
		{
			SfxService.Click();
			TextVoiceService.Stop();
			_openManufacturerId = null;
			if (_pitchPanel != null)
				_pitchPanel.Visible = false;
			Build();
		};
		_pitchButtons.AddChild(back);
	}

	private string BuildStatusLine()
	{
		var sb = new StringBuilder();
		if (_run.SolarOnboarding)
		{
			sb.Append("Four independent companies are hiring one frontier corp. They do not manufacture MAP parts and have no affiliation with the Big Four. ");
			sb.Append("Compare their motives carefully: your employer's assets, mining rigs, and settlement become your responsibility. ");
		}
		else
		{
			sb.Append("No personal MAP. Shop every booth. Three demo attempts each — fail them all and that house withdraws. ");
			sb.Append("Optional: torch a rival's floor model; a scorned recruiter might forgive once. ");
		}
		if (_run.Convention.HasAnyQualified())
			sb.Append("You have at least one qualified offer — sign when ready.");
		else if (_run.Convention.AllWithdrawnWithNoQualify())
			sb.Append(_run.SolarOnboarding
				? "All evaluation offers were withdrawn — one last-chance charter remains."
				: "All offers withdrawn — Trinova salvage is the last door.");
		return sb.ToString();
	}

	private static string FormatCompanyLine(FrontierCompanyData company, string line)
	{
		var shortName = company.LiaisonName.Split(' ')[^1].ToUpperInvariant();
		return $"{shortName}  ·  {line}";
	}

	private static IEnumerable<string> FormatCompanyLines(
		FrontierCompanyData company,
		IEnumerable<string> lines)
	{
		foreach (var line in lines)
			yield return FormatCompanyLine(company, line);
	}
}
