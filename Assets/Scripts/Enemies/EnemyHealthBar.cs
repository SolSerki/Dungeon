using UnityEngine;

// Barra de vida roja sobre la cabeza del enemigo.
// Hay un sprite por cada píxel de relleno (0 = vacía ... el último = llena), así el corte
// siempre cae en un píxel entero y mantiene el borde inclinado de la barra original.
// Aparece con el primer golpe y desaparece cuando el enemigo muere.
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private HealthSystem _health;
    [SerializeField] private Sprite[] _frames;           // EnemyHealthBar_0 .. EnemyHealthBar_16, en orden
    [SerializeField] private bool _hideWhenFull = true;  // con la vida llena no se muestra
    [SerializeField] private float _visibleTime = 3f;    // se esconde tras estos segundos sin cambios (0 = siempre visible)
                                                         // así no delata a los enemigos que quedaron en la oscuridad

    private SpriteRenderer _renderer;
    private float _hideAt;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_health == null) _health = GetComponentInParent<HealthSystem>();
    }

    private void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
        _health.OnHealthChanged += Refresh;
        _health.OnDied += Hide;
    }

    private void OnDisable()
    {
        _health.OnDamaged -= HandleDamaged;
        _health.OnHealthChanged -= Refresh;
        _health.OnDied -= Hide;
    }

    private void Start()
    {
        Refresh(_health.CurrentHealth, _health.MaxHealth);
        if (_health.CurrentHealth < _health.MaxHealth) return;
        Hide(); // al empezar no se muestra aunque _hideWhenFull esté apagado: espera al primer golpe
    }

    private void Update()
    {
        if (_renderer.enabled && _visibleTime > 0f && Time.time >= _hideAt) Hide();
    }

    private void HandleDamaged(HealthSystem.DamageInfo info) => Refresh(info.CurrentHealth, info.MaxHealth);

    private void Refresh(float current, float max)
    {
        if (_health.IsDead || _frames == null || _frames.Length == 0)
        {
            Hide();
            return;
        }

        float t = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        int last = _frames.Length - 1;
        // Mientras esté vivo siempre se ve al menos un píxel rojo
        int index = t <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(t * last));

        _renderer.sprite = _frames[index];
        _renderer.enabled = !(_hideWhenFull && t >= 1f);
        _hideAt = Time.time + _visibleTime;
    }

    private void Hide() => _renderer.enabled = false;
}
