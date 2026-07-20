using Godot;

namespace Mechanize;

/// <summary>
/// Team-colored X-ray silhouette — only while cover (world/targets) blocks the
/// camera's view of the host. Allies = cyan, enemies = red.
/// </summary>
public partial class OcclusionSilhouette : Node
{
	public const string NodeName = "OcclusionSilhouette";
	public const string GhostName = "OcclusionSilhouetteGhost";

	/// <summary>Cover that can hide a MAP from the camera — not other mechs.</summary>
	private const uint OccluderMask = PhysicsLayers.World | PhysicsLayers.Targets;

	private static readonly Color AllyColor = new(0.28f, 0.88f, 1f, 0.9f);
	private static readonly Color EnemyColor = new(1f, 0.22f, 0.16f, 0.9f);

	private static Shader? _shader;

	private Node3D? _host;
	private ShaderMaterial? _material;
	private TeamId _team = TeamId.Neutral;
	private float _rescanTimer;
	private float _occlusionTimer;
	private bool _ghostsWanted;
	private bool _ghostsVisible;
	private Godot.Collections.Array<Rid>? _excludeRids;

	public static void EnsureOn(Node3D host)
	{
		if (host is MechController { HangarDisplayOnly: true })
			return;
		if (host.GetNodeOrNull(NodeName) != null)
			return;
		host.AddChild(new OcclusionSilhouette { Name = NodeName });
	}

	public override void _Ready()
	{
		_host = GetParent() as Node3D;
		_team = ResolveTeam();
		_material = CreateMaterial(ColorFor(_team));
		CallDeferred(MethodName.RescanMeshes);
	}

	public override void _Process(double delta)
	{
		if (_host == null || !GodotObject.IsInstanceValid(_host))
			return;

		if (!IsHostEligible())
		{
			_ghostsWanted = false;
			ApplyGhostVisibility(false);
			return;
		}

		var team = ResolveTeam();
		if (team != _team && _material != null)
		{
			_team = team;
			_material.SetShaderParameter("silhouette_color", ColorFor(team));
		}

		var dt = (float)delta;
		_rescanTimer -= dt;
		if (_rescanTimer <= 0f)
		{
			_rescanTimer = 0.4f;
			RescanMeshes();
			_excludeRids = null;
		}

		_occlusionTimer -= dt;
		if (_occlusionTimer <= 0f)
		{
			_occlusionTimer = 0.12f;
			_ghostsWanted = IsOccludedFromCamera();
		}

		ApplyGhostVisibility(_ghostsWanted);
	}

	private bool IsHostEligible()
	{
		if (_host == null || !_host.IsVisibleInTree())
			return false;
		if (_host is MechController mech)
		{
			if (mech.HangarDisplayOnly)
				return false;
			return mech.Integrity?.IsCollapsed != true && mech.Health?.IsDead != true;
		}
		if (_host is SupportUnit support)
			return support.IsAlive;
		if (_host is HellfireTurret turret)
			return turret.IsAlive;
		if (_host is EscortAsset escort)
			return !escort.IsDestroyed;
		return true;
	}

	/// <summary>
	/// True when at least one probe on the host is blocked from the camera by cover.
	/// </summary>
	private bool IsOccludedFromCamera()
	{
		if (_host == null)
			return false;

		var camera = _host.GetViewport()?.GetCamera3D();
		if (camera == null || !camera.Current)
			return false;

		var space = _host.GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return false;

		var from = camera.GlobalPosition;
		var exclude = GetExcludeRids();

		foreach (var to in GetProbePoints())
		{
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.CollisionMask = OccluderMask;
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0)
				continue;

			var impact = hit["position"].AsVector3();
			// Something solid sits closer than this probe — cover is in the way.
			if (from.DistanceSquaredTo(impact) + 0.05f < from.DistanceSquaredTo(to))
				return true;
		}

		return false;
	}

	private Vector3[] GetProbePoints()
	{
		var basis = _host!.GlobalTransform.Basis;
		var right = basis.X.Normalized();
		var forward = (-basis.Z).Normalized();
		var origin = _host.GlobalPosition + Vector3.Up * 1.15f;

		return
		[
			origin,
			origin + Vector3.Up * 0.85f,
			origin + Vector3.Up * 0.25f,
			origin + right * 0.75f,
			origin - right * 0.75f,
			origin + forward * 0.55f,
			origin - forward * 0.55f
		];
	}

	private Godot.Collections.Array<Rid> GetExcludeRids()
	{
		if (_excludeRids != null)
			return _excludeRids;

		_excludeRids = new Godot.Collections.Array<Rid>();
		if (_host is CollisionObject3D hostBody)
			_excludeRids.Add(hostBody.GetRid());

		foreach (var child in _host!.FindChildren("*", "CollisionObject3D", recursive: true, owned: false))
		{
			if (child is CollisionObject3D body)
				_excludeRids.Add(body.GetRid());
		}

		return _excludeRids;
	}

	private TeamId ResolveTeam() => _host != null ? TeamUtil.GetTeam(_host) : TeamId.Neutral;

	private static Color ColorFor(TeamId team) =>
		team == TeamId.Enemy ? EnemyColor : AllyColor;

	private static ShaderMaterial CreateMaterial(Color color)
	{
		_shader ??= GD.Load<Shader>("res://shaders/occlusion_silhouette.gdshader");
		var mat = new ShaderMaterial
		{
			Shader = _shader,
			RenderPriority = 16
		};
		mat.SetShaderParameter("silhouette_color", color);
		return mat;
	}

	private void RescanMeshes()
	{
		if (_host == null || _material == null || !GodotObject.IsInstanceValid(_host))
			return;

		foreach (var child in _host.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
		{
			if (child is not MeshInstance3D mi)
				continue;
			if (mi.Name == GhostName || mi.Mesh == null)
				continue;
			if (mi.GetNodeOrNull(GhostName) != null)
				continue;
			// Skip particles / soft quads that should not silhouette.
			if (mi.Mesh is QuadMesh)
				continue;

			var ghost = new MeshInstance3D
			{
				Name = GhostName,
				Mesh = mi.Mesh,
				MaterialOverride = _material,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Layers = mi.Layers,
				SortingOffset = 8f,
				Visible = _ghostsWanted
			};
			mi.AddChild(ghost);
		}

		_ghostsVisible = _ghostsWanted;
	}

	private void ApplyGhostVisibility(bool active)
	{
		if (_ghostsVisible == active)
			return;
		_ghostsVisible = active;

		if (_host == null || !GodotObject.IsInstanceValid(_host))
			return;

		foreach (var child in _host.FindChildren(GhostName, "MeshInstance3D", recursive: true, owned: false))
		{
			if (child is MeshInstance3D ghost)
				ghost.Visible = active;
		}
	}
}
