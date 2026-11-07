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
    [SerializeField] private float _chunkDetectRange = 4f;   // desde qué distancia ve un pedazo de slime
    [SerializeField] private float _chunkStopDistance = 0.5f; // se queda a esta distancia del pedazo

    private HealthSystem _playerHealth;
    private Transform _player;
    [SerializeField] private State _state;
    private int _patrolIndex = 0;
    private float _lastSeenTime = -Mathf.Infinity;
    private SlimeChunk _chunk; // el pedazo que lo está distrayendo

    // con un solo punto no hay a dónde patrullar
    private State DefaultState => _patrolPoints.Count > 1 ? State.Patrol : State.Idle;

    private void OnEnable() => _health.OnDied += HandleDied;
    private void OnDisable() => _health.OnDied -= HandleDied;
    private void HandleDied() => enabled = false;

    private bool HasLineOfSight(Vector2 target) =>
        !Physics2D.Linecast(transform.position, target, _obstacleMask);

    // El pedazo de slime (ya en el piso) más cercano que esté en rango y a la vista
    private SlimeChunk FindChunk()
    {
        SlimeChunk best = null;
        float bestDistance = _chunkDetectRange;
        foreach (var chunk in SlimeChunk.Active)
        {
            if (!chunk.IsLanded || !chunk.Lures) continue;
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
        EnterState(DefaultState); // directo, sin el chequeo de ChangeState: _state puede venir del Inspector
    }

    private void Update()
    {
        if (_attack.IsAttacking) return; // termina el golpe antes de decidir otra cosa

        float distance = Vector2.Distance(transform.position, _player.position);
        bool canSee = !_playerHealth.IsDead && distance <= _detectRange && HasLineOfSight(_player.position);
        if (canSee) _lastSeenTime = Time.time;

        if (_playerHealth.IsDead && (_state == State.Chase || _state == State.Attack))
            ChangeState(DefaultState);

        // Un pedazo de slime a la vista le gana a todo menos a un golpe en curso
        if (_state != State.Attack && _state != State.Distracted)
        {
            var chunk = FindChunk();
            if (chunk != null)
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

    // acciones que pasan una sola vez al entrar al estado
    private void EnterState(State newState)
    {
        _state = newState;
        switch (newState)
        {
            case State.Idle:
            case State.Attack:
                _movement.Stop();
                break;
            case State.Patrol:
                _movement.MoveTo(_patrolPoints[_patrolIndex].position); // retoma el punto al que iba
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

    // Va hacia el pedazo y se queda mirándolo hasta que se apaga o el slime lo reabsorbe.
    // Si el slime se le pone a tiro mientras tanto, lo ataca.
    private void UpdateDistracted(float distance, bool canSee)
    {
        if (_chunk == null) // se destruyó
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
        if (close || _movement.HasArrived) // HasArrived: si no llega del todo (borde del NavMesh), se queda lo más cerca posible
        {
            _movement.Stop();
            _movement.FaceTowards(chunkPos);
        }
        else _movement.MoveTo(chunkPos);
    }
}
