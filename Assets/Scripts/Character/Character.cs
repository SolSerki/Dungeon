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

    private Knockback _knockback;

    private int _animatorMoveXHash;
    private int _animatorMoveYHash;
    private int _isWalkingHash;
    private int _isRunningHash;
    private int _attackHash;
    private int _isDeadHash;
    private int _recieveDamageHash;

    private InputController _inputController;

    private void Awake()
    {
        // Ensure that the required components are assigned
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_health == null) _health = GetComponent<HealthSystem>();
        if (_attack == null) _attack = GetComponent<AttackSystem>();
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
        if (_light == null) _light = GetComponentInChildren<Light2D>();

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

        if (_animator != null && ctx.performed)
        {
            _animator.SetFloat(_animatorMoveXHash, _movement.x);
            _animator.SetFloat(_animatorMoveYHash, _movement.y);
            _animator.SetBool(_isWalkingHash, true);
        }
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
        // Implement sprint logic here
        if (_animator != null && ctx.performed)
        {
            _animator.SetBool(_isRunningHash, true);
        }
    }
    private void StopSprinting(InputAction.CallbackContext ctx)
    {
        if (_animator != null)
        {
            _animator.SetBool(_isRunningHash, false);
        }
    }

    private void LightGlow (){

        _light.intensity = Mathf.PingPong(Time.time, 1f) + 0.5f; // Ajusta la intensidad de la luz entre 0.5 y 1.5
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
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.simulated = false;
    }

    private void Update()
    {
        LightGlow();
    }

    private void FixedUpdate()
    {
        // Durante el knockback dejamos que el impulso mueva al personaje
        if (_knockback != null && _knockback.IsKnockedBack) return;

        // Movemos con el Rigidbody2D: la física respeta las paredes y la velocidad
        // se recalcula cada paso, así que el empuje de un golpe no se acumula
        bool running = _animator != null && _animator.GetBool(_isRunningHash);
        _rigidbody.linearVelocity = _movement * (running ? _sprintSpeed : _speed);
    }
}
