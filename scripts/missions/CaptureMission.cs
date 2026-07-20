using System.Collections.Generic;
using Godot;

namespace Mechanize;

public sealed class CaptureMission : MissionBase
{
	private readonly int _zoneCount;
	private readonly float _captureTime;
	private readonly List<ZoneState> _zones = new();
	private const float SingleCaptureTime = 30f;
	private const float MultiCaptureTime = 22f;
	private float _captureSfxCooldown;
	private RandomNumberGenerator? _rng;

	private sealed class ZoneState
	{
		public MissionZone Zone = null!;
		public float Progress;
		public bool Captured;
		public MissionPressure Pressure = null!;
	}

	public CaptureMission(int zoneCount)
		: base(zoneCount <= 1 ? MissionType.CaptureArea : MissionType.CaptureMultipleAreas)
	{
		_zoneCount = Mathf.Clamp(zoneCount, 1, 3);
		_captureTime = _zoneCount <= 1 ? SingleCaptureTime : MultiCaptureTime;
	}

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();
		_zones.Clear();
		_rng = new RandomNumberGenerator();
		_rng.Randomize();

		var sites = BuildSites();
		for (var i = 0; i < _zoneCount; i++)
		{
			var zone = MissionZone.Create(
				$"Capture_{i}",
				sites[i],
				11f,
				new Color(0.35f, 0.7f, 1f),
				_zoneCount == 1 ? "CLAIM ZONE" : $"ZONE {i + 1}");
			root.AddChild(zone);
			_zones.Add(new ZoneState
			{
				Zone = zone,
				Pressure = MissionPressure.Create(Host, $"Cap{i}", _rng)
			});
		}

		Host.SpawnEnemyMech("Trinova_Detachment", Host.EnemySpawnA, 0);
		if (Host.Difficulty != PilotDifficulty.Easy)
			Host.SpawnEnemyMech("OuroTech_Detachment", Host.EnemySpawnB, 1);
		else
			Host.DespawnEnemyMech("OuroTech_Detachment");

		Host.SpawnSupport("EnemyA_Buggy", "scout_buggy", TeamId.Enemy, sites[0] + new Vector3(6f, 0f, 2f));
		Host.SpawnSupport("Player_Tank", "light_tank", TeamId.Player, Offset(Host.PlayerSpawn, -4f, 3f));
		Host.DespawnSupport("Player_Tower");
		Host.DespawnSupport("EnemyA_Tank");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");
	}

	public override void Tick(float dt)
	{
		if (!Host.IsFighting || Host.Player == null)
			return;

		_captureSfxCooldown = Mathf.Max(0f, _captureSfxCooldown - dt);
		var playerPos = Host.Player.GlobalPosition;
		var allCaptured = true;
		var anyCapturing = false;

		foreach (var state in _zones)
		{
			if (state.Captured)
			{
				state.Zone.SetColor(new Color(0.35f, 0.9f, 0.45f));
				state.Zone.SetLabel("SECURED");
				continue;
			}

			allCaptured = false;
			var playerIn = state.Zone.Contains(playerPos);
			var contested = HostileInZone(state.Zone);
			var focus = state.Zone.GlobalPosition;

			if (playerIn && !contested)
			{
				state.Progress = Mathf.Min(_captureTime, state.Progress + dt);
				var ratio = state.Progress / _captureTime;
				state.Pressure.NotifyProgress(ratio, focus);
				state.Zone.SetColor(new Color(0.45f, 0.85f, 0.55f));
				state.Zone.SetLabel($"CAPTURING {ratio * 100f:0}%");
				anyCapturing = true;
				if (state.Progress >= _captureTime)
				{
					state.Captured = true;
					state.Zone.SetLabel("SECURED");
					state.Zone.SetColor(new Color(0.35f, 0.9f, 0.45f));
					state.Pressure.NotifyObjectiveSecured(focus);
					SfxService.Confirm();
				}
			}
			else if (playerIn && contested)
			{
				state.Zone.SetColor(new Color(0.95f, 0.75f, 0.25f));
				state.Zone.SetLabel("CONTESTED");
			}
			else
			{
				state.Progress = Mathf.Max(0f, state.Progress - dt * 0.45f);
				state.Zone.SetColor(new Color(0.35f, 0.7f, 1f));
				state.Zone.SetLabel(_zoneCount == 1 ? "CLAIM ZONE" : state.Zone.ZoneId.Replace("Capture_", "ZONE "));
			}
		}

		if (anyCapturing && _captureSfxCooldown <= 0f)
		{
			SfxService.Play("capture", 1f, -8f);
			_captureSfxCooldown = 1.6f;
		}

		if (allCaptured)
			MarkObjectivesComplete();
	}

	public override string GetHudLine()
	{
		var done = 0;
		foreach (var z in _zones)
		{
			if (z.Captured)
				done++;
		}

		var core = _zoneCount == 1
			? $"OBJECTIVE  Capture the zone  ({done}/1)"
			: $"OBJECTIVE  Secure zones  ({done}/{_zoneCount})";
		return core + ExtractHudHint();
	}

	private bool HostileInZone(MissionZone zone)
	{
		foreach (var child in Host.Root.GetChildren())
		{
			if (child is MechController mech && mech.Team == TeamId.Enemy && mech.Visible
				&& mech.Integrity?.IsCollapsed != true && zone.Contains(mech.GlobalPosition))
				return true;
			if (child is SupportUnit support && support.Team == TeamId.Enemy && support.IsAlive
				&& zone.Contains(support.GlobalPosition))
				return true;
		}

		return false;
	}

	private Vector3[] BuildSites()
	{
		if (_zoneCount == 1)
			return [new Vector3(0f, 0f, 0f)];

		return
		[
			new Vector3(-14f, 0f, 8f),
			new Vector3(14f, 0f, -6f),
			new Vector3(0f, 0f, -16f)
		];
	}

	private Vector3 Offset(Vector3 from, float right, float forward)
	{
		var dir = Vector3.Zero - from;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
			dir = new Vector3(0f, 0f, -1f);
		dir = dir.Normalized();
		var side = dir.Cross(Vector3.Up).Normalized();
		return from + side * right + dir * forward;
	}
}
