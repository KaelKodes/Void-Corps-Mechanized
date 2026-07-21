using Godot;

namespace Mechanize;

/// <summary>
/// Diegetic L/R arm heat projected onto the Fleet cockpit front glass.
/// Stacking rectangles light bottom → top as each arm heats.
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
	private const int LayoutVersion = 12;

	private MeshInstance3D[] _segsL = System.Array.Empty<MeshInstance3D>();
	private MeshInstance3D[] _segsR = System.Array.Empty<MeshInstance3D>();
	private StandardMaterial3D[] _matsL = System.Array.Empty<StandardMaterial3D>();
	private StandardMaterial3D[] _matsR = System.Array.Empty<StandardMaterial3D>();
	private Label3D? _overheatL;
	private Label3D? _overheatR;
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
		var hasL = HasLivingArm(mech, PartSlot.WeaponL);
		var hasR = HasLivingArm(mech, PartSlot.WeaponR);
		ApplyHeat(_segsL, _matsL, hasL ? power?.ArmHeatRatioL ?? 0f : -1f);
		ApplyHeat(_segsR, _matsR, hasR ? power?.ArmHeatRatioR ?? 0f : -1f);

		var cue = power?.ResolveOverheatCue() ?? OverheatCue.None;
		SetOverheatLabel(_overheatL, cue == OverheatCue.ArmL);
		SetOverheatLabel(_overheatR, cue == OverheatCue.ArmR);
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

		var barL = BuildBar("HeatBarL", new Vector3(-xOffset, y, z));
		var barR = BuildBar("HeatBarR", new Vector3(xOffset, y, z));
		AddChild(barL);
		AddChild(barR);

		_overheatL = MakeOverheatLabel(barL);
		_overheatR = MakeOverheatLabel(barR);

		_segsL = CollectSegments(barL, out _matsL);
		_segsR = CollectSegments(barR, out _matsR);
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
			if (NodeAlive(child))
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
		_overheatR = null;
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

	private static Node3D BuildBar(string name, Vector3 position)
	{
		var root = new Node3D { Name = name, Position = position };
		var totalGaps = SegmentGap * (SegmentCount - 1);
		var segH = (BarHeight - totalGaps) / SegmentCount;

		for (var i = 0; i < SegmentCount; i++)
		{
			var y = -BarHeight * 0.5f + segH * 0.5f + i * (segH + SegmentGap);
			var mat = MakeSegmentMaterial();
			// Unique mesh per segment — shared PrimitiveMesh.Material mutations poison siblings.
			var mi = new MeshInstance3D
			{
				Name = $"Seg_{i}",
				Mesh = new BoxMesh { Size = new Vector3(BarWidth, segH, 0.008f) },
				Position = new Vector3(0f, y, 0f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			mi.SetSurfaceOverrideMaterial(0, mat);
			root.AddChild(mi);
		}

		return root;
	}

	private static MeshInstance3D[] CollectSegments(Node3D bar, out StandardMaterial3D[] mats)
	{
		var segs = new MeshInstance3D[SegmentCount];
		mats = new StandardMaterial3D[SegmentCount];
		for (var i = 0; i < SegmentCount; i++)
		{
			segs[i] = bar.GetNode<MeshInstance3D>($"Seg_{i}");
			mats[i] = (StandardMaterial3D)segs[i].GetSurfaceOverrideMaterial(0);
		}

		return segs;
	}

	private static void ApplyHeat(MeshInstance3D[] segs, StandardMaterial3D[] mats, float ratio)
	{
		if (ratio < 0f)
		{
			foreach (var seg in segs)
			{
				if (NodeAlive(seg))
					seg.Visible = false;
			}
			return;
		}

		var litCount = Mathf.Clamp(Mathf.CeilToInt(ratio * SegmentCount), 0, SegmentCount);
		for (var i = 0; i < segs.Length; i++)
		{
			if (!NodeAlive(segs[i]))
				continue;
			segs[i].Visible = true;
			ApplySegmentLook(mats[i], i < litCount, ratio);
		}
	}

	private static void ApplySegmentLook(StandardMaterial3D mat, bool lit, float ratio)
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

	private static bool HasLivingArm(MechController mech, PartSlot slot)
	{
		var hp = mech.Assembler?.Hardpoints.GetValueOrDefault(slot);
		return hp?.EquippedPart != null && hp.EquippedPart.VisualKind != "empty" && !hp.IsDestroyed;
	}

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
