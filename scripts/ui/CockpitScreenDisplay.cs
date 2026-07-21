using Godot;

namespace Mechanize;

/// <summary>
/// Renders a 2D Control onto a cockpit dashboard mesh via SubViewport + emission material.
/// </summary>
public partial class CockpitScreenDisplay : Node
{
	private SubViewport? _viewport;
	private MeshInstance3D? _mesh;
	private Material? _originalMaterial;
	private StandardMaterial3D? _screenMaterial;
	private Control? _contentRoot;
	private bool _materialReady;
	private bool _wantActive;

	public bool IsMeshValid => _mesh != null && GodotObject.IsInstanceValid(_mesh);

	public Control? ContentRoot => _contentRoot;

	public void Attach(MeshInstance3D mesh, Vector2I viewportSize, Control content)
	{
		Detach();

		_mesh = mesh;
		_originalMaterial = mesh.GetSurfaceOverrideMaterial(0)
		                    ?? mesh.GetActiveMaterial(0);

		_viewport = new SubViewport
		{
			Name = $"Vp_{mesh.Name}",
			Size = viewportSize,
			TransparentBg = false,
			HandleInputLocally = true,
			Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			RenderTargetClearMode = SubViewport.ClearMode.Always
		};
		AddChild(_viewport);

		var shell = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = viewportSize,
			Size = viewportSize
		};
		_viewport.AddChild(shell);

		var bg = new ColorRect
		{
			Color = new Color(0.04f, 0.055f, 0.07f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		shell.AddChild(bg);

		_contentRoot = content;
		content.ZIndex = 1;
		shell.AddChild(content);
		if (content.IsNodeReady())
			FitContent(content, viewportSize);
		else
			content.Ready += () => FitContent(content, viewportSize);

		_materialReady = false;
		_ = FinishAttachAsync();
	}

	public void SetActive(bool active)
	{
		_wantActive = active;
		if (_viewport == null)
			return;

		_viewport.RenderTargetUpdateMode = active
			? SubViewport.UpdateMode.Always
			: SubViewport.UpdateMode.Disabled;

		ApplyMaterialState();
	}

	public void Detach()
	{
		_wantActive = false;
		if (IsMeshValid
		    && !_mesh!.IsQueuedForDeletion()
		    && _mesh.Mesh != null
		    && _originalMaterial != null
		    && GodotObject.IsInstanceValid(_originalMaterial))
		{
			_mesh.SetSurfaceOverrideMaterial(0, _originalMaterial);
		}

		if (_viewport != null && GodotObject.IsInstanceValid(_viewport) && !_viewport.IsQueuedForDeletion())
			_viewport.QueueFree();

		_viewport = null;
		_mesh = null;
		_originalMaterial = null;
		_screenMaterial = null;
		_contentRoot = null;
		_materialReady = false;
	}

	private async System.Threading.Tasks.Task FinishAttachAsync()
	{
		if (_viewport == null)
			return;

		// Let the SubViewport enter the tree and draw one 2D frame before sampling.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		if (_viewport == null
		    || !GodotObject.IsInstanceValid(_viewport)
		    || _viewport.IsQueuedForDeletion()
		    || IsQueuedForDeletion())
			return;

		BuildScreenMaterial();
		ApplyMaterialState();
	}

	private void BuildScreenMaterial()
	{
		if (!IsMeshValid
		    || _mesh!.IsQueuedForDeletion()
		    || _mesh.Mesh == null
		    || _viewport == null
		    || !GodotObject.IsInstanceValid(_viewport))
			return;

		// Direct viewport texture — ViewportTexture + ViewportPath breaks across instanced torso scenes.
		var tex = _viewport.GetTexture();

		_screenMaterial = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			AlbedoTexture = tex,
			EmissionEnabled = true,
			Emission = Colors.White,
			EmissionTexture = tex,
			EmissionEnergyMultiplier = 1.35f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear
		};
		_materialReady = true;
	}

	private void ApplyMaterialState()
	{
		if (!IsMeshValid || _mesh!.IsQueuedForDeletion() || _mesh.Mesh == null)
			return;

		if (_wantActive)
		{
			if (!_materialReady)
				return;
			if (_screenMaterial != null)
				_mesh.SetSurfaceOverrideMaterial(0, _screenMaterial);
		}
		else if (_originalMaterial != null && GodotObject.IsInstanceValid(_originalMaterial))
		{
			_mesh.SetSurfaceOverrideMaterial(0, _originalMaterial);
		}
	}

	private static void FitContent(Control content, Vector2I viewportSize)
	{
		content.ResetSize();
		var contentSize = content.GetCombinedMinimumSize();
		if (contentSize.X < 1f || contentSize.Y < 1f)
			contentSize = content.CustomMinimumSize;
		if (contentSize.X < 1f || contentSize.Y < 1f)
			contentSize = new Vector2(viewportSize.X, viewportSize.Y);

		var pad = 6f;
		var scale = Mathf.Min(
			(viewportSize.X - pad * 2f) / contentSize.X,
			(viewportSize.Y - pad * 2f) / contentSize.Y);
		scale = Mathf.Clamp(scale, 0.35f, 1.25f);
		content.Scale = new Vector2(scale, scale);
		content.Position = new Vector2(
			(viewportSize.X - contentSize.X * scale) * 0.5f,
			(viewportSize.Y - contentSize.Y * scale) * 0.5f);
	}
}
