using Godot;

namespace Mechanize;

public partial class MainMenuUi : Control
{
	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		MusicService.Cue(MusicCue.Menu);
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
			ClearBootVeil();
			ShowSkirmishSetup();
			return;
		}

		if (session is { StudioIntroPlayed: false })
		{
			// Parent is still finishing _Ready children — defer the intro mount.
			CallDeferred(nameof(BeginStudioIntro));
			return;
		}

		ClearBootVeil();
		BuildUi();
	}

	private void BeginStudioIntro()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var parent = GetParent();
		if (session == null || parent == null)
		{
			ClearBootVeil();
			BuildUi();
			return;
		}

		if (session.StudioIntroPlayed)
		{
			ClearBootVeil();
			BuildUi();
			return;
		}

		session.StudioIntroPlayed = true;
		var intro = StudioIntroUi.Create(RevealMainMenu);
		parent.AddChild(intro);
		intro.MoveToFront();
		// Intro brings its own veil; boot placeholder can go.
		ClearBootVeil();
	}

	private void RevealMainMenu()
	{
		ClearBootVeil();
		BuildUi();
		Modulate = new Color(1f, 1f, 1f, 0f);
		var fade = CreateTween();
		fade.TweenProperty(this, "modulate:a", 1f, 1.35f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
	}

	private void ClearBootVeil()
	{
		GetParent()?.GetNodeOrNull<ColorRect>("BootVeil")?.QueueFree();
	}

	private void ClearUi()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
	}

	private void BuildUi()
	{
		ClearUi();
		MouseFilter = MouseFilterEnum.Ignore;

		var titleBlock = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		titleBlock.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		titleBlock.OffsetTop = 36;
		titleBlock.OffsetBottom = 140;
		titleBlock.AddThemeConstantOverride("separation", 6);
		AddChild(titleBlock);

		var brand = new Label
		{
			Text = VoidCorpsIdentity.ProductTitle,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		brand.AddThemeFontSizeOverride("font_size", 48);
		titleBlock.AddChild(brand);

		var tag = new Label
		{
			Text = VoidCorpsIdentity.Tagline,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted,
			MouseFilter = MouseFilterEnum.Ignore
		};
		tag.AddThemeFontSizeOverride("font_size", 15);
		titleBlock.AddChild(tag);

		var version = new Label
		{
			Text = $"v{VoidCorpsIdentity.GameVersion}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted.Darkened(0.15f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		version.AddThemeFontSizeOverride("font_size", 12);
		titleBlock.AddChild(version);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var bar = MechUiTheme.MakePanel("MenuBar", deep: true);
		bar.MouseFilter = MouseFilterEnum.Stop;
		bar.SetAnchorsPreset(LayoutPreset.BottomWide);
		bar.AnchorTop = 1f;
		bar.AnchorBottom = 1f;
		bar.OffsetLeft = 120;
		bar.OffsetRight = -120;
		bar.OffsetTop = -168;
		bar.OffsetBottom = -28;
		AddChild(bar);

		var barInner = new VBoxContainer();
		barInner.AddThemeConstantOverride("separation", 10);
		bar.AddChild(barInner);

		var status = new Label
		{
			Text = $"Profile  ·  Scrap {session?.Profile.Scrap ?? 0}  ·  Parts {session?.Profile.OwnedCopyCount ?? 0}  ·  " +
			       $"Lives bank {session?.Profile.LivesBank ?? 2}  ·  Record {session?.Profile.SkirmishesWon ?? 0}/{session?.Profile.SkirmishesPlayed ?? 0}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		status.AddThemeFontSizeOverride("font_size", 13);
		barInner.AddChild(status);

		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		row.AddThemeConstantOverride("separation", 12);
		barInner.AddChild(row);

		row.AddChild(MakeBarButton("SKIRMISH", ShowSkirmishSetup, primary: true));
		row.AddChild(MakeBarButton("CO-OP", ShowCoopLobby));
		row.AddChild(MakeBarButton("CAMPAIGN", ShowCampaignEntry));
		row.AddChild(MakeBarButton("NEW PROFILE", () =>
		{
			session?.NewProfile();
			GetTree().ReloadCurrentScene();
		}));
		row.AddChild(MakeBarButton("QUIT", () => GetTree().Quit()));
	}

	private Control MakeSubmenuShell(out VBoxContainer content)
	{
		ClearUi();
		MouseFilter = MouseFilterEnum.Stop;

		var dim = MechUiTheme.MakeDimOverlay();
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		center.OffsetLeft = 80;
		center.OffsetRight = -80;
		center.OffsetTop = 48;
		center.OffsetBottom = -48;
		AddChild(center);

		var panel = MechUiTheme.MakePanel("SubmenuPanel", minWidth: 720f, deep: true);
		panel.MouseFilter = MouseFilterEnum.Stop;
		center.AddChild(panel);

		content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		content.AddThemeConstantOverride("separation", 12);
		panel.AddChild(content);
		return panel;
	}

	private void ShowCampaignEntry()
	{
		MakeSubmenuShell(out var root);

		var title = new Label
		{
			Text = "CAMPAIGN",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);

		var blurb = new Label
		{
			Text =
				"Begin with MAP certification, or skip the tutorial and enter the Big Four convention directly.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		root.AddChild(blurb);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var profile = session?.Profile;
		var affiliation = string.IsNullOrEmpty(profile?.AffiliatedManufacturerId)
			? "Provisional / not chosen yet"
			: GameCatalog.GetManufacturer(profile!.AffiliatedManufacturerId).DisplayName;
		var dossier = new Label
		{
			Text =
				$"Merc corps  ·  {profile?.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename}\n" +
				$"Manufacturer affiliation  ·  {affiliation}\n" +
				$"{VoidCorpsIdentity.CampaignPremise}",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.62f, 0.69f, 0.74f)
		};
		dossier.AddThemeFontSizeOverride("font_size", 15);
		root.AddChild(dossier);

		var acts = new Label
		{
			Text =
				"Act 1  ·  MAP Cadet Program\n" +
				"Act 2  ·  Convention → Trials → Sign\n" +
				"Act 3  ·  Sector 1 → 2 → 3 (kit persists while you have lives)",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Text
		};
		acts.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(acts);

		if (session?.Campaign is { Alive: true })
		{
			root.AddChild(MakeButton("CONTINUE RUN", () =>
			{
				SfxService.Confirm();
				ContinueCampaignRun(session);
			}, primary: true));
		}

		root.AddChild(MakeButton("CADET PROGRAM — TUTORIAL", () =>
		{
			SfxService.Confirm();
			session?.BeginCadetProgram();
			GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
		}, primary: true));

		root.AddChild(MakeButton("CONVENTION — SKIP TUTORIAL", () =>
		{
			SfxService.Confirm();
			session?.BeginConventionProgram();
			GetTree().ChangeSceneToFile("res://scenes/convention_hall.tscn");
		}));

		root.AddChild(MakeButton("Back", BuildUi));
	}

	private void ContinueCampaignRun(GameSession session)
	{
		var run = session.Campaign;
		if (run == null || !run.Alive)
			return;

		if (run.Phase == CampaignPhase.CadetProgram)
		{
			if (run.AcademyStep == AcademyStep.Graduation)
			{
				session.OpenAcademyGraduation = true;
				GetTree().ChangeSceneToFile("res://scenes/academy_graduation.tscn");
				return;
			}

			session.ResumeCadetIfNeeded();
			if (session.OpenAcademyGraduation)
			{
				GetTree().ChangeSceneToFile("res://scenes/academy_graduation.tscn");
				return;
			}

			GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
			return;
		}

		if (run.Phase == CampaignPhase.ManufacturerConvention)
		{
			GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
			return;
		}

		GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
	}

	private void ShowSkirmishSetup()
	{
		MakeSubmenuShell(out var root);

		var title = new Label
		{
			Text = "SKIRMISH SETUP",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
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
		if (missionType is MissionType.BossEncounter or MissionType.CadetRange)
			missionType = MissionType.DestroyAllEnemies;

		var claimLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Text
		};
		claimLabel.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(claimLabel);

		void RefreshClaim()
		{
			var claim = VoidCorpsIdentity.ClaimSites[claimIndex];
			claimLabel.Text =
				$"[{ArenaSizeUtil.Label(claim.Size)}]  v{claim.MapVersion:0.0}\n" +
				$"{claim.Code}\n{claim.DisplayName}\n{claim.Brief}";
			session?.SetClaim(claim);
		}
		RefreshClaim();

		var claimRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		claimRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(claimRow);
		var prevClaim = MakeButton("< Claim", () =>
		{
			claimIndex = (claimIndex - 1 + VoidCorpsIdentity.ClaimSites.Length) % VoidCorpsIdentity.ClaimSites.Length;
			RefreshClaim();
		});
		claimRow.AddChild(prevClaim);
		var nextClaim = MakeButton("Claim >", () =>
		{
			claimIndex = (claimIndex + 1) % VoidCorpsIdentity.ClaimSites.Length;
			RefreshClaim();
		});
		claimRow.AddChild(nextClaim);

		var missionLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Text
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
		missionRow.AddChild(MakeButton("< Mission", () =>
		{
			var values = (MissionType[])System.Enum.GetValues(typeof(MissionType));
			var idx = System.Array.IndexOf(values, missionType);
			do
			{
				idx = (idx - 1 + values.Length) % values.Length;
				missionType = values[idx];
			} while (missionType is MissionType.BossEncounter or MissionType.CadetRange);
			RefreshMission();
		}));
		missionRow.AddChild(MakeButton("Mission >", () =>
		{
			var values = (MissionType[])System.Enum.GetValues(typeof(MissionType));
			var idx = System.Array.IndexOf(values, missionType);
			do
			{
				idx = (idx + 1) % values.Length;
				missionType = values[idx];
			} while (missionType is MissionType.BossEncounter or MissionType.CadetRange);
			RefreshMission();
		}));

		var diffLabel = new Label
		{
			Text = $"Difficulty: {difficulty}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		diffLabel.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(diffLabel);

		var diffRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		diffRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(diffRow);
		foreach (PilotDifficulty d in System.Enum.GetValues(typeof(PilotDifficulty)))
		{
			var captured = d;
			diffRow.AddChild(MakeButton(d.ToString(), () =>
			{
				difficulty = captured;
				diffLabel.Text = $"Difficulty: {difficulty}";
				if (session != null)
					session.PendingDifficulty = difficulty;
			}));
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
		}, primary: true));

		root.AddChild(MakeButton("Back", () => GetTree().ReloadCurrentScene()));
	}

	private void ShowCoopLobby()
	{
		MakeSubmenuShell(out var root);

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		net?.SetLocalDisplayName(session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename);

		var title = new Label
		{
			Text = "CO-OP DETACHMENT",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);

		var blurb = new Label
		{
			Text =
				"Listen-server wing. Host owns the match. Up to 4 MAP pilots, same team. " +
				"Guests connect by IP (port forward may be required).",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		root.AddChild(blurb);

		var status = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent
		};
		status.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(status);

		var roster = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		root.AddChild(roster);

		var addressEdit = new LineEdit
		{
			PlaceholderText = "Host IP (default 127.0.0.1)",
			Text = "127.0.0.1",
			CustomMinimumSize = new Vector2(360, 36),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		root.AddChild(addressEdit);

		var portEdit = new LineEdit
		{
			PlaceholderText = "Port",
			Text = NetSession.DefaultPort.ToString(),
			CustomMinimumSize = new Vector2(160, 36),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		root.AddChild(portEdit);

		void RefreshLobby()
		{
			status.Text = net?.StatusMessage ?? "Offline";
			if (net == null || !net.IsOnline)
			{
				roster.Text = "Not connected.";
				return;
			}

			var lines = new System.Text.StringBuilder();
			lines.AppendLine($"Peers {net.PeerCount}/{NetSession.MaxCoopPlayers}");
			foreach (var id in net.GetOrderedPeerIds())
			{
				var ready = net.IsPeerReady(id) ? "READY" : "staging";
				lines.AppendLine($"{net.PeerDisplayName(id)}  ·  peer {id}  ·  {ready}");
			}

			roster.Text = lines.ToString();
		}

		RefreshLobby();
		if (net != null)
			net.RosterChanged += RefreshLobby;

		root.AddChild(MakeButton("HOST WING", () =>
		{
			var port = portEdit.Text.ToInt();
			if (port <= 0)
				port = NetSession.DefaultPort;
			net?.Host(port);
			RefreshLobby();
		}, primary: true));

		root.AddChild(MakeButton("JOIN WING", () =>
		{
			var port = portEdit.Text.ToInt();
			if (port <= 0)
				port = NetSession.DefaultPort;
			net?.Join(addressEdit.Text, port);
			RefreshLobby();
		}, primary: true));

		root.AddChild(MakeButton("TOGGLE READY", () =>
		{
			if (net is not { IsOnline: true })
				return;
			net.SetLocalReady(!net.IsPeerReady(net.LocalPeerId));
			RefreshLobby();
		}));

		root.AddChild(MakeButton("START CO-OP SKIRMISH (HOST)", () =>
		{
			if (net is not { Mode: NetSession.NetMode.Hosting })
			{
				status.Text = "Only the host can start.";
				return;
			}

			net.Intent = NetSession.LobbyIntent.CoopSkirmish;
			if (session != null)
			{
				session.CoopMatch = true;
				session.PendingBossEncounter = BossEncounterId.None;
			}

			SfxService.Confirm();
			net.HostLaunchMatch(session?.BuildLaunchPayload(false) ?? new Godot.Collections.Dictionary());
		}, primary: true));

		root.AddChild(MakeButton("START CO-OP CAMPAIGN (HOST)", () =>
		{
			if (net is not { Mode: NetSession.NetMode.Hosting })
			{
				status.Text = "Only the host can start.";
				return;
			}

			net.Intent = NetSession.LobbyIntent.CoopCampaign;
			session?.BeginCampaignRun(0);
			if (session != null)
				session.CoopMatch = true;
			SfxService.Confirm();
			var payload = session?.BuildLaunchPayload(false, "campaign")
			              ?? new Godot.Collections.Dictionary { ["scene"] = "campaign" };
			net.HostLaunchMatch(payload);
		}));

		root.AddChild(MakeButton("DISCONNECT", () =>
		{
			net?.DisconnectSession();
			RefreshLobby();
		}));

		root.AddChild(MakeButton("Back", () =>
		{
			if (net != null)
				net.RosterChanged -= RefreshLobby;
			BuildUi();
		}));
	}

	private static Button MakeBarButton(string text, System.Action onPress, bool primary = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(148, 44),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		button.AddThemeFontSizeOverride("font_size", 16);
		if (primary)
			MechUiTheme.StylePrimaryButton(button);
		else
			MechUiTheme.StyleGhostButton(button);
		button.Pressed += () =>
		{
			SfxService.Click();
			onPress();
		};
		return button;
	}

	private static Button MakeButton(string text, System.Action onPress, bool primary = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(360, 44),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		if (primary)
			MechUiTheme.StylePrimaryButton(button);
		else
			MechUiTheme.StyleGhostButton(button);
		button.Pressed += () =>
		{
			SfxService.Click();
			onPress();
		};
		return button;
	}
}
