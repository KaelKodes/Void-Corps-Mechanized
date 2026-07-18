using Godot;

namespace Mechanize;

public partial class ShatterBurst : Node3D
{
	public static void Spawn(
		Node parent,
		Vector3 origin,
		Color color,
		Vector3 sourceSize,
		int pieceCount = 14)
	{
		var burst = new ShatterBurst { Name = "ShatterBurst" };
		parent.AddChild(burst);
		burst.GlobalPosition = origin;
		burst.Build(color, sourceSize, pieceCount);
		SfxService.Play("explosion", (float)GD.RandRange(0.85, 1.1), -1f);
	}

	private void Build(Color color, Vector3 sourceSize, int pieceCount)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		var flash = new OmniLight3D
		{
			LightColor = new Color(1f, 0.7f, 0.35f),
			LightEnergy = 4f,
			OmniRange = 10f
		};
		AddChild(flash);

		var chunkSize = new Vector3(
			Mathf.Clamp(sourceSize.X / 4f, 0.25f, 0.9f),
			Mathf.Clamp(sourceSize.Y / 4f, 0.25f, 0.9f),
			Mathf.Clamp(sourceSize.Z / 4f, 0.25f, 0.9f));

		for (var i = 0; i < pieceCount; i++)
		{
			var body = new RigidBody3D
			{
				CollisionLayer = 0,
				CollisionMask = 1,
				Mass = 0.4f + rng.Randf() * 0.6f,
				GravityScale = 1.4f,
				LinearDamp = 0.4f,
				AngularDamp = 0.3f
			};

			var shape = new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = chunkSize * (0.6f + rng.Randf() * 0.7f) }
			};
			body.AddChild(shape);

			var mat = new StandardMaterial3D
			{
				AlbedoColor = color.Lerp(new Color(0.15f, 0.15f, 0.15f), rng.Randf() * 0.35f),
				Roughness = 0.85f,
				Metallic = 0.15f
			};
			var mesh = MeshMat.Make(
				new BoxMesh { Size = ((BoxShape3D)shape.Shape).Size },
				mat,
				castShadow: GeometryInstance3D.ShadowCastingSetting.Off);
			body.AddChild(mesh);

			AddChild(body);
			body.Position = new Vector3(
				rng.RandfRange(-sourceSize.X * 0.35f, sourceSize.X * 0.35f),
				rng.RandfRange(0.1f, sourceSize.Y * 0.5f),
				rng.RandfRange(-sourceSize.Z * 0.35f, sourceSize.Z * 0.35f));

			var outward = body.Position;
			if (outward.LengthSquared() < 0.01f)
				outward = new Vector3(rng.RandfRange(-1f, 1f), 0.5f, rng.RandfRange(-1f, 1f));
			outward = outward.Normalized();

			body.LinearVelocity = outward * rng.RandfRange(6f, 14f) + Vector3.Up * rng.RandfRange(4f, 9f);
			body.AngularVelocity = new Vector3(
				rng.RandfRange(-8f, 8f),
				rng.RandfRange(-8f, 8f),
				rng.RandfRange(-8f, 8f));
		}

		var timer = GetTree().CreateTimer(0.12);
		timer.Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(flash))
				flash.QueueFree();
		};

		var cleanup = GetTree().CreateTimer(2.4);
		cleanup.Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(this))
				MeshMat.QueueFreeSafe(this);
		};
	}
}
