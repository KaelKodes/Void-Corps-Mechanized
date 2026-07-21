using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Co-op / multiplayer wing spawn, ready gate, and host-auth match phase sync.</summary>
public partial class ArenaController
{
	private readonly Dictionary<int, MechController> _wingByPeer = new();
	private readonly Dictionary<int, LoadoutData> _wingLoadouts = new();
	private readonly HashSet<int> _wingReady = new();
	private Label? _coopStatus;

	private bool IsCoopMatch
	{
		get
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			var net = GetNodeOrNull<NetSession>("/root/NetSession");
			return session is { CoopMatch: true } || net is { IsOnline: true };
		}
	}

	private bool IsPvpMatch =>
		GetNodeOrNull<GameSession>("/root/GameSession")?.IsPvpMatch == true;

	private void SetupCoopWings()
	{
		_wingByPeer.Clear();
		_wingLoadouts.Clear();
		_wingReady.Clear();

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (net is not { IsOnline: true } || session == null)
		{
			if (_mech != null)
			{
				_mech.OwningPeerId = 0;
				_mech.IsPlayerControlled = true;
				_wingByPeer[1] = _mech;
			}
			return;
		}

		var roster = GetEffectiveRoster(session, net);
		var firstHuman = true;
		var packed = GD.Load<PackedScene>("res://scenes/mech.tscn");

		for (var i = 0; i < roster.Count; i++)
		{
			var slot = roster[i];
			if (!slot.IsOccupied)
				continue;

			var owningId = slot.OwningId;
			var human = slot.IsHuman;
			var team = LobbyModeRules.IsCoop(session.MultiplayerGameMode)
				? TeamId.Player
				: LobbyModeRules.ToCombatTeam(slot);

			MechController mech;
			if (human && firstHuman)
			{
				mech = _mech!;
				mech.Name = $"Wing_{owningId}";
				firstHuman = false;
			}
			else
			{
				var existing = GetNodeOrNull<MechController>($"Wing_{owningId}");
				if (existing != null)
				{
					mech = existing;
				}
				else
				{
					mech = packed.Instantiate<MechController>();
					mech.Name = $"Wing_{owningId}";
					AddChild(mech);
				}
			}

			mech.ConfigureNetworkPilot(owningId, human, team);
			if (!human)
				AttachBotPilot(mech, session.PendingDifficulty);

			_wingByPeer[owningId] = mech;

			if (human && owningId == net.LocalPeerId)
			{
				_mech = mech;
				var cam = GetNodeOrNull<TopDownCamera>("Camera3D");
				cam?.SetTarget(mech);
			}
		}

		// Ensure local peer has a mech even if roster was empty somehow.
		if (_mech != null && !_wingByPeer.ContainsValue(_mech))
		{
			_mech.ConfigureNetworkPilot(net.LocalPeerId, true, TeamId.Player);
			_wingByPeer[net.LocalPeerId] = _mech;
		}

		EnsureCoopStatusLabel();
		RefreshCoopStatus();
	}

	private static List<LobbySlot> GetEffectiveRoster(GameSession session, NetSession net)
	{
		if (session.LobbyRoster.Count > 0)
			return session.LobbyRoster;

		var list = new List<LobbySlot>();
		foreach (var slot in net.Slots)
		{
			if (slot.IsOccupied)
				list.Add(slot.Clone());
		}

		if (list.Count > 0)
			return list;

		foreach (var peerId in net.GetOrderedPeerIds())
		{
			list.Add(new LobbySlot
			{
				Kind = LobbySlotKind.Human,
				PeerId = peerId,
				DisplayName = net.PeerDisplayName(peerId),
				Team = LobbyTeam.None
			});
		}

		return list;
	}

	private void AttachBotPilot(MechController mech, PilotDifficulty difficulty)
	{
		var existing = mech.GetNodeOrNull<MechPilotAI>("MechPilotAI");
		if (existing != null)
		{
			existing.ApplyDifficulty(difficulty);
			return;
		}

		var ai = new MechPilotAI
		{
			Name = "MechPilotAI",
			Difficulty = difficulty
		};
		mech.AddChild(ai);
		ai.ApplyDifficulty(difficulty);
	}

	private void EnsureCoopStatusLabel()
	{
		var ui = GetNodeOrNull("UI");
		if (ui == null)
			return;
		_coopStatus = ui.GetNodeOrNull<Label>("CoopStatus");
		if (_coopStatus != null)
			return;
		_coopStatus = new Label
		{
			Name = "CoopStatus",
			Position = new Vector2(24, 140),
			Size = new Vector2(700, 40),
			Modulate = new Color(0.7f, 0.85f, 0.95f)
		};
		_coopStatus.AddThemeFontSizeOverride("font_size", 14);
		ui.AddChild(_coopStatus);
	}

	private void RefreshCoopStatus()
	{
		if (_coopStatus == null)
			return;
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is not { IsOnline: true })
		{
			_coopStatus.Visible = false;
			return;
		}

		_coopStatus.Visible = true;
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var modeLabel = session != null
			? LobbyModeRules.ModeLabel(session.MultiplayerGameMode)
			: "Co-op";
		_coopStatus.Text =
			$"{modeLabel}  {_wingReady.Count}/{_wingByPeer.Count} READY  ·  " +
			$"you are peer {net.LocalPeerId}" +
			(Multiplayer.IsServer() ? " (HOST AUTH)" : " (CLIENT)");
	}

	private void PlaceWingMechsAtSpawn()
	{
		if (_wingByPeer.Count == 0)
		{
			if (_mech != null)
			{
				_mech.GlobalPosition = _playerSpawn;
				FaceToward(_mech, Vector3.Zero);
			}
			return;
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var mode = session?.MultiplayerGameMode ?? MultiplayerGameMode.CoopRogueLike;

		if (mode == MultiplayerGameMode.TeamSkirmish)
		{
			PlaceTeamSpawns();
			return;
		}

		if (mode == MultiplayerGameMode.FfaSkirmish)
		{
			PlaceFfaSpawns();
			return;
		}

		var right = Vector3.Right;
		var i = 0;
		var count = _wingByPeer.Count;
		foreach (var (_, mech) in _wingByPeer)
		{
			var offset = right * ((i - (count - 1) * 0.5f) * 4.5f);
			mech.Team = TeamId.Player;
			mech.Visible = true;
			mech.GlobalPosition = _playerSpawn + offset;
			FaceToward(mech, Vector3.Zero);
			i++;
		}
	}

	private void PlaceTeamSpawns()
	{
		var alpha = new List<MechController>();
		var bravo = new List<MechController>();
		foreach (var (_, mech) in _wingByPeer)
		{
			if (mech.Team == TeamId.Bravo)
				bravo.Add(mech);
			else
				alpha.Add(mech);
		}

		PlaceLine(alpha, _playerSpawn, Vector3.Right, faceToward: _enemySpawnA);
		PlaceLine(bravo, _enemySpawnA, Vector3.Right, faceToward: _playerSpawn);
	}

	private void PlaceFfaSpawns()
	{
		var mechs = new List<MechController>(_wingByPeer.Values);
		var count = Math.Max(1, mechs.Count);
		var radius = 28f;
		for (var i = 0; i < mechs.Count; i++)
		{
			var angle = i * Mathf.Tau / count;
			var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			var mech = mechs[i];
			mech.Visible = true;
			mech.GlobalPosition = pos;
			FaceToward(mech, Vector3.Zero);
		}
	}

	private void PlaceLine(List<MechController> mechs, Vector3 origin, Vector3 right, Vector3 faceToward)
	{
		for (var i = 0; i < mechs.Count; i++)
		{
			var offset = right * ((i - (mechs.Count - 1) * 0.5f) * 4.5f);
			var mech = mechs[i];
			mech.Visible = true;
			mech.GlobalPosition = origin + offset;
			FaceToward(mech, faceToward);
		}
	}

	private void OnCoopReadyPressed(LoadoutData loadout)
	{
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (net == null || session == null)
			return;

		session.SetLoadout(loadout);
		var peerId = net.LocalPeerId;
		_wingLoadouts[peerId] = loadout.Clone();
		_wingReady.Add(peerId);
		RefreshCoopStatus();

		Rpc(MethodName.RpcWingReady, peerId, loadout.ToDict());

		if (Multiplayer.IsServer())
		{
			AutoReadyBots(loadout);
			if (_wingReady.Count >= _wingByPeer.Count && _wingByPeer.Count > 0)
				HostBeginCoopCountdown();
		}
	}

	private void AutoReadyBots(LoadoutData template)
	{
		foreach (var (id, _) in _wingByPeer)
		{
			if (id >= 0)
				continue;
			if (_wingReady.Contains(id))
				continue;
			var botLoadout = template.Clone();
			_wingLoadouts[id] = botLoadout;
			_wingReady.Add(id);
			Rpc(MethodName.RpcWingReady, id, botLoadout.ToDict());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcWingReady(int peerId, Godot.Collections.Dictionary loadoutDict)
	{
		_wingLoadouts[peerId] = LoadoutData.FromDict(loadoutDict);
		_wingReady.Add(peerId);
		RefreshCoopStatus();

		if (Multiplayer.IsServer() && _wingReady.Count >= _wingByPeer.Count && _wingByPeer.Count > 0
		    && _phase == MatchPhase.Prep)
			HostBeginCoopCountdown();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcApplyWingLoadout(int peerId, Godot.Collections.Dictionary loadoutDict)
	{
		// Mid-fight full rebuilds are no longer allowed (field logistics only).
		if (_phase != MatchPhase.Prep)
			return;
		var sender = Multiplayer.GetRemoteSenderId();
		if (sender != 0 && sender != peerId && !Multiplayer.IsServer())
			return;
		var loadout = LoadoutData.FromDict(loadoutDict);
		_wingLoadouts[peerId] = loadout;
		if (_wingByPeer.TryGetValue(peerId, out var mech))
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			if (session is { UsingTemporaryLoaner: true })
				mech.RebuildFromLoadout(loadout, forceFullRepair: true);
			else
				mech.RebuildFromLoadout(loadout, BuildProfileConditions(session?.Profile));
		}
	}

	private void HostBeginCoopCountdown()
	{
		if (!Multiplayer.IsServer() || _phase != MatchPhase.Prep)
			return;

		var bag = new Godot.Collections.Dictionary();
		foreach (var (peerId, loadout) in _wingLoadouts)
			bag[peerId.ToString()] = loadout.ToDict();

		Rpc(MethodName.RpcBeginCoopCountdown, bag);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcBeginCoopCountdown(Godot.Collections.Dictionary loadoutBag)
	{
		foreach (var key in loadoutBag.Keys)
		{
			var peerId = key.AsString().ToInt();
			var dict = loadoutBag[key].AsGodotDictionary();
			var loadout = LoadoutData.FromDict(dict);
			_wingLoadouts[peerId] = loadout;
			if (_wingByPeer.TryGetValue(peerId, out var mech))
				mech.RebuildFromLoadout(loadout);
		}

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var localId = net?.LocalPeerId ?? 1;
		if (_wingLoadouts.TryGetValue(localId, out var localLoadout))
		{
			var session = GetNodeOrNull<GameSession>("/root/GameSession");
			session?.SetLoadout(localLoadout);
		}

		if (_garage != null)
		{
			_garage.Visible = false;
			_garage.ConfigurePrepMode(false);
		}

		SetCombatHudVisible(false);
		_enemyResolved.Clear();
		_objectivesComplete = false;
		PlaceCombatants();
		StagePlayerDropHold();
		SetCombatActive(false);

		_phase = MatchPhase.Countdown;
		_countdownRemaining = 5.25f;
		_lastCountdownSecond = -1;
		_playerDropStarted = false;
		MusicService.Cue(MusicCue.Combat);
		if (_countdownLabel != null)
		{
			_countdownLabel.Visible = true;
			_countdownLabel.Text = "5";
		}
	}

	private void DropAllWings(float fallTime)
	{
		foreach (var (_, mech) in _wingByPeer)
			BeginDropIn(mech, mech.GlobalPosition with { Y = 0f }, enableAiWhenDone: mech.GetNodeOrNull<MechPilotAI>("MechPilotAI") != null, createBeacon: false, fallTime, warningSeconds: 0.05f);
	}

	private void HookAllWingDeaths()
	{
		foreach (var (_, mech) in _wingByPeer)
		{
			if (mech.Health != null)
			{
				mech.Health.Died -= OnPlayerDown;
				mech.Health.Died += OnPlayerDown;
			}

			if (mech.Integrity != null)
			{
				mech.Integrity.MechCollapsed -= OnPlayerDown;
				mech.Integrity.MechCollapsed += OnPlayerDown;
				HookIntegrityTelemetry(mech.Integrity, playerOwned: mech.Team is TeamId.Player or TeamId.Alpha);
			}
		}

		_playerDeathHooked = true;
	}

	/// <summary>Host-only: last team / last pilot standing for Team & FFA skirmish.</summary>
	private bool TryResolvePvpElimination()
	{
		if (!IsPvpMatch || _matchResolved || _phase != MatchPhase.Fighting)
			return false;
		if (!Multiplayer.IsServer() && GetNodeOrNull<NetSession>("/root/NetSession") is { IsOnline: true })
			return false;

		var alive = new List<MechController>();
		foreach (var mech in _wingByPeer.Values)
		{
			if (mech.Health?.IsDead == true || mech.Integrity?.IsCollapsed == true)
				continue;
			if (!mech.Visible)
				continue;
			alive.Add(mech);
		}

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var mode = session?.MultiplayerGameMode ?? MultiplayerGameMode.FfaSkirmish;

		if (mode == MultiplayerGameMode.FfaSkirmish)
		{
			if (alive.Count > 1)
				return false;
			var winnerId = alive.Count == 1 ? alive[0].OwningPeerId : 0;
			_netCombat?.HostShowPvpResults(winnerId, (int)TeamId.Neutral);
			if (_netCombat == null)
				ClientPresentPvpResults(winnerId, TeamId.Neutral);
			return true;
		}

		// Team skirmish
		var alphaAlive = 0;
		var bravoAlive = 0;
		foreach (var mech in alive)
		{
			if (mech.Team == TeamId.Bravo)
				bravoAlive++;
			else
				alphaAlive++;
		}

		if (alphaAlive > 0 && bravoAlive > 0)
			return false;

		var winTeam = alphaAlive > 0 ? TeamId.Alpha : TeamId.Bravo;
		if (alphaAlive == 0 && bravoAlive == 0)
			winTeam = TeamId.Neutral;
		_netCombat?.HostShowPvpResults(0, (int)winTeam);
		if (_netCombat == null)
			ClientPresentPvpResults(0, winTeam);
		return true;
	}

	public void ClientPresentPvpResults(int winnerOwningId, TeamId winningTeam)
	{
		if (_matchResolved)
			return;

		_matchResolved = true;
		_phase = MatchPhase.Fighting;
		EscortMission.ClearFieldInteractReservation();
		SetCombatActive(false);
		if (_garage != null)
			_garage.Visible = false;
		SetCombatHudVisible(false);

		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var localId = net?.LocalPeerId ?? 1;
		var mode = session?.MultiplayerGameMode ?? MultiplayerGameMode.FfaSkirmish;

		bool won;
		if (mode == MultiplayerGameMode.FfaSkirmish)
			won = winnerOwningId != 0 && (_mech?.OwningPeerId == winnerOwningId || winnerOwningId == localId);
		else if (winningTeam == TeamId.Neutral)
			won = false;
		else
			won = _mech != null && _mech.Team == winningTeam;

		var outcome = won ? MatchOutcome.Victory : MatchOutcome.Defeat;
		if (session != null)
		{
			if (_mech != null)
				session.Match.CaptureFinalCondition(_mech.CapturePartConditions());
			session.Match.End(outcome);
		}

		SfxService.Play(won ? "victory" : "defeat", 1f, -2f);
		if (session != null)
			OpenPostMissionFlow(session);
	}
}
