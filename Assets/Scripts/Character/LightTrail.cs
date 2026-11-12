using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Holding the space bar while moving, the slime leaves light crumbs on the floor
// every so often (plus the goo particles). It spends mana while active.
// Crumbs come from a fixed pool that gets recycled: the queue keeps them ordered by age,
// so if they run out the oldest one is reused, which was already fading out.
public class LightTrail : MonoBehaviour
{
    [SerializeField] private Character _character;
    [SerializeField] private Light2D _slimeLight;      // the crumbs copy its color
    [SerializeField] private TrailCrumb _crumbPrefab;
    [SerializeField] private ParticleSystem _goo;      // optional: the goo on the floor
    [SerializeField] private float _spacing = 0.6f;    // distance between crumbs
    [SerializeField] private float _lifetime = 2f;
    [SerializeField] private int _poolSize = 20;       // ~ lifetime * speed / spacing
    [SerializeField] private SfxLoop _trailSound;      // continuous while leaving a trail

    [Header("Mana")]
    [SerializeField] private ManaSystem _mana;
    [SerializeField] private float _manaCost = 25f;        // mana per second while leaving the trail
    [SerializeField] private float _minManaToStart = 15f;  // minimum mana to start

    private readonly Queue<TrailCrumb> _pool = new();
    private InputController _input;
    private Vector2 _lastDrop;
    private bool _held;       // the space bar is held
    private bool _exhausted;  // mana ran out: the key must be released to start again
    private bool _active;
    private bool _wasActive;

    // Actually leaving the trail: key held, moving and has mana
    public bool IsActive => _active;

    private void Awake()
    {
        if (_character == null) _character = GetComponent<Character>();
        if (_mana == null) _mana = GetComponent<ManaSystem>();
        _input = new InputController();

        if (_crumbPrefab == null)
        {
            Debug.LogWarning($"{name}: LightTrail has no crumb prefab assigned.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < _poolSize; i++)
        {
            // No parent: the crumbs must stay on the floor even when the slime moves
            var crumb = Instantiate(_crumbPrefab);
            crumb.gameObject.SetActive(false);
            _pool.Enqueue(crumb);
        }
    }

    private void OnDestroy()
    {
        _input?.Dispose();
        foreach (var crumb in _pool)
        {
            if (crumb != null) Destroy(crumb.gameObject);
        }
    }

    private void OnEnable()
    {
        PortalSeam.Warped += OnWarped;
        _input.Player.Trail.started += OnTrailPressed;
        _input.Player.Trail.canceled += OnTrailReleased;
        _input.Player.Trail.Enable();
    }

    private void OnDisable()
    {
        PortalSeam.Warped -= OnWarped;
        _input.Player.Trail.started -= OnTrailPressed;
        _input.Player.Trail.canceled -= OnTrailReleased;
        _input.Player.Trail.Disable();
        _held = false;
        if (_trailSound != null) _trailSound.Playing = false;
    }

    private void OnTrailPressed(InputAction.CallbackContext ctx) => _held = true;

    private void OnTrailReleased(InputAction.CallbackContext ctx)
    {
        _held = false;
        _exhausted = false;
    }

    // When crossing a maze seam, the crumbs and goo left behind travel with the
    // slime: the corridor on the other side is a copy, so they must show up in the same place.
    private void OnWarped(PortalSeam seam, Rigidbody2D rb, Vector2 delta)
    {
        if (rb.gameObject != gameObject) return;

        _lastDrop += delta;
        foreach (var crumb in _pool)
        {
            if (crumb.gameObject.activeSelf) crumb.transform.position += (Vector3)delta;
        }

        if (_goo == null || !_goo.gameObject.activeInHierarchy) return;
        var particles = new ParticleSystem.Particle[_goo.particleCount];
        int n = _goo.GetParticles(particles);
        for (int i = 0; i < n; i++) particles[i].position += (Vector3)delta;
        _goo.SetParticles(particles, n);
    }

    // Decides whether it leaves a trail this frame and spends the mana
    private void UpdateActive()
    {
        bool moving = _character != null && _character.IsMoving;
        bool active;

        if (!_held || !moving) active = false;
        else if (_active) active = true; // was already leaving a trail: keeps going while there's mana
        else active = !_exhausted && (_mana == null || _mana.CurrentMana >= _minManaToStart);

        if (active && _mana != null)
        {
            _mana.Drain(_manaCost * Time.deltaTime);
            if (_mana.CurrentMana <= 0f)
            {
                active = false;
                _exhausted = true;
            }
        }

        _active = active;
    }

    private void Update()
    {
        UpdateActive();
        if (_trailSound != null) _trailSound.Playing = _active;

        if (_goo != null)
        {
            var emission = _goo.emission;
            emission.enabled = _active;
        }

        if (!_active)
        {
            _wasActive = false;
            return;
        }

        Vector2 pos = transform.position;

        // On start, the first crumb only drops after covering _spacing
        if (!_wasActive)
        {
            _lastDrop = pos;
            _wasActive = true;
            return;
        }

        if ((pos - _lastDrop).sqrMagnitude < _spacing * _spacing) return;

        _lastDrop = pos;
        var next = _pool.Dequeue();   // the first one in the queue is the oldest
        Color color = _slimeLight != null ? _slimeLight.color : Color.white;
        next.Spawn(pos, _lifetime, color);
        _pool.Enqueue(next);
    }
}
