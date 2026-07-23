using Godot;

namespace Mechanize;

/// <summary>
/// Physical part crate on the battlefield. Hold Interact to install into the matching slot.
/// Swapping drops the outgoing equipped instance as another crate.
/// </summary>
public partial class FieldPartCrate : Area3D
{
	public string InstanceId { get; private set; } = "";
	public string PartId { get; private set; } = "";
	public PartSlot Slot { get; private set; }
	public bool Landed { get; private set; }
	public bool Recoverable { get; private set; } = true;
	/// <summary>Peer that owns this crate's inventory entry. 0 = local/offline.</summary>
	public int OwnerPeerId { get; private set; }
	/// <summary>
	/// Replica of another peer's crate. It is interactive, but ownership must be
	/// transferred by that peer before installation.
	/// </summary>
	public bool VisualOnly { get; private set; }

	private Label3D? _label;
	private float _hold;
	private const float HoldSeconds = 1.15f;
	private bool _busy;
	/// <summary>
	/// Must see Interact released while overlapping before a hold can start.
	/// Stops mount/dismount taps and extract holds from silently installing crates.
	/// </summary>
	private bool _releaseGate;

	public static FieldPartCrate Create(
		string instanceId,
		string partId,
		PartSlot slot,
		Vector3 position,
		int ownerPeerId = 0,
		bool visualOnly = false)
	{
		var crate = new FieldPartCrate
		{
			Name = $"FieldCrate_{instanceId}_{(visualOnly ? "fx" : "live")}",
			InstanceId = instanceId,
			PartId = partId,
			Slot = slot,
			OwnerPeerId = ownerPeerId,
			VisualOnly = visualOnly,
			Recoverable = !visualOnly,
			CollisionLayer = 0,
			CollisionMask = 1,
			Monitoring = true,
			Monitorable = false
		};
		crate.BuildVisual();
		crate.Position = position;
		return crate;
	}

	public override void _ExitTree()
	{
		MeshMat.DetachBeforeFree(this);
		base._ExitTree();
	}

	private void BuildVisual()
	{
		var mat = SurfaceLibrary.Get(SurfaceLibrary.Kind.Steel, new Color(0.55f, 0.62f, 0.7f));
		mat.EmissionEnabled = true;
		mat.Emission = new Color(0.35f, 0.7f, 0.85f);
		mat.EmissionEnergyMultiplier = 0.55f;
		var body = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(1.1f, 0.7f, 1.1f) },
			Position = new Vector3(0f, 0.35f, 0f),
			MaterialOverride = mat
		};
		AddChild(body);

		var stripe = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(1.15f, 0.08f, 0.2f) },
			Position = new Vector3(0f, 0.55f, 0f),
			MaterialOverride = SurfaceLibrary.Flat(
				new Color(0.85f, 0.7f, 0.35f),
				metallic: 0.35f,
				roughness: 0.45f,
				emission: new Color(0.85f, 0.7f, 0.35f),
				emissionEnergy: 1.2f)
		};
		AddChild(stripe);

		var shape = new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = 2.4f }
		};
		AddChild(shape);

		var part = GameCatalog.GetPart(PartId);
		_label = new Label3D
		{
			Text = part?.DisplayName ?? PartId,
			Position = new Vector3(0f, 1.6f, 0f),
			FontSize = 28,
			OutlineSize = 6,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(0.85f, 0.9f, 0.95f)
		};
		AddChild(_label);
	}

	public void MarkLanded()
	{
		Landed = true;
		if (_label == null)
			return;
		var name = GameCatalog.GetPart(PartId)?.DisplayName ?? PartId;
		_label.Text = VisualOnly
			? $"{name}\n[Hold F] Claim + Install"
			: $"{name}\n[Hold F] Install";
	}

	public void MarkTransferPending()
	{
		_busy = true;
		_hold = 0f;
		if (_label != null)
			_label.Text = $"{GameCatalog.GetPart(PartId)?.DisplayName ?? PartId}\nAwaiting owner…";
	}

	public void ResetTransferClaim()
	{
		_busy = false;
		_hold = 0f;
		MarkLanded();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Landed || _busy || string.IsNullOrEmpty(InstanceId))
			return;

		var player = FindNearbyLocalPlayer();
		if (player == null)
		{
			_hold = 0f;
			_releaseGate = false;
			return;
		}

		if (!Input.IsActionPressed("interact"))
		{
			_hold = 0f;
			_releaseGate = true;
			SetIdleLabel();
			return;
		}

		// Interact is shared with mining-rig mount/dismount and extract. Never steal those holds.
		if (InteractOwnedElsewhere(player))
		{
			_hold = 0f;
			_releaseGate = false;
			SetDeferredLabel();
			return;
		}

		if (!_releaseGate)
		{
			_hold = 0f;
			SetIdleLabel();
			return;
		}

		_hold += (float)delta;
		if (_label != null)
			_label.Text = $"Installing… {Mathf.Clamp(_hold / HoldSeconds, 0f, 1f) * 100f:0}%";

		if (_hold < HoldSeconds)
			return;

		_busy = true;
		_releaseGate = false;
		TryInstall(player);
	}

	private void SetIdleLabel()
	{
		if (_label == null)
			return;
		var verb = VisualOnly ? "Claim + Install" : "Install";
		_label.Text = $"{GameCatalog.GetPart(PartId)?.DisplayName ?? PartId}\n[Hold F] {verb}";
	}

	private void SetDeferredLabel()
	{
		if (_label == null)
			return;
		var name = GameCatalog.GetPart(PartId)?.DisplayName ?? PartId;
		_label.Text = $"{name}\n[Hold F] Install — move clear of mount/extract first";
	}

	/// <summary>
	/// Mount/dismount and extract also bind Interact. Prefer those contexts over crate installs
	/// so a held Interact after hopping the mining rig cannot silently swap arm weapons.
	/// </summary>
	private static bool InteractOwnedElsewhere(MechController player)
	{
		if (player.IsCarrierMounted || EscortMission.ReservesFieldInteract)
			return true;

		var tree = player.GetTree();
		if (tree == null)
			return false;

		foreach (var node in tree.GetNodesInGroup("drop_beacon"))
		{
			if (node is not DropBeacon beacon || !GodotObject.IsInstanceValid(beacon))
				continue;
			if (beacon.State is not (DropBeaconState.ExtractArmed or DropBeaconState.Extracting))
				continue;
			if (beacon.Contains(player.GlobalPosition))
				return true;
		}

		return false;
	}

	private MechController? FindNearbyLocalPlayer()
	{
		var net = GetNodeOrNull<NetSession>("/root/NetSession");
		var localPeer = net?.LocalPeerId ?? 0;
		foreach (var body in GetOverlappingBodies())
		{
			if (body is not MechController mech || !mech.IsPlayerControlled)
				continue;
			if (localPeer != 0 && mech.OwningPeerId != 0 && mech.OwningPeerId != localPeer)
				continue;
			return mech;
		}

		return null;
	}

	private void TryInstall(MechController mech)
	{
		var arena = GetParent() as ArenaController;
		if (arena == null)
		{
			_busy = false;
			_hold = 0f;
			return;
		}

		if (!arena.TryInstallFieldCrate(this, mech))
		{
			_busy = false;
			_hold = 0f;
			SfxService.PlayUiError(UiErrorTone.DeeDoo);
			return;
		}

		if (VisualOnly)
		{
			MarkTransferPending();
			return;
		}

		SfxService.Confirm();
		MeshMat.QueueFreeSafe(this);
	}
}
