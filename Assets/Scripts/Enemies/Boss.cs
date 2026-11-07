using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

// Jefe final (Shadow Demon Dragon).
// De Enemy hereda la vida, la animación de daño y la muerte; acá le sumamos su propia IA:
// duerme hasta que el jugador se acerca, lo persigue y ataca con un coletazo que además
// lanza una onda de sombra. Por debajo de la mitad de la vida entra en fase 2:
// camina más rápido, ataca más seguido y lanza tres ondas en abanico.
[RequireComponent(typeof(EnemyMovement), typeof(NavMeshAgent))]
public class Boss : Enemy
{
    private enum State { Dormant, Chase, Attack, Stagger, Dead }

    [Header("Referencias")]
    [SerializeField] private EnemyMovement _movement;
    [SerializeField] private Transform _waveOrigin;           // de dónde sale la onda, del lado derecho (se espeja al mirar a la izquierda)
    [SerializeField] private BossProjectile _wavePrefab;
    [SerializeField] private CinemachineImpulseSource _impulse;
    [SerializeField] private AudioSource _audio;

    [Header("Detección")]
    [SerializeField] private float _wakeRange = 8f;           // a esta distancia se despierta y ya no deja de perseguir

    [Header("Ataque")]
    [SerializeField] private float _meleeRange = 3f;          // distancia a la que frena y ataca
    [SerializeField] private float _waveRange = 8f;           // desde más lejos ataca solo si la onda está lista
    [SerializeField] private float _meleeDamage = 1f;
    [SerializeField] private float _bodyHeight = 0.8f;        // centro del cuerpo sobre los pies (el pivot)
    [SerializeField] private float _meleeReach = 1.6f;        // cuánto se adelanta el golpe hacia el jugador
    [SerializeField] private float _meleeRadius = 1.5f;
    [SerializeField] private float _attackDuration = 0.83f;   // largo del clip de ataque (10 frames a 12 fps)
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private float _waveCooldown = 4f;
    [SerializeField] private float _impactShake = 0.4f;

    [Header("Fase 2")]
    [SerializeField, Range(0f, 1f)] private float _phase2Threshold = 0.5f; // fracción de vida
    [SerializeField] private float _phase2SpeedMultiplier = 1.5f;
    [SerializeField] private float _phase2CooldownMultiplier = 0.6f;
    [SerializeField] private int _phase2Waves = 3;
    [SerializeField] private float _waveSpreadAngle = 25f;    // grados entre ondas del abanico

    [Header("Reacción al daño")]
    [SerializeField] private float _staggerDuration = 1.25f;  // largo del clip de daño (30 frames a 24 fps)
    [SerializeField] private float _staggerCooldown = 4f;     // un jefe no se interrumpe con cada golpe

    [Header("Derrota")]
    [SerializeField] private float _defeatDelay = 2f;         // deja ver la animación de muerte
    [SerializeField] private GameObject[] _activateOnDefeat;  // ej.: la salida del nivel

    [Header("Sonidos")]
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

    private float FacingSign => _spriteRenderer.flipX ? -1f : 1f; // EnemyMovement voltea con flipX

    protected override void Start()
    {
        base.Start(); // Enemy se suscribe a OnDamaged y OnDied
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
            _movement.Stop(); // ya está al lado: espera el cooldown mirando al jugador
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
        _attackDirection = ((Vector2)_player.position - BodyCenter).normalized; // el golpe va hacia donde estaba el jugador al empezar
        _animator.SetTrigger("_attack");
        Play(_attackSound);
    }

    private Vector2 BodyCenter => (Vector2)transform.position + Vector2.up * _bodyHeight;
    private Vector2 MeleeCenter() => BodyCenter + _attackDirection * _meleeReach;

    // Animation Event: frame del coletazo en ShadowDragon_attack_*
    public void AttackImpact()
    {
        if (_state != State.Attack) return; // el golpe se canceló (murió en medio del ataque)

        Shake(_impactShake);

        Vector2 center = MeleeCenter();
        foreach (var hit in Physics2D.OverlapCircleAll(center, _meleeRadius))
        {
            var character = hit.GetComponentInParent<Character>();
            if (character == null) continue;

            character.GetComponent<HealthSystem>().TakeDamage(_meleeDamage, _attackDirection);
            break; // el Character tiene dos colliders: un solo golpe
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
            float angle = (i - (count - 1) / 2f) * _waveSpreadAngle; // 1 onda: 0°; 3 ondas: -25°, 0°, 25°
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            Instantiate(_wavePrefab, origin, Quaternion.identity).Launch(direction);
        }
    }

    // Animation Event: pasos en ShadowDragon_walk_*
    public void Footstep() => Play(_footstepSound, 0.6f);

    // Animation Event: cuando el cuerpo toca el suelo en ShadowDragon_death_*
    public void DeathFall()
    {
        Play(_fallSound);
        Shake(_impactShake * 2f);
    }

    public override void TakeDamage(HealthSystem.DamageInfo damageInfo)
    {
        if (damageInfo.CurrentHealth <= 0f) return; // el golpe final lo maneja Die
        Play(_hitSound);

        if (_state == State.Dormant) Wake(); // si lo atacan dormido, se despierta

        if (!_isPhase2 && damageInfo.CurrentHealth <= damageInfo.MaxHealth * _phase2Threshold)
        {
            EnterPhase2();
            return;
        }

        // no interrumpe sus propios ataques y no se aturde con cada golpe
        if (_state == State.Attack || Time.time < _nextStaggerTime) return;
        Stagger();
    }

    private void Stagger()
    {
        _state = State.Stagger;
        _stateEndTime = Time.time + _staggerDuration;
        _nextStaggerTime = _stateEndTime + _staggerCooldown;
        _movement.Stop();
        base.TakeDamage(default); // Enemy dispara la animación de daño
    }

    private void EnterPhase2()
    {
        _isPhase2 = true;
        _agent.speed *= _phase2SpeedMultiplier;
        Play(_wakeSound);
        Shake(_impactShake * 2f);
        Stagger(); // se sacude y vuelve más agresivo
    }

    protected override void Die()
    {
        base.Die(); // animación de muerte, apaga la física y se desuscribe
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
