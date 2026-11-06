using UnityEngine;

public class EnemyMeleeAttack : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private float _damage = 1f;
    [SerializeField] private float _range = 0.9f;          // distancia a la que empieza a atacar
    [SerializeField] private float _hitOffset = 0.5f;      // cuánto se adelanta el golpe
    [SerializeField] private float _hitRadius = 0.5f;
    [SerializeField] private float _attackDuration = 0.75f; // duración de la animación de ataque
    [SerializeField] private float _cooldown = 1f;

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

    // Lo llama un Animation Event en el fotograma del impacto
    public void DealDamage()
    {
        Vector2 hitCenter = (Vector2)transform.position + _attackDirection * _hitOffset;
        foreach (var hit in Physics2D.OverlapCircleAll(hitCenter, _hitRadius))
        {
            var character = hit.GetComponentInParent<Character>();
            if (character == null) continue;

            character.GetComponent<HealthSystem>().TakeDamage(_damage, _attackDirection);
            return; // el Character tiene dos colliders: un solo golpe
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + _attackDirection * _hitOffset, _hitRadius);
    }
}