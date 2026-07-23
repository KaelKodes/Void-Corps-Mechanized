namespace Mechanize;

/// <summary>Themed field cover / set-dressing for claim arenas.</summary>
public enum CoverKind
{
	ShippingContainer,
	ContainerStack,
	ConcreteBarrier,
	BarrierRow,
	OilTank,
	OilTankCluster,
	SemiTrailer,
	Warehouse,
	IndustrialShed,
	Skyscraper,
	PipeRack,
	/// <summary>Thin E–W cargo deck with underpass clearance (~3.2 m) and end pillars.</summary>
	CargoOverpass,
	/// <summary>Walkable ramp (≤45°) rising along local +X to meet a mid deck.</summary>
	DockRamp,
	/// <summary>Short raised dock ledge (~2.5 m) with thin walkable top.</summary>
	DockLedge,
	/// <summary>Hollow service corridor (open ends); walls + walkable roof, interior ~3.2×4.5 m.</summary>
	ServiceTunnel
}
