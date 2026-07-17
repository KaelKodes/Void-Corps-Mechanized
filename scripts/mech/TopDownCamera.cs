using Godot;

namespace Mechanize;

public partial class TopDownCamera : Camera3D
{
	[Export] public NodePath TargetPath { get; set; } = "../Mech";
	[Export] public Vector3 Offset { get; set; } = new(0f, 28f, 18f);
	[Export] public float FollowSpeed { get; set; } = 8f;
	[Export] public float LookHeight { get; set; } = 1.2f;

	private Node3D? _target;

	public override void _Ready()
	{
		_target = GetNodeOrNull<Node3D>(TargetPath);
		if (_target != null)
		{
			GlobalPosition = _target.GlobalPosition + Offset;
			LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
		}
	}

	public override void _Process(double delta)
	{
		if (_target == null)
			_target = GetNodeOrNull<Node3D>(TargetPath);
		if (_target == null)
			return;

		var desired = _target.GlobalPosition + Offset;
		GlobalPosition = GlobalPosition.Lerp(desired, 1f - Mathf.Exp(-FollowSpeed * (float)delta));
		LookAt(_target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
	}

	public void SetTarget(Node3D target)
	{
		_target = target;
		TargetPath = GetPathTo(target);
	}
}
