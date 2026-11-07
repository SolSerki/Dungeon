using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class Character : MonoBehaviour
{

    [SerializeField] private float _speed = 2.5f;
    [SerializeField] private float _sprintSpeed = 5f;
    [SerializeField] private Vector2 _movement;
    [SerializeField] private Animator _animator;
    [SerializeField] private HealthSystem _health;   
    [SerializeField] private AttackSystem _attack;
    [SerializeField] private Rigidbody2D _rigidbody;

    [SerializeField] private ManaSystem _mana;
    [SerializeField] private float _sprintManaCost = 25f;   // maná por segundo corriendo
    [SerializeField] private float _minManaToSprint = 15f;  // maná mínimo para arrancar a correr

    [SerializeField] private Light2D _light;
    [SerializeField] private float _glowSpeed = 1f;                      // velocidad de la "respiración" de la luz
    [SerializeField, Range(0f, 0.5f)] private float _glowAmount = 0.1f;  // cuánto crece/achica (0.1 = ±10%)

    [Header("Sonidos")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _walkSounds;
    [SerializeField] private AudioClip[] _runSounds;
    [SerializeField] private AudioClip[] _sprintStartSounds;   // un "swish" al arrancar a correr
    [SerializeField] private AudioClip[] _damageSounds;
    [SerializeField] private float _walkStepInterval = 0.55f;  // segundos entre sonidos caminando
    [SerializeField] private float _runStepInterval = 0.3f;    // corriendo suenan más seguido
    [SerializeField, Range(0f, 1f)] private float _walkVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _runVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _sprintStartVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _damageVolume = 0.8f;

    private float _nextStepTime;
    private Vector3 _lightBaseScale;
    private Knockback _knockback;

    private int _animatorMoveXHash;
    private int _animatorMoveYHash;
    private int _isWalkingHash;
    private int _isRunningHash;
    private int _attackHash;
    private int _isDeadHash;
    private int _recieveDamageHash;
    private int _spitHash;
    private int _isChargingHash;

    private InputController _inputController;
    private float _faceOverrideUntil; // mientras dure, el slime mira hacia donde escupió y no hacia donde camina

    private bool _sprintHeld;       // el botón de sprint está apretado
    private bool _sprintExhausted;  // se vació el maná: hay que soltar el sprint para volver a correr
    private bool _isSprinting;

    // Corriendo de verdad: sprint apretado, moviéndose y con maná
    public bool IsSprinting => _isSprinting;

    // Hacia dónde mira el slime: la última dirección en la que se movió
    private Vector2 _facing = Vector2.down;
    public Vector2 Facing => _facing;

    private void Awake()
    {
        // Ensure that the required components are assigned
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_health == null) _health = GetComponent<HealthSystem>();
        if (_attack == null) _attack = GetComponent<AttackSystem>();
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
        if (_mana == null) _mana = GetComponent<ManaSystem>();
        if (_light == null) _light = GetComponentInChildren<Light2D>();
        if (_light != null) _lightBaseScale = _light.transform.localScale;
        if (_audio == null) _audio = GetComponent<AudioSource>();

        // Initialize the knockback component
        _knockback = GetComponent<Knockback>();
    }

    private void Start()
    {
        // Initialization code here
        _inputController = new InputController();
        _inputController.Player.Move.performed += MoveCharacter;
        _inputController.Player.Move.canceled += StopCharacter;
        _inputController.Player.Sprint.performed += SprintCharacter;
        _inputController.Player.Sprint.canceled += StopSprinting;
        _inputController.Player.Attack.performed += AttackCharacter;
        _inputController.Player.Enable();
        _animatorMoveXHash = Animator.StringToHash("_animatorMoveX");
        _animatorMoveYHash = Animator.StringToHash("_animatorMoveY");
        _isWalkingHash = Animator.StringToHash("_isWalking");
        _isRunningHash = Animator.StringToHash("_isRunning");
        _attackHash = Animator.StringToHash("_attack");
        _isDeadHash = Animator.StringToHash("_isDead");
        _recieveDamageHash = Animator.StringToHash("_recieveDamage");
        _spitHash = Animator.StringToHash("_spit");
        _isChargingHash = Animator.StringToHash("_isCharging");
        _attack = GetComponent<AttackSystem>();
        _health = GetComponent<HealthSystem>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _knockback = GetComponent<Knockback>();
        _health.enabled = true;
        _health.OnDied += Died;
        _health.OnDamaged += TakeDamage;
    }
    private void OnDestroy()
    {
        // Unsubscribe from input events to avoid memory leaks
        _inputController.Player.Move.performed -= MoveCharacter;
        _inputController.Player.Sprint.performed -= SprintCharacter;
        _inputController.Player.Sprint.canceled -= StopSprinting;
        _inputController.Player.Move.canceled -= StopCharacter;
        _inputController.Player.Attack.performed -= AttackCharacter;
        _inputController.Player.Disable();
        _health.OnDied -= Died;
        _health.OnDamaged -= TakeDamage;
    }
    private void TakeDamage(HealthSystem.DamageInfo damageInfo)
    {
        Sfx.PlayRandom(_audio, _damageSounds, _damageVolume);
        if (_animator != null)
        {
            _animator.SetTrigger(_recieveDamageHash);
        }
    }
    private void AttackCharacter(InputAction.CallbackContext ctx)
    {
        if (_animator != null && ctx.performed)
        {
            _animator.SetTrigger(_attackHash);
        }
    }
    private void MoveCharacter(InputAction.CallbackContext ctx)
    {
        _movement = ctx.ReadValue<Vector2>();
        if (_movement.sqrMagnitude > 0.01f) _facing = _movement.normalized;

        if (_animator != null && ctx.performed)
        {
            if (Time.time >= _faceOverrideUntil) SetLookDirection(_movement);
            _animator.SetBool(_isWalkingHash, true);
        }
    }

    private void SetLookDirection(Vector2 dir)
    {
        _animator.SetFloat(_animatorMoveXHash, dir.x);
        _animator.SetFloat(_animatorMoveYHash, dir.y);
    }

    // Escupe hacia 'dir': gira al slime para ese lado mientras dura la animación
    public void PlaySpit(Vector2 dir, float duration)
    {
        if (_animator == null || dir.sqrMagnitude < 0.0001f) return;

        _facing = dir.normalized;
        _faceOverrideUntil = Time.time + duration;
        SetLookDirection(_facing);
        _animator.SetBool(_isChargingHash, false);
        _animator.SetTrigger(_spitHash);
    }

    // Mientras carga el escupitajo (se llama cada frame): pose de carga mirando hacia donde apunta
    public void HoldCharge(Vector2 dir)
    {
        if (_animator == null) return;

        if (dir.sqrMagnitude > 0.0001f)
        {
            _facing = dir.normalized;
            SetLookDirection(_facing);
        }
        _faceOverrideUntil = Time.time + 0.1f; // mientras siga cargando, no mira hacia donde camina
        _animator.SetBool(_isChargingHash, true);
    }

    // Se soltó o se canceló la carga sin escupir
    public void EndCharge()
    {
        if (_animator != null) _animator.SetBool(_isChargingHash, false);
    }

    private void StopCharacter(InputAction.CallbackContext ctx)
    {
        _movement = Vector2.zero;
        if (_animator != null)
        {
        _animator.SetBool(_isWalkingHash, false);
        }
    }
    private void SprintCharacter(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) _sprintHeld = true;
    }
    private void StopSprinting(InputAction.CallbackContext ctx)
    {
        _sprintHeld = false;
        _sprintExhausted = false;
    }

    // Decide si corre este frame y descuenta el maná del sprint
    private void UpdateSprint()
    {
        bool moving = _movement.sqrMagnitude > 0.01f;
        bool running;

        if (!_sprintHeld || !moving) running = false;
        else if (_isSprinting) running = true; // ya venía corriendo: sigue mientras quede maná
        else running = !_sprintExhausted && (_mana == null || _mana.CurrentMana >= _minManaToSprint);

        if (running && _mana != null)
        {
            _mana.Drain(_sprintManaCost * Time.deltaTime);
            if (_mana.CurrentMana <= 0f)
            {
                running = false;
                _sprintExhausted = true;
            }
        }

        if (running && !_isSprinting)
        {
            Sfx.PlayRandom(_audio, _sprintStartSounds, _sprintStartVolume);
            _nextStepTime = Time.time + _runStepInterval; // el swish ya cubre el primer paso
        }

        _isSprinting = running;
        if (_animator != null) _animator.SetBool(_isRunningHash, running);
    }

    // Mientras se mueve suena cada cierto tiempo: más seguido y con otro sonido si corre
    private void UpdateMoveSounds()
    {
        bool moving = _movement.sqrMagnitude > 0.01f && _rigidbody.simulated
                      && (_knockback == null || !_knockback.IsKnockedBack);
        if (!moving)
        {
            _nextStepTime = 0f; // al volver a moverse suena enseguida
            return;
        }
        if (Time.time < _nextStepTime) return;

        if (_isSprinting)
        {
            Sfx.PlayRandom(_audio, _runSounds, _runVolume);
            _nextStepTime = Time.time + _runStepInterval;
        }
        else
        {
            Sfx.PlayRandom(_audio, _walkSounds, _walkVolume);
            _nextStepTime = Time.time + _walkStepInterval;
        }
    }

    private void LightGlow()
    {
        if (_light == null) return;

        // La luz "respira" cambiando de tamaño en vez de intensidad, como la Breath Light del tutorial
        float breath = 1f + Mathf.Sin(Time.time * _glowSpeed) * _glowAmount;
        _light.transform.localScale = _lightBaseScale * breath;
    }

    private void Died()
    {
        if (_animator != null)
        {
        _animator.SetBool(_isDeadHash, true);
        }
        _health.enabled = false;
        _attack.enabled = false;
        _inputController.Player.Disable();
        _movement = Vector2.zero;
        _sprintHeld = false;
        _isSprinting = false;
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.simulated = false;
    }

    private void Update()
    {
        UpdateSprint();
        UpdateMoveSounds();
        LightGlow();

        // Terminado el escupitajo, si se está moviendo vuelve a mirar hacia donde camina
        if (_faceOverrideUntil > 0f && Time.time >= _faceOverrideUntil)
        {
            _faceOverrideUntil = 0f;
            if (_movement.sqrMagnitude > 0.01f && _animator != null)
            {
                _facing = _movement.normalized;
                SetLookDirection(_movement);
            }
        }
    }

    private void FixedUpdate()
    {
        // Durante el knockback dejamos que el impulso mueva al personaje
        if (_knockback != null && _knockback.IsKnockedBack) return;

        // Movemos con el Rigidbody2D: la física respeta las paredes y la velocidad
        // se recalcula cada paso, así que el empuje de un golpe no se acumula
        _rigidbody.linearVelocity = _movement * (_isSprinting ? _sprintSpeed : _speed);
    }
}
