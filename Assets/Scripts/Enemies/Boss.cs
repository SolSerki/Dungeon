using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

// Final boss (Shadow Demon Dragon).
// It inherits health, the hurt animation and death from Enemy; here we add its own AI:
// it sleeps until the player gets close, chases them and attacks with a tail swipe that also
// launches a shadow wave. Below half health it enters phase 2:
// it walks faster, attacks more often and launches three waves in a fan.
[RequireComponent(typeof(EnemyMovement), typeof(NavMeshAgent))]
public class Boss : Enemy
{
    private enum State { Dormant, Chase, Attack, Stagger, Dead }

    [Header("References")]
    [SerializeField] private EnemyMovement _movement;
    [SerializeField] private Transform _waveOrigin;           // where the wave comes out, on the right side (mirrored when facing left)
    [SerializeField] private BossProjectile _wavePrefab;
    [SerializeField] private CinemachineImpulseSource _impulse;
    // _audio now lives in Enemy (protected): its hurt/death sounds and the Boss's play from the same source

    [Header("Detection")]
    [SerializeField] private float _wakeRange = 8f;           // wakes up at this distance and never stops chasing

    [Header("Attack")]
    [SerializeField] private float _meleeRange = 3f;          // distance at which it stops and attacks
    [SerializeField] private float _waveRange = 8f;           // from further away it only attacks if the wave is ready
    [SerializeField] private float _meleeDamage = 1f;
    [SerializeField] private float _bodyHeight = 0.8f;        // body center above the feet (the pivot)
    [SerializeField] private float _meleeReach = 1.6f;        // how far the hit reaches towards the player
    [SerializeField] private float _meleeRadius = 1.5f;
    [SerializeField] private float _attackDuration = 0.83f;   // attack clip length (10 frames at 12 fps)
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private float _waveCooldown = 4f;
    [SerializeField] private float _impactShake = 0.4f;

    [Header("Phase 2")]
    [SerializeField, Range(0f, 1f)] private float _phase2Threshold = 0.5f; // fraction of health
    [SerializeField] private float _phase2SpeedMultiplier = 1.5f;
    [SerializeField] private float _phase2CooldownMultiplier = 0.6f;
    [SerializeField] private int _phase2Waves = 3;
    [SerializeField] private float _waveSpreadAngle = 25f;    // degrees between the waves in the fan

    [Header("Damage reaction")]
    [SerializeField] private float _staggerDuration = 1.25f;  // hurt clip length (30 frames at 24 fps)
    [SerializeField] private float _staggerCooldown = 4f;     // a boss isn't interrupted by every hit

    [Header("Defeat")]
    [SerializeField] private float _defeatDelay = 2f;         // lets the death animation play out
    [SerializeField] private GameObject[] _activateOnDefeat;  // e.g. the level exit

    [Header("Sounds")]
    [SerializeField] private AudioClip _wakeSound;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _deathSound;
    [SerializeField] private AudioClip _fallSound;
    [SerializeField] private AudioClip _footstepSound;

    private State _state = State.Dormant;
    private Transform _player;
    private HealthSystem _playerHealth;
    private NavMeshAgent _agent;
    private bool _isPhase2;
    private bool _attackWithWave;
    private Vector2 _attackDirection = Vector2.right;
    private float _stateEndTime;
    private float _nextAttackTime;
    private float _nextWaveTime;
    private float _nextStaggerTime;

    public bool IsAwake => _state != State.Dormant;

    private float FacingSign => _spriteRenderer.flipX ? -1f : 1f; // EnemyMovement flips with flipX

    protected override void Start()
    {
        base.Start(); // Enemy subscribes to OnDamaged and OnDied
        _agent = GetComponent<NavMeshAgent>();
        _player = GameObject.FindWithTag("Player").transform;
        _playerHealth = _player.GetComponent<HealthSystem>();
        _movement.Stop();
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        if (_playerHealth.IsDead)
        {
            _movement.Stop();
            return;
        }

        float distance = Vector2.Distance(transform.position, _player.position);
        switch (_state)
        {
            case State.Dormant:
                if (distance <= _wakeRange) Wake();
                break;
            case State.Chase:
                UpdateChase(distance);
                break;
            case State.Attack:
            case State.Stagger:
                if (Time.time >= _stateEndTime) _state = State.Chase;
                break;
        }
    }

    private void Wake()
    {
        _state = State.Chase;
        _nextAttackTime = Time.time + 0.5f;
        _nextWaveTime = Time.time + 1.5f;
        Play(_wakeSound);
        Shake(_impactShake);
    }

    private void UpdateChase(float distance)
    {
        bool waveReady = Time.time >= _nextWaveTime;
        bool attackReady = Time.time >= _nextAttackTime;

        if (attackReady && (distance <= _meleeRange || (waveReady && distance <= _waveRange)))
        {
            StartAttack(withWave: waveReady);
        }
        else if (distance <= _meleeRange)
        {
            _movement.Stop(); // already next to it: waits out the cooldown facing the player
            _movement.FaceTowards(_player.position);
        }
        else
        {
            _movement.MoveTo(_player.position);
        }
    }

    private void StartAttack(bool withWave)
    {
        _state = State.Attack;
        _attackWithWave = withWave;
        _stateEndTime = Time.time + _attackDuration;

        float cooldownMultiplier = _isPhase2 ? _phase2CooldownMultiplier : 1f;
        _nextAttackTime = _stateEndTime + _attackCooldown * cooldownMultiplier;
        if (withWave) _nextWaveTime = _stateEndTime + _waveCooldown * cooldownMultiplier;

        _movement.Stop();
        _movement.FaceTowards(_player.position);
        _attackDirection = ((Vector2)_player.position - BodyCenter).normalized; // the hit goes where the player was when it started
        _animator.SetTrigger("_attack");
        Play(_attackSound);
    }

    private Vector2 BodyCenter => (Vector2)transform.position + Vector2.up * _bodyHeight;
    private Vector2 MeleeCenter() => BodyCenter + _attackDirection * _meleeReach;

    // Animation Event: tail swipe frame in ShadowDragon_attack_*
    public void AttackImpact()
    {
        if (_state != State.Attack) return; // the hit was cancelled (it died mid-attack)

        Shake(_impactShake);

        Vector2 center = MeleeCenter();
        foreach (var hit in Physics2D.OverlapCircleAll(center, _meleeRadius))
        {
            var character = hit.GetComponentInParent<Character>();
            if (character == null) continue;

            character.GetComponent<HealthSystem>().TakeDamage(_meleeDamage, _attackDirection);
            break; // the Character has two colliders: a single hit
        }

        if (_attackWithWave) LaunchWaves();
    }

    private void LaunchWaves()
    {
        Vector2 origin = transform.position;
        if (_waveOrigin != null)
        {
            Vector2 local = _waveOrigin.localPosition;
            origin += new Vector2(local.x * FacingSign, local.y);
        }

        Vector2 aim = ((Vector2)_player.position - origin).normalized;
        int count = _isPhase2 ? _phase2Waves : 1;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) / 2f) * _waveSpreadAngle; // 1 wave: 0°; 3 waves: -25°, 0°, 25°
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            Instantiate(_wavePrefab, origin, Quaternion.identity).Launch(direction);
        }
    }

    // Animation Event: footsteps in ShadowDragon_walk_*
    public void Footstep() => Play(_footstepSound, 0.6f);

    // Animation Event: when the body hits the ground in ShadowDragon_death_*
    public void DeathFall()
    {
        Play(_fallSound);
        Shake(_impactShake * 2f);
    }

    public override void TakeDamage(HealthSystem.DamageInfo damageInfo)
    {
        if (damageInfo.CurrentHealth <= 0f) return; // the final blow is handled by Die
        Play(_hitSound);

        if (_state == State.Dormant) Wake(); // if attacked while asleep, it wakes up

        if (!_isPhase2 && damageInfo.CurrentHealth <= damageInfo.MaxHealth * _phase2Threshold)
        {
            EnterPhase2();
            return;
        }

        // doesn't interrupt its own attacks and isn't stunned by every hit
        if (_state == State.Attack || Time.time < _nextStaggerTime) return;
        Stagger();
    }

    private void Stagger()
    {
        _state = State.Stagger;
        _stateEndTime = Time.time + _staggerDuration;
        _nextStaggerTime = _stateEndTime + _staggerCooldown;
        _movement.Stop();
        base.TakeDamage(default); // Enemy triggers the hurt animation
    }

    private void EnterPhase2()
    {
        _isPhase2 = true;
        _agent.speed *= _phase2SpeedMultiplier;
        Play(_wakeSound);
        Shake(_impactShake * 2f);
        Stagger(); // shakes it off and comes back more aggressive
    }

    protected override void Die()
    {
        base.Die(); // death animation, turns off physics and unsubscribes
        _state = State.Dead;
        Play(_deathSound);
        StartCoroutine(Defeat());
    }

    private IEnumerator Defeat()
    {
        yield return new WaitForSeconds(_defeatDelay);
        foreach (var go in _activateOnDefeat)
            if (go != null) go.SetActive(true);
    }

    private void Play(AudioClip clip, float volume = 1f)
    {
        if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume);
    }

    private void Shake(float force)
    {
        if (_impulse != null) _impulse.GenerateImpulseWithVelocity(Vector3.down * force);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _wakeRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(MeleeCenter(), _meleeRadius);
    }
}
