using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIHealt : MonoBehaviour
{
    [SerializeField] private HealthSystem _health;
    [SerializeField] private Image _heartPrefab;
    [SerializeField] private Sprite _fullHeart;
    [SerializeField] private Sprite _halfHeart;
    [SerializeField] private Sprite _emptyHeart;

    private readonly List<Image> _hearts = new();

    private void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
        _health.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        _health.OnDamaged -= HandleDamaged;
        _health.OnHealthChanged -= HandleHealthChanged;
    }

    private void Start()
    {
        BuildHearts(_health.MaxHealth);
        Refresh(_health.CurrentHealth);
    }
    private void HandleDamaged(HealthSystem.DamageInfo damageInfo) => Refresh(damageInfo.CurrentHealth);
    private void HandleHealthChanged(float currentHealth, float maxHealth) => Refresh(currentHealth);
    private void BuildHearts(float maxHealth)
    {
        int heartCount = Mathf.CeilToInt(maxHealth / 2f);
        for (int i = 0; i < heartCount; i++)
        {
            _hearts.Add(Instantiate(_heartPrefab, transform));
        }
    }
     private void Refresh(float currentHealth)
    {
        int hp = Mathf.Max(0, Mathf.CeilToInt(currentHealth));
        for (int i = 0; i < _hearts.Count; i++)
        {
            int heartHp = Mathf.Clamp(hp - i * 2, 0, 2);
            _hearts[i].sprite = heartHp switch
            {
                2 => _fullHeart,
                1 => _halfHeart,
                _ => _emptyHeart
            };
        }
    }
}
