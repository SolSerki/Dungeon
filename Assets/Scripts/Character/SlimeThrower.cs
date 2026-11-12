using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Throws slime chunks towards the mouse (costs health) and drops one when taking damage.
// It charges while held: the longer, the further it lands.
[RequireComponent(typeof(HealthSystem), typeof(Character))]
public class SlimeThrower : MonoBehaviour
{
    [SerializeField] private SlimeChunk _chunkPrefab;
    [SerializeField] private float _cost = 1f;               // half a heart
    [SerializeField] private float _minRange = 1f;           // distance with a short tap
    [SerializeField, FormerlySerializedAs("_throwDistance")] private float _maxRange = 4f; // distance at full charge
    [SerializeField] private float _chargeTime = 1f;         // seconds held to reach _maxRange
    [SerializeField] private float _cooldown = 0.3f;           // equal to or longer than the spit animation
    [SerializeField] private float _damageDropDistance = 1.2f;
    [SerializeField] private float _spitDelay = 0f;            // the wind-up already happened in the charge pose: it fires on release
    [SerializeField] private float _spitAnimLength = 0.25f;    // 3 frames at 12 fps (spit, bounce, settle)

    [Header("Sounds")]
    [SerializeField] private AudioSource _audio;        // the slime's (for the spit)
    [SerializeField] private AudioSource _chargeAudio;  // separate, so the charge sound can be cut on release
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
    private InputDevice _aimDevice; // what's being used to aim (mouse or joystick)

    public bool IsCharging => _charging;
    public SlimeChunk ChunkPrefab => _chunkPrefab;

    // 0..1 while held
    public float ChargeProgress => _charging ? Mathf.Clamp01((Time.time - _chargeStart) / _chargeTime) : 0f;

    // Where the chunk would go (ignoring walls) if released now
    public Vector2 AimOffset(Vector2 from) =>
        AimDirection(from) * Mathf.Lerp(_minRange, _maxRange, ChargeProgress);

    // Where the chunk would land if released now (for the preview)
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
        // Charge pose facing where it aims
        if (_charging) _character.HoldCharge(AimDirection(transform.position));
    }

    // Attacking cancels the charge (no health spent): the attack takes priority
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

    // On press: starts charging (if it can throw)
    private void OnChargeStarted(InputAction.CallbackContext ctx)
    {
        if (_chunkPrefab == null || Time.time < _nextThrowTime || _health.IsDead) return;
        if (_health.CurrentHealth <= _cost) return; // with half a heart left it can't throw

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

    // On release: spits at a distance based on how long it was held
    private void OnChargeReleased(InputAction.CallbackContext ctx)
    {
        if (!_charging) return;
        if (ctx.control != null) _aimDevice = ctx.control.device;
        Vector2 offset = AimOffset(transform.position); // same math as the preview
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

    // The chunk comes out when the animation spits it, not when the button is pressed
    private IEnumerator SpitAfterDelay(Vector2 offset)
    {
        yield return new WaitForSeconds(_spitDelay);
        if (_health.IsDead) yield break;

        Vector2 from = transform.position;
        Spawn(from, from + offset);
    }

    // With a mouse: towards the cursor. With a joystick (or no camera): where the slime faces.
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
        CancelCharge(); // a hit cuts the charge (no health spent)
        if (_chunkPrefab == null || _health.IsDead) return;

        // Direction is where the hit pushes you: the chunk flies the other way, towards the enemy
        Vector2 away = info.Direction.sqrMagnitude > 0.0001f ? -info.Direction.normalized : -_character.Facing;
        Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f)) * away;
        Vector2 from = transform.position;
        Spawn(from, from + dir * _damageDropDistance);
    }

    private void Spawn(Vector2 from, Vector2 to)
    {
        var chunk = Instantiate(_chunkPrefab, from, Quaternion.identity);
        chunk.Launch(from, to);
    }
}
