using System.Collections.Generic;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>Big Four convention floor — banners, recruiter pitches, trials, signing.</summary>
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
	private PanelContainer? _pityPanel;

	private string? _openManufacturerId;
	/// <summary>Index into display lines (forgiveness prepended as line 0 when granted).</summary>
	private int _lineIndex;
	private readonly List<string> _lines = new();

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Campaign);
		ConventionCatalog.EnsureBuilt();
		_session = GetNodeOrNull<GameSession>("/root/GameSession");
		_run = _session?.Campaign ?? CampaignRun.Load() ?? CampaignRun.StartCadet();
		if (_run.Phase != CampaignPhase.ManufacturerConvention)
			_run.EnterConventionGate();
		_run.Convention.EnsureAllManufacturers();
		if (_session != null)
		{
			_session.Campaign = _run;
			_session.ReturnToConventionHall = false;
			_session.ClearConventionDemoLoaner();
		}

		Build();
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_pitchPanel = null;
		_pityPanel = null;

		var dim = new ColorRect
		{
			Color = new Color(0.04f, 0.05f, 0.07f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var title = new Label
		{
			Text = "BIG FOUR CONVENTION",
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

		foreach (var id in HouseOrder)
			row.AddChild(MakeBanner(id));

		BuildPitchPanel();
		BuildPityPanel();

		var back = new Button
		{
			Text = "Sector Map",
			Position = new Vector2(1700, 28),
			CustomMinimumSize = new Vector2(160, 40)
		};
		back.Pressed += () =>
		{
			_run.Save();
			GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
		};
		AddChild(back);

		if (_run.Convention.AllWithdrawnWithNoQualify())
			ShowPity();
	}

	private Control MakeBanner(string manufacturerId)
	{
		var mfg = GameCatalog.GetManufacturer(manufacturerId);
		var def = ConventionCatalog.Get(manufacturerId);
		var status = _run.Convention.Get(manufacturerId);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420, 380),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.09f, 0.11f, 0.96f),
			BorderColor = mfg.AccentColor,
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

		var name = new Label
		{
			Text = mfg.DisplayName.ToUpperInvariant(),
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = mfg.AccentColor
		};
		name.AddThemeFontSizeOverride("font_size", 22);
		col.AddChild(name);

		var focus = new Label
		{
			Text = mfg.Niche,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.7f, 0.75f, 0.8f)
		};
		focus.AddThemeFontSizeOverride("font_size", 12);
		col.AddChild(focus);

		var snippet = new Label
		{
			Text = def.BannerSnippet,
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
			Position = new Vector2(280, 540),
			CustomMinimumSize = new Vector2(1360, 340)
		};
		_pitchPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.1f, 0.98f),
			BorderColor = new Color(0.62f, 0.5f, 0.28f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 18,
			ContentMarginTop = 14,
			ContentMarginRight = 18,
			ContentMarginBottom = 14,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		});
		AddChild(_pitchPanel);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 10);
		_pitchPanel.AddChild(root);

		_pitchTitle = new Label { Modulate = new Color(0.85f, 0.7f, 0.38f) };
		_pitchTitle.AddThemeFontSizeOverride("font_size", 20);
		root.AddChild(_pitchTitle);

		_pitchBody = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0, 150),
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
		_pityPanel = new PanelContainer
		{
			Visible = false,
			Position = new Vector2(360, 200),
			CustomMinimumSize = new Vector2(1200, 280)
		};
		_pityPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.1f, 0.09f, 0.98f),
			BorderColor = GameCatalog.GetManufacturer("trinova").AccentColor,
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

		var t = new Label
		{
			Text = "TRINOVA SALVAGE CONTRACT",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = GameCatalog.GetManufacturer("trinova").AccentColor
		};
		t.AddThemeFontSizeOverride("font_size", 22);
		col.AddChild(t);

		var body = new Label
		{
			Text =
				"Every floor withdrew. Trinova's logistics desk still has a scrap-heap MAP and a pity affiliation.\n" +
				"Take it, or your wings stay grounded.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		body.AddThemeFontSizeOverride("font_size", 15);
		col.AddChild(body);

		var take = new Button
		{
			Text = "Accept salvage affiliation (Trinova)",
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

		var rival = _run.Convention.TryGrantForgiveness(manufacturerId);
		if (rival != null)
		{
			_run.Save();
			var rivalName = GameCatalog.GetManufacturer(rival).DisplayName;
			_lines.Add($"RECRUITER  ·  Heard you slagged {rivalName}'s floor model. Ha. One more demo. Don't waste it.");
		}

		var def = ConventionCatalog.Get(manufacturerId);
		var status = _run.Convention.Get(manufacturerId);
		if (status.Qualified)
			_lines.AddRange(def.QualifiedReturnLines);
		else if (status.Withdrawn)
			_lines.AddRange(def.WithdrawnReturnLines);
		else if (status.AttemptsRemaining < ConventionState.MaxAttempts)
			_lines.AddRange(def.FailedReturnLines);
		else
			_lines.AddRange(def.PitchLines);

		if (_pitchPanel != null)
			_pitchPanel.Visible = true;
		RefreshPitch();
	}

	private void RefreshPitch()
	{
		if (_openManufacturerId == null || _pitchTitle == null || _pitchBody == null || _pitchButtons == null)
			return;

		var id = _openManufacturerId;
		var mfg = GameCatalog.GetManufacturer(id);
		var def = ConventionCatalog.Get(id);
		var status = _run.Convention.Get(id);

		_pitchTitle.Text = $"{mfg.DisplayName}  ·  Recruiter booth";
		_pitchTitle.Modulate = mfg.AccentColor;

		var atActions = _lineIndex >= _lines.Count;
		if (!atActions)
			_pitchBody.Text = _lines[_lineIndex];
		else
		{
			_pitchBody.Text =
				$"{_lines[^1]}\n\n" +
				$"Trial: {MissionCatalog.Get(def.TrialMission).Title}  ·  {StatusText(status)}\n" +
				$"Signing package: {def.SigningBonusBlurb}";
		}

		foreach (var child in _pitchButtons.GetChildren())
			child.QueueFree();

		if (!atActions)
		{
			var next = new Button { Text = "Continue", CustomMinimumSize = new Vector2(160, 40) };
			next.Pressed += () =>
			{
				SfxService.Click();
				_lineIndex++;
				RefreshPitch();
			};
			_pitchButtons.AddChild(next);
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
				Text = "Sign affiliation",
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
		sb.Append("No personal MAP. Shop every booth. Three demo attempts each — fail them all and that house withdraws. ");
		sb.Append("Optional: torch a rival's floor model; a scorned recruiter might forgive once. ");
		if (_run.Convention.HasAnyQualified())
			sb.Append("You have at least one Qualified offer — sign when ready.");
		else if (_run.Convention.AllWithdrawnWithNoQualify())
			sb.Append("All offers withdrawn — Trinova salvage is the last door.");
		return sb.ToString();
	}
}
