using Godot;

namespace Mechanize;

/// <summary>
/// Draws a team-colored X-ray silhouette on mesh fragments occluded by cover
/// (or any closer geometry). Allies = cyan, enemies = red.
/// </summary>
public partial class OcclusionSilhouette : Node
{
	public const string NodeName = "OcclusionSilhouette";
	public const string GhostName = "OcclusionSilhouetteGhost";

	private static readonly Color AllyColor = new(0.28f, 0.88f, 1f, 0.9f);
	private static readonly Color EnemyColor = new(1f, 0.22f, 0.16f, 0.9f);

	private static Shader? _shader;

	private Node3D? _host;
	private ShaderMaterial? _material;
	private TeamId _team = TeamId.Neutral;
	private float _rescanTimer;

	public static void EnsureOn(Node3D host)
	{
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
			SetGhostsActive(false);
			return;
		}

		var team = ResolveTeam();
		if (team != _team && _material != null)
		{
			_team = team;
			_material.SetShaderParameter("silhouette_color", ColorFor(team));
		}

		_rescanTimer -= (float)delta;
		if (_rescanTimer <= 0f)
		{
			_rescanTimer = 0.4f;
			RescanMeshes();
		}

		SetGhostsActive(true);
	}

	private bool IsHostEligible()
	{
		if (_host == null || !_host.IsVisibleInTree())
			return false;
		if (_host is MechController mech)
			return mech.Integrity?.IsCollapsed != true && mech.Health?.IsDead != true;
		if (_host is SupportUnit support)
			return support.IsAlive;
		if (_host is HellfireTurret turret)
			return turret.IsAlive;
		if (_host is EscortAsset escort)
			return !escort.IsDestroyed;
		return true;
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
				SortingOffset = 8f
			};
			mi.AddChild(ghost);
		}
	}

	private void SetGhostsActive(bool active)
	{
		if (_host == null || !GodotObject.IsInstanceValid(_host))
			return;

		foreach (var child in _host.FindChildren(GhostName, "MeshInstance3D", recursive: true, owned: false))
		{
			if (child is MeshInstance3D ghost)
				ghost.Visible = active;
		}
	}
}
