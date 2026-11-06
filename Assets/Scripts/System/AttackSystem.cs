using UnityEngine;
using System.Collections;
using Unity.VisualScripting;
using System;

public class AttackSystem : MonoBehaviour
{
    [SerializeField] private float _damage = 1;
    [SerializeField] private float _attackRange = 1f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _attackRadius = 0.5f;
    [SerializeField] private Transform _attackPoint;
    [SerializeField] private LayerMask _enemyLayer;
    public event Action<Vector2> OnAttack;

      public void Attack() {

        Vector2 direction = (_attackPoint.position - transform.position).normalized;
        OnAttack?.Invoke(direction);
        var hits = Physics2D.OverlapCircleAll(_attackPoint.position, _attackRadius, _enemyLayer);
        foreach (var hit in hits) {
            var hp = hit.GetComponent<HealthSystem>();
            if (hp == null) continue;
            Vector2 pushDirection = (hit.transform.position - transform.position).normalized;
            hp.TakeDamage(_damage, pushDirection);
            }
        }

    private void OnDrawGizmosSelected()
    {
        if (_attackPoint == null) return;
        Gizmos.color = Color.red; 
        Gizmos.DrawWireSphere(_attackPoint.position, _attackRadius);
    }
}
