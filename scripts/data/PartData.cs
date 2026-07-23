using Godot;

namespace Mechanize;

[GlobalClass]
public partial class PartData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string ManufacturerId { get; set; } = "";
	/// <summary>1 = Field, 2 = Claim, 3 = Threat. Gates shop/loot by sector.</summary>
	[Export] public int Tier { get; set; } = 1;
	[Export] public PartSlot Slot { get; set; }
	[Export] public AimMode AimMode { get; set; } = AimMode.Fixed;
	/// <summary>
	/// While holding fire, scroll can raise/lower shot height (visual barrel pitch + projectile spawn height).
	/// Does not change ballistic range angle — shots stay level. Gimbaled guns usually enable this;
	/// fixed mounts may opt in for limited vertical bias (Titan work, leg sweeps).
	/// </summary>
	[Export] public bool AllowsFireElevation { get; set; }
	[Export] public LegMode LegMode { get; set; } = LegMode.Locked;
	[Export] public LegType LegType { get; set; } = LegType.Bipedal;

	/// <summary>Permanent local mitigation for this component; does not deplete.</summary>
	[Export] public float Armor { get; set; }
	[Export] public float MaxSpeed { get; set; }
	[Export] public float TurnRateDegrees { get; set; }
	[Export] public float Damage { get; set; }
	[Export] public float FireRate { get; set; } = 2f;
	[Export] public float Range { get; set; } = 40f;
	[Export] public float ProjectileSpeed { get; set; } = 45f;
	/// <summary>Structural health for this component. For legs, per-limb budget into a shared package pool.</summary>
	[Export] public float StructureHp { get; set; }
	[Export] public TargetingMode TargetingMode { get; set; } = TargetingMode.Standard;
	/// <summary>Missile ability aim mode. Non-missile abilities ignore this.</summary>
	[Export] public MissileGuidanceMode MissileGuidance { get; set; } = MissileGuidanceMode.Paint;

	[Export] public AbilityKind AbilityKind { get; set; } = AbilityKind.None;
	[Export] public AbilityId AbilityId { get; set; } = AbilityId.None;
	[Export] public float AbilityCooldown { get; set; } = 8f;
	[Export] public float AbilityDuration { get; set; } = 3f;
	[Export] public float AbilityRadius { get; set; } = 12f;
	[Export] public float AbilityPower { get; set; } = 1f;
	[Export] public float FireRateBonus { get; set; }

	[Export] public Color Tint { get; set; } = Colors.White;
	[Export] public Vector3 VisualScale { get; set; } = Vector3.One;
	[Export] public string VisualKind { get; set; } = "box";

	/// <summary>Hollow Mech 2.0 hull with CockpitAnchor (see <see cref="CockpitHullRegistry"/>).</summary>
	public bool IsCockpitHull => CockpitHullRegistry.IsCockpitHull(VisualKind);
	// --- Torso mounts / housing ---
	[Export] public int ShoulderMountCount { get; set; }
	[Export] public int BackpackMountCount { get; set; }
	[Export] public int PowerCoreHousing { get; set; }

	// --- Power core ---
	[Export] public int PowerCoreClass { get; set; }
	[Export] public float PowerCapacity { get; set; }
	/// <summary>Power generated per second into the operational pool.</summary>
	[Export] public float PowerOutput { get; set; }
	/// <summary>Permanent standby reservation against core capacity. Cores use 0.</summary>
	[Export] public float PowerRequirement { get; set; }

	// --- Heat ---
	[Export] public float HeatCapBonus { get; set; }
	[Export] public float HeatDissipation { get; set; }
	[Export] public float IdleHeatPerSec { get; set; }
	[Export] public float MoveHeatPerSec { get; set; }
	[Export] public float HeatPerShot { get; set; }
	/// <summary>Operational power spent once per successful weapon shot.</summary>
	[Export] public float PowerPerShot { get; set; }
	/// <summary>
	/// Ballistic magazine capacity. 0 = no magazine (energy / melee / non-guns).
	/// </summary>
	[Export] public int MagazineSize { get; set; }
	/// <summary>Seconds to refill a ballistic magazine. Ignored when <see cref="MagazineSize"/> is 0.</summary>
	[Export] public float ReloadTime { get; set; }
	/// <summary>Utility bonus added to ballistic magazine size while this part is alive.</summary>
	[Export] public int MagazineBonus { get; set; }
	/// <summary>
	/// Utility reload speed bonus. Effective reload = ReloadTime / (1 + sum of bonuses).
	/// </summary>
	[Export] public float ReloadSpeedBonus { get; set; }
	/// <summary>Burst spend on ability activate, or per-second drain while channelling pulse repair.</summary>
	[Export] public float AbilityPowerLoad { get; set; }
	[Export] public float AbilityHeatBurst { get; set; }
	[Export] public WeaponFamily WeaponFamily { get; set; } = WeaponFamily.None;

	/// <summary>Arm-slot held shield. Hold fire bind to raise; does not shoot.</summary>
	[Export] public bool IsHeldShield { get; set; }
	[Export] public float ShieldArcDegrees { get; set; } = 120f;
	[Export] public float ShieldPowerPerSec { get; set; } = 14f;
	/// <summary>Heat gained per point of damage absorbed while raised.</summary>
	[Export] public float ShieldHeatPerDamage { get; set; } = 0.5f;

	// --- Head sensors ---
	[Export] public float VisionRange { get; set; }
	[Export] public float VisionAngleDeg { get; set; }
	[Export] public float CloseTargeting { get; set; }
	[Export] public float ScannerRange { get; set; }
	[Export] public float ScannerResolution { get; set; }
	/// <summary>
	/// Passive contact-scan cover rule. Heads set baseline; Systems/Backpack may override
	/// with a non-<see cref="ScanPenetrationMode.Inherit"/> value. Live X-ray is never granted.
	/// </summary>
	[Export] public ScanPenetrationMode ScanPenetration { get; set; } = ScanPenetrationMode.Inherit;
	/// <summary>Last-known blip presentation. Heads set baseline; enhancers may override.</summary>
	[Export] public ScanBlipStyle ScanBlipStyle { get; set; } = ScanBlipStyle.Inherit;

	/// <summary>Mass units. Soft mobility constraint vs leg LoadRating.</summary>
	[Export] public float Weight { get; set; }
	/// <summary>Legs only: total assembled weight this package is rated to carry.</summary>
	[Export] public float LoadRating { get; set; }

	// --- Legs / sprint ---
	[Export] public bool CanSprint { get; set; }
	[Export] public float SprintMultiplier { get; set; } = 1.45f;
	[Export] public float SprintHeatPerSec { get; set; }
	[Export] public float SprintPowerLoad { get; set; }

	// --- Leg mobility modules (Boosters = jump, Thrusters = dash) ---
	[Export] public MobilityModuleKind MobilityModule { get; set; } = MobilityModuleKind.None;
	[Export] public float DashSpeed { get; set; }
	[Export] public float DashDuration { get; set; } = 0.18f;
	[Export] public float DashCooldown { get; set; } = 1.2f;
	[Export] public float DashPowerCost { get; set; }
	[Export] public float DashHeat { get; set; }
	/// <summary>Climb thrust while boosters fire (m/s target up-rate).</summary>
	[Export] public float JumpImpulse { get; set; }
	/// <summary>Booster fuel — seconds of hold-to-thrust flight before landing refill.</summary>
	[Export] public float JumpDuration { get; set; } = 1.1f;
	/// <summary>Power draw per second while thrusting (sprint-style continuous load).</summary>
	[Export] public float JumpPowerCost { get; set; }
	/// <summary>Heat per second while thrusting.</summary>
	[Export] public float JumpHeat { get; set; }

	public bool GrantsActiveAbility => AbilityKind == AbilityKind.Active && AbilityId != AbilityId.None;

	public bool ProvidesMount(PartSlot mount) => Slot == PartSlot.Torso && mount switch
	{
		PartSlot.ShoulderL => ShoulderMountCount >= 1,
		PartSlot.ShoulderR => ShoulderMountCount >= 2,
		PartSlot.Backpack => BackpackMountCount >= 1,
		_ => false
	};
}
