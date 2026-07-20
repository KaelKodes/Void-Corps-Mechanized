using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

public enum DeploymentKind
{
	VesselDrop,
	GroundApproach,
	CargoPod,
	Remount
}

public enum DeploymentPhase
{
	Warning,
	Inbound,
	Impact,
	Activated
}

public sealed class DeploymentRequest
{
	public required string JobId;
	public required DeploymentKind Kind;
	public required Vector3 Target;
	public TeamId Team = TeamId.Enemy;
	public float WarningSeconds = 2f;
	public float FallSeconds = 1.35f;
	public float OpenSeconds = 0.75f;
	public float DropHeight = 28f;
	public bool CreateBeacon;
	public DropBeacon? ExistingBeacon;
	public MechController? Mech;
	public SupportUnit? Support;
	public FieldPartCrate? Cargo;
	public bool EnableAiWhenDone;
	public Action? OnActivated;
	public Vector3? ApproachFrom;
}

/// <summary>
/// Host-authoritative arrival scheduler: Warning → Inbound → Impact → Activated.
/// </summary>
public partial class DeploymentDirector : Node
{
	public event Action<string, DeploymentPhase, Vector3, int>? PhaseChanged;

	private readonly List<Job> _jobs = new();
	private int _nextId = 1;
	private Node3D? _worldRoot;
	private Func<MechController?>? _pickAiTarget;
	private Func<bool>? _isFighting;
	private DropBeacon? _playerDropBeacon;
	private Action<DropBeacon?>? _onPlayerDropReady;

	private sealed class Job
	{
		public required string Id;
		public required DeploymentRequest Request;
		public DeploymentPhase Phase = DeploymentPhase.Warning;
		public float Elapsed;
		public float StartY;
		public bool Opening;
		public DeploymentTelegraph? Telegraph;
		public DropBeacon? Beacon;
	}

	public int ActiveCount => _jobs.Count;
	public bool HasActiveJobs => _jobs.Count > 0;

	public bool IsMechDeploying(MechController? mech)
	{
		if (mech == null)
			return false;
		foreach (var job in _jobs)
		{
			if (job.Request.Mech == mech && job.Phase != DeploymentPhase.Activated)
				return true;
		}

		return false;
	}

	public void Bind(
		Node3D worldRoot,
		Func<MechController?> pickAiTarget,
		Func<bool> isFighting,
		Func<DropBeacon?> getPlayerBeacon,
		Action<DropBeacon?>? onPlayerDropReady = null)
	{
		_worldRoot = worldRoot;
		_pickAiTarget = pickAiTarget;
		_isFighting = isFighting;
		_onPlayerDropReady = onPlayerDropReady;
		_playerDropBeacon = getPlayerBeacon();
	}

	public void SetPlayerDropBeacon(DropBeacon? beacon) => _playerDropBeacon = beacon;

	public string Schedule(DeploymentRequest request)
	{
		request.JobId = $"deploy_{_nextId++}";
		request.Target = new Vector3(request.Target.X, 0f, request.Target.Z);
		request.WarningSeconds = Mathf.Clamp(request.WarningSeconds, 1f, 3f);

		var job = new Job
		{
			Id = request.JobId,
			Request = request,
			Phase = DeploymentPhase.Warning,
			Elapsed = 0f
		};

		Node3D? preview = request.Mech ?? (Node3D?)request.Support ?? request.Cargo;
		job.Telegraph = DeploymentTelegraph.Create(
			$"Telegraph_{request.JobId}",
			request.Target,
			request.Team,
			request.WarningSeconds,
			preview,
			request.Kind == DeploymentKind.CargoPod ? 2.2f : 3.2f);
		_worldRoot?.AddChild(job.Telegraph);
		if (job.Telegraph != null)
			job.Telegraph.GlobalPosition = new Vector3(request.Target.X, 0.06f, request.Target.Z);

		if (request.Mech != null)
		{
			request.Mech.Visible = false;
			request.Mech.SetControlsEnabled(false);
			request.Mech.Velocity = Vector3.Zero;
			if (!request.Mech.IsPlayerControlled)
				request.Mech.GetNodeOrNull<MechPilotAI>("MechPilotAI")?.SetTarget(null);
		}

		if (request.Support != null)
		{
			request.Support.Visible = false;
			request.Support.ProcessMode = ProcessModeEnum.Disabled;
		}

		_jobs.Add(job);
		EmitPhase(job);
		SfxService.Play("alarm", 1.1f, -8f);
		return request.JobId;
	}

	public void Tick(float dt)
	{
		for (var i = _jobs.Count - 1; i >= 0; i--)
		{
			var job = _jobs[i];
			job.Elapsed += dt;
			switch (job.Phase)
			{
				case DeploymentPhase.Warning:
					TickWarning(job);
					break;
				case DeploymentPhase.Inbound:
					TickInbound(job);
					break;
				case DeploymentPhase.Impact:
					TickImpact(job);
					break;
			}

			if (job.Phase == DeploymentPhase.Activated)
			{
				CleanupTelegraph(job);
				_jobs.RemoveAt(i);
			}
		}
	}

	private void TickWarning(Job job)
	{
		if (job.Elapsed < job.Request.WarningSeconds)
			return;

		job.Phase = DeploymentPhase.Inbound;
		job.Elapsed = 0f;
		job.Telegraph?.MarkInbound();
		BeginInboundMotion(job);
		EmitPhase(job);
	}

	private void BeginInboundMotion(Job job)
	{
		var req = job.Request;
		if (req.Kind == DeploymentKind.GroundApproach)
		{
			var from = req.ApproachFrom ?? req.Target + new Vector3(0f, 0f, -18f);
			from.Y = 0f;
			if (req.Support != null)
			{
				req.Support.GlobalPosition = from;
				req.Support.Visible = true;
				req.Support.ProcessMode = ProcessModeEnum.Inherit;
			}

			if (req.Mech != null)
			{
				req.Mech.GlobalPosition = from;
				req.Mech.Visible = true;
			}

			return;
		}

		DropBeacon? beacon = req.ExistingBeacon;
		if (req.CreateBeacon && _worldRoot != null)
		{
			var beaconName = $"DropBeacon_{req.Mech?.Name ?? req.JobId}";
			_worldRoot.GetNodeOrNull<DropBeacon>(beaconName)?.QueueFree();
			var pad = DropBeacon.PadBesideSpawn(req.Target);
			beacon = DropBeacon.Create(beaconName, pad, req.Team);
			req.Target = new Vector3(pad.X, 0f, pad.Z);
			_worldRoot.AddChild(beacon);
		}
		else if (beacon != null && req.Kind == DeploymentKind.VesselDrop)
		{
			// Player pad drops reuse the existing extract beacon; enemies free-fall with no pad.
			req.Target = new Vector3(beacon.GlobalPosition.X, 0f, beacon.GlobalPosition.Z);
		}

		job.Beacon = beacon;
		job.StartY = req.Target.Y + req.DropHeight;
		var start = new Vector3(req.Target.X, job.StartY, req.Target.Z);

		if (beacon != null)
		{
			beacon.GlobalPosition = start;
			beacon.MarkDropping();
		}

		if (req.Mech != null)
		{
			req.Mech.Visible = false;
			req.Mech.GlobalPosition = start;
			req.Mech.Velocity = Vector3.Zero;
			req.Mech.SetControlsEnabled(false);
		}

		if (req.Cargo != null)
		{
			req.Cargo.GlobalPosition = start;
			req.Cargo.Visible = true;
		}
	}

	private void TickInbound(Job job)
	{
		var req = job.Request;
		if (req.Kind == DeploymentKind.GroundApproach)
		{
			var t = Mathf.Clamp(job.Elapsed / Mathf.Max(0.05f, req.FallSeconds), 0f, 1f);
			var from = req.ApproachFrom ?? req.Target + new Vector3(0f, 0f, -18f);
			from.Y = 0f;
			var pos = from.Lerp(req.Target, t * t);
			if (req.Support != null)
				req.Support.GlobalPosition = pos;
			if (req.Mech != null)
				req.Mech.GlobalPosition = pos;

			if (t < 1f)
				return;

			job.Phase = DeploymentPhase.Impact;
			job.Elapsed = 0f;
			job.Telegraph?.MarkImpact();
			EmitPhase(job);
			Activate(job);
			return;
		}

		var fallT = Mathf.Clamp(job.Elapsed / Mathf.Max(0.05f, req.FallSeconds), 0f, 1f);
		var eased = fallT * fallT;
		var y = Mathf.Lerp(job.StartY, req.Target.Y, eased);
		var dropPos = new Vector3(req.Target.X, y, req.Target.Z);
		if (req.Mech != null)
		{
			req.Mech.GlobalPosition = dropPos;
			req.Mech.Velocity = Vector3.Zero;
		}

		if (req.Cargo != null)
			req.Cargo.GlobalPosition = dropPos;
		if (job.Beacon != null && IsInstanceValid(job.Beacon))
			job.Beacon.GlobalPosition = dropPos;

		if (fallT < 1f)
			return;

		job.Phase = DeploymentPhase.Impact;
		job.Elapsed = 0f;
		job.Opening = true;
		job.Telegraph?.MarkImpact();
		if (req.Mech != null)
			req.Mech.GlobalPosition = req.Target;
		if (req.Cargo != null)
			req.Cargo.GlobalPosition = req.Target + new Vector3(0f, 0.4f, 0f);
		if (job.Beacon != null && IsInstanceValid(job.Beacon))
		{
			job.Beacon.GlobalPosition = req.Target;
			job.Beacon.MarkOpening();
			job.Beacon.SetOpenAmount(0f);
		}

		SfxService.Play("drop_impact", 1f, -2f);
		EmitPhase(job);
	}

	private void TickImpact(Job job)
	{
		var req = job.Request;
		if (req.Kind == DeploymentKind.GroundApproach)
			return;

		// No vessel pad: free-fall arrivals activate as soon as they hit.
		if (job.Beacon == null)
		{
			if (req.Mech != null)
			{
				req.Mech.Visible = true;
				req.Mech.GlobalPosition = req.Target;
			}

			if (req.Cargo != null)
			{
				req.Cargo.Visible = true;
				req.Cargo.GlobalPosition = req.Target + new Vector3(0f, 0.4f, 0f);
			}

			Activate(job);
			return;
		}

		var openT = Mathf.Clamp(job.Elapsed / Mathf.Max(0.05f, req.OpenSeconds), 0f, 1f);
		job.Beacon?.SetOpenAmount(openT);

		if (req.Mech != null && openT >= 0.45f && !req.Mech.Visible)
		{
			req.Mech.Visible = true;
			req.Mech.GlobalPosition = req.Target + new Vector3(0f, 0.05f, 0f);
		}

		if (req.Cargo != null && openT >= 0.35f)
			req.Cargo.Visible = true;

		if (openT < 1f)
			return;

		Activate(job);
	}

	private void Activate(Job job)
	{
		var req = job.Request;
		var fighting = _isFighting?.Invoke() ?? true;

		if (req.Mech != null)
		{
			req.Mech.Visible = true;
			req.Mech.GlobalPosition = req.Target;
			req.Mech.SetControlsEnabled(fighting && req.Mech.IsPlayerControlled);
			if (req.EnableAiWhenDone && !req.Mech.IsPlayerControlled && fighting)
				req.Mech.GetNodeOrNull<MechPilotAI>("MechPilotAI")?.SetTarget(_pickAiTarget?.Invoke());
		}

		if (req.Support != null)
		{
			req.Support.Visible = true;
			req.Support.ProcessMode = ProcessModeEnum.Inherit;
			req.Support.GlobalPosition = req.Target;
		}

		if (req.Cargo != null)
		{
			req.Cargo.Visible = true;
			req.Cargo.GlobalPosition = req.Target + new Vector3(0f, 0.35f, 0f);
			req.Cargo.MarkLanded();
		}

		job.Beacon?.MarkReady();
		if (job.Beacon != null && job.Beacon == _playerDropBeacon)
			_onPlayerDropReady?.Invoke(job.Beacon);

		req.OnActivated?.Invoke();
		job.Phase = DeploymentPhase.Activated;
		EmitPhase(job);
	}

	private void CleanupTelegraph(Job job)
	{
		if (job.Telegraph != null && IsInstanceValid(job.Telegraph))
			MeshMat.QueueFreeSafe(job.Telegraph);
		job.Telegraph = null;
	}

	private void EmitPhase(Job job)
	{
		PhaseChanged?.Invoke(job.Id, job.Phase, job.Request.Target, (int)job.Request.Team);
	}
}
