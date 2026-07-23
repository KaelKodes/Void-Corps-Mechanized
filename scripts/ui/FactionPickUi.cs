using Godot;

namespace Mechanize;

/// <summary>
/// Profile create / faction lock: pilot name, Cat/Dog, and portrait.
/// </summary>
public partial class FactionPickUi : Control
{
	private FactionId _faction = FactionId.Cat;
	private int _portraitIndex;
	private LineEdit? _nameEdit;
	private Label? _status;
	private Button? _confirm;
	private readonly Button[] _factionTabs = new Button[2];
	private readonly Button[] _portraitButtons = new Button[PilotPortraits.PortraitCount];
	private bool _creatingSlot;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Menu);
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		_creatingSlot = session != null
			&& (!session.HasAnyProfile
			    || session.PendingCreateSlot >= 0
			    || session.PendingFactionContinue == PendingFactionContinue.NewProfile);
		Build();
		RefreshPortraits();
		RefreshConfirm();
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		var bg = new ColorRect
		{
			Color = MechUiTheme.MapVoid,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var root = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 120;
		root.OffsetRight = -120;
		root.OffsetTop = 40;
		root.OffsetBottom = -40;
		root.AddThemeConstantOverride("separation", 12);
		AddChild(root);

		var title = new Label
		{
			Text = _creatingSlot ? "CREATE PILOT" : "CHOOSE YOUR PEOPLE",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent
		};
		title.AddThemeFontSizeOverride("font_size", 36);
		root.AddChild(title);

		var blurb = new Label
		{
			Text =
				"Cat-folk and dog-folk share this system — different homeworlds, same frontier.\n" +
				"Name your pilot, pick a side, and lock a cockpit ID portrait for this profile.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		root.AddChild(blurb);

		_nameEdit = new LineEdit
		{
			PlaceholderText = "Pilot callsign (1–24 characters)",
			CustomMinimumSize = new Vector2(420, 40),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MaxLength = SaveService.MaxHandleLength,
			Alignment = HorizontalAlignment.Center
		};
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session != null && !_creatingSlot)
			_nameEdit.Text = session.Profile.ResolveAccountHandle();
		else if (session != null && !string.IsNullOrWhiteSpace(session.Profile.AccountHandle)
		         && session.Profile.AccountHandle != VoidCorpsIdentity.PlayerCorpCodename)
			_nameEdit.Text = session.Profile.ResolveAccountHandle();
		_nameEdit.TextChanged += _ => RefreshConfirm();
		root.AddChild(_nameEdit);

		var tabRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		tabRow.AddThemeConstantOverride("separation", 16);
		root.AddChild(tabRow);

		_factionTabs[0] = MakeFactionTab("CAT", FactionId.Cat);
		_factionTabs[1] = MakeFactionTab("DOG", FactionId.Dog);
		tabRow.AddChild(_factionTabs[0]);
		tabRow.AddChild(_factionTabs[1]);

		var grid = new GridContainer
		{
			Columns = PilotPortraits.GridSize,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		root.AddChild(grid);

		for (var i = 0; i < PilotPortraits.PortraitCount; i++)
		{
			var index = i;
			var btn = new Button
			{
				ToggleMode = true,
				CustomMinimumSize = new Vector2(112, 112),
				Flat = true
			};
			btn.Pressed += () =>
			{
				_portraitIndex = index;
				RefreshPortraitSelection();
				RefreshConfirm();
				SfxService.Click();
			};
			_portraitButtons[i] = btn;
			grid.AddChild(btn);
		}

		_status = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent
		};
		root.AddChild(_status);

		_confirm = new Button
		{
			Text = _creatingSlot ? "CREATE PROFILE" : "LOCK FACTION",
			CustomMinimumSize = new Vector2(280, 48),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		_confirm.AddThemeFontSizeOverride("font_size", 18);
		MechUiTheme.StylePrimaryButton(_confirm);
		_confirm.Pressed += OnConfirm;
		root.AddChild(_confirm);

		if (!_creatingSlot)
		{
			var back = new Button
			{
				Text = "BACK",
				CustomMinimumSize = new Vector2(160, 40),
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter
			};
			MechUiTheme.StyleGhostButton(back);
			back.Pressed += () =>
			{
				SfxService.Click();
				var s = GetNodeOrNull<GameSession>("/root/GameSession");
				if (s != null)
					s.PendingFactionContinue = PendingFactionContinue.None;
				GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
			};
			root.AddChild(back);
		}
	}

	private Button MakeFactionTab(string label, FactionId faction)
	{
		var btn = new Button
		{
			Text = label,
			ToggleMode = true,
			CustomMinimumSize = new Vector2(140, 44)
		};
		MechUiTheme.StyleGhostButton(btn);
		btn.Pressed += () =>
		{
			_faction = faction;
			RefreshFactionTabs();
			RefreshPortraits();
			RefreshConfirm();
			SfxService.Click();
		};
		return btn;
	}

	private void RefreshFactionTabs()
	{
		_factionTabs[0].ButtonPressed = _faction == FactionId.Cat;
		_factionTabs[1].ButtonPressed = _faction == FactionId.Dog;
		_factionTabs[0].Modulate = _faction == FactionId.Cat ? MechUiTheme.AccentHot : Colors.White;
		_factionTabs[1].Modulate = _faction == FactionId.Dog ? MechUiTheme.AccentHot : Colors.White;
	}

	private void RefreshPortraits()
	{
		RefreshFactionTabs();
		for (var i = 0; i < _portraitButtons.Length; i++)
		{
			var btn = _portraitButtons[i];
			foreach (var child in btn.GetChildren())
				child.QueueFree();
			btn.Text = "";

			var tex = PilotPortraits.GetPortrait(_faction, i);
			if (tex != null)
			{
				var rect = new TextureRect
				{
					Texture = tex,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
					MouseFilter = MouseFilterEnum.Ignore
				};
				rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				btn.AddChild(rect);
			}
			else
			{
				btn.Text = $"{i + 1}";
			}
		}

		RefreshPortraitSelection();
	}

	private void RefreshPortraitSelection()
	{
		for (var i = 0; i < _portraitButtons.Length; i++)
		{
			var selected = i == _portraitIndex;
			_portraitButtons[i].ButtonPressed = selected;
			_portraitButtons[i].Modulate = selected ? MechUiTheme.AccentHot : Colors.White;
		}
	}

	private void RefreshConfirm()
	{
		var handle = SaveService.SanitizeHandle(_nameEdit?.Text);
		var validName = SaveService.IsValidHandle(handle);
		if (_status != null)
		{
			_status.Text = validName
				? $"{handle}  ·  {PilotPortraits.DisplayName(_faction)} pilot #{_portraitIndex + 1}"
				: "Enter a pilot callsign (1–24 characters).";
		}

		if (_confirm != null)
			_confirm.Disabled = !validName;
	}

	private void OnConfirm()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null || _nameEdit == null)
			return;

		var handle = SaveService.SanitizeHandle(_nameEdit.Text);
		if (!SaveService.IsValidHandle(handle))
		{
			if (_status != null)
				_status.Text = "Enter a valid pilot callsign.";
			SfxService.PlayUiError(UiErrorTone.Incorrect);
			return;
		}

		if (_creatingSlot || !session.HasAnyProfile)
		{
			var slot = session.PendingCreateSlot;
			if (slot < 0)
			{
				slot = 0;
				for (var i = 0; i < SaveService.MaxSlots; i++)
				{
					if (!SaveService.SlotOccupied(i))
					{
						slot = i;
						break;
					}
				}
			}

			if (SaveService.SlotOccupied(slot))
			{
				if (_status != null)
					_status.Text = "That slot is occupied — pick another from the profile hub.";
				SfxService.PlayUiError(UiErrorTone.DeeDoo);
				return;
			}

			if (!session.CreateSlot(slot, handle, _faction, _portraitIndex))
			{
				if (_status != null)
					_status.Text = "Could not create profile.";
				SfxService.PlayUiError(UiErrorTone.BuzzBuzz);
				return;
			}

			session.PendingFactionContinue = PendingFactionContinue.NewProfile;
			SfxService.Confirm();
			session.ContinueAfterFactionPick();
			return;
		}

		session.ActivateCampaignProfile();
		session.Profile.SetAccountHandle(handle);
		if (!session.Profile.SetFaction(_faction, _portraitIndex))
		{
			if (_status != null)
				_status.Text = "Faction already locked on this profile.";
			SfxService.PlayUiError(UiErrorTone.DeeDoo);
			return;
		}

		session.SaveProfile();
		session.SyncIdentityAfterCreate();
		SfxService.Confirm();
		session.ContinueAfterFactionPick();
	}
}
