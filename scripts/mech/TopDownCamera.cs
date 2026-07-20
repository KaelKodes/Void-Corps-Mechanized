using Godot;

namespace Mechanize;

public partial class TopDownCamera : Camera3D
{
	[Export] public NodePath TargetPath { get; set; } = "../Mech";
	[Export] public Vector3 Offset { get; set; } = new(0f, 28f, 18f);
	[Export] public float FollowSpeed { get; set; } = 8f;
	[Export] public float LookHeight { get; set; } = 1.2f;
	[Export] public float FirstPersonFov { get; set; } = 80f;
	[Export] public float FirstPersonEyeHeight { get; set; } = 2.05f;
	[Export] public float FirstPersonForwardBias { get; set; } = 0.55f;
	[Export] public float FirstPersonDefaultPitch { get; set; } = -0.12f;
	[Export] public bool StartFirstPerson { get; set; } = true;

	private Node3D? _target;
	private bool _firstPerson;
	private float _headLookYaw;
	private float _headLookPitch;
	private float _topDownFov = 50f;

	public bool IsFirstPerson => _firstPerson;
	public float HeadLookYaw => _headLookYaw;
	public float HeadLookPitch => _headLookPitch;

	public override void _Ready()
	{
		_topDownFov = Fov;
		_target = GetNodeOrNull<Node3D>(TargetPath);
		if (_target != null)
		{
			GlobalPosition = _target.GlobalPosition + Offset;
			LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
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
		}
	}

	public override void _Process(double delta)
	{
		if (_target == null)
			_target = GetNodeOrNull<Node3D>(TargetPath);
		if (_target == null)
			return;

		if (_firstPerson)
			UpdateFirstPerson();
		else
			UpdateTopDown(delta);
	}

	public void SetTarget(Node3D target)
	{
		_target = target;
		TargetPath = GetPathTo(target);
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
		// Cursor stays visible — mouse aims gimbals, not the camera.
		Input.MouseMode = Input.MouseModeEnum.Visible;
		Current = true;
	}

	public void ExitFirstPerson()
	{
		if (!_firstPerson)
			return;

		_firstPerson = false;
		_headLookYaw = 0f;
		_headLookPitch = 0f;
		Fov = _topDownFov;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		if (_target != null)
		{
			GlobalPosition = _target.GlobalPosition + Offset;
			LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
		}
	}

	/// <summary>Temporary look offset relative to chassis forward. Cleared on release.</summary>
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

	private void UpdateFirstPerson()
	{
		var scale = _target is MechController mech
			? MechChassisClassUtil.VisualScale(mech.ChassisClass)
			: 1f;
		var eye = ResolveEyeAnchor(_target!, scale);

		// Follow chassis facing; arrow/Alt look is a temporary offset on top.
		// Upper-body gimbal aim must not drag the camera (gimbals track the cursor).
		var baseYaw = _target!.GlobalRotation.Y;
		GlobalRotation = new Vector3(
			FirstPersonDefaultPitch + _headLookPitch,
			baseYaw + _headLookYaw,
			0f);
		GlobalPosition = eye - GlobalTransform.Basis.Z * (FirstPersonForwardBias * scale);
	}

	private Vector3 ResolveEyeAnchor(Node3D target, float scale)
	{
		var head = target.GetNodeOrNull<Node3D>("Sockets/UpperBody/Head");
		if (head != null && GodotObject.IsInstanceValid(head))
			return head.GlobalPosition + Vector3.Up * (0.15f * scale);

		return target.GlobalPosition + Vector3.Up * (FirstPersonEyeHeight * scale);
	}
}
