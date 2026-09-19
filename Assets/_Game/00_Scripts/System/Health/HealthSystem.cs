using System;

public class HealthSystem
{
    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    // Events untuk menangani respon khusus Player atau Enemy
    public event Action<float, float> OnHealthChanged; // (currentHealth, maxHealth)
    public event Action<float> OnDamageReceived;       // (damageAmount)
    public event Action OnDeath;

    public HealthSystem(float maxHealth)
    {
        MaxHealth = MathF.Max(1f, maxHealth);
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;

        CurrentHealth -= damage;
        CurrentHealth = MathF.Max(0f, CurrentHealth);

        OnDamageReceived?.Invoke(damage);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (IsDead)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth += amount;
        CurrentHealth = MathF.Min(MaxHealth, CurrentHealth);

        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }
}