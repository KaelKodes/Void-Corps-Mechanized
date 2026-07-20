namespace Mechanize;

public enum CrosshairStyle
{
	Cross,
	Chevron,
	X
}

public static class CrosshairStyleUtil
{
	public static CrosshairStyle FromPart(PartData? part)
	{
		if (part == null || part.VisualKind == "empty")
			return CrosshairStyle.Cross;
		if (part.WeaponFamily == WeaponFamily.Energy)
			return CrosshairStyle.X;
		if (part.AimMode == AimMode.Gimbaled)
			return CrosshairStyle.Chevron;
		return CrosshairStyle.Cross;
	}

	public static CrosshairStyle ResolveActive(MechController mech, bool firePrimary, bool fireSecondary)
	{
		PartData? part = null;
		if (fireSecondary && TryRangedPart(mech, PartSlot.WeaponR, out var r))
			part = r;
		else if (firePrimary && TryRangedPart(mech, PartSlot.WeaponL, out var l))
			part = l;
		else if (TryRangedPart(mech, PartSlot.WeaponL, out var left))
			part = left;
		else if (TryRangedPart(mech, PartSlot.WeaponR, out var right))
			part = right;

		return FromPart(part);
	}

	public static bool TryHasRangedWeapon(MechController mech) =>
		TryRangedPart(mech, PartSlot.WeaponL, out _) || TryRangedPart(mech, PartSlot.WeaponR, out _);

	private static bool TryRangedPart(MechController mech, PartSlot slot, out PartData part)
	{
		part = null!;
		var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
		if (hp?.EquippedPart == null || hp.IsDestroyed)
			return false;
		var p = hp.EquippedPart;
		if (p.VisualKind == "empty" || p.IsHeldShield || p.WeaponFamily == WeaponFamily.Melee)
			return false;
		part = p;
		return true;
	}
}
