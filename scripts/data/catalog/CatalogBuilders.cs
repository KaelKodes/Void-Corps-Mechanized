using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Shared PartData factories for catalogue modules.</summary>
public static class CatalogBuilders
{
	public static ManufacturerData MakeManufacturer(string id, string name, Color color, string blurb, string niche) => new()
	{
		Id = id, DisplayName = name, AccentColor = color, Blurb = blurb, Niche = niche
	};

	public static PartData Empty(string id, string name, PartSlot slot) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = "trinova", Slot = slot,
		Tint = new Color(0.4f, 0.4f, 0.4f), VisualKind = "empty"
	};

	/// <summary>Baseline hold-to-thrust pack present on every legs kit.</summary>
	public const float StockJumpImpulse = 8f;
	public const float StockJumpDuration = 1.05f;
	public const float StockJumpPower = 12f;
	public const float StockJumpHeat = 7f;
	/// <summary>Baseline dash pack present on every legs kit.</summary>
	public const float StockDashSpeed = 24f;
	public const float StockDashDuration = 0.18f;
	public const float StockDashCooldown = 1.2f;
	public const float StockDashPower = 12f;
	public const float StockDashHeat = 9f;

	public static PartData Leg(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		float armor, float speed, float turn, string kind, LegMode mode, LegType type,
		bool canSprint, float sprintMult = 1.45f, float sprintHeat = 0f, float sprintLoad = 0f,
		float moveHeat = 2f, float idleHeat = 0.5f) =>
		WithBoosterAndThruster(new PartData
		{
			Id = id, DisplayName = name, ManufacturerId = mfg, Slot = PartSlot.Legs,
			Armor = armor, MaxSpeed = speed, TurnRateDegrees = turn, Tint = m[mfg].AccentColor,
			VisualKind = kind, LegMode = mode, LegType = type, CanSprint = canSprint,
			SprintMultiplier = sprintMult, SprintHeatPerSec = sprintHeat, SprintPowerLoad = sprintLoad,
			MoveHeatPerSec = moveHeat, IdleHeatPerSec = idleHeat
		},
			StockJumpImpulse, StockJumpDuration, StockJumpPower, StockJumpHeat,
			StockDashSpeed, StockDashDuration, StockDashCooldown, StockDashPower, StockDashHeat);

	/// <summary>Bipedal jump specialist — no sprint; stock thruster dash still included.</summary>
	public static PartData BoosterLegs(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		float armor, float speed, float turn,
		float jumpImpulse, float jumpDuration, float jumpPower, float jumpHeat,
		float moveHeat = 2f, float idleHeat = 0.5f) =>
		WithBoosterAndThruster(new PartData
		{
			Id = id, DisplayName = name, ManufacturerId = mfg, Slot = PartSlot.Legs,
			Armor = armor, MaxSpeed = speed, TurnRateDegrees = turn, Tint = m[mfg].AccentColor,
			VisualKind = "legs_biped", LegMode = LegMode.Locked, LegType = LegType.Bipedal,
			CanSprint = false,
			MoveHeatPerSec = moveHeat, IdleHeatPerSec = idleHeat
		},
			jumpImpulse, jumpDuration, jumpPower, jumpHeat,
			StockDashSpeed, StockDashDuration, StockDashCooldown, StockDashPower, StockDashHeat);

	/// <summary>Agile dash specialist — no sprint; stock booster flight still included.</summary>
	public static PartData ThrusterLegs(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		float armor, float speed, float turn, LegMode mode,
		float dashSpeed, float dashDuration, float dashCooldown, float dashPower, float dashHeat,
		float moveHeat = 2f, float idleHeat = 0.5f) =>
		WithBoosterAndThruster(new PartData
		{
			Id = id, DisplayName = name, ManufacturerId = mfg, Slot = PartSlot.Legs,
			Armor = armor, MaxSpeed = speed, TurnRateDegrees = turn, Tint = m[mfg].AccentColor,
			VisualKind = "legs_biped", LegMode = mode, LegType = LegType.Bipedal,
			CanSprint = false,
			MoveHeatPerSec = moveHeat, IdleHeatPerSec = idleHeat
		},
			StockJumpImpulse, StockJumpDuration, StockJumpPower, StockJumpHeat,
			dashSpeed, dashDuration, dashCooldown, dashPower, dashHeat);

	/// <summary>
	/// Jump + dash on one package. Every legs kit uses this (stock or tuned).
	/// Keeps whatever sprint flags the base kit already set.
	/// </summary>
	public static PartData WithBoosterAndThruster(
		PartData legs,
		float jumpImpulse, float jumpDuration, float jumpPower, float jumpHeat,
		float dashSpeed, float dashDuration, float dashCooldown, float dashPower, float dashHeat)
	{
		legs.MobilityModule = MobilityModuleKind.Both;
		legs.JumpImpulse = jumpImpulse;
		legs.JumpDuration = jumpDuration;
		legs.JumpPowerCost = jumpPower;
		legs.JumpHeat = jumpHeat;
		legs.DashSpeed = dashSpeed;
		legs.DashDuration = dashDuration;
		legs.DashCooldown = dashCooldown;
		legs.DashPowerCost = dashPower;
		legs.DashHeat = dashHeat;
		return legs;
	}

	public static PartData Torso(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		float armor, int housing, float structureHp, int shoulders, int backs, Vector3? scale = null,
		float heatCap = 10f, float idleHeat = 1f, string visualKind = "torso") => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = PartSlot.Torso,
		Armor = armor, Tint = m[mfg].AccentColor, VisualKind = visualKind,
		VisualScale = scale ?? Vector3.One, PowerCoreHousing = housing, StructureHp = structureHp,
		ShoulderMountCount = shoulders, BackpackMountCount = backs,
		HeatCapBonus = heatCap, IdleHeatPerSec = idleHeat
	};

	public static PartData Head(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		float armor, float turn, float visionRange, float visionAngle, float close,
		float scanRange, float scanRes, float idleHeat, float speed = 0f, float fireRateBonus = 0f,
		Vector3? scale = null, string visualKind = "head",
		ScanPenetrationMode scanPenetration = ScanPenetrationMode.Contact,
		ScanBlipStyle scanBlipStyle = ScanBlipStyle.WorldPip) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = PartSlot.Head,
		Armor = armor, TurnRateDegrees = turn, MaxSpeed = speed, Tint = m[mfg].AccentColor,
		VisualKind = visualKind, VisualScale = scale ?? Vector3.One, FireRateBonus = fireRateBonus,
		VisionRange = visionRange, VisionAngleDeg = visionAngle, CloseTargeting = close,
		ScannerRange = scanRange, ScannerResolution = scanRes, IdleHeatPerSec = idleHeat,
		ScanPenetration = scanPenetration, ScanBlipStyle = scanBlipStyle
	};

	public static PartData Core(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		int cls, float capacity, float output, float heatCap, float idleHeat, float dissipate,
		float armor = -1f) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = PartSlot.PowerCore,
		Tint = m[mfg].AccentColor, VisualKind = "core", PowerCoreClass = cls,
		PowerCapacity = capacity, PowerOutput = output, HeatCapBonus = heatCap,
		IdleHeatPerSec = idleHeat, HeatDissipation = dissipate,
		Armor = armor < 0f ? 5f + cls * 3f : armor
	};

	public static PartData Weapon(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		PartSlot slot, string kind, float damage, float fireRate, float range, float proj, AimMode aim,
		float heatShot, float powerLoad, WeaponFamily family,
		TargetingMode targeting = TargetingMode.Standard,
		bool? allowsFireElevation = null,
		int magazineSize = 0, float reloadTime = 0f) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = slot, VisualKind = kind,
		Tint = m[mfg].AccentColor, Damage = damage, FireRate = fireRate, Range = range,
		ProjectileSpeed = proj, AimMode = aim, TargetingMode = targeting,
		// Gimbaled always; fixed mounts default on so pilots can elevate for Titans,
		// but catalogs may pass false for true hard-fixed barrels.
		AllowsFireElevation = allowsFireElevation ?? true,
		HeatPerShot = heatShot, PowerPerShot = powerLoad, IdleHeatPerSec = 0.2f,
		WeaponFamily = family,
		MagazineSize = family == WeaponFamily.Ballistic ? Mathf.Max(1, magazineSize) : 0,
		ReloadTime = family == WeaponFamily.Ballistic ? Mathf.Max(0.35f, reloadTime) : 0f
	};

	public static PartData MeleeWeapon(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		PartSlot slot, string kind, float damage, float fireRate, float range,
		float heatShot, float powerLoad, float armor = 0f, float structureHp = 0f) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = slot, VisualKind = kind,
		Tint = m[mfg].AccentColor, Damage = damage, FireRate = fireRate, Range = range,
		ProjectileSpeed = 0f, AimMode = AimMode.Gimbaled, TargetingMode = TargetingMode.AimedComponent,
		HeatPerShot = heatShot, PowerPerShot = powerLoad, IdleHeatPerSec = 0.25f,
		WeaponFamily = WeaponFamily.Melee,
		Armor = armor, StructureHp = structureHp
	};

	public static PartData HeldShield(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		PartSlot slot, string kind, float arcDegrees, float raisePowerPerSec, float heatPerDamage,
		float armor, float structureHp, float powerReq = 9f) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = slot, VisualKind = kind,
		Tint = m[mfg].AccentColor, Damage = 0f, FireRate = 0f, Range = 0f,
		AimMode = AimMode.Fixed, IsHeldShield = true,
		ShieldArcDegrees = arcDegrees, ShieldPowerPerSec = raisePowerPerSec,
		ShieldHeatPerDamage = heatPerDamage, Armor = armor, StructureHp = structureHp,
		PowerRequirement = powerReq, IdleHeatPerSec = 0.15f, WeaponFamily = WeaponFamily.None
	};

	public static PartData AbilityPart(string id, string name, string mfg, Dictionary<string, ManufacturerData> m,
		PartSlot slot, string kind, float armor, AbilityId abilityId, float cd, float power,
		float heatBurst, float powerLoad, float damage = 0f, float range = 40f, float proj = 30f,
		float radius = 12f, float duration = 3f, float speed = 0f,
		WeaponFamily family = WeaponFamily.Missile,
		MissileGuidanceMode missileGuidance = MissileGuidanceMode.Paint) => new()
	{
		Id = id, DisplayName = name, ManufacturerId = mfg, Slot = slot, Armor = armor,
		Tint = m[mfg].AccentColor, VisualKind = kind, AbilityKind = AbilityKind.Active,
		AbilityId = abilityId, AbilityCooldown = cd, AbilityPower = power, AbilityRadius = radius,
		AbilityDuration = duration, Damage = damage, Range = range, ProjectileSpeed = proj,
		MaxSpeed = speed, AbilityHeatBurst = heatBurst, AbilityPowerLoad = powerLoad,
		IdleHeatPerSec = 0.3f, WeaponFamily = family, MissileGuidance = missileGuidance
	};
}
