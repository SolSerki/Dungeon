using UnityEngine;

// Red health bar above the enemy's head.
// There's one sprite per fill pixel (0 = empty ... the last = full), so the cut
// always lands on a whole pixel and keeps the original bar's slanted edge.
// It shows up on the first hit and disappears when the enemy dies.
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private HealthSystem _health;
    [SerializeField] private Sprite[] _frames;           // EnemyHealthBar_0 .. EnemyHealthBar_16, in order
    [SerializeField] private bool _hideWhenFull = true;  // hidden at full health
    [SerializeField] private float _visibleTime = 3f;    // hides after this many seconds without changes (0 = always visible)
                                                         // so it doesn't give away enemies left in the dark

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
        Hide(); // hidden on start even if _hideWhenFull is off: waits for the first hit
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
        // While alive, at least one red pixel always shows
        int index = t <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(t * last));

        _renderer.sprite = _frames[index];
        _renderer.enabled = !(_hideWhenFull && t >= 1f);
        _hideAt = Time.time + _visibleTime;
    }

    private void Hide() => _renderer.enabled = false;
}
