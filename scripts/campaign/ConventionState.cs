using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>Per-manufacturer and run-wide convention floor state.</summary>
public sealed class ConventionState
{
	public const int MaxAttempts = 3;

	public Dictionary<string, ManufacturerConventionStatus> ByManufacturer { get; } = new();
	public bool ForgivenessUsed { get; set; }
	public bool PityContractUsed { get; set; }
	/// <summary>Manufacturer id whose trial is in progress (empty when in hall).</summary>
	public string ActiveTrialManufacturerId { get; set; } = "";
	/// <summary>True if the active trial's Demo Cradle was destroyed.</summary>
	public bool ActiveTrialSabotaged { get; set; }

	public void EnsureAllManufacturers()
	{
		GameCatalog.EnsureBuilt();
		foreach (var id in GameCatalog.CampaignManufacturerIds)
		{
			if (!ByManufacturer.ContainsKey(id))
				ByManufacturer[id] = ManufacturerConventionStatus.CreateFresh();
		}
	}

	public ManufacturerConventionStatus Get(string manufacturerId)
	{
		if (!ByManufacturer.TryGetValue(manufacturerId, out var status))
		{
			status = ManufacturerConventionStatus.CreateFresh();
			ByManufacturer[manufacturerId] = status;
		}

		return status;
	}

	public bool HasAnyQualified()
	{
		foreach (var s in ByManufacturer.Values)
		{
			if (s.Qualified)
				return true;
		}

		return false;
	}

	public bool AllWithdrawnWithNoQualify()
	{
		if (ByManufacturer.Count == 0)
			return false;
		var anyOpen = false;
		foreach (var s in ByManufacturer.Values)
		{
			if (s.Qualified)
				return false;
			if (!s.Withdrawn)
				anyOpen = true;
		}

		return !anyOpen;
	}

	public bool HasSabotagedOtherThan(string manufacturerId)
	{
		foreach (var (id, s) in ByManufacturer)
		{
			if (id != manufacturerId && s.Sabotaged)
				return true;
		}

		return false;
	}

	public string? FirstSabotagedOtherThan(string manufacturerId)
	{
		foreach (var (id, s) in ByManufacturer)
		{
			if (id != manufacturerId && s.Sabotaged)
				return id;
		}

		return null;
	}

	/// <summary>
	/// If withdrawn, sabotage exists vs another house, and run forgiveness unused — restore +1 attempt.
	/// Returns the sabotaged rival id for dialogue, or null if no restore.
	/// </summary>
	public string? TryGrantForgiveness(string manufacturerId)
	{
		if (ForgivenessUsed)
			return null;

		var status = Get(manufacturerId);
		if (!status.Withdrawn || status.Qualified)
			return null;

		var rival = FirstSabotagedOtherThan(manufacturerId);
		if (rival == null)
			return null;

		status.Withdrawn = false;
		status.AttemptsRemaining = Mathf.Max(1, status.AttemptsRemaining);
		if (status.AttemptsRemaining <= 0)
			status.AttemptsRemaining = 1;
		status.ForgivenessGranted = true;
		ForgivenessUsed = true;
		return rival;
	}

	public Godot.Collections.Dictionary ToDict()
	{
		var houses = new Godot.Collections.Dictionary();
		foreach (var (id, s) in ByManufacturer)
			houses[id] = s.ToDict();

		return new Godot.Collections.Dictionary
		{
			["houses"] = houses,
			["forgiveness_used"] = ForgivenessUsed,
			["pity_used"] = PityContractUsed,
			["active_trial"] = ActiveTrialManufacturerId,
			["active_sabotage"] = ActiveTrialSabotaged
		};
	}

	public static ConventionState FromDict(Godot.Collections.Dictionary dict)
	{
		var state = new ConventionState
		{
			ForgivenessUsed = dict.ContainsKey("forgiveness_used") && dict["forgiveness_used"].AsBool(),
			PityContractUsed = dict.ContainsKey("pity_used") && dict["pity_used"].AsBool(),
			ActiveTrialManufacturerId = dict.ContainsKey("active_trial") ? dict["active_trial"].AsString() : "",
			ActiveTrialSabotaged = dict.ContainsKey("active_sabotage") && dict["active_sabotage"].AsBool()
		};

		if (dict.ContainsKey("houses") && dict["houses"].VariantType == Variant.Type.Dictionary)
		{
			foreach (var (key, value) in dict["houses"].AsGodotDictionary())
			{
				if (value.VariantType != Variant.Type.Dictionary)
					continue;
				state.ByManufacturer[key.AsString()] =
					ManufacturerConventionStatus.FromDict(value.AsGodotDictionary());
			}
		}

		if (state.ByManufacturer.Count == 0)
			state.EnsureAllManufacturers();
		return state;
	}
}

public sealed class ManufacturerConventionStatus
{
	public int AttemptsRemaining { get; set; } = ConventionState.MaxAttempts;
	public bool Qualified { get; set; }
	public bool Withdrawn { get; set; }
	public bool Sabotaged { get; set; }
	public bool ForgivenessGranted { get; set; }

	public static ManufacturerConventionStatus CreateFresh() => new()
	{
		AttemptsRemaining = ConventionState.MaxAttempts
	};

	public Godot.Collections.Dictionary ToDict() => new()
	{
		["attempts"] = AttemptsRemaining,
		["qualified"] = Qualified,
		["withdrawn"] = Withdrawn,
		["sabotaged"] = Sabotaged,
		["forgiveness"] = ForgivenessGranted
	};

	public static ManufacturerConventionStatus FromDict(Godot.Collections.Dictionary d) => new()
	{
		AttemptsRemaining = d.ContainsKey("attempts") ? d["attempts"].AsInt32() : ConventionState.MaxAttempts,
		Qualified = d.ContainsKey("qualified") && d["qualified"].AsBool(),
		Withdrawn = d.ContainsKey("withdrawn") && d["withdrawn"].AsBool(),
		Sabotaged = d.ContainsKey("sabotaged") && d["sabotaged"].AsBool(),
		ForgivenessGranted = d.ContainsKey("forgiveness") && d["forgiveness"].AsBool()
	};
}
