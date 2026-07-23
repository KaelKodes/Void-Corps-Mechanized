using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Chassis heat pool plus rechargeable operational power pool.
/// Weapons feed the shared heat pool; only the MAP dissipates it.
/// Capacity gates installation; generation refills CurrentPower up to OperationalMax.
/// </summary>
public partial class MechPowerHeat : Node
{
	public const float OverheatHysteresis = 0.8f;

	private MechAssembler? _assembler;
	private readonly Dictionary<string, float> _drainRates = new();
	private bool _overheated;
	private float _replicatedOperationalMax;

	public float CurrentHeat { get; private set; }
	/// <summary>Rechargeable combat power (0 … OperationalMax).</summary>
	public float CurrentPower { get; private set; }
	public bool IsOverheated => _overheated;
	public MechStats Stats => _assembler?.Stats ?? MechStats.BlindFallback;

	/// <summary>
	/// Host uses live assembler stats; clients use the replicated maximum
	/// (destruction is not yet fully synced).
	/// </summary>
	public float EffectiveOperationalMax =>
		IsPowerAuthority ? Stats.OperationalMax : _replicatedOperationalMax;

	public float HeatRatio => Stats.HeatCap <= 0.01f ? 0f : CurrentHeat / Stats.HeatCap;

	public float PowerRatio
	{
		get
		{
			var max = EffectiveOperationalMax;
			return max <= 0.01f ? 0f : CurrentPower / max;
		}
	}

	public float ActiveDrainPerSec
	{
		get
		{
			float sum = 0f;
			foreach (var rate in _drainRates.Values)
				sum += rate;
			return sum;
		}
	}

	private bool IsPowerAuthority
	{
		get
		{
			if (!Multiplayer.HasMultiplayerPeer())
				return true;
			return Multiplayer.IsServer();
		}
	}

	/// <summary>Host→client replication for HUD meters.</summary>
	[Export]
	public float ReplicatedHeat
	{
		get => CurrentHeat;
		set
		{
			CurrentHeat = Mathf.Max(0f, value);
			SyncOverheatFlag();
		}
	}

	/// <summary>Replicates operational power (legacy property name kept for SceneReplicationConfig).</summary>
	[Export]
	public float ReplicatedLoad
	{
		get => CurrentPower;
		set => CurrentPower = Mathf.Max(0f, value);
	}

	/// <summary>Host→client operational pool maximum (grows as components die).</summary>
	[Export]
	public float ReplicatedOperationalMax
	{
		get => IsPowerAuthority ? Stats.OperationalMax : _replicatedOperationalMax;
		set => _replicatedOperationalMax = Mathf.Max(0f, value);
	}

	/// <summary>Full rebuild (garage / respawn). Clears heat and fills the operational pool.</summary>
	public void Bind(MechAssembler assembler) => Bind(assembler, resetRuntime: true);

	/// <summary>
	/// Rebind assembler stats after component loss without wiping heat.
	/// Destroyed components release their PowerRequirement, growing OperationalMax.
	/// </summary>
	public void RefreshStats(MechAssembler assembler) => Bind(assembler, resetRuntime: false);

	private void Bind(MechAssembler assembler, bool resetRuntime)
	{
		_assembler = assembler;
		_replicatedOperationalMax = Stats.OperationalMax;
		if (resetRuntime)
		{
			_drainRates.Clear();
			CurrentHeat = 0f;
			_overheated = false;
			CurrentPower = Stats.OperationalMax;
			return;
		}

		CurrentHeat = Mathf.Clamp(CurrentHeat, 0f, Stats.HeatCap);
		CurrentPower = Mathf.Clamp(CurrentPower, 0f, Stats.OperationalMax);
		SyncOverheatFlag();
	}

	public override void _Process(double delta)
	{
		if (_assembler == null || !IsPowerAuthority)
			return;

		var dt = (float)delta;
		var stats = Stats;

		CurrentHeat += stats.IdleHeatPerSec * dt;
		CurrentHeat -= stats.HeatDissipation * dt;
		CurrentHeat = Mathf.Clamp(CurrentHeat, 0f, stats.HeatCap);
		SyncOverheatFlag();

		// Generate, then apply sustained drains (sprint / pulse repair / future shields).
		CurrentPower += stats.PowerGeneration * dt;
		var drain = ActiveDrainPerSec * dt;
		if (drain > 0.0001f)
			CurrentPower -= drain;
		CurrentPower = Mathf.Clamp(CurrentPower, 0f, stats.OperationalMax);
		_replicatedOperationalMax = stats.OperationalMax;
	}

	public bool CanSpend(float amount) =>
		amount <= 0.01f || CurrentPower + 0.01f >= amount;

	public bool TrySpend(float amount)
	{
		if (amount <= 0.01f)
			return true;
		if (!CanSpend(amount) || _overheated)
			return false;
		CurrentPower = Mathf.Max(0f, CurrentPower - amount);
		return true;
	}

	/// <summary>Begin a sustained drain in power-per-second. Used by sprint and channelled abilities.</summary>
	public bool TryDraw(string key, float ratePerSec)
	{
		if (ratePerSec <= 0.01f)
			return true;
		if (_drainRates.ContainsKey(key))
			return true;
		if (_overheated || EffectiveOperationalMax <= 0.01f || CurrentPower <= 0.01f)
			return false;

		_drainRates[key] = ratePerSec;
		return true;
	}

	public void Release(string key) => _drainRates.Remove(key);

	public void AddHeat(float amount)
	{
		if (amount <= 0f || _assembler == null)
			return;
		CurrentHeat = Mathf.Min(Stats.HeatCap, CurrentHeat + amount);
		SyncOverheatFlag();
	}

	private void SyncOverheatFlag()
	{
		if (_assembler == null)
		{
			_overheated = false;
			return;
		}

		var cap = Stats.HeatCap;
		if (cap <= 0.01f)
		{
			_overheated = false;
			return;
		}

		if (CurrentHeat >= cap - 0.01f)
			_overheated = true;
		else if (_overheated && CurrentHeat <= cap * OverheatHysteresis)
			_overheated = false;
	}

	public float FireRateThrottle
	{
		get
		{
			if (_overheated)
				return 0.2f;
			if (HeatRatio > 0.85f)
				return 0.55f;
			if (HeatRatio > 0.7f)
				return 0.8f;
			return 1f;
		}
	}

	public bool CanSprint =>
		!_overheated && Stats.CanSprint && EffectiveOperationalMax > 0.01f && CurrentPower > 0.5f;

	public bool CanUseAbilities => !_overheated && CurrentPower > 0.5f;
}
