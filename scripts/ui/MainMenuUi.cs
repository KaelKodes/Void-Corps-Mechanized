using System.Linq;
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
		if (session?.ReturnToSolarMap == true)
		{
			session.ActivateMainProfile();
			session.ReturnToSolarMap = false;
			GetTree().ChangeSceneToFile("res://scenes/solar_system_map.tscn");
			return;
		}
		if (session?.ReturnToCampaignMap == true && session.Campaign is { Alive: true })
		{
			session.ReturnToCampaignMap = false;
			GetTree().ChangeSceneToFile("res://scenes/campaign_map.tscn");
			return;
		}

		session?.ActivateCampaignProfile();

		if (session?.OpenSkirmishSetupOnMenu == true)
		{
			if (session.HasAnyProfile && session.Profile.HasFaction)
			{
				session.OpenSkirmishSetupOnMenu = false;
				ClearBootVeil();
				ShowSkirmishSetup();
				return;
			}

			session.OpenSkirmishSetupOnMenu = false;
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
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.ActivateCampaignProfile();

		if (session == null || !session.HasAnyProfile)
		{
			OpenProfileCreate(firstEmptySlot: 0);
			return;
		}

		if (!session.Profile.HasFaction)
		{
			session.PendingFactionContinue = PendingFactionContinue.NewProfile;
			session.PendingCreateSlot = -1;
			GetTree().ChangeSceneToFile("res://scenes/faction_pick.tscn");
			return;
		}

		ShowProfileHub();
	}

	private void OpenProfileCreate(int firstEmptySlot)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return;
		session.PendingCreateSlot = firstEmptySlot;
		session.PendingFactionContinue = PendingFactionContinue.NewProfile;
		GetTree().ChangeSceneToFile("res://scenes/faction_pick.tscn");
	}

	private void AddBrandHeader()
	{
		var titleBlock = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		titleBlock.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		titleBlock.OffsetTop = 28;
		titleBlock.OffsetBottom = 150;
		titleBlock.AddThemeConstantOverride("separation", 2);
		AddChild(titleBlock);

		var universe = new Label
		{
			Text = "CATS  ·  DOGS",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent.Darkened(0.08f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		universe.AddThemeFontSizeOverride("font_size", 22);
		universe.AddThemeConstantOverride("outline_size", 2);
		universe.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.65f));
		titleBlock.AddChild(universe);

		var brand = new Label
		{
			Text = VoidCorpsIdentity.ShortTitle,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.AccentHot,
			MouseFilter = MouseFilterEnum.Ignore
		};
		brand.AddThemeFontSizeOverride("font_size", 56);
		brand.AddThemeConstantOverride("outline_size", 4);
		brand.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.75f));
		brand.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.55f));
		brand.AddThemeConstantOverride("shadow_offset_x", 2);
		brand.AddThemeConstantOverride("shadow_offset_y", 3);
		titleBlock.AddChild(brand);

		var version = new Label
		{
			Text = $"v{VoidCorpsIdentity.GameVersion}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted.Darkened(0.2f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		version.AddThemeFontSizeOverride("font_size", 13);
		titleBlock.AddChild(version);
	}

	private void ShowProfileHub()
	{
		ClearUi();
		MouseFilter = MouseFilterEnum.Ignore;
		AddBrandHeader();

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.ActivateCampaignProfile();

		var bar = MechUiTheme.MakePanel("ProfileHub", deep: true);
		bar.MouseFilter = MouseFilterEnum.Stop;
		bar.SetAnchorsPreset(LayoutPreset.BottomWide);
		bar.AnchorTop = 1f;
		bar.AnchorBottom = 1f;
		bar.OffsetLeft = 100;
		bar.OffsetRight = -100;
		bar.OffsetTop = -280;
		bar.OffsetBottom = -28;
		AddChild(bar);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 12);
		bar.AddChild(inner);

		var identityRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		identityRow.AddThemeConstantOverride("separation", 16);
		inner.AddChild(identityRow);

		var portrait = PilotPortraits.GetPortrait(
			session?.Profile.Faction ?? FactionId.None,
			session?.Profile.PilotPortraitIndex ?? 0);
		if (portrait != null)
		{
			var frame = new TextureRect
			{
				Texture = portrait,
				CustomMinimumSize = new Vector2(96, 96),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
			};
			identityRow.AddChild(frame);
		}

		var idCol = new VBoxContainer();
		idCol.AddThemeConstantOverride("separation", 4);
		identityRow.AddChild(idCol);

		var handle = session?.Profile.ResolveAccountHandle() ?? "Pilot";
		var faction = session != null && session.Profile.HasFaction
			? PilotPortraits.DisplayName(session.Profile.Faction)
			: "Unset";
		var nameLabel = new Label
		{
			Text = handle,
			Modulate = MechUiTheme.AccentHot
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 28);
		idCol.AddChild(nameLabel);
		idCol.AddChild(new Label
		{
			Text = $"{faction} pilot  ·  Slot {(session?.ActiveSlotIndex ?? 0) + 1}",
			Modulate = MechUiTheme.Muted
		});

		var skirmish = session != null
			? SaveService.LoadSkirmishProfile(session.ActiveSlotIndex)
			: null;
		var campaignScrap = session?.Profile.Scrap ?? 0;
		var status = new Label
		{
			Text =
				$"Campaign scrap {campaignScrap}  ·  Skirmish scrap {skirmish?.Scrap ?? 0}  ·  " +
				$"Skirmish record {skirmish?.SkirmishesWon ?? 0}/{skirmish?.SkirmishesPlayed ?? 0}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		status.AddThemeFontSizeOverride("font_size", 13);
		inner.AddChild(status);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		inner.AddChild(row);
		row.AddChild(MakeBarButton("ENTER MODES", ShowModeHub, primary: true));
		row.AddChild(MakeBarButton("MANAGE SLOTS", ShowProfileSlots));
		row.AddChild(MakeBarButton("QUIT", () => GetTree().Quit()));
	}

	private void ShowModeHub()
	{
		ClearUi();
		MouseFilter = MouseFilterEnum.Ignore;
		AddBrandHeader();

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.ActivateCampaignProfile();

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

		var handle = session?.Profile.ResolveAccountHandle() ?? "Pilot";
		var faction = session != null && session.Profile.HasFaction
			? PilotPortraits.DisplayName(session.Profile.Faction)
			: "?";
		var skirmish = session != null
			? SaveService.LoadSkirmishProfile(session.ActiveSlotIndex)
			: null;
		var status = new Label
		{
			Text =
				$"{handle} ({faction})  ·  Campaign scrap {session?.Profile.Scrap ?? 0}  ·  " +
				$"Skirmish scrap {skirmish?.Scrap ?? 0}  ·  Record {skirmish?.SkirmishesWon ?? 0}/{skirmish?.SkirmishesPlayed ?? 0}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		status.AddThemeFontSizeOverride("font_size", 13);
		barInner.AddChild(status);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		barInner.AddChild(row);

		row.AddChild(MakeBarButton("SKIRMISH", ShowSkirmishSetup, primary: true));
		row.AddChild(MakeBarButton("MULTIPLAYER", ShowMultiplayerLobby));
		row.AddChild(MakeBarButton("CAMPAIGN", ShowSolarCampaignEntry));
		row.AddChild(MakeBarButton("ROGUELIKE", ShowRoguelikeEntry));
		row.AddChild(MakeBarButton("PROFILES", ShowProfileHub));
		row.AddChild(MakeBarButton("QUIT", () => GetTree().Quit()));
	}

	private void ShowProfileSlots()
	{
		MakeSubmenuShell(out var root);
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var selected = session?.ActiveSlotIndex ?? 0;
		var copyMode = false;
		var confirmDelete = false;

		var title = new Label
		{
			Text = "Pilot Profiles",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.AccentHot
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		root.AddChild(title);

		var blurb = new Label
		{
			Text = "Four local pilots. Callsign is your LAN multiplayer handle. Each profile holds Campaign, Rogue-Like, and Skirmish progress.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		blurb.AddThemeFontSizeOverride("font_size", 13);
		root.AddChild(blurb);

		var status = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		status.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(status);

		var slotRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		slotRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(slotRow);

		var summary = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Text
		};
		summary.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(summary);

		var handleEdit = new LineEdit
		{
			PlaceholderText = "Account handle (1–24 characters)",
			CustomMinimumSize = new Vector2(360, 36),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MaxLength = SaveService.MaxHandleLength
		};
		root.AddChild(handleEdit);

		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		actions.AddThemeConstantOverride("separation", 8);
		root.AddChild(actions);

		void Refresh()
		{
			foreach (var child in slotRow.GetChildren())
				child.QueueFree();

			var manifest = SaveService.Manifest;
			for (var i = 0; i < SaveService.MaxSlots; i++)
			{
				var idx = i;
				var occupied = SaveService.SlotOccupied(i);
				var handle = occupied
					? (string.IsNullOrEmpty(manifest.Slots[i].AccountHandle)
						? $"Pilot {i + 1}"
						: manifest.Slots[i].AccountHandle)
					: "Empty";
				var label = occupied
					? $"Slot {i + 1}\n{handle}"
					: $"Slot {i + 1}\n— empty —";
				var active = i == (session?.ActiveSlotIndex ?? -1);
				var btn = MakeBarButton(label, () =>
				{
					if (copyMode)
					{
						if (occupied)
						{
							status.Text = "Copy needs an empty destination slot.";
							return;
						}

						var from = selected;
						if (session?.CopySlot(from, idx) == true)
						{
							SfxService.Confirm();
							copyMode = false;
							selected = idx;
							status.Text = $"Copied slot {from + 1} → {idx + 1}.";
							Refresh();
						}
						else
							status.Text = "Copy failed.";
						return;
					}

					selected = idx;
					confirmDelete = false;
					Refresh();
				}, primary: selected == i);
				btn.CustomMinimumSize = new Vector2(150, 72);
				if (active)
					btn.Modulate = MechUiTheme.AccentHot;
				slotRow.AddChild(btn);
			}

			var occ = SaveService.SlotOccupied(selected);
			var slotInfo = SaveService.Manifest.Slots[selected];
			if (occ)
			{
				var faction = slotInfo.Faction is (int)FactionId.Cat or (int)FactionId.Dog
					? PilotPortraits.DisplayName((FactionId)slotInfo.Faction)
					: "Unset";
				var scrapHint = selected == session?.ActiveSlotIndex
					? $"Campaign scrap {session?.Profile.Scrap ?? 0}  ·  {faction}"
					: $"{faction} pilot";
				var last = "";
				if (slotInfo.LastPlayedUnix > 0)
				{
					var dt = System.DateTimeOffset.FromUnixTimeSeconds(slotInfo.LastPlayedUnix).LocalDateTime;
					last = $"  ·  Last played {dt:g}";
				}
				summary.Text =
					$"Selected Slot {selected + 1}: {slotInfo.AccountHandle}\n{scrapHint}{last}" +
					(selected == session?.ActiveSlotIndex ? "\n(currently active)" : "");
				handleEdit.Text = slotInfo.AccountHandle;
				handleEdit.Editable = true;
			}
			else
			{
				summary.Text = $"Selected Slot {selected + 1}: empty — press New to create a pilot.";
				if (string.IsNullOrWhiteSpace(handleEdit.Text))
					handleEdit.Text = "";
				handleEdit.Editable = true;
			}

			if (copyMode)
				status.Text = "Copy mode: click an empty slot as destination.";
			else if (confirmDelete)
				status.Text = "Press Delete again to confirm wipe of this slot.";
			else if (string.IsNullOrEmpty(status.Text) || status.Text.StartsWith("Copy mode") || status.Text.StartsWith("Press Delete"))
				status.Text = SaveService.SlotOccupied(session?.ActiveSlotIndex ?? 0)
					? $"Active: {session?.Profile.ResolveAccountHandle()}"
					: "Pick a slot.";
		}

		actions.AddChild(MakeBarButton("Select", () =>
		{
			if (session == null)
				return;
			if (!SaveService.SlotOccupied(selected))
			{
				status.Text = "Slot is empty — use New first.";
				return;
			}

			if (selected == session.ActiveSlotIndex)
			{
				SfxService.Click();
				ShowProfileHub();
				return;
			}

			if (session.SwitchToSlot(selected))
			{
				SfxService.Confirm();
				GetTree().ReloadCurrentScene();
			}
			else
				status.Text = "Could not switch profile.";
		}, primary: true));

		actions.AddChild(MakeBarButton("New", () =>
		{
			if (session == null)
				return;
			if (SaveService.SlotOccupied(selected))
			{
				status.Text = "Slot occupied — delete it or pick an empty slot.";
				return;
			}

			SfxService.Confirm();
			OpenProfileCreate(selected);
		}));

		actions.AddChild(MakeBarButton("Rename", () =>
		{
			if (session == null)
				return;
			if (!SaveService.SlotOccupied(selected))
			{
				status.Text = "Nothing to rename.";
				return;
			}

			var handle = SaveService.SanitizeHandle(handleEdit.Text);
			if (!SaveService.IsValidHandle(handle))
			{
				status.Text = "Enter a valid account handle (1–24 characters).";
				return;
			}

			if (session.RenameSlot(selected, handle))
			{
				SfxService.Confirm();
				status.Text = $"Renamed to {handle}.";
				confirmDelete = false;
				copyMode = false;
				Refresh();
				if (selected == session.ActiveSlotIndex)
				{
					// Refresh main identity without full reload if staying here.
				}
			}
			else
				status.Text = "Rename failed.";
		}));

		actions.AddChild(MakeBarButton("Copy To…", () =>
		{
			if (!SaveService.SlotOccupied(selected))
			{
				status.Text = "Select an occupied slot to copy.";
				return;
			}

			var hasEmpty = false;
			for (var i = 0; i < SaveService.MaxSlots; i++)
			{
				if (!SaveService.SlotOccupied(i))
				{
					hasEmpty = true;
					break;
				}
			}

			if (!hasEmpty)
			{
				status.Text = "No empty slot — delete one first.";
				return;
			}

			copyMode = true;
			confirmDelete = false;
			Refresh();
		}));

		actions.AddChild(MakeBarButton("Delete", () =>
		{
			if (session == null)
				return;
			if (!SaveService.SlotOccupied(selected))
			{
				status.Text = "Slot already empty.";
				return;
			}

			if (!confirmDelete)
			{
				confirmDelete = true;
				copyMode = false;
				Refresh();
				return;
			}

			if (session.DeleteSlot(selected))
			{
				SfxService.Confirm();
				GetTree().ReloadCurrentScene();
			}
			else
				status.Text = "Delete failed.";
		}));

		root.AddChild(MakeButton("Back", () =>
		{
			copyMode = false;
			confirmDelete = false;
			ShowProfileHub();
		}));

		Refresh();
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

	private void ShowSolarCampaignEntry()
	{
		MakeSubmenuShell(out var root);
		var title = new Label
		{
			Text = "SYSTEM CLAIM CAMPAIGN",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);
		root.AddChild(new Label
		{
			Text =
				$"{VoidCorpsIdentity.CampaignPremise}\n\n" +
				"Unlock permanent locations, revisit operations for known drops, license manufacturer technology with scrap, and fabricate gear from salvaged materials. Independent merchants sell mixed stock at marked bazaars.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		});

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		root.AddChild(MakeButton("CONTINUE", () =>
		{
			session?.BeginSolarCampaign();
			if (session?.InSolarOnboarding == true)
			{
				session.LaunchSolarOnboarding();
				var destination = session.Campaign?.Phase == CampaignPhase.ManufacturerConvention
					? "res://scenes/convention_hall.tscn"
					: session.OpenAcademyGraduation
						? "res://scenes/academy_graduation.tscn"
						: "res://scenes/arena.tscn";
				GetTree().ChangeSceneToFile(destination);
			}
			else
				GetTree().ChangeSceneToFile("res://scenes/solar_system_map.tscn");
		}, primary: true));
		root.AddChild(MakeButton("NEW", ShowNewSolarCampaignOptions));
		root.AddChild(MakeButton("Back", ShowModeHub));
	}

	private void ShowNewSolarCampaignOptions()
	{
		MakeSubmenuShell(out var root);
		var title = new Label
		{
			Text = "NEW CAMPAIGN",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);
		root.AddChild(new Label
		{
			Text =
				"Start a fresh system claim.\n\n" +
				"Take the cadet tutorial first, or skip straight to the job convention and sign with an employer.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		});

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		root.AddChild(MakeButton("TUTORIAL", () =>
		{
			SfxService.Confirm();
			BeginWithFactionGate(PendingFactionContinue.SolarTutorial, () =>
			{
				session?.BeginSolarCampaign(reset: true);
				session?.LaunchSolarOnboarding();
				GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
			});
		}, primary: true));
		root.AddChild(MakeButton("SKIP TO CONVENTION", () =>
		{
			SfxService.Confirm();
			BeginWithFactionGate(PendingFactionContinue.SolarSkipConvention, () =>
			{
				session?.BeginSolarCampaignSkipToConvention();
				GetTree().ChangeSceneToFile("res://scenes/convention_hall.tscn");
			});
		}));
		root.AddChild(MakeButton("Back", ShowSolarCampaignEntry));
	}

	private void ShowRoguelikeEntry()
	{
		MakeSubmenuShell(out var root);

		var title = new Label
		{
			Text = "ROGUELIKE",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);

		var blurb = new Label
		{
			Text =
				"The original linear run: certify, choose a path, and survive three claim sectors on limited lives.",
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
		var factionLine = profile is { HasFaction: true }
			? $"{PilotPortraits.DisplayName(profile.Faction)}  ·  pilot #{profile.PilotPortraitIndex + 1}"
			: "Unset — choose before starting a run";
		var dossier = new Label
		{
			Text =
				$"Handle  ·  {profile?.ResolveAccountHandle() ?? VoidCorpsIdentity.PlayerCorpCodename}\n" +
				$"Faction  ·  {factionLine}\n" +
				$"Manufacturer license (run)  ·  {affiliation}\n" +
				$"{VoidCorpsIdentity.PlayerCorpBlurb}",
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

		var savedRoguelikeRun = CampaignRun.Load();
		if (session != null && savedRoguelikeRun is { Alive: true })
		{
			root.AddChild(MakeButton("CONTINUE RUN", () =>
			{
				SfxService.Confirm();
				session.Campaign = savedRoguelikeRun;
				ContinueCampaignRun(session);
			}, primary: true));
		}

		root.AddChild(MakeButton("CADET PROGRAM — TUTORIAL", () =>
		{
			SfxService.Confirm();
			BeginWithFactionGate(PendingFactionContinue.RoguelikeCadet, () =>
			{
				session?.BeginCadetProgram();
				GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
			});
		}, primary: true));

		root.AddChild(MakeButton("CONVENTION — SKIP TUTORIAL", () =>
		{
			SfxService.Confirm();
			BeginWithFactionGate(PendingFactionContinue.RoguelikeConvention, () =>
			{
				session?.BeginConventionProgram();
				GetTree().ChangeSceneToFile("res://scenes/convention_hall.tscn");
			});
		}));

		root.AddChild(MakeButton("Back", ShowModeHub));
	}

	private void BeginWithFactionGate(PendingFactionContinue pending, System.Action whenReady)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			return;
		if (session.TryBeginWithFactionGate(pending, whenReady))
			GetTree().ChangeSceneToFile("res://scenes/faction_pick.tscn");
	}

	private void ContinueCampaignRun(GameSession session)
	{
		var run = session.Campaign is { SolarOnboarding: false }
			? session.Campaign
			: CampaignRun.Load();
		if (run == null || !run.Alive)
			return;
		session.Campaign = run;
		session.ActivateRoguelikeProfile();

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
		var skirmishClaims = VoidCorpsIdentity.StandardClaimSites.ToArray();
		var claimIndex = 0;
		for (var i = 0; i < skirmishClaims.Length; i++)
		{
			if (session != null && skirmishClaims[i].Code == session.CurrentClaim.Code)
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

		void SyncClaimToMission()
		{
			if (missionType == MissionType.Sabotage)
			{
				var sab = VoidCorpsIdentity.FindClaim(SabotageMission.ClaimCode);
				if (sab.HasValue)
					session?.SetClaim(sab.Value);
				return;
			}

			if (session != null
				&& (session.CurrentClaim.SabotageOnly
					|| session.CurrentClaim.Code == SabotageMission.ClaimCode)
				&& skirmishClaims.Length > 0)
			{
				claimIndex = 0;
				session.SetClaim(skirmishClaims[claimIndex]);
			}
		}

		void RefreshClaim()
		{
			if (missionType == MissionType.Sabotage)
			{
				var sab = VoidCorpsIdentity.FindClaim(SabotageMission.ClaimCode);
				if (sab.HasValue)
				{
					claimLabel.Text =
						$"[SABOTAGE ONLY]\n{sab.Value.Code}\n{sab.Value.DisplayName}\n{sab.Value.Brief}";
					session?.SetClaim(sab.Value);
					return;
				}
			}

			if (skirmishClaims.Length == 0)
				return;
			claimIndex = (claimIndex % skirmishClaims.Length + skirmishClaims.Length) % skirmishClaims.Length;
			var claim = skirmishClaims[claimIndex];
			claimLabel.Text =
				$"[{ArenaSizeUtil.Label(claim.Size)}]  v{claim.MapVersion:0.0}\n" +
				$"{claim.Code}\n{claim.DisplayName}\n{claim.Brief}";
			session?.SetClaim(claim);
		}

		SyncClaimToMission();
		RefreshClaim();

		var claimRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		claimRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(claimRow);
		var prevClaim = MakeButton("< Claim", () =>
		{
			if (missionType == MissionType.Sabotage || skirmishClaims.Length == 0)
				return;
			claimIndex = (claimIndex - 1 + skirmishClaims.Length) % skirmishClaims.Length;
			RefreshClaim();
		});
		claimRow.AddChild(prevClaim);
		var nextClaim = MakeButton("Claim >", () =>
		{
			if (missionType == MissionType.Sabotage || skirmishClaims.Length == 0)
				return;
			claimIndex = (claimIndex + 1) % skirmishClaims.Length;
			RefreshClaim();
		});
		claimRow.AddChild(nextClaim);

		// Day / Night — dual military clocks (12h apart) + toggle; preference persisted.
		var arenaPeriod = session?.Profile.PreferredSkirmishArenaPeriod ?? ArenaPeriod.Night;
		if (session != null)
			session.PendingArenaPeriod = arenaPeriod;

		var periodRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		periodRow.AddThemeConstantOverride("separation", 16);
		root.AddChild(periodRow);

		var dayClockLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		dayClockLabel.AddThemeFontSizeOverride("font_size", 22);
		periodRow.AddChild(dayClockLabel);

		var nightClockLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		nightClockLabel.AddThemeFontSizeOverride("font_size", 22);
		periodRow.AddChild(nightClockLabel);

		var toggleRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		toggleRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(toggleRow);

		Button? dayBtn = null;
		Button? nightBtn = null;

		void RefreshPeriodClocks()
		{
			dayClockLabel.Text = $"{FrontierClock.FormatMilitary(ArenaPeriod.Day)}  DAY";
			nightClockLabel.Text = $"{FrontierClock.FormatMilitary(ArenaPeriod.Night)}  NIGHT";
			dayClockLabel.Modulate = arenaPeriod == ArenaPeriod.Day ? MechUiTheme.AccentHot : MechUiTheme.Muted;
			nightClockLabel.Modulate = arenaPeriod == ArenaPeriod.Night ? MechUiTheme.AccentHot : MechUiTheme.Muted;
			if (dayBtn != null)
				dayBtn.Modulate = arenaPeriod == ArenaPeriod.Day ? MechUiTheme.AccentHot : MechUiTheme.Text;
			if (nightBtn != null)
				nightBtn.Modulate = arenaPeriod == ArenaPeriod.Night ? MechUiTheme.AccentHot : MechUiTheme.Text;
		}

		void SetArenaPeriod(ArenaPeriod period)
		{
			arenaPeriod = period;
			if (session != null)
			{
				session.PendingArenaPeriod = period;
				session.Profile.PreferredSkirmishArenaPeriod = period;
				session.SaveProfile();
			}

			RefreshPeriodClocks();
		}

		dayBtn = MakeButton("DAY", () => SetArenaPeriod(ArenaPeriod.Day));
		nightBtn = MakeButton("NIGHT", () => SetArenaPeriod(ArenaPeriod.Night));
		toggleRow.AddChild(dayBtn);
		toggleRow.AddChild(nightBtn);
		RefreshPeriodClocks();

		var clockTimer = new Godot.Timer
		{
			WaitTime = 1.0,
			Autostart = true
		};
		clockTimer.Timeout += RefreshPeriodClocks;
		root.AddChild(clockTimer);

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
			SyncClaimToMission();
			RefreshClaim();
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
				session.PendingArenaPeriod = arenaPeriod;
			}
			ShowSkirmishMechSelect();
		}, primary: true));

		root.AddChild(MakeButton("Back", () => GetTree().ReloadCurrentScene()));
	}

	private void ShowSkirmishMechSelect()
	{
		var panel = MakeSubmenuShell(out var root);
		panel.CustomMinimumSize = new Vector2(980, 0);

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		GameCatalog.EnsureBuilt();
		var premades = GameCatalog.SkirmishPremades;
		var selected = session?.SkirmishPremadeVariant ?? -1;
		if (selected < 0 && premades.Length > 0)
			selected = premades[0].Variant;

		var title = new Label
		{
			Text = "SELECT LOANER MAP",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);

		var blurb = new Label
		{
			Text =
				"Skirmish uses fixed premade kits. Your campaign garage loadout is not used or overwritten. " +
				"In co-op, wingmates may take the same design.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		root.AddChild(blurb);

		var detail = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Text
		};
		detail.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(detail);

		var preview = new HangarMechPreview
		{
			CustomMinimumSize = new Vector2(420, 260),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		root.AddChild(preview);

		var pickRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		pickRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(pickRow);

		var pickButtons = new Button[premades.Length];

		void RefreshSelection()
		{
			SkirmishPremadeDef? chosen = null;
			foreach (var def in premades)
			{
				if (def.Variant == selected)
				{
					chosen = def;
					break;
				}
			}

			chosen ??= premades.Length > 0 ? premades[0] : null;
			if (chosen == null)
				return;

			selected = chosen.Value.Variant;
			session?.SetSkirmishPremade(selected);
			detail.Text =
				$"{chosen.Value.DisplayName}\n{chosen.Value.Manufacturer}\n{chosen.Value.Blurb}";
			preview.ShowLoadout(chosen.Value.Loadout.Clone());

			for (var i = 0; i < pickButtons.Length; i++)
			{
				if (pickButtons[i] == null)
					continue;
				var isOn = premades[i].Variant == selected;
				pickButtons[i].Text = isOn
					? $"▶ {premades[i].DisplayName}"
					: premades[i].DisplayName;
			}
		}

		for (var i = 0; i < premades.Length; i++)
		{
			var def = premades[i];
			var captured = def.Variant;
			var btn = new Button
			{
				Text = def.DisplayName,
				CustomMinimumSize = new Vector2(210, 52),
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter
			};
			btn.AddThemeFontSizeOverride("font_size", 14);
			MechUiTheme.StyleGhostButton(btn);
			btn.Pressed += () =>
			{
				SfxService.Click();
				selected = captured;
				RefreshSelection();
			};
			pickButtons[i] = btn;
			pickRow.AddChild(btn);
		}

		RefreshSelection();

		root.AddChild(MakeButton("DEPLOY", () =>
		{
			if (session == null)
				return;
			session.SetSkirmishPremade(selected);
			session.BeginSkirmish();
			GetTree().ChangeSceneToFile("res://scenes/arena.tscn");
		}, primary: true));

		root.AddChild(MakeButton("Back", ShowSkirmishSetup));
	}

	private void ShowMultiplayerLobby()
	{
		ClearUi();
		MouseFilter = MouseFilterEnum.Stop;
		AddChild(MultiplayerLobbyUi.Create(ShowModeHub));
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
