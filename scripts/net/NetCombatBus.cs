using Godot;

namespace Mechanize;

/// <summary>
/// Host broadcasts combat FX / projectile visuals so every peer shares the same battlefield.
/// Authentic damage only runs on the listen-server.
/// </summary>
public partial class NetCombatBus : Node
{
	public static NetCombatBus? Find(Node from)
	{
		var scene = from.GetTree()?.CurrentScene;
		if (scene == null)
			return null;
		var direct = scene.GetNodeOrNull<NetCombatBus>("NetCombatBus");
		if (direct != null)
			return direct;
		foreach (var child in scene.GetChildren())
		{
			if (child is NetCombatBus bus)
				return bus;
			var nested = child.GetNodeOrNull<NetCombatBus>("NetCombatBus");
			if (nested != null)
				return nested;
		}

		return null;
	}

	public void EnsureUnder(Node arenaRoot)
	{
		if (GetParent() == arenaRoot)
			return;
		if (GetParent() != null)
			GetParent().RemoveChild(this);
		Name = "NetCombatBus";
		arenaRoot.AddChild(this);
	}

	public void HostSpawnProjectile(
		Node parent,
		Node? source,
		Vector3 position,
		Vector3 velocity,
		float damage,
		float lifetime,
		TeamId team,
		TargetingMode targeting,
		int preferredSlot,
		bool ballistic,
		float gravity,
		bool playsWorldImpactSfx = true,
		bool damagesWorldObjects = true)
	{
		var projectile = BuildProjectile(
			source,
			velocity,
			damage,
			lifetime,
			team,
			targeting,
			preferredSlot,
			ballistic,
			gravity,
			dealsDamage: true,
			playsWorldImpactSfx,
			damagesWorldObjects);
		parent.AddChild(projectile);
		PlaceProjectile(projectile, position, velocity);

		if (Multiplayer.MultiplayerPeer == null || !Multiplayer.IsServer())
			return;

		var sourcePath = source != null ? source.GetPath().ToString() : "";
		Rpc(
			MethodName.RpcSpawnProjectileVisual,
			sourcePath,
			position,
			velocity,
			damage,
			lifetime,
			(int)team,
			(int)targeting,
			preferredSlot,
			ballistic,
			gravity,
			playsWorldImpactSfx,
			damagesWorldObjects);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void RpcSpawnProjectileVisual(
		string sourcePath,
		Vector3 position,
		Vector3 velocity,
		float damage,
		float lifetime,
		int team,
		int targeting,
		int preferredSlot,
		bool ballistic,
		float gravity,
		bool playsWorldImpactSfx,
		bool damagesWorldObjects)
	{
		Node? source = null;
		if (!string.IsNullOrEmpty(sourcePath))
			source = GetTree()?.Root.GetNodeOrNull(sourcePath);

		var parent = GetTree()?.CurrentScene ?? GetParent();
		if (parent == null)
			return;

		var projectile = BuildProjectile(
			source,
			velocity,
			damage,
			lifetime,
			(TeamId)team,
			(TargetingMode)targeting,
			preferredSlot,
			ballistic,
			gravity,
			dealsDamage: false,
			playsWorldImpactSfx,
			damagesWorldObjects);
		parent.AddChild(projectile);
		PlaceProjectile(projectile, position, velocity);
	}

	private static Projectile BuildProjectile(
		Node? source,
		Vector3 velocity,
		float damage,
		float lifetime,
		TeamId team,
		TargetingMode targeting,
		int preferredSlot,
		bool ballistic,
		float gravity,
		bool dealsDamage,
		bool playsWorldImpactSfx,
		bool damagesWorldObjects)
	{
		var projectile = Projectile.Create(ballistic);
		projectile.Source = source;
		projectile.SourceTeam = team;
		projectile.Damage = damage;
		projectile.Velocity = velocity;
		projectile.Lifetime = lifetime;
		projectile.GravityAccel = gravity;
		projectile.TargetingMode = targeting;
		projectile.PreferredSlot = preferredSlot >= 0 ? (PartSlot)preferredSlot : null;
		projectile.DealsDamage = dealsDamage;
		projectile.PlaysWorldImpactSfx = playsWorldImpactSfx;
		projectile.DamagesWorldObjects = damagesWorldObjects;
		return projectile;
	}

	private static void PlaceProjectile(Projectile projectile, Vector3 position, Vector3 velocity)
	{
		projectile.GlobalPosition = position;
		if (velocity.LengthSquared() > 0.01f)
			projectile.LookAt(position + velocity, Vector3.Up);
	}

	public void HostSyncMatchHud(int lives, int scrap, int lifeCost)
	{
		if (Multiplayer.MultiplayerPeer == null || !Multiplayer.IsServer())
			return;
		Rpc(MethodName.RpcSyncMatchHud, lives, scrap, lifeCost);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RpcSyncMatchHud(int lives, int scrap, int lifeCost)
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.Match.ApplyHostSnapshot(lives, scrap, lifeCost);
	}

	public void HostShowResults(int outcome)
	{
		if (Multiplayer.MultiplayerPeer == null || !Multiplayer.IsServer())
			return;
		Rpc(MethodName.RpcShowResults, outcome);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcShowResults(int outcome)
	{
		var arena = GetParent() as ArenaController;
		arena?.ClientPresentResults((MatchOutcome)outcome);
	}
}
