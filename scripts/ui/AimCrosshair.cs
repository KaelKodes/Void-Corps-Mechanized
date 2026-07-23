using Godot;

namespace Mechanize;

/// <summary>
/// Center-screen aim reticle. Wide while moving; tight when planted.
/// Overall chassis heat: horizontal warning bar under the reticle (shows from 60%+).
/// OVERHEAT cue under this bar when overheated and glass meters are not live (FP).
/// </summary>
public partial class AimCrosshair : Control
{
	private CrosshairStyle _style = CrosshairStyle.Cross;
	private float _wideT;
	private float _targetWideT = 1f;
	private bool _canFire = true;
	private bool _showReticle;

	private float _heatRatio;
	/// <summary>1 while heat ≥ 60%; fades to 0 after cooling below threshold.</summary>
	private float _heatWarnT;
	private bool _heatTracking;
	private bool _showChassisOverheat;

	private const float WideExtent = 58f;
	private const float PreciseScale = 0.34f;
	private const float CornerArm = 16f;
	private const float LineWidthWide = 2f;
	private const float LineWidthPrecise = 2.5f;

	private const float HeatWarnThreshold = 0.6f;
	private const float HeatBarHalfWidth = 42f;
	private const float HeatBarHeight = 5f;
	private const float HeatBarGapBelow = 14f;
	private const float HeatFadeSpeed = 2.8f;

	private static readonly Color HeatAtWarn = new(1f, 0.72f, 0.18f);   // yellowish orange @ 60%
	private static readonly Color HeatAtCap = new(0.72f, 0.06f, 0.04f);  // deep red @ 100%

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
		OffsetLeft = 0f;
		OffsetTop = 0f;
		OffsetRight = 0f;
		OffsetBottom = 0f;
		ZIndex = 40;
	}

	public override void _Process(double delta)
	{
		if (!_showReticle && !_heatTracking && _heatWarnT <= 0.001f && !_showChassisOverheat)
			return;

		var dt = (float)delta;
		if (_showReticle)
			_wideT = Mathf.Lerp(_wideT, _targetWideT, 1f - Mathf.Exp(-14f * dt));

		if (_heatRatio >= HeatWarnThreshold)
			_heatWarnT = 1f;
		else if (_heatWarnT > 0f)
			_heatWarnT = Mathf.MoveToward(_heatWarnT, 0f, HeatFadeSpeed * dt);

		if (!_heatTracking && _heatWarnT <= 0.001f)
			_heatWarnT = 0f;

		Visible = _showReticle || _heatWarnT > 0.001f || _showChassisOverheat;
		QueueRedraw();
	}

	public void Refresh(MechController? mech)
	{
		if (mech == null || !mech.IsPlayerControlled || !mech.ControlsEnabled)
		{
			_showReticle = false;
			_heatTracking = false;
			_heatRatio = 0f;
			_showChassisOverheat = false;
			if (_heatWarnT <= 0.001f)
				Visible = false;
			return;
		}

		_heatTracking = true;
		var power = mech.PowerHeat;
		_heatRatio = Mathf.Clamp(power?.HeatRatio ?? 0f, 0f, 1f);
		var firstPerson = mech.GetViewport()?.GetCamera3D() is TopDownCamera { IsFirstPerson: true };
		var glassMetersLive = firstPerson && CockpitDiegeticHud.MechHasCockpitScreens(mech);
		// Glass left bar owns OVERHEAT in FP; center cue covers top-down / no-cockpit kits.
		_showChassisOverheat = power?.IsOverheated == true && !glassMetersLive;

		_showReticle = CrosshairStyleUtil.TryHasRangedWeapon(mech);
		if (_showReticle)
		{
			MoveToFront();
			var firePrimary = Input.IsActionPressed("fire_primary");
			var fireSecondary = Input.IsActionPressed("fire_secondary");
			_style = CrosshairStyleUtil.ResolveActive(mech, firePrimary, fireSecondary);
			_canFire = mech.CanFireWeapons;
			_targetWideT = mech.IsWideFire ? 1f : 0f;
		}

		Visible = _showReticle || _heatWarnT > 0.001f || _heatRatio >= HeatWarnThreshold || _showChassisOverheat;
		if (Visible)
			MoveToFront();
	}

	public override void _Draw()
	{
		var center = Size * 0.5f;

		if (_showReticle)
			DrawReticle(center);

		if (_heatWarnT > 0.001f || _showChassisOverheat)
			DrawHeatBar(center);
	}

	private void DrawReticle(Vector2 center)
	{
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

	private void DrawHeatBar(Vector2 center)
	{
		var scale = _showReticle ? Mathf.Lerp(PreciseScale, 1f, _wideT) : 1f;
		var extent = WideExtent * scale;
		var y = center.Y + extent + HeatBarGapBelow;
		var halfW = HeatBarHalfWidth;
		var h = HeatBarHeight;

		var trackA = 0.22f * Mathf.Max(_heatWarnT, _showChassisOverheat ? 1f : 0f);
		var track = new Color(0.08f, 0.08f, 0.1f, trackA);
		var trackRect = new Rect2(center.X - halfW, y - h * 0.5f, halfW * 2f, h);
		DrawRect(trackRect, track);

		var fill = Mathf.Clamp(_heatRatio, 0f, 1f);
		if (fill >= 0.01f && _heatWarnT > 0.001f)
		{
			var heatColor = HeatColor(_heatRatio, _heatWarnT);
			var fillW = halfW * 2f * fill;
			var fillRect = new Rect2(center.X - halfW, y - h * 0.5f, fillW, h);
			DrawRect(fillRect, heatColor);

			var edge = new Color(heatColor.R, heatColor.G, heatColor.B, heatColor.A * 0.85f);
			DrawLine(
				new Vector2(center.X - halfW, y - h * 0.5f),
				new Vector2(center.X - halfW + fillW, y - h * 0.5f),
				edge,
				1.2f);
		}

		if (!_showChassisOverheat)
			return;

		var label = "OVERHEAT";
		var font = ThemeDB.FallbackFont;
		var fontSize = 13;
		var textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		var textPos = new Vector2(center.X - textSize.X * 0.5f, y + h * 0.5f + 14f);
		DrawString(font, textPos + new Vector2(1f, 1f), label, HorizontalAlignment.Left, -1, fontSize,
			new Color(0f, 0f, 0f, 0.65f));
		DrawString(font, textPos, label, HorizontalAlignment.Left, -1, fontSize,
			new Color(1f, 0.35f, 0.2f, 0.95f));
	}

	/// <summary>
	/// 60% → yellowish orange, 100% → deep red.
	/// While fading out below 60%, lerp toward white and drop alpha with <paramref name="warnT"/>.
	/// </summary>
	private static Color HeatColor(float ratio, float warnT)
	{
		Color baseCol;
		if (ratio >= HeatWarnThreshold)
		{
			var t = Mathf.Clamp((ratio - HeatWarnThreshold) / (1f - HeatWarnThreshold), 0f, 1f);
			baseCol = HeatAtWarn.Lerp(HeatAtCap, t);
		}
		else
		{
			// Cooling under threshold: wash toward white as warnT falls.
			var wash = 1f - warnT;
			baseCol = HeatAtWarn.Lerp(Colors.White, wash);
		}

		baseCol.A = Mathf.Clamp(0.55f + 0.4f * Mathf.Clamp(ratio, 0f, 1f), 0.4f, 0.95f) * warnT;
		return baseCol;
	}

	private void DrawCornerFrame(Vector2 c, float extent, float arm, Color color, float width)
	{
		DrawLine(c + new Vector2(-extent, -extent), c + new Vector2(-extent + arm, -extent), color, width);
		DrawLine(c + new Vector2(-extent, -extent), c + new Vector2(-extent, -extent + arm), color, width);
		DrawLine(c + new Vector2(extent, -extent), c + new Vector2(extent - arm, -extent), color, width);
		DrawLine(c + new Vector2(extent, -extent), c + new Vector2(extent, -extent + arm), color, width);
		DrawLine(c + new Vector2(-extent, extent), c + new Vector2(-extent + arm, extent), color, width);
		DrawLine(c + new Vector2(-extent, extent), c + new Vector2(-extent, extent - arm), color, width);
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
