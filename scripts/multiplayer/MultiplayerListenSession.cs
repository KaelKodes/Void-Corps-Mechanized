using Godot;

namespace Mechanize;

/// <summary>
/// Listen-server session helper for lobby host/join.
/// UI-facing: create/tear down peer, resolve a shareable join string.
/// </summary>
public static class MultiplayerListenSession
{
	public const int DefaultPort = 7777;
	public const int MaxPeers = 20; // headroom for 10v10; campaign uses ≤4

	public static string? JoinAddress { get; private set; }
	public static int Port { get; private set; } = DefaultPort;
	public static bool IsHosting { get; private set; }
	public static bool IsClient { get; private set; }

	public static bool IsActive => IsHosting || IsClient;

	public static Error Host(SceneTree tree, int port = DefaultPort, int maxClients = MaxPeers)
	{
		Shutdown(tree);
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateServer(port, maxClients);
		if (err != Error.Ok)
			return err;

		tree.GetMultiplayer().MultiplayerPeer = peer;
		Port = port;
		IsHosting = true;
		IsClient = false;
		JoinAddress = $"{PickBestLocalAddress()}:{port}";
		return Error.Ok;
	}

	public static Error Join(SceneTree tree, string address)
	{
		Shutdown(tree);
		if (!TryParseAddress(address, out var host, out var port))
			return Error.InvalidParameter;

		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateClient(host, port);
		if (err != Error.Ok)
			return err;

		tree.GetMultiplayer().MultiplayerPeer = peer;
		Port = port;
		IsHosting = false;
		IsClient = true;
		JoinAddress = $"{host}:{port}";
		return Error.Ok;
	}

	public static void Shutdown(SceneTree tree)
	{
		var mp = tree.GetMultiplayer();
		if (mp.MultiplayerPeer != null)
		{
			mp.MultiplayerPeer.Close();
			mp.MultiplayerPeer = null;
		}

		JoinAddress = null;
		IsHosting = false;
		IsClient = false;
		Port = DefaultPort;
	}

	public static bool TryParseAddress(string raw, out string host, out int port)
	{
		host = "";
		port = DefaultPort;
		if (string.IsNullOrWhiteSpace(raw))
			return false;

		var trimmed = raw.Trim();
		var parts = trimmed.Split(':');
		if (parts.Length == 1)
		{
			host = parts[0].Trim();
			return host.Length > 0;
		}

		if (parts.Length != 2)
			return false;
		host = parts[0].Trim();
		if (host.Length == 0)
			return false;
		return int.TryParse(parts[1].Trim(), out port) && port is > 0 and < 65536;
	}

	public static string PickBestLocalAddress()
	{
		string? fallback = null;
		foreach (var addr in IP.GetLocalAddresses())
		{
			if (string.IsNullOrEmpty(addr) || addr.Contains(':'))
				continue; // skip IPv6 for v1 join string
			if (addr.StartsWith("127."))
			{
				fallback ??= addr;
				continue;
			}

			// Prefer common private LAN ranges
			if (addr.StartsWith("192.168.") || addr.StartsWith("10.") || addr.StartsWith("172."))
				return addr;
			fallback ??= addr;
		}

		return fallback ?? "127.0.0.1";
	}
}
