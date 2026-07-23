using Godot;

namespace Mechanize;

/// <summary>
/// FP seat adjust: hold Alt and press F to start, keep holding F to adjust
/// (Alt may be released). Scroll / LMB / RMB move the camera offset; F+Q resets.
/// Release F persists to GameSettings. No lever gaze required.
/// </summary>
public partial class CockpitSeatAdjust : Node
{
	public const string NodeName = "CockpitSeatAdjust";

	private const float ScrollStep = 0.04f;
	private const float VerticalSpeed = 0.38f;

	private MechController? _mech;
	private float _scrollDelta;

	public bool IsAdjusting { get; private set; }
	/// <summary>True when F should not start extract / other world interacts.</summary>
	public bool BlocksWorldInteract => IsAdjusting;

	public void Bind(MechController mech) => _mech = mech;

	public void NotifyScroll(float steps)
	{
		if (IsAdjusting)
			_scrollDelta += steps;
	}

	public void Tick(float dt)
	{
		if (_mech == null || !_mech.IsLocalPilot || !_mech.ControlsEnabled || _mech.HangarDisplayOnly)
		{
			EndAdjust(persist: IsAdjusting);
			return;
		}

		if (_mech.GetViewport()?.GetCamera3D() is not TopDownCamera cam || !cam.IsFirstPerson)
		{
			EndAdjust(persist: IsAdjusting);
			CockpitSeatLever.SetHighlight(CockpitSeatLever.Find(_mech), false);
			return;
		}

		if (!HasHollowCockpit(_mech))
		{
			EndAdjust(persist: IsAdjusting);
			return;
		}

		_ = CockpitSeatLever.Find(_mech) ?? EnsureLever(_mech);
		var holdingF = Input.IsActionPressed("interact");
		var altHeld = Input.IsKeyPressed(Key.Alt);

		if (!IsAdjusting)
		{
			// Arm with Alt held, then press F — Alt can be released after that.
			if (altHeld && Input.IsActionJustPressed("interact"))
			{
				IsAdjusting = true;
				cam.SeatAdjustBlocksInspect = true;
				cam.LoadSeatFromSettings();
			}
			else
			{
				_scrollDelta = 0f;
				CockpitSeatLever.SetHighlight(CockpitSeatLever.Find(_mech), false);
				return;
			}
		}

		CockpitSeatLever.SetHighlight(CockpitSeatLever.Find(_mech), true);

		if (holdingF)
			ApplyAdjust(cam, dt);
		else
		{
			cam.SetSeatOffset(cam.SeatForward, cam.SeatUp, persist: true);
			EndAdjust(persist: false);
		}
	}

	private void ApplyAdjust(TopDownCamera cam, float dt)
	{
		if (Input.IsKeyPressed(Key.Q))
		{
			cam.ResetSeatOffset(persist: false);
			_scrollDelta = 0f;
			return;
		}

		var forward = cam.SeatForward + _scrollDelta * ScrollStep;
		_scrollDelta = 0f;
		var up = cam.SeatUp;
		if (Input.IsMouseButtonPressed(MouseButton.Right))
			up += VerticalSpeed * dt;
		if (Input.IsMouseButtonPressed(MouseButton.Left))
			up -= VerticalSpeed * dt;
		cam.SetSeatOffset(forward, up, persist: false);
	}

	private void EndAdjust(bool persist)
	{
		if (_mech?.GetViewport()?.GetCamera3D() is TopDownCamera cam)
		{
			if (persist && IsAdjusting)
				cam.SetSeatOffset(cam.SeatForward, cam.SeatUp, persist: true);
			cam.SeatAdjustBlocksInspect = false;
		}

		IsAdjusting = false;
		_scrollDelta = 0f;
		if (_mech != null)
			CockpitSeatLever.SetHighlight(CockpitSeatLever.Find(_mech), false);
	}

	private static bool HasHollowCockpit(MechController mech) =>
		mech.FindChild("CockpitAnchor", recursive: true, owned: false) != null;

	private static Area3D? EnsureLever(MechController mech)
	{
		var hull = FindHull(mech);
		return hull == null ? null : CockpitSeatLever.EnsureOn(hull);
	}

	private static CockpitTorsoVisual? FindHull(Node node)
	{
		if (node is CockpitTorsoVisual self)
			return self;
		foreach (var child in node.GetChildren())
		{
			var found = FindHull(child);
			if (found != null)
				return found;
		}

		return null;
	}
}
