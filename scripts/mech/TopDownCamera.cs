using Godot;

namespace Mechanize;

/// <summary>
/// Chase / first-person camera. FP uses captured mouselook (body window); TP uses visible cursor aim.
/// </summary>
public partial class TopDownCamera : Camera3D
{
	[Export] public NodePath TargetPath { get; set; } = "../Mech";
	[Export] public Vector3 Offset { get; set; } = new(0f, 28f, 18f);
	[Export] public float FollowSpeed { get; set; } = 8f;
	[Export] public float LookHeight { get; set; } = 1.2f;
	[Export] public float FirstPersonFov { get; set; } = 96f;
	[Export] public float FirstPersonEyeHeight { get; set; } = 2.35f;
	/// <summary>
	/// Push the eye out past solid chest mesh when no CockpitAnchor exists.
	/// Hollow cockpit torsos use FirstPersonCockpitForwardBias instead.
	/// </summary>
	[Export] public float FirstPersonForwardBias { get; set; } = 1.35f;
	/// <summary>Minimal nudge toward the glass when seated on CockpitAnchor.</summary>
	[Export] public float FirstPersonCockpitForwardBias { get; set; } = 0.02f;
	[Export] public float FirstPersonDefaultPitch { get; set; } = -0.1f;
	/// <summary>Lift above UpperBody socket — mid/top torso window, not hip joint.</summary>
	[Export] public float FirstPersonTorsoEyeLift { get; set; } = 1.55f;
	[Export] public float FirstPersonLookSensitivity { get; set; } = 0.0024f;
	[Export] public float FirstPersonPitchMin { get; set; } = -1.15f;
	[Export] public float FirstPersonPitchMax { get; set; } = 0.85f;
	[Export] public bool StartFirstPerson { get; set; } = true;
	/// <summary>Max Alt+scroll inspect magnification (2 = twice as close).</summary>
	[Export] public float InspectZoomMax { get; set; } = 2f;
	[Export] public float InspectZoomScrollStep { get; set; } = 0.18f;
	[Export] public float InspectZoomLerp { get; set; } = 14f;

	// Cockpit gait feel (walk = stompy, sprint/dash = fluid)
	[Export] public float WalkBobVertical { get; set; } = 0.09f;
	[Export] public float WalkBobLateral { get; set; } = 0.055f;
	[Export] public float WalkBobRoll { get; set; } = 0.045f;
	[Export] public float WalkBobPitch { get; set; } = 0.028f;
	[Export] public float SprintBobScale { get; set; } = 0.28f;
	[Export] public float DashBobScale { get; set; } = 0.08f;

	private Node3D? _target;
	private bool _firstPerson;
	private float _headLookYaw;
	private float _headLookPitch;
	private float _bodyLookYaw;
	private float _bodyLookPitch;
	private float _topDownFov = 50f;
	private bool _uiBlocksCapture;
	private Vector3 _cockpitBobPos;
	private float _cockpitBobRoll;
	private float _cockpitBobPitchExtra;
	private float _inspectZoom;
	private float _inspectZoomTarget;
	private CanvasLayer? _inspectFxLayer;
	private ColorRect? _inspectFx;
	private ShaderMaterial? _inspectFxMat;

	public bool IsFirstPerson => _firstPerson;
	public float HeadLookYaw => _headLookYaw;
	public float HeadLookPitch => _headLookPitch;
	/// <summary>World-space body look yaw used for FP torso/aim/move basis.</summary>
	public float BodyLookYaw => _bodyLookYaw;
	public float BodyLookPitch => _bodyLookPitch;
	/// <summary>0 = none, 1 = full InspectZoomMax magnification.</summary>
	public float InspectZoomAmount => _inspectZoom;

	public override void _Ready()
	{
		_topDownFov = Fov;
		_target = GetNodeOrNull<Node3D>(TargetPath);
		if (_target != null)
		{
			GlobalPosition = _target.GlobalPosition + Offset;
			LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
			_bodyLookYaw = _target.GlobalRotation.Y;
			_bodyLookPitch = FirstPersonDefaultPitch;
		}

		if (StartFirstPerson)
			EnterFirstPerson();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false } key
		    && key.PhysicalKeycode == Key.P)
		{
			ToggleFirstPerson();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_firstPerson || _uiBlocksCapture)
			return;

		if (@event is InputEventMouseButton { Pressed: true } mouse
		    && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown
		    && Input.IsKeyPressed(Key.Alt)
		    && !Input.IsKeyPressed(Key.Ctrl))
		{
			var delta = mouse.ButtonIndex == MouseButton.WheelUp
				? InspectZoomScrollStep
				: -InspectZoomScrollStep;
			_inspectZoomTarget = Mathf.Clamp(_inspectZoomTarget + delta, 0f, 1f);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseMotion motion)
		{
			// Alt = head peek only (MechController); do not turn the body window.
			if (Input.IsKeyPressed(Key.Alt))
				return;

			_bodyLookYaw -= motion.Relative.X * FirstPersonLookSensitivity;
			_bodyLookPitch -= motion.Relative.Y * FirstPersonLookSensitivity;
			_bodyLookPitch = Mathf.Clamp(_bodyLookPitch, FirstPersonPitchMin, FirstPersonPitchMax);
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (_target == null)
			_target = GetNodeOrNull<Node3D>(TargetPath);
		if (_target == null)
			return;

		if (_firstPerson)
		{
			TickInspectZoom((float)delta);
			UpdateFirstPerson();
		}
		else
			UpdateTopDown(delta);
	}

	public void SetTarget(Node3D target)
	{
		if (_firstPerson)
			ApplyLocalFpLowerBodyHide(false);

		_target = target;
		TargetPath = GetPathTo(target);
		_bodyLookYaw = target.GlobalRotation.Y;
		_bodyLookPitch = FirstPersonDefaultPitch;

		if (_firstPerson)
			ApplyLocalFpLowerBodyHide(true);
	}

	/// <summary>
	/// Temporary: hide hips/legs on the local pilot while in FP so pelvis geometry
	/// does not poke through the view before real cockpit shells exist.
	/// </summary>
	private void ApplyLocalFpLowerBodyHide(bool hide)
	{
		if (_target is not MechController mech || !mech.IsLocalPilot)
			return;
		mech.SetFirstPersonHideLowerBody(hide);
	}

	public void ToggleFirstPerson()
	{
		if (_firstPerson)
			ExitFirstPerson();
		else
			EnterFirstPerson();
	}

	public void EnterFirstPerson()
	{
		if (_firstPerson)
			return;

		_firstPerson = true;
		_topDownFov = Fov;
		Fov = FirstPersonFov;
		_headLookYaw = 0f;
		_headLookPitch = 0f;
		_inspectZoom = 0f;
		_inspectZoomTarget = 0f;
		if (_target != null)
		{
			_bodyLookYaw = _target.GlobalRotation.Y;
			_bodyLookPitch = FirstPersonDefaultPitch;
		}

		EnsureInspectFx();
		RefreshMouseMode();
		Current = true;
		ApplyLocalFpLowerBodyHide(true);
		ApplyInspectFov();
	}

	public void ExitFirstPerson()
	{
		if (!_firstPerson)
			return;

		_firstPerson = false;
		_headLookYaw = 0f;
		_headLookPitch = 0f;
		_inspectZoom = 0f;
		_inspectZoomTarget = 0f;
		Fov = _topDownFov;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		ApplyLocalFpLowerBodyHide(false);
		UpdateInspectFx(0f);
		if (_target != null)
		{
			GlobalPosition = _target.GlobalPosition + Offset;
			LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
		}
	}

	/// <summary>Pause / garage / menus: release capture while UI is up.</summary>
	public void SetUiBlocksCapture(bool blocked)
	{
		_uiBlocksCapture = blocked;
		RefreshMouseMode();
	}

	public void RefreshMouseMode()
	{
		if (_firstPerson && !_uiBlocksCapture)
			Input.MouseMode = Input.MouseModeEnum.Captured;
		else
			Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	/// <summary>Temporary look offset relative to body look. Cleared on release.</summary>
	public void SetHeadLookOffset(float yawRadians, float pitchRadians)
	{
		_headLookYaw = yawRadians;
		_headLookPitch = pitchRadians;
	}

	private void UpdateTopDown(double delta)
	{
		var desired = _target!.GlobalPosition + Offset;
		GlobalPosition = GlobalPosition.Lerp(desired, 1f - Mathf.Exp(-FollowSpeed * (float)delta));
		LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
	}

	private void TickInspectZoom(float dt)
	{
		// Zoom is an Alt-held inspect; releasing Alt eases back to normal vision.
		if (!Input.IsKeyPressed(Key.Alt) || _uiBlocksCapture)
			_inspectZoomTarget = 0f;

		_inspectZoom = Mathf.MoveToward(
			_inspectZoom,
			_inspectZoomTarget,
			dt * InspectZoomLerp * Mathf.Max(0.35f, Mathf.Abs(_inspectZoomTarget - _inspectZoom) + 0.15f));
		ApplyInspectFov();
		UpdateInspectFx(_inspectZoom);
	}

	private void ApplyInspectFov()
	{
		if (!_firstPerson)
			return;
		var mag = Mathf.Lerp(1f, Mathf.Max(1.01f, InspectZoomMax), _inspectZoom);
		Fov = FirstPersonFov / mag;
	}

	private void EnsureInspectFx()
	{
		if (_inspectFx != null)
			return;

		var shader = GD.Load<Shader>("res://shaders/fp_inspect_zoom.gdshader");
		if (shader == null)
			return;

		_inspectFxMat = new ShaderMaterial { Shader = shader };
		_inspectFxMat.SetShaderParameter("strength", 0f);

		_inspectFxLayer = new CanvasLayer
		{
			Name = "InspectZoomFx",
			Layer = 64,
			Visible = false
		};
		AddChild(_inspectFxLayer);

		_inspectFx = new ColorRect
		{
			Name = "InspectZoomBlit",
			Color = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = _inspectFxMat
		};
		_inspectFx.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_inspectFxLayer.AddChild(_inspectFx);
	}

	private void UpdateInspectFx(float amount)
	{
		EnsureInspectFx();
		if (_inspectFxLayer == null || _inspectFxMat == null)
			return;

		var active = _firstPerson && amount > 0.01f;
		_inspectFxLayer.Visible = active;
		_inspectFxMat.SetShaderParameter("strength", active ? amount : 0f);
	}

	private void UpdateFirstPerson()
	{
		// Keep hips hidden if a rebuild re-showed them mid-fight.
		ApplyLocalFpLowerBodyHide(true);

		var scale = _target is MechController mech
			? MechChassisClassUtil.VisualScale(mech.ChassisClass)
			: 1f;
		var eye = ResolveEyeAnchor(_target!, scale);
		UpdateCockpitGaitBob((float)GetProcessDeltaTime(), scale);

		var lookBasis = Basis.FromEuler(new Vector3(
			_bodyLookPitch + _headLookPitch + _cockpitBobPitchExtra,
			_bodyLookYaw + _headLookYaw,
			_cockpitBobRoll));
		GlobalBasis = lookBasis;
		GlobalPosition = eye
			+ lookBasis * _cockpitBobPos
			- lookBasis.Z * (ResolveForwardBias(scale));
	}

	/// <summary>
	/// Step-synced cockpit motion so FP feels planted in the chassis.
	/// Walk is stompy; sprint/dash damp the sway into a smoother ride.
	/// </summary>
	private void UpdateCockpitGaitBob(float dt, float scale)
	{
		var targetPos = Vector3.Zero;
		var targetRoll = 0f;
		var targetPitch = 0f;

		if (_target is MechController mech
		    && !mech.IsCarrierMounted
		    && mech.GetNodeOrNull<MechLegAnimator>(MechLegAnimator.NodeName) is { } gait
		    && gait.GaitMoving)
		{
			var phase = gait.GaitPhase;
			var pace = Mathf.Clamp(gait.GaitPace, 0f, 1.2f);
			// Two footfalls per biped cycle (Abs sin peaks on each plant).
			var plant = Mathf.Abs(Mathf.Sin(phase));
			var sway = Mathf.Sin(phase);
			var thud = Mathf.Pow(plant, 1.6f);

			float modeScale;
			if (mech.IsDashing)
				modeScale = DashBobScale;
			else if (mech.IsSprinting)
				modeScale = SprintBobScale;
			else
				modeScale = 1f;

			var amp = modeScale * pace * scale;
			targetPos = new Vector3(
				sway * WalkBobLateral * amp,
				-thud * WalkBobVertical * amp,
				0f);
			targetRoll = sway * WalkBobRoll * amp;
			targetPitch = -thud * WalkBobPitch * amp;
		}

		var fluid = _target is MechController m && (m.IsSprinting || m.IsDashing);
		var settle = fluid ? 18f : 12f;
		var t = 1f - Mathf.Exp(-settle * dt);
		_cockpitBobPos = _cockpitBobPos.Lerp(targetPos, t);
		_cockpitBobRoll = Mathf.Lerp(_cockpitBobRoll, targetRoll, t);
		_cockpitBobPitchExtra = Mathf.Lerp(_cockpitBobPitchExtra, targetPitch, t);
	}

	private float ResolveForwardBias(float scale)
	{
		if (FindCockpitAnchor(_target!) != null)
			return FirstPersonCockpitForwardBias * scale;
		return FirstPersonForwardBias * scale;
	}

	private Vector3 ResolveEyeAnchor(Node3D target, float scale)
	{
		var cockpit = FindCockpitAnchor(target);
		if (cockpit != null)
			return cockpit.GlobalPosition;

		// Body camera: mid-to-top of the torso window (UpperBody socket is near the waist).
		var upper = target.GetNodeOrNull<Node3D>("Sockets/UpperBody");
		if (upper != null && GodotObject.IsInstanceValid(upper))
			return upper.GlobalPosition + Vector3.Up * (FirstPersonTorsoEyeLift * scale);

		var torso = target.GetNodeOrNull<Node3D>("Sockets/Torso");
		if (torso != null && GodotObject.IsInstanceValid(torso))
			return torso.GlobalPosition + Vector3.Up * (FirstPersonTorsoEyeLift * scale);

		return target.GlobalPosition + Vector3.Up * (FirstPersonEyeHeight * scale);
	}

	private static Node3D? FindCockpitAnchor(Node3D target)
	{
		var found = target.FindChild("CockpitAnchor", recursive: true, owned: false);
		return found is Node3D anchor && GodotObject.IsInstanceValid(anchor) ? anchor : null;
	}
}
