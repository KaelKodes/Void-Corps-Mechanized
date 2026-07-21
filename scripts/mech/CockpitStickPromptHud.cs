using Godot;

namespace Mechanize;

/// <summary>
/// P camera / Esc pause prompts on the Fleet center-stick handle (ViewPanel-style SubViewport quad).
/// </summary>
public partial class CockpitStickPromptHud : Node3D
{
	private static readonly Vector2I ViewportSize = new(256, 64);
	private const float PanelWidth = 0.11f;
	private const float PanelHeight = 0.04f;
	private const int LayoutVersion = 1;
	/// <summary>Local to Handle mesh — face the pilot on the stick crown.</summary>
	private static readonly Vector3 PanelLocalPos = new(0f, 0.02f, 0.04f);

	private SubViewport? _viewport;
	private MeshInstance3D? _quad;
	private Label? _label;
	private ulong _handleId;
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
			SetHudVisible(false);
			return;
		}

		var handle = FindHandle(mech);
		if (handle == null)
		{
			SetHudVisible(false);
			return;
		}

		EnsureBuilt(handle);
		if (!_built || !NodeAlive(this))
			return;

		TopLevel = true;
		GlobalTransform = handle.GlobalTransform;
		ApplyQuadFacing();
		SetHudVisible(true);
		if (_viewport != null && NodeAlive(_viewport))
			_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
	}

	public void TearDown()
	{
		_built = false;
		_visible = false;
		_handleId = 0;
		_builtLayoutVersion = 0;
		_viewport = null;
		_quad = null;
		_label = null;
		if (!NodeAlive(this))
			return;
		GetParent()?.RemoveChild(this);
		QueueFree();
	}

	private void EnsureBuilt(MeshInstance3D handle)
	{
		if (!NodeAlive(this) || !NodeAlive(handle))
			return;

		var handleId = handle.GetInstanceId();
		if (_built && _handleId == handleId && _builtLayoutVersion == LayoutVersion)
		{
			ApplyQuadFacing();
			return;
		}

		foreach (var child in GetChildren())
		{
			if (NodeAlive(child))
				child.QueueFree();
		}

		Name = "StickPromptHud";
		TopLevel = true;
		Position = Vector3.Zero;
		Rotation = Vector3.Zero;
		Scale = Vector3.One;

		_viewport = new SubViewport
		{
			Name = "StickVp",
			Size = ViewportSize,
			TransparentBg = true,
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

		_label = new Label
		{
			Text = "P camera\nEsc pause",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.85f, 0.9f, 0.95f, 0.95f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_label.AddThemeFontSizeOverride("font_size", 16);
		shell.AddChild(_label);

		_quad = new MeshInstance3D
		{
			Name = "StickPromptQuad",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = PanelLocalPos,
			RotationDegrees = new Vector3(0f, 180f, 0f),
			Mesh = new QuadMesh { Size = new Vector2(PanelWidth, PanelHeight) }
		};
		AddChild(_quad);

		_handleId = handleId;
		_builtLayoutVersion = LayoutVersion;
		_built = true;
		Visible = false;
		_visible = false;
		_ = FinishMaterialAsync();
	}

	private async System.Threading.Tasks.Task FinishMaterialAsync()
	{
		if (_viewport == null || _quad == null)
			return;

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		if (!NodeAlive(this) || !NodeAlive(_viewport) || !NodeAlive(_quad))
			return;

		var tex = _viewport.GetTexture();
		_quad.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			AlbedoTexture = tex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			EmissionEnabled = true,
			Emission = Colors.White,
			EmissionTexture = tex,
			EmissionEnergyMultiplier = 1.05f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
			Uv1Scale = new Vector3(-1f, 1f, 1f)
		});
		ApplyQuadFacing();
	}

	private void ApplyQuadFacing()
	{
		if (_quad == null || !NodeAlive(_quad))
			return;

		_quad.Position = PanelLocalPos;
		_quad.RotationDegrees = new Vector3(0f, 180f, 0f);
		_quad.Scale = Vector3.One;
		if (_quad.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
			mat.Uv1Scale = new Vector3(-1f, 1f, 1f);
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

	private static MeshInstance3D? FindHandle(MechController mech)
	{
		var stick = mech.FindChild("CenterStick", recursive: true, owned: false);
		if (stick == null)
			return null;
		var handle = stick.FindChild("Handle", recursive: false, owned: false);
		return handle is MeshInstance3D mesh && NodeAlive(mesh) ? mesh : null;
	}

	private static bool NodeAlive(Node? node) =>
		node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
}
