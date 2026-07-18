using Godot;

namespace Mechanize;

/// <summary>
/// World loot — scrap nugget or part crate. Player mech must drive over it.
/// </summary>
public partial class LootPickup : Area3D
{
	public int ScrapAmount { get; private set; }
	public string PartId { get; private set; } = "";

	private bool _taken;
	private Label3D? _label;

	public static LootPickup CreateScrap(Vector3 position, int scrap)
	{
		var drop = new LootPickup
		{
			Name = "ScrapDrop",
			ScrapAmount = Mathf.Max(1, scrap),
			Position = position
		};
		drop.Build(isPart: false);
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
		drop.Build(isPart: true);
		return drop;
	}

	private void Build(bool isPart)
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		Monitoring = true;
		Monitorable = false;

		var color = isPart
			? new Color(0.35f, 0.85f, 0.95f)
			: new Color(0.85f, 0.62f, 0.22f);

		AddChild(new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = 2.4f },
			Position = new Vector3(0f, 1f, 0f)
		});

		var mesh = new MeshInstance3D
		{
			Position = new Vector3(0f, 0.85f, 0f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = color,
				EmissionEnabled = true,
				Emission = color,
				EmissionEnergyMultiplier = 1.2f,
				Roughness = 0.45f,
				Metallic = isPart ? 0.35f : 0.55f
			}
		};
		mesh.Mesh = isPart
			? new BoxMesh { Size = new Vector3(1.1f, 0.55f, 0.85f) }
			: new SphereMesh { Radius = 0.45f, Height = 0.9f };
		AddChild(mesh);

		var title = isPart
			? (GameCatalog.GetPart(PartId)?.DisplayName ?? "PART")
			: $"+{ScrapAmount} SCRAP";
		_label = new Label3D
		{
			Text = title,
			Position = new Vector3(0f, 2.1f, 0f),
			FontSize = 36,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = color
		};
		AddChild(_label);

		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
		// Gentle bob so drops read as interactable.
		RotateY((float)delta * 1.4f);
		if (_label != null)
			_label.Position = new Vector3(0f, 2.1f + Mathf.Sin(Time.GetTicksMsec() * 0.004f) * 0.12f, 0f);
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_taken)
			return;
		if (body is not MechController mech || !mech.IsPlayerControlled)
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

		if (!string.IsNullOrEmpty(PartId))
		{
			session.Match.AddPartDrop(PartId);
			SfxService.Play("disk", 1.05f, -2f);
		}

		Visible = false;
		SetDeferred(Area3D.PropertyName.Monitoring, false);
		CallDeferred(nameof(FreeSafe));
	}

	private void FreeSafe() => MeshMat.QueueFreeSafe(this);
}
