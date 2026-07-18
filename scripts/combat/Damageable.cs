using Godot;

namespace Mechanize;

public partial class Damageable : Node
{
	[Signal] public delegate void DamagedEventHandler(float amount, float remaining);
	[Signal] public delegate void DiedEventHandler();

	[Export] public float MaxHealth { get; set; } = 100f;

	public float CurrentHealth { get; private set; }
	public bool IsDead => CurrentHealth <= 0f;

	/// <summary>Host→client replication surface for MultiplayerSynchronizer.</summary>
	[Export]
	public float ReplicatedHealth
	{
		get => CurrentHealth;
		set
		{
			var wasDead = IsDead;
			CurrentHealth = Mathf.Clamp(value, 0f, MaxHealth > 0f ? MaxHealth : value);
			if (!wasDead && IsDead)
				EmitSignal(SignalName.Died);
		}
	}

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
	}

	public void ResetHealth(float maxHealth)
	{
		MaxHealth = maxHealth;
		CurrentHealth = maxHealth;
	}

	public void ApplyDamage(float amount)
	{
		if (IsDead || amount <= 0f)
			return;

		CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
		EmitSignal(SignalName.Damaged, amount, CurrentHealth);

		if (CurrentHealth <= 0f)
			EmitSignal(SignalName.Died);
	}

	public void ApplyHeal(float amount)
	{
		if (IsDead || amount <= 0f)
			return;

		CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
		EmitSignal(SignalName.Damaged, -amount, CurrentHealth);
	}
}
