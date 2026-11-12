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

    [Header("Sounds")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _attackSounds;   // plays on the same frame as the hit, whether it lands or not
    [SerializeField, Range(0f, 1f)] private float _attackVolume = 0.6f;

    public event Action<Vector2> OnAttack;

      // Called by an Animation Event on the hit frame
      public void Attack() {

        Sfx.PlayRandom(_audio, _attackSounds, _attackVolume);

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
