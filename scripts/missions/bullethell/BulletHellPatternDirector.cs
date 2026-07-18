using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Music-synced hellfire patterns, fired from physical HellfireTurret emplacements.
/// </summary>
public sealed class BulletHellPatternDirector
{
	private readonly IMissionHost _host;
	private readonly Node _projectileParent;
	private readonly float _northZ;
	private readonly float _halfX;
	private readonly float _muzzleY = HellfireTurret.MuzzleHeight;
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<HellfireTurret> _turrets = new();
	private readonly List<HellfireTurret> _scratch = new();

	private float _cooldown;
	private int _patternPhase;

	public BulletHellPatternDirector(IMissionHost host, Node projectileParent, float northZ, float halfX)
	{
		_host = host;
		_projectileParent = projectileParent;
		_northZ = northZ;
		_halfX = halfX;
		_rng.Randomize();
	}

	public float DamageScale { get; set; } = 1f;
	public float DensityScale { get; set; } = 1f;

	public void BindTurrets(IEnumerable<HellfireTurret> turrets)
	{
		_turrets.Clear();
		_turrets.AddRange(turrets);
	}

	public void Tick(
		float dt,
		BulletHellMusicClock clock,
		List<BulletHellOnset> dueOnsets,
		List<float> dueBeats,
		Vector3 playerPos)
	{
		_cooldown = Mathf.Max(0f, _cooldown - dt);
		TickTurretAggro(dt, playerPos);

		var intensity = clock.Intensity;
		var densityGate = Mathf.Lerp(0.48f, 0.10f, Mathf.Clamp(intensity * DensityScale, 0f, 1f));

		foreach (var onset in dueOnsets)
		{
			if (onset.Strength < densityGate && !onset.IsDownbeat)
				continue;
			if (_cooldown > 0.03f && onset.Strength < 0.65f && !onset.IsDownbeat)
				continue;

			FireForOnset(onset, playerPos, intensity);
			_cooldown = Mathf.Lerp(0.09f, 0.035f, Mathf.Clamp(intensity, 0f, 1f));
		}

		if (dueBeats.Count > 0 && dueOnsets.Count == 0 && intensity > 0.5f && _cooldown <= 0f && _rng.Randf() < 0.35f)
		{
			FireLaneSpray(playerPos, 0.4f + intensity * 0.25f, lanes: 11);
			_cooldown = 0.08f;
		}
	}

	private void TickTurretAggro(float dt, Vector3 playerPos)
	{
		foreach (var turret in _turrets)
		{
			if (!turret.IsAlive)
				continue;
			if (!turret.TickAggro(dt, playerPos))
				continue;

			// Return fire: tight, faster, from the offended barrel.
			var muzzle = turret.GetMuzzleWorld();
			var toPlayer = playerPos + new Vector3(0f, 1.0f, 0f) - muzzle;
			toPlayer.Y = 0f;
			if (toPlayer.LengthSquared() < 0.01f)
				continue;
			var dir = toPlayer.Normalized();
			var speed = Mathf.Lerp(34f, 44f, turret.AggroIntensity);
			var damage = BaseDamage() * Mathf.Lerp(1.15f, 1.55f, turret.AggroIntensity);
			SpawnStraight(muzzle, dir, speed, damage, turret);
			if (turret.AggroIntensity > 0.7f)
				SpawnStraight(muzzle, dir.Rotated(Vector3.Up, _rng.RandfRange(-0.08f, 0.08f)), speed * 0.95f, damage * 0.85f, turret);
		}
	}

	private void FireForOnset(BulletHellOnset onset, Vector3 playerPos, float intensity)
	{
		_patternPhase = (_patternPhase + 1) % 5;

		if (onset.IsDownbeat || onset.Band == BulletHellBand.Low)
		{
			FireWallWithGap(playerPos, onset.Strength, intensity);
			if (onset.IsDownbeat && intensity > 0.55f && _rng.Randf() < 0.35f)
				FireAimedBurst(playerPos, onset.Strength * 0.7f, count: 1);
			return;
		}

		switch (onset.Band)
		{
			case BulletHellBand.High:
				FireLaneSpray(playerPos, onset.Strength, lanes: 13 + (onset.Strength > 0.6f ? 3 : 0));
				break;
			case BulletHellBand.Mid:
				FireAimedBurst(playerPos, onset.Strength, count: 3 + (onset.Strength > 0.55f ? 1 : 0));
				if (_patternPhase == 0)
					FireSideSlash(playerPos, onset.Strength * 0.75f, bolts: 3);
				break;
			default:
				if (onset.Strength > 0.7f)
					FireFan(playerPos, onset.Strength);
				else if (_patternPhase >= 3)
					FireScatter(playerPos, onset.Strength, count: 4);
				else
					FireAimedBurst(playerPos, onset.Strength, count: 2);
				break;
		}
	}

	private HellfireTurret? PickTurretAhead(Vector3 playerPos, float maxAhead = 70f, float minAhead = 12f)
	{
		CollectAhead(playerPos, maxAhead, minAhead);
		if (_scratch.Count == 0)
			return null;

		// Prefer aggroed batteries, then nearest.
		HellfireTurret? best = null;
		var bestScore = float.MinValue;
		foreach (var turret in _scratch)
		{
			var ahead = playerPos.Z - turret.GlobalPosition.Z;
			var score = -ahead;
			if (turret.IsAggro)
				score += 40f;
			score += _rng.RandfRange(-4f, 4f);
			if (score > bestScore)
			{
				bestScore = score;
				best = turret;
			}
		}

		return best;
	}

	private void CollectAhead(Vector3 playerPos, float maxAhead, float minAhead)
	{
		_scratch.Clear();
		foreach (var turret in _turrets)
		{
			if (!turret.IsAlive)
				continue;
			var ahead = playerPos.Z - turret.GlobalPosition.Z;
			if (ahead < minAhead || ahead > maxAhead)
				continue;
			_scratch.Add(turret);
		}

		// If the player has pushed past everyone, let northern leftovers still fire south.
		if (_scratch.Count == 0)
		{
			foreach (var turret in _turrets)
			{
				if (!turret.IsAlive)
					continue;
				if (turret.GlobalPosition.Z < playerPos.Z - 4f)
					_scratch.Add(turret);
			}
		}
	}

	private Vector3 EmitOrigin(Vector3 playerPos, float preferredX, out HellfireTurret? source)
	{
		source = PickTurretAhead(playerPos);
		if (source != null)
		{
			var muzzle = source.GetMuzzleWorld();
			// Keep the shot tied to the turret Z, but allow pattern X across the lane.
			return new Vector3(
				Mathf.Clamp(preferredX, -_halfX + 1.5f, _halfX - 1.5f),
				muzzle.Y,
				muzzle.Z + _rng.RandfRange(-1.2f, 1.2f));
		}

		var ahead = Mathf.Lerp(30f, 52f, _rng.Randf());
		var z = Mathf.Max(_northZ + 3f, playerPos.Z - ahead);
		return new Vector3(preferredX, _muzzleY, z);
	}

	private void FireWallWithGap(Vector3 playerPos, float strength, float intensity)
	{
		var columns = 19 + (intensity > 0.6f ? 4 : 0);
		var gapWidth = Mathf.Lerp(3.6f, 2.4f, Mathf.Clamp(strength, 0f, 1f));
		var gapCenter = Mathf.Clamp(
			playerPos.X + _rng.RandfRange(-7f, 7f),
			-_halfX + 4f,
			_halfX - 4f);
		var speed = Mathf.Lerp(24f, 34f, strength);
		var damage = BaseDamage() * Mathf.Lerp(0.9f, 1.3f, strength);
		var wallSkew = _rng.RandfRange(-0.04f, 0.04f);
		var anchor = PickTurretAhead(playerPos);
		var z = anchor?.GetMuzzleWorld().Z
			?? Mathf.Max(_northZ + 3f, playerPos.Z - Mathf.Lerp(30f, 52f, _rng.Randf()));

		for (var i = 0; i < columns; i++)
		{
			var u = columns == 1 ? 0.5f : i / (float)(columns - 1);
			var x = Mathf.Lerp(-_halfX + 2.5f, _halfX - 2.5f, u);
			if (Mathf.Abs(x - gapCenter) <= gapWidth * 0.5f)
				continue;

			// Fire from the nearest battery in the band so walls still read as turret barrages.
			var gun = NearestTurretToX(x, z) ?? anchor;
			var muzzle = gun?.GetMuzzleWorld() ?? new Vector3(x, _muzzleY, z);
			var release = new Vector3(
				x + _rng.RandfRange(-0.65f, 0.65f),
				muzzle.Y,
				muzzle.Z + _rng.RandfRange(-1.5f, 1.5f));
			var direction = new Vector3(wallSkew + _rng.RandfRange(-0.025f, 0.025f), 0f, 1f).Normalized();
			SpawnStraight(release, direction, speed * _rng.RandfRange(0.94f, 1.06f), damage, gun);
		}
	}

	private HellfireTurret? NearestTurretToX(float x, float aroundZ)
	{
		HellfireTurret? best = null;
		var bestDist = float.MaxValue;
		foreach (var turret in _turrets)
		{
			if (!turret.IsAlive)
				continue;
			var dz = Mathf.Abs(turret.GlobalPosition.Z - aroundZ);
			if (dz > 55f)
				continue;
			var d = Mathf.Abs(turret.GlobalPosition.X - x) + dz * 0.15f;
			if (d < bestDist)
			{
				bestDist = d;
				best = turret;
			}
		}

		return best;
	}

	private void FireLaneSpray(Vector3 playerPos, float strength, int lanes)
	{
		var speed = Mathf.Lerp(26f, 36f, strength);
		var damage = BaseDamage() * 0.9f;
		var spacing = (_halfX * 2f - 6f) / Mathf.Max(1, lanes - 1);
		var originX = -_halfX + 3f + _rng.RandfRange(-spacing * 0.3f, spacing * 0.3f);
		var safeLane = _rng.RandiRange(1, Mathf.Max(1, lanes - 2));

		for (var i = 0; i < lanes; i++)
		{
			var x = originX + i * spacing + _rng.RandfRange(-0.55f, 0.55f);
			if (Mathf.Abs(x) > _halfX - 2f)
				continue;
			if (i == safeLane)
				continue;
			var release = EmitOrigin(playerPos, x, out var gun);
			release.Z += _rng.RandfRange(-2f, 2f);
			var direction = new Vector3(_rng.RandfRange(-0.035f, 0.035f), 0f, 1f).Normalized();
			SpawnStraight(release, direction, speed * _rng.RandfRange(0.92f, 1.08f), damage, gun);
		}

		if (strength > 0.55f)
			FireAimedBurst(playerPos, strength * 0.75f, count: 1);
	}

	private void FireScatter(Vector3 playerPos, float strength, int count)
	{
		var speed = Mathf.Lerp(24f, 32f, strength);
		var damage = BaseDamage() * 0.85f;
		for (var i = 0; i < count; i++)
		{
			var x = Mathf.Clamp(playerPos.X + _rng.RandfRange(-14f, 14f), -_halfX + 2f, _halfX - 2f);
			var release = EmitOrigin(playerPos, x, out var gun);
			var dir = new Vector3(_rng.RandfRange(-0.2f, 0.2f), 0f, 1f).Normalized();
			SpawnStraight(release, dir, speed, damage, gun);
		}
	}

	private void FireAimedBurst(Vector3 playerPos, float strength, int count)
	{
		var gun = PickTurretAhead(playerPos);
		var origin = gun?.GetMuzzleWorld()
			?? EmitOrigin(playerPos, Mathf.Clamp(playerPos.X + _rng.RandfRange(-4f, 4f), -_halfX + 2f, _halfX - 2f), out _);
		var toPlayer = playerPos + new Vector3(0f, 1.1f, 0f) - origin;
		toPlayer.Y = 0f;
		if (toPlayer.LengthSquared() < 0.01f)
			toPlayer = new Vector3(0f, 0f, 1f);
		toPlayer = toPlayer.Normalized();

		var speed = Mathf.Lerp(28f, 38f, strength);
		var damage = BaseDamage() * Mathf.Lerp(1f, 1.35f, strength);
		var spread = Mathf.Lerp(0.14f, 0.3f, 1f - strength);

		for (var i = 0; i < count; i++)
		{
			var yaw = (i - (count - 1) * 0.5f) * spread;
			var dir = toPlayer.Rotated(Vector3.Up, yaw);
			SpawnStraight(origin, dir, speed * _rng.RandfRange(0.96f, 1.04f), damage, gun);
		}
	}

	private void FireFan(Vector3 playerPos, float strength)
	{
		var gun = PickTurretAhead(playerPos);
		var origin = gun?.GetMuzzleWorld()
			?? EmitOrigin(playerPos, Mathf.Clamp(playerPos.X, -_halfX + 2f, _halfX - 2f), out _);
		var bolts = 7;
		var speed = Mathf.Lerp(25f, 33f, strength);
		var damage = BaseDamage();
		for (var i = 0; i < bolts; i++)
		{
			var u = i / (float)(bolts - 1);
			var yaw = Mathf.Lerp(-0.5f, 0.5f, u);
			var dir = new Vector3(0f, 0f, 1f).Rotated(Vector3.Up, yaw);
			SpawnStraight(origin, dir, speed, damage, gun);
		}
	}

	private void FireSideSlash(Vector3 playerPos, float strength, int bolts)
	{
		var fromLeft = _patternPhase % 2 == 0;
		HellfireTurret? gun = null;
		var best = float.MaxValue;
		foreach (var turret in _turrets)
		{
			if (!turret.IsAlive)
				continue;
			var ahead = playerPos.Z - turret.GlobalPosition.Z;
			if (ahead < 8f || ahead > 55f)
				continue;
			var sideScore = fromLeft ? turret.GlobalPosition.X : -turret.GlobalPosition.X;
			var score = -sideScore + ahead * 0.05f;
			if (score < best)
			{
				best = score;
				gun = turret;
			}
		}

		var muzzle = gun?.GetMuzzleWorld()
			?? new Vector3(fromLeft ? -_halfX + 2f : _halfX - 2f, _muzzleY, Mathf.Max(_northZ + 4f, playerPos.Z - 18f));
		var speed = Mathf.Lerp(26f, 34f, strength);
		var damage = BaseDamage();
		var toward = fromLeft ? 1f : -1f;

		for (var i = 0; i < bolts; i++)
		{
			var dir = new Vector3(toward, 0f, 0.55f).Normalized();
			var release = muzzle + new Vector3(0f, 0f, i * 1.1f);
			SpawnStraight(release, dir, speed, damage, gun);
		}
	}

	private float BaseDamage()
	{
		var difficulty = _host.Difficulty switch
		{
			PilotDifficulty.Hard => 1.25f,
			PilotDifficulty.Medium => 1f,
			_ => 0.7f
		};
		return 7.5f * DamageScale * difficulty;
	}

	private void SpawnStraight(Vector3 from, Vector3 direction, float speed, float damage, Node? source)
	{
		direction.Y = 0f;
		if (direction.LengthSquared() < 0.0001f)
			direction = new Vector3(0f, 0f, 1f);
		direction = direction.Normalized();
		var velocity = direction * speed;
		var travel = Mathf.Max(70f, _halfX * 3.5f);
		var lifetime = Mathf.Clamp(travel / Mathf.Max(8f, speed), 1.4f, 3.8f);

		var bus = NetCombatBus.Find(_host.Root);
		if (bus != null)
		{
			bus.HostSpawnProjectile(
				_projectileParent,
				source,
				from,
				velocity,
				damage,
				lifetime,
				TeamId.Enemy,
				TargetingMode.Standard,
				preferredSlot: -1,
				ballistic: false,
				gravity: 0f,
				playsWorldImpactSfx: false,
				damagesWorldObjects: false);
			return;
		}

		var projectile = Projectile.Create();
		projectile.Source = source;
		projectile.SourceTeam = TeamId.Enemy;
		projectile.Damage = damage;
		projectile.Velocity = velocity;
		projectile.Lifetime = lifetime;
		projectile.GravityAccel = 0f;
		projectile.PlaysWorldImpactSfx = false;
		projectile.DamagesWorldObjects = false;
		_projectileParent.AddChild(projectile);
		projectile.GlobalPosition = from;
		projectile.LookAt(from + direction, Vector3.Up);
	}
}
