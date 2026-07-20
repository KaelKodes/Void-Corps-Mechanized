using Godot;

namespace Mechanize;

/// <summary>
/// World loot — scrap salvage, crafting materials, or part crates. Drive over to collect.
/// </summary>
public partial class LootPickup : Area3D
{
	public int ScrapAmount { get; private set; }
	public string PartId { get; private set; } = "";
	public string MaterialId { get; private set; } = "";
	public int MaterialAmount { get; private set; }

	private bool _taken;
	private Label3D? _label;
	private Node3D? _visualRoot;
	private float _spin;
	private float _labelBaseY = 2f;
	private Color _accent = new(0.85f, 0.62f, 0.22f);

	public static LootPickup CreateScrap(Vector3 position, int scrap)
	{
		var drop = new LootPickup
		{
			Name = "ScrapDrop",
			ScrapAmount = Mathf.Max(1, scrap),
			Position = position
		};
		drop.BuildScrap();
		return drop;
	}

	public static LootPickup CreateMaterial(Vector3 position, string materialId, int amount = 1)
	{
		var drop = new LootPickup
		{
			Name = "MaterialDrop",
			MaterialId = materialId,
			MaterialAmount = Mathf.Max(1, amount),
			Position = position
		};
		drop.BuildMaterial();
		return drop;
	}

	public static LootPickup CreatePart(Vector3 position, string partId)
	{
		var drop = new LootPickup
		{
			Name = "PartDrop",
			PartId = partId,
			Position = position
		};
		drop.BuildPart();
		return drop;
	}

	public override void _ExitTree()
	{
		MeshMat.DetachBeforeFree(this);
		base._ExitTree();
	}

	private void SetupArea()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		Monitoring = true;
		Monitorable = false;
		AddChild(new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = 2.4f },
			Position = new Vector3(0f, 1f, 0f)
		});
		BodyEntered += OnBodyEntered;
	}

	private void BuildScrap()
	{
		SetupArea();
		_accent = new Color(0.92f, 0.68f, 0.28f);
		_visualRoot = new Node3D { Name = "Visual", Position = new Vector3(0f, 0.15f, 0f) };
		AddChild(_visualRoot);

		var steel = MakeMat(new Color(0.42f, 0.44f, 0.47f), metallic: 0.72f, roughness: 0.42f);
		var dark = MakeMat(new Color(0.22f, 0.23f, 0.25f), metallic: 0.65f, roughness: 0.5f);
		var brass = MakeMat(_accent, metallic: 0.8f, roughness: 0.35f, emission: _accent, emissionEnergy: 0.55f);
		var rust = MakeMat(new Color(0.55f, 0.32f, 0.18f), metallic: 0.4f, roughness: 0.65f);

		// Irregular salvage pile — plates, pipe, and a glowing scrap nugget.
		AddBox(_visualRoot, steel, new Vector3(0.85f, 0.08f, 0.55f), new Vector3(0.05f, 0.22f, 0.05f), new Vector3(0.15f, 0.4f, 0.2f));
		AddBox(_visualRoot, dark, new Vector3(0.55f, 0.07f, 0.7f), new Vector3(-0.2f, 0.28f, -0.1f), new Vector3(-0.35f, -0.25f, 0.55f));
		AddBox(_visualRoot, rust, new Vector3(0.4f, 0.06f, 0.35f), new Vector3(0.25f, 0.35f, -0.15f), new Vector3(0.5f, 0.8f, -0.3f));
		AddBox(_visualRoot, brass, new Vector3(0.28f, 0.12f, 0.22f), new Vector3(-0.05f, 0.42f, 0.12f), new Vector3(0.1f, 0.2f, 0.4f));
		AddCylinder(_visualRoot, dark, 0.07f, 0.55f, new Vector3(0.32f, 0.38f, 0.18f), new Vector3(0.9f, 0f, 0.4f));
		AddSphere(_visualRoot, brass, 0.16f, new Vector3(0f, 0.55f, 0f));

		AddBeaconLight(_accent, 0.7f, 3.5f);
		AddLabel($"+{ScrapAmount} SCRAP", _accent, 2.0f);
	}

	private void BuildMaterial()
	{
		SetupArea();
		var data = MaterialCatalog.All.TryGetValue(MaterialId, out var mat)
			? mat
			: new CraftingMaterialData(MaterialId, "Material", "", new Color(0.7f, 0.7f, 0.7f), 1);
		_accent = data.Color;
		_visualRoot = new Node3D { Name = "Visual", Position = new Vector3(0f, 0.2f, 0f) };
		AddChild(_visualRoot);

		switch (MaterialId)
		{
			case MaterialCatalog.Alloy:
				BuildAlloyVisual(_accent);
				break;
			case MaterialCatalog.Servo:
				BuildServoVisual(_accent);
				break;
			case MaterialCatalog.Circuit:
				BuildCircuitVisual(_accent);
				break;
			case MaterialCatalog.Optics:
				BuildOpticsVisual(_accent);
				break;
			case MaterialCatalog.Reactor:
				BuildReactorVisual(_accent);
				break;
			case MaterialCatalog.Exotic:
				BuildExoticVisual(_accent);
				break;
			default:
				AddBox(_visualRoot!, MakeMat(_accent, 0.4f, 0.5f, _accent, 0.8f),
					new Vector3(0.55f, 0.35f, 0.45f), new Vector3(0f, 0.35f, 0f));
				break;
		}

		AddBeaconLight(_accent, 0.85f, 3.8f);
		var qty = MaterialAmount > 1 ? $" ×{MaterialAmount}" : "";
		AddLabel($"{data.DisplayName.ToUpperInvariant()}{qty}", _accent, 2.05f);
	}

	private void BuildPart()
	{
		SetupArea();
		_accent = new Color(0.35f, 0.85f, 0.95f);
		_visualRoot = new Node3D { Name = "Visual", Position = new Vector3(0f, 0.15f, 0f) };
		AddChild(_visualRoot);

		var shell = MakeMat(new Color(0.18f, 0.22f, 0.26f), metallic: 0.55f, roughness: 0.4f);
		var trim = MakeMat(_accent, metallic: 0.35f, roughness: 0.35f, emission: _accent, emissionEnergy: 0.9f);
		var latch = MakeMat(new Color(0.55f, 0.58f, 0.62f), metallic: 0.7f, roughness: 0.35f);

		AddBox(_visualRoot, shell, new Vector3(1.05f, 0.55f, 0.8f), new Vector3(0f, 0.4f, 0f));
		AddBox(_visualRoot, trim, new Vector3(0.9f, 0.08f, 0.65f), new Vector3(0f, 0.7f, 0f));
		AddBox(_visualRoot, latch, new Vector3(0.22f, 0.12f, 0.18f), new Vector3(0f, 0.55f, -0.42f));
		AddBox(_visualRoot, trim, new Vector3(0.35f, 0.06f, 0.08f), new Vector3(0f, 0.48f, -0.45f));

		AddBeaconLight(_accent, 0.9f, 4f);
		var title = GameCatalog.GetPart(PartId)?.DisplayName ?? "PART";
		AddLabel(title.ToUpperInvariant(), _accent, 2.15f);
	}

	private void BuildAlloyVisual(Color accent)
	{
		var steel = MakeMat(accent, metallic: 0.85f, roughness: 0.32f);
		var dark = MakeMat(accent.Darkened(0.35f), metallic: 0.8f, roughness: 0.4f);
		AddBox(_visualRoot!, steel, new Vector3(0.7f, 0.12f, 0.4f), new Vector3(0f, 0.2f, 0f));
		AddBox(_visualRoot!, dark, new Vector3(0.65f, 0.12f, 0.38f), new Vector3(0.05f, 0.34f, 0.03f), new Vector3(0f, 0.15f, 0f));
		AddBox(_visualRoot!, steel, new Vector3(0.6f, 0.12f, 0.35f), new Vector3(-0.04f, 0.48f, -0.02f), new Vector3(0f, -0.12f, 0f));
	}

	private void BuildServoVisual(Color accent)
	{
		var body = MakeMat(accent, metallic: 0.55f, roughness: 0.4f);
		var dark = MakeMat(accent.Darkened(0.4f), metallic: 0.6f, roughness: 0.45f);
		var glow = MakeMat(accent.Lightened(0.2f), metallic: 0.2f, roughness: 0.35f, emission: accent, emissionEnergy: 1.1f);
		AddCylinder(_visualRoot!, body, 0.22f, 0.35f, new Vector3(0f, 0.35f, 0f), Vector3.Zero);
		AddCylinder(_visualRoot!, dark, 0.14f, 0.45f, new Vector3(0f, 0.45f, 0f), Vector3.Zero);
		AddBox(_visualRoot!, glow, new Vector3(0.5f, 0.08f, 0.12f), new Vector3(0f, 0.35f, 0f));
		AddBox(_visualRoot!, glow, new Vector3(0.12f, 0.08f, 0.5f), new Vector3(0f, 0.35f, 0f));
	}

	private void BuildCircuitVisual(Color accent)
	{
		var board = MakeMat(accent.Darkened(0.45f), metallic: 0.15f, roughness: 0.55f);
		var trace = MakeMat(accent, metallic: 0.2f, roughness: 0.35f, emission: accent, emissionEnergy: 1.3f);
		var chip = MakeMat(new Color(0.15f, 0.16f, 0.18f), metallic: 0.4f, roughness: 0.4f);
		AddBox(_visualRoot!, board, new Vector3(0.75f, 0.06f, 0.55f), new Vector3(0f, 0.25f, 0f));
		AddBox(_visualRoot!, trace, new Vector3(0.55f, 0.02f, 0.04f), new Vector3(0f, 0.3f, -0.12f));
		AddBox(_visualRoot!, trace, new Vector3(0.04f, 0.02f, 0.35f), new Vector3(0.18f, 0.3f, 0.05f));
		AddBox(_visualRoot!, chip, new Vector3(0.22f, 0.1f, 0.22f), new Vector3(-0.12f, 0.34f, 0.08f));
		AddSphere(_visualRoot!, trace, 0.05f, new Vector3(0.22f, 0.32f, 0.15f));
	}

	private void BuildOpticsVisual(Color accent)
	{
		var frame = MakeMat(new Color(0.3f, 0.32f, 0.36f), metallic: 0.7f, roughness: 0.35f);
		var glass = MakeMat(accent, metallic: 0.1f, roughness: 0.15f, emission: accent, emissionEnergy: 1.6f);
		AddCylinder(_visualRoot!, frame, 0.28f, 0.12f, new Vector3(0f, 0.28f, 0f), Vector3.Right * Mathf.Tau * 0.25f);
		AddCylinder(_visualRoot!, glass, 0.22f, 0.08f, new Vector3(0f, 0.28f, 0f), Vector3.Right * Mathf.Tau * 0.25f);
		AddSphere(_visualRoot!, glass, 0.12f, new Vector3(0f, 0.28f, 0.05f));
	}

	private void BuildReactorVisual(Color accent)
	{
		var cage = MakeMat(new Color(0.35f, 0.3f, 0.4f), metallic: 0.65f, roughness: 0.4f);
		var core = MakeMat(accent, metallic: 0.1f, roughness: 0.25f, emission: accent, emissionEnergy: 2.2f);
		AddSphere(_visualRoot!, core, 0.2f, new Vector3(0f, 0.4f, 0f));
		AddBox(_visualRoot!, cage, new Vector3(0.08f, 0.55f, 0.08f), new Vector3(-0.28f, 0.4f, -0.28f));
		AddBox(_visualRoot!, cage, new Vector3(0.08f, 0.55f, 0.08f), new Vector3(0.28f, 0.4f, -0.28f));
		AddBox(_visualRoot!, cage, new Vector3(0.08f, 0.55f, 0.08f), new Vector3(-0.28f, 0.4f, 0.28f));
		AddBox(_visualRoot!, cage, new Vector3(0.08f, 0.55f, 0.08f), new Vector3(0.28f, 0.4f, 0.28f));
		AddBox(_visualRoot!, cage, new Vector3(0.7f, 0.06f, 0.7f), new Vector3(0f, 0.12f, 0f));
		AddBox(_visualRoot!, cage, new Vector3(0.55f, 0.06f, 0.55f), new Vector3(0f, 0.68f, 0f));
	}

	private void BuildExoticVisual(Color accent)
	{
		var crystal = MakeMat(accent, metallic: 0.05f, roughness: 0.2f, emission: accent, emissionEnergy: 2.0f);
		var dark = MakeMat(accent.Darkened(0.35f), metallic: 0.1f, roughness: 0.3f, emission: accent, emissionEnergy: 0.8f);
		AddBox(_visualRoot!, crystal, new Vector3(0.22f, 0.7f, 0.22f), new Vector3(0f, 0.45f, 0f), new Vector3(0.2f, 0.35f, 0.15f));
		AddBox(_visualRoot!, dark, new Vector3(0.16f, 0.45f, 0.16f), new Vector3(0.18f, 0.4f, -0.1f), new Vector3(-0.4f, -0.25f, 0.5f));
		AddBox(_visualRoot!, crystal, new Vector3(0.14f, 0.35f, 0.14f), new Vector3(-0.16f, 0.35f, 0.12f), new Vector3(0.5f, 0.6f, -0.3f));
		AddSphere(_visualRoot!, crystal, 0.1f, new Vector3(0f, 0.75f, 0f));
	}

	private void AddBeaconLight(Color color, float energy, float range)
	{
		AddChild(new OmniLight3D
		{
			Position = new Vector3(0f, 1.1f, 0f),
			LightColor = color,
			LightEnergy = energy,
			OmniRange = range
		});
	}

	private void AddLabel(string text, Color color, float height)
	{
		_labelBaseY = height;
		_label = new Label3D
		{
			Text = text,
			Position = new Vector3(0f, height, 0f),
			FontSize = 34,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = color
		};
		AddChild(_label);
	}

	public override void _Process(double delta)
	{
		_spin += (float)delta * 1.15f;
		if (_visualRoot != null)
		{
			_visualRoot.Rotation = new Vector3(0f, _spin, 0f);
			_visualRoot.Position = new Vector3(0f, 0.15f + Mathf.Sin(_spin * 2.2f) * 0.06f, 0f);
		}

		if (_label != null)
			_label.Position = new Vector3(0f, _labelBaseY + Mathf.Sin(_spin * 2.2f) * 0.1f, 0f);
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_taken)
			return;
		if (body is not MechController mech || !mech.IsLocalPilot)
			return;

		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		if (session?.Match.Active != true)
			return;

		_taken = true;
		if (ScrapAmount > 0)
		{
			session.Match.AddScrap(ScrapAmount);
			SfxService.Play("scrap", 1f, -3f);
		}

		if (!string.IsNullOrEmpty(MaterialId) && MaterialAmount > 0)
		{
			session.Match.AddMaterialDrop(MaterialId, MaterialAmount);
			SfxService.Play("scrap", 1.12f, -4f);
		}

		if (!string.IsNullOrEmpty(PartId))
		{
			session.Match.AddPartDropInstance(session.Profile, PartId);
			SfxService.Play("disk", 1.05f, -2f);
		}

		Visible = false;
		SetDeferred(Area3D.PropertyName.Monitoring, false);
		CallDeferred(nameof(FreeSafe));
	}

	private void FreeSafe() => MeshMat.QueueFreeSafe(this);

	private static StandardMaterial3D MakeMat(
		Color albedo,
		float metallic = 0.35f,
		float roughness = 0.55f,
		Color? emission = null,
		float emissionEnergy = 0f)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = albedo,
			Metallic = metallic,
			Roughness = roughness
		};
		if (emission.HasValue && emissionEnergy > 0f)
		{
			mat.EmissionEnabled = true;
			mat.Emission = emission.Value;
			mat.EmissionEnergyMultiplier = emissionEnergy;
		}

		return mat;
	}

	private static void AddBox(Node3D parent, Material mat, Vector3 size, Vector3 position, Vector3? rotation = null)
	{
		parent.AddChild(MeshMat.Make(new BoxMesh { Size = size }, mat, position, rotation));
	}

	private static void AddCylinder(Node3D parent, Material mat, float radius, float height, Vector3 position, Vector3 rotation)
	{
		parent.AddChild(MeshMat.Make(
			new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
			mat,
			position,
			rotation));
	}

	private static void AddSphere(Node3D parent, Material mat, float radius, Vector3 position)
	{
		parent.AddChild(MeshMat.Make(
			new SphereMesh { Radius = radius, Height = radius * 2f },
			mat,
			position));
	}
}
