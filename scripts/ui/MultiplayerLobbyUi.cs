using System;
using System.Linq;
using Godot;

namespace Mechanize;

/// <summary>
/// Two-phase multiplayer lobby: Entry (mode + humans) then MatchSetup (ready / bots / start).
/// Co-op Campaign and Rogue-Like share one MatchSetup layout.
/// </summary>
public partial class MultiplayerLobbyUi : Control
{
	private Action? _onBack;
	private NetSession? _net;
	private GameSession? _session;
	private LobbyChatPanel? _chat;
	private Label? _statusLabel;
	private VBoxContainer? _slotList;
	private Label? _modeTitle;
	private Button? _startButton;
	private Button? _readyButton;
	private int _selectedPremade = -1;
	private LobbyTeam _botTeamTarget = LobbyTeam.Alpha;

	public static MultiplayerLobbyUi Create(Action onBack)
	{
		var ui = new MultiplayerLobbyUi();
		ui._onBack = onBack;
		ui.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		ui.MouseFilter = MouseFilterEnum.Stop;
		return ui;
	}

	public override void _Ready()
	{
		_net = GetNodeOrNull<NetSession>("/root/NetSession");
		_session = GetNodeOrNull<GameSession>("/root/GameSession");
		_net?.SetLocalDisplayName(_session?.Profile.ResolveAccountHandle() ?? VoidCorpsIdentity.PlayerCorpCodename);
		if (_net != null)
		{
			_net.RosterChanged += Rebuild;
			_net.LobbyStateChanged += Rebuild;
			_net.Disconnected += Rebuild;
		}

		Rebuild();
	}

	public override void _ExitTree()
	{
		if (_net != null)
		{
			_net.RosterChanged -= Rebuild;
			_net.LobbyStateChanged -= Rebuild;
			_net.Disconnected -= Rebuild;
		}

		_chat?.Unbind();
	}

	private void Rebuild()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_chat = null;
		_statusLabel = null;
		_slotList = null;
		_modeTitle = null;
		_startButton = null;
		_readyButton = null;

		var dim = MechUiTheme.MakeDimOverlay();
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		center.OffsetLeft = 48;
		center.OffsetRight = -48;
		center.OffsetTop = 36;
		center.OffsetBottom = -36;
		AddChild(center);

		var panel = MechUiTheme.MakePanel("MpLobbyPanel", minWidth: 980f, deep: true);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.CustomMinimumSize = new Vector2(980, 560);
		center.AddChild(panel);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 10);
		panel.AddChild(root);

		var phase = _net?.LobbyPhase ?? LobbyPhase.Offline;
		if (phase == LobbyPhase.MatchSetup)
			BuildMatchSetup(root);
		else
			BuildEntry(root);
	}

	// ─── Entry Lobby ─────────────────────────────────────────────────────────

	private void BuildEntry(VBoxContainer root)
	{
		var header = new Label
		{
			Text = "Multiplayer Lobby",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.AccentHot
		};
		header.AddThemeFontSizeOverride("font_size", 28);
		root.AddChild(header);

		var sub = new Label
		{
			Text = "Decide who is playing and which mode. Host Ready advances to match setup.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		sub.AddThemeFontSizeOverride("font_size", 13);
		root.AddChild(sub);

		_statusLabel = new Label
		{
			Text = _net?.StatusMessage ?? "Offline",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent
		};
		_statusLabel.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(_statusLabel);

		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 16);
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(body);

		body.AddChild(BuildModeColumn());
		body.AddChild(BuildEntryLobbyColumn());

		_chat = LobbyChatPanel.Create(_net, 120f);
		_chat.CustomMinimumSize = new Vector2(0, 150);
		root.AddChild(_chat);

		var connectRow = BuildConnectRow();
		root.AddChild(connectRow);
	}

	private Control BuildModeColumn()
	{
		var col = MechUiTheme.MakePanel("ModeCol", deep: false);
		col.CustomMinimumSize = new Vector2(260, 280);
		col.SizeFlagsVertical = SizeFlags.ExpandFill;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		col.AddChild(inner);

		var title = new Label { Text = "Mode", Modulate = MechUiTheme.Muted };
		title.AddThemeFontSizeOverride("font_size", 14);
		inner.AddChild(title);

		foreach (MultiplayerGameMode mode in Enum.GetValues(typeof(MultiplayerGameMode)))
		{
			// Hide alias until Phase 2 solar co-op exists (both were launching RL).
			if (mode == MultiplayerGameMode.CoopCampaign)
				continue;

			var captured = mode;
			var selected = _net != null && _net.GameMode == mode;
			if (mode == MultiplayerGameMode.CoopRogueLike
			    && _net?.GameMode == MultiplayerGameMode.CoopCampaign)
				selected = true;
			var btn = MakeLobbyButton(LobbyModeRules.ModeLabel(mode), () =>
			{
				_net?.HostSetGameMode(captured);
				Rebuild();
			}, primary: selected);
			btn.Disabled = _net is { Mode: NetSession.NetMode.Client };
			inner.AddChild(btn);
		}

		return col;
	}

	private Control BuildEntryLobbyColumn()
	{
		var col = MechUiTheme.MakePanel("LobbyCol", deep: false);
		col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		col.SizeFlagsVertical = SizeFlags.ExpandFill;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		col.AddChild(inner);

		var title = new Label { Text = "Lobby", Modulate = MechUiTheme.Muted };
		title.AddThemeFontSizeOverride("font_size", 14);
		inner.AddChild(title);

		_slotList = new VBoxContainer();
		_slotList.AddThemeConstantOverride("separation", 6);
		inner.AddChild(_slotList);
		RefreshSlotList(entryStyle: true);

		inner.AddChild(MakeLobbyButton("+Invite", () =>
		{
			var summary = _net?.InviteSummary() ?? "Offline";
			DisplayServer.ClipboardSet(summary);
			if (_statusLabel != null)
				_statusLabel.Text = $"Invite copied — {summary}";
		}));

		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		inner.AddChild(spacer);

		var actions = new VBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		inner.AddChild(actions);

		actions.AddChild(MakeLobbyButton("Leave", () =>
		{
			_net?.DisconnectSession();
			_onBack?.Invoke();
		}));

		_readyButton = MakeLobbyButton("Ready", () =>
		{
			if (_net is not { Mode: NetSession.NetMode.Hosting })
			{
				if (_statusLabel != null)
					_statusLabel.Text = "Only the host can advance the lobby.";
				return;
			}

			if (_net.HostAdvanceToMatchSetup())
			{
				SfxService.Confirm();
				Rebuild();
			}
			else if (_statusLabel != null)
				_statusLabel.Text = "Need at least one pilot seated.";
		}, primary: true);
		_readyButton.Disabled = _net is not { Mode: NetSession.NetMode.Hosting };
		actions.AddChild(_readyButton);

		return col;
	}

	private Control BuildConnectRow()
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 8);

		var addressEdit = new LineEdit
		{
			PlaceholderText = "Host IP",
			Text = "127.0.0.1",
			CustomMinimumSize = new Vector2(200, 36)
		};
		var portEdit = new LineEdit
		{
			PlaceholderText = "Port",
			Text = NetSession.DefaultPort.ToString(),
			CustomMinimumSize = new Vector2(90, 36)
		};
		row.AddChild(addressEdit);
		row.AddChild(portEdit);

		row.AddChild(MakeLobbyButton("Host", () =>
		{
			var port = portEdit.Text.ToInt();
			if (port <= 0) port = NetSession.DefaultPort;
			var max = LobbyModeRules.MaxHumanPeers(_net?.GameMode ?? MultiplayerGameMode.CoopRogueLike);
			_net?.Host(port, max);
			Rebuild();
		}, primary: true));

		row.AddChild(MakeLobbyButton("Join", () =>
		{
			var port = portEdit.Text.ToInt();
			if (port <= 0) port = NetSession.DefaultPort;
			_net?.Join(addressEdit.Text, port);
			Rebuild();
		}, primary: true));

		row.AddChild(MakeLobbyButton("Back", () =>
		{
			_net?.DisconnectSession();
			_onBack?.Invoke();
		}));

		return row;
	}

	// ─── Match Setup (shared routing) ────────────────────────────────────────

	private void BuildMatchSetup(VBoxContainer root)
	{
		var mode = _net?.GameMode ?? MultiplayerGameMode.CoopRogueLike;
		if (LobbyModeRules.IsCoop(mode))
			BuildCoopSetup(root);
		else if (mode == MultiplayerGameMode.TeamSkirmish)
			BuildTeamSetup(root);
		else
			BuildFfaSetup(root);
	}

	private void BuildCoopSetup(VBoxContainer root)
	{
		EnsurePremadeSelected();
		var mode = _net!.GameMode;

		_modeTitle = new Label
		{
			Text = LobbyModeRules.ModeLabel(mode),
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.AccentHot
		};
		_modeTitle.AddThemeFontSizeOverride("font_size", 26);
		root.AddChild(_modeTitle);

		root.AddChild(MakeLobbyButton("- Details -", () => { }, primary: false));

		_statusLabel = MakeStatus();
		root.AddChild(_statusLabel);

		var actors = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		actors.AddThemeConstantOverride("separation", 12);
		actors.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(actors);

		var slots = _net.Slots;
		for (var i = 0; i < Math.Max(4, slots.Count); i++)
		{
			var slot = i < slots.Count ? slots[i] : new LobbySlot();
			actors.AddChild(BuildActorCard(slot, i));
		}

		var bottom = new HBoxContainer();
		bottom.AddThemeConstantOverride("separation", 12);
		bottom.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(bottom);

		bottom.AddChild(BuildCoopSettingsPanel());
		_chat = LobbyChatPanel.Create(_net, 160f);
		_chat.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		bottom.AddChild(_chat);

		root.AddChild(BuildMatchActionRow(includeAddBot: true));
	}

	private Control BuildCoopSettingsPanel()
	{
		var panel = MechUiTheme.MakePanel("CoopSettings", deep: false);
		panel.CustomMinimumSize = new Vector2(360, 180);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		panel.AddChild(inner);

		var mode = _net!.GameMode;
		inner.AddChild(new Label
		{
			Text =
				"Shared wing on the Rogue-Like sector map. Host deploys nodes.\n" +
				"(Solar campaign co-op is Phase 2 — this mode is Rogue-Like for now.)",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		});

		inner.AddChild(new Label
		{
			Text = "Optional: launch a co-op skirmish instead (loaner MAP, skirmish bag).",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		});

		GameCatalog.EnsureBuilt();
		var premades = GameCatalog.SkirmishPremades;
		var mechLabel = new Label { Modulate = MechUiTheme.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		mechLabel.AddThemeFontSizeOverride("font_size", 13);
		inner.AddChild(mechLabel);

		void RefreshMech()
		{
			SkirmishPremadeDef? chosen = null;
			foreach (var def in premades)
			{
				if (def.Variant == _selectedPremade)
				{
					chosen = def;
					break;
				}
			}

			if (chosen == null && premades.Length > 0)
			{
				chosen = premades[0];
				_selectedPremade = chosen.Value.Variant;
			}

			if (chosen == null)
			{
				mechLabel.Text = "No skirmish premades.";
				return;
			}

			_session?.SetSkirmishPremade(_selectedPremade);
			mechLabel.Text = $"Loaner: {chosen.Value.DisplayName}";
		}

		var mechRow = new HBoxContainer();
		mechRow.AddThemeConstantOverride("separation", 4);
		inner.AddChild(mechRow);
		foreach (var def in premades.Take(4))
		{
			var v = def.Variant;
			mechRow.AddChild(MakeLobbyButton(def.DisplayName, () =>
			{
				_selectedPremade = v;
				RefreshMech();
			}));
		}

		RefreshMech();

		if (_net.Mode == NetSession.NetMode.Hosting)
		{
			inner.AddChild(MakeLobbyButton("Start Co-op Skirmish (PvE)", () => TryStartCoopSkirmish(), primary: false));
		}

		return panel;
	}

	private void BuildFfaSetup(VBoxContainer root)
	{
		EnsurePremadeSelected();
		_modeTitle = new Label
		{
			Text = "FFA Skirmish",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.AccentHot
		};
		_modeTitle.AddThemeFontSizeOverride("font_size", 26);
		root.AddChild(_modeTitle);

		_statusLabel = MakeStatus();
		root.AddChild(_statusLabel);

		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 12);
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(body);

		body.AddChild(BuildSideLobbyColumn(showAddBot: false));
		body.AddChild(BuildMapSettingsPanel("Free-for-all. Last MAP standing wins."));
		_chat = LobbyChatPanel.Create(_net, 200f);
		_chat.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.AddChild(_chat);

		root.AddChild(BuildMatchActionRow(includeAddBot: true));
	}

	private void BuildTeamSetup(VBoxContainer root)
	{
		EnsurePremadeSelected();
		_modeTitle = new Label
		{
			Text = "Team Skirmish",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.AccentHot
		};
		_modeTitle.AddThemeFontSizeOverride("font_size", 26);
		root.AddChild(_modeTitle);

		_statusLabel = MakeStatus();
		root.AddChild(_statusLabel);

		var teams = new HBoxContainer();
		teams.AddThemeConstantOverride("separation", 12);
		teams.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(teams);

		teams.AddChild(BuildTeamColumn(LobbyTeam.Alpha, "Team 1"));
		teams.AddChild(BuildTeamCenterControls());
		teams.AddChild(BuildTeamColumn(LobbyTeam.Bravo, "Team 2"));

		var bottom = new HBoxContainer();
		bottom.AddThemeConstantOverride("separation", 12);
		root.AddChild(bottom);

		var actions = new VBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		actions.CustomMinimumSize = new Vector2(280, 0);
		bottom.AddChild(actions);
		actions.AddChild(BuildMatchActionRow(includeAddBot: false));

		_chat = LobbyChatPanel.Create(_net, 140f);
		_chat.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		bottom.AddChild(_chat);
	}

	private Control BuildTeamColumn(LobbyTeam team, string title)
	{
		var col = MechUiTheme.MakePanel($"Team_{team}", deep: false);
		col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		col.SizeFlagsVertical = SizeFlags.ExpandFill;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		col.AddChild(inner);

		inner.AddChild(new Label { Text = title, Modulate = MechUiTheme.Muted });
		if (_net == null)
			return col;

		for (var i = 0; i < _net.Slots.Count; i++)
		{
			var slot = _net.Slots[i];
			if (!slot.IsOccupied || slot.Team != team)
				continue;
			var idx = i;
			inner.AddChild(BuildReadySlotRow(slot, idx, allowTeamSwap: true));
		}

		var empties = 0;
		foreach (var s in _net.Slots)
		{
			if (!s.IsOccupied) empties++;
		}

		for (var e = 0; e < Math.Min(empties, 2); e++)
		{
			inner.AddChild(new Label
			{
				Text = "— open —",
				Modulate = MechUiTheme.Muted.Darkened(0.2f)
			});
		}

		return col;
	}

	private Control BuildTeamCenterControls()
	{
		var col = MechUiTheme.MakePanel("TeamCenter", deep: true);
		col.CustomMinimumSize = new Vector2(200, 0);
		var inner = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		inner.AddThemeConstantOverride("separation", 8);
		col.AddChild(inner);

		inner.AddChild(new Label
		{
			Text = "Map Info",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Text
		});
		inner.AddChild(new Label
		{
			Text = "Additional Settings",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		var host = _net is { Mode: NetSession.NetMode.Hosting };
		var addAlpha = MakeLobbyButton("Add Bot → T1", () =>
		{
			_botTeamTarget = LobbyTeam.Alpha;
			_net?.HostAddBot(LobbyTeam.Alpha);
		});
		addAlpha.Disabled = !host;
		inner.AddChild(addAlpha);

		var addBravo = MakeLobbyButton("Add Bot → T2", () =>
		{
			_botTeamTarget = LobbyTeam.Bravo;
			_net?.HostAddBot(LobbyTeam.Bravo);
		});
		addBravo.Disabled = !host;
		inner.AddChild(addBravo);

		inner.AddChild(BuildPremadePickerCompact());
		return col;
	}

	private Control BuildSideLobbyColumn(bool showAddBot)
	{
		var col = MechUiTheme.MakePanel("SideLobby", deep: false);
		col.CustomMinimumSize = new Vector2(240, 0);
		col.SizeFlagsVertical = SizeFlags.ExpandFill;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		col.AddChild(inner);

		inner.AddChild(new Label { Text = "Lobby", Modulate = MechUiTheme.Muted });
		_slotList = new VBoxContainer();
		_slotList.AddThemeConstantOverride("separation", 4);
		inner.AddChild(_slotList);
		RefreshSlotList(entryStyle: false);

		if (showAddBot)
		{
			var add = MakeLobbyButton("+Invite", () =>
			{
				DisplayServer.ClipboardSet(_net?.InviteSummary() ?? "");
			});
			inner.AddChild(add);
		}

		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		inner.AddChild(spacer);
		return col;
	}

	private Control BuildMapSettingsPanel(string blurb)
	{
		var panel = MechUiTheme.MakePanel("MapSettings", deep: false);
		panel.CustomMinimumSize = new Vector2(260, 0);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 8);
		panel.AddChild(inner);

		inner.AddChild(new Label { Text = "Map Info", Modulate = MechUiTheme.Text });
		inner.AddChild(new Label
		{
			Text = blurb,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		});
		inner.AddChild(new Label { Text = "Additional Settings", Modulate = MechUiTheme.Muted });
		inner.AddChild(BuildPremadePickerCompact());
		return panel;
	}

	private Control BuildPremadePickerCompact()
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		GameCatalog.EnsureBuilt();
		var premades = GameCatalog.SkirmishPremades;
		var label = new Label { Modulate = MechUiTheme.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", 12);
		box.AddChild(label);

		void Refresh()
		{
			foreach (var def in premades)
			{
				if (def.Variant != _selectedPremade)
					continue;
				label.Text = $"MAP: {def.DisplayName}";
				_session?.SetSkirmishPremade(_selectedPremade);
				return;
			}

			if (premades.Length > 0)
			{
				_selectedPremade = premades[0].Variant;
				label.Text = $"MAP: {premades[0].DisplayName}";
				_session?.SetSkirmishPremade(_selectedPremade);
			}
		}

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		box.AddChild(row);
		foreach (var def in premades.Take(3))
		{
			var v = def.Variant;
			row.AddChild(MakeLobbyButton(def.DisplayName, () =>
			{
				_selectedPremade = v;
				Refresh();
			}));
		}

		Refresh();
		return box;
	}

	private Control BuildMatchActionRow(bool includeAddBot)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 8);

		if (includeAddBot)
		{
			var addBot = MakeLobbyButton("Add Bot", () =>
			{
				_net?.HostAddBot(_botTeamTarget);
			});
			addBot.Disabled = _net is not { Mode: NetSession.NetMode.Hosting };
			row.AddChild(addBot);
		}

		row.AddChild(MakeLobbyButton("Leave", () =>
		{
			_net?.DisconnectSession();
			_onBack?.Invoke();
		}));

		_readyButton = MakeLobbyButton("Ready", () =>
		{
			_net?.ToggleLocalReady();
		}, primary: false);
		_readyButton.Disabled = _net is not { IsOnline: true };
		row.AddChild(_readyButton);

		_startButton = MakeLobbyButton("Start", () => TryStartMatch(), primary: true);
		_startButton.Disabled = !(_net?.CanHostStartMatch() ?? false);
		row.AddChild(_startButton);

		row.AddChild(MakeLobbyButton("Back", () =>
		{
			if (_net is { Mode: NetSession.NetMode.Hosting })
				_net.HostReturnToEntry();
			else if (_statusLabel != null)
				_statusLabel.Text = "Only the host can return to entry.";
			Rebuild();
		}));

		return row;
	}

	private Control BuildActorCard(LobbySlot slot, int index)
	{
		var card = MechUiTheme.MakePanel($"Actor_{index}", deep: true);
		card.CustomMinimumSize = new Vector2(150, 220);
		var inner = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		inner.AddThemeConstantOverride("separation", 8);
		card.AddChild(inner);

		var preview = new ColorRect
		{
			CustomMinimumSize = new Vector2(110, 140),
			Color = slot.IsOccupied
				? new Color(0.18f, 0.22f, 0.28f)
				: new Color(0.08f, 0.09f, 0.11f)
		};
		inner.AddChild(preview);

		var actor = new Label
		{
			Text = slot.IsOccupied ? "Actor" : "Empty",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Muted
		};
		inner.AddChild(actor);

		var readyRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		var check = new CheckBox
		{
			ButtonPressed = slot.Ready,
			Disabled = true,
			Text = ""
		};
		readyRow.AddChild(check);
		readyRow.AddChild(new Label
		{
			Text = slot.IsOccupied
				? (string.IsNullOrEmpty(slot.DisplayName) ? $"Player {index + 1}" : slot.DisplayName)
				: $"Player {index + 1}",
			Modulate = MechUiTheme.Text
		});
		inner.AddChild(readyRow);

		if (slot.IsBot)
		{
			inner.AddChild(new Label
			{
				Text = "(bot)",
				HorizontalAlignment = HorizontalAlignment.Center,
				Modulate = MechUiTheme.Cyan
			});
		}

		return card;
	}

	private Control BuildReadySlotRow(LobbySlot slot, int slotIndex, bool allowTeamSwap)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		var check = new CheckBox { ButtonPressed = slot.Ready, Disabled = true };
		row.AddChild(check);
		row.AddChild(new Label
		{
			Text = string.IsNullOrEmpty(slot.DisplayName) ? $"Slot {slotIndex + 1}" : slot.DisplayName,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Modulate = MechUiTheme.Text
		});

		if (slot.IsBot && _net is { Mode: NetSession.NetMode.Hosting })
		{
			var botId = slot.BotId;
			row.AddChild(MakeLobbyButton("X", () => _net.HostRemoveBot(botId)));
		}

		if (allowTeamSwap && slot.IsOccupied && _net is { Mode: NetSession.NetMode.Hosting })
		{
			var other = slot.Team == LobbyTeam.Alpha ? LobbyTeam.Bravo : LobbyTeam.Alpha;
			var idx = slotIndex;
			row.AddChild(MakeLobbyButton("<>", () => _net.HostSetSlotTeam(idx, other)));
		}

		return row;
	}

	private void RefreshSlotList(bool entryStyle)
	{
		if (_slotList == null || _net == null)
			return;
		foreach (var c in _slotList.GetChildren())
			c.QueueFree();

		for (var i = 0; i < _net.Slots.Count; i++)
		{
			var slot = _net.Slots[i];
			if (entryStyle)
			{
				var label = new Label
				{
					Text = slot.IsOccupied
						? $"{slot.DisplayName}"
						: $"Slot {i + 1}",
					Modulate = slot.IsOccupied ? MechUiTheme.Text : MechUiTheme.Muted
				};
				var frame = MechUiTheme.MakePanel($"Slot_{i}", deep: true);
				frame.AddChild(label);
				_slotList.AddChild(frame);
			}
			else
			{
				_slotList.AddChild(BuildReadySlotRow(slot, i, allowTeamSwap: false));
			}
		}
	}

	private Label MakeStatus()
	{
		var label = new Label
		{
			Text = FormatMatchStatus(),
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = MechUiTheme.Accent
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		return label;
	}

	private string FormatMatchStatus()
	{
		if (_net == null || !_net.IsOnline)
			return "Offline";
		var ready = 0;
		var total = 0;
		foreach (var s in _net.Slots)
		{
			if (!s.IsOccupied) continue;
			total++;
			if (s.Ready) ready++;
		}

		return $"{_net.StatusMessage}  ·  Ready {ready}/{total}";
	}

	private void EnsurePremadeSelected()
	{
		GameCatalog.EnsureBuilt();
		if (_selectedPremade < 0 && GameCatalog.SkirmishPremades.Length > 0)
			_selectedPremade = GameCatalog.SkirmishPremades[0].Variant;
		if (_selectedPremade >= 0)
			_session?.SetSkirmishPremade(_selectedPremade);
	}

	private void TryStartMatch()
	{
		if (_net is not { Mode: NetSession.NetMode.Hosting } || _session == null)
			return;
		if (!_net.CanHostStartMatch())
		{
			if (_statusLabel != null)
				_statusLabel.Text = "All pilots (and bots) must Ready before Start.";
			return;
		}

		var mode = _net.GameMode;
		_session.MultiplayerGameMode = mode;
		_session.ApplyLobbyRoster(_net.BuildRosterPayload());

		if (LobbyModeRules.IsCoop(mode))
		{
			_net.Intent = NetSession.LobbyIntent.CoopCampaign;
			_session.ClearSkirmishLoaner();
			_session.BeginCampaignRun(0);
			_session.CoopMatch = true;
			SfxService.Confirm();
			var payload = _session.BuildLaunchPayload(false, "campaign");
			payload["mp_mode"] = (int)mode;
			payload["roster"] = _net.BuildRosterPayload();
			_net.HostLaunchMatch(payload);
			return;
		}

		// Team / FFA skirmish
		EnsurePremadeSelected();
		if (_session.SkirmishLoaner == null)
		{
			if (_statusLabel != null)
				_statusLabel.Text = "Pick a loaner MAP first.";
			return;
		}

		_net.Intent = NetSession.LobbyIntent.CoopSkirmish;
		_session.CoopMatch = true;
		_session.PendingBossEncounter = BossEncounterId.None;
		_session.BeginCoopSkirmish();
		SfxService.Confirm();
		var skirmishPayload = _session.BuildLaunchPayload(false);
		skirmishPayload["mp_mode"] = (int)mode;
		skirmishPayload["roster"] = _net.BuildRosterPayload();
		skirmishPayload["pvp"] = true;
		_net.HostLaunchMatch(skirmishPayload);
	}

	private void TryStartCoopSkirmish()
	{
		if (_net is not { Mode: NetSession.NetMode.Hosting } || _session == null)
			return;
		if (!_net.CanHostStartMatch())
		{
			if (_statusLabel != null)
				_statusLabel.Text = "All pilots must Ready before Start.";
			return;
		}

		EnsurePremadeSelected();
		if (_session.SkirmishLoaner == null)
		{
			if (_statusLabel != null)
				_statusLabel.Text = "Pick a loaner MAP first.";
			return;
		}

		_net.Intent = NetSession.LobbyIntent.CoopSkirmish;
		_session.MultiplayerGameMode = _net.GameMode;
		_session.ApplyLobbyRoster(_net.BuildRosterPayload());
		_session.CoopMatch = true;
		_session.PendingBossEncounter = BossEncounterId.None;
		_session.BeginCoopSkirmish();
		SfxService.Confirm();
		var payload = _session.BuildLaunchPayload(false);
		payload["mp_mode"] = (int)_net.GameMode;
		payload["roster"] = _net.BuildRosterPayload();
		payload["pvp"] = false;
		_net.HostLaunchMatch(payload);
	}

	private static Button MakeLobbyButton(string text, Action onPress, bool primary = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(120, 36),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		button.AddThemeFontSizeOverride("font_size", 14);
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
