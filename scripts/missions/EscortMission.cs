using Godot;

namespace Mechanize;

/// <summary>
/// Escort a company mining rig to a claim vein, guard it while cargo fills, then bring it home to the drop beacon.
/// </summary>
public sealed class EscortMission : MissionBase
{
	/// <summary>Mission-exclusive haul-line corridor (see ClaimArenaLayout / VoidCorpsIdentity).</summary>
	public const string ClaimCode = "VC-CLAIM DRIFT-HAUL";

	private enum Phase
	{
		ToVein,
		Mining,
		Returning
	}

	private EscortAsset? _rig;
	private MissionZone? _marker;
	private Vector3 _veinPos;
	private Vector3 _homePos;
	private Phase _phase = Phase.ToVein;
	private float _mineDuration = 48f;
	private float _mineElapsed;
	private bool _mounted;
	private bool _awaitingDismountToDrill;
	private MissionPressure? _pressure;

	/// <summary>
	/// True while this mission owns Interact (F) for mount/dismount — field crates must yield.
	/// </summary>
	public static bool ReservesFieldInteract { get; private set; }

	public static void ClearFieldInteractReservation() => ReservesFieldInteract = false;

	public EscortMission() : base(MissionType.Escort) { }

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();

		_homePos = Host.PlayerDropBeacon?.GlobalPosition ?? Host.PlayerSpawn;
		_homePos.Y = 0f;

		var start = _homePos + (Vector3.Zero - _homePos).Normalized() * 6f;
		start.Y = 0f;
		if (start.LengthSquared() < 0.01f)
			start = Host.PlayerSpawn + new Vector3(4f, 0f, 2f);

		_veinPos = Host.EnemySpawnA;
		_veinPos.Y = 0f;
		// Keep the vein away from home so the loop reads clearly.
		if (_veinPos.DistanceTo(_homePos) < 28f)
		{
			_veinPos = (Host.EnemySpawnA + Host.EnemySpawnB) * 0.5f;
			_veinPos.Y = 0f;
		}

		_mineDuration = Host.Difficulty switch
		{
			PilotDifficulty.Hard => 58f,
			PilotDifficulty.Medium => 48f,
			_ => 38f
		};

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		_pressure = MissionPressure.Create(Host, "Escort", rng);

		_marker = MissionZone.Create("MiningVein", _veinPos, 10f, new Color(0.85f, 0.65f, 0.3f), "VEIN");
		root.AddChild(_marker);

		_rig = EscortAsset.Create(start, _veinPos);
		root.AddChild(_rig);
		_rig.SetEscort(Host.Player);

		Host.SpawnEnemyMech("Trinova_Detachment", _veinPos + new Vector3(8f, 0f, 6f), 0);
		if (Host.Difficulty != PilotDifficulty.Easy)
			Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1);
		else
			Host.DespawnEnemyMech("OuroTech_Detachment");

		Host.SpawnSupport("EnemyA_Tank", "light_tank", TeamId.Enemy, _veinPos + new Vector3(-6f, 0f, 4f));
		Host.SpawnSupport("EnemyA_Buggy", "scout_buggy", TeamId.Enemy, Vector3.Zero + new Vector3(8f, 0f, -4f));
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, 4f, 2f));
		Host.DespawnSupport("Player_Tower");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
	}

	public override void OnFightStarted()
	{
		_rig?.SetEscort(Host.Player);
		// Drop beacon may exist only after prep — refresh home pad.
		_homePos = Host.PlayerDropBeacon?.GlobalPosition ?? Host.PlayerSpawn;
		_homePos.Y = 0f;
	}

	public override void Tick(float dt)
	{
		if (!Host.IsFighting || ObjectivesComplete || _rig == null)
		{
			ReservesFieldInteract = false;
			return;
		}

		_rig.SetEscort(Host.Player);

		if (_rig.IsDestroyed)
		{
			ReservesFieldInteract = false;
			if (_mounted)
				ForceDismount();
			SfxService.Play("alarm");
			Lose();
			return;
		}

		RefreshInteractReservation();
		HandleMountInput();

		switch (_phase)
		{
			case Phase.ToVein:
				if (_rig.HasArrived)
				{
					if (_mounted)
					{
						// Rig parks at the vein but will not drill with a rider aboard.
						_awaitingDismountToDrill = true;
						_marker?.SetLabel("DISMOUNT");
					}
					else
					{
						BeginMining();
					}
				}
				break;

			case Phase.Mining:
				TickMining(dt);
				break;

			case Phase.Returning:
				if (_rig.HasArrived)
				{
					_marker?.SetLabel("SECURE");
					MarkObjectivesComplete();
				}
				break;
		}
	}

	private void BeginMining()
	{
		_phase = Phase.Mining;
		_mineElapsed = 0f;
		_rig?.SetHold(true);
		_rig?.SetCargoFill(0f);
		_marker?.SetLabel("MINING");
		SfxService.Confirm();
	}

	private void TickMining(float dt)
	{
		if (_rig == null)
			return;

		// Dig only while the escort stays close — otherwise the rig idles under threat.
		if (_rig.IsEscorted)
			_mineElapsed += dt;

		var fill = Mathf.Clamp(_mineElapsed / _mineDuration, 0f, 1f);
		_rig.SetCargoFill(fill);
		_pressure?.NotifyProgress(fill, _veinPos);

		if (fill < 1f)
			return;

		_pressure?.NotifyObjectiveSecured(_veinPos);
		BeginReturn();
	}

	private void BeginReturn()
	{
		_phase = Phase.Returning;
		_homePos = Host.PlayerDropBeacon?.GlobalPosition ?? Host.PlayerSpawn;
		_homePos.Y = 0f;
		_rig?.SetCargoFill(1f);
		_rig?.SetDestination(_homePos);
		_marker?.QueueFree();
		_marker = MissionZone.Create("HomePad", _homePos, 10f, new Color(0.55f, 0.85f, 1f), "HOME");
		var root = Host.Root.GetNodeOrNull<Node3D>("MissionRuntime");
		root?.AddChild(_marker);
		SfxService.Confirm();
	}

	private bool MountAllowed =>
		_phase is Phase.ToVein or Phase.Returning && _rig is { IsDestroyed: false };

	private void RefreshInteractReservation()
	{
		var player = Host.Player;
		ReservesFieldInteract =
			_mounted
			|| _awaitingDismountToDrill
			|| (MountAllowed
			    && player != null
			    && GodotObject.IsInstanceValid(player)
			    && _rig != null
			    && _rig.CanMountFrom(player.GlobalPosition));
	}

	private void HandleMountInput()
	{
		var player = Host.Player;
		if (_rig == null || player == null || !GodotObject.IsInstanceValid(player))
			return;

		// Pilot down while aboard — set them off so the collapse resolves normally.
		if (_mounted && player.Integrity?.IsCollapsed == true)
		{
			ForceDismount();
			return;
		}

		// Riding is a single-player feature for now; co-op is host-authoritative and
		// doesn't replicate the interact tap.
		var net = player.GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { IsOnline: true })
			return;

		if (!Input.IsActionJustPressed("interact"))
			return;

		if (_mounted)
			DoDismount();
		else if (MountAllowed && _rig.CanMountFrom(player.GlobalPosition))
			DoMount();
	}

	private void DoMount()
	{
		var mount = _rig?.MountPoint;
		if (mount == null || Host.Player == null)
			return;

		Host.Player.MountToCarrier(mount);
		_mounted = true;
		SfxService.Confirm();
	}

	private void DoDismount()
	{
		if (_rig == null || Host.Player == null)
			return;

		Host.Player.DismountFromCarrier(_rig.SafeDismountPosition());
		_mounted = false;
		SfxService.Click();

		// Dropped off at the vein — the drill can spin up now.
		if (_awaitingDismountToDrill)
		{
			_awaitingDismountToDrill = false;
			BeginMining();
		}
	}

	private void ForceDismount()
	{
		if (Host.Player is { } player && GodotObject.IsInstanceValid(player) && _rig != null)
			player.DismountFromCarrier(_rig.SafeDismountPosition());
		_mounted = false;
	}

	public override string GetHudLine()
	{
		if (_rig == null)
			return "OBJECTIVE  Escort the mining rig";
		if (_rig.IsDestroyed)
			return "OBJECTIVE  Mining rig destroyed";
		if (ObjectivesComplete)
			return "OBJECTIVE  Ore secured at drop beacon" + ExtractHudHint();

		var hp = _rig.HealthRatio * 100f;

		if (_awaitingDismountToDrill)
			return $"OBJECTIVE  Dismount (F) to begin drilling  (HP {hp:0}%)";

		if (_mounted)
			return _phase == Phase.Returning
				? $"OBJECTIVE  Riding rig home — Shift overdrive{OverdriveState()}  ·  F dismount  (HP {hp:0}%)"
				: $"OBJECTIVE  Riding rig to vein — Shift overdrive{OverdriveState()}  ·  F dismount  (HP {hp:0}%)";

		return _phase switch
		{
			Phase.ToVein =>
				$"OBJECTIVE  Escort mining rig to the vein  (HP {hp:0}% — stay close){MountHint()}",
			Phase.Mining when _rig.IsEscorted =>
				$"OBJECTIVE  Guard the dig  ·  cargo {Mathf.RoundToInt(_rig.CargoFill * 100f)}%  (HP {hp:0}%)",
			Phase.Mining =>
				$"OBJECTIVE  Return to the rig — mining paused  ·  cargo {Mathf.RoundToInt(_rig.CargoFill * 100f)}%",
			Phase.Returning =>
				$"OBJECTIVE  Escort full rig back to drop beacon  (HP {hp:0}% — stay close){MountHint()}",
			_ => "OBJECTIVE  Mining escort"
		};
	}

	private string MountHint()
	{
		if (!MountAllowed || _rig == null || Host.Player is not { } player)
			return "";
		var net = player.GetNodeOrNull<NetSession>("/root/NetSession");
		if (net is { IsOnline: true })
			return "";
		return _rig.CanMountFrom(player.GlobalPosition) ? "  |  F to mount & ride" : "";
	}

	private string OverdriveState() =>
		Host.Player?.IsCarrierOverdriveActive == true ? " [ACTIVE]" : " (normal PWR / 2× HEAT)";

	private Vector3 Offset(Vector3 from, float right, float forward)
	{
		var dir = Vector3.Zero - from;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			dir = new Vector3(0f, 0f, -1f);
		dir = dir.Normalized();
		var side = dir.Cross(Vector3.Up).Normalized();
		return from + side * right + dir * forward;
	}
}
