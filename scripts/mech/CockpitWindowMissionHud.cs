using Godot;

namespace Mechanize;

/// <summary>
/// Objective + mission flavor as a SubViewport texture on a floating quad.
/// Default (Fleet, etc.): upper-right of ViewPanel glass.
/// Lumina Oracle: flush on Exterior/FacetTop (no ViewPanel glass chrome).
/// Owned by CockpitDiegeticHud (TopLevel) so torso rebuilds cannot free us mid-frame.
/// </summary>
public partial class CockpitWindowMissionHud : Node3D
{
	private static readonly Vector2I ViewportSize = new(512, 180);
	private const int LayoutVersion = 8;

	private enum AnchorKind
	{
		ViewPanel,
		FacetTop
	}

	private SubViewport? _viewport;
	private MeshInstance3D? _quad;
	private Label? _objective;
	private Label? _flavor;
	private ulong _anchorId;
	private AnchorKind _kind;
	private string _visualKind = "";
	private int _builtLayoutVersion;
	private bool _built;
	private bool _visible;
	private bool _materialReady;

	private string _objectiveText = "";
	private string _flavorText = "";
	private string _statusText = "";

	public void SetChrome(string objective, string flavor, string status = "")
	{
		_objectiveText = objective ?? "";
		_flavorText = flavor ?? "";
		_statusText = status ?? "";
		ApplyText();
	}

	public void Refresh(MechController mech, bool firstPerson)
	{
		if (!NodeAlive(this))
			return;

		var show = firstPerson
		           && mech.IsPlayerControlled
		           && CockpitDiegeticHud.MechHasCockpitScreens(mech);
		if (!show)
		{
			SetHudVisible(false);
			return;
		}

		var (anchor, kind) = FindMissionAnchor(mech);
		if (anchor == null)
		{
			SetHudVisible(false);
			return;
		}

		var visualKind = GetTorsoVisualKind(mech);
		EnsureBuilt(anchor, kind, visualKind);
		if (!_built || !NodeAlive(this))
			return;

		TopLevel = true;
		GlobalTransform = anchor.GlobalTransform;
		ApplyQuadFacing();
		SetHudVisible(true);
		ApplyText();
		if (_viewport != null && NodeAlive(_viewport))
			_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
	}

	private void ApplyQuadFacing()
	{
		if (_quad == null || !NodeAlive(_quad))
			return;

		var (pos, size) = QuadLayout(_kind, _visualKind);
		_quad.Position = pos;
		_quad.RotationDegrees = new Vector3(0f, 180f, 0f);
		_quad.Scale = Vector3.One;
		if (_quad.Mesh is QuadMesh qm)
			qm.Size = size;

		if (_quad.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
			mat.Uv1Scale = new Vector3(-1f, 1f, 1f);
	}

	private static (Vector3 Pos, Vector2 Size) QuadLayout(AnchorKind kind, string visualKind) => kind switch
	{
		// FacetTop is a 0.42×0.5×0.08 box. Sit just outside the +Z face (pilot-facing /
		// cockpit underside) so the opaque facet doesn't occlude the SubViewport quad.
		AnchorKind.FacetTop => (new Vector3(0f, 0f, 0.055f), new Vector2(0.38f, 0.46f)),
		// Thinspine: raised + shifted left so text clears the narrow mullion/frame.
		_ when visualKind == "torso_ouro_thin" =>
			(new Vector3(0.14f, 0.34f, 0.028f), new Vector2(0.40f, 0.13f)),
		// ViewPanel (Fleet default): upper-right, dropped to clear Fleet frame chrome.
		_ => (new Vector3(0.28f, 0.16f, 0.028f), new Vector2(0.40f, 0.13f))
	};

	public void TearDown()
	{
		_built = false;
		_visible = false;
		_materialReady = false;
		_anchorId = 0;
		_builtLayoutVersion = 0;
		_visualKind = "";
		_viewport = null;
		_quad = null;
		_objective = null;
		_flavor = null;
		if (!NodeAlive(this))
			return;

		GetParent()?.RemoveChild(this);
		QueueFree();
	}

	private void EnsureBuilt(MeshInstance3D anchor, AnchorKind kind, string visualKind)
	{
		if (!NodeAlive(this) || !NodeAlive(anchor))
			return;

		var anchorId = anchor.GetInstanceId();
		if (_built
		    && _anchorId == anchorId
		    && _builtLayoutVersion == LayoutVersion
		    && _kind == kind
		    && _visualKind == visualKind)
		{
			ApplyQuadFacing();
			return;
		}

		foreach (var child in GetChildren())
		{
			if (NodeAlive(child))
				child.QueueFree();
		}

		Name = "WindowMissionHud";
		TopLevel = true;
		Position = Vector3.Zero;
		Rotation = Vector3.Zero;
		Scale = Vector3.One;
		_kind = kind;
		_visualKind = visualKind;

		_viewport = new SubViewport
		{
			Name = "MissionVp",
			Size = ViewportSize,
			TransparentBg = kind == AnchorKind.ViewPanel,
			HandleInputLocally = false,
			Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			RenderTargetClearMode = SubViewport.ClearMode.Always
		};
		AddChild(_viewport);

		var shell = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = ViewportSize,
			Size = ViewportSize
		};
		_viewport.AddChild(shell);

		if (kind == AnchorKind.FacetTop)
		{
			var bg = new ColorRect
			{
				Color = new Color(0.05f, 0.1f, 0.09f, 0.94f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			shell.AddChild(bg);
		}

		var col = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		col.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		col.AddThemeConstantOverride("separation", 4);
		if (kind == AnchorKind.FacetTop)
		{
			col.OffsetLeft = 14;
			col.OffsetTop = 12;
			col.OffsetRight = -14;
			col.OffsetBottom = -12;
		}

		shell.AddChild(col);

		var align = kind == AnchorKind.FacetTop
			? HorizontalAlignment.Center
			: HorizontalAlignment.Right;

		_objective = new Label
		{
			HorizontalAlignment = align,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.85f, 0.93f, 1f, 0.95f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_objective.AddThemeFontSizeOverride("font_size", kind == AnchorKind.FacetTop ? 22 : 17);
		col.AddChild(_objective);

		_flavor = new Label
		{
			HorizontalAlignment = align,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.72f, 0.82f, 0.9f, 0.9f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_flavor.AddThemeFontSizeOverride("font_size", kind == AnchorKind.FacetTop ? 15 : 12);
		col.AddChild(_flavor);

		var (pos, size) = QuadLayout(kind, visualKind);
		_quad = new MeshInstance3D
		{
			Name = "MissionGlassQuad",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = pos,
			RotationDegrees = new Vector3(0f, 180f, 0f),
			Mesh = new QuadMesh { Size = size }
		};
		AddChild(_quad);

		_anchorId = anchorId;
		_builtLayoutVersion = LayoutVersion;
		_built = true;
		_materialReady = false;
		Visible = false;
		_visible = false;
		ApplyText();
		_ = FinishMaterialAsync();
	}

	private async System.Threading.Tasks.Task FinishMaterialAsync()
	{
		if (_viewport == null || _quad == null)
			return;

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		if (!NodeAlive(this)
		    || _viewport == null
		    || !NodeAlive(_viewport)
		    || _quad == null
		    || !NodeAlive(_quad))
			return;

		var tex = _viewport.GetTexture();
		_quad.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			AlbedoTexture = tex,
			Transparency = _kind == AnchorKind.ViewPanel
				? BaseMaterial3D.TransparencyEnum.Alpha
				: BaseMaterial3D.TransparencyEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			EmissionEnabled = true,
			Emission = Colors.White,
			EmissionTexture = tex,
			EmissionEnergyMultiplier = _kind == AnchorKind.FacetTop ? 1.4f : 1.1f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
			Uv1Scale = new Vector3(-1f, 1f, 1f)
		});
		_materialReady = true;
		ApplyQuadFacing();
	}

	private void ApplyText()
	{
		if (_objective != null && GodotObject.IsInstanceValid(_objective))
			_objective.Text = string.IsNullOrEmpty(_objectiveText) ? "" : _objectiveText;

		if (_flavor == null || !GodotObject.IsInstanceValid(_flavor))
			return;

		if (!string.IsNullOrEmpty(_statusText))
			_flavor.Text = string.IsNullOrEmpty(_flavorText)
				? _statusText
				: $"{_flavorText}\n{_statusText}";
		else
			_flavor.Text = _flavorText;
	}

	private void SetHudVisible(bool visible)
	{
		if (!NodeAlive(this))
			return;
		if (_visible == visible)
			return;
		_visible = visible;
		Visible = visible;
		if (_viewport != null && NodeAlive(_viewport))
		{
			_viewport.RenderTargetUpdateMode = visible
				? SubViewport.UpdateMode.Always
				: SubViewport.UpdateMode.Disabled;
		}
	}

	/// <summary>
	/// Oracle prefers Exterior/FacetTop. Everyone else uses ViewPanel glass.
	/// </summary>
	private static (MeshInstance3D? Anchor, AnchorKind Kind) FindMissionAnchor(MechController mech)
	{
		var facet = mech.FindChild("FacetTop", recursive: true, owned: false);
		if (facet is MeshInstance3D facetMesh && NodeAlive(facetMesh))
			return (facetMesh, AnchorKind.FacetTop);

		var found = mech.FindChild("ViewPanel", recursive: true, owned: false);
		if (found is MeshInstance3D glass && NodeAlive(glass))
			return (glass, AnchorKind.ViewPanel);

		return (null, AnchorKind.ViewPanel);
	}

	private static string GetTorsoVisualKind(MechController mech) =>
		mech.Assembler?.Hardpoints.GetValueOrDefault(PartSlot.Torso)?.EquippedPart?.VisualKind ?? "";

	private static bool NodeAlive(Node? node) =>
		node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
}
