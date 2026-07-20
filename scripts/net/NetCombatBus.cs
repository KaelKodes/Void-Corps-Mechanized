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
		ProjectileStyle style,
		float gravity,
		bool playsWorldImpactSfx = true,
		bool damagesWorldObjects = true,
		float visualScale = 1f)
	{
		var projectile = BuildProjectile(
			source,
			velocity,
			damage,
			lifetime,
			team,
			targeting,
			preferredSlot,
			style,
			gravity,
			dealsDamage: true,
			playsWorldImpactSfx,
			damagesWorldObjects,
			visualScale);
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
			(int)style,
			gravity,
			playsWorldImpactSfx,
			damagesWorldObjects,
			visualScale);
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
		int style,
		float gravity,
		bool playsWorldImpactSfx,
		bool damagesWorldObjects,
		float visualScale)
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
			(ProjectileStyle)style,
			gravity,
			dealsDamage: false,
			playsWorldImpactSfx,
			damagesWorldObjects,
			visualScale);
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
		ProjectileStyle style,
		float gravity,
		bool dealsDamage,
		bool playsWorldImpactSfx,
		bool damagesWorldObjects,
		float visualScale)
	{
		var projectile = Projectile.Create(style, visualScale);
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

	public void HostDeploymentPhase(string jobId, int phase, Vector3 target, int team)
	{
		if (Multiplayer.MultiplayerPeer == null || !Multiplayer.IsServer())
			return;
		Rpc(MethodName.RpcDeploymentPhase, jobId, phase, target, team);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void RpcDeploymentPhase(string jobId, int phase, Vector3 target, int team)
	{
		var arena = GetParent() as ArenaController;
		arena?.ClientObserveDeployment(jobId, (DeploymentPhase)phase, target, (TeamId)team);
	}

	public void BroadcastFieldCargoPod(int ownerPeer, int slot, string instanceId, string partId, Vector3 landing)
	{
		if (Multiplayer.MultiplayerPeer == null)
			return;
		Rpc(MethodName.RpcFieldCargoPod, ownerPeer, slot, instanceId, partId, landing);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcFieldCargoPod(int ownerPeer, int slot, string instanceId, string partId, Vector3 landing)
	{
		var sender = Multiplayer.GetRemoteSenderId();
		if (sender != 0 && sender != ownerPeer)
			return;
		var arena = GetParent() as ArenaController;
		arena?.ObserveRemoteFieldCargoPod(ownerPeer, (PartSlot)slot, instanceId, partId, landing);
	}

	public void BroadcastFieldCrateLanded(int ownerPeer, int slot, string instanceId, string partId, Vector3 position)
	{
		if (Multiplayer.MultiplayerPeer == null)
			return;
		Rpc(MethodName.RpcFieldCrateLanded, ownerPeer, slot, instanceId, partId, position);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcFieldCrateLanded(int ownerPeer, int slot, string instanceId, string partId, Vector3 position)
	{
		var sender = Multiplayer.GetRemoteSenderId();
		if (sender != 0 && sender != ownerPeer)
			return;
		var arena = GetParent() as ArenaController;
		arena?.ObserveRemoteFieldCrateLanded(ownerPeer, (PartSlot)slot, instanceId, partId, position);
	}

	public void BroadcastFieldCrateConsumed(string instanceId)
	{
		if (Multiplayer.MultiplayerPeer == null || string.IsNullOrEmpty(instanceId))
			return;
		Rpc(MethodName.RpcFieldCrateConsumed, instanceId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcFieldCrateConsumed(string instanceId)
	{
		var arena = GetParent() as ArenaController;
		arena?.ObserveRemoteFieldCrateConsumed(instanceId);
	}

	public void RequestFieldTradeClaim(
		int ownerPeer,
		int claimantPeer,
		int slot,
		string instanceId,
		string partId)
	{
		if (Multiplayer.MultiplayerPeer == null
		    || ownerPeer <= 0
		    || claimantPeer <= 0
		    || string.IsNullOrEmpty(instanceId))
			return;
		RpcId(
			ownerPeer,
			MethodName.RpcRequestFieldTradeClaim,
			claimantPeer,
			slot,
			instanceId,
			partId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcRequestFieldTradeClaim(
		int claimantPeer,
		int slot,
		string instanceId,
		string partId)
	{
		if (Multiplayer.GetRemoteSenderId() != claimantPeer)
			return;

		var arena = GetParent() as ArenaController;
		var condition = new Godot.Collections.Dictionary();
		var granted = arena != null && arena.AuthorizeFieldTradeClaim(
			claimantPeer,
			(PartSlot)slot,
			instanceId,
			partId,
			out condition);

		var ownerPeer = Multiplayer.GetUniqueId();
		if (granted)
		{
			RpcId(
				claimantPeer,
				MethodName.RpcFieldTradeGranted,
				ownerPeer,
				slot,
				instanceId,
				partId,
				condition);
		}
		else
		{
			RpcId(
				claimantPeer,
				MethodName.RpcFieldTradeRejected,
				ownerPeer,
				instanceId);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcFieldTradeGranted(
		int ownerPeer,
		int slot,
		string instanceId,
		string partId,
		Godot.Collections.Dictionary condition)
	{
		if (Multiplayer.GetRemoteSenderId() != ownerPeer)
			return;
		var arena = GetParent() as ArenaController;
		arena?.CompleteFieldTradeClaim(
			ownerPeer,
			(PartSlot)slot,
			instanceId,
			partId,
			condition);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcFieldTradeRejected(int ownerPeer, string instanceId)
	{
		if (Multiplayer.GetRemoteSenderId() != ownerPeer)
			return;
		var arena = GetParent() as ArenaController;
		arena?.RejectFieldTradeClaim(instanceId);
	}
}
