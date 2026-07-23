using Godot;

namespace Mechanize;

/// <summary>
/// Diegetic heat + power brackets on the cockpit front glass.
/// Left = chassis heat pool; right = operational power. Segment stacks fill bottom → top.
/// Owned by CockpitDiegeticHud (TopLevel) so torso rebuilds cannot free us mid-frame.
/// </summary>
public partial class CockpitWindowHeatBars : Node3D
{
	private const int SegmentCount = 8;
	private const float BarWidth = 0.055f;
	private const float BarHeight = 0.42f;
	private const float SegmentGap = 0.008f;
	private const float GlassInset = 0.022f;
	private const float DefaultBarXOffset = 0.48f;
	/// <summary>Thinspine glass is narrower — pull bars inward off the side pillars.</summary>
	private const float ThinspineBarXOffset = 0.38f;
	/// <summary>Anvil: default offset plus three bar-widths farther from center.</summary>
	private const float AnvilBarXOffset = DefaultBarXOffset + BarWidth * 3f;
	private const int LayoutVersion = 14;

	private MeshInstance3D[] _segsL = System.Array.Empty<MeshInstance3D>();
	private MeshInstance3D[] _segsR = System.Array.Empty<MeshInstance3D>();
	private StandardMaterial3D[] _matsL = System.Array.Empty<StandardMaterial3D>();
	private StandardMaterial3D[] _matsR = System.Array.Empty<StandardMaterial3D>();
	private Label3D? _overheatL;
	private ulong _torsoId;
	private string _visualKind = "";
	private int _builtLayoutVersion;
	private bool _built;
	private bool _visible;

	public void Refresh(MechController mech, bool firstPerson)
	{
		if (!NodeAlive(this))
			return;

		var show = firstPerson
		           && mech.IsPlayerControlled
		           && CockpitDiegeticHud.MechHasCockpitScreens(mech);
		if (!show)
		{
			SetBarsVisible(false);
			return;
		}

		var torso = FindCockpitTorso(mech);
		if (torso == null)
		{
			SetBarsVisible(false);
			return;
		}

		var visualKind = GetTorsoVisualKind(mech);
		EnsureBuilt(torso, visualKind);
		if (!_built || !NodeAlive(this))
			return;

		// Follow torso in world space without being parented under it (survives Assemble()).
		TopLevel = true;
		GlobalTransform = torso.GlobalTransform;

		SetBarsVisible(true);

		var power = mech.PowerHeat;
		ApplySegments(_segsL, _matsL, power?.HeatRatio ?? 0f, MeterKind.Heat);
		ApplySegments(_segsR, _matsR, power?.PowerRatio ?? 0f, MeterKind.Power);
		SetOverheatLabel(_overheatL, power?.IsOverheated == true);
	}

	/// <summary>Safe teardown — never touches Godot APIs on a disposed / queued instance.</summary>
	public void TearDown()
	{
		ResetManagedState();
		if (!NodeAlive(this))
			return;

		var parent = GetParent();
		parent?.RemoveChild(this);
		QueueFree();
	}

	private void EnsureBuilt(Node3D torsoRoot, string visualKind)
	{
		if (!NodeAlive(this) || !NodeAlive(torsoRoot))
			return;

		var torsoId = torsoRoot.GetInstanceId();
		if (_built
		    && _torsoId == torsoId
		    && _visualKind == visualKind
		    && _builtLayoutVersion == LayoutVersion)
			return;

		FreeChildrenSafe();
		Name = "WindowHeatBars";
		TopLevel = true;
		_visualKind = visualKind;
		_builtLayoutVersion = LayoutVersion;

		var glass = torsoRoot.FindChild("ViewPanel", recursive: true, owned: false) as MeshInstance3D;
		var glassPos = glass?.Position ?? new Vector3(0f, 0.51f, -0.47f);
		var z = glassPos.Z + GlassInset;
		var y = glassPos.Y;
		var xOffset = visualKind switch
		{
			"torso_ouro_thin" => ThinspineBarXOffset,
			"torso_brin_anvil" => AnvilBarXOffset,
			_ => DefaultBarXOffset
		};

		var barL = BuildBar("HeatBar", new Vector3(-xOffset, y, z), out _matsL);
		var barR = BuildBar("PowerBar", new Vector3(xOffset, y, z), out _matsR);
		AddChild(barL);
		AddChild(barR);

		_overheatL = MakeOverheatLabel(barL);

		_segsL = CollectSegments(barL);
		_segsR = CollectSegments(barR);
		_torsoId = torsoId;
		_built = true;
		Visible = false;
		_visible = false;
	}

	private void FreeChildrenSafe()
	{
		ResetManagedState();
		if (!NodeAlive(this))
			return;

		foreach (var child in GetChildren())
		{
			if (!NodeAlive(child))
				continue;
			RemoveChild(child);
			child.QueueFree();
		}
	}

	private void ResetManagedState()
	{
		_built = false;
		_visible = false;
		_torsoId = 0;
		_visualKind = "";
		_builtLayoutVersion = 0;
		_segsL = System.Array.Empty<MeshInstance3D>();
		_segsR = System.Array.Empty<MeshInstance3D>();
		_matsL = System.Array.Empty<StandardMaterial3D>();
		_matsR = System.Array.Empty<StandardMaterial3D>();
		_overheatL = null;
	}

	private static Label3D MakeOverheatLabel(Node3D bar)
	{
		var label = new Label3D
		{
			Name = "OverheatCue",
			Text = "OVERHEAT",
			FontSize = 32,
			PixelSize = 0.0018f,
			Modulate = new Color(1f, 0.35f, 0.18f, 0.95f),
			OutlineModulate = new Color(0f, 0f, 0f, 0.75f),
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
			Position = new Vector3(0f, -BarHeight * 0.5f - 0.035f, 0.01f),
			RotationDegrees = new Vector3(0f, 180f, 0f),
			Visible = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		bar.AddChild(label);
		return label;
	}

	private static void SetOverheatLabel(Label3D? label, bool show)
	{
		if (label == null || !NodeAlive(label))
			return;
		label.Visible = show;
	}

	private void SetBarsVisible(bool visible)
	{
		if (!NodeAlive(this))
			return;
		if (_visible == visible)
			return;
		_visible = visible;
		Visible = visible;
	}

	private static Node3D BuildBar(string name, Vector3 position, out StandardMaterial3D[] mats)
	{
		var root = new Node3D { Name = name, Position = position };
		var totalGaps = SegmentGap * (SegmentCount - 1);
		var segH = (BarHeight - totalGaps) / SegmentCount;
		mats = new StandardMaterial3D[SegmentCount];

		for (var i = 0; i < SegmentCount; i++)
		{
			var y = -BarHeight * 0.5f + segH * 0.5f + i * (segH + SegmentGap);
			var mat = MakeSegmentMaterial();
			mats[i] = mat;
			var mi = new MeshInstance3D
			{
				Name = $"Seg_{i}",
				Mesh = new BoxMesh { Size = new Vector3(BarWidth, segH, 0.008f) },
				Position = new Vector3(0f, y, 0f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			MeshMat.Bind(mi, mat);
			root.AddChild(mi);
		}

		return root;
	}

	private static MeshInstance3D[] CollectSegments(Node3D bar)
	{
		var segs = new MeshInstance3D[SegmentCount];
		for (var i = 0; i < SegmentCount; i++)
			segs[i] = bar.GetNode<MeshInstance3D>($"Seg_{i}");
		return segs;
	}

	private enum MeterKind
	{
		Heat,
		Power
	}

	private static void ApplySegments(MeshInstance3D[] segs, StandardMaterial3D[] mats, float ratio, MeterKind kind)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		var litCount = ratio <= 0.001f
			? 0
			: Mathf.Clamp(Mathf.CeilToInt(ratio * SegmentCount), 1, SegmentCount);
		for (var i = 0; i < segs.Length; i++)
		{
			if (!NodeAlive(segs[i]) || i >= mats.Length || !GodotObject.IsInstanceValid(mats[i]))
				continue;
			segs[i].Visible = true;
			if (kind == MeterKind.Heat)
				ApplyHeatLook(mats[i], i < litCount, ratio);
			else
				ApplyPowerLook(mats[i], i < litCount, ratio);
		}
	}

	private static void ApplyHeatLook(StandardMaterial3D mat, bool lit, float ratio)
	{
		if (!GodotObject.IsInstanceValid(mat))
			return;

		if (!lit)
		{
			mat.AlbedoColor = new Color(0.12f, 0.14f, 0.16f, 0.35f);
			mat.EmissionEnabled = true;
			mat.Emission = new Color(0.25f, 0.3f, 0.35f);
			mat.EmissionEnergyMultiplier = 0.15f;
			return;
		}

		var hot = ratio >= 0.55f;
		mat.AlbedoColor = hot
			? new Color(0.95f, 0.28f, 0.15f, 0.92f)
			: new Color(0.95f, 0.55f, 0.18f, 0.85f);
		mat.EmissionEnabled = true;
		mat.Emission = hot
			? new Color(1f, 0.35f, 0.12f)
			: new Color(1f, 0.65f, 0.2f);
		mat.EmissionEnergyMultiplier = Mathf.Lerp(0.9f, 1.6f, ratio);
	}

	private static void ApplyPowerLook(StandardMaterial3D mat, bool lit, float ratio)
	{
		if (!GodotObject.IsInstanceValid(mat))
			return;

		if (!lit)
		{
			mat.AlbedoColor = new Color(0.1f, 0.12f, 0.16f, 0.35f);
			mat.EmissionEnabled = true;
			mat.Emission = new Color(0.2f, 0.28f, 0.4f);
			mat.EmissionEnergyMultiplier = 0.12f;
			return;
		}

		var low = ratio <= 0.25f;
		mat.AlbedoColor = low
			? new Color(0.95f, 0.55f, 0.2f, 0.9f)
			: new Color(0.28f, 0.72f, 1f, 0.88f);
		mat.EmissionEnabled = true;
		mat.Emission = low
			? new Color(1f, 0.55f, 0.18f)
			: new Color(0.35f, 0.8f, 1f);
		mat.EmissionEnergyMultiplier = low
			? 1.1f
			: Mathf.Lerp(0.7f, 1.35f, ratio);
	}

	private static StandardMaterial3D MakeSegmentMaterial() => new()
	{
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		AlbedoColor = new Color(0.12f, 0.14f, 0.16f, 0.35f),
		EmissionEnabled = true,
		Emission = new Color(0.25f, 0.3f, 0.35f),
		EmissionEnergyMultiplier = 0.15f
	};

	private static Node3D? FindCockpitTorso(MechController mech)
	{
		var found = mech.FindChild("TorsoTriFleet", recursive: true, owned: false);
		if (found is Node3D torso && NodeAlive(torso))
			return torso;

		found = mech.FindChild("TorsoOuroThin", recursive: true, owned: false);
		if (found is Node3D thin && NodeAlive(thin))
			return thin;

		var anchor = mech.FindChild("CockpitAnchor", recursive: true, owned: false);
		if (anchor == null || !NodeAlive(anchor))
			return null;

		return anchor.GetParent()?.GetParent() as Node3D
		       ?? anchor.GetParent() as Node3D;
	}

	private static string GetTorsoVisualKind(MechController mech) =>
		mech.Assembler?.Hardpoints.GetValueOrDefault(PartSlot.Torso)?.EquippedPart?.VisualKind ?? "";

	private static bool NodeAlive(Node? node) =>
		node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
}
