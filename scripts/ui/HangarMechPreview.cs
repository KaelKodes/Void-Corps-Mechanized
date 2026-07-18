using Godot;

namespace Mechanize;

/// <summary>
/// Hangar 3D MAP preview: SubViewport + orbit camera. Always Standard chassis (no Titans).
/// </summary>
public partial class HangarMechPreview : Control
{
	private SubViewportContainer _container = null!;
	private SubViewport _viewport = null!;
	private Camera3D _camera = null!;
	private Node3D _rig = null!;
	private MechController? _mech;
	private string _loadoutKey = "";

	private float _yaw = 0.55f;
	private float _pitch = 0.35f;
	private float _distance = 7.5f;
	private bool _dragging;
	private Vector2 _lastMouse;
	private Vector3 _focus = new(0f, 1.1f, 0f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
		BuildViewport();
		UpdateCamera();
	}

	public override void _ExitTree()
	{
		ClearMech();
	}

	public void ShowLoadout(LoadoutData loadout)
	{
		if (!IsInsideTree())
			return;

		GameCatalog.SanitizeMounts(loadout);
		var key = LoadoutKey(loadout);
		if (_mech != null && key == _loadoutKey)
			return;

		_loadoutKey = key;
		EnsureMech();
		if (_mech == null)
			return;

		_mech.ApplyChassisClass(MechChassisClass.Standard);
		_mech.RebuildFromLoadout(loadout);
		_mech.SetControlsEnabled(false);
		_mech.ProcessMode = ProcessModeEnum.Disabled;
		_mech.GlobalPosition = Vector3.Zero;
		// Camera starts on +Z; face the MAP toward the viewer (default forward is -Z).
		_mech.Rotation = new Vector3(0f, Mathf.Pi, 0f);
		FrameMech();
		UpdateCamera();
	}

	public override void _GuiInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
				_dragging = true;
				_lastMouse = press.Position;
				AcceptEvent();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
				_dragging = false;
				AcceptEvent();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
				_distance = Mathf.Clamp(_distance - 0.45f, 3.5f, 16f);
				UpdateCamera();
				AcceptEvent();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
				_distance = Mathf.Clamp(_distance + 0.45f, 3.5f, 16f);
				UpdateCamera();
				AcceptEvent();
				break;
			case InputEventMouseMotion motion when _dragging:
				var delta = motion.Position - _lastMouse;
				_lastMouse = motion.Position;
				_yaw -= delta.X * 0.008f;
				_pitch = Mathf.Clamp(_pitch + delta.Y * 0.006f, 0.08f, 1.25f);
				UpdateCamera();
				AcceptEvent();
				break;
		}
	}

	private void BuildViewport()
	{
		_container = new SubViewportContainer
		{
			Stretch = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_container);

		_viewport = new SubViewport
		{
			Size = new Vector2I(640, 480),
			TransparentBg = true,
			HandleInputLocally = false,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Msaa3D = Viewport.Msaa.Msaa2X,
			OwnWorld3D = true
		};
		_container.AddChild(_viewport);

		_rig = new Node3D { Name = "HangarRig" };
		_viewport.AddChild(_rig);

		_camera = new Camera3D
		{
			Current = true,
			Fov = 38f,
			Near = 0.05f,
			Far = 80f
		};
		_viewport.AddChild(_camera);

		AddLight(new Vector3(-2.2f, 3.2f, 2.8f), new Color(1f, 0.94f, 0.84f), 2.1f);
		AddLight(new Vector3(2.8f, 1.6f, -2.4f), new Color(0.45f, 0.62f, 0.95f), 1.25f);
		AddLight(new Vector3(0.2f, 2.5f, 3.5f), new Color(0.55f, 0.58f, 0.65f), 0.55f);

		var floor = MeshMat.Make(
			new BoxMesh { Size = new Vector3(8f, 0.08f, 8f) },
			new StandardMaterial3D
			{
				AlbedoColor = new Color(0.08f, 0.1f, 0.12f),
				Metallic = 0.35f,
				Roughness = 0.7f
			},
			new Vector3(0f, -0.04f, 0f),
			castShadow: GeometryInstance3D.ShadowCastingSetting.Off);
		_rig.AddChild(floor);

		Resized += OnResized;
		CallDeferred(MethodName.OnResized);
	}

	private void AddLight(Vector3 from, Color color, float energy)
	{
		var light = new DirectionalLight3D
		{
			LightColor = color,
			LightEnergy = energy,
			ShadowEnabled = false
		};
		_viewport.AddChild(light);
		light.LookAtFromPosition(from.Normalized() * 8f, Vector3.Zero, Vector3.Up);
	}

	private void OnResized()
	{
		if (_viewport == null || !GodotObject.IsInstanceValid(_viewport))
			return;
		var size = Size;
		if (size.X < 8 || size.Y < 8)
			return;
		_viewport.Size = new Vector2I(
			Mathf.Clamp(Mathf.RoundToInt(size.X), 160, 1280),
			Mathf.Clamp(Mathf.RoundToInt(size.Y), 120, 960));
	}

	private void EnsureMech()
	{
		if (_mech != null && GodotObject.IsInstanceValid(_mech))
			return;

		ClearMech();
		var packed = GD.Load<PackedScene>("res://scenes/mech.tscn");
		if (packed == null)
			return;

		_mech = packed.Instantiate<MechController>();
		_mech.Name = "HangarMap";
		_mech.IsPlayerControlled = true;
		_mech.Team = TeamId.Neutral;
		_rig.AddChild(_mech);
		_mech.SetControlsEnabled(false);
		_mech.ProcessMode = ProcessModeEnum.Disabled;
		_mech.ApplyChassisClass(MechChassisClass.Standard);
	}

	private void ClearMech()
	{
		if (_mech != null && GodotObject.IsInstanceValid(_mech))
			MeshMat.QueueFreeSafe(_mech);
		_mech = null;
		_loadoutKey = "";
	}

	private void FrameMech()
	{
		if (_mech == null)
			return;

		Aabb? total = null;
		foreach (var node in _mech.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
		{
			if (node is not MeshInstance3D mi)
				continue;
			var local = mi.GetAabb();
			var world = TransformAabb(mi.GlobalTransform, local);
			total = total.HasValue ? total.Value.Merge(world) : world;
		}

		if (!total.HasValue)
		{
			_focus = new Vector3(0f, 1.1f, 0f);
			_distance = 7.5f;
			return;
		}

		var aabb = total.Value;
		_focus = aabb.GetCenter();
		var radius = Mathf.Max(0.8f, aabb.Size.Length() * 0.55f);
		_distance = Mathf.Clamp(radius * 2.4f, 4.5f, 14f);
	}

	private void UpdateCamera()
	{
		if (_camera == null)
			return;

		var cp = Mathf.Cos(_pitch);
		var offset = new Vector3(
			Mathf.Sin(_yaw) * cp,
			Mathf.Sin(_pitch),
			Mathf.Cos(_yaw) * cp) * _distance;
		var pos = _focus + offset;
		_camera.LookAtFromPosition(pos, _focus, Vector3.Up);
	}

	private static Aabb TransformAabb(Transform3D t, Aabb box)
	{
		var min = box.Position;
		var max = box.End;
		var result = new Aabb(t * min, Vector3.Zero);
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

	private static string LoadoutKey(LoadoutData loadout) =>
		string.Join("|",
			loadout.LegsId, loadout.TorsoId, loadout.HeadId, loadout.PowerCoreId,
			loadout.WeaponLId, loadout.WeaponRId, loadout.ShoulderLId, loadout.ShoulderRId,
			loadout.BackpackId, loadout.SystemsId);
}
