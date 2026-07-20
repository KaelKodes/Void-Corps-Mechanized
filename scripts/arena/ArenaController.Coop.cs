using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Co-op wing spawn, ready gate, and host-auth match phase sync.</summary>
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

		var peers = net.GetOrderedPeerIds();
		var right = Vector3.Right;
		for (var i = 0; i < peers.Count && i < NetSession.MaxCoopPlayers; i++)
		{
			var peerId = peers[i];
			MechController mech;
			if (i == 0)
			{
				mech = _mech!;
				mech.Name = $"Wing_{peerId}";
			}
			else
			{
				var existing = GetNodeOrNull<MechController>($"Wing_{peerId}");
				if (existing != null)
				{
					mech = existing;
				}
				else
				{
					var packed = GD.Load<PackedScene>("res://scenes/mech.tscn");
					mech = packed.Instantiate<MechController>();
					mech.Name = $"Wing_{peerId}";
					mech.IsPlayerControlled = false;
					mech.OwningPeerId = peerId;
					AddChild(mech);
				}
			}

			var offset = right * ((i - (peers.Count - 1) * 0.5f) * 4.5f);
			mech.ConfigureNetworkPilot(peerId, human: true);
			mech.GlobalPosition = _playerSpawn + offset;
			FaceToward(mech, Vector3.Zero);
			_wingByPeer[peerId] = mech;

			if (peerId == net.LocalPeerId)
			{
				_mech = mech;
				var cam = GetNodeOrNull<TopDownCamera>("Camera3D");
				cam?.SetTarget(mech);
			}
		}

		EnsureCoopStatusLabel();
		RefreshCoopStatus();
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
		_coopStatus.Text =
			$"DETACHMENT  {_wingReady.Count}/{_wingByPeer.Count} READY  ·  " +
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

		if (Multiplayer.IsServer() && _wingReady.Count >= _wingByPeer.Count && _wingByPeer.Count > 0)
			HostBeginCoopCountdown();
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
			mech.RebuildFromLoadout(loadout, BuildProfileConditions(
				GetNodeOrNull<GameSession>("/root/GameSession")?.Profile));
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
			BeginDropIn(mech, mech.GlobalPosition with { Y = 0f }, enableAiWhenDone: false, createBeacon: false, fallTime, warningSeconds: 0.05f);
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
				HookIntegrityTelemetry(mech.Integrity, playerOwned: true);
			}
		}

		_playerDeathHooked = true;
	}
}
