using Godot;

namespace Mechanize;

/// <summary>
/// Reusable damage tell. Starts emitting once its owner has lost 60% health
/// (40% remaining), with denser/darker smoke as health approaches zero.
/// </summary>
public partial class DamageSmoke : Node3D
{
	public const float RemainingThreshold = 0.4f;

	private GpuParticles3D? _particles;

	public static DamageSmoke Create(float size = 1f) => new()
	{
		Name = "DamageSmoke",
		Scale = Vector3.One * Mathf.Max(0.2f, size)
	};

	public override void _Ready()
	{
		_particles = BuildParticles();
		AddChild(_particles);
	}

	public void SetHealth(float current, float maximum, bool damageable = true)
	{
		if (_particles == null)
			return;

		var ratio = maximum > 0.01f ? Mathf.Clamp(current / maximum, 0f, 1f) : 1f;
		_particles.Emitting = damageable && ratio <= RemainingThreshold;

		if (!_particles.Emitting)
			return;

		var severity = 1f - ratio / RemainingThreshold;
		_particles.AmountRatio = Mathf.Lerp(0.45f, 1f, severity);
		_particles.SpeedScale = Mathf.Lerp(0.8f, 1.2f, severity);
	}

	public void Stop()
	{
		if (_particles != null)
			_particles.Emitting = false;
	}

	private static GpuParticles3D BuildParticles()
	{
		var life = new Gradient
		{
			Offsets = new[] { 0f, 0.18f, 0.7f, 1f },
			Colors =
			[
				new Color(0.18f, 0.18f, 0.17f, 0f),
				new Color(0.2f, 0.2f, 0.19f, 0.72f),
				new Color(0.12f, 0.12f, 0.12f, 0.42f),
				new Color(0.08f, 0.08f, 0.08f, 0f)
			]
		};

		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.18f,
			Direction = Vector3.Up,
			Spread = 28f,
			InitialVelocityMin = 0.45f,
			InitialVelocityMax = 1.05f,
			Gravity = new Vector3(0f, 0.22f, 0f),
			DampingMin = 0.08f,
			DampingMax = 0.25f,
			ScaleMin = 0.25f,
			ScaleMax = 0.65f,
			ColorRamp = new GradientTexture1D { Gradient = life, Width = 128 }
		};

		var puff = new QuadMesh { Size = new Vector2(0.7f, 0.7f) };
		puff.Material = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			DisableReceiveShadows = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		return new GpuParticles3D
		{
			Amount = 24,
			AmountRatio = 0.45f,
			Lifetime = 2.4f,
			Randomness = 0.55f,
			LocalCoords = false,
			Emitting = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			VisibilityAabb = new Aabb(new Vector3(-4f, -1f, -4f), new Vector3(8f, 8f, 8f)),
			ProcessMaterial = process,
			DrawPass1 = puff
		};
	}
}
