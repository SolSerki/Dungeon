using UnityEngine;

public class EnemyMeleeAttack : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private float _damage = 1f;
    [SerializeField] private float _range = 0.9f;          // distance at which it starts attacking
    [SerializeField] private float _hitOffset = 0.5f;      // how far ahead the hit lands
    [SerializeField] private float _hitRadius = 0.5f;
    [SerializeField] private float _attackDuration = 0.75f; // attack animation duration
    [SerializeField] private float _cooldown = 1f;

    [Header("Sounds")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _attackSounds;   // plays on the hit frame, whether it lands or not
    [SerializeField, Range(0f, 1f)] private float _attackVolume = 0.6f;

    private int _attackHash;
    private Vector2 _attackDirection;
    private float _attackEndTime;
    private float _nextAttackTime;

    public float Range => _range;
    public bool IsAttacking => Time.time < _attackEndTime;

    private void Awake() => _attackHash = Animator.StringToHash("_attack");

    public void TryAttack(Vector2 target)
    {
        if (IsAttacking || Time.time < _nextAttackTime) return;

        _attackDirection = (target - (Vector2)transform.position).normalized;
        _attackEndTime = Time.time + _attackDuration;
        _nextAttackTime = _attackEndTime + _cooldown;
        _animator.SetTrigger(_attackHash);
    }

    // Called by an Animation Event on the impact frame
    public void DealDamage()
    {
        Sfx.PlayRandom(_audio, _attackSounds, _attackVolume);

        Vector2 hitCenter = (Vector2)transform.position + _attackDirection * _hitOffset;
        foreach (var hit in Physics2D.OverlapCircleAll(hitCenter, _hitRadius))
        {
            var character = hit.GetComponentInParent<Character>();
            if (character == null) continue;

            character.GetComponent<HealthSystem>().TakeDamage(_damage, _attackDirection);
            return; // the Character has two colliders: a single hit
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + _attackDirection * _hitOffset, _hitRadius);
    }
}