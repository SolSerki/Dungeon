using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{

    [SerializeField] private float _maxHealth = 10;
    [SerializeField] private float _invulnerabilityDuration = 1; // Duration of invulnerability in seconds
    private float _lastDamageTime = -Mathf.Infinity; // Time when the last damage was taken
    private float _currentHealth; 
    public bool IsDead => _currentHealth <= 0;
    public event Action<DamageInfo> OnDamaged;
    public event Action OnDied;
    public event Action OnRevived;
    public event Action<float, float> OnHealthChanged; // (current, max)
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public float InvulnerabilityDuration => _invulnerabilityDuration;

    public struct DamageInfo
    {
        public float Amount;
        public Vector2 Direction;
        public float CurrentHealth;
        public float MaxHealth;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake() 
    {
        _currentHealth = _maxHealth;
    }


    public void TakeDamage(float damage, Vector2 direction)
    {
        if (IsDead || Time.time < _lastDamageTime + _invulnerabilityDuration) return;

        _lastDamageTime = Time.time;
        _currentHealth -= damage;
        var damageInfo = new DamageInfo
        {
            Amount = damage,
            Direction = direction,
            CurrentHealth = _currentHealth,
            MaxHealth = _maxHealth
        };
        OnDamaged?.Invoke(damageInfo);

        if (IsDead)
        {
            OnDied?.Invoke();
        }    
    }

    // Spends health on purpose (throwing a chunk): no invulnerability, knockback or hurt animation.
    // The last bit of health can't be spent.
    public bool TrySpend(float amount)
    {
        if (IsDead || _currentHealth <= amount) return false;

        _currentHealth -= amount;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        return true;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    // Back to full health after dying (a Ghost reviving a Slime2, see Revivable).
    // Each component undoes what it did in OnDied by listening to OnRevived.
    public void Revive()
    {
        if (!IsDead) return;

        _currentHealth = _maxHealth;
        _lastDamageTime = -Mathf.Infinity;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        OnRevived?.Invoke();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
