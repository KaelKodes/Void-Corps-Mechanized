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
/// Delivery vessel / drop pad. Sealed shell carries a mech; on landing it opens into a
/// textured relay cradle (steel / concrete deck, bay lights, console) used for extract.
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
	private Node3D? _padRoot;
	private Node3D? _terminalRoot;
	private MeshInstance3D? _hull;
	private MeshInstance3D? _hatch;
	private MeshInstance3D? _ring;
	private Label3D? _label;
	private StandardMaterial3D? _hullMat;
	private StandardMaterial3D? _hatchMat;
	private StandardMaterial3D? _ringMat;
	private StandardMaterial3D? _terminalMat;
	private StandardMaterial3D? _bayLightMat;
	private Color _teamColor = new(0.35f, 0.75f, 0.95f);
	private string _readyLabel = "";

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
		beacon.AddToGroup("drop_beacon");
		beacon.SetState(DropBeaconState.Ready);
		beacon.SetOpenAmount(1f);
		return beacon;
	}

	/// <summary>Pad beside spawn, nudged toward the map rim so drops stay off midfield.</summary>
	public static Vector3 PadBesideSpawn(Vector3 spawn, float offset = 4f, float limit = -1f, float limitZ = -1f)
	{
		var awayFromCenter = new Vector3(spawn.X, 0f, spawn.Z);
		if (awayFromCenter.LengthSquared() < 0.01f)
			awayFromCenter = new Vector3(1f, 0f, 1f);
		awayFromCenter = awayFromCenter.Normalized();

		var pad = spawn + awayFromCenter * offset;
		if (limit < 0f)
			limit = ArenaSizeUtil.PadLimit(ArenaSize.Small);
		if (limitZ < 0f)
			limitZ = limit;
		pad.X = Mathf.Clamp(pad.X, -limit, limit);
		pad.Z = Mathf.Clamp(pad.Z, -limitZ, limitZ);
		pad.Y = 0f;
		return pad;
	}

	private void Build()
	{
		_teamColor = Team == TeamId.Player
			? new Color(0.35f, 0.78f, 0.95f)
			: new Color(0.95f, 0.42f, 0.28f);

		BuildSealedVessel();
		BuildOpenPad();
		BuildRelayTerminal();

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

	private void BuildSealedVessel()
	{
		_vesselRoot = new Node3D { Name = "Vessel" };
		AddChild(_vesselRoot);

		_hullMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.55f, 0.58f, 0.62f));
		_hullMat.EmissionEnabled = true;
		_hullMat.Emission = _teamColor;
		_hullMat.EmissionEnergyMultiplier = 0.25f;
		_hull = new MeshInstance3D
		{
			Name = "Hull",
			Mesh = new CylinderMesh
			{
				TopRadius = 2.15f,
				BottomRadius = 2.55f,
				Height = VesselHeight,
				RadialSegments = 16
			},
			Position = new Vector3(0f, VesselHeight * 0.5f, 0f),
			MaterialOverride = _hullMat
		};
		_vesselRoot.AddChild(_hull);

		var stripeMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.PaintedMetal, _teamColor);
		stripeMat.EmissionEnabled = true;
		stripeMat.Emission = _teamColor;
		stripeMat.EmissionEnergyMultiplier = 0.45f;
		_vesselRoot.AddChild(new MeshInstance3D
		{
			Name = "HullStripe",
			Mesh = new CylinderMesh
			{
				TopRadius = 2.22f,
				BottomRadius = 2.22f,
				Height = 0.32f,
				RadialSegments = 16
			},
			Position = new Vector3(0f, VesselHeight * 0.62f, 0f),
			MaterialOverride = stripeMat
		});

		var finMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.4f, 0.42f, 0.46f));
		for (var i = 0; i < 4; i++)
		{
			var yaw = i * Mathf.Tau * 0.25f;
			_vesselRoot.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.6f, 0.85f) },
				Position = new Vector3(Mathf.Sin(yaw) * 2.35f, 1.1f, Mathf.Cos(yaw) * 2.35f),
				Rotation = new Vector3(0f, yaw, 0f),
				MaterialOverride = finMat
			});
			_vesselRoot.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.18f, 0.7f) },
				Position = new Vector3(Mathf.Sin(yaw) * 2.55f, 0.12f, Mathf.Cos(yaw) * 2.55f),
				Rotation = new Vector3(0f, yaw, 0f),
				MaterialOverride = finMat
			});
		}

		_hatchMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.Steel, _teamColor.Darkened(0.2f));
		_hatchMat.EmissionEnabled = true;
		_hatchMat.Emission = _teamColor;
		_hatchMat.EmissionEnergyMultiplier = 0.4f;
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
	}

	private void BuildOpenPad()
	{
		_padRoot = new Node3D { Name = "OpenPad", Visible = false };
		AddChild(_padRoot);

		var deckMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.48f, 0.5f, 0.54f), uvScaleMul: 0.85f);
		var plateMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.Steel, new Color(0.62f, 0.64f, 0.68f), uvScaleMul: 0.7f);
		var concreteMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.ConcreteRough, new Color(0.55f, 0.56f, 0.58f));
		var paintMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.PaintedMetal, _teamColor);
		paintMat.EmissionEnabled = true;
		paintMat.Emission = _teamColor;
		paintMat.EmissionEnergyMultiplier = 0.35f;

		// Outer apron — reads as poured pad / hardstand.
		_padRoot.AddChild(new MeshInstance3D
		{
			Name = "Apron",
			Mesh = new CylinderMesh
			{
				TopRadius = Radius + 0.35f,
				BottomRadius = Radius + 0.55f,
				Height = 0.28f,
				RadialSegments = 28
			},
			Position = new Vector3(0f, 0.12f, 0f),
			MaterialOverride = concreteMat
		});

		// Main steel deck.
		_padRoot.AddChild(new MeshInstance3D
		{
			Name = "Deck",
			Mesh = new CylinderMesh
			{
				TopRadius = Radius - 0.55f,
				BottomRadius = Radius - 0.45f,
				Height = 0.22f,
				RadialSegments = 24
			},
			Position = new Vector3(0f, 0.28f, 0f),
			MaterialOverride = deckMat
		});

		// Inner bay floor (recessed).
		_padRoot.AddChild(new MeshInstance3D
		{
			Name = "BayFloor",
			Mesh = new CylinderMesh
			{
				TopRadius = 2.05f,
				BottomRadius = 2.15f,
				Height = 0.16f,
				RadialSegments = 20
			},
			Position = new Vector3(0f, 0.18f, 0f),
			MaterialOverride = plateMat
		});

		// Cross grating plates inside the bay.
		_padRoot.AddChild(new MeshInstance3D
		{
			Name = "GrateX",
			Mesh = new BoxMesh { Size = new Vector3(3.6f, 0.06f, 0.35f) },
			Position = new Vector3(0f, 0.3f, 0f),
			MaterialOverride = plateMat
		});
		_padRoot.AddChild(new MeshInstance3D
		{
			Name = "GrateZ",
			Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.06f, 3.6f) },
			Position = new Vector3(0f, 0.3f, 0f),
			MaterialOverride = plateMat
		});

		// Team chevron ring on the deck.
		_padRoot.AddChild(new MeshInstance3D
		{
			Name = "ChevronRing",
			Mesh = new TorusMesh
			{
				InnerRadius = 3.35f,
				OuterRadius = 3.7f,
				Rings = 12,
				RingSegments = 36
			},
			Position = new Vector3(0f, 0.42f, 0f),
			MaterialOverride = paintMat
		});

		// Perimeter blast skirts + corner posts — the "body" when open.
		var wallMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.42f, 0.44f, 0.48f));
		for (var i = 0; i < 4; i++)
		{
			var yaw = i * Mathf.Tau * 0.25f + Mathf.Pi * 0.25f;
			var wall = new MeshInstance3D
			{
				Name = $"Skirt_{i}",
				Mesh = new BoxMesh { Size = new Vector3(3.4f, 1.15f, 0.22f) },
				Position = new Vector3(Mathf.Sin(yaw) * 4.6f, 0.7f, Mathf.Cos(yaw) * 4.6f),
				Rotation = new Vector3(0f, yaw, 0f),
				MaterialOverride = wallMat
			};
			_padRoot.AddChild(wall);

			var postYaw = i * Mathf.Tau * 0.25f;
			_padRoot.AddChild(new MeshInstance3D
			{
				Name = $"Post_{i}",
				Mesh = new BoxMesh { Size = new Vector3(0.38f, 1.7f, 0.38f) },
				Position = new Vector3(Mathf.Sin(postYaw) * 5.4f, 0.9f, Mathf.Cos(postYaw) * 5.4f),
				MaterialOverride = wallMat
			});
			_padRoot.AddChild(new MeshInstance3D
			{
				Name = $"BeaconLamp_{i}",
				Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.18f, 0.22f) },
				Position = new Vector3(Mathf.Sin(postYaw) * 5.4f, 1.85f, Mathf.Cos(postYaw) * 5.4f),
				MaterialOverride = paintMat
			});
		}

		// Recessed bay light strips (not a floating glow blob).
		_bayLightMat = SurfaceLibrary.Flat(
			new Color(_teamColor.R, _teamColor.G, _teamColor.B, 0.85f),
			metallic: 0.15f,
			roughness: 0.35f,
			emission: _teamColor,
			emissionEnergy: 1.1f);
		_bayLightMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		for (var i = 0; i < 4; i++)
		{
			var yaw = i * Mathf.Tau * 0.25f;
			_padRoot.AddChild(new MeshInstance3D
			{
				Name = $"BayLight_{i}",
				Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.08f, 0.22f) },
				Position = new Vector3(Mathf.Sin(yaw) * 1.55f, 0.36f, Mathf.Cos(yaw) * 1.55f),
				Rotation = new Vector3(0f, yaw + Mathf.Pi * 0.5f, 0f),
				MaterialOverride = _bayLightMat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			});
		}

		_ringMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.PaintedMetal, _teamColor);
		_ringMat.EmissionEnabled = true;
		_ringMat.Emission = _teamColor;
		_ringMat.EmissionEnergyMultiplier = 0.9f;
		_ring = new MeshInstance3D
		{
			Name = "Ring",
			Mesh = new TorusMesh
			{
				InnerRadius = Radius - 0.35f,
				OuterRadius = Radius - 0.05f,
				Rings = 14,
				RingSegments = 40
			},
			Position = new Vector3(0f, 0.38f, 0f),
			MaterialOverride = _ringMat
		};
		_padRoot.AddChild(_ring);
	}

	private void BuildRelayTerminal()
	{
		_terminalRoot = new Node3D
		{
			Name = "RelayTerminal",
			Position = new Vector3(0f, 0f, 2.55f),
			Visible = false
		};
		AddChild(_terminalRoot);

		_terminalMat = SurfaceLibrary.Get(SurfaceLibrary.Kind.SteelDark, new Color(0.45f, 0.48f, 0.52f));
		var consolePaint = SurfaceLibrary.Get(SurfaceLibrary.Kind.PaintedMetal, _teamColor.Darkened(0.1f));

		_terminalRoot.AddChild(new MeshInstance3D
		{
			Name = "Base",
			Mesh = new BoxMesh { Size = new Vector3(1.45f, 0.35f, 1.1f) },
			Position = new Vector3(0f, 0.2f, 0f),
			MaterialOverride = _terminalMat
		});
		_terminalRoot.AddChild(new MeshInstance3D
		{
			Name = "Pedestal",
			Mesh = new BoxMesh { Size = new Vector3(1.05f, 1.35f, 0.75f) },
			Position = new Vector3(0f, 1.0f, 0.05f),
			MaterialOverride = _terminalMat
		});
		_terminalRoot.AddChild(new MeshInstance3D
		{
			Name = "ConsoleShelf",
			Mesh = new BoxMesh { Size = new Vector3(1.2f, 0.12f, 0.55f) },
			Position = new Vector3(0f, 1.55f, 0.45f),
			Rotation = new Vector3(Mathf.DegToRad(-18f), 0f, 0f),
			MaterialOverride = consolePaint
		});

		var screenMat = SurfaceLibrary.Flat(
			_teamColor,
			metallic: 0.15f,
			roughness: 0.3f,
			emission: _teamColor,
			emissionEnergy: 1.6f);
		_terminalRoot.AddChild(new MeshInstance3D
		{
			Name = "Screen",
			Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.55f, 0.06f) },
			Position = new Vector3(0f, 1.85f, 0.52f),
			Rotation = new Vector3(Mathf.DegToRad(-12f), 0f, 0f),
			MaterialOverride = screenMat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		});

		_terminalRoot.AddChild(new MeshInstance3D
		{
			Name = "AntennaMast",
			Mesh = new CylinderMesh
			{
				TopRadius = 0.05f,
				BottomRadius = 0.07f,
				Height = 1.1f,
				RadialSegments = 8
			},
			Position = new Vector3(0.35f, 2.35f, -0.15f),
			MaterialOverride = _terminalMat
		});
		_terminalRoot.AddChild(new MeshInstance3D
		{
			Name = "Dish",
			Mesh = new SphereMesh { Radius = 0.32f, Height = 0.32f },
			Position = new Vector3(0.35f, 2.95f, -0.15f),
			Scale = new Vector3(1f, 0.32f, 1f),
			MaterialOverride = consolePaint
		});
	}

	public void SetReadyLabel(string text)
	{
		_readyLabel = text ?? "";
		RefreshVisual();
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
		ExtractProgress = Mathf.Clamp(ExtractProgress + dt / ExtractHoldSeconds, 0f, 1f);
		RefreshVisual();
		if (ExtractProgress < 1f)
			return false;

		SetState(DropBeaconState.Retrieved);
		return true;
	}

	public void MarkDropping()
	{
		SetState(DropBeaconState.Dropping);
		// Must reseal — Create()/Ready leaves OpenAmount at 1, which parks an open pad
		// at drop altitude and looks like a second map floating above the claim.
		SetOpenAmount(0f);
	}

	public void MarkOpening() => SetState(DropBeaconState.Opening);

	public void MarkReady()
	{
		if (State == DropBeaconState.Retrieved)
			return;
		SetState(DropBeaconState.Ready);
	}

	/// <summary>Animate vessel hatch / hull. 0 sealed, 1 open relay pad.</summary>
	public void SetOpenAmount(float amount)
	{
		OpenAmount = Mathf.Clamp(amount, 0f, 1f);
		var t = OpenAmount;
		var ease = 1f - (1f - t) * (1f - t);

		if (_vesselRoot != null)
		{
			// Sealed flight silhouette → collapses into a low shipping collar around the pad.
			_vesselRoot.Scale = new Vector3(1f, Mathf.Lerp(1f, 0.22f, ease), 1f);
			_vesselRoot.Visible = t < 0.98f;
			if (_hull != null)
				_hull.Transparency = Mathf.Lerp(0f, 0.15f, ease);
		}

		if (_hatchPivot != null)
			_hatchPivot.RotationDegrees = new Vector3(Mathf.Lerp(0f, -118f, ease), 0f, 0f);

		if (_padRoot != null)
		{
			var showPad = t > 0.15f;
			_padRoot.Visible = showPad;
			if (showPad)
			{
				var rise = Mathf.Clamp((t - 0.15f) / 0.85f, 0f, 1f);
				var riseEase = 1f - (1f - rise) * (1f - rise);
				_padRoot.Scale = new Vector3(
					Mathf.Lerp(0.85f, 1f, riseEase),
					Mathf.Lerp(0.35f, 1f, riseEase),
					Mathf.Lerp(0.85f, 1f, riseEase));
			}
		}

		if (_ring != null)
		{
			var ringScale = Mathf.Lerp(0.45f, 1f, ease);
			_ring.Scale = new Vector3(ringScale, 1f, ringScale);
		}

		if (_bayLightMat != null)
		{
			var pulse = t < 0.95f ? 0.55f + Mathf.Sin(t * Mathf.Pi) * 0.45f : 0.75f;
			_bayLightMat.EmissionEnergyMultiplier = pulse * 1.4f;
			_bayLightMat.AlbedoColor = new Color(_teamColor.R, _teamColor.G, _teamColor.B, 0.55f + pulse * 0.35f);
		}

		if (_terminalRoot != null)
		{
			var show = t > 0.4f;
			_terminalRoot.Visible = show;
			if (show)
			{
				var rise = Mathf.Clamp((t - 0.4f) / 0.6f, 0f, 1f);
				var riseEase = 1f - (1f - rise) * (1f - rise);
				_terminalRoot.Position = new Vector3(0f, Mathf.Lerp(-1.4f, 0f, riseEase), 2.55f);
				_terminalRoot.Scale = new Vector3(1f, Mathf.Lerp(0.25f, 1f, riseEase), 1f);
			}
		}

		if (_label != null)
			_label.Position = new Vector3(0f, Mathf.Lerp(VesselHeight + 1.4f, 3.6f, ease), 0f);

		if (_hullMat != null)
			_hullMat.EmissionEnergyMultiplier = Mathf.Lerp(0.45f, 0.12f, ease);
		if (_hatchMat != null)
			_hatchMat.EmissionEnergyMultiplier = Mathf.Lerp(0.55f, 0.2f, ease);
		if (_terminalMat != null)
			_terminalMat.EmissionEnergyMultiplier = Mathf.Lerp(0.15f, 0.4f, ease);
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
			_hullMat.EmissionEnergyMultiplier = energy * 0.28f;
		if (_terminalMat != null)
			_terminalMat.EmissionEnergyMultiplier = energy * 0.35f;
		if (_bayLightMat != null)
			_bayLightMat.EmissionEnergyMultiplier = 0.8f + energy * 0.45f;

		_label.Text = State switch
		{
			DropBeaconState.Dropping => "VESSEL INBOUND",
			DropBeaconState.Opening => "DEPLOYING",
			DropBeaconState.ExtractArmed => "HOLD [F] — SIGNAL RETRIEVAL",
			DropBeaconState.Extracting => $"RETRIEVING {ExtractProgress * 100f:0}%",
			DropBeaconState.Retrieved => "RETRIEVED",
			DropBeaconState.Ready when !string.IsNullOrEmpty(_readyLabel) => _readyLabel,
			DropBeaconState.Ready when Team == TeamId.Player => "RELAY TERMINAL",
			_ => Team == TeamId.Player ? "DROP BEACON" : "DROP PAD"
		};
		_label.Modulate = State is DropBeaconState.ExtractArmed or DropBeaconState.Extracting
			? new Color(0.45f, 1f, 0.55f)
			: _teamColor;
	}
}
