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

    [SerializeField] private Light2D _light;
    [SerializeField] private float _glowSpeed = 1f;                      // speed of the light's "breathing"
    [SerializeField, Range(0f, 0.5f)] private float _glowAmount = 0.1f;  // how much it grows/shrinks (0.1 = ±10%)

    [Header("Focus (E): stops moving and the light reaches further")]
    [SerializeField] private float _focusLightScale = 2f;  // how much the light's diameter grows
    [SerializeField] private float _focusSpeed = 6f;       // how fast it grows and returns
    [SerializeField] private ManaSystem _mana;
    [SerializeField] private float _focusManaCost = 25f;    // mana per second while focusing
    [SerializeField] private float _minManaToFocus = 15f;   // minimum mana to start

    [Header("Sounds")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _walkSounds;
    [SerializeField] private AudioClip[] _runSounds;
    [SerializeField] private AudioClip[] _sprintStartSounds;   // a "swish" when starting to run
    [SerializeField] private AudioClip[] _damageSounds;
    [SerializeField] private float _walkStepInterval = 0.55f;  // seconds between sounds while walking
    [SerializeField] private float _runStepInterval = 0.3f;    // running, they play more often
    [SerializeField, Range(0f, 1f)] private float _walkVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _runVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _sprintStartVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _damageVolume = 0.8f;
    [SerializeField] private SfxLoop _focusSound;               // continuous while focusing (E)

    private float _nextStepTime;
    private bool _focusHeld;       // E held
    private bool _isFocusing;      // actually focusing: E held and has mana
    private bool _focusExhausted;  // mana ran out: E must be released to focus again
    private float _focusAmount;  // 0 = normal light, 1 = enlarged light (rises and falls smoothly)
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
    private int _isFocusingHash;

    private InputController _inputController;
    private float _faceOverrideUntil; // while it lasts, the slime faces where it spat instead of where it walks

    private bool _sprintHeld;       // the sprint button is held
    private bool _isSprinting;

    private bool _isCharging;       // charging the spit
    private float _spitLockUntil;   // while the spit animation lasts

    // Actually running: sprint held and moving
    public bool IsSprinting => _isSprinting;

    // While focusing, charging or spitting it stays still even if a direction is pressed
    private bool IsRooted => _isFocusing || _isCharging || Time.time < _spitLockUntil;

    // Actually moving: there's input, nothing stops it and it isn't dead
    public bool IsMoving => _movement.sqrMagnitude > 0.01f && !IsRooted
                            && (_rigidbody == null || _rigidbody.simulated);

    // Stopped with the light enlarged
    public bool IsFocusing => _isFocusing;

    // Where the slime faces: the last direction it moved in
    private Vector2 _facing = Vector2.down;
    public Vector2 Facing => _facing;

    private void Awake()
    {
        // Ensure that the required components are assigned
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_health == null) _health = GetComponent<HealthSystem>();
        if (_attack == null) _attack = GetComponent<AttackSystem>();
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
        if (_light == null) _light = GetComponentInChildren<Light2D>();
        if (_mana == null) _mana = GetComponent<ManaSystem>();
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
        _inputController.Player.Focus.started += StartFocus;
        _inputController.Player.Focus.canceled += StopFocus;
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
        _isFocusingHash = Animator.StringToHash("_isFocusing");
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
        _inputController.Player.Focus.started -= StartFocus;
        _inputController.Player.Focus.canceled -= StopFocus;
        _inputController.Player.Disable();
        _health.OnDied -= Died;
        _health.OnDamaged -= TakeDamage;
    }
    // For sounds of objects destroyed instantly (pickups): they play from the slime
    public void PlaySound(AudioClip[] clips, float volume = 1f) => Sfx.PlayRandom(_audio, clips, volume);

    // Cutscenes (e.g. the dragon's dialogue): with false the slime ignores input and stands still.
    // Disabling the actions cancels the ones in progress, so StopCharacter/StopSprinting/StopFocus run;
    // when enabled again, a direction that is still held makes it walk right away.
    public void SetControlsEnabled(bool enabled)
    {
        if (enabled)
        {
            _inputController.Player.Enable();
            return;
        }
        _inputController.Player.Disable();
        _movement = Vector2.zero;
        _sprintHeld = false;
        _focusHeld = false;
        if (_animator != null) _animator.SetBool(_isWalkingHash, false);
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
            _animator.SetBool(_isWalkingHash, !_isFocusing); // while focusing it turns but doesn't walk
        }
    }

    private void SetLookDirection(Vector2 dir)
    {
        _animator.SetFloat(_animatorMoveXHash, dir.x);
        _animator.SetFloat(_animatorMoveYHash, dir.y);
    }

    // Spits towards 'dir': turns the slime that way while the animation lasts
    public void PlaySpit(Vector2 dir, float duration)
    {
        if (_animator == null || dir.sqrMagnitude < 0.0001f) return;

        _isCharging = false;
        _spitLockUntil = Time.time + duration; // doesn't start walking until it finishes spitting
        _facing = dir.normalized;
        _faceOverrideUntil = Time.time + duration;
        SetLookDirection(_facing);
        _animator.SetBool(_isChargingHash, false);
        _animator.SetTrigger(_spitHash);
    }

    // While charging the spit (called every frame): charge pose facing where it aims
    public void HoldCharge(Vector2 dir)
    {
        _isCharging = true; // stops even without an Animator
        if (_animator == null) return;

        if (dir.sqrMagnitude > 0.0001f)
        {
            _facing = dir.normalized;
            SetLookDirection(_facing);
        }
        _faceOverrideUntil = Time.time + 0.1f; // while still charging, it doesn't face where it walks
        _animator.SetBool(_isChargingHash, true);
    }

    // The charge was released or cancelled without spitting
    public void EndCharge()
    {
        _isCharging = false;
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
    }

    // Focus: while E is held (and there's mana) the slime stays still and its light grows
    private void StartFocus(InputAction.CallbackContext ctx) => _focusHeld = true;

    private void StopFocus(InputAction.CallbackContext ctx)
    {
        _focusHeld = false;
        _focusExhausted = false;
    }

    // Decides whether it focuses this frame and spends the mana (same as the space bar trail)
    private void UpdateFocus()
    {
        bool focusing;
        if (!_focusHeld) focusing = false;
        else if (_isFocusing) focusing = true; // was already focusing: keeps going while there's mana
        else focusing = !_focusExhausted && (_mana == null || _mana.CurrentMana >= _minManaToFocus);

        if (focusing && _mana != null)
        {
            _mana.Drain(_focusManaCost * Time.deltaTime);
            if (_mana.CurrentMana <= 0f)
            {
                focusing = false;
                _focusExhausted = true;
            }
        }

        if (focusing != _isFocusing) SetFocusing(focusing);
    }

    private void SetFocusing(bool focusing)
    {
        _isFocusing = focusing;
        if (_focusSound != null) _focusSound.Playing = focusing;
        if (_animator == null) return;

        _animator.SetBool(_isFocusingHash, focusing); // animation: shrinks and shakes with a straining face
        if (focusing)
        {
            _animator.SetBool(_isWalkingHash, false);
        }
        else if (_movement.sqrMagnitude > 0.01f)
        {
            // If a direction is still held, resumes walking without having to release it
            _animator.SetBool(_isWalkingHash, true);
            if (Time.time >= _faceOverrideUntil) SetLookDirection(_movement);
        }
    }

    // Running is free: while sprint is held and it's moving, it runs
    private void UpdateSprint()
    {
        bool running = _sprintHeld && IsMoving;

        if (running && !_isSprinting)
        {
            Sfx.PlayRandom(_audio, _sprintStartSounds, _sprintStartVolume);
            _nextStepTime = Time.time + _runStepInterval; // the swish already covers the first step
        }

        _isSprinting = running;
        if (_animator != null) _animator.SetBool(_isRunningHash, running);
    }

    // While moving it plays every so often: more often and with another sound when running
    private void UpdateMoveSounds()
    {
        bool moving = IsMoving && (_knockback == null || !_knockback.IsKnockedBack);
        if (!moving)
        {
            _nextStepTime = 0f; // when it starts moving again it plays right away
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

        // The light "breathes" by changing size instead of intensity, like the tutorial's Breath Light
        float breath = 1f + Mathf.Sin(Time.time * _glowSpeed) * _glowAmount;

        // While focusing, the light's diameter grows up to _focusLightScale (and eases back on release)
        float target = _isFocusing ? 1f : 0f;
        _focusAmount = Mathf.Lerp(_focusAmount, target, 1f - Mathf.Exp(-_focusSpeed * Time.deltaTime));
        float focus = Mathf.Lerp(1f, _focusLightScale, _focusAmount);

        _light.transform.localScale = _lightBaseScale * (breath * focus);
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
        _focusHeld = false;
        _isFocusing = false;
        if (_focusSound != null) _focusSound.Playing = false;
        if (_animator != null) _animator.SetBool(_isFocusingHash, false);
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.simulated = false;
    }

    private void Update()
    {
        UpdateFocus();
        UpdateSprint();
        UpdateMoveSounds();
        LightGlow();

        // Once the spit ends, if it's moving it faces where it walks again
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
        // During knockback, let the impulse move the character
        if (_knockback != null && _knockback.IsKnockedBack) return;

        // Move with the Rigidbody2D: physics respects the walls and the velocity
        // is recalculated every step, so a hit's push doesn't accumulate
        // While focusing, charging or spitting it stays still even if a direction is pressed
        Vector2 move = IsRooted ? Vector2.zero : _movement;
        _rigidbody.linearVelocity = move * (_isSprinting ? _sprintSpeed : _speed);
    }
}
