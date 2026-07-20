using Godot;

namespace Mechanize;

/// <summary>
/// Center-screen aim reticle. Wide while moving; tight when planted for the stationary bonus.
/// </summary>
public partial class AimCrosshair : Control
{
	private CrosshairStyle _style = CrosshairStyle.Cross;
	private float _wideT;
	private float _targetWideT = 1f;
	private bool _canFire = true;
	private bool _show;

	private const float WideExtent = 58f;
	private const float PreciseScale = 0.34f;
	private const float CornerArm = 16f;
	private const float LineWidthWide = 2f;
	private const float LineWidthPrecise = 2.5f;

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
		if (!_show)
			return;

		var dt = (float)delta;
		_wideT = Mathf.Lerp(_wideT, _targetWideT, 1f - Mathf.Exp(-14f * dt));
		QueueRedraw();
	}

	public void Refresh(MechController? mech)
	{
		if (mech == null || !mech.IsPlayerControlled || !mech.ControlsEnabled)
		{
			_show = false;
			Visible = false;
			return;
		}

		if (!CrosshairStyleUtil.TryHasRangedWeapon(mech))
		{
			_show = false;
			Visible = false;
			return;
		}

		var firePrimary = Input.IsActionPressed("fire_primary");
		var fireSecondary = Input.IsActionPressed("fire_secondary");

		_show = true;
		Visible = true;
		_style = CrosshairStyleUtil.ResolveActive(mech, firePrimary, fireSecondary);
		_canFire = mech.CanFireWeapons;
		_targetWideT = mech.IsWideFire ? 1f : 0f;
	}

	public override void _Draw()
	{
		if (!_show)
			return;

		var center = Size * 0.5f;
		var scale = Mathf.Lerp(PreciseScale, 1f, _wideT);
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
