using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mechanize;

/// <summary>
/// Renders each part's actual 3D model (from <see cref="PartVisualFactory"/>) into a
/// catalogue thumbnail on demand. Drop-in for <see cref="PartPortrait.Get"/>:
/// returns a texture immediately (procedural seed) and refreshes it in place once
/// the off-screen 3D render completes. No authored art required — new parts get
/// thumbnails automatically from their models.
/// </summary>
public partial class PartThumbnail : Node
{
	public static PartThumbnail? Instance { get; private set; }

	/// <summary>Internal render resolution; downsized to the requested plate size.</summary>
	private const int RenderSize = 320;

	private static readonly Vector3 ViewDir = new Vector3(0.9f, 0.68f, 1f).Normalized();

	private SubViewport _viewport = null!;
	private Camera3D _camera = null!;
	private Node3D _modelRoot = null!;
	private Node3D? _currentModel;

	private readonly Dictionary<string, ImageTexture> _cache = new();
	private readonly Queue<Request> _queue = new();
	private bool _working;

	private sealed class Request
	{
		public required PartData Part;
		public required int Size;
		public required ImageTexture Target;
	}

	public override void _Ready()
	{
		Instance = this;
		BuildRig();
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	/// <summary>
	/// Thumbnail for a part. Falls back to the procedural portrait when the renderer
	/// isn't available or the part has no model.
	/// </summary>
	public static Texture2D Get(PartData? part, int size = 128)
	{
		if (Instance == null || part == null || string.IsNullOrEmpty(part.Id) || part.VisualKind == "empty")
			return PartPortrait.Get(part, size);

		return Instance.GetOrEnqueue(part, size);
	}

	private Texture2D GetOrEnqueue(PartData part, int size)
	{
		var key = $"{part.Id}:{size}";
		if (_cache.TryGetValue(key, out var tex))
			return tex;

		// Seed with the procedural plate so the slot shows something instantly.
		var seed = PartPortrait.BuildImage(part, size);
		var texture = ImageTexture.CreateFromImage(seed);
		_cache[key] = texture;

		_queue.Enqueue(new Request { Part = part, Size = size, Target = texture });
		if (!_working)
			_ = ProcessQueueAsync();

		return texture;
	}

	private void BuildRig()
	{
		_viewport = new SubViewport
		{
			Size = new Vector2I(RenderSize, RenderSize),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			RenderTargetClearMode = SubViewport.ClearMode.Always,
			Msaa3D = Viewport.Msaa.Msaa4X,
			OwnWorld3D = true
		};
		AddChild(_viewport);

		_modelRoot = new Node3D { Name = "ModelRoot" };
		_viewport.AddChild(_modelRoot);

		_camera = new Camera3D
		{
			Projection = Camera3D.ProjectionType.Orthogonal,
			Current = true,
			Near = 0.05f,
			Far = 100f
		};
		_viewport.AddChild(_camera);

		// Catalogue three-point lighting: warm key upper-left, cool rim behind,
		// soft front fill so no visible face reads pure black. Shadows off for clean icons.
		AddLight(new Vector3(-2.4f, 3f, 2.4f), new Color(1f, 0.95f, 0.86f), 2.3f);
		AddLight(new Vector3(2.6f, 1.4f, -2.8f), new Color(0.45f, 0.62f, 1f), 1.5f);
		AddLight(ViewDir, new Color(0.6f, 0.64f, 0.72f), 0.55f);
	}

	private void AddLight(Vector3 fromDir, Color color, float energy)
	{
		var light = new DirectionalLight3D
		{
			LightColor = color,
			LightEnergy = energy,
			ShadowEnabled = false
		};
		_viewport.AddChild(light);
		light.LookAtFromPosition(fromDir.Normalized() * 6f, Vector3.Zero, Vector3.Up);
	}

	private async Task ProcessQueueAsync()
	{
		_working = true;
		while (_queue.Count > 0)
		{
			var req = _queue.Dequeue();
			await RenderOneAsync(req);
			if (!GodotObject.IsInstanceValid(this))
				return;
		}
		_working = false;
	}

	private async Task RenderOneAsync(Request req)
	{
		ClearModel();

		var model = PartVisualFactory.Create(req.Part);
		_currentModel = model;
		_modelRoot.AddChild(model);
		FrameModel(model);

		_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(req.Target))
		{
			ClearModel();
			return;
		}

		var render = _viewport.GetTexture().GetImage();
		var plate = ComposePlate(render, req.Size);
		req.Target.Update(plate);

		ClearModel();
	}

	private void ClearModel()
	{
		if (_currentModel != null && GodotObject.IsInstanceValid(_currentModel))
		{
			_modelRoot.RemoveChild(_currentModel);
			MeshMat.QueueFreeSafe(_currentModel);
		}
		_currentModel = null;
	}

	private void FrameModel(Node3D model)
	{
		_modelRoot.Position = Vector3.Zero;
		_modelRoot.ForceUpdateTransform();

		var aabb = ComputeModelAabb(model);
		var radius = Mathf.Max(0.1f, aabb.Size.Length() * 0.5f);

		// Centre the model on the world origin so the fixed camera always frames it.
		_modelRoot.Position = -aabb.GetCenter();

		var dist = radius * 4f + 2f;
		_camera.LookAtFromPosition(ViewDir * dist, Vector3.Zero, Vector3.Up);
		_camera.Size = radius * 2.15f;
		_camera.Near = 0.05f;
		_camera.Far = dist + radius * 3f + 20f;
	}

	private Aabb ComputeModelAabb(Node3D model)
	{
		Aabb? total = null;
		var rootInv = _modelRoot.GlobalTransform.AffineInverse();

		foreach (var node in model.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
		{
			if (node is not MeshInstance3D mi)
				continue;

			var local = mi.GetAabb();
			var rel = rootInv * mi.GlobalTransform;
			var world = TransformAabb(rel, local);
			total = total.HasValue ? total.Value.Merge(world) : world;
		}

		return total ?? new Aabb(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.One);
	}

	private static Aabb TransformAabb(Transform3D t, Aabb box)
	{
		var min = box.Position;
		var max = box.End;
		var first = t * min;
		var result = new Aabb(first, Vector3.Zero);

		for (var i = 1; i < 8; i++)
		{
			var corner = new Vector3(
				(i & 1) == 0 ? min.X : max.X,
				(i & 2) == 0 ? min.Y : max.Y,
				(i & 4) == 0 ? min.Z : max.Z);
			result = result.Expand(t * corner);
		}

		return result;
	}

	private static Image ComposePlate(Image render, int size)
	{
		if (render.GetFormat() != Image.Format.Rgba8)
			render.Convert(Image.Format.Rgba8);
		if (render.GetWidth() != size || render.GetHeight() != size)
			render.Resize(size, size, Image.Interpolation.Lanczos);

		var plate = PartPortrait.CreateBackdrop(size);
		plate.BlendRect(render, new Rect2I(0, 0, size, size), Vector2I.Zero);
		PartPortrait.DrawPlateFrame(plate);
		return plate;
	}
}
