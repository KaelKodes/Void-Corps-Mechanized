using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Runtime power load budget and heat pool. Hard-enforces throttle / gates from MechStats.
/// </summary>
public partial class MechPowerHeat : Node
{
	public const float OverheatHysteresis = 0.8f;

	private MechAssembler? _assembler;
	private readonly Dictionary<string, float> _draws = new();
	private bool _overheated;

	public float CurrentHeat { get; private set; }
	public float CurrentLoad { get; private set; }
	public bool IsOverheated => _overheated;
	public MechStats Stats => _assembler?.Stats ?? MechStats.BlindFallback;

	public float HeatRatio => Stats.HeatCap <= 0.01f ? 0f : CurrentHeat / Stats.HeatCap;
	public float LoadRatio => Stats.PowerCapacity <= 0.01f ? 0f : CurrentLoad / Stats.PowerCapacity;

	public void Bind(MechAssembler assembler)
	{
		_assembler = assembler;
		_draws.Clear();
		CurrentLoad = 0f;
		CurrentHeat = 0f;
		_overheated = false;
	}

	public override void _Process(double delta)
	{
		if (_assembler == null)
			return;

		var dt = (float)delta;
		var stats = Stats;
		CurrentHeat += stats.IdleHeatPerSec * dt;
		CurrentHeat -= stats.HeatDissipation * dt;
		CurrentHeat = Mathf.Clamp(CurrentHeat, 0f, stats.HeatCap);

		if (CurrentHeat >= stats.HeatCap - 0.01f)
			_overheated = true;
		else if (_overheated && CurrentHeat <= stats.HeatCap * OverheatHysteresis)
			_overheated = false;
	}

	public bool CanAfford(float load) => CurrentLoad + load <= Stats.PowerCapacity + 0.01f;

	public bool TryDraw(string key, float load)
	{
		if (load <= 0.01f)
			return true;
		if (_draws.ContainsKey(key))
			return true;
		if (!CanAfford(load) || _overheated)
			return false;

		_draws[key] = load;
		CurrentLoad += load;
		return true;
	}

	public void Release(string key)
	{
		if (!_draws.TryGetValue(key, out var load))
			return;
		_draws.Remove(key);
		CurrentLoad = Mathf.Max(0f, CurrentLoad - load);
	}

	public void AddHeat(float amount)
	{
		if (amount <= 0f || _assembler == null)
			return;
		CurrentHeat = Mathf.Min(Stats.HeatCap, CurrentHeat + amount);
		if (CurrentHeat >= Stats.HeatCap - 0.01f)
			_overheated = true;
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

	public bool CanSprint => !_overheated && Stats.CanSprint;

	public bool CanUseAbilities => !_overheated;
}
