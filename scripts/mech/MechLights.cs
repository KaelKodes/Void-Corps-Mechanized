using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// MAP exterior headlights (all mechs) + dim local-only cab fill for hollow cockpits.
/// L toggles headlights; Alt+L toggles cab. Defaults: both on.
/// </summary>
public partial class MechLights : Node
{
	public const string NodeName = "MechLights";

	private MechController? _mech;
	private readonly List<Light3D> _headLights = new();
	private readonly List<Light3D> _cabLights = new();
	private readonly List<MeshInstance3D> _lampMeshes = new();
	private Node3D? _rig;
	private bool _headlightsOn = true;
	private bool _cabOn = true;

	public bool HeadlightsOn => _headlightsOn;
	public bool CabLightsOn => _cabOn;

	public static MechLights EnsureOn(MechController mech)
	{
		var existing = mech.GetNodeOrNull<MechLights>(NodeName);
		if (existing != null)
			return existing;

		var lights = new MechLights { Name = NodeName };
		mech.AddChild(lights);
		lights.Bind(mech);
		return lights;
	}

	public void Bind(MechController mech)
	{
		_mech = mech;
		Rebuild();
	}

	public void Rebuild()
	{
		ClearRig();
		if (_mech == null || !GodotObject.IsInstanceValid(_mech))
			return;

		var torso = _mech.Assembler?.Hardpoints.GetValueOrDefault(PartSlot.Torso)?.Visual;
		if (torso == null || !GodotObject.IsInstanceValid(torso))
			return;

		_rig = new Node3D { Name = "LampRig" };
		torso.AddChild(_rig);

		BuildHeadlights(torso);
		BuildCabLights(torso);
		ApplyState();
	}

	public void SetHeadlights(bool on)
	{
		_headlightsOn = on;
		ApplyHeadlights();
	}

	public void SetCabLights(bool on)
	{
		_cabOn = on;
		ApplyCab();
	}

	public void ToggleHeadlights() => SetHeadlights(!_headlightsOn);

	public void ToggleCabLights() => SetCabLights(!_cabOn);

	public override void _Process(double delta)
	{
		if (_mech == null || !GodotObject.IsInstanceValid(_mech))
			return;
		if (!_mech.IsLocalPilot || _mech.HangarDisplayOnly || !_mech.ControlsEnabled)
			return;

		// Godot may match L to both actions when Alt is held (and sometimes the reverse).
		// Resolve by physical Alt: Alt+L = cab only, L = headlights only.
		var cabPressed = Input.IsActionJustPressed("toggle_cab_light");
		var lightsPressed = Input.IsActionJustPressed("toggle_lights");
		if (!cabPressed && !lightsPressed)
			return;

		var altHeld = Input.IsPhysicalKeyPressed(Key.Alt);
		if (altHeld)
			ToggleCabLights();
		else
			ToggleHeadlights();
	}

	private void BuildHeadlights(Node3D torso)
	{
		var markers = CollectLampMarkers(torso);
		if (markers.Count == 0)
			markers = CreateFallbackMarkers(torso);

		foreach (var marker in markers)
		{
			var spot = new SpotLight3D
			{
				Name = "Headlight",
				LightColor = new Color(0.92f, 0.96f, 1f),
				LightEnergy = 3.2f,
				LightIndirectEnergy = 0.35f,
				LightSpecular = 0.4f,
				ShadowEnabled = false,
				SpotRange = 42f,
				SpotAttenuation = 1.1f,
				SpotAngle = 32f,
				SpotAngleAttenuation = 0.85f,
				Position = marker.Position,
				Rotation = marker.Rotation
			};
			_rig!.AddChild(spot);
			_headLights.Add(spot);

			var bulb = new MeshInstance3D
			{
				Name = "LampBulb",
				Mesh = new SphereMesh { Radius = 0.045f, Height = 0.09f, RadialSegments = 10, Rings = 6 },
				Position = marker.Position,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			var bulbMat = SurfaceLibrary.Flat(
				new Color(0.85f, 0.92f, 1f),
				metallic: 0.1f,
				roughness: 0.35f,
				emission: new Color(0.7f, 0.85f, 1f),
				emissionEnergy: 1.4f);
			MeshMat.Bind(bulb, bulbMat);
			_rig.AddChild(bulb);
			_lampMeshes.Add(bulb);
		}
	}

	private void BuildCabLights(Node3D torso)
	{
		if (_mech == null || !_mech.IsLocalPilot || _mech.HangarDisplayOnly)
			return;
		if (!CockpitHullRegistry.IsCockpitHull(
			    _mech.Assembler?.Hardpoints.GetValueOrDefault(PartSlot.Torso)?.EquippedPart?.VisualKind))
			return;

		var interior = torso.FindChild("CockpitInterior", recursive: true, owned: false) as Node3D;
		var anchor = torso.FindChild("CockpitAnchor", recursive: true, owned: false) as Node3D;
		var parent = interior ?? _rig!;

		var dashPos = interior != null
			? new Vector3(0f, 0.42f, -0.12f)
			: (anchor?.Position ?? new Vector3(0f, 0.55f, 0.05f)) + new Vector3(0f, -0.08f, -0.15f);
		var overheadPos = interior != null
			? new Vector3(0f, 0.78f, 0.05f)
			: (anchor?.Position ?? new Vector3(0f, 0.55f, 0.05f)) + new Vector3(0f, 0.2f, 0.02f);

		parent.AddChild(MakeCabOmni("CabDash", dashPos, energy: 0.22f, range: 1.7f,
			color: new Color(1f, 0.92f, 0.78f)));
		parent.AddChild(MakeCabOmni("CabOverhead", overheadPos, energy: 0.16f, range: 2.0f,
			color: new Color(0.85f, 0.9f, 1f)));
	}

	private OmniLight3D MakeCabOmni(string name, Vector3 position, float energy, float range, Color color)
	{
		var omni = new OmniLight3D
		{
			Name = name,
			LightColor = color,
			LightEnergy = energy,
			LightIndirectEnergy = 0.15f,
			LightSpecular = 0.15f,
			ShadowEnabled = false,
			OmniRange = range,
			OmniAttenuation = 1.4f,
			Position = position
		};
		_cabLights.Add(omni);
		return omni;
	}

	private static List<Node3D> CollectLampMarkers(Node3D torso)
	{
		var list = new List<Node3D>();
		CollectNamed(torso, "Lamp_Fwd", list);
		CollectNamed(torso, "Lamp_FwdL", list);
		CollectNamed(torso, "Lamp_FwdR", list);
		return list;
	}

	private static void CollectNamed(Node3D root, string name, List<Node3D> into)
	{
		var found = root.FindChild(name, recursive: true, owned: false);
		if (found is Node3D node && GodotObject.IsInstanceValid(node))
			into.Add(node);
	}

	/// <summary>Procedural / missing-marker kits: dual cheek mounts facing −Z.</summary>
	private List<Node3D> CreateFallbackMarkers(Node3D torso)
	{
		var list = new List<Node3D>();
		var head = _mech?.Assembler?.Hardpoints.GetValueOrDefault(PartSlot.Head)?.Visual;
		var y = head != null ? 0.95f : 0.72f;
		var z = -0.58f;
		list.Add(MakeFallbackMarker("Lamp_FwdL", new Vector3(-0.38f, y, z)));
		list.Add(MakeFallbackMarker("Lamp_FwdR", new Vector3(0.38f, y, z)));
		return list;
	}

	private Marker3D MakeFallbackMarker(string name, Vector3 position)
	{
		var marker = new Marker3D
		{
			Name = name,
			Position = position
			// Default basis faces −Z (mech forward).
		};
		_rig!.AddChild(marker);
		return marker;
	}

	private void ApplyState()
	{
		ApplyHeadlights();
		ApplyCab();
	}

	private void ApplyHeadlights()
	{
		foreach (var light in _headLights)
		{
			if (GodotObject.IsInstanceValid(light))
				light.Visible = _headlightsOn;
		}

		foreach (var mesh in _lampMeshes)
		{
			if (!GodotObject.IsInstanceValid(mesh))
				continue;
			mesh.Visible = true;
			if (mesh.MaterialOverride is StandardMaterial3D mat)
				mat.EmissionEnergyMultiplier = _headlightsOn ? 1.4f : 0.12f;
		}
	}

	private void ApplyCab()
	{
		foreach (var light in _cabLights)
		{
			if (GodotObject.IsInstanceValid(light))
				light.Visible = _cabOn;
		}
	}

	private void ClearRig()
	{
		foreach (var light in _cabLights)
		{
			if (GodotObject.IsInstanceValid(light) && !light.IsQueuedForDeletion())
				light.QueueFree();
		}

		_headLights.Clear();
		_cabLights.Clear();
		_lampMeshes.Clear();

		if (_rig != null && GodotObject.IsInstanceValid(_rig) && !_rig.IsQueuedForDeletion())
			_rig.QueueFree();
		_rig = null;
	}
}
