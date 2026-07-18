using Godot;

namespace Mechanize;

public abstract class MissionBase
{
	protected IMissionHost Host { get; private set; } = null!;
	public MissionType Type { get; }
	public string Title { get; }
	public bool Completed { get; private set; }
	public bool ObjectivesComplete { get; private set; }

	protected MissionBase(MissionType type)
	{
		Type = type;
		Title = MissionCatalog.Get(type).Title;
	}

	public void Bind(IMissionHost host)
	{
		Host = host;
	}

	/// <summary>Place objectives, enemies, and props before prep/countdown.</summary>
	public abstract void SetupBattlefield();

	public virtual void OnFightStarted() { }

	public virtual void Tick(float dt) { }

	public virtual void NotifyEnemyMechDown(MechController enemy) { }

	public virtual string GetHudLine() => Title;

	/// <summary>Optional absolute res:// combat track. Null = default Combat cue.</summary>
	public virtual string? PreferredCombatTrack => null;

	/// <summary>Optional extract pad (e.g. Sabotage Exfil Uplink). Null = player drop beacon.</summary>
	public virtual DropBeacon? ExtractBeaconOverride => null;

	/// <summary>Objectives done — player must extract at drop beacon. Does not end the match.</summary>
	protected void MarkObjectivesComplete()
	{
		if (Completed || ObjectivesComplete || Host.MatchResolved)
			return;
		ObjectivesComplete = true;
		Host.NotifyObjectivesComplete();
	}

	protected string ExtractHudHint()
	{
		if (!ObjectivesComplete || Completed)
			return "";
		return ExtractBeaconOverride != null
			? "  |  EXTRACT — hold E at Exfil Uplink"
			: "  |  EXTRACT — hold E at drop beacon";
	}

	/// <summary>Immediate victory (used after successful extract by host, or rare mission shortcuts).</summary>
	protected void Win()
	{
		if (Completed || Host.MatchResolved)
			return;
		Completed = true;
		Host.ReportMissionOutcome(MatchOutcome.Victory);
	}

	protected void Lose()
	{
		if (Completed || Host.MatchResolved)
			return;
		Completed = true;
		Host.ReportMissionOutcome(MatchOutcome.Defeat);
	}

	protected Node3D EnsureMissionRoot()
	{
		var existing = Host.Root.GetNodeOrNull<Node3D>("MissionRuntime");
		if (existing != null)
		{
			Host.Root.RemoveChild(existing);
			existing.Free();
		}

		var root = new Node3D { Name = "MissionRuntime" };
		Host.Root.AddChild(root);
		return root;
	}

	protected static StandardMaterial3D MakeMat(Color color, float alpha = 1f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = new Color(color.R, color.G, color.B, alpha),
			Transparency = alpha < 0.99f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			Roughness = 0.7f,
			Metallic = 0.15f,
			EmissionEnabled = alpha < 0.99f,
			Emission = color,
			EmissionEnergyMultiplier = alpha < 0.99f ? 0.35f : 0f
		};
	}
}
