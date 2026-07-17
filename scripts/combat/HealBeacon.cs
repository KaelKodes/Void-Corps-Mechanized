using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Deployed healing pad. Any living unit in the radius receives repair while active.
/// Greater-heal variants can set <see cref="OfflineWhileHealing"/> to lock units down.
/// </summary>
public partial class HealBeacon : Node3D
{
	public static readonly List<HealBeacon> Active = new();

	public float Radius { get; private set; } = 10f;
	public float HealPerSecond { get; private set; } = 14f;
	public float Remaining { get; private set; } = 8f;
	public bool OfflineWhileHealing { get; private set; }
	public Node? Source { get; private set; }

	private Label3D? _label;
	private StandardMaterial3D? _ringMat;

	public static HealBeacon Create(
		Vector3 position,
		float radius,
		float healPerSecond,
		float duration,
		Node? source,
		bool offlineWhileHealing = false)
	{
		var beacon = new HealBeacon
		{
			Name = "HealBeacon",
			Position = new Vector3(position.X, 0f, position.Z),
			Radius = Mathf.Max(3f, radius),
			HealPerSecond = Mathf.Max(1f, healPerSecond),
			Remaining = Mathf.Max(0.5f, duration),
			Source = source,
			OfflineWhileHealing = offlineWhileHealing
		};
		beacon.Build();
		return beacon;
	}

	public static HealBeacon? FindBestFor(MechController mech)
	{
		HealBeacon? best = null;
		var bestDist = float.MaxValue;
		foreach (var beacon in Active)
		{
			if (!IsInstanceValid(beacon) || beacon.Remaining <= 0f)
				continue;
			var dist = mech.GlobalPosition.DistanceTo(beacon.GlobalPosition);
			// Prefer beacons we're already in, then nearest within a short approach range.
			if (beacon.Contains(mech.GlobalPosition))
				return beacon;
			if (dist < beacon.Radius + 14f && dist < bestDist)
			{
				bestDist = dist;
				best = beacon;
			}
		}
		return best;
	}

	private void Build()
	{
		var color = new Color(0.35f, 0.95f, 0.55f);
		_ringMat = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 1.2f,
			Roughness = 0.4f
		};

		AddChild(new MeshInstance3D
		{
			Mesh = new TorusMesh
			{
				InnerRadius = Radius - 0.4f,
				OuterRadius = Radius,
				Rings = 16,
				RingSegments = 40
			},
			Position = new Vector3(0f, 0.1f, 0f),
			MaterialOverride = _ringMat
		});

		AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh
			{
				TopRadius = Radius * 0.96f,
				BottomRadius = Radius * 0.96f,
				Height = 0.06f,
				RadialSegments = 40
			},
			Position = new Vector3(0f, 0.04f, 0f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(color.R, color.G, color.B, 0.22f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				EmissionEnabled = true,
				Emission = color,
				EmissionEnergyMultiplier = 0.45f
			}
		});

		// Shipping-shell core.
		AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh
			{
				TopRadius = 0.9f,
				BottomRadius = 1.2f,
				Height = 1.6f,
				RadialSegments = 10
			},
			Position = new Vector3(0f, 0.85f, 0f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.2f, 0.28f, 0.24f),
				Metallic = 0.55f,
				Roughness = 0.35f,
				EmissionEnabled = true,
				Emission = color,
				EmissionEnergyMultiplier = 0.6f
			}
		});

		_label = new Label3D
		{
			Text = OfflineWhileHealing ? "DEEP MEND" : "MEND BEACON",
			Position = new Vector3(0f, 3.4f, 0f),
			FontSize = 40,
			OutlineSize = 8,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = color
		};
		AddChild(_label);
	}

	public override void _EnterTree()
	{
		if (!Active.Contains(this))
			Active.Add(this);
	}

	public override void _ExitTree()
	{
		Active.Remove(this);
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		Remaining -= dt;
		if (_label != null)
			_label.Text = OfflineWhileHealing
				? $"DEEP MEND {Remaining:0.0}s"
				: $"MEND FIELD {Remaining:0.0}s";

		if (Remaining <= 0f)
		{
			QueueFree();
			return;
		}

		TickHeals(dt);
	}

	public bool Contains(Vector3 worldPoint)
	{
		var delta = worldPoint - GlobalPosition;
		delta.Y = 0f;
		return delta.LengthSquared() <= Radius * Radius;
	}

	public bool IsHealing(MechController mech) =>
		IsInstanceValid(mech) && Remaining > 0f && Contains(mech.GlobalPosition);

	private void TickHeals(float dt)
	{
		var budget = HealPerSecond * dt;
		var scene = GetTree()?.CurrentScene ?? GetParent();
		if (scene == null)
			return;

		foreach (var child in scene.GetChildren())
		{
			if (child is MechController mech && mech.Visible
			    && mech.Integrity?.IsCollapsed != true
			    && Contains(mech.GlobalPosition))
			{
				mech.Integrity?.ApplyMend(budget);
			}
			else if (child is SupportUnit support && support.IsAlive
			         && support.Health != null
			         && Contains(support.GlobalPosition))
			{
				support.Health.ApplyHeal(budget);
			}
			else if (child is Node3D group && group.Name == "MissionRuntime")
			{
				foreach (var nested in group.GetChildren())
				{
					if (nested is SupportUnit nestedSupport && nestedSupport.IsAlive
					    && nestedSupport.Health != null
					    && Contains(nestedSupport.GlobalPosition))
						nestedSupport.Health.ApplyHeal(budget);
					else if (nested is EscortAsset escort && !escort.IsDestroyed
					         && escort.GetNodeOrNull<Damageable>("Damageable") is { } dmg
					         && Contains(escort.GlobalPosition))
						dmg.ApplyHeal(budget);
				}
			}
		}
	}
}
