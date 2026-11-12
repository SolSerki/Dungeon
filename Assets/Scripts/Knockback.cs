using UnityEngine;
using static HealthSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Knockback : MonoBehaviour
{
    [SerializeField] private float _force = 5f;
    [SerializeField] private float _duration = 0.2f;
    private Rigidbody2D _rigidbody;
    private HealthSystem _health;
    private float _endTime;

    public bool IsKnockedBack => Time.time < _endTime;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _health = GetComponent<HealthSystem>();
    }

    private void OnEnable() => _health.OnDamaged += Apply;
    private void OnDisable() => _health.OnDamaged -= Apply;

    private void Apply(DamageInfo info)
    {
        _rigidbody.linearVelocity = Vector2.zero; // so it doesn't add to the velocity it already had
        _rigidbody.AddForce(info.Direction * _force, ForceMode2D.Impulse);
        _endTime = Time.time + _duration;
    }
}