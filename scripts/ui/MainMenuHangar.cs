using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Cinematic hangar backdrop for the main menu: three display mechs + atmosphere.</summary>
public partial class MainMenuHangar : Node3D
{
	[Export] public NodePath BayFrontPath { get; set; } = "Bays/BayFront";
	[Export] public NodePath BayLeftPath { get; set; } = "Bays/BayLeft";
	[Export] public NodePath BayRightPath { get; set; } = "Bays/BayRight";
	[Export] public NodePath VfxRootPath { get; set; } = "Vfx";

	private GpuParticles3D? _sparks;
	private float _sparkCooldown;
	private readonly List<HangingPanLight> _hangingLights = new();
	private float _time;

	public override void _Ready()
	{
		DarkenHangar();
		SpawnDisplayMechs();
		SetupHangingLights();
		SetupAtmosphereVfx();
		_sparkCooldown = 1.2f;
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		foreach (var lamp in _hangingLights)
			lamp.ApplySway(_time);

		if (_sparks == null)
			return;

		_sparkCooldown -= (float)delta;
		if (_sparkCooldown > 0f)
			return;

		_sparks.Restart();
		_sparks.Emitting = true;
		_sparkCooldown = (float)GD.RandRange(2.4, 5.5);
	}

	private void DarkenHangar()
	{
		var envNode = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (envNode?.Environment != null)
		{
			var env = envNode.Environment;
			env.BackgroundColor = new Color(0.01f, 0.012f, 0.016f);
			env.AmbientLightColor = new Color(0.12f, 0.14f, 0.16f);
			env.AmbientLightEnergy = 0.06f;
			// Distance fog stays subtle; real mist comes from FogVolumes + lights.
			env.FogEnabled = true;
			env.FogLightColor = new Color(0.16f, 0.18f, 0.2f);
			env.FogDensity = 0.02f;
			env.VolumetricFogEnabled = true;
			env.VolumetricFogDensity = 0.008f;
			env.VolumetricFogAlbedo = new Color(0.62f, 0.66f, 0.7f);
			env.VolumetricFogEmission = new Color(0.012f, 0.014f, 0.018f);
			env.VolumetricFogAnisotropy = 0.4f;
			env.VolumetricFogLength = 64f;
			env.VolumetricFogDetailSpread = 2f;
			env.TonemapExposure = 0.95f;
		}

		// Kill the old broad fill — mechs live in the dark except under pan lights.
		DisableLight("FillLight");
		DisableLight("BaySpot");
		DisableLight("RimLight");
	}

	private void DisableLight(string path)
	{
		var node = GetNodeOrNull(path);
		if (node is Light3D light)
		{
			light.LightEnergy = 0f;
			light.Visible = false;
		}
	}

	private void SpawnDisplayMechs()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		var playerLoadout = session?.Profile.Loadout?.Clone() ?? GameCatalog.CreateStarterLoadout();
		var playerTorso = playerLoadout.TorsoId;

		var leftVariant = PickRearVariant(playerTorso, -1);
		var rightVariant = PickRearVariant(playerTorso, leftVariant);

		SpawnProp("HangarMechFront", BayFrontPath, playerLoadout, yawDegrees: 168f);
		SpawnProp("HangarMechLeft", BayLeftPath, GameCatalog.CreateEnemyLoadout(leftVariant), yawDegrees: 115f);
		SpawnProp("HangarMechRight", BayRightPath, GameCatalog.CreateEnemyLoadout(rightVariant), yawDegrees: -115f);
	}

	private static int PickRearVariant(string playerTorsoId, int excludeVariant)
	{
		for (var attempt = 0; attempt < 8; attempt++)
		{
			var variant = (int)GD.Randi() % 4;
			if (variant == excludeVariant)
				continue;
			var loadout = GameCatalog.CreateEnemyLoadout(variant);
			if (loadout.TorsoId == playerTorsoId)
				continue;
			return variant;
		}

		var fallback = (excludeVariant + 1 + (int)(GD.Randi() % 3)) % 4;
		return fallback;
	}

	private void SpawnProp(string name, NodePath bayPath, LoadoutData loadout, float yawDegrees)
	{
		var bay = GetNodeOrNull<Node3D>(bayPath);
		if (bay == null)
			return;

		var packed = GD.Load<PackedScene>("res://scenes/mech.tscn");
		if (packed == null)
			return;

		var mech = packed.Instantiate<MechController>();
		mech.Name = name;
		mech.HangarDisplayOnly = true;
		mech.IsPlayerControlled = false;
		mech.Team = TeamId.Player;
		AddChild(mech);
		mech.RebuildFromLoadout(loadout);
		mech.SetControlsEnabled(false);
		mech.GlobalPosition = bay.GlobalPosition;
		mech.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
		DisableAsDisplayProp(mech);
	}

	private static void DisableAsDisplayProp(MechController mech)
	{
		mech.CollisionLayer = 0;
		mech.CollisionMask = 0;
		foreach (var child in mech.FindChildren("*", "CollisionShape3D", true, false))
		{
			if (child is CollisionShape3D shape)
				shape.Disabled = true;
		}

		var pilot = mech.GetNodeOrNull<MechPilotAI>("MechPilotAI");
		pilot?.QueueFree();

		StripOcclusionSilhouette(mech);
		Callable.From(() => StripOcclusionSilhouette(mech)).CallDeferred();

		mech.ProcessMode = ProcessModeEnum.Disabled;
	}

	private static void StripOcclusionSilhouette(MechController mech)
	{
		if (!GodotObject.IsInstanceValid(mech))
			return;

		mech.GetNodeOrNull(OcclusionSilhouette.NodeName)?.QueueFree();
		foreach (var child in mech.FindChildren(OcclusionSilhouette.GhostName, "MeshInstance3D", true, false))
			child.QueueFree();
	}

	private void SetupHangingLights()
	{
		var root = new Node3D { Name = "HangingLights" };
		AddChild(root);

		var front = GetNodeOrNull<Node3D>(BayFrontPath)?.GlobalPosition ?? Vector3.Zero;
		var left = GetNodeOrNull<Node3D>(BayLeftPath)?.GlobalPosition ?? Vector3.Zero;
		var right = GetNodeOrNull<Node3D>(BayRightPath)?.GlobalPosition ?? Vector3.Zero;

		_hangingLights.Add(HangingPanLight.Create(root, "PanFront", front + new Vector3(0f, 6.4f, 0.2f), energy: 5.2f, warm: true, phase: 0.2f));
		_hangingLights.Add(HangingPanLight.Create(root, "PanLeft", left + new Vector3(0f, 6.1f, 0f), energy: 3.4f, warm: false, phase: 1.7f));
		_hangingLights.Add(HangingPanLight.Create(root, "PanRight", right + new Vector3(0f, 6.1f, 0f), energy: 3.4f, warm: false, phase: 3.1f));
	}

	private void SetupAtmosphereVfx()
	{
		var root = GetNodeOrNull<Node3D>(VfxRootPath);
		if (root == null)
		{
			root = new Node3D { Name = "Vfx" };
			AddChild(root);
		}

		var bayFront = GetNodeOrNull<Node3D>(BayFrontPath)?.GlobalPosition ?? Vector3.Zero;

		// Low ground sheet — slightly softer so tendrils can read above it.
		AddFogVolume(
			root, "FloorMist",
			position: new Vector3(0f, 0.32f, 1.5f),
			size: new Vector3(36f, 1.0f, 22f),
			density: 0.26f,
			edge: 0.45f,
			heightFalloff: 2.6f,
			shape: RenderingServer.FogVolumeShape.Box);

		AddFogVolume(
			root, "BayFrontMist",
			position: bayFront + new Vector3(0f, 0.26f, 0.4f),
			size: new Vector3(7f, 0.8f, 5.5f),
			density: 0.32f,
			edge: 0.6f,
			heightFalloff: 3.0f,
			shape: RenderingServer.FogVolumeShape.Ellipsoid);

		AddFogVolume(
			root, "SideMistL",
			position: new Vector3(-8f, 0.28f, 2f),
			size: new Vector3(10f, 0.85f, 14f),
			density: 0.22f,
			edge: 0.55f,
			heightFalloff: 2.4f,
			shape: RenderingServer.FogVolumeShape.Box);
		AddFogVolume(
			root, "SideMistR",
			position: new Vector3(8f, 0.28f, 2f),
			size: new Vector3(10f, 0.85f, 14f),
			density: 0.22f,
			edge: 0.55f,
			heightFalloff: 2.4f,
			shape: RenderingServer.FogVolumeShape.Box);

		// Steam tendrils rising off the fog bank — slight, not dramatic.
		SpawnSteamTendrils(root, new Vector3(0f, 0.55f, 2.2f));
		SpawnSteamTendrils(root, bayFront + new Vector3(0f, 0.5f, 0.6f));
		SpawnSteamTendrils(root, new Vector3(-6.5f, 0.5f, 1.8f));
		SpawnSteamTendrils(root, new Vector3(6.5f, 0.5f, 1.8f));

		_sparks = MakeSparkParticles();
		_sparks.Name = "WeldingSparks";
		root.AddChild(_sparks);
		_sparks.GlobalPosition = bayFront + new Vector3(0.55f, 1.65f, 0.35f);
		_sparks.Emitting = false;
	}

	private static void SpawnSteamTendrils(Node3D parent, Vector3 origin)
	{
		var tendrils = MakeSteamTendrils();
		tendrils.Name = $"SteamTendrils_{parent.GetChildCount()}";
		tendrils.Emitting = true;
		parent.AddChild(tendrils);
		tendrils.GlobalPosition = origin;
	}

	private static void AddFogVolume(
		Node3D parent,
		string name,
		Vector3 position,
		Vector3 size,
		float density,
		float edge,
		float heightFalloff,
		RenderingServer.FogVolumeShape shape)
	{
		var mat = new FogMaterial
		{
			Density = density,
			Albedo = new Color(0.68f, 0.72f, 0.76f),
			Emission = new Color(0.018f, 0.02f, 0.025f),
			HeightFalloff = heightFalloff,
			EdgeFade = edge
		};

		var volume = new FogVolume
		{
			Name = name,
			Shape = shape,
			Size = size,
			Material = mat,
			Position = position
		};
		parent.AddChild(volume);
	}

	/// <summary>Tall soft steam fingers rising from the fog bank — subtle motion, no spin.</summary>
	private static GpuParticles3D MakeSteamTendrils()
	{
		var mat = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(3.2f, 0.05f, 1.8f),
			Direction = new Vector3(0.02f, 1f, 0.03f),
			Spread = 8f,
			InitialVelocityMin = 0.12f,
			InitialVelocityMax = 0.28f,
			Gravity = new Vector3(0f, 0.06f, 0f),
			DampingMin = 0.35f,
			DampingMax = 0.7f,
			AngularVelocityMin = 0f,
			AngularVelocityMax = 0f,
			ScaleMin = 0.7f,
			ScaleMax = 1.35f,
			Color = new Color(0.82f, 0.86f, 0.9f, 1f),
			ParticleFlagAlignY = true
		};

		var gradient = new Gradient
		{
			Offsets = new[] { 0f, 0.12f, 0.35f, 0.65f, 0.88f, 1f },
			Colors = new[]
			{
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 0.055f),
				new Color(1f, 1f, 1f, 0.04f),
				new Color(1f, 1f, 1f, 0.022f),
				new Color(1f, 1f, 1f, 0.008f),
				new Color(1f, 1f, 1f, 0f)
			}
		};
		mat.ColorRamp = new GradientTexture1D { Gradient = gradient, Width = 256 };

		var scaleCurve = new Curve();
		scaleCurve.AddPoint(new Vector2(0f, 0.45f));
		scaleCurve.AddPoint(new Vector2(0.25f, 0.9f));
		scaleCurve.AddPoint(new Vector2(0.7f, 1.35f));
		scaleCurve.AddPoint(new Vector2(1f, 1.7f));
		mat.ScaleCurve = new CurveTexture { Curve = scaleCurve };

		// Tall thin card — reads as a rising tendril, not a flat fog blotch.
		var mesh = new QuadMesh { Size = new Vector2(0.55f, 2.4f) };
		var drawMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = new Color(0.88f, 0.9f, 0.93f, 0.55f),
			AlbedoTexture = MakeTendrilTexture(),
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			ParticlesAnimHFrames = 1,
			ParticlesAnimVFrames = 1,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			DisableReceiveShadows = true,
			ProximityFadeEnabled = true,
			ProximityFadeDistance = 0.8f
		};
		mesh.Material = drawMat;

		return new GpuParticles3D
		{
			Amount = 10,
			Lifetime = 7.5f,
			Explosiveness = 0f,
			Randomness = 0.55f,
			LocalCoords = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			VisibilityAabb = new Aabb(new Vector3(-6f, -1f, -5f), new Vector3(12f, 8f, 10f)),
			ProcessMaterial = mat,
			DrawPass1 = mesh
		};
	}

	/// <summary>Vertical soft streak — steam finger, not a round puff.</summary>
	private static ImageTexture MakeTendrilTexture()
	{
		const int w = 48;
		const int h = 128;
		var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		var cx = (w - 1) * 0.5f;
		for (var y = 0; y < h; y++)
		{
			var v = y / (float)(h - 1);
			// Stronger near base, dissolves toward tip.
			var along = Mathf.Sin(v * Mathf.Pi);
			along = Mathf.Pow(along, 0.65f);
			for (var x = 0; x < w; x++)
			{
				var u = (x - cx) / cx;
				// Soft vertical core with slight sideways wobble.
				var wobble = 0.12f * Mathf.Sin(v * 9.5f + u * 2f);
				var dist = Mathf.Abs(u - wobble);
				var radial = Mathf.Clamp(1f - dist * 1.35f, 0f, 1f);
				radial = radial * radial * (3f - 2f * radial);
				var n = Fract(Mathf.Sin(u * 17.1f + v * 23.7f) * 43758.55f);
				var alpha = radial * along * (0.55f + 0.35f * n) * 0.7f;
				image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static ImageTexture MakeWispyFogTexture()
	{
		const int size = 128;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		var center = (size - 1) * 0.5f;

		var lobes = new (float ox, float oy, float sx, float sy, float weight)[]
		{
			(0.00f, 0.00f, 0.95f, 0.48f, 1.00f),
			(-0.28f, 0.12f, 0.55f, 0.38f, 0.70f),
			(0.32f, -0.08f, 0.60f, 0.34f, 0.65f),
			(-0.05f, -0.22f, 0.70f, 0.28f, 0.55f),
			(0.18f, 0.25f, 0.42f, 0.30f, 0.45f),
			(-0.40f, -0.05f, 0.35f, 0.42f, 0.40f)
		};

		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var u = (x - center) / center;
				var v = (y - center) / center;
				var density = 0f;
				foreach (var (ox, oy, sx, sy, weight) in lobes)
				{
					var dx = (u - ox) / sx;
					var dy = (v - oy) / sy;
					var dist = Mathf.Sqrt(dx * dx + dy * dy);
					var lobe = Mathf.Clamp(1f - dist, 0f, 1f);
					lobe = lobe * lobe * (3f - 2f * lobe);
					density += lobe * weight;
				}

				var n = Fract(Mathf.Sin(u * 12.7f + v * 9.3f) * 43758.55f);
				density *= 0.82f + 0.28f * n;
				var alpha = Mathf.Clamp(density * 0.42f, 0f, 0.85f);
				image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static float Fract(float v) => v - Mathf.Floor(v);

	private static GpuParticles3D MakeSparkParticles()
	{
		var mat = new ParticleProcessMaterial
		{
			Direction = new Vector3(0.2f, 1f, 0.4f),
			Spread = 55f,
			InitialVelocityMin = 2.5f,
			InitialVelocityMax = 6.5f,
			Gravity = new Vector3(0f, -9.5f, 0f),
			DampingMin = 1.5f,
			DampingMax = 3f,
			ScaleMin = 0.04f,
			ScaleMax = 0.1f,
			Color = new Color(1f, 0.72f, 0.28f, 1f),
			HueVariationMin = -0.05f,
			HueVariationMax = 0.08f
		};

		var mesh = new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.04f) };
		var drawMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			EmissionEnabled = true,
			Emission = new Color(1f, 0.65f, 0.2f),
			EmissionEnergyMultiplier = 3.5f,
			AlbedoColor = new Color(1f, 0.85f, 0.4f),
			VertexColorUseAsAlbedo = true
		};
		mesh.Material = drawMat;

		return new GpuParticles3D
		{
			Amount = 36,
			Lifetime = 0.55f,
			OneShot = true,
			Explosiveness = 0.92f,
			Randomness = 0.4f,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			VisibilityAabb = new Aabb(new Vector3(-2f, -2f, -2f), new Vector3(4f, 4f, 4f)),
			ProcessMaterial = mat,
			DrawPass1 = mesh
		};
	}

	/// <summary>Rust-style triangular ceiling pan light on a hanging cable.</summary>
	private sealed class HangingPanLight
	{
		private readonly Node3D _pivot;
		private readonly float _phase;
		private readonly float _ampX;
		private readonly float _ampZ;
		private readonly float _speedX;
		private readonly float _speedZ;

		private HangingPanLight(Node3D pivot, float phase)
		{
			_pivot = pivot;
			_phase = phase;
			_ampX = (float)GD.RandRange(1.6, 2.8);
			_ampZ = (float)GD.RandRange(1.2, 2.4);
			_speedX = (float)GD.RandRange(0.35, 0.55);
			_speedZ = (float)GD.RandRange(0.28, 0.48);
		}

		public void ApplySway(float time)
		{
			_pivot.RotationDegrees = new Vector3(
				Mathf.Sin(time * _speedX + _phase) * _ampX,
				0f,
				Mathf.Cos(time * _speedZ + _phase * 1.3f) * _ampZ);
		}

		public static HangingPanLight Create(Node3D parent, string name, Vector3 attachPos, float energy, bool warm, float phase)
		{
			var root = new Node3D { Name = name };
			parent.AddChild(root);
			root.GlobalPosition = attachPos;

			var pivot = new Node3D { Name = "Pivot" };
			root.AddChild(pivot);

			var housingMat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.18f, 0.17f, 0.15f),
				Roughness = 0.72f,
				Metallic = 0.55f
			};
			var emitterMat = new StandardMaterial3D
			{
				AlbedoColor = warm ? new Color(1f, 0.86f, 0.55f) : new Color(0.85f, 0.92f, 1f),
				EmissionEnabled = true,
				Emission = warm ? new Color(1f, 0.78f, 0.4f) : new Color(0.7f, 0.82f, 1f),
				EmissionEnergyMultiplier = 2.8f,
				Roughness = 0.35f
			};

			// Cable from ceiling mount down to the pan.
			var cable = new MeshInstance3D
			{
				Mesh = new CylinderMesh
				{
					TopRadius = 0.018f,
					BottomRadius = 0.018f,
					Height = 1.35f,
					RadialSegments = 8
				},
				MaterialOverride = housingMat,
				Position = new Vector3(0f, -0.65f, 0f)
			};
			pivot.AddChild(cable);

			// Triangular / trapezoid industrial pan housing.
			var pan = new MeshInstance3D
			{
				Mesh = new PrismMesh
				{
					Size = new Vector3(1.55f, 0.22f, 1.15f)
				},
				MaterialOverride = housingMat,
				Position = new Vector3(0f, -1.42f, 0f),
				RotationDegrees = new Vector3(180f, 0f, 0f)
			};
			pivot.AddChild(pan);

			var emitter = new MeshInstance3D
			{
				Mesh = new PrismMesh
				{
					Size = new Vector3(1.25f, 0.04f, 0.9f)
				},
				MaterialOverride = emitterMat,
				Position = new Vector3(0f, -1.52f, 0f),
				RotationDegrees = new Vector3(180f, 0f, 0f)
			};
			pivot.AddChild(emitter);

			var spot = new SpotLight3D
			{
				LightColor = warm ? new Color(1f, 0.82f, 0.55f) : new Color(0.78f, 0.86f, 1f),
				LightEnergy = energy,
				LightIndirectEnergy = 0.65f,
				LightVolumetricFogEnergy = 1.6f,
				SpotRange = 11f,
				SpotAngle = 32f,
				SpotAngleAttenuation = 0.7f,
				ShadowEnabled = true,
				ShadowBias = 0.04f,
				Position = new Vector3(0f, -1.55f, 0f),
				RotationDegrees = new Vector3(-90f, 0f, 0f)
			};
			pivot.AddChild(spot);

			var dust = MakeDustMotes(warm);
			pivot.AddChild(dust);
			dust.Position = new Vector3(0f, -3.6f, 0f);

			return new HangingPanLight(pivot, phase);
		}

		private static GpuParticles3D MakeDustMotes(bool warm)
		{
			var mat = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(1.1f, 2.2f, 1.1f),
				Direction = new Vector3(0.05f, -0.15f, 0.05f),
				Spread = 40f,
				InitialVelocityMin = 0.02f,
				InitialVelocityMax = 0.12f,
				Gravity = new Vector3(0f, -0.02f, 0f),
				DampingMin = 0.4f,
				DampingMax = 1.1f,
				ScaleMin = 0.015f,
				ScaleMax = 0.045f,
				Color = warm
					? new Color(1f, 0.9f, 0.7f, 0.55f)
					: new Color(0.85f, 0.9f, 1f, 0.5f)
			};

			var life = new Gradient
			{
				Offsets = new[] { 0f, 0.2f, 0.7f, 1f },
				Colors = new[]
				{
					new Color(1f, 1f, 1f, 0f),
					new Color(1f, 1f, 1f, 0.7f),
					new Color(1f, 1f, 1f, 0.45f),
					new Color(1f, 1f, 1f, 0f)
				}
			};
			mat.ColorRamp = new GradientTexture1D { Gradient = life, Width = 128 };

			var mesh = new QuadMesh { Size = new Vector2(0.04f, 0.04f) };
			var drawMat = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				VertexColorUseAsAlbedo = true,
				AlbedoColor = Colors.White,
				BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
				DisableReceiveShadows = true,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			mesh.Material = drawMat;

			return new GpuParticles3D
			{
				Amount = 48,
				Lifetime = 9f,
				Explosiveness = 0f,
				Randomness = 0.6f,
				LocalCoords = true,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				VisibilityAabb = new Aabb(new Vector3(-3f, -5f, -3f), new Vector3(6f, 8f, 6f)),
				ProcessMaterial = mat,
				DrawPass1 = mesh
			};
		}
	}
}
