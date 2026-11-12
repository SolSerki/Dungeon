using UnityEngine;
using UnityEngine.AI;

[RequireComponent (typeof(NavMeshAgent), typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
[SerializeField] private Animator _animator;
[SerializeField] private SpriteRenderer _spriteRenderer;
[SerializeField] private float _repathDistance = 0.3f;
[SerializeField] private bool _flipToFace = true; // off for 4-direction sprites with their own left art (Slime2): sets MoveX/MoveY instead
private HealthSystem _health;
private NavMeshAgent _agent;
private Rigidbody2D _rigidbody;
private Knockback _knockback;
private int _moveXHash, _moveYHash, _isWalkingHash;
public bool HasArrived => !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;

private void OnEnable() => _health.OnDied += HandleDied;
private void OnDisable() => _health.OnDied -= HandleDied;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _knockback = GetComponent<Knockback>();

        _agent.updateRotation = false;  // in 2D the sprite must not rotate
        _agent.updateUpAxis = false;    // the NavMesh is on XY
        _agent.updatePosition = false;  // the Rigidbody2D moves; the agent only computes the path

        _moveXHash = Animator.StringToHash("MoveX");
        _moveYHash = Animator.StringToHash("MoveY");
        _isWalkingHash = Animator.StringToHash("_isWalking");
        _health = GetComponent<HealthSystem>();
        _health.OnRevived += HandleRevived; // here and not in OnEnable: the component is disabled while dead
    }

    private void OnDestroy() => _health.OnRevived -= HandleRevived;

    public void MoveTo(Vector2 target)
    {
        _agent.isStopped = false;
        if (_agent.hasPath && Vector2.Distance(_agent.destination, target) < _repathDistance) return;
        _agent.SetDestination(target);
    }

    public void FaceTowards(Vector2 target)
    {
        if (!_flipToFace)
        {
            // 4-direction sprites: the blend trees pick the clip that looks at the target
            Vector2 dir = target - (Vector2)transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;
            _animator.SetFloat(_moveXHash, dir.x);
            _animator.SetFloat(_moveYHash, dir.y);
            return;
        }
        float dx = target.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) _spriteRenderer.flipX = dx < 0f; // margin so it doesn't flicker when moving vertically
    }

    public void Stop() => _agent.isStopped = true;

    private void FixedUpdate()
    {
        if (_knockback != null && _knockback.IsKnockedBack)
        {
            _agent.nextPosition = _rigidbody.position; // let the hit push it
            return;
        }
        _rigidbody.linearVelocity = _agent.isStopped ? Vector2.zero : (Vector2)_agent.desiredVelocity;
        _agent.nextPosition = _rigidbody.position;
    }

    private void Update()
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        bool walking = velocity.sqrMagnitude > 0.01f;
        _animator.SetBool(_isWalkingHash, walking);
        if (walking)
        {
            _animator.SetFloat(_moveXHash, velocity.x);
            _animator.SetFloat(_moveYHash, velocity.y);
            FaceTowards(_agent.steeringTarget); // next corner of the path, not the final destination
        }
    }

    private void HandleDied()
{
    _agent.enabled = false;
    enabled = false;
}

    // Subscribed in Awake, so it runs before EnemyAI's (subscribed in Start): the agent is back when the AI gives orders
    private void HandleRevived()
    {
        _agent.enabled = true; // goes back onto the NavMesh where the body is
        enabled = true;
    }
}
