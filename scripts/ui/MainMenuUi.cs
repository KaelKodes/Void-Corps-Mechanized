using Godot;

namespace Mechanize;

public partial class MainMenuUi : Control
{
	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session?.ReturnToCampaignMap == true && session.Campaign is { Alive: true })
		{
			session.ReturnToCampaignMap = false;
			GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
			return;
		}

		if (session?.OpenSkirmishSetupOnMenu == true)
		{
			session.OpenSkirmishSetupOnMenu = false;
			ShowSkirmishSetup();
			return;
		}

		BuildUi();
	}

	private void BuildUi()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		var dim = new ColorRect
		{
			Color = new Color(0.04f, 0.06f, 0.08f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var root = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 80;
		root.OffsetRight = -80;
		root.AddThemeConstantOverride("separation", 14);
		AddChild(root);

		var brand = new Label
		{
			Text = VoidCorpsIdentity.ProductTitle,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		brand.AddThemeFontSizeOverride("font_size", 42);
		root.AddChild(brand);

		var tag = new Label
		{
			Text = VoidCorpsIdentity.Tagline,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.7f, 0.78f, 0.84f)
		};
		tag.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(tag);

		root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var status = new Label
		{
			Text = $"Profile  ·  Scrap {session?.Profile.Scrap ?? 0}  ·  Parts {session?.Profile.OwnedCopyCount ?? 0}  ·  " +
			       $"Lives bank {session?.Profile.LivesBank ?? 2}  ·  Record {session?.Profile.SkirmishesWon ?? 0}/{session?.Profile.SkirmishesPlayed ?? 0}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.6f, 0.66f, 0.7f)
		};
		root.AddChild(status);

		root.AddChild(MakeButton("SKIRMISH", ShowSkirmishSetup));
		root.AddChild(MakeButton("CAMPAIGN", ShowCampaignEntry));

		root.AddChild(MakeButton("NEW PROFILE", () =>
		{
			session?.NewProfile();
			GetTree().ReloadCurrentScene();
		}));

		root.AddChild(MakeButton("QUIT", () => GetTree().Quit()));
	}

	private void ShowCampaignEntry()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		var dim = new ColorRect { Color = new Color(0.04f, 0.06f, 0.08f, 1f) };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var root = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 160;
		root.OffsetRight = -160;
		root.AddThemeConstantOverride("separation", 12);
		AddChild(root);

		var title = new Label
		{
			Text = "CAMPAIGN",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);

		var blurb = new Label
		{
			Text = "One-way sector path. Fight, refit, commit. Warning node is a named boss.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.7f, 0.76f, 0.8f)
		};
		root.AddChild(blurb);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session?.Campaign is { Alive: true })
		{
			root.AddChild(MakeButton("CONTINUE RUN", () =>
			{
				SfxService.Confirm();
				GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
			}));
		}

		root.AddChild(MakeButton("NEW RUN — Sector 1", () =>
		{
			SfxService.Confirm();
			session?.BeginCampaignRun(0);
			GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
		}));

		root.AddChild(MakeButton("Back", BuildUi));
	}

	private void ShowSkirmishSetup()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		var dim = new ColorRect { Color = new Color(0.04f, 0.06f, 0.08f, 1f) };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var root = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 120;
		root.OffsetRight = -120;
		root.AddThemeConstantOverride("separation", 12);
		AddChild(root);

		var title = new Label
		{
			Text = "SKIRMISH SETUP",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var claimIndex = 0;
		for (var i = 0; i < VoidCorpsIdentity.ClaimSites.Length; i++)
		{
			if (session != null && VoidCorpsIdentity.ClaimSites[i].Code == session.CurrentClaim.Code)
				claimIndex = i;
		}

		var difficulty = session?.PendingDifficulty ?? PilotDifficulty.Easy;
		var missionType = session?.PendingMission ?? MissionType.DestroyAllEnemies;
		if (missionType == MissionType.BossEncounter)
			missionType = MissionType.DestroyAllEnemies;

		var claimLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		claimLabel.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(claimLabel);

		void RefreshClaim()
		{
			var claim = VoidCorpsIdentity.ClaimSites[claimIndex];
			claimLabel.Text = $"{claim.Code}\n{claim.DisplayName}\n{claim.Brief}";
			session?.SetClaim(claim);
		}
		RefreshClaim();

		var claimRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		claimRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(claimRow);
		var prevClaim = new Button { Text = "< Claim" };
		prevClaim.Pressed += () =>
		{
			claimIndex = (claimIndex - 1 + VoidCorpsIdentity.ClaimSites.Length) % VoidCorpsIdentity.ClaimSites.Length;
			RefreshClaim();
		};
		claimRow.AddChild(prevClaim);
		var nextClaim = new Button { Text = "Claim >" };
		nextClaim.Pressed += () =>
		{
			claimIndex = (claimIndex + 1) % VoidCorpsIdentity.ClaimSites.Length;
			RefreshClaim();
		};
		claimRow.AddChild(nextClaim);

		var missionLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		missionLabel.AddThemeFontSizeOverride("font_size", 17);
		root.AddChild(missionLabel);

		void RefreshMission()
		{
			var info = MissionCatalog.Get(missionType);
			missionLabel.Text = $"MISSION: {info.Title}\n{info.Brief}";
			if (session != null)
				session.PendingMission = missionType;
		}
		RefreshMission();

		var missionRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		missionRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(missionRow);
		var prevMission = new Button { Text = "< Mission" };
		prevMission.Pressed += () =>
		{
			var values = (MissionType[])System.Enum.GetValues(typeof(MissionType));
			var idx = System.Array.IndexOf(values, missionType);
			do
			{
				idx = (idx - 1 + values.Length) % values.Length;
				missionType = values[idx];
			} while (missionType == MissionType.BossEncounter);
			RefreshMission();
		};
		missionRow.AddChild(prevMission);
		var nextMission = new Button { Text = "Mission >" };
		nextMission.Pressed += () =>
		{
			var values = (MissionType[])System.Enum.GetValues(typeof(MissionType));
			var idx = System.Array.IndexOf(values, missionType);
			do
			{
				idx = (idx + 1) % values.Length;
				missionType = values[idx];
			} while (missionType == MissionType.BossEncounter);
			RefreshMission();
		};
		missionRow.AddChild(nextMission);

		var diffLabel = new Label
		{
			Text = $"Difficulty: {difficulty}",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		diffLabel.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(diffLabel);

		var diffRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		diffRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(diffRow);
		foreach (PilotDifficulty d in System.Enum.GetValues(typeof(PilotDifficulty)))
		{
			var b = new Button { Text = d.ToString() };
			var captured = d;
			b.Pressed += () =>
			{
				difficulty = captured;
				diffLabel.Text = $"Difficulty: {difficulty}";
				if (session != null)
					session.PendingDifficulty = difficulty;
			};
			diffRow.AddChild(b);
		}

		root.AddChild(MakeButton("DEPLOY", () =>
		{
			if (session != null)
			{
				session.PendingDifficulty = difficulty;
				session.PendingMission = missionType;
				session.BeginSkirmish();
			}
			GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
		}));

		root.AddChild(MakeButton("Back", () => GetTree().ReloadCurrentScene()));
	}

	private static Button MakeButton(string text, System.Action onPress)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(360, 44),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		button.Pressed += () =>
		{
			SfxService.Click();
			onPress();
		};
		return button;
	}
}
