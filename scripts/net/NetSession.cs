using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Mechanize;

/// <summary>
/// Listen-server multiplayer session. Host is match authority; guests connect directly.
/// Two-phase lobby: Entry (mode + humans) then MatchSetup (ready / bots / start).
/// </summary>
public partial class NetSession : Node
{
	public const int DefaultPort = 7770;
	public const int MaxCoopPlayers = 4;
	public const int MaxSkirmishPlayers = 20; // aspirational 10v10
	public const int MaxChatLines = 40;
	public const int MaxChatMessageLength = 180;

	public enum NetMode
	{
		Offline,
		Hosting,
		Client
	}

	/// <summary>Legacy launch intent; prefer <see cref="GameMode"/>.</summary>
	public enum LobbyIntent
	{
		None,
		CoopSkirmish,
		CoopCampaign
	}

	public NetMode Mode { get; private set; } = NetMode.Offline;
	public LobbyIntent Intent { get; set; } = LobbyIntent.None;
	public LobbyPhase LobbyPhase { get; private set; } = LobbyPhase.Offline;
	public MultiplayerGameMode GameMode { get; private set; } = MultiplayerGameMode.CoopRogueLike;
	public string StatusMessage { get; private set; } = "Offline";
	public int Port { get; private set; } = DefaultPort;

	private readonly Dictionary<int, bool> _ready = new();
	private readonly Dictionary<int, string> _displayNames = new();
	private readonly List<LobbySlot> _slots = new();
	private readonly List<(string Speaker, string Text)> _chat = new();
	private int _nextBotId = LobbyModeRules.BotIdBase;
	private LobbyTeam _preferredBotTeam = LobbyTeam.Alpha;

	public bool IsOnline => Mode != NetMode.Offline;
	public bool IsHost => Mode is NetMode.Hosting or NetMode.Offline;
	public bool IsServerPeer => !IsOnline || Multiplayer.IsServer();
	public int LocalPeerId => IsOnline ? Multiplayer.GetUniqueId() : 1;
	public int PeerCount => IsOnline ? 1 + Multiplayer.GetPeers().Length : 1;
	public IReadOnlyList<LobbySlot> Slots => _slots;
	public IReadOnlyList<(string Speaker, string Text)> ChatLines => _chat;

	public event Action? RosterChanged;
	public event Action? LobbyStateChanged;
	public event Action? Disconnected;
	public event Action? MatchLaunchReceived;
	public event Action<(string Speaker, string Text)>? ChatReceived;

	/// <summary>Payload for guests when host starts a match.</summary>
	public Godot.Collections.Dictionary? PendingLaunch { get; private set; }

	public override void _Ready()
	{
		Name = "NetSession";
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		RebuildEmptySlots();
	}

	public IReadOnlyList<int> GetOrderedPeerIds()
	{
		var list = new List<int>();
		if (!IsOnline)
		{
			list.Add(1);
			return list;
		}

		list.Add(1); // host always 1 on ENet listen server
		foreach (var id in Multiplayer.GetPeers())
		{
			if (id != 1)
				list.Add(id);
		}

		list.Sort();
		return list;
	}

	public bool IsPeerReady(int peerId) => _ready.GetValueOrDefault(peerId);

	public string PeerDisplayName(int peerId)
	{
		if (_displayNames.TryGetValue(peerId, out var name) && !string.IsNullOrEmpty(name))
			return name;
		return peerId == 1 ? "Host" : $"Pilot {peerId}";
	}

	public bool AllPeersReady()
	{
		foreach (var id in GetOrderedPeerIds())
		{
			if (!_ready.GetValueOrDefault(id))
				return false;
		}

		return true;
	}

	public bool AllSlotsReady()
	{
		var any = false;
		foreach (var slot in _slots)
		{
			if (!slot.IsOccupied)
				continue;
			any = true;
			if (!slot.Ready)
				return false;
		}

		return any;
	}

	public int OccupiedSlotCount()
	{
		var n = 0;
		foreach (var slot in _slots)
		{
			if (slot.IsOccupied)
				n++;
		}

		return n;
	}

	public Error Host(int port = DefaultPort, int maxPeers = -1)
	{
		ShutdownPeer();
		Port = port;
		if (maxPeers <= 0)
			maxPeers = LobbyModeRules.MaxHumanPeers(GameMode);
		maxPeers = Math.Clamp(maxPeers, 2, LobbyModeRules.MaxHumans);

		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateServer(port, maxPeers);
		if (err != Error.Ok)
		{
			StatusMessage = $"Host failed: {err}";
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Hosting;
		LobbyPhase = LobbyPhase.Entry;
		StatusMessage = $"Hosting on port {port}";
		_ready.Clear();
		_displayNames.Clear();
		_chat.Clear();
		_nextBotId = LobbyModeRules.BotIdBase;
		RebuildEmptySlots();
		RegisterLocalIdentity();
		SeatPeer(LocalPeerId, PeerDisplayName(LocalPeerId));
		BroadcastFullLobbyState();
		RosterChanged?.Invoke();
		LobbyStateChanged?.Invoke();
		return Error.Ok;
	}

	public Error Join(string address, int port = DefaultPort)
	{
		ShutdownPeer();
		Port = port;
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateClient(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), port);
		if (err != Error.Ok)
		{
			StatusMessage = $"Join failed: {err}";
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Client;
		LobbyPhase = LobbyPhase.Entry;
		StatusMessage = $"Connecting to {address}:{port}…";
		_ready.Clear();
		_displayNames.Clear();
		_chat.Clear();
		RebuildEmptySlots();
		LobbyStateChanged?.Invoke();
		return Error.Ok;
	}

	public void DisconnectSession()
	{
		ShutdownPeer();
		Mode = NetMode.Offline;
		Intent = LobbyIntent.None;
		LobbyPhase = LobbyPhase.Offline;
		PendingLaunch = null;
		_ready.Clear();
		_displayNames.Clear();
		_chat.Clear();
		_nextBotId = LobbyModeRules.BotIdBase;
		RebuildEmptySlots();
		StatusMessage = "Offline";
		RosterChanged?.Invoke();
		LobbyStateChanged?.Invoke();
		Disconnected?.Invoke();
	}

	public void HostSetGameMode(MultiplayerGameMode mode)
	{
		if (Mode == NetMode.Client)
			return;
		if (LobbyPhase == LobbyPhase.MatchSetup)
			return;
		GameMode = mode;
		RebuildEmptySlotsPreservingHumans();
		SyncStatusPeers();
		if (Mode == NetMode.Hosting)
			BroadcastFullLobbyState();
		LobbyStateChanged?.Invoke();
		RosterChanged?.Invoke();
	}

	/// <summary>Host advances Entry → MatchSetup after humans + mode are set.</summary>
	public bool HostAdvanceToMatchSetup()
	{
		if (Mode != NetMode.Hosting || LobbyPhase != LobbyPhase.Entry)
			return false;
		if (OccupiedSlotCount() < 1)
			return false;

		LobbyPhase = LobbyPhase.MatchSetup;
		ClearHumanReadyFlags();
		ClearBots();
		AssignDefaultTeams();
		BroadcastFullLobbyState();
		LobbyStateChanged?.Invoke();
		RosterChanged?.Invoke();
		return true;
	}

	public bool HostReturnToEntry()
	{
		if (Mode != NetMode.Hosting || LobbyPhase != LobbyPhase.MatchSetup)
			return false;
		LobbyPhase = LobbyPhase.Entry;
		ClearBots();
		ClearHumanReadyFlags();
		BroadcastFullLobbyState();
		LobbyStateChanged?.Invoke();
		RosterChanged?.Invoke();
		return true;
	}

	public void SetLocalReady(bool ready)
	{
		if (!IsOnline)
			return;
		var id = LocalPeerId;
		_ready[id] = ready;
		ApplyHumanReady(id, ready);
		Rpc(MethodName.RpcSetReady, id, ready);
		if (Mode == NetMode.Hosting && LobbyPhase == LobbyPhase.MatchSetup)
			SyncBotReadyWithHost();
		BroadcastSlotsIfHost();
		RosterChanged?.Invoke();
	}

	public void ToggleLocalReady()
	{
		if (!IsOnline)
			return;
		SetLocalReady(!IsPeerReady(LocalPeerId));
	}

	public void SetLocalDisplayName(string name)
	{
		var id = LocalPeerId;
		_displayNames[id] = name;
		UpdateSlotName(id, name);
		if (IsOnline)
			Rpc(MethodName.RpcSetDisplayName, id, name);
		BroadcastSlotsIfHost();
		RosterChanged?.Invoke();
	}

	public bool HostAddBot(LobbyTeam preferredTeam = LobbyTeam.None)
	{
		if (Mode != NetMode.Hosting || LobbyPhase != LobbyPhase.MatchSetup)
			return false;
		if (!LobbyModeRules.SupportsBots(GameMode))
			return false;

		var empty = FindEmptySlotIndex();
		if (empty < 0)
			return false;

		if (preferredTeam != LobbyTeam.None)
			_preferredBotTeam = preferredTeam;

		var botId = _nextBotId--;
		var slot = _slots[empty];
		slot.Kind = LobbySlotKind.Bot;
		slot.BotId = botId;
		slot.PeerId = 0;
		slot.DisplayName = $"Bot {-botId}";
		slot.Ready = IsPeerReady(1);
		AssignTeamForNewSlot(slot, _preferredBotTeam);
		BroadcastFullLobbyState();
		RosterChanged?.Invoke();
		LobbyStateChanged?.Invoke();
		return true;
	}

	public bool HostRemoveBot(int botId)
	{
		if (Mode != NetMode.Hosting)
			return false;
		for (var i = 0; i < _slots.Count; i++)
		{
			if (_slots[i].Kind != LobbySlotKind.Bot || _slots[i].BotId != botId)
				continue;
			_slots[i] = new LobbySlot();
			AssignDefaultTeamToEmptyAware(i);
			BroadcastFullLobbyState();
			RosterChanged?.Invoke();
			return true;
		}

		return false;
	}

	public bool HostSetSlotTeam(int slotIndex, LobbyTeam team)
	{
		if (Mode != NetMode.Hosting || LobbyPhase != LobbyPhase.MatchSetup)
			return false;
		if (slotIndex < 0 || slotIndex >= _slots.Count)
			return false;
		if (!_slots[slotIndex].IsOccupied)
			return false;
		if (GameMode != MultiplayerGameMode.TeamSkirmish)
			return false;
		if (team is not (LobbyTeam.Alpha or LobbyTeam.Bravo))
			return false;

		_slots[slotIndex].Team = team;
		BroadcastFullLobbyState();
		RosterChanged?.Invoke();
		return true;
	}

	public void SendChat(string message)
	{
		if (!IsOnline)
			return;
		message = SanitizeChat(message);
		if (string.IsNullOrEmpty(message))
			return;
		var speaker = PeerDisplayName(LocalPeerId);
		Rpc(MethodName.RpcChat, speaker, message);
	}

	/// <summary>Host pushes match launch to all peers (including self via CallLocal).</summary>
	public void HostLaunchMatch(Godot.Collections.Dictionary payload)
	{
		if (Mode == NetMode.Client)
			return;
		if (IsOnline)
			Rpc(MethodName.RpcMatchLaunch, payload);
		else
			ApplyMatchLaunch(payload);
	}

	public bool CanHostStartMatch() =>
		Mode == NetMode.Hosting
		&& LobbyPhase == LobbyPhase.MatchSetup
		&& AllSlotsReady()
		&& OccupiedSlotCount() > 0;

	public Godot.Collections.Array BuildRosterPayload()
	{
		var arr = new Godot.Collections.Array();
		foreach (var slot in _slots)
		{
			if (slot.IsOccupied)
				arr.Add(slot.ToDict());
		}

		return arr;
	}

	public void ApplyRosterFromPayload(Godot.Collections.Array arr)
	{
		_slots.Clear();
		foreach (var item in arr)
		{
			if (item.VariantType == Variant.Type.Dictionary)
				_slots.Add(LobbySlot.FromDict(item.AsGodotDictionary()));
		}
	}

	public string InviteSummary() => $"Port {Port}  ·  {LobbyModeRules.ModeLabel(GameMode)}";

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcSetReady(int peerId, bool ready)
	{
		_ready[peerId] = ready;
		ApplyHumanReady(peerId, ready);
		if (Mode == NetMode.Hosting && LobbyPhase == LobbyPhase.MatchSetup && peerId == 1)
			SyncBotReadyWithHost();
		if (Mode == NetMode.Hosting)
			BroadcastSlotsOnly();
		RosterChanged?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcSetDisplayName(int peerId, string name)
	{
		_displayNames[peerId] = name;
		UpdateSlotName(peerId, name);
		if (Mode == NetMode.Hosting)
			BroadcastSlotsOnly();
		RosterChanged?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcMatchLaunch(Godot.Collections.Dictionary payload)
	{
		ApplyMatchLaunch(payload);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcChat(string speaker, string message)
	{
		message = SanitizeChat(message);
		if (string.IsNullOrEmpty(message))
			return;
		_chat.Add((speaker, message));
		while (_chat.Count > MaxChatLines)
			_chat.RemoveAt(0);
		ChatReceived?.Invoke((speaker, message));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcLobbyState(int phase, int gameMode, Godot.Collections.Array slots)
	{
		LobbyPhase = (LobbyPhase)phase;
		GameMode = (MultiplayerGameMode)gameMode;
		_slots.Clear();
		foreach (var item in slots)
		{
			if (item.VariantType == Variant.Type.Dictionary)
				_slots.Add(LobbySlot.FromDict(item.AsGodotDictionary()));
		}

		EnsureSlotCapacity();
		SyncReadyFromSlots();
		LobbyStateChanged?.Invoke();
		RosterChanged?.Invoke();
	}

	private void ApplyMatchLaunch(Godot.Collections.Dictionary payload)
	{
		PendingLaunch = payload;
		MatchLaunchReceived?.Invoke();
	}

	public void ClearPendingLaunch() => PendingLaunch = null;

	private void RegisterLocalIdentity()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var name = session?.Profile.ResolveAccountHandle() ?? VoidCorpsIdentity.PlayerCorpCodename;
		_displayNames[LocalPeerId] = name;
		_ready[LocalPeerId] = false;
	}

	private void OnPeerConnected(long id)
	{
		var peerId = (int)id;
		_ready.TryAdd(peerId, false);
		if (Mode == NetMode.Hosting)
		{
			SeatPeer(peerId, PeerDisplayName(peerId));
			foreach (var (pid, name) in _displayNames)
				RpcId(peerId, MethodName.RpcSetDisplayName, pid, name);
			foreach (var (pid, ready) in _ready)
				RpcId(peerId, MethodName.RpcSetReady, pid, ready);
			foreach (var (speaker, text) in _chat)
				RpcId(peerId, MethodName.RpcChat, speaker, text);
			BroadcastFullLobbyState();
		}

		SyncStatusPeers();
		RosterChanged?.Invoke();
		LobbyStateChanged?.Invoke();
	}

	private void OnPeerDisconnected(long id)
	{
		var peerId = (int)id;
		_ready.Remove(peerId);
		_displayNames.Remove(peerId);
		UnseatPeer(peerId);
		if (Mode == NetMode.Hosting)
			BroadcastFullLobbyState();
		SyncStatusPeers();
		RosterChanged?.Invoke();
		LobbyStateChanged?.Invoke();
	}

	private void OnConnectedToServer()
	{
		StatusMessage = "Connected";
		RegisterLocalIdentity();
		SetLocalDisplayName(_displayNames.GetValueOrDefault(LocalPeerId, "Pilot"));
		RosterChanged?.Invoke();
		LobbyStateChanged?.Invoke();
	}

	private void OnConnectionFailed()
	{
		StatusMessage = "Connection failed";
		DisconnectSession();
	}

	private void OnServerDisconnected()
	{
		StatusMessage = "Host disconnected — session ended";
		DisconnectSession();
	}

	private void ShutdownPeer()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	private void RebuildEmptySlots()
	{
		_slots.Clear();
		var count = LobbyModeRules.SlotCount(GameMode);
		for (var i = 0; i < count; i++)
			_slots.Add(new LobbySlot());
	}

	private void RebuildEmptySlotsPreservingHumans()
	{
		var humans = new List<LobbySlot>();
		foreach (var slot in _slots)
		{
			if (slot.IsHuman)
				humans.Add(slot.Clone());
		}

		RebuildEmptySlots();
		for (var i = 0; i < humans.Count && i < _slots.Count; i++)
		{
			_slots[i] = humans[i];
			AssignTeamForNewSlot(_slots[i], LobbyTeam.None);
			_slots[i].Ready = false;
		}
	}

	private void EnsureSlotCapacity()
	{
		var needed = LobbyModeRules.SlotCount(GameMode);
		while (_slots.Count < needed)
			_slots.Add(new LobbySlot());
		while (_slots.Count > needed)
		{
			var last = _slots[^1];
			if (last.IsOccupied)
				break;
			_slots.RemoveAt(_slots.Count - 1);
		}
	}

	private void SeatPeer(int peerId, string displayName)
	{
		foreach (var slot in _slots)
		{
			if (slot.Kind == LobbySlotKind.Human && slot.PeerId == peerId)
			{
				slot.DisplayName = displayName;
				return;
			}
		}

		var empty = FindEmptySlotIndex();
		if (empty < 0)
			return;
		var s = _slots[empty];
		s.Kind = LobbySlotKind.Human;
		s.PeerId = peerId;
		s.BotId = 0;
		s.DisplayName = displayName;
		s.Ready = false;
		AssignTeamForNewSlot(s, LobbyTeam.None);
	}

	private void UnseatPeer(int peerId)
	{
		for (var i = 0; i < _slots.Count; i++)
		{
			if (_slots[i].Kind == LobbySlotKind.Human && _slots[i].PeerId == peerId)
				_slots[i] = new LobbySlot();
		}
	}

	private void UpdateSlotName(int peerId, string name)
	{
		foreach (var slot in _slots)
		{
			if (slot.Kind == LobbySlotKind.Human && slot.PeerId == peerId)
				slot.DisplayName = name;
		}
	}

	private void ApplyHumanReady(int peerId, bool ready)
	{
		foreach (var slot in _slots)
		{
			if (slot.Kind == LobbySlotKind.Human && slot.PeerId == peerId)
				slot.Ready = ready;
		}
	}

	private void SyncBotReadyWithHost()
	{
		var hostReady = IsPeerReady(1);
		foreach (var slot in _slots)
		{
			if (slot.IsBot)
				slot.Ready = hostReady;
		}
	}

	private void ClearHumanReadyFlags()
	{
		foreach (var id in GetOrderedPeerIds())
			_ready[id] = false;
		foreach (var slot in _slots)
		{
			if (slot.IsHuman)
				slot.Ready = false;
		}
	}

	private void ClearBots()
	{
		for (var i = 0; i < _slots.Count; i++)
		{
			if (_slots[i].IsBot)
				_slots[i] = new LobbySlot();
		}

		_nextBotId = LobbyModeRules.BotIdBase;
	}

	private void AssignDefaultTeams()
	{
		foreach (var slot in _slots)
		{
			if (slot.IsOccupied)
				AssignTeamForNewSlot(slot, LobbyTeam.None);
		}
	}

	private void AssignDefaultTeamToEmptyAware(int _)
	{
		// no-op placeholder for future balancing hooks
	}

	private void AssignTeamForNewSlot(LobbySlot slot, LobbyTeam preferred)
	{
		switch (GameMode)
		{
			case MultiplayerGameMode.TeamSkirmish:
			{
				if (preferred is LobbyTeam.Alpha or LobbyTeam.Bravo)
				{
					slot.Team = preferred;
					return;
				}

				var alpha = 0;
				var bravo = 0;
				foreach (var s in _slots)
				{
					if (!s.IsOccupied || ReferenceEquals(s, slot))
						continue;
					if (s.Team == LobbyTeam.Alpha) alpha++;
					else if (s.Team == LobbyTeam.Bravo) bravo++;
				}

				slot.Team = alpha <= bravo ? LobbyTeam.Alpha : LobbyTeam.Bravo;
				break;
			}
			case MultiplayerGameMode.FfaSkirmish:
			{
				slot.Team = LobbyTeam.Ffa;
				var used = new HashSet<int>();
				foreach (var s in _slots)
				{
					if (s.IsOccupied && s.Team == LobbyTeam.Ffa && !ReferenceEquals(s, slot))
						used.Add(s.FfaIndex);
				}

				var idx = 0;
				while (used.Contains(idx) && idx < 8)
					idx++;
				slot.FfaIndex = idx;
				break;
			}
			default:
				slot.Team = LobbyTeam.None;
				slot.FfaIndex = 0;
				break;
		}
	}

	private int FindEmptySlotIndex()
	{
		for (var i = 0; i < _slots.Count; i++)
		{
			if (!_slots[i].IsOccupied)
				return i;
		}

		return -1;
	}

	private void SyncReadyFromSlots()
	{
		foreach (var slot in _slots)
		{
			if (slot.IsHuman)
				_ready[slot.PeerId] = slot.Ready;
		}
	}

	private void BroadcastFullLobbyState()
	{
		if (Mode != NetMode.Hosting || !IsOnline)
		{
			LobbyStateChanged?.Invoke();
			return;
		}

		Rpc(MethodName.RpcLobbyState, (int)LobbyPhase, (int)GameMode, SlotsToArray());
	}

	private void BroadcastSlotsOnly() => BroadcastFullLobbyState();

	private void BroadcastSlotsIfHost()
	{
		if (Mode == NetMode.Hosting)
			BroadcastFullLobbyState();
	}

	private Godot.Collections.Array SlotsToArray()
	{
		var arr = new Godot.Collections.Array();
		foreach (var slot in _slots)
			arr.Add(slot.ToDict());
		return arr;
	}

	private void SyncStatusPeers()
	{
		if (Mode == NetMode.Hosting)
		{
			StatusMessage =
				$"Hosting — {PeerCount}/{LobbyModeRules.MaxHumanPeers(GameMode)} pilots  ·  {LobbyModeRules.ModeLabel(GameMode)}";
		}
	}

	private static string SanitizeChat(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return "";
		message = message.Trim();
		if (message.Length > MaxChatMessageLength)
			message = message[..MaxChatMessageLength];
		var sb = new StringBuilder(message.Length);
		foreach (var c in message)
		{
			if (c is '\n' or '\r' or '\t')
				sb.Append(' ');
			else
				sb.Append(c);
		}

		return sb.ToString().Trim();
	}

	public override void _ExitTree()
	{
		ShutdownPeer();
	}
}
