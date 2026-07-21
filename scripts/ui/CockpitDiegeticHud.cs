using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Maps cockpit dashboard meshes to combat HUD panels while in first-person (Fleet torso).
/// Screen_Threat → sensors, Screen_Self → integrity,
/// Screen_WingR → weapons/modules + run strip, Screen_WingL → claim header + map placeholder.
/// Glass: arm heat + objective/flavor. Stick handle: P / Esc prompts.
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
	private CockpitTacticalMapPanel? _tacticalMap;
	private CockpitWindowHeatBars? _windowHeat;
	private CockpitWindowMissionHud? _windowMission;
	private CockpitStickPromptHud? _stickPrompt;
	private ulong _boundMechId;
	private bool _active;

	private string _objective = "";
	private string _flavor = "";
	private string _status = "";
	private string _claimLine = "";
	private string _contractLine = "";
	private string _runStrip = "";

	public bool HasScreens => _screens.Count > 0;
	public bool IsDiegeticActive => _active && HasScreens;

	public static bool MechHasCockpitScreens(MechController mech) =>
		mech.FindChild("CockpitAnchor", recursive: true, owned: false) != null
		&& FindScreenMesh(mech, "Screen_Threat") != null;

	public override void _Ready() { }

	public void SetMissionChrome(
		string claimLine,
		string contractLine,
		string objective,
		string flavor,
		string status,
		string runStrip)
	{
		_claimLine = claimLine ?? "";
		_contractLine = contractLine ?? "";
		_objective = objective ?? "";
		_flavor = flavor ?? "";
		_status = status ?? "";
		_runStrip = runStrip ?? "";
		ApplyMissionChrome();
	}

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

		TryAttach(mech, "Screen_WingL", WingScreenSize, () =>
		{
			_tacticalMap = new CockpitTacticalMapPanel();
			return _tacticalMap;
		});

		ApplyMissionChrome();
	}

	public void Refresh(MechController mech, bool firstPerson)
	{
		if (!MechHasCockpitScreens(mech))
		{
			ApplyActive(false);
			if (IsAlive(_windowHeat))
				_windowHeat!.Refresh(mech, false);
			if (IsAlive(_windowMission))
				_windowMission!.Refresh(mech, false);
			if (IsAlive(_stickPrompt))
				_stickPrompt!.Refresh(mech, false);
			return;
		}

		Bind(mech);
		ApplyActive(firstPerson);
		EnsureWindowHeat().Refresh(mech, firstPerson);
		EnsureWindowMission().Refresh(mech, firstPerson);
		EnsureStickPrompt().Refresh(mech, firstPerson);
		if (!firstPerson)
			return;

		_sensor?.BindPilot(mech);
		_sensor?.Refresh(mech);
		_integrity?.Refresh(mech);
		_modules?.Refresh(mech);
		ApplyMissionChrome();
	}

	private void ApplyMissionChrome()
	{
		_tacticalMap?.SetHeader(_claimLine, _contractLine);
		_modules?.SetRunStrip(_runStrip);
		_windowMission?.SetChrome(_objective, _flavor, _status);
	}

	private CockpitWindowHeatBars EnsureWindowHeat()
	{
		if (IsAlive(_windowHeat))
			return _windowHeat!;

		_windowHeat = new CockpitWindowHeatBars { Name = "WindowHeatBars" };
		AddChild(_windowHeat);
		return _windowHeat;
	}

	private CockpitWindowMissionHud EnsureWindowMission()
	{
		if (IsAlive(_windowMission))
			return _windowMission!;

		_windowMission = new CockpitWindowMissionHud { Name = "WindowMissionHud" };
		AddChild(_windowMission);
		return _windowMission;
	}

	private CockpitStickPromptHud EnsureStickPrompt()
	{
		if (IsAlive(_stickPrompt))
			return _stickPrompt!;

		_stickPrompt = new CockpitStickPromptHud { Name = "StickPromptHud" };
		AddChild(_stickPrompt);
		return _stickPrompt;
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
		{
			display.Detach();
			if (IsAlive(display))
				display.QueueFree();
		}

		_screens.Clear();
		_meshIds.Clear();
		_integrity = null;
		_sensor = null;
		_modules = null;
		_tacticalMap = null;
		if (_windowHeat != null)
		{
			if (IsAlive(_windowHeat))
				_windowHeat.TearDown();
			_windowHeat = null;
		}

		if (_windowMission != null)
		{
			if (IsAlive(_windowMission))
				_windowMission.TearDown();
			_windowMission = null;
		}

		if (_stickPrompt != null)
		{
			if (IsAlive(_stickPrompt))
				_stickPrompt.TearDown();
			_stickPrompt = null;
		}

		_boundMechId = 0;
	}

	private static bool IsAlive(Node? node) =>
		node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();

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
}
