using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Listen-server multiplayer session. Host is match authority; guests connect directly.
/// Caps: co-op campaign 4; skirmish can grow later on the same pipe.
/// </summary>
public partial class NetSession : Node
{
	public const int DefaultPort = 7770;
	public const int MaxCoopPlayers = 4;
	public const int MaxSkirmishPlayers = 20; // 10v10

	public enum NetMode
	{
		Offline,
		Hosting,
		Client
	}

	public enum LobbyIntent
	{
		None,
		CoopSkirmish,
		CoopCampaign
	}

	public NetMode Mode { get; private set; } = NetMode.Offline;
	public LobbyIntent Intent { get; set; } = LobbyIntent.None;
	public string StatusMessage { get; private set; } = "Offline";
	public int Port { get; private set; } = DefaultPort;

	private readonly Dictionary<int, bool> _ready = new();
	private readonly Dictionary<int, string> _displayNames = new();

	public bool IsOnline => Mode != NetMode.Offline;
	public bool IsHost => Mode is NetMode.Hosting or NetMode.Offline;
	public bool IsServerPeer => !IsOnline || Multiplayer.IsServer();
	public int LocalPeerId => IsOnline ? Multiplayer.GetUniqueId() : 1;
	public int PeerCount => IsOnline ? 1 + Multiplayer.GetPeers().Length : 1;

	public event Action? RosterChanged;
	public event Action? Disconnected;
	public event Action? MatchLaunchReceived;

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

	public Error Host(int port = DefaultPort)
	{
		ShutdownPeer();
		Port = port;
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateServer(port, MaxCoopPlayers);
		if (err != Error.Ok)
		{
			StatusMessage = $"Host failed: {err}";
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Hosting;
		StatusMessage = $"Hosting on port {port}";
		_ready.Clear();
		_displayNames.Clear();
		RegisterLocalIdentity();
		RosterChanged?.Invoke();
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
		StatusMessage = $"Connecting to {address}:{port}…";
		_ready.Clear();
		_displayNames.Clear();
		return Error.Ok;
	}

	public void DisconnectSession()
	{
		ShutdownPeer();
		Mode = NetMode.Offline;
		Intent = LobbyIntent.None;
		PendingLaunch = null;
		_ready.Clear();
		_displayNames.Clear();
		StatusMessage = "Offline";
		RosterChanged?.Invoke();
		Disconnected?.Invoke();
	}

	public void SetLocalReady(bool ready)
	{
		if (!IsOnline)
			return;
		var id = LocalPeerId;
		_ready[id] = ready;
		Rpc(MethodName.RpcSetReady, id, ready);
		RosterChanged?.Invoke();
	}

	public void SetLocalDisplayName(string name)
	{
		var id = LocalPeerId;
		_displayNames[id] = name;
		if (IsOnline)
			Rpc(MethodName.RpcSetDisplayName, id, name);
		RosterChanged?.Invoke();
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcSetReady(int peerId, bool ready)
	{
		_ready[peerId] = ready;
		RosterChanged?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcSetDisplayName(int peerId, string name)
	{
		_displayNames[peerId] = name;
		RosterChanged?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcMatchLaunch(Godot.Collections.Dictionary payload)
	{
		ApplyMatchLaunch(payload);
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
		var name = session?.Profile.MercCorpName ?? VoidCorpsIdentity.PlayerCorpCodename;
		_displayNames[LocalPeerId] = name;
		_ready[LocalPeerId] = false;
	}

	private void OnPeerConnected(long id)
	{
		var peerId = (int)id;
		_ready.TryAdd(peerId, false);
		if (Mode == NetMode.Hosting)
		{
			// Sync our name to the new peer.
			foreach (var (pid, name) in _displayNames)
				RpcId(peerId, MethodName.RpcSetDisplayName, pid, name);
			foreach (var (pid, ready) in _ready)
				RpcId(peerId, MethodName.RpcSetReady, pid, ready);
		}

		StatusMessage = Mode == NetMode.Hosting
			? $"Hosting — {PeerCount}/{MaxCoopPlayers} pilots"
			: StatusMessage;
		RosterChanged?.Invoke();
	}

	private void OnPeerDisconnected(long id)
	{
		var peerId = (int)id;
		_ready.Remove(peerId);
		_displayNames.Remove(peerId);
		StatusMessage = Mode == NetMode.Hosting
			? $"Hosting — {PeerCount}/{MaxCoopPlayers} pilots"
			: StatusMessage;
		RosterChanged?.Invoke();
	}

	private void OnConnectedToServer()
	{
		StatusMessage = "Connected";
		RegisterLocalIdentity();
		SetLocalDisplayName(_displayNames.GetValueOrDefault(LocalPeerId, "Pilot"));
		RosterChanged?.Invoke();
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

	public override void _ExitTree()
	{
		ShutdownPeer();
	}
}
