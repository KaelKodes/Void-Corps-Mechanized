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

	public void Assemble(LoadoutData loadout, IReadOnlyDictionary<PartSlot, PartCondition>? conditions = null)
	{
		if (_sockets == null)
			_Ready();

		ClearHardpoints();
		GameCatalog.SanitizeMounts(loadout);

		foreach (var slot in EquipOrder)
		{
			if (!GameCatalog.IsMountAvailable(loadout, slot))
				continue;
			var partId = loadout.GetPartId(slot);
			EquipSlot(slot, partId);
			if (conditions != null
			    && conditions.TryGetValue(slot, out var condition)
			    && _hardpoints.TryGetValue(slot, out var hardpoint))
			{
				hardpoint.RestoreCondition(condition);
			}
		}

		FitMountSocketsToTorso(
			GameCatalog.GetPart(loadout.TorsoId),
			GameCatalog.GetPart(loadout.BackpackId));
		RefreshCockpitDependentVisuals(GameCatalog.GetPart(loadout.TorsoId));
		Stats = DeriveStats();
		ApplyMagazineConfig();
	}

	/// <summary>
	/// Torso VisualScale changes hull depth/width; keep Back / Systems / Shoulders on the hull surface.
	/// Matches PartVisualFactory torso main box: size (1.4, 1.05, 1.0)*scale at local (0, 0.55, 0).
	/// Backpack mounts disperse by kit — e.g. Ashwhisk stabilizer sits low like a tail; utility packs ride high.
	/// </summary>
	private void FitMountSocketsToTorso(PartData? torso, PartData? backpack = null)
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
		{
			// Stabilizer = lower-back dock flush to the hull. Utility packs ride the dorsal shelf.
			if (backpack?.VisualKind == "ash_stabilizer")
				back.Position = new Vector3(bodyCenter.X, bodyCenter.Y - 0.36f, backZ + 0.02f);
			else
				back.Position = new Vector3(bodyCenter.X, bodyCenter.Y + 0.32f, backZ + 0.02f);
		}

		if (_socketNodes.TryGetValue(PartSlot.Systems, out var systems))
		{
			// Keep systems off the tail root when the stabilizer owns the lower dock.
			if (backpack?.VisualKind == "ash_stabilizer")
				systems.Position = new Vector3(bodyCenter.X, bodyCenter.Y + 0.22f, backZ + 0.04f);
			else
				systems.Position = new Vector3(bodyCenter.X, bodyCenter.Y - 0.05f, backZ + 0.06f);
		}

		if (_socketNodes.TryGetValue(PartSlot.ShoulderL, out var shoulderL))
			shoulderL.Position = new Vector3(-(halfW + 0.08f), bodyCenter.Y + halfH * 0.5f, bodyCenter.Z + halfD * 0.12f);

		if (_socketNodes.TryGetValue(PartSlot.ShoulderR, out var shoulderR))
			shoulderR.Position = new Vector3(halfW + 0.08f, bodyCenter.Y + halfH * 0.5f, bodyCenter.Z + halfD * 0.12f);

		if (_socketNodes.TryGetValue(PartSlot.PowerCore, out var core))
		{
			if (torso?.IsCockpitHull == true)
			{
				// Aft cavity mount — keeps the core glow out of the viewport.
				core.Position = torsoSocket.Position + new Vector3(0f, 0.12f, 0.38f);
			}
			else
				core.Position = new Vector3(0f, 1.15f, -0.15f);
		}
	}

	/// <summary>Power-core mesh depends on whether the torso exposes a cockpit cavity.</summary>
	private void RefreshCockpitDependentVisuals(PartData? torso)
	{
		if (!_hardpoints.TryGetValue(PartSlot.PowerCore, out var coreHp))
			return;

		var encased = torso?.IsCockpitHull == true;
		if (coreHp.EquippedPart != null)
			coreHp.RebuildVisualForCockpit(encased);
	}

	public MechStats DeriveStats()
	{
		float torsoHp = 40f;
		float speed = 8f;
		float turn = 70f;
		float fireBonus = 1f;
		float heatCap = 40f;
		float dissipate = 6f;
		float idleHeat = 0.5f;
		float moveHeat = 0f;
		float powerCap = 0f;
		float powerGen = 0f;
		float powerReserved = 0f;
		var hasCore = false;
		int coreClass = 0;
		int housing = 1;
		int shoulders = 0;
		int backs = 0;
		float visionRange = 12f;
		float visionAngle = 50f;
		float close = 0.15f;
		float scanRange = 20f;
		float scanRes = 0.1f;
		float scanRangeBonus = 0f;
		float scanResBonus = 0f;
		var scanPenetration = ScanPenetrationMode.Contact;
		var scanBlipStyle = ScanBlipStyle.WorldPip;
		bool canSprint = false;
		float sprintMult = 1.45f;
		float sprintHeat = 18f;
		float sprintLoad = 25f;
		var legMode = LegMode.Locked;
		var legType = LegType.Bipedal;
		var headAlive = true;
		float totalWeight = 0f;
		float loadRating = 0f;
		var hasThruster = false;
		float dashSpeed = 0f;
		float dashDuration = 0.18f;
		float dashCooldown = 1.2f;
		float dashPower = 0f;
		float dashHeat = 0f;
		var hasBooster = false;
		float jumpImpulse = 0f;
		float jumpDuration = 1.1f;
		float jumpPower = 0f;
		float jumpHeat = 0f;
		int magazineBonus = 0;
		float reloadSpeedBonus = 0f;

		foreach (var slot in EquipOrder)
		{
			if (!_hardpoints.TryGetValue(slot, out var hp) || hp.EquippedPart == null)
				continue;
			var p = hp.EquippedPart;

			// Destroyed parts still weigh the chassis; losing an arm does not lighten the MAP.
			if (p.VisualKind != "empty")
				totalWeight += Mathf.Max(0f, p.Weight);

			if (hp.IsDestroyed)
			{
				if (slot == PartSlot.Head)
					headAlive = false;
				continue;
			}

			if (p.VisualKind != "empty")
				powerReserved += Mathf.Max(0f, p.PowerRequirement);

			speed += p.MaxSpeed;
			turn += p.TurnRateDegrees;
			fireBonus += p.FireRateBonus;
			heatCap += p.HeatCapBonus;
			dissipate += p.HeatDissipation;
			idleHeat += p.IdleHeatPerSec;
			moveHeat += p.MoveHeatPerSec;
			magazineBonus += Mathf.Max(0, p.MagazineBonus);
			reloadSpeedBonus += Mathf.Max(0f, p.ReloadSpeedBonus);

			if (slot == PartSlot.Torso)
			{
				torsoHp = Mathf.Max(1f, p.StructureHp);
				housing = Mathf.Max(1, p.PowerCoreHousing);
				shoulders = p.ShoulderMountCount;
				backs = p.BackpackMountCount;
			}
			else if (slot == PartSlot.PowerCore)
			{
				hasCore = true;
				coreClass = p.PowerCoreClass;
				powerCap = Mathf.Max(0f, p.PowerCapacity);
				powerGen = Mathf.Max(0f, p.PowerOutput);
			}
			else if (slot == PartSlot.Head)
			{
				visionRange = p.VisionRange;
				visionAngle = p.VisionAngleDeg;
				close = p.CloseTargeting;
				scanRange = p.ScannerRange;
				scanRes = p.ScannerResolution;
				if (p.ScanPenetration != ScanPenetrationMode.Inherit)
					scanPenetration = p.ScanPenetration;
				if (p.ScanBlipStyle != ScanBlipStyle.Inherit)
					scanBlipStyle = p.ScanBlipStyle;
			}
			else if (slot == PartSlot.Legs)
			{
				legMode = p.LegMode;
				legType = p.LegType;
				canSprint = p.CanSprint;
				sprintMult = p.SprintMultiplier > 0.1f ? p.SprintMultiplier : 1.45f;
				sprintHeat = p.SprintHeatPerSec;
				sprintLoad = p.SprintPowerLoad;
				loadRating = Mathf.Max(0f, p.LoadRating);
			}
			else
			{
				// Systems / backpack / mounts may enhance the head's passive scan.
				if (p.ScannerRange > 0.01f)
					scanRangeBonus += p.ScannerRange;
				if (p.ScannerResolution > 0.001f)
					scanResBonus += p.ScannerResolution;
				if (p.ScanPenetration != ScanPenetrationMode.Inherit)
					scanPenetration = p.ScanPenetration;
				if (p.ScanBlipStyle != ScanBlipStyle.Inherit)
					scanBlipStyle = p.ScanBlipStyle;
			}

			if (p.DashSpeed > 0.1f
			    && p.MobilityModule is MobilityModuleKind.Thruster or MobilityModuleKind.Both)
			{
				hasThruster = true;
				if (p.DashSpeed >= dashSpeed)
				{
					dashSpeed = p.DashSpeed;
					dashDuration = Mathf.Max(0.08f, p.DashDuration);
					dashCooldown = Mathf.Max(0.2f, p.DashCooldown);
					dashPower = Mathf.Max(0f, p.DashPowerCost);
					dashHeat = Mathf.Max(0f, p.DashHeat);
				}
			}

			if (p.JumpImpulse > 0.1f
			    && p.MobilityModule is MobilityModuleKind.Booster or MobilityModuleKind.Both)
			{
				hasBooster = true;
				if (p.JumpImpulse >= jumpImpulse)
				{
					jumpImpulse = p.JumpImpulse;
					jumpDuration = Mathf.Max(0.08f, p.JumpDuration);
					jumpPower = Mathf.Max(0f, p.JumpPowerCost);
					jumpHeat = Mathf.Max(0f, p.JumpHeat);
				}
			}
		}

		if (!headAlive || !_hardpoints.ContainsKey(PartSlot.Head))
		{
			visionRange = 12f;
			visionAngle = 50f;
			close = 0.15f;
			scanRange = 20f;
			scanRes = 0.1f;
			scanRangeBonus = 0f;
			scanResBonus = 0f;
			scanPenetration = ScanPenetrationMode.LineOfSight;
			scanBlipStyle = ScanBlipStyle.WorldPip;
		}
		else
		{
			scanRange += scanRangeBonus;
			scanRes += scanResBonus;
		}

		// No living core → no capacity / generation (destroyed or unequipped).
		if (!hasCore)
		{
			powerCap = 0f;
			powerGen = 0f;
		}

		powerReserved = Mathf.Max(0f, powerReserved);
		var operational = Mathf.Max(0f, powerCap - powerReserved);
		var (weightMove, weightTurn, loadRatio) =
			CatalogWeight.ComputeOverloadMultipliers(totalWeight, loadRating);

		return new MechStats
		{
			TorsoHp = torsoHp,
			ShoulderMounts = shoulders,
			BackMounts = backs,
			PowerCoreClass = coreClass,
			PowerCoreHousing = housing,
			PowerCapacity = powerCap,
			PowerGeneration = Mathf.Max(0f, powerGen),
			PowerReserved = powerReserved,
			OperationalMax = operational,
			HeatCap = Mathf.Max(40f, heatCap),
			HeatDissipation = Mathf.Max(2f, dissipate),
			IdleHeatPerSec = idleHeat,
			MoveHeatPerSec = moveHeat,
			VisionRange = visionRange,
			VisionAngleDeg = visionAngle,
			CloseTargeting = close,
			ScannerRange = scanRange,
			ScannerResolution = scanRes,
			ScanRequiresLos = scanPenetration == ScanPenetrationMode.LineOfSight,
			ScanBlipStyle = scanBlipStyle,
			MagazineBonus = magazineBonus,
			ReloadSpeedBonus = reloadSpeedBonus,
			WalkSpeed = Mathf.Max(2.2f, speed * 0.72f),
			TurnRateDegrees = Mathf.Max(18f, turn * 0.9f),
			FireRateMultiplier = Mathf.Max(0.25f, fireBonus),
			CanSprint = canSprint,
			SprintMultiplier = sprintMult,
			SprintHeatPerSec = sprintHeat,
			SprintPowerLoad = sprintLoad,
			LegMode = legMode,
			LegType = legType,
			HasThruster = hasThruster,
			DashSpeed = dashSpeed,
			DashDuration = dashDuration,
			DashCooldown = dashCooldown,
			DashPowerCost = dashPower,
			DashHeat = dashHeat,
			HasBooster = hasBooster,
			JumpImpulse = jumpImpulse,
			JumpDuration = jumpDuration,
			JumpPowerCost = jumpPower,
			JumpHeat = jumpHeat,
			TotalWeight = totalWeight,
			LoadRating = loadRating,
			LoadRatio = loadRatio,
			WeightMoveMultiplier = weightMove,
			WeightTurnMultiplier = weightTurn
		};
	}

	public void RefreshStatsAfterDamage()
	{
		Stats = DeriveStats();
		ApplyMagazineConfig();
	}

	private void ApplyMagazineConfig()
	{
		var bonus = Stats.MagazineBonus;
		foreach (var slot in new[] { PartSlot.WeaponL, PartSlot.WeaponR })
		{
			if (_hardpoints.TryGetValue(slot, out var hp))
				hp.ConfigureMagazine(bonus);
		}
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
