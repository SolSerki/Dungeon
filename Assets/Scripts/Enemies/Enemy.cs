using UnityEngine;

public class Enemy : MonoBehaviour
{

    // protected: las clases hijas (Boss) los usan
    [SerializeField] protected Animator _animator;
    [SerializeField] protected HealthSystem _health;
    [SerializeField] protected Rigidbody2D _rigidbody;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    [SerializeField] private int _deadSortingOrder = 0; // por debajo del Character (1)

    private int _attackHash;
    private int _isDeadHash;
    private int _recieveDamageHash;

    // virtual: una clase hija puede extenderlo llamando a base.Start()
    protected virtual void Start()
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

    public virtual void TakeDamage(HealthSystem.DamageInfo damageInfo)
    {
        _animator.SetTrigger(_recieveDamageHash);
    }

    protected virtual void Die()
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
