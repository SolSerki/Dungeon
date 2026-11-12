using UnityEngine;

public class Enemy : MonoBehaviour
{

    // protected: child classes (Boss) use them
    [SerializeField] protected Animator _animator;
    [SerializeField] protected HealthSystem _health;
    [SerializeField] protected Rigidbody2D _rigidbody;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    [SerializeField] private int _deadSortingOrder = 0; // below the Character (1)

    [Header("Sounds")]
    [SerializeField] protected AudioSource _audio;       // protected: the Boss uses the same source
    [SerializeField] private AudioClip[] _hurtSounds;
    [SerializeField] private AudioClip[] _deathSounds;
    [SerializeField, Range(0f, 1f)] private float _hurtVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _deathVolume = 0.7f;

    private int _attackHash;
    private int _isDeadHash;
    private int _recieveDamageHash;
    private int _aliveSortingOrder;

    // virtual: a child class can extend it by calling base.Start()
    protected virtual void Start()
    {
        _attackHash = Animator.StringToHash("_attack");
        _isDeadHash = Animator.StringToHash("_isDead");
        _recieveDamageHash = Animator.StringToHash("_recieveDamage");
        _health = GetComponent<HealthSystem>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _aliveSortingOrder = _spriteRenderer.sortingOrder;
        _health.enabled = true;
        _health.OnDied += Die;
        _health.OnDamaged += TakeDamage;
        _health.OnRevived += Revive; // never removed: it has to hear it while dead
        _health.OnDamaged += (damageInfo) => Debug.Log($"Enemy took damage! Current Health: {damageInfo.CurrentHealth}, Max Health: {damageInfo.MaxHealth}");
    }

    public virtual void TakeDamage(HealthSystem.DamageInfo damageInfo)
    {
        Sfx.PlayRandom(_audio, _hurtSounds, _hurtVolume);
        _animator.SetTrigger(_recieveDamageHash);
    }

    protected virtual void Die()
    {
        Sfx.PlayRandom(_audio, _deathSounds, _deathVolume);
        _animator.SetTrigger(_isDeadHash);
        _rigidbody.simulated = false; // the remains stop colliding and taking hits
        _spriteRenderer.sortingOrder = _deadSortingOrder;
        _health.OnDied -= Die;
        _health.OnDamaged -= TakeDamage;
    }

    // The reverse of Die(). The animation is already handled by Revivable.
    protected virtual void Revive()
    {
        _rigidbody.simulated = true;
        _spriteRenderer.sortingOrder = _aliveSortingOrder;
        _health.OnDied += Die;
        _health.OnDamaged += TakeDamage;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
