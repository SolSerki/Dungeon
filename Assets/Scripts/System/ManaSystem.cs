using System;
using UnityEngine;

// The slime's mana: it gets spent (for now by sprinting) and recharges on its own
// after a while without spending.
public class ManaSystem : MonoBehaviour
{
    [SerializeField] private float _maxMana = 100f;
    [SerializeField] private float _regenPerSecond = 20f;
    [SerializeField] private float _regenDelay = 0.75f; // seconds without spending before it starts recharging

    private float _currentMana;
    private float _lastSpendTime = -Mathf.Infinity;

    public float MaxMana => _maxMana;
    public float CurrentMana => _currentMana;
    public event Action<float, float> OnManaChanged; // (current, max)

    private void Awake()
    {
        _currentMana = _maxMana;
    }

    // Spends up to 'amount' (whatever is available) and returns how much was actually spent
    public float Drain(float amount)
    {
        float spent = Mathf.Min(amount, _currentMana);
        if (spent <= 0f) return 0f;

        _currentMana -= spent;
        _lastSpendTime = Time.time;
        OnManaChanged?.Invoke(_currentMana, _maxMana);
        return spent;
    }

    private void Update()
    {
        if (_currentMana >= _maxMana || Time.time < _lastSpendTime + _regenDelay) return;

        _currentMana = Mathf.Min(_currentMana + _regenPerSecond * Time.deltaTime, _maxMana);
        OnManaChanged?.Invoke(_currentMana, _maxMana);
    }
}
