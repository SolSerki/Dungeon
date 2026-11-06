using UnityEngine;
using UnityEngine.AI;

[RequireComponent (typeof(NavMeshAgent), typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
[SerializeField] private Animator _animator;
[SerializeField] private SpriteRenderer _spriteRenderer;
[SerializeField] private float _repathDistance = 0.3f;
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

        _agent.updateRotation = false;  // en 2D no queremos que rote el sprite
        _agent.updateUpAxis = false;    // el NavMesh está en XY
        _agent.updatePosition = false;  // el Rigidbody2D mueve, el agent solo calcula la ruta

        _moveXHash = Animator.StringToHash("MoveX");
        _moveYHash = Animator.StringToHash("MoveY");
        _isWalkingHash = Animator.StringToHash("_isWalking");
        _health = GetComponent<HealthSystem>();
    }

    public void MoveTo(Vector2 target)
    {
        _agent.isStopped = false;
        if (_agent.hasPath && Vector2.Distance(_agent.destination, target) < _repathDistance) return;
        _agent.SetDestination(target);
    }

    public void FaceTowards(Vector2 target)
    {
        float dx = target.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) _spriteRenderer.flipX = dx < 0f; // margen para que no parpadee en vertical
    }

    public void Stop() => _agent.isStopped = true;

    private void FixedUpdate()
    {
        if (_knockback != null && _knockback.IsKnockedBack)
        {
            _agent.nextPosition = _rigidbody.position; // dejamos que el golpe lo empuje
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
            FaceTowards(_agent.steeringTarget); // próxima esquina de la ruta, no el destino final
        }
    }

    private void HandleDied()
{
    _agent.enabled = false;
    enabled = false;
}
}
