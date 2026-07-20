using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Warcraft 3–inspired multiplayer lobby: form session → pick game type → mode settings lobby.
/// Solo-reviewable without a second player.
/// </summary>
public partial class MultiplayerLobbyUi : Control
{
	private enum Stage
	{
		FormSession,
		PickGameType,
		ModeLobby
	}

	private sealed class SlotRow
	{
		public LobbySlotFill Fill;
		public string Name = "Open";
		public int Team; // 0 = detachment / Alpha, 1 = Bravo
		public Color Swatch;
	}

	private Stage _stage = Stage.FormSession;
	private LobbyGameType _gameType = LobbyGameType.None;
	private string _statusMessage = "";
	private string _joinField = "";
	private readonly List<string> _log = new();
	private readonly List<SlotRow> _slots = new();
	private int _claimIndex;
	private PilotDifficulty _difficulty = PilotDifficulty.Easy;
	private MissionType _missionType = MissionType.DestroyAllEnemies;
	private int _skirmishTeamSize = 2; // per side; 2→2v2 … up to 10

	private static readonly Color[] Swatches =
	{
		new(0.85f, 0.25f, 0.22f),
		new(0.25f, 0.45f, 0.9f),
		new(0.2f, 0.75f, 0.7f),
		new(0.9f, 0.75f, 0.2f),
		new(0.7f, 0.35f, 0.85f),
		new(0.95f, 0.55f, 0.2f),
		new(0.4f, 0.85f, 0.35f),
		new(0.85f, 0.45f, 0.65f),
		new(0.55f, 0.6f, 0.7f),
		new(0.95f, 0.95f, 0.95f)
	};

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session != null)
		{
			_difficulty = session.PendingDifficulty;
			_missionType = session.PendingMission == MissionType.BossEncounter
				? MissionType.DestroyAllEnemies
				: session.PendingMission;
			for (var i = 0; i < VoidCorpsIdentity.ClaimSites.Length; i++)
			{
				if (VoidCorpsIdentity.ClaimSites[i].Code == session.CurrentClaim.Code)
					_claimIndex = i;
			}
		}

		Rebuild();
	}

	public override void _ExitTree()
	{
		// Leaving the lobby scene without an active match tears the listen session down.
		if (IsInsideTree())
			MultiplayerListenSession.Shutdown(GetTree());
	}

	private void Rebuild()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		AddChild(MechUiTheme.MakeDimOverlay());

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 48);
		margin.AddThemeConstantOverride("margin_right", 48);
		margin.AddThemeConstantOverride("margin_top", 36);
		margin.AddThemeConstantOverride("margin_bottom", 36);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);

		var subtitle = _stage switch
		{
			Stage.FormSession => "Form a detachment session — host or join by address",
			Stage.PickGameType => "Choose the operation type for this session",
			_ => _gameType == LobbyGameType.CoopCampaign
				? "Co-op campaign lobby — detachment slots and claim brief"
				: "Skirmish lobby — teams, claim, and launch settings"
		};
		root.AddChild(MechUiTheme.MakeHeaderStrip("MULTIPLAYER LOBBY", subtitle));

		if (!string.IsNullOrEmpty(_statusMessage))
		{
			var status = new Label
			{
				Text = _statusMessage,
				HorizontalAlignment = HorizontalAlignment.Center,
				Modulate = MechUiTheme.Danger,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			root.AddChild(status);
		}

		switch (_stage)
		{
			case Stage.FormSession:
				BuildFormSession(root);
				break;
			case Stage.PickGameType:
				BuildPickGameType(root);
				break;
			case Stage.ModeLobby:
				BuildModeLobby(root);
				break;
		}
	}

	private void BuildFormSession(VBoxContainer root)
	{
		var panel = MechUiTheme.MakePanel("SessionPanel", deep: true);
		root.AddChild(panel);
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 12);
		panel.AddChild(col);

		col.AddChild(MechUiTheme.MakeSectionLabel("SESSION"));
		var blurb = new Label
		{
			Text = "Listen-server: one pilot hosts on their machine. Friends connect with the join address — no dedicated servers for now.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted
		};
		col.AddChild(blurb);

		var hostBtn = MakePrimary("HOST SESSION", TryHost);
		col.AddChild(hostBtn);

		col.AddChild(MechUiTheme.MakeSectionLabel("JOIN"));
		var joinRow = new HBoxContainer();
		joinRow.AddThemeConstantOverride("separation", 10);
		col.AddChild(joinRow);

		var joinEdit = new LineEdit
		{
			Text = _joinField,
			PlaceholderText = "ip:port  (e.g. 192.168.1.10:7777)",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(320, 40)
		};
		joinEdit.TextChanged += t => _joinField = t;
		joinRow.AddChild(joinEdit);

		var joinBtn = MakeGhost("JOIN", () => TryJoin(joinEdit.Text));
		joinBtn.CustomMinimumSize = new Vector2(120, 40);
		joinRow.AddChild(joinBtn);

		col.AddChild(MakeGhost("BACK TO MENU", LeaveToMenu));
	}

	private void BuildPickGameType(VBoxContainer root)
	{
		if (MultiplayerListenSession.IsHosting && !string.IsNullOrEmpty(MultiplayerListenSession.JoinAddress))
			root.AddChild(BuildJoinInfoBar());

		if (!MultiplayerListenSession.IsHosting)
		{
			var waitPanel = MechUiTheme.MakePanel("WaitHost", deep: true);
			root.AddChild(waitPanel);
			var waitCol = new VBoxContainer();
			waitCol.AddThemeConstantOverride("separation", 10);
			waitPanel.AddChild(waitCol);
			waitCol.AddChild(MechUiTheme.MakeSectionLabel("WAITING"));
			var wait = new Label
			{
				Text = "Connected. Waiting for the host to choose Co-op Campaign or Skirmish.\n(Game-type sync ships with full lobby replication — for now only the host advances this screen.)",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Modulate = MechUiTheme.Muted
			};
			waitCol.AddChild(wait);
			root.AddChild(MakeGhost("LEAVE SESSION", CancelSessionToForm));
			return;
		}

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		row.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(row);

		row.AddChild(BuildGameTypeCard(
			"CO-OP CAMPAIGN",
			"Up to 4 wing MAPs on one claim detachment. Host owns the campaign save; guests bring loadouts.",
			() => EnterModeLobby(LobbyGameType.CoopCampaign)));

		row.AddChild(BuildGameTypeCard(
			"SKIRMISH",
			"Team fights on a claim site. Near-term cap 10v10. Use this lobby to stress sync and gather feedback.",
			() => EnterModeLobby(LobbyGameType.Skirmish)));

		root.AddChild(MakeGhost("CANCEL SESSION", CancelSessionToForm));
	}

	private Control BuildGameTypeCard(string title, string body, System.Action onPick)
	{
		var panel = MechUiTheme.MakePanel(title, deep: false);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 12);
		panel.AddChild(col);

		var t = new Label { Text = title, Modulate = MechUiTheme.AccentHot };
		t.AddThemeFontSizeOverride("font_size", 22);
		col.AddChild(t);

		var b = new Label
		{
			Text = body,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		col.AddChild(b);

		col.AddChild(MakePrimary("SELECT", onPick));
		return panel;
	}

	private void BuildModeLobby(VBoxContainer root)
	{
		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 16);
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(body);

		body.AddChild(BuildRosterPanel());
		body.AddChild(BuildBriefingPanel());

		root.AddChild(BuildOpsLog());
		root.AddChild(BuildActionRow());
	}

	private Control BuildRosterPanel()
	{
		var panel = MechUiTheme.MakePanel("Roster", deep: true);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsStretchRatio = 1.35f;

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 8);
		panel.AddChild(col);

		col.AddChild(MechUiTheme.MakeSectionLabel("ROSTER"));

		if (_gameType == LobbyGameType.CoopCampaign)
		{
			col.AddChild(MakeGroupHeader("DETACHMENT"));
			for (var i = 0; i < _slots.Count; i++)
				col.AddChild(BuildSlotRow(i, showTeam: false, allowComputer: false));
		}
		else
		{
			var sizeRow = new HBoxContainer();
			sizeRow.AddThemeConstantOverride("separation", 8);
			col.AddChild(sizeRow);
			var sizeLabel = new Label
			{
				Text = $"Team size: {_skirmishTeamSize}v{_skirmishTeamSize}  (max 10v10)",
				Modulate = MechUiTheme.Muted,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			sizeRow.AddChild(sizeLabel);
			if (MultiplayerListenSession.IsHosting)
			{
				var dec = MakeGhost("−", () =>
				{
					_skirmishTeamSize = Mathf.Max(1, _skirmishTeamSize - 1);
					RebuildSkirmishSlots();
					Rebuild();
				});
				dec.CustomMinimumSize = new Vector2(44, 32);
				sizeRow.AddChild(dec);
				var inc = MakeGhost("+", () =>
				{
					_skirmishTeamSize = Mathf.Min(10, _skirmishTeamSize + 1);
					RebuildSkirmishSlots();
					Rebuild();
				});
				inc.CustomMinimumSize = new Vector2(44, 32);
				sizeRow.AddChild(inc);
			}

			col.AddChild(MakeGroupHeader("ALPHA"));
			for (var i = 0; i < _slots.Count; i++)
			{
				if (_slots[i].Team == 0)
					col.AddChild(BuildSlotRow(i, showTeam: true, allowComputer: true));
			}

			col.AddChild(MakeGroupHeader("BRAVO"));
			for (var i = 0; i < _slots.Count; i++)
			{
				if (_slots[i].Team == 1)
					col.AddChild(BuildSlotRow(i, showTeam: true, allowComputer: true));
			}
		}

		return panel;
	}

	private Control BuildBriefingPanel()
	{
		var panel = MechUiTheme.MakePanel("Briefing", deep: false);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.CustomMinimumSize = new Vector2(360, 0);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 10);
		panel.AddChild(col);

		col.AddChild(MechUiTheme.MakeSectionLabel("BRIEFING"));
		var mode = new Label
		{
			Text = _gameType == LobbyGameType.CoopCampaign ? "CO-OP CAMPAIGN" : "SKIRMISH",
			Modulate = MechUiTheme.AccentHot
		};
		mode.AddThemeFontSizeOverride("font_size", 20);
		col.AddChild(mode);

		if (MultiplayerListenSession.IsHosting && !string.IsNullOrEmpty(MultiplayerListenSession.JoinAddress))
			col.AddChild(BuildJoinInfoBar(compact: true));

		var filled = 0;
		foreach (var s in _slots)
		{
			if (s.Fill is LobbySlotFill.Human or LobbySlotFill.Computer)
				filled++;
		}

		var slotsLabel = new Label
		{
			Text = $"Slots  ·  {filled} / {_slots.Count} occupied",
			Modulate = MechUiTheme.Cyan
		};
		col.AddChild(slotsLabel);

		if (_gameType == LobbyGameType.CoopCampaign)
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			var corp = session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename;
			var camp = new Label
			{
				Text = session?.InCampaign == true
					? $"Host campaign active  ·  {corp}\nGuests drop into the host’s current claim path."
					: $"Host campaign  ·  {corp}\nHost should have (or start) a campaign run before deploy. Lobby UI is ready either way.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Modulate = MechUiTheme.Muted
			};
			col.AddChild(camp);
		}
		else
		{
			col.AddChild(BuildClaimMissionControls());
		}

		return panel;
	}

	private Control BuildClaimMissionControls()
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);

		var claim = VoidCorpsIdentity.ClaimSites[_claimIndex];
		var claimLabel = new Label
		{
			Text = $"[{ArenaSizeUtil.Label(claim.Size)}]  {claim.Code}\n{claim.DisplayName}\n{claim.Brief}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Text
		};
		box.AddChild(claimLabel);

		if (MultiplayerListenSession.IsHosting)
		{
			var claimRow = new HBoxContainer();
			claimRow.AddThemeConstantOverride("separation", 8);
			box.AddChild(claimRow);
			var prev = MakeGhost("< Claim", () =>
			{
				_claimIndex = (_claimIndex - 1 + VoidCorpsIdentity.ClaimSites.Length) % VoidCorpsIdentity.ClaimSites.Length;
				Rebuild();
			});
			claimRow.AddChild(prev);
			var next = MakeGhost("Claim >", () =>
			{
				_claimIndex = (_claimIndex + 1) % VoidCorpsIdentity.ClaimSites.Length;
				Rebuild();
			});
			claimRow.AddChild(next);

			var info = MissionCatalog.Get(_missionType);
			var missionLabel = new Label
			{
				Text = $"MISSION: {info.Title}\n{info.Brief}",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Modulate = MechUiTheme.Muted
			};
			box.AddChild(missionLabel);

			var missionRow = new HBoxContainer();
			missionRow.AddThemeConstantOverride("separation", 8);
			box.AddChild(missionRow);
			missionRow.AddChild(MakeGhost("< Mission", () =>
			{
				CycleMission(-1);
				Rebuild();
			}));
			missionRow.AddChild(MakeGhost("Mission >", () =>
			{
				CycleMission(1);
				Rebuild();
			}));

			var diffLabel = new Label
			{
				Text = $"Difficulty: {_difficulty}",
				Modulate = MechUiTheme.Text
			};
			box.AddChild(diffLabel);
			var diffRow = new HBoxContainer();
			diffRow.AddThemeConstantOverride("separation", 6);
			box.AddChild(diffRow);
			foreach (PilotDifficulty d in System.Enum.GetValues(typeof(PilotDifficulty)))
			{
				var captured = d;
				var b = MakeGhost(d.ToString(), () =>
				{
					_difficulty = captured;
					Rebuild();
				});
				b.CustomMinimumSize = new Vector2(0, 32);
				if (d == _difficulty)
					MechUiTheme.StylePrimaryButton(b);
				diffRow.AddChild(b);
			}
		}

		return box;
	}

	private Control BuildOpsLog()
	{
		var panel = MechUiTheme.MakePanel("OpsLog", deep: true);
		panel.CustomMinimumSize = new Vector2(0, 120);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		panel.AddChild(col);
		col.AddChild(MechUiTheme.MakeSectionLabel("OPS LOG"));

		var logText = _log.Count == 0 ? "—" : string.Join("\n", _log);
		var log = new Label
		{
			Text = logText,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Muted,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		col.AddChild(log);
		return panel;
	}

	private Control BuildActionRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.Alignment = BoxContainer.AlignmentMode.End;

		row.AddChild(MakeGhost("BACK TO GAME TYPE", () =>
		{
			_stage = Stage.PickGameType;
			_gameType = LobbyGameType.None;
			_slots.Clear();
			_statusMessage = "";
			Rebuild();
		}));

		row.AddChild(MakeGhost("CANCEL", CancelSessionToForm));

		if (MultiplayerListenSession.IsHosting)
		{
			var start = MakePrimary("START GAME", OnStartPressed);
			row.AddChild(start);
		}
		else
		{
			var wait = new Label
			{
				Text = "Waiting for host to start…",
				Modulate = MechUiTheme.Muted,
				VerticalAlignment = VerticalAlignment.Center
			};
			row.AddChild(wait);
		}

		return row;
	}

	private Control BuildJoinInfoBar(bool compact = false)
	{
		var panel = MechUiTheme.MakePanel("JoinInfo", deep: compact);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);

		var label = new Label
		{
			Text = compact
				? $"Join  ·  {MultiplayerListenSession.JoinAddress}"
				: $"Session live  ·  Share this join address:\n{MultiplayerListenSession.JoinAddress}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = MechUiTheme.Cyan,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddChild(label);

		var copy = MakePrimary("COPY", () =>
		{
			var addr = MultiplayerListenSession.JoinAddress ?? "";
			DisplayServer.ClipboardSet(addr);
			PushLog($"Copied join address: {addr}");
			_statusMessage = "Join address copied.";
			Rebuild();
		});
		copy.CustomMinimumSize = new Vector2(110, 40);
		row.AddChild(copy);
		return panel;
	}

	private Control BuildSlotRow(int index, bool showTeam, bool allowComputer)
	{
		var slot = _slots[index];
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var swatch = new ColorRect
		{
			Color = slot.Swatch,
			CustomMinimumSize = new Vector2(18, 18)
		};
		row.AddChild(swatch);

		var name = new Label
		{
			Text = slot.Fill == LobbySlotFill.Open ? "Open"
				: slot.Fill == LobbySlotFill.Closed ? "Closed"
				: slot.Name,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Modulate = slot.Fill == LobbySlotFill.Human ? MechUiTheme.Text : MechUiTheme.Muted
		};
		row.AddChild(name);

		if (MultiplayerListenSession.IsHosting)
		{
			var cycle = MakeGhost(FillLabel(slot.Fill), () =>
			{
				CycleFill(index, allowComputer);
				Rebuild();
			});
			cycle.CustomMinimumSize = new Vector2(110, 30);
			row.AddChild(cycle);

			if (showTeam)
			{
				var team = MakeGhost(slot.Team == 0 ? "Alpha" : "Bravo", () =>
				{
					slot.Team = slot.Team == 0 ? 1 : 0;
					Rebuild();
				});
				team.CustomMinimumSize = new Vector2(80, 30);
				row.AddChild(team);
			}
		}
		else
		{
			var fill = new Label
			{
				Text = FillLabel(slot.Fill),
				Modulate = MechUiTheme.Muted,
				CustomMinimumSize = new Vector2(110, 0)
			};
			row.AddChild(fill);
		}

		return row;
	}

	private static Label MakeGroupHeader(string text)
	{
		var label = new Label
		{
			Text = text,
			Modulate = MechUiTheme.Accent
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		return label;
	}

	private void TryHost()
	{
		var err = MultiplayerListenSession.Host(GetTree());
		if (err != Error.Ok)
		{
			_statusMessage = $"Host failed ({err}). Port may be in use — try again or close the other session.";
			PushLog($"Host failed: {err}");
			Rebuild();
			return;
		}

		_statusMessage = "";
		_stage = Stage.PickGameType;
		SeedHostSlot();
		PushLog($"Hosting on {MultiplayerListenSession.JoinAddress}");
		Rebuild();
	}

	private void TryJoin(string address)
	{
		_joinField = address;
		var err = MultiplayerListenSession.Join(GetTree(), address);
		if (err != Error.Ok)
		{
			_statusMessage = err == Error.InvalidParameter
				? "Enter a valid address as ip:port."
				: $"Join failed ({err}).";
			PushLog($"Join failed: {err}");
			Rebuild();
			return;
		}

		_statusMessage = "";
		_stage = Stage.PickGameType;
		PushLog($"Joining {MultiplayerListenSession.JoinAddress}…");
		Rebuild();
	}

	private void EnterModeLobby(LobbyGameType type)
	{
		_gameType = type;
		_stage = Stage.ModeLobby;
		_statusMessage = "";
		if (type == LobbyGameType.CoopCampaign)
			RebuildCoopSlots();
		else
			RebuildSkirmishSlots();
		PushLog(type == LobbyGameType.CoopCampaign
			? "Game type: Co-op campaign"
			: $"Game type: Skirmish ({_skirmishTeamSize}v{_skirmishTeamSize})");
		Rebuild();
	}

	private void SeedHostSlot()
	{
		_slots.Clear();
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var name = session?.Profile.MercCorpName ?? "Host";
		if (string.IsNullOrWhiteSpace(name))
			name = "Host";
		// Placeholder until mode lobby rebuilds slots for the chosen type.
		_slots.Add(new SlotRow
		{
			Fill = LobbySlotFill.Human,
			Name = $"{name} (Host)",
			Team = 0,
			Swatch = Swatches[0]
		});
	}

	private void RebuildCoopSlots()
	{
		var hostName = HostDisplayName();
		_slots.Clear();
		for (var i = 0; i < 4; i++)
		{
			_slots.Add(new SlotRow
			{
				Fill = i == 0 ? LobbySlotFill.Human : LobbySlotFill.Open,
				Name = i == 0 ? $"{hostName} (Host)" : "Open",
				Team = 0,
				Swatch = Swatches[i % Swatches.Length]
			});
		}
	}

	private void RebuildSkirmishSlots()
	{
		var hostName = HostDisplayName();
		_slots.Clear();
		var per = _skirmishTeamSize;
		for (var team = 0; team < 2; team++)
		{
			for (var i = 0; i < per; i++)
			{
				var index = _slots.Count;
				var isHost = index == 0;
				_slots.Add(new SlotRow
				{
					Fill = isHost ? LobbySlotFill.Human : LobbySlotFill.Open,
					Name = isHost ? $"{hostName} (Host)" : "Open",
					Team = team,
					Swatch = Swatches[index % Swatches.Length]
				});
			}
		}
	}

	private string HostDisplayName()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var name = session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename;
		return string.IsNullOrWhiteSpace(name) ? "Host" : name;
	}

	private void CycleFill(int index, bool allowComputer)
	{
		var slot = _slots[index];
		if (index == 0)
			return; // host seat stays human

		slot.Fill = slot.Fill switch
		{
			LobbySlotFill.Open => allowComputer ? LobbySlotFill.Computer : LobbySlotFill.Closed,
			LobbySlotFill.Computer => LobbySlotFill.Closed,
			LobbySlotFill.Closed => LobbySlotFill.Open,
			_ => LobbySlotFill.Open
		};
		slot.Name = slot.Fill switch
		{
			LobbySlotFill.Computer => "Computer",
			LobbySlotFill.Closed => "Closed",
			_ => "Open"
		};
	}

	private static string FillLabel(LobbySlotFill fill) => fill switch
	{
		LobbySlotFill.Human => "Human",
		LobbySlotFill.Computer => "Computer",
		LobbySlotFill.Closed => "Closed",
		_ => "Open"
	};

	private void CycleMission(int delta)
	{
		var values = (MissionType[])System.Enum.GetValues(typeof(MissionType));
		var idx = System.Array.IndexOf(values, _missionType);
		do
		{
			idx = (idx + delta + values.Length) % values.Length;
			_missionType = values[idx];
		} while (_missionType == MissionType.BossEncounter);
	}

	private void OnStartPressed()
	{
		var humans = 0;
		foreach (var s in _slots)
		{
			if (s.Fill == LobbySlotFill.Human)
				humans++;
		}

		PushLog("START requested — arena multiplayer sync is not wired yet.");
		PushLog($"Would launch {_gameType} with {humans} human pilot(s), {_slots.Count} slots.");
		_statusMessage = "Lobby ready. Combat sync comes next — Start is UI-complete only for now.";
		Rebuild();
	}

	private void CancelSessionToForm()
	{
		MultiplayerListenSession.Shutdown(GetTree());
		_stage = Stage.FormSession;
		_gameType = LobbyGameType.None;
		_slots.Clear();
		_log.Clear();
		_statusMessage = "Session closed.";
		Rebuild();
	}

	private void LeaveToMenu()
	{
		MultiplayerListenSession.Shutdown(GetTree());
		GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
	}

	private void PushLog(string line)
	{
		_log.Add(line);
		while (_log.Count > 8)
			_log.RemoveAt(0);
	}

	private static Button MakePrimary(string text, System.Action onPress)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 44),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		button.AddThemeFontSizeOverride("font_size", 16);
		MechUiTheme.StylePrimaryButton(button);
		button.Pressed += () =>
		{
			SfxService.Confirm();
			onPress();
		};
		return button;
	}

	private static Button MakeGhost(string text, System.Action onPress)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 40)
		};
		button.AddThemeFontSizeOverride("font_size", 14);
		MechUiTheme.StyleGhostButton(button);
		button.Pressed += () =>
		{
			SfxService.Click();
			onPress();
		};
		return button;
	}
}
