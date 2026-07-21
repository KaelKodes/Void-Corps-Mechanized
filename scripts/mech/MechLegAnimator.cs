using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Drives articulated leg visuals from mech velocity.
/// Bipeds get an alternating walk cycle; hexapods get a tripod gait.
/// </summary>
public partial class MechLegAnimator : Node
{
	public const string NodeName = "MechLegAnimator";

	private MechController? _mech;
	private MechAssembler? _assembler;
	private Node3D? _rig;
	private string _rigKind = "";
	private string _legsPartId = "";
	private readonly List<BipedLimb> _bipeds = new();
	private readonly List<HexLimb> _hexes = new();
	private float _phase;
	private float _bindCooldown;
	private float _pace;
	private bool _gaitMoving;
	private float _prevLeftWave;
	private float _prevRightWave;
	private float _prevTrip0;
	private float _prevTrip1;
	private bool _stepPrimed;

	/// <summary>Shared with FP cockpit bob so the camera feels the same footsteps as the legs.</summary>
	public float GaitPhase => _phase;
	public float GaitPace => _pace;
	public bool GaitMoving => _gaitMoving;

	private struct BipedLimb
	{
		public Node3D Hip;
		public Node3D Knee;
		public Vector3 HipRest;
		public Vector3 KneeRest;
		public float SideSign;
	}

	private struct HexLimb
	{
		public Node3D Root;
		public MeshInstance3D Upper;
		public MeshInstance3D Lower;
		public MeshInstance3D Knee;
		public MeshInstance3D Foot;
		public Vector3 Hip;
		public Vector3 RestFoot;
		public int Index;
	}

	public static void EnsureOn(MechController mech)
	{
		if (mech.GetNodeOrNull(NodeName) != null)
			return;
		mech.AddChild(new MechLegAnimator { Name = NodeName });
	}

	public override void _Ready()
	{
		_mech = GetParent() as MechController;
		_assembler = _mech?.GetNodeOrNull<MechAssembler>("MechAssembler");
		CallDeferred(MethodName.BindRig);
	}

	public override void _Process(double delta)
	{
		if (_mech == null || !GodotObject.IsInstanceValid(_mech))
			return;
		if (_mech.HangarDisplayOnly || _mech.ProcessMode == ProcessModeEnum.Disabled)
			return;
		if (_mech.Integrity?.IsCollapsed == true || _mech.Health?.IsDead == true)
		{
			ResetPose();
			return;
		}

		_bindCooldown -= (float)delta;
		var liveVisual = _assembler?.Hardpoints.TryGetValue(PartSlot.Legs, out var legsHp) == true
			? legsHp.Visual
			: null;
		var needsBind = _bindCooldown <= 0f
			|| _rig == null
			|| !GodotObject.IsInstanceValid(_rig)
			|| _rig.IsQueuedForDeletion()
			|| liveVisual != _rig
			|| (_rigKind == "biped" && _bipeds.Count == 0)
			|| (_rigKind == "hex" && _hexes.Count == 0);
		if (needsBind)
		{
			_bindCooldown = 0.35f;
			BindRig();
		}

		if (_rig == null)
			return;

		var dt = (float)delta;
		var planar = new Vector3(_mech.Velocity.X, 0f, _mech.Velocity.Z);
		var speed = planar.Length();
		var maxSpeed = Mathf.Max(1f, _assembler?.MaxSpeed ?? 10f);
		_pace = Mathf.Clamp(speed / maxSpeed, 0f, 1.35f);
		_gaitMoving = _pace > 0.04f;

		if (_gaitMoving)
			_phase += dt * Mathf.Lerp(4.5f, 9.5f, _pace);
		else
			_phase = Mathf.MoveToward(_phase, Mathf.Snapped(_phase, Mathf.Pi), dt * 6f);

		switch (_rigKind)
		{
			case "biped":
				AnimateBiped(_pace, _gaitMoving);
				TickBipedSteps(_gaitMoving);
				break;
			case "hex":
				AnimateHex(_pace, _gaitMoving);
				TickHexSteps(_gaitMoving);
				break;
		}
	}

	public void BindRig()
	{
		_bipeds.Clear();
		_hexes.Clear();
		_rig = null;
		_rigKind = "";
		_legsPartId = "";
		_stepPrimed = false;

		_assembler ??= _mech?.GetNodeOrNull<MechAssembler>("MechAssembler");
		if (_assembler == null || !_assembler.Hardpoints.TryGetValue(PartSlot.Legs, out var legsHp))
			return;

		_legsPartId = legsHp.EquippedPart?.Id ?? "";

		// Prefer the hardpoint's live visual — GetChildren can still include QueueFree'd
		// leftovers after an equip/rebuild, which left some bipeds stuck without a gait.
		var visual = legsHp.Visual ?? FindLiveLegVisual(legsHp);
		if (visual == null || !visual.HasMeta("LegRig"))
			return;

		_rig = visual;
		_rigKind = visual.GetMeta("LegRig").AsString();

		if (_rigKind == "biped")
		{
			TryAddBiped("Leg_L", -1f);
			TryAddBiped("Leg_R", 1f);
		}
		else if (_rigKind == "hex")
		{
			for (var i = 0; i < 6; i++)
			{
				var leg = _rig.GetNodeOrNull<Node3D>($"HexLeg_{i}");
				if (leg == null || !leg.HasMeta("Hip") || !leg.HasMeta("RestFoot"))
					continue;
				if (leg.GetNodeOrNull<MeshInstance3D>("Upper") is not { } upper
				    || leg.GetNodeOrNull<MeshInstance3D>("Lower") is not { } lower
				    || leg.GetNodeOrNull<MeshInstance3D>("Knee") is not { } knee
				    || leg.GetNodeOrNull<MeshInstance3D>("Foot") is not { } foot)
					continue;

				_hexes.Add(new HexLimb
				{
					Root = leg,
					Upper = upper,
					Lower = lower,
					Knee = knee,
					Foot = foot,
					Hip = leg.GetMeta("Hip").AsVector3(),
					RestFoot = leg.GetMeta("RestFoot").AsVector3(),
					Index = i
				});
			}
		}
	}

	private static Node3D? FindLiveLegVisual(Hardpoint legsHp)
	{
		Node3D? found = null;
		foreach (var child in legsHp.GetChildren())
		{
			if (child is not Node3D n || n.IsQueuedForDeletion() || !GodotObject.IsInstanceValid(n))
				continue;
			var name = n.Name.ToString();
			if (!name.StartsWith("Visual_") && !name.StartsWith("TitanVisual_"))
				continue;
			found = n; // last live match wins if duplicates linger
		}

		return found;
	}

	private void TryAddBiped(string name, float side)
	{
		if (_rig == null)
			return;
		var hip = _rig.GetNodeOrNull<Node3D>(name);
		var knee = hip?.GetNodeOrNull<Node3D>("Knee");
		if (hip == null || knee == null)
			return;

		_bipeds.Add(new BipedLimb
		{
			Hip = hip,
			Knee = knee,
			HipRest = ReadRest(hip),
			KneeRest = ReadRest(knee),
			SideSign = side
		});
	}

	private static Vector3 ReadRest(Node3D node)
	{
		if (node.HasMeta("RestRotation"))
			return node.GetMeta("RestRotation").AsVector3();
		return node.Rotation;
	}

	private void AnimateBiped(float pace, bool moving)
	{
		if (_bipeds.Count == 0)
			return;

		if (!moving)
		{
			foreach (var limb in _bipeds)
			{
				limb.Hip.Rotation = limb.Hip.Rotation.Lerp(limb.HipRest, 0.18f);
				limb.Knee.Rotation = limb.Knee.Rotation.Lerp(limb.KneeRest, 0.18f);
			}
			return;
		}

		var swing = Mathf.Lerp(0.22f, 0.48f, pace);
		var lift = Mathf.Lerp(0.18f, 0.42f, pace);

		foreach (var limb in _bipeds)
		{
			// Opposite phases for left/right.
			var wave = Mathf.Sin(_phase + (limb.SideSign < 0f ? 0f : Mathf.Pi));
			var hipPitch = wave * swing;
			// Bend the knee on the forward swing / clearance half-cycle.
			var kneeBend = Mathf.Max(0f, -wave) * lift + Mathf.Abs(wave) * 0.08f;

			var hipTarget = limb.HipRest + new Vector3(hipPitch, 0f, 0f);
			var kneeTarget = limb.KneeRest + new Vector3(kneeBend, 0f, 0f);
			limb.Hip.Rotation = limb.Hip.Rotation.Lerp(hipTarget, 0.35f);
			limb.Knee.Rotation = limb.Knee.Rotation.Lerp(kneeTarget, 0.35f);
		}
	}

	private void TickBipedSteps(bool moving)
	{
		var leftWave = Mathf.Sin(_phase);
		var rightWave = Mathf.Sin(_phase + Mathf.Pi);

		if (!_stepPrimed)
		{
			_prevLeftWave = leftWave;
			_prevRightWave = rightWave;
			_stepPrimed = true;
			return;
		}

		if (moving)
		{
			// Plant when the clearance half-cycle ends (wave rises through 0).
			if (_prevLeftWave < 0f && leftWave >= 0f)
				PlayFootstep();
			if (_prevRightWave < 0f && rightWave >= 0f)
				PlayFootstep();
		}

		_prevLeftWave = leftWave;
		_prevRightWave = rightWave;
	}

	private void TickHexSteps(bool moving)
	{
		var trip0 = Mathf.Sin(_phase);
		var trip1 = Mathf.Sin(_phase + Mathf.Pi);

		if (!_stepPrimed)
		{
			_prevTrip0 = trip0;
			_prevTrip1 = trip1;
			_stepPrimed = true;
			return;
		}

		if (moving)
		{
			// Tripod plants as lift wave falls through 0 (feet return to ground).
			if (_prevTrip0 > 0f && trip0 <= 0f)
				PlayFootstep();
			if (_prevTrip1 > 0f && trip1 <= 0f)
				PlayFootstep();
		}

		_prevTrip0 = trip0;
		_prevTrip1 = trip1;
	}

	/// <summary>~40 feet — beyond this, remote footfalls are silent.</summary>
	private const float StepHearRange = 12.2f;

	private void PlayFootstep()
	{
		if (_mech == null || string.IsNullOrEmpty(_legsPartId))
			return;

		var volumeDb = -2f;
		if (_rigKind == "hex")
			volumeDb -= 2f;
		volumeDb += Mathf.Lerp(-1f, 0.5f, Mathf.Clamp(_pace, 0f, 1f));

		if (!_mech.IsLocalPilot)
		{
			var listener = FindLocalMech();
			if (listener == null)
				return;

			var dist = _mech.GlobalPosition.DistanceTo(listener.GlobalPosition);
			if (dist >= StepHearRange)
				return;

			// Same near-volume as local; quadratic fade to silence at hear range.
			var t = dist / StepHearRange;
			volumeDb += Mathf.Lerp(0f, -48f, t * t);
		}

		SfxService.PlayMechStepForLegs(_legsPartId, volumeDb);
	}

	private MechController? FindLocalMech()
	{
		var tree = GetTree();
		if (tree == null)
			return null;

		foreach (var node in tree.GetNodesInGroup("mechs"))
		{
			if (node is MechController { IsLocalPilot: true } local
			    && GodotObject.IsInstanceValid(local))
				return local;
		}

		return null;
	}

	private void AnimateHex(float pace, bool moving)
	{
		if (_hexes.Count == 0)
			return;

		var planar = new Vector3(_mech!.Velocity.X, 0f, _mech.Velocity.Z);
		var localMove = planar.LengthSquared() > 0.001f && _rig != null
			? (_rig.GlobalBasis.Inverse() * planar.Normalized()).Normalized()
			: Vector3.Forward;
		var lift = Mathf.Lerp(0.08f, 0.2f, pace);
		var stride = Mathf.Lerp(0.06f, 0.18f, pace);

		foreach (var limb in _hexes)
		{
			var target = limb.RestFoot;
			var swingAmount = 0f;

			if (moving)
			{
				// Alternating tripods: 0/3/4 and 1/2/5.
				var tripod = limb.Index is 0 or 3 or 4 ? 0 : 1;
				var legPhase = _phase + tripod * Mathf.Pi;
				var wave = Mathf.Sin(legPhase);
				swingAmount = Mathf.Max(0f, wave);
				target += localMove * (-Mathf.Cos(legPhase) * stride);
				target.Y += swingAmount * lift;
			}

			// Feet return to authored ground targets at idle and only rise during swing.
			var foot = limb.Foot.Position.Lerp(target, moving ? 0.36f : 0.18f);
			var outward = new Vector3(
				limb.RestFoot.X - limb.Hip.X,
				0f,
				limb.RestFoot.Z - limb.Hip.Z).Normalized();
			var knee = limb.Hip.Lerp(foot, 0.52f)
				+ outward * 0.18f
				+ Vector3.Up * (0.24f + swingAmount * 0.08f);

			PoseStrut(limb.Upper, limb.Hip, knee);
			PoseStrut(limb.Lower, knee, foot);
			limb.Knee.Position = knee;
			limb.Foot.Position = foot;
		}
	}

	private static void PoseStrut(MeshInstance3D strut, Vector3 from, Vector3 to)
	{
		var delta = to - from;
		var length = Mathf.Max(0.001f, delta.Length());
		strut.Position = (from + to) * 0.5f;
		strut.Quaternion = new Quaternion(Vector3.Up, delta / length);
		strut.Scale = new Vector3(1f, length, 1f);
	}

	private void ResetPose()
	{
		foreach (var limb in _bipeds)
		{
			if (GodotObject.IsInstanceValid(limb.Hip))
				limb.Hip.Rotation = limb.HipRest;
			if (GodotObject.IsInstanceValid(limb.Knee))
				limb.Knee.Rotation = limb.KneeRest;
		}

		foreach (var limb in _hexes)
		{
			if (!GodotObject.IsInstanceValid(limb.Root))
				continue;
			var outward = new Vector3(
				limb.RestFoot.X - limb.Hip.X,
				0f,
				limb.RestFoot.Z - limb.Hip.Z).Normalized();
			var knee = limb.Hip.Lerp(limb.RestFoot, 0.52f) + outward * 0.18f + Vector3.Up * 0.24f;
			PoseStrut(limb.Upper, limb.Hip, knee);
			PoseStrut(limb.Lower, knee, limb.RestFoot);
			limb.Knee.Position = knee;
			limb.Foot.Position = limb.RestFoot;
		}
	}
}
