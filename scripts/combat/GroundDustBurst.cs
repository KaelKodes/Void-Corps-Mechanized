using Godot;

namespace Mechanize;

/// <summary>
/// One-shot ground dust kicked up by heavy MAP contact (landings, foot plants).
/// Intensity 0–1 scales puff count, radius, and blow-out speed.
/// Spawns for every MAP in view — player, remotes, and AI.
/// </summary>
public partial class GroundDustBurst : Node3D
{
	private const float StepViewRange = 36f;
	private const float LandingViewRange = 55f;

	public static void SpawnLanding(Node3D mech, float fallSpeedNormalized, Vector3? origin = null)
	{
		var intensity = Mathf.Clamp(fallSpeedNormalized, 0f, 1f);
		intensity = Mathf.Lerp(0.35f, 1f, intensity * intensity);
		Spawn(mech, origin ?? mech.GlobalPosition, intensity, LandingViewRange, chassisBoost: true);
	}

	public static void SpawnFootstep(Node3D mech, Vector3 origin, float weightNormalized, float pace)
	{
		var weight = Mathf.Clamp(weightNormalized, 0f, 1f);
		var paceT = Mathf.Clamp(pace, 0f, 1f);
		var intensity = Mathf.Lerp(0.12f, 0.28f, weight) * Mathf.Lerp(0.85f, 1.2f, paceT);
		Spawn(mech, origin, intensity, StepViewRange, chassisBoost: true);
	}

	private static void Spawn(
		Node3D mech,
		Vector3 origin,
		float intensity,
		float viewRange,
		bool chassisBoost)
	{
		if (intensity < 0.06f)
			return;

		var parent = mech.GetTree()?.CurrentScene ?? mech.GetParent();
		if (parent == null)
			return;

		if (!IsInViewRange(mech, origin, viewRange))
			return;

		var sizeScale = 1f;
		if (chassisBoost && mech is MechController map)
			sizeScale = MechChassisClassUtil.VisualScale(map.ChassisClass);

		var burst = new GroundDustBurst { Name = "GroundDustBurst" };
		parent.AddChild(burst);
		burst.GlobalPosition = origin + Vector3.Up * 0.05f;
		burst.Build(intensity, sizeScale);
	}

	private static bool IsInViewRange(Node from, Vector3 origin, float range)
	{
		var cam = from.GetViewport()?.GetCamera3D();
		if (cam != null && GodotObject.IsInstanceValid(cam))
			return cam.GlobalPosition.DistanceTo(origin) <= range;

		foreach (var node in from.GetTree().GetNodesInGroup("mechs"))
		{
			if (node is MechController { IsLocalPilot: true } local)
				return local.GlobalPosition.DistanceTo(origin) <= range;
		}

		return true;
	}

	private void Build(float intensity, float sizeScale)
	{
		sizeScale = Mathf.Max(0.35f, sizeScale);
		var amount = (int)Mathf.Round(Mathf.Lerp(12f, 42f, intensity));
		var emitRadius = Mathf.Lerp(0.28f, 1.15f, intensity) * sizeScale;
		var blowSpeed = Mathf.Lerp(1.1f, 4.8f, intensity) * sizeScale;
		var puffScale = Mathf.Lerp(0.35f, 1.05f, intensity) * sizeScale;
		var lifetime = Mathf.Lerp(0.55f, 1.15f, intensity);

		var life = new Gradient
		{
			Offsets = new[] { 0f, 0.12f, 0.55f, 1f },
			Colors =
			[
				new Color(0.62f, 0.55f, 0.42f, 0f),
				new Color(0.58f, 0.5f, 0.38f, 0.55f),
				new Color(0.45f, 0.4f, 0.32f, 0.28f),
				new Color(0.35f, 0.32f, 0.28f, 0f)
			]
		};

		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
			EmissionRingRadius = emitRadius * 0.35f,
			EmissionRingInnerRadius = 0.02f,
			EmissionRingHeight = 0.02f,
			EmissionRingAxis = Vector3.Up,
			Direction = Vector3.Up,
			Spread = 78f,
			InitialVelocityMin = blowSpeed * 0.45f,
			InitialVelocityMax = blowSpeed,
			Gravity = new Vector3(0f, -0.35f, 0f),
			DampingMin = 1.2f,
			DampingMax = 2.8f,
			ScaleMin = puffScale * 0.55f,
			ScaleMax = puffScale,
			ColorRamp = new GradientTexture1D { Gradient = life, Width = 128 },
			ParticleFlagAlignY = false
		};

		var mesh = new QuadMesh { Size = new Vector2(0.55f, 0.55f) * puffScale };
		mesh.Material = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			DisableReceiveShadows = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		var particles = new GpuParticles3D
		{
			Amount = amount,
			Lifetime = lifetime,
			OneShot = true,
			Explosiveness = 0.92f,
			Randomness = 0.65f,
			LocalCoords = false,
			Emitting = true,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			VisibilityAabb = new Aabb(
				new Vector3(-6f, -1f, -6f) * sizeScale,
				new Vector3(12f, 8f, 12f) * sizeScale),
			ProcessMaterial = process,
			DrawPass1 = mesh
		};
		AddChild(particles);

		var cleanup = GetTree().CreateTimer(lifetime + 0.45);
		cleanup.Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(this))
				QueueFree();
		};
	}
}
