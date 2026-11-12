using UnityEngine;

// Shadow wave launched by the Boss: travels in a straight line while the VFX plays
// and damages the player only once. It stops against walls and obstacles.
public class BossProjectile : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private float _speed = 7f;
    [SerializeField] private float _lifetime = 1f;
    [SerializeField] private float _damage = 1f;
    [SerializeField] private float _hitRadius = 0.6f;
    [SerializeField] private LayerMask _obstacleMask;

    private Vector2 _direction;
    private bool _hasHit;

    public void Launch(Vector2 direction)
    {
        _direction = direction.normalized;
        transform.right = _direction; // the VFX sprite faces right

        // stretches the animation (0.58 s) so it lasts as long as the flight
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            float clipLength = _animator.runtimeAnimatorController.animationClips[0].length;
            _animator.speed = clipLength / _lifetime;
        }
        Destroy(gameObject, _lifetime);
    }

    private void Update()
    {
        Vector2 step = _direction * _speed * Time.deltaTime;
        if (Physics2D.Raycast(transform.position, _direction, step.magnitude, _obstacleMask))
        {
            Destroy(gameObject);
            return;
        }
        transform.position += (Vector3)step;

        if (_hasHit) return;
        foreach (var hit in Physics2D.OverlapCircleAll(transform.position, _hitRadius))
        {
            var character = hit.GetComponentInParent<Character>();
            if (character == null) continue;

            character.GetComponent<HealthSystem>().TakeDamage(_damage, _direction);
            _hasHit = true; // keeps flying but no longer deals damage
            return;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _hitRadius);
    }
}
