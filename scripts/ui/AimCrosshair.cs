using Godot;

namespace Mechanize;

/// <summary>
/// Center-screen aim reticle with HEAT brackets ( ) for L/R arm heat.
/// Wide while moving; tight when planted for the stationary bonus.
/// </summary>
public partial class AimCrosshair : Control
{
	private CrosshairStyle _style = CrosshairStyle.Cross;
	private float _wideT;
	private float _targetWideT = 1f;
	private bool _canFire = true;
	private bool _showReticle;
	private bool _showArmHeat;
	private float _armHeatL;
	private float _armHeatR;
	private bool _drawArmL;
	private bool _drawArmR;

	private const float WideExtent = 58f;
	private const float PreciseScale = 0.34f;
	private const float CornerArm = 16f;
	private const float LineWidthWide = 2f;
	private const float LineWidthPrecise = 2.5f;
	private const float ArmBracketOffsetX = 78f;
	private const float ArmBracketHeight = 58f;
	private const float ArmBracketHalfWidth = 12f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
		OffsetLeft = 0f;
		OffsetTop = 0f;
		OffsetRight = 0f;
		OffsetBottom = 0f;
	}

	public override void _Process(double delta)
	{
		if (!_showReticle && !_showArmHeat)
			return;

		var dt = (float)delta;
		_wideT = Mathf.Lerp(_wideT, _targetWideT, 1f - Mathf.Exp(-14f * dt));
		QueueRedraw();
	}

	public void Refresh(MechController? mech)
	{
		if (mech == null || !mech.IsPlayerControlled || !mech.ControlsEnabled)
		{
			_showReticle = false;
			_showArmHeat = false;
			Visible = false;
			return;
		}

		var hasRanged = CrosshairStyleUtil.TryHasRangedWeapon(mech);
		var hasArmL = TryLivingArm(mech, PartSlot.WeaponL);
		var hasArmR = TryLivingArm(mech, PartSlot.WeaponR);

		_showReticle = hasRanged;
		// HEAT lives on the crosshair brackets, not a HUD bar.
		_showArmHeat = hasArmL || hasArmR;
		_drawArmL = hasArmL;
		_drawArmR = hasArmR;
		Visible = _showReticle || _showArmHeat;

		if (!_showReticle && !_showArmHeat)
			return;

		if (_showReticle)
		{
			var firePrimary = Input.IsActionPressed("fire_primary");
			var fireSecondary = Input.IsActionPressed("fire_secondary");
			_style = CrosshairStyleUtil.ResolveActive(mech, firePrimary, fireSecondary);
			_canFire = mech.CanFireWeapons;
			_targetWideT = mech.IsWideFire ? 1f : 0f;
		}

		var power = mech.PowerHeat;
		_armHeatL = power?.ArmHeatRatioL ?? 0f;
		_armHeatR = power?.ArmHeatRatioR ?? 0f;
	}

	public override void _Draw()
	{
		if (!_showReticle && !_showArmHeat)
			return;

		var center = Size * 0.5f;
		var scale = Mathf.Lerp(PreciseScale, 1f, _wideT);

		if (_showReticle)
		{
			var extent = WideExtent * scale;
			var arm = CornerArm * scale;
			var alpha = _canFire ? Mathf.Lerp(0.92f, 0.72f, _wideT) : 0.38f;
			var color = new Color(1f, 1f, 1f, alpha);
			var width = Mathf.Lerp(LineWidthPrecise, LineWidthWide, _wideT);

			DrawCornerFrame(center, extent, arm, color, width);

			switch (_style)
			{
				case CrosshairStyle.Chevron:
					DrawChevron(center, scale, color, width);
					break;
				case CrosshairStyle.X:
					DrawX(center, scale, color, width);
					break;
				default:
					DrawCross(center, scale, color, width);
					break;
			}
		}

		if (_showArmHeat)
		{
			if (_drawArmL)
				DrawArmHeatBracket(center, -ArmBracketOffsetX, _armHeatL, leftSide: true);
			if (_drawArmR)
				DrawArmHeatBracket(center, ArmBracketOffsetX, _armHeatR, leftSide: false);
		}
	}

	private static bool TryLivingArm(MechController mech, PartSlot slot)
	{
		var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
		return hp?.EquippedPart != null && hp.EquippedPart.VisualKind != "empty" && !hp.IsDestroyed;
	}

	private void DrawArmHeatBracket(Vector2 center, float offsetX, float ratio, bool leftSide)
	{
		var anchor = center + new Vector2(offsetX, 0f);
		var color = HeatBracketColor(ratio);
		var width = Mathf.Lerp(2f, 3.5f, ratio);
		const int segments = 14;
		var startAngle = leftSide ? Mathf.DegToRad(250f) : Mathf.DegToRad(-70f);
		var endAngle = leftSide ? Mathf.DegToRad(110f) : Mathf.DegToRad(70f);
		var filledEnd = Mathf.Lerp(startAngle, endAngle, Mathf.Clamp(ratio, 0.04f, 1f));

		var prev = anchor + BracketPoint(startAngle, leftSide);
		for (var i = 1; i <= segments; i++)
		{
			var t = (float)i / segments;
			var angle = Mathf.Lerp(startAngle, filledEnd, t);
			var next = anchor + BracketPoint(angle, leftSide);
			DrawLine(prev, next, color, width);
			prev = next;
		}
	}

	private Vector2 BracketPoint(float angle, bool leftSide)
	{
		var x = Mathf.Cos(angle) * ArmBracketHalfWidth * (leftSide ? 1f : -1f);
		var y = Mathf.Sin(angle) * ArmBracketHeight * 0.5f;
		return new Vector2(x, y);
	}

	private static Color HeatBracketColor(float ratio)
	{
		if (ratio <= 0.01f)
			return new Color(0.82f, 0.88f, 0.92f, 0.42f);
		if (ratio < 0.55f)
			return new Color(0.95f, 0.75f, 0.35f, Mathf.Lerp(0.55f, 0.9f, ratio / 0.55f));
		return new Color(0.98f, 0.42f, 0.22f, Mathf.Lerp(0.85f, 1f, (ratio - 0.55f) / 0.45f));
	}

	private void DrawCornerFrame(Vector2 c, float extent, float arm, Color color, float width)
	{
		// Top-left
		DrawLine(c + new Vector2(-extent, -extent), c + new Vector2(-extent + arm, -extent), color, width);
		DrawLine(c + new Vector2(-extent, -extent), c + new Vector2(-extent, -extent + arm), color, width);
		// Top-right
		DrawLine(c + new Vector2(extent, -extent), c + new Vector2(extent - arm, -extent), color, width);
		DrawLine(c + new Vector2(extent, -extent), c + new Vector2(extent, -extent + arm), color, width);
		// Bottom-left
		DrawLine(c + new Vector2(-extent, extent), c + new Vector2(-extent + arm, extent), color, width);
		DrawLine(c + new Vector2(-extent, extent), c + new Vector2(-extent, extent - arm), color, width);
		// Bottom-right
		DrawLine(c + new Vector2(extent, extent), c + new Vector2(extent - arm, extent), color, width);
		DrawLine(c + new Vector2(extent, extent), c + new Vector2(extent, extent - arm), color, width);
	}

	private void DrawCross(Vector2 c, float scale, Color color, float width)
	{
		var half = 20f * scale;
		DrawLine(c + new Vector2(-half, 0f), c + new Vector2(half, 0f), color, width);
		DrawLine(c + new Vector2(0f, -half), c + new Vector2(0f, half), color, width);
	}

	private void DrawChevron(Vector2 c, float scale, Color color, float width)
	{
		var inset = 26f * scale;
		var rise = 18f * scale;
		var bottom = c.Y + inset * 0.35f;
		DrawLine(c + new Vector2(-inset, bottom), c + new Vector2(0f, bottom - rise), color, width);
		DrawLine(c + new Vector2(inset, bottom), c + new Vector2(0f, bottom - rise), color, width);
	}

	private void DrawX(Vector2 c, float scale, Color color, float width)
	{
		var outer = 24f * scale;
		var inner = 7f * scale;
		DrawLine(c + new Vector2(-outer, -outer), c + new Vector2(-inner, -inner), color, width);
		DrawLine(c + new Vector2(outer, -outer), c + new Vector2(inner, -inner), color, width);
		DrawLine(c + new Vector2(-outer, outer), c + new Vector2(-inner, inner), color, width);
		DrawLine(c + new Vector2(outer, outer), c + new Vector2(inner, inner), color, width);
	}
}
