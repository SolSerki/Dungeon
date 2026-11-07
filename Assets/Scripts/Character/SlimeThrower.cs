using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Lanza pedazos de slime hacia el mouse (cuesta vida) y suelta uno cuando recibe daño.
// Se carga manteniendo apretado: cuanto más tiempo, más lejos cae.
[RequireComponent(typeof(HealthSystem), typeof(Character))]
public class SlimeThrower : MonoBehaviour
{
    [SerializeField] private SlimeChunk _chunkPrefab;
    [SerializeField] private float _cost = 1f;               // medio corazón
    [SerializeField] private float _minRange = 1f;           // distancia con un toque corto
    [SerializeField, FormerlySerializedAs("_throwDistance")] private float _maxRange = 4f; // distancia con la carga completa
    [SerializeField] private float _chargeTime = 1f;         // segundos apretado para llegar a _maxRange
    [SerializeField] private float _cooldown = 0.3f;           // igual o mayor que la animación de escupir
    [SerializeField] private float _damageDropDistance = 1.2f;
    [SerializeField] private bool _damageDropLures = false;    // el pedazo que se suelta al recibir daño, ¿distrae enemigos?
    [SerializeField] private float _spitDelay = 0f;            // la anticipación ya pasó en la pose de carga: sale al soltar
    [SerializeField] private float _spitAnimLength = 0.25f;    // 3 frames a 12 fps (escupe, rebote, asentarse)

    [Header("Sonidos")]
    [SerializeField] private AudioSource _audio;        // el del slime (para el escupitajo)
    [SerializeField] private AudioSource _chargeAudio;  // aparte, para poder cortar la carga al soltar
    [SerializeField] private AudioClip[] _chargeSounds;
    [SerializeField] private AudioClip[] _releaseSounds;
    [SerializeField, Range(0f, 1f)] private float _chargeVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _releaseVolume = 0.7f;

    private HealthSystem _health;
    private Character _character;
    private InputController _input;
    private float _nextThrowTime;
    private bool _charging;
    private float _chargeStart;
    private InputDevice _aimDevice; // con qué se está apuntando (mouse o joystick)

    public bool IsCharging => _charging;
    public SlimeChunk ChunkPrefab => _chunkPrefab;

    // 0..1 mientras se mantiene apretado
    public float ChargeProgress => _charging ? Mathf.Clamp01((Time.time - _chargeStart) / _chargeTime) : 0f;

    // Hacia dónde iría el pedazo (sin contar paredes) si se soltara ahora
    public Vector2 AimOffset(Vector2 from) =>
        AimDirection(from) * Mathf.Lerp(_minRange, _maxRange, ChargeProgress);

    // Dónde caería el pedazo si se soltara ahora (para la vista previa)
    public Vector2 PreviewTarget(Vector2 from) => _chunkPrefab.ResolveTarget(from, from + AimOffset(from));

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _character = GetComponent<Character>();
        _input = new InputController();
    }

    private void OnEnable()
    {
        _input.Player.Throw.started += OnChargeStarted;
        _input.Player.Throw.canceled += OnChargeReleased;
        _input.Player.Throw.Enable();
        _input.Player.Attack.performed += OnAttack;
        _input.Player.Attack.Enable();
        _health.OnDamaged += DropOnDamage;
    }

    private void OnDisable()
    {
        _input.Player.Throw.started -= OnChargeStarted;
        _input.Player.Throw.canceled -= OnChargeReleased;
        _input.Player.Throw.Disable();
        _input.Player.Attack.performed -= OnAttack;
        _input.Player.Attack.Disable();
        _health.OnDamaged -= DropOnDamage;
        CancelCharge();
    }

    private void Update()
    {
        // Pose de carga mirando hacia donde apunta
        if (_charging) _character.HoldCharge(AimDirection(transform.position));
    }

    // Atacar cancela la carga (no gasta vida): el ataque tiene prioridad
    private void OnAttack(InputAction.CallbackContext ctx) => CancelCharge();

    private void CancelCharge()
    {
        if (!_charging) return;
        _charging = false;
        StopChargeSound();
        _character.EndCharge();
    }

    private void StopChargeSound()
    {
        if (_chargeAudio != null) _chargeAudio.Stop();
    }

    private void OnDestroy()
    {
        _input?.Dispose();
    }

    // Al apretar: empieza a cargar (si puede lanzar)
    private void OnChargeStarted(InputAction.CallbackContext ctx)
    {
        if (_chunkPrefab == null || Time.time < _nextThrowTime || _health.IsDead) return;
        if (_health.CurrentHealth <= _cost) return; // con medio corazón no se puede lanzar

        _charging = true;
        _chargeStart = Time.time;
        _aimDevice = ctx.control?.device;

        var clip = Sfx.Random(_chargeSounds);
        if (_chargeAudio != null && clip != null)
        {
            _chargeAudio.clip = clip;
            _chargeAudio.volume = _chargeVolume;
            _chargeAudio.Play();
        }
    }

    // Al soltar: escupe a una distancia según cuánto tiempo estuvo apretado
    private void OnChargeReleased(InputAction.CallbackContext ctx)
    {
        if (!_charging) return;
        if (ctx.control != null) _aimDevice = ctx.control.device;
        Vector2 offset = AimOffset(transform.position); // misma cuenta que la vista previa
        _charging = false;
        StopChargeSound();

        if (_health.IsDead || !_health.TrySpend(_cost))
        {
            _character.EndCharge();
            return;
        }

        Sfx.PlayRandom(_audio, _releaseSounds, _releaseVolume);
        _character.PlaySpit(offset, _spitAnimLength);
        StartCoroutine(SpitAfterDelay(offset));
        _nextThrowTime = Time.time + _cooldown;
    }

    // El pedazo sale cuando la animación lo escupe, no al apretar el botón
    private IEnumerator SpitAfterDelay(Vector2 offset)
    {
        yield return new WaitForSeconds(_spitDelay);
        if (_health.IsDead) yield break;

        Vector2 from = transform.position;
        Spawn(from, from + offset);
    }

    // Con mouse: hacia el cursor. Con joystick (o sin cámara): hacia donde mira el slime.
    private Vector2 AimDirection(Vector2 from)
    {
        var cam = Camera.main;
        if (_aimDevice is Mouse mouse && cam != null)
        {
            Vector2 cursor = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 offset = cursor - from;
            if (offset.sqrMagnitude > 0.0001f) return offset.normalized;
        }
        return _character.Facing;
    }

    private void DropOnDamage(HealthSystem.DamageInfo info)
    {
        CancelCharge(); // un golpe corta la carga (no gasta vida)
        if (_chunkPrefab == null || _health.IsDead) return;

        // Direction es hacia donde te empuja el golpe: el pedazo sale para el otro lado, hacia el enemigo
        Vector2 away = info.Direction.sqrMagnitude > 0.0001f ? -info.Direction.normalized : -_character.Facing;
        Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f)) * away;
        Vector2 from = transform.position;
        Spawn(from, from + dir * _damageDropDistance, _damageDropLures);
    }

    private void Spawn(Vector2 from, Vector2 to, bool lures = true)
    {
        var chunk = Instantiate(_chunkPrefab, from, Quaternion.identity);
        chunk.Launch(from, to, lures);
    }
}
