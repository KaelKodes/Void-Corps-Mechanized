using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Maps cockpit dashboard meshes to combat HUD panels while in first-person (Fleet torso).
/// Screen_Threat → sensors, Screen_Self → integrity, Screen_WingR → weapons/modules,
/// Screen_WingL → tactical map placeholder (deferred).
/// PWR / SPD float beside the Integrity HUD; HEAT lives on the crosshair brackets.
/// </summary>
public partial class CockpitDiegeticHud : Node
{
	private static readonly Vector2I MainScreenSize = new(512, 384);
	private static readonly Vector2I WingScreenSize = new(320, 256);

	private readonly Dictionary<string, CockpitScreenDisplay> _screens = new();
	private readonly Dictionary<string, ulong> _meshIds = new();
	private IntegritySchematic? _integrity;
	private EnemyTargetSchematic? _sensor;
	private CockpitModulesPanel? _modules;
	private ulong _boundMechId;
	private bool _active;

	public bool HasScreens => _screens.Count > 0;

	public static bool MechHasCockpitScreens(MechController mech) =>
		mech.FindChild("CockpitAnchor", recursive: true, owned: false) != null
		&& FindScreenMesh(mech, "Screen_Threat") != null;

	public override void _Ready() { }

	public void Bind(MechController mech)
	{
		if (!NeedsRebind(mech))
			return;

		ClearBindings();
		_boundMechId = mech.GetInstanceId();

		TryAttach(mech, "Screen_Threat", MainScreenSize, () =>
		{
			_sensor = new EnemyTargetSchematic
			{
				MouseFilter = Control.MouseFilterEnum.Stop
			};
			return _sensor;
		});

		TryAttach(mech, "Screen_Self", MainScreenSize, () =>
		{
			_integrity = new IntegritySchematic();
			return _integrity;
		});

		TryAttach(mech, "Screen_WingR", WingScreenSize, () =>
		{
			_modules = new CockpitModulesPanel();
			return _modules;
		});

		TryAttach(mech, "Screen_WingL", WingScreenSize, () => BuildMapPlaceholder());
	}

	public void Refresh(MechController mech, bool firstPerson)
	{
		if (!MechHasCockpitScreens(mech))
		{
			ApplyActive(false);
			return;
		}

		Bind(mech);
		ApplyActive(firstPerson);
		if (!firstPerson)
			return;

		_sensor?.BindPilot(mech);
		_sensor?.Refresh(mech);
		_integrity?.Refresh(mech);
		_modules?.Refresh(mech);
	}

	public void ApplyActive(bool active)
	{
		_active = active;
		foreach (var display in _screens.Values)
			display.SetActive(active);
	}

	public void ClearBindings()
	{
		_active = false;
		foreach (var display in _screens.Values)
			display.Detach();
		_screens.Clear();
		_meshIds.Clear();
		_integrity = null;
		_sensor = null;
		_modules = null;
		_boundMechId = 0;
	}

	public override void _ExitTree() => ClearBindings();

	private bool NeedsRebind(MechController mech)
	{
		if (_boundMechId != mech.GetInstanceId() || !HasScreens)
			return true;

		foreach (var (name, display) in _screens)
		{
			if (!display.IsMeshValid)
				return true;

			var mesh = FindScreenMesh(mech, name);
			if (mesh == null || !GodotObject.IsInstanceValid(mesh))
				return true;
			if (!_meshIds.TryGetValue(name, out var id) || mesh.GetInstanceId() != id)
				return true;
		}

		return false;
	}

	private void TryAttach(
		MechController mech,
		string screenName,
		Vector2I viewportSize,
		System.Func<Control> createContent)
	{
		var mesh = FindScreenMesh(mech, screenName);
		if (mesh == null)
			return;

		var display = new CockpitScreenDisplay { Name = $"Display_{screenName}" };
		AddChild(display);
		display.Attach(mesh, viewportSize, createContent());
		_screens[screenName] = display;
		_meshIds[screenName] = mesh.GetInstanceId();
	}

	private static MeshInstance3D? FindScreenMesh(Node root, string screenName)
	{
		var node = root.FindChild(screenName, recursive: true, owned: false);
		if (node is MeshInstance3D mesh)
			return mesh;
		if (node is Node3D group)
			return group.GetNodeOrNull<MeshInstance3D>("Quad");
		return null;
	}

	private static Control BuildMapPlaceholder()
	{
		var panel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(280, 220)
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.055f, 0.07f, 0.92f),
			BorderColor = new Color(0.35f, 0.55f, 0.42f, 0.75f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 8,
			ContentMarginRight = 8,
			ContentMarginBottom = 8
		});

		var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 6);
		panel.AddChild(col);

		var header = new Label
		{
			Text = "// TACTICAL MAP",
			Modulate = MechUiTheme.Accent,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		header.AddThemeFontSizeOverride("font_size", 9);
		col.AddChild(header);

		var body = new Label
		{
			Text = "Reserved\nfor sector\nnavigation.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			Modulate = new Color(0.55f, 0.72f, 0.62f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		body.AddThemeFontSizeOverride("font_size", 10);
		col.AddChild(body);

		return panel;
	}
}
