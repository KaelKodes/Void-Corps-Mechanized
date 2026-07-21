using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Lightweight Verlet rope for plated stabilizer tails. Segment nodes stay children of this
/// chain; world positions lag behind motion so the fin reads like a flexible counterweight.
/// </summary>
public partial class SoftTailChain : Node3D
{
	[Export] public float SegmentLength { get; set; } = 0.28f;
	[Export] public float Damping { get; set; } = 0.92f;
	[Export] public int StiffnessIterations { get; set; } = 3;
	[Export] public float MaxSag { get; set; } = 0.35f;
	[Export] public float Gravity { get; set; } = 4.5f;
	[Export] public float InertiaFromParent { get; set; } = 0.55f;

	private readonly List<Node3D> _segments = new();
	private Vector3[] _pos = [];
	private Vector3[] _prev = [];
	private Vector3 _anchorPrev;
	private bool _ready;

	public override void _Ready() => RebuildChain();

	public void RebuildChain()
	{
		_segments.Clear();
		foreach (var child in GetChildren())
		{
			if (child is Node3D n && n.Name.ToString().StartsWith("Seg_"))
				_segments.Add(n);
		}

		_segments.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
		var count = _segments.Count;
		_pos = new Vector3[count];
		_prev = new Vector3[count];

		var anchor = GlobalPosition;
		_anchorPrev = anchor;
		for (var i = 0; i < count; i++)
		{
			var p = anchor + GlobalTransform.Basis * new Vector3(0f, 0f, SegmentLength * (i + 1));
			_pos[i] = p;
			_prev[i] = p;
			_segments[i].GlobalPosition = p;
		}

		_ready = count > 0;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_ready || _segments.Count == 0)
			return;

		var dt = Mathf.Clamp((float)delta, 0.001f, 0.05f);
		var anchor = GlobalPosition;
		var anchorDelta = anchor - _anchorPrev;
		_anchorPrev = anchor;

		for (var i = 0; i < _pos.Length; i++)
		{
			var velocity = (_pos[i] - _prev[i]) * Damping;
			velocity += anchorDelta * (InertiaFromParent * (1f - i / (float)(_pos.Length + 1)));
			_prev[i] = _pos[i];
			_pos[i] += velocity;
			_pos[i] += Vector3.Down * Gravity * dt * dt * (0.35f + i * 0.12f);
		}

		var iters = Mathf.Max(1, StiffnessIterations);
		for (var iter = 0; iter < iters; iter++)
		{
			_pos[0] = anchor + GlobalTransform.Basis * new Vector3(0f, 0f, SegmentLength);

			for (var i = 1; i < _pos.Length; i++)
			{
				var prev = _pos[i - 1];
				var deltaVec = _pos[i] - prev;
				var dist = deltaVec.Length();
				if (dist < 0.0001f)
					continue;
				_pos[i] -= deltaVec * ((dist - SegmentLength) / dist);
			}

			for (var i = 0; i < _pos.Length; i++)
			{
				var rest = anchor + GlobalTransform.Basis * new Vector3(0f, 0f, SegmentLength * (i + 1));
				var sag = _pos[i] - rest;
				if (sag.Length() > MaxSag)
					_pos[i] = rest + sag.Normalized() * MaxSag;
			}
		}

		for (var i = 0; i < _segments.Count; i++)
		{
			var from = i == 0 ? anchor : _pos[i - 1];
			var to = _pos[i];
			var dir = to - from;
			if (dir.LengthSquared() < 0.00001f)
				dir = GlobalTransform.Basis.Z;
			_segments[i].GlobalPosition = (from + to) * 0.5f;
			_segments[i].LookAt(to, Vector3.Up);
			_segments[i].RotateObjectLocal(Vector3.Up, Mathf.Pi);
		}
	}
}
