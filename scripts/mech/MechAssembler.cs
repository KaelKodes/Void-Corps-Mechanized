using System.Collections.Generic;
using Godot;

namespace Mechanize;

public partial class MechAssembler : Node
{
	[Export] public NodePath SocketsPath { get; set; } = "../Sockets";

	private static readonly PartSlot[] EquipOrder =
	{
		PartSlot.Legs,
		PartSlot.Torso,
		PartSlot.Head,
		PartSlot.PowerCore,
		PartSlot.WeaponL,
		PartSlot.WeaponR,
		PartSlot.ShoulderL,
		PartSlot.ShoulderR,
		PartSlot.Backpack,
		PartSlot.Systems
	};

	private Node3D? _sockets;
	private readonly Dictionary<PartSlot, Node3D> _socketNodes = new();
	private readonly Dictionary<PartSlot, Hardpoint> _hardpoints = new();

	public IReadOnlyDictionary<PartSlot, Hardpoint> Hardpoints => _hardpoints;
	public MechStats Stats { get; private set; } = MechStats.BlindFallback;

	public float TotalArmor => Stats.HullHp;
	public float MaxSpeed => Stats.WalkSpeed;
	public float TurnRateDegrees => Stats.TurnRateDegrees;
	public float FireRateMultiplier => Stats.FireRateMultiplier;
	public LegMode LegMode => Stats.LegMode;
	public LegType LegType => Stats.LegType;

	public override void _Ready()
	{
		_sockets = GetNodeOrNull<Node3D>(SocketsPath) ?? GetParent()?.GetNodeOrNull<Node3D>("Sockets");
		CacheSockets();
	}

	public void Assemble(LoadoutData loadout)
	{
		if (_sockets == null)
			_Ready();

		ClearHardpoints();
		GameCatalog.SanitizeMounts(loadout);

		foreach (var slot in EquipOrder)
		{
			if (!GameCatalog.IsMountAvailable(loadout, slot))
				continue;
			EquipSlot(slot, loadout.GetPartId(slot));
		}

		FitMountSocketsToTorso(GameCatalog.GetPart(loadout.TorsoId));
		Stats = DeriveStats();
	}

	/// <summary>
	/// Torso VisualScale changes hull depth/width; keep Back / Systems / Shoulders on the hull surface.
	/// Matches PartVisualFactory torso main box: size (1.4, 1.05, 1.0)*scale at local (0, 0.55, 0).
	/// </summary>
	private void FitMountSocketsToTorso(PartData? torso)
	{
		if (!_socketNodes.TryGetValue(PartSlot.Torso, out var torsoSocket))
			return;

		var scale = torso?.VisualScale ?? Vector3.One;
		const float bodyLocalY = 0.55f;
		var halfW = 0.5f * 1.4f * scale.X;
		var halfH = 0.5f * 1.05f * scale.Y;
		var halfD = 0.5f * 1.0f * scale.Z;

		var bodyCenter = torsoSocket.Position + new Vector3(0f, bodyLocalY, 0f);
		var backZ = bodyCenter.Z + halfD;

		if (_socketNodes.TryGetValue(PartSlot.Backpack, out var back))
			back.Position = new Vector3(bodyCenter.X, bodyCenter.Y + 0.06f, backZ + 0.02f);

		if (_socketNodes.TryGetValue(PartSlot.Systems, out var systems))
			systems.Position = new Vector3(bodyCenter.X, bodyCenter.Y - 0.22f, backZ + 0.06f);

		if (_socketNodes.TryGetValue(PartSlot.ShoulderL, out var shoulderL))
			shoulderL.Position = new Vector3(-(halfW + 0.08f), bodyCenter.Y + halfH * 0.5f, bodyCenter.Z + halfD * 0.12f);

		if (_socketNodes.TryGetValue(PartSlot.ShoulderR, out var shoulderR))
			shoulderR.Position = new Vector3(halfW + 0.08f, bodyCenter.Y + halfH * 0.5f, bodyCenter.Z + halfD * 0.12f);
	}

	public MechStats DeriveStats()
	{
		float hull = 40f;
		float speed = 8f;
		float turn = 70f;
		float fireBonus = 1f;
		float heatCap = 40f;
		float dissipate = 6f;
		float idleHeat = 0.5f;
		float moveHeat = 0f;
		float powerCap = 40f;
		float powerOut = 10f;
		int coreClass = 0;
		int housing = 1;
		int shoulders = 0;
		int backs = 0;
		float visionRange = 12f;
		float visionAngle = 50f;
		float close = 0.15f;
		float scanRange = 20f;
		float scanRes = 0.1f;
		bool canSprint = false;
		float sprintMult = 1.45f;
		float sprintHeat = 18f;
		float sprintLoad = 25f;
		var legMode = LegMode.Locked;
		var legType = LegType.Bipedal;
		var headAlive = true;

		foreach (var slot in EquipOrder)
		{
			if (!_hardpoints.TryGetValue(slot, out var hp) || hp.EquippedPart == null)
				continue;
			var p = hp.EquippedPart;
			if (hp.IsDestroyed)
			{
				if (slot == PartSlot.Head)
					headAlive = false;
				continue;
			}

			hull += p.Armor + p.HullBonus;
			speed += p.MaxSpeed;
			turn += p.TurnRateDegrees;
			fireBonus += p.FireRateBonus;
			heatCap += p.HeatCapBonus;
			dissipate += p.HeatDissipation;
			idleHeat += p.IdleHeatPerSec;
			moveHeat += p.MoveHeatPerSec;

			if (slot == PartSlot.Torso)
			{
				housing = Mathf.Max(1, p.PowerCoreHousing);
				shoulders = p.ShoulderMountCount;
				backs = p.BackpackMountCount;
			}
			else if (slot == PartSlot.PowerCore)
			{
				coreClass = p.PowerCoreClass;
				powerCap += p.PowerCapacity;
				powerOut += p.PowerOutput;
			}
			else if (slot == PartSlot.Head)
			{
				visionRange = p.VisionRange;
				visionAngle = p.VisionAngleDeg;
				close = p.CloseTargeting;
				scanRange = p.ScannerRange;
				scanRes = p.ScannerResolution;
			}
			else if (slot == PartSlot.Legs)
			{
				legMode = p.LegMode;
				legType = p.LegType;
				canSprint = p.CanSprint;
				sprintMult = p.SprintMultiplier > 0.1f ? p.SprintMultiplier : 1.45f;
				sprintHeat = p.SprintHeatPerSec;
				sprintLoad = p.SprintPowerLoad;
			}
		}

		if (!headAlive || !_hardpoints.ContainsKey(PartSlot.Head))
		{
			visionRange = 12f;
			visionAngle = 50f;
			close = 0.15f;
			scanRange = 20f;
			scanRes = 0.1f;
		}

		return new MechStats
		{
			HullHp = Mathf.Max(40f, hull),
			ShoulderMounts = shoulders,
			BackMounts = backs,
			PowerCoreClass = coreClass,
			PowerCoreHousing = housing,
			PowerCapacity = Mathf.Max(40f, powerCap),
			PowerOutput = Mathf.Max(5f, powerOut),
			HeatCap = Mathf.Max(40f, heatCap),
			HeatDissipation = Mathf.Max(2f, dissipate),
			IdleHeatPerSec = idleHeat,
			MoveHeatPerSec = moveHeat,
			VisionRange = visionRange,
			VisionAngleDeg = visionAngle,
			CloseTargeting = close,
			ScannerRange = scanRange,
			ScannerResolution = scanRes,
			WalkSpeed = Mathf.Max(2.2f, speed * 0.72f),
			TurnRateDegrees = Mathf.Max(18f, turn * 0.9f),
			FireRateMultiplier = Mathf.Max(0.25f, fireBonus),
			CanSprint = canSprint,
			SprintMultiplier = sprintMult,
			SprintHeatPerSec = sprintHeat,
			SprintPowerLoad = sprintLoad,
			LegMode = legMode,
			LegType = legType
		};
	}

	public void RefreshStatsAfterDamage()
	{
		Stats = DeriveStats();
	}

	public IEnumerable<PartData> GetActiveAbilityParts()
	{
		foreach (var slot in EquipOrder)
		{
			if (!_hardpoints.TryGetValue(slot, out var hp) || hp.EquippedPart == null)
				continue;
			if (hp.IsDestroyed)
				continue;
			if (hp.EquippedPart.GrantsActiveAbility)
				yield return hp.EquippedPart;
		}
	}

	private void EquipSlot(PartSlot slot, string partId)
	{
		var part = GameCatalog.GetPart(partId);
		if (part == null)
			return;

		if (!_socketNodes.TryGetValue(slot, out var socket))
			return;

		var hardpoint = new Hardpoint
		{
			Name = $"Hardpoint_{slot}",
			Slot = slot
		};
		socket.AddChild(hardpoint);
		hardpoint.Equip(part);
		_hardpoints[slot] = hardpoint;
	}

	private void ClearHardpoints()
	{
		foreach (var hp in _hardpoints.Values)
			MeshMat.QueueFreeSafe(hp);
		_hardpoints.Clear();
	}

	private void CacheSockets()
	{
		_socketNodes.Clear();
		if (_sockets == null)
			return;

		TryBind(PartSlot.Legs, "Hips");
		TryBind(PartSlot.Torso, "UpperBody/Torso");
		TryBind(PartSlot.Head, "UpperBody/Head");
		TryBind(PartSlot.PowerCore, "UpperBody/PowerCore");
		TryBind(PartSlot.WeaponL, "UpperBody/LeftArm");
		TryBind(PartSlot.WeaponR, "UpperBody/RightArm");
		TryBind(PartSlot.ShoulderL, "UpperBody/ShoulderL");
		TryBind(PartSlot.ShoulderR, "UpperBody/ShoulderR");
		TryBind(PartSlot.Backpack, "UpperBody/Back");
		TryBind(PartSlot.Systems, "UpperBody/Systems");
	}

	private void TryBind(PartSlot slot, string nodeName)
	{
		var node = _sockets!.GetNodeOrNull<Node3D>(nodeName);
		if (node != null)
			_socketNodes[slot] = node;
	}
}
