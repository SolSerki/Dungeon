using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private enum State { Idle, Patrol, Chase, Attack, Distracted }

    [SerializeField] private EnemyMovement _movement;
    [SerializeField] private EnemyMeleeAttack _attack;
    [SerializeField] private HealthSystem _health;
    [SerializeField] private List<Transform> _patrolPoints;
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float _detectRange = 4f;
    [SerializeField] private float _loseSightTime = 2f;
    [SerializeField] private float _chunkDetectRange = 4f;   // distance from which it sees a slime chunk
    [SerializeField] private float _chunkStopDistance = 0.5f; // stays at this distance from the chunk

    private HealthSystem _playerHealth;
    private Transform _player;
    [SerializeField] private State _state;
    private int _patrolIndex = 0;
    private float _lastSeenTime = -Mathf.Infinity;
    private SlimeChunk _chunk; // the chunk distracting it
    private Vector2? _seamTarget; // maze seam the slime left through

    // with a single point there's nowhere to patrol
    private State DefaultState => _patrolPoints.Count > 1 ? State.Patrol : State.Idle;

    private void OnEnable()
    {
        _health.OnDied += HandleDied;
        PortalSeam.Warped += OnWarped;
    }

    private void OnDisable()
    {
        _health.OnDied -= HandleDied;
        PortalSeam.Warped -= OnWarped;
    }

    private void OnDestroy()
    {
        ReleaseChunk();
        _health.OnRevived -= HandleRevived;
    }

    private void HandleDied()
    {
        ReleaseChunk(); // when dead it frees the chunk for another enemy
        enabled = false;
    }

    // A Ghost revived it: starts over as if it had just spawned
    private void HandleRevived()
    {
        enabled = true;
        _lastSeenTime = -Mathf.Infinity;
        EnterState(DefaultState);
    }

    private void ReleaseChunk()
    {
        if (_chunk != null) _chunk.Release(this);
        _chunk = null;
    }

    // If the slime escapes through a seam while being chased, go to the seam and cross it
    // too (PortalSeam teleports on contact). On the other side it can see the slime again.
    private void OnWarped(PortalSeam seam, Rigidbody2D rb, Vector2 delta)
    {
        if (rb.gameObject == gameObject)
        {
            _seamTarget = null;
            _lastSeenTime = Time.time; // just crossed: give it a moment to spot the slime again
        }
        else if (_player != null && rb.transform == _player && (_state == State.Chase || _state == State.Attack))
        {
            ChangeState(State.Chase);         // if it was about to hit, now it has to follow
            _seamTarget = seam.ChasePoint;    // after ChangeState: entering another state clears it
        }
    }

    private bool HasLineOfSight(Vector2 target) =>
        !Physics2D.Linecast(transform.position, target, _obstacleMask);

    // The nearest slime chunk (already on the floor) that is in range, in sight and unclaimed
    private SlimeChunk FindChunk()
    {
        SlimeChunk best = null;
        float bestDistance = _chunkDetectRange;
        foreach (var chunk in SlimeChunk.Active)
        {
            if (!chunk.IsLanded || chunk.IsClaimedByOther(this)) continue; // another enemy is already watching it
            Vector2 pos = chunk.transform.position;
            float d = Vector2.Distance(transform.position, pos);
            if (d > bestDistance || !HasLineOfSight(pos)) continue;
            best = chunk;
            bestDistance = d;
        }
        return best;
    }

    private void Start()
    {
        _player = GameObject.FindWithTag("Player").transform;
        _playerHealth = _player.GetComponent<HealthSystem>();
        EnterState(DefaultState); // direct, without ChangeState's check: _state may come from the Inspector
        _health.OnRevived += HandleRevived; // in Start, after EnemyMovement's (Awake): its agent is already back on
    }

    private void Update()
    {
        if (_attack.IsAttacking) return; // finish the hit before deciding anything else

        float distance = Vector2.Distance(transform.position, _player.position);
        bool canSee = !_playerHealth.IsDead && distance <= _detectRange && HasLineOfSight(_player.position);
        if (canSee) _lastSeenTime = Time.time;

        if (_playerHealth.IsDead && (_state == State.Chase || _state == State.Attack))
            ChangeState(DefaultState);

        // A visible slime chunk beats everything except a hit in progress
        if (_state != State.Attack && _state != State.Distracted)
        {
            var chunk = FindChunk();
            if (chunk != null && chunk.TryClaim(this))
            {
                _chunk = chunk;
                ChangeState(State.Distracted);
            }
        }

        switch (_state)
        {
            case State.Idle:   UpdateIdle(canSee); break;
            case State.Patrol: UpdatePatrol(canSee); break;
            case State.Chase:  UpdateChase(distance); break;
            case State.Attack: UpdateAttack(distance); break;
            case State.Distracted: UpdateDistracted(distance, canSee); break;
        }
    }

    private void ChangeState(State newState)
    {
        if (_state == newState) return;
        EnterState(newState);
    }

    // actions that happen only once on entering the state
    private void EnterState(State newState)
    {
        if (_state == State.Distracted && newState != State.Distracted) ReleaseChunk();
        _state = newState;
        if (newState != State.Chase) _seamTarget = null;
        switch (newState)
        {
            case State.Idle:
            case State.Attack:
                _movement.Stop();
                break;
            case State.Patrol:
                _movement.MoveTo(_patrolPoints[_patrolIndex].position); // resumes the point it was heading to
                break;
            case State.Distracted:
                _movement.MoveTo(_chunk.transform.position);
                break;
        }
    }

    private void UpdateIdle(bool canSee)
    {
        if (canSee) ChangeState(State.Chase);
    }

    private void UpdatePatrol(bool canSee)
    {
        if (canSee) { ChangeState(State.Chase); return; }
        if (!_movement.HasArrived) return;

        _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Count;
        _movement.MoveTo(_patrolPoints[_patrolIndex].position);
    }

    private void UpdateChase(float distance)
    {
        // Chasing it to the seam: doesn't give up until it arrives (the seam carries it across)
        if (_seamTarget.HasValue)
        {
            _movement.MoveTo(_seamTarget.Value);
            if (_movement.HasArrived && Vector2.Distance(transform.position, _seamTarget.Value) < 0.75f)
            {
                _seamTarget = null; // arrived but didn't cross (shouldn't happen): back to the normal chase
                _lastSeenTime = Time.time;
            }
            return;
        }

        if (Time.time - _lastSeenTime > _loseSightTime) ChangeState(DefaultState);
        else if (distance <= _attack.Range) ChangeState(State.Attack);
        else _movement.MoveTo(_player.position);
    }

    private void UpdateAttack(float distance)
    {
        if (distance > _attack.Range) { ChangeState(State.Chase); return; }
        _movement.FaceTowards(_player.position);
        _attack.TryAttack(_player.position);
    }

    // Goes to the chunk and stays watching it until it fades or the slime reabsorbs it.
    // If the slime comes within reach meanwhile, it attacks.
    private void UpdateDistracted(float distance, bool canSee)
    {
        if (_chunk == null) // it was destroyed
        {
            ChangeState(canSee ? State.Chase : DefaultState);
            return;
        }
        if (canSee && distance <= _attack.Range)
        {
            ChangeState(State.Attack);
            return;
        }

        Vector2 chunkPos = _chunk.transform.position;
        bool close = Vector2.Distance(transform.position, chunkPos) <= _chunkStopDistance;
        if (close || _movement.HasArrived) // HasArrived: if it can't quite get there (NavMesh edge), it stays as close as possible
        {
            _movement.Stop();
            _movement.FaceTowards(chunkPos);
        }
        else _movement.MoveTo(chunkPos);
    }
}
