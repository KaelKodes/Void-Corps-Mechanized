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
	/// <summary>Structural health for this component. For legs, this is health per limb.</summary>
	[Export] public float StructureHp { get; set; }
	[Export] public TargetingMode TargetingMode { get; set; } = TargetingMode.Standard;

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

	/// <summary>Mass units. Soft mobility constraint vs leg LoadRating.</summary>
	[Export] public float Weight { get; set; }
	/// <summary>Legs only: total assembled weight this package is rated to carry.</summary>
	[Export] public float LoadRating { get; set; }

	// --- Legs / sprint ---
	[Export] public bool CanSprint { get; set; }
	[Export] public float SprintMultiplier { get; set; } = 1.45f;
	[Export] public float SprintHeatPerSec { get; set; }
	[Export] public float SprintPowerLoad { get; set; }

	public bool GrantsActiveAbility => AbilityKind == AbilityKind.Active && AbilityId != AbilityId.None;

	public bool ProvidesMount(PartSlot mount) => Slot == PartSlot.Torso && mount switch
	{
		PartSlot.ShoulderL => ShoulderMountCount >= 1,
		PartSlot.ShoulderR => ShoulderMountCount >= 2,
		PartSlot.Backpack => BackpackMountCount >= 1,
		_ => false
	};
}
