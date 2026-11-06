using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private enum State { Idle, Patrol, Chase, Attack }

    [SerializeField] private EnemyMovement _movement;
    [SerializeField] private EnemyMeleeAttack _attack;
    [SerializeField] private HealthSystem _health;
    [SerializeField] private List<Transform> _patrolPoints;
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float _detectRange = 4f;
    [SerializeField] private float _loseSightTime = 2f;

    private HealthSystem _playerHealth;
    private Transform _player;
    [SerializeField] private State _state;
    private int _patrolIndex = 0;
    private float _lastSeenTime = -Mathf.Infinity;

    // con un solo punto no hay a dónde patrullar
    private State DefaultState => _patrolPoints.Count > 1 ? State.Patrol : State.Idle;

    private void OnEnable() => _health.OnDied += HandleDied;
    private void OnDisable() => _health.OnDied -= HandleDied;
    private void HandleDied() => enabled = false;

    private bool HasLineOfSight() =>
        !Physics2D.Linecast(transform.position, _player.position, _obstacleMask);

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
        bool canSee = !_playerHealth.IsDead && distance <= _detectRange && HasLineOfSight();
        if (canSee) _lastSeenTime = Time.time;

        if (_playerHealth.IsDead && (_state == State.Chase || _state == State.Attack))
            ChangeState(DefaultState);

        switch (_state)
        {
            case State.Idle:   UpdateIdle(canSee); break;
            case State.Patrol: UpdatePatrol(canSee); break;
            case State.Chase:  UpdateChase(distance); break;
            case State.Attack: UpdateAttack(distance); break;
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
}
