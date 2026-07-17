using Godot;

namespace Mechanize;

public enum DropBeaconState
{
	Idle,
	Dropping,
	Opening,
	Ready,
	ExtractArmed,
	Extracting,
	Retrieved
}

/// <summary>
/// Delivery vessel / drop pad. Sealed shell carries a mech; on landing it opens and becomes the extract beacon.
/// </summary>
public partial class DropBeacon : Node3D
{
	public const float DefaultRadius = 7f;
	public const float ExtractHoldSeconds = 2f;
	public const float VesselHeight = 5.6f;

	public TeamId Team { get; private set; } = TeamId.Player;
	public float Radius { get; private set; } = DefaultRadius;
	public DropBeaconState State { get; private set; } = DropBeaconState.Idle;
	public float ExtractProgress { get; private set; }
	/// <summary>0 = sealed vessel, 1 = fully open pad.</summary>
	public float OpenAmount { get; private set; }

	private Node3D? _vesselRoot;
	private Node3D? _hatchPivot;
	private Node3D? _terminalRoot;
	private MeshInstance3D? _hull;
	private MeshInstance3D? _hatch;
	private MeshInstance3D? _ring;
	private MeshInstance3D? _glow;
	private MeshInstance3D? _terminalScreen;
	private Label3D? _label;
	private StandardMaterial3D? _hullMat;
	private StandardMaterial3D? _hatchMat;
	private StandardMaterial3D? _ringMat;
	private StandardMaterial3D? _glowMat;
	private StandardMaterial3D? _terminalMat;
	private Color _teamColor = new(0.35f, 0.75f, 0.95f);

	public static DropBeacon Create(string name, Vector3 position, TeamId team, float radius = DefaultRadius)
	{
		var beacon = new DropBeacon
		{
			Name = name,
			Team = team,
			Radius = radius,
			Position = position
		};
		beacon.Build();
		beacon.SetState(DropBeaconState.Ready);
		beacon.SetOpenAmount(1f);
		return beacon;
	}

	/// <summary>Pad beside spawn, nudged toward the map rim so drops stay off midfield.</summary>
	public static Vector3 PadBesideSpawn(Vector3 spawn, float offset = 4f, float limit = -1f)
	{
		var awayFromCenter = new Vector3(spawn.X, 0f, spawn.Z);
		if (awayFromCenter.LengthSquared() < 0.01f)
			awayFromCenter = new Vector3(1f, 0f, 1f);
		awayFromCenter = awayFromCenter.Normalized();

		var pad = spawn + awayFromCenter * offset;
		if (limit < 0f)
			limit = ArenaSizeUtil.PadLimit(ArenaSize.Small);
		pad.X = Mathf.Clamp(pad.X, -limit, limit);
		pad.Z = Mathf.Clamp(pad.Z, -limit, limit);
		pad.Y = 0f;
		return pad;
	}

	private void Build()
	{
		_teamColor = Team == TeamId.Player
			? new Color(0.35f, 0.78f, 0.95f)
			: new Color(0.95f, 0.42f, 0.28f);

		_vesselRoot = new Node3D { Name = "Vessel" };
		AddChild(_vesselRoot);

		_hullMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.16f, 0.18f, 0.22f),
			Metallic = 0.72f,
			Roughness = 0.32f,
			EmissionEnabled = true,
			Emission = _teamColor,
			EmissionEnergyMultiplier = 0.35f
		};
		_hull = new MeshInstance3D
		{
			Name = "Hull",
			Mesh = new CylinderMesh
			{
				TopRadius = 2.15f,
				BottomRadius = 2.55f,
				Height = VesselHeight,
				RadialSegments = 14
			},
			Position = new Vector3(0f, VesselHeight * 0.5f, 0f),
			MaterialOverride = _hullMat
		};
		_vesselRoot.AddChild(_hull);

		// Hull stripe + landing fins for a less-generic pod silhouette.
		var stripeMat = new StandardMaterial3D
		{
			AlbedoColor = _teamColor.Darkened(0.15f),
			Metallic = 0.65f,
			Roughness = 0.35f,
			EmissionEnabled = true,
			Emission = _teamColor,
			EmissionEnergyMultiplier = 0.55f
		};
		_vesselRoot.AddChild(new MeshInstance3D
		{
			Name = "HullStripe",
			Mesh = new CylinderMesh
			{
				TopRadius = 2.2f,
				BottomRadius = 2.2f,
				Height = 0.35f,
				RadialSegments = 14
			},
			Position = new Vector3(0f, VesselHeight * 0.62f, 0f),
			MaterialOverride = stripeMat
		});
		var finMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.12f, 0.14f, 0.17f),
			Metallic = 0.7f,
			Roughness = 0.4f
		};
		for (var i = 0; i < 4; i++)
		{
			var yaw = i * Mathf.Tau * 0.25f;
			var fin = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.6f, 0.85f) },
				Position = new Vector3(Mathf.Sin(yaw) * 2.35f, 1.1f, Mathf.Cos(yaw) * 2.35f),
				Rotation = new Vector3(0f, yaw, 0f),
				MaterialOverride = finMat
			};
			_vesselRoot.AddChild(fin);
			var foot = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.18f, 0.7f) },
				Position = new Vector3(Mathf.Sin(yaw) * 2.55f, 0.12f, Mathf.Cos(yaw) * 2.55f),
				Rotation = new Vector3(0f, yaw, 0f),
				MaterialOverride = finMat
			};
			_vesselRoot.AddChild(foot);
		}

		_hatchMat = new StandardMaterial3D
		{
			AlbedoColor = _teamColor.Darkened(0.4f),
			Metallic = 0.75f,
			Roughness = 0.28f,
			EmissionEnabled = true,
			Emission = _teamColor,
			EmissionEnergyMultiplier = 0.55f
		};
		// Pivot at the rear lip so the hatch flips open like a cargo door.
		_hatchPivot = new Node3D
		{
			Name = "HatchPivot",
			Position = new Vector3(0f, VesselHeight - 0.05f, -2.0f)
		};
		_vesselRoot.AddChild(_hatchPivot);
		_hatch = new MeshInstance3D
		{
			Name = "Hatch",
			Mesh = new BoxMesh { Size = new Vector3(4.2f, 0.28f, 4.0f) },
			Position = new Vector3(0f, 0.1f, 2.0f),
			MaterialOverride = _hatchMat
		};
		_hatchPivot.AddChild(_hatch);

		_glowMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(_teamColor.R, _teamColor.G, _teamColor.B, 0.35f),
			EmissionEnabled = true,
			Emission = _teamColor,
			EmissionEnergyMultiplier = 1.2f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		_glow = new MeshInstance3D
		{
			Name = "BayGlow",
			Mesh = new CylinderMesh
			{
				TopRadius = 1.6f,
				BottomRadius = 1.6f,
				Height = 0.2f,
				RadialSegments = 12
			},
			Position = new Vector3(0f, 0.35f, 0f),
			MaterialOverride = _glowMat,
			Visible = false
		};
		AddChild(_glow);

		// Relay console — rises from the bay once the vessel opens (phone-home terminal).
		_terminalRoot = new Node3D
		{
			Name = "RelayTerminal",
			Position = new Vector3(0f, 0f, 1.35f),
			Visible = false
		};
		AddChild(_terminalRoot);
		_terminalMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.14f, 0.16f, 0.2f),
			Metallic = 0.6f,
			Roughness = 0.4f,
			EmissionEnabled = true,
			Emission = _teamColor,
			EmissionEnergyMultiplier = 0.45f
		};
		var pedestal = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(1.1f, 1.8f, 0.7f) },
			Position = new Vector3(0f, 0.9f, 0f),
			MaterialOverride = _terminalMat
		};
		_terminalRoot.AddChild(pedestal);
		_terminalScreen = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.85f, 0.55f, 0.08f) },
			Position = new Vector3(0f, 1.55f, 0.38f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = _teamColor,
				EmissionEnabled = true,
				Emission = _teamColor,
				EmissionEnergyMultiplier = 1.4f
			}
		};
		_terminalRoot.AddChild(_terminalScreen);
		var dish = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.35f, Height = 0.35f },
			Position = new Vector3(0f, 2.05f, -0.05f),
			Scale = new Vector3(1f, 0.35f, 1f),
			MaterialOverride = _terminalMat
		};
		_terminalRoot.AddChild(dish);

		_ringMat = new StandardMaterial3D
		{
			AlbedoColor = _teamColor,
			EmissionEnabled = true,
			Emission = _teamColor,
			EmissionEnergyMultiplier = 0.9f,
			Roughness = 0.4f
		};
		_ring = new MeshInstance3D
		{
			Name = "Ring",
			Mesh = new TorusMesh
			{
				InnerRadius = Radius - 0.4f,
				OuterRadius = Radius,
				Rings = 16,
				RingSegments = 40
			},
			Position = new Vector3(0f, 0.08f, 0f),
			MaterialOverride = _ringMat
		};
		AddChild(_ring);

		_label = new Label3D
		{
			Text = Team == TeamId.Player ? "DROP BEACON" : "INBOUND DROP",
			Position = new Vector3(0f, VesselHeight + 1.4f, 0f),
			FontSize = 42,
			OutlineSize = 10,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = _teamColor
		};
		AddChild(_label);
	}

	public bool Contains(Vector3 worldPoint)
	{
		var delta = worldPoint - GlobalPosition;
		delta.Y = 0f;
		return delta.LengthSquared() <= Radius * Radius;
	}

	public void SetState(DropBeaconState state)
	{
		State = state;
		ExtractProgress = state is DropBeaconState.Extracting ? ExtractProgress : 0f;
		RefreshVisual();
	}

	public void ArmExtract()
	{
		if (State is DropBeaconState.Retrieved or DropBeaconState.Extracting)
			return;
		SetState(DropBeaconState.ExtractArmed);
	}

	/// <summary>
	/// Tick player extract channel. Returns true when retrieval completes.
	/// </summary>
	public bool TickExtract(float dt, bool holdingInteract, Vector3 playerWorldPos)
	{
		if (State is DropBeaconState.Retrieved)
			return false;

		if (State is not (DropBeaconState.ExtractArmed or DropBeaconState.Extracting))
			return false;

		var inZone = Contains(playerWorldPos);
		if (!inZone || !holdingInteract)
		{
			if (State == DropBeaconState.Extracting)
				SetState(DropBeaconState.ExtractArmed);
			ExtractProgress = 0f;
			RefreshVisual();
			return false;
		}

		State = DropBeaconState.Extracting;
		ExtractProgress = Mathf.Min(1f, ExtractProgress + dt / ExtractHoldSeconds);
		RefreshVisual();

		if (ExtractProgress < 1f)
			return false;

		SetState(DropBeaconState.Retrieved);
		return true;
	}

	public void MarkDropping()
	{
		SetOpenAmount(0f);
		SetState(DropBeaconState.Dropping);
	}

	public void MarkOpening() => SetState(DropBeaconState.Opening);

	public void MarkReady()
	{
		if (State == DropBeaconState.Retrieved)
			return;
		SetOpenAmount(1f);
		SetState(DropBeaconState.Ready);
	}

	/// <summary>Animate vessel hatch / hull. 0 sealed, 1 open relay pad.</summary>
	public void SetOpenAmount(float amount)
	{
		OpenAmount = Mathf.Clamp(amount, 0f, 1f);
		var t = OpenAmount;
		// Ease-out so the hatch pops then settles.
		var ease = 1f - (1f - t) * (1f - t);

		if (_vesselRoot != null)
		{
			// Hull settles into a low shipping shell — stays on-site as the pad body.
			_vesselRoot.Scale = new Vector3(1f, Mathf.Lerp(1f, 0.38f, ease), 1f);
			_vesselRoot.Visible = true;
		}

		if (_hatchPivot != null)
			_hatchPivot.RotationDegrees = new Vector3(Mathf.Lerp(0f, -118f, ease), 0f, 0f);

		if (_ring != null)
		{
			var ringScale = Mathf.Lerp(0.35f, 1f, ease);
			_ring.Scale = new Vector3(ringScale, 1f, ringScale);
			_ring.Visible = true;
		}

		if (_glow != null)
		{
			// Soft bay light remains after deploy.
			_glow.Visible = t > 0.2f;
			if (_glowMat != null)
			{
				var a = t < 0.95f ? Mathf.Sin(t * Mathf.Pi) * 0.55f : 0.28f;
				_glowMat.AlbedoColor = new Color(_teamColor.R, _teamColor.G, _teamColor.B, a);
				_glowMat.EmissionEnergyMultiplier = 0.7f + a * 1.8f;
			}
		}

		if (_terminalRoot != null)
		{
			var show = t > 0.35f;
			_terminalRoot.Visible = show;
			if (show)
			{
				var rise = Mathf.Clamp((t - 0.35f) / 0.65f, 0f, 1f);
				var riseEase = 1f - (1f - rise) * (1f - rise);
				_terminalRoot.Position = new Vector3(0f, Mathf.Lerp(-1.2f, 0f, riseEase), 1.35f);
				_terminalRoot.Scale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, riseEase), 1f);
			}
		}

		if (_label != null)
			_label.Position = new Vector3(0f, Mathf.Lerp(VesselHeight + 1.4f, 4.0f, ease), 0f);

		if (_hullMat != null)
			_hullMat.EmissionEnergyMultiplier = Mathf.Lerp(0.55f, 0.25f, ease);
		if (_hatchMat != null)
			_hatchMat.EmissionEnergyMultiplier = Mathf.Lerp(0.7f, 0.35f, ease);
		if (_terminalMat != null)
			_terminalMat.EmissionEnergyMultiplier = Mathf.Lerp(0.2f, 0.55f, ease);
	}

	private void RefreshVisual()
	{
		if (_label == null)
			return;

		var energy = State switch
		{
			DropBeaconState.ExtractArmed => 1.8f,
			DropBeaconState.Extracting => 2.4f,
			DropBeaconState.Dropping => 1.6f,
			DropBeaconState.Opening => 2.0f,
			DropBeaconState.Retrieved => 0.2f,
			_ => 0.9f
		};

		if (_ringMat != null)
			_ringMat.EmissionEnergyMultiplier = energy;
		if (_hullMat != null)
			_hullMat.EmissionEnergyMultiplier = energy * 0.35f;
		if (_terminalMat != null)
			_terminalMat.EmissionEnergyMultiplier = energy * 0.4f;

		_label.Text = State switch
		{
			DropBeaconState.Dropping => "VESSEL INBOUND",
			DropBeaconState.Opening => "DEPLOYING",
			DropBeaconState.ExtractArmed => "HOLD [E] — SIGNAL RETRIEVAL",
			DropBeaconState.Extracting => $"RETRIEVING {ExtractProgress * 100f:0}%",
			DropBeaconState.Retrieved => "RETRIEVED",
			DropBeaconState.Ready when Team == TeamId.Player => "RELAY TERMINAL",
			_ => Team == TeamId.Player ? "DROP BEACON" : "DROP PAD"
		};
		_label.Modulate = State is DropBeaconState.ExtractArmed or DropBeaconState.Extracting
			? new Color(0.45f, 1f, 0.55f)
			: _teamColor;
	}
}
