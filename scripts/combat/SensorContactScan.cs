using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Player-local passive head scan: periodic radar pulse that stamps frozen
/// last-known contacts in <see cref="MechStats.ScannerRange"/>.
/// World 3D blips stay off until an ability (or debug flag) reveals them for a duration.
/// </summary>
public partial class SensorContactScan : Node
{
	public const string NodeName = "SensorContactScan";

	/// <summary>Debug override — keeps world blips on regardless of ability reveals.</summary>
	public static bool ShowWorldBlips { get; set; }

	private static readonly Color AllyColor = new(0.28f, 0.88f, 1f);
	private static readonly Color EnemyColor = new(1f, 0.32f, 0.18f);

	private MechController? _mech;
	private float _pulseTimer;
	private float _worldBlipsRemaining;
	private Node? _blipRoot;
	private readonly List<SensorContactStamp> _lastKnown = new();
	private float _stampExpireAt;

	public IReadOnlyList<SensorContactStamp> LastKnownContacts => _lastKnown;

	/// <summary>True while debug override or an ability reveal window is active.</summary>
	public bool WorldBlipsActive => ShowWorldBlips || _worldBlipsRemaining > 0f;

	/// <summary>Seconds left on the current ability-driven 3D reveal (0 if none).</summary>
	public float WorldBlipsRemaining => Mathf.Max(0f, _worldBlipsRemaining);

	/// <summary>Seconds remaining before the current stamp set should be treated as stale.</summary>
	public float StampRemaining => Mathf.Max(0f, _stampExpireAt - Time.GetTicksMsec() * 0.001f);

	public static void EnsureOn(MechController host)
	{
		if (host.HangarDisplayOnly || !host.IsPlayerControlled)
			return;
		if (host.GetNodeOrNull(NodeName) != null)
			return;
		host.AddChild(new SensorContactScan { Name = NodeName });
	}

	public override void _Ready()
	{
		_mech = GetParent() as MechController;
		_pulseTimer = 0.35f;
	}

	public override void _Process(double delta)
	{
		if (_mech == null || !GodotObject.IsInstanceValid(_mech))
			return;
		if (_mech.HangarDisplayOnly || !_mech.IsPlayerControlled)
			return;
		if (_mech.Integrity?.IsCollapsed == true || _mech.Health?.IsDead == true)
		{
			_lastKnown.Clear();
			_worldBlipsRemaining = 0f;
			return;
		}

		var dt = (float)delta;
		if (_worldBlipsRemaining > 0f)
			_worldBlipsRemaining = Mathf.Max(0f, _worldBlipsRemaining - dt);

		_pulseTimer -= dt;
		if (_pulseTimer > 0f)
			return;

		var stats = _mech.Assembler?.Stats ?? MechStats.BlindFallback;
		Pulse(stats);
		_pulseTimer = ComputeInterval(stats.ScannerResolution);
	}

	/// <summary>
	/// Turn passive world 3D blips on for <paramref name="duration"/> seconds.
	/// Stacks by extending if already revealing. Optionally stamps immediately.
	/// </summary>
	public void RevealWorldBlips(float duration, bool forcePulse = true)
	{
		_worldBlipsRemaining = Mathf.Max(_worldBlipsRemaining, Mathf.Max(0.25f, duration));
		if (forcePulse)
			ForcePulse();
	}

	/// <summary>Force an immediate refresh (future active utilities).</summary>
	public void ForcePulse()
	{
		if (_mech == null)
			return;
		var stats = _mech.Assembler?.Stats ?? MechStats.BlindFallback;
		Pulse(stats);
		_pulseTimer = ComputeInterval(stats.ScannerResolution);
	}

	private void Pulse(MechStats stats)
	{
		if (_mech == null)
			return;

		var range = Mathf.Max(8f, stats.ScannerRange);
		var linger = ComputeLinger(stats.ScannerResolution);
		var style = stats.ScanBlipStyle;
		var requiresLos = stats.ScanRequiresLos;
		var origin = _mech.GlobalPosition + Vector3.Up * 1.4f;
		var rangeSq = range * range;
		var now = Time.GetTicksMsec() * 0.001f;
		var spawnWorld = WorldBlipsActive;

		_lastKnown.Clear();
		_stampExpireAt = now + linger;

		Node? root = null;
		if (spawnWorld)
			root = EnsureBlipRoot();

		foreach (var contact in GatherContacts())
		{
			if (contact == _mech)
				continue;
			if (!TeamUtil.IsAliveCombatant(contact))
				continue;
			if (_mech.GlobalPosition.DistanceSquaredTo(contact.GlobalPosition) > rangeSq)
				continue;
			if (IsCloakedHostile(contact))
				continue;
			if (requiresLos && !HasClearSensorRay(origin, TeamUtil.GetAimPoint(contact), contact))
				continue;

			var team = TeamUtil.GetTeam(contact);
			var hostile = TeamUtil.IsHostile(_mech.Team, team);
			var pos = contact.GlobalPosition;
			_lastKnown.Add(new SensorContactStamp(pos, team, hostile, now, linger));

			if (root == null)
				continue;

			var color = hostile ? EnemyColor : AllyColor;
			var blip = new SensorBlip();
			root.AddChild(blip);
			blip.Configure(pos, color, linger, style);
		}
	}

	private List<Node3D> GatherContacts()
	{
		var result = new List<Node3D>();
		var tree = _mech?.GetTree();
		if (tree == null)
			return result;

		CollectFromGroup(tree, "mechs", result);
		CollectFromGroup(tree, "support", result);
		CollectFromGroup(tree, "escorts", result);
		CollectFromGroup(tree, "turrets", result);

		if (result.Count == 0 && tree.CurrentScene != null)
			CollectFallback(tree.CurrentScene, result);

		return result;
	}

	private static void CollectFromGroup(SceneTree tree, string group, List<Node3D> into)
	{
		foreach (var node in tree.GetNodesInGroup(group))
		{
			if (node is Node3D body && !into.Contains(body))
				into.Add(body);
		}
	}

	private static void CollectFallback(Node node, List<Node3D> into)
	{
		if (node is MechController or SupportUnit or EscortAsset or HellfireTurret)
		{
			if (node is Node3D body && !into.Contains(body))
				into.Add(body);
		}

		foreach (var child in node.GetChildren())
			CollectFallback(child, into);
	}

	private bool IsCloakedHostile(Node3D contact)
	{
		if (_mech == null || contact is not MechController other)
			return false;
		if (!TeamUtil.IsHostile(_mech.Team, other.Team))
			return false;
		return other.Abilities?.IsCloaked == true;
	}

	private bool HasClearSensorRay(Vector3 from, Vector3 to, Node3D target)
	{
		if (_mech == null)
			return true;

		var space = _mech.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return true;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = PhysicsLayers.World | PhysicsLayers.Targets;
		var exclude = new Godot.Collections.Array<Rid> { _mech.GetRid() };
		if (target is CollisionObject3D body)
			exclude.Add(body.GetRid());
		query.Exclude = exclude;
		return space.IntersectRay(query).Count == 0;
	}

	private Node EnsureBlipRoot()
	{
		if (_blipRoot != null && GodotObject.IsInstanceValid(_blipRoot))
			return _blipRoot;

		var tree = GetTree();
		Node parent = tree?.CurrentScene != null ? tree.CurrentScene : this;
		_blipRoot = parent.GetNodeOrNull("SensorBlipRoot");
		if (_blipRoot == null)
		{
			_blipRoot = new Node { Name = "SensorBlipRoot" };
			parent.AddChild(_blipRoot);
		}

		return _blipRoot;
	}

	/// <summary>Higher resolution → more frequent pulses.</summary>
	private static float ComputeInterval(float resolution)
	{
		var t = Mathf.Clamp(resolution, 0.05f, 1f);
		return Mathf.Lerp(4.2f, 2.0f, t);
	}

	/// <summary>Higher resolution → slightly longer linger.</summary>
	private static float ComputeLinger(float resolution)
	{
		var t = Mathf.Clamp(resolution, 0.05f, 1f);
		return Mathf.Lerp(1.2f, 2.0f, t);
	}
}

/// <summary>Frozen contact from the last passive (or forced) scan pulse.</summary>
public readonly struct SensorContactStamp
{
	public Vector3 WorldPosition { get; }
	public TeamId Team { get; }
	public bool IsHostile { get; }
	public float StampedAtSec { get; }
	public float LingerSec { get; }

	public SensorContactStamp(
		Vector3 worldPosition,
		TeamId team,
		bool isHostile,
		float stampedAtSec,
		float lingerSec)
	{
		WorldPosition = worldPosition;
		Team = team;
		IsHostile = isHostile;
		StampedAtSec = stampedAtSec;
		LingerSec = lingerSec;
	}

	public bool IsExpired(float nowSec) => nowSec >= StampedAtSec + LingerSec;
}
