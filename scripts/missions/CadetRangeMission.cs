using Godot;

namespace Mechanize;

/// <summary>
/// Scripted cadet range: teach movement, fire/heat, abilities, cover/scrap, damage+mend, extract.
/// Cannot fail — deaths free-respawn.
/// </summary>
public sealed class CadetRangeMission : MissionBase
{
	private enum Step
	{
		Move,
		Fire,
		Ability,
		CoverScrap,
		TakeHit,
		Mend,
		Extract
	}

	private Step _step = Step.Move;
	private Vector3 _spawnPos;
	private bool _moved;
	private bool _fired;
	private bool _usedAbility;
	private bool _gotScrap;
	private bool _tookHit;
	private bool _mended;
	private bool _hitApplied;
	private float _scrapWatch;
	private int _scrapAtStart = -1;
	private DummyTarget? _drillTarget;

	public CadetRangeMission() : base(MissionType.CadetRange) { }

	public override void SetupBattlefield()
	{
		var root = EnsureMissionRoot();
		_spawnPos = Host.PlayerSpawn;
		Host.DespawnEnemyMech("Trinova_Detachment");
		Host.DespawnEnemyMech("OuroTech_Detachment");
		Host.DespawnSupport("Player_Tank");
		Host.DespawnSupport("Player_Tower");
		Host.DespawnSupport("EnemyA_Tank");
		Host.DespawnSupport("EnemyA_Tower");
		Host.DespawnSupport("EnemyA_Buggy");
		Host.DespawnSupport("EnemyB_Tank");
		Host.DespawnSupport("EnemyB_Tower");

		// Soft training plate to shoot — not a lethal MAP.
		_drillTarget = new DummyTarget { Name = "RangeDrillPlate" };
		root.AddChild(_drillTarget);
		_drillTarget.GlobalPosition = Host.PlayerSpawn + new Vector3(-14f, 0f, -10f);
	}

	public override void OnFightStarted()
	{
		_spawnPos = Host.Player?.GlobalPosition ?? Host.PlayerSpawn;
		_scrapAtStart = TelemetryUtil.Match(Host.Root)?.RunScrap ?? 0;
	}

	public override void Tick(float dt)
	{
		if (ObjectivesComplete || Host.MatchResolved)
			return;

		var player = Host.Player;
		if (player == null)
			return;

		var session = TelemetryUtil.Match(Host.Root);
		switch (_step)
		{
			case Step.Move:
				if (!_moved)
				{
					var delta = player.GlobalPosition - _spawnPos;
					delta.Y = 0f;
					if (delta.Length() > 6f)
					{
						_moved = true;
						_step = Step.Fire;
					}
				}
				break;

			case Step.Fire:
				if (!_fired)
				{
					var shots = session?.Telemetry.ShotsFired ?? 0;
					if (shots > 0 || player.PowerHeat is { HeatRatio: > 0.02f })
					{
						_fired = true;
						_step = Step.Ability;
					}
				}
				break;

			case Step.Ability:
				if (!_usedAbility)
				{
					var util = session?.Telemetry.UtilityUses ?? 0;
					if (util > 0)
					{
						_usedAbility = true;
						_step = Step.CoverScrap;
						SpawnTrainingScrap();
					}
				}
				break;

			case Step.CoverScrap:
				_scrapWatch += dt;
				var scrap = session?.RunScrap ?? 0;
				if (!_gotScrap && scrap > _scrapAtStart)
				{
					_gotScrap = true;
					_step = Step.TakeHit;
				}
				else if (!_gotScrap && _scrapWatch > 12f)
				{
					session?.AddScrap(5);
					_gotScrap = true;
					_step = Step.TakeHit;
				}
				break;

			case Step.TakeHit:
				if (!_hitApplied)
				{
					_hitApplied = true;
					player.Health?.ApplyDamage(Mathf.Max(18f, player.Health.MaxHealth * 0.12f));
					SfxService.PlayDamageSustained();
				}

				if (!_tookHit && player.Health != null
				    && player.Health.CurrentHealth < player.Health.MaxHealth - 1f)
				{
					_tookHit = true;
					_step = Step.Mend;
				}
				break;

			case Step.Mend:
				var healed = session?.Telemetry.HealApplied ?? 0f;
				if (!_mended && (healed > 5f
				                 || (player.Health != null
				                     && player.Health.CurrentHealth >= player.Health.MaxHealth * 0.92f)))
				{
					_mended = true;
					_step = Step.Extract;
					MarkObjectivesComplete();
				}
				break;
		}
	}

	public override string GetHudLine()
	{
		return _step switch
		{
			Step.Move =>
				$"CADET RANGE  Move with {InputBindings.FormatAction("move_forward")}/{InputBindings.FormatAction("move_back")}/{InputBindings.FormatAction("turn_left")}/{InputBindings.FormatAction("turn_right")}  ·  Sprint {InputBindings.FormatAction("sprint")}",
			Step.Fire =>
				$"CADET RANGE  Fire {InputBindings.FormatAction("fire_primary")} / {InputBindings.FormatAction("fire_secondary")}  ·  Watch HEAT on the HUD",
			Step.Ability =>
				$"CADET RANGE  Use an ability ({InputBindings.FormatAction("ability_1")}–{InputBindings.FormatAction("ability_6")})  ·  Remap anytime in Pause → Controls",
			Step.CoverScrap =>
				"CADET RANGE  Break the marked scrap crate and drive over the pickup",
			Step.TakeHit =>
				"CADET RANGE  Training pulse incoming — sustain the hit",
			Step.Mend =>
				$"CADET RANGE  Heal up — Mend Beacon: hold {InputBindings.FormatAction("ability_1")} (or your mend key), aim, release",
			Step.Extract =>
				"CADET RANGE  Objectives clear" + ExtractHudHint(),
			_ => "CADET RANGE"
		};
	}

	private void SpawnTrainingScrap()
	{
		var pos = Host.PlayerSpawn + new Vector3(8f, 0f, 6f);
		LootService.SpawnWorldDrops(Host.Root, pos, scrap: 8, partId: null);
		// Also leave a visible crate-ish marker via DummyTarget scrap if available.
		var root = Host.Root.GetNodeOrNull<Node3D>("MissionRuntime");
		if (root == null)
			return;
		var marker = new MeshInstance3D
		{
			Name = "ScrapDrillMarker",
			Mesh = new BoxMesh { Size = new Vector3(1.4f, 1.1f, 1.4f) },
			MaterialOverride = MakeMat(new Color(0.75f, 0.55f, 0.2f), 0.85f)
		};
		root.AddChild(marker);
		marker.GlobalPosition = pos + Vector3.Up * 0.55f;
	}
}
