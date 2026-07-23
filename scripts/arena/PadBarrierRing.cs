using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Invisible play-edge ring: keeps wall colliders, swaps visuals to the SciFi shield shader.
/// Trial for map containment FX — same shader family as future MAP energy shields.
/// </summary>
public sealed class PadBarrierRing
{
	private const string ShaderPath = "res://art/vfx/shield/shd_shield.gdshader";
	private static readonly string[] WallNames = ["WallNorth", "WallSouth", "WallWest", "WallEast"];

	private readonly Node3D _arenaRoot;
	private readonly Dictionary<string, ShaderMaterial> _mats = new();
	private readonly Dictionary<string, float> _hitAge = new(); // 0 → 1 over HitDuration
	private ShaderMaterial? _prototype;

	private const float HitDuration = 0.45f;

	public PadBarrierRing(Node3D arenaRoot)
	{
		_arenaRoot = arenaRoot;
	}

	public void Apply()
	{
		_mats.Clear();
		_hitAge.Clear();
		_prototype = BuildPrototype();
		if (_prototype == null)
		{
			GD.PushWarning($"PadBarrierRing: failed to build shield material from {ShaderPath}");
			return;
		}

		foreach (var name in WallNames)
		{
			var mesh = _arenaRoot.GetNodeOrNull<MeshInstance3D>($"World/{name}/Mesh");
			if (mesh == null)
				continue;

			var mat = (ShaderMaterial)_prototype.Duplicate();
			// Idle: no impact ring; fresnel-only edge.
			mat.SetShaderParameter("hit_progress", 1f);
			mat.SetShaderParameter("hit_position", Vector3.Up);
			MeshMat.Bind(mesh, mat);
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			_mats[name] = mat;
			_hitAge[name] = 1f;
		}
	}

	private static ShaderMaterial? BuildPrototype()
	{
		var shader = GD.Load<Shader>(ShaderPath);
		if (shader == null)
			return null;

		var noise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
			Frequency = 0.05f
		};
		var noiseTex = new NoiseTexture2D
		{
			Width = 512,
			Height = 512,
			Noise = noise
		};

		var mat = new ShaderMaterial { Shader = shader };
		mat.SetShaderParameter("shield_color", new Color(0.35f, 0.75f, 1f, 0.85f));
		mat.SetShaderParameter("noise_texture", noiseTex);
		mat.SetShaderParameter("wave_height", 0.02f);
		mat.SetShaderParameter("wave_speed", new Vector2(0.015f, 0.008f));
		mat.SetShaderParameter("fresnel_power", 2.4f);
		mat.SetShaderParameter("center_visibility", 0f);
		mat.SetShaderParameter("proximity_visibility", 0f);
		mat.SetShaderParameter("proximity_point_world", Vector3.Zero);
		mat.SetShaderParameter("proximity_radius", 7f);
		mat.SetShaderParameter("hit_position", Vector3.Zero);
		mat.SetShaderParameter("hit_progress", 1f);
		mat.SetShaderParameter("hit_color", new Color(1.2f, 0.95f, 0.55f));
		mat.SetShaderParameter("hit_radius", 2.5f);
		mat.SetShaderParameter("hit_ring_width", 0.35f);
		mat.SetShaderParameter("hit_push_force", 0.15f);
		mat.SetShaderParameter("hit_point_world", Vector3.Zero);
		mat.SetShaderParameter("hit_world_radius", 6f);
		return mat;
	}

	/// <summary>Start fading the rim glow in within this distance (meters).</summary>
	private const float ProximityFar = 12f;
	/// <summary>Full idle glow by this distance.</summary>
	private const float ProximityNear = 4.5f;
	/// <summary>How wide the glow patch is along the wall face.</summary>
	private const float BlobRadius = 7f;

	public void Tick(float dt, MechController? mech)
	{
		if (_mats.Count == 0)
			return;

		var mechValid = mech != null && GodotObject.IsInstanceValid(mech);
		if (mechValid)
			TryPulseFromCollisions(mech!);

		var mechPos = mechValid ? mech!.GlobalPosition : Vector3.Zero;

		foreach (var name in WallNames)
		{
			if (!_mats.TryGetValue(name, out var mat))
				continue;

			var age = _hitAge.GetValueOrDefault(name, 1f);
			if (age < 1f)
			{
				age = Mathf.Min(1f, age + dt / HitDuration);
				_hitAge[name] = age;
				mat.SetShaderParameter("hit_progress", age);
			}

			var proximity = 0f;
			var blobCenter = Vector3.Zero;
			if (mechValid)
			{
				proximity = ProximityToWall(name, mechPos);
				blobCenter = ProjectOntoWall(name, mechPos);
				if (age < 0.95f)
					proximity = Mathf.Max(proximity, 1f - age);
			}

			mat.SetShaderParameter("proximity_visibility", proximity);
			mat.SetShaderParameter("proximity_point_world", blobCenter);
			mat.SetShaderParameter("proximity_radius", BlobRadius);
		}
	}

	private float ProximityToWall(string wallName, Vector3 mechPos)
	{
		var wall = _arenaRoot.GetNodeOrNull<Node3D>($"World/{wallName}");
		if (wall == null)
			return 0f;

		var wp = wall.GlobalPosition;
		var dist = wallName is "WallNorth" or "WallSouth"
			? Mathf.Abs(mechPos.Z - wp.Z)
			: Mathf.Abs(mechPos.X - wp.X);

		if (dist >= ProximityFar)
			return 0f;
		if (dist <= ProximityNear)
			return 1f;
		return 1f - (dist - ProximityNear) / (ProximityFar - ProximityNear);
	}

	private Vector3 ProjectOntoWall(string wallName, Vector3 mechPos)
	{
		var wall = _arenaRoot.GetNodeOrNull<Node3D>($"World/{wallName}");
		if (wall == null)
			return mechPos;

		var wp = wall.GlobalPosition;
		var y = Mathf.Clamp(mechPos.Y + 1.1f, 0.8f, PerimeterApproxTop());
		return wallName is "WallNorth" or "WallSouth"
			? new Vector3(mechPos.X, y, wp.Z)
			: new Vector3(wp.X, y, mechPos.Z);
	}

	private static float PerimeterApproxTop() => 24f;

	private void TryPulseFromCollisions(MechController mech)
	{
		var count = mech.GetSlideCollisionCount();
		for (var i = 0; i < count; i++)
		{
			var col = mech.GetSlideCollision(i);
			if (col.GetCollider() is not Node collider)
				continue;

			var wallBody = collider as Node3D;
			if (wallBody == null)
				continue;

			// Collision may be on the StaticBody or a child shape owner.
			var wallName = wallBody.Name.ToString();
			if (!wallName.StartsWith("Wall"))
			{
				var parent = wallBody.GetParentOrNull<Node3D>();
				if (parent == null || !parent.Name.ToString().StartsWith("Wall"))
					continue;
				wallBody = parent;
				wallName = wallBody.Name.ToString();
			}

			if (!_mats.ContainsKey(wallName))
				continue;

			// Retrigger only when idle / nearly finished so walking along the wall isn't a strobe.
			if (_hitAge.GetValueOrDefault(wallName, 1f) < 0.85f)
				continue;

			var hitWorld = col.GetPosition();
			_mats[wallName].SetShaderParameter("hit_point_world", hitWorld);
			_mats[wallName].SetShaderParameter("hit_world_radius", 6f);
			_mats[wallName].SetShaderParameter("hit_progress", 0f);
			_hitAge[wallName] = 0f;
		}
	}
}
