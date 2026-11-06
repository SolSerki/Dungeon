using UnityEngine;

public class Enemy : MonoBehaviour
{

    [SerializeField] private Animator _animator;
    [SerializeField] private HealthSystem _health;
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private int _deadSortingOrder = 0; // por debajo del Character (1)

    private int _attackHash;
    private int _isDeadHash;
    private int _recieveDamageHash;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _attackHash = Animator.StringToHash("_attack");
        _isDeadHash = Animator.StringToHash("_isDead");
        _recieveDamageHash = Animator.StringToHash("_recieveDamage");
        _health = GetComponent<HealthSystem>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health.enabled = true;
        _health.OnDied += Die;
        _health.OnDamaged += TakeDamage;
        _health.OnDamaged += (damageInfo) => Debug.Log($"Enemy took damage! Current Health: {damageInfo.CurrentHealth}, Max Health: {damageInfo.MaxHealth}");
    }

    public void TakeDamage(HealthSystem.DamageInfo damageInfo)
    {
        _animator.SetTrigger(_recieveDamageHash);
    }

    void Die()
    {
        _animator.SetTrigger(_isDeadHash);
        _rigidbody.simulated = false; // los restos dejan de colisionar y de recibir golpes
        _spriteRenderer.sortingOrder = _deadSortingOrder;
        _health.OnDied -= Die;
        _health.OnDamaged -= TakeDamage;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
