using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Slime chunk: flies in an arc, lands, lights the floor and fades out gradually.
// If the slime steps on it before it fades, it reabsorbs it and heals.
public class SlimeChunk : Pickup
{
    [SerializeField] private Transform _body;              // child with the sprite: rises and falls along the arc
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private SpriteRenderer _shadow;       // always stays on the floor
    [SerializeField] private Light2D _light;
    [SerializeField] private LayerMask _obstacleMask;      // walls: the chunk doesn't pass through them
    [SerializeField] private float _flightTime = 0.4f;
    [SerializeField] private float _arcHeightPerUnit = 0.25f; // the arc height grows with distance
    [SerializeField] private float _minArcHeight = 0.3f;
    [SerializeField] private float _lifetime = 8f;
    [SerializeField] private AnimationCurve _fade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float _healAmount = 1f;       // half a heart
    [SerializeField] private float _pickupDelay = 0.5f;    // so it isn't reabsorbed right as it lands

    [Header("Sounds")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _landSounds;      // when it hits the floor
    [SerializeField, Range(0f, 1f)] private float _landVolume = 0.5f;

    // Chunks that currently exist: enemies look them up here instead of calling FindObjectsByType every frame
    private static readonly List<SlimeChunk> _active = new();
    public static IReadOnlyList<SlimeChunk> Active => _active;

    private Vector2 _from, _to;
    private float _arcHeight;
    private float _flightT;
    private float _age;
    private bool _landed;
    private float _baseIntensity;
    private float _shadowAlpha;

    public bool IsLanded => _landed;

    // A chunk distracts a single enemy: the first one that heads for it "claims" it
    // and the others ignore it (they follow the slime if they see it).
    public EnemyAI Claimant { get; private set; }
    public bool IsClaimedByOther(EnemyAI enemy) => Claimant != null && Claimant != enemy;

    public bool TryClaim(EnemyAI enemy)
    {
        if (IsClaimedByOther(enemy)) return false;
        Claimant = enemy;
        return true;
    }

    public void Release(EnemyAI enemy)
    {
        if (Claimant == enemy) Claimant = null;
    }

    private void Awake()
    {
        _baseIntensity = _light.intensity;
        _shadowAlpha = _shadow.color.a;
    }

    private void OnEnable() => _active.Add(this);
    private void OnDisable() => _active.Remove(this);

    // Arc height for a throw of this distance (also used by the preview)
    public float ArcHeightFor(float distance) => Mathf.Max(_minArcHeight, distance * _arcHeightPerUnit);

    // Where it actually lands: if there's a wall in the way, just before it (also used by the preview)
    public Vector2 ResolveTarget(Vector2 from, Vector2 target)
    {
        Vector2 delta = target - from;
        if (delta.sqrMagnitude <= 0.0001f) return target;

        var hit = Physics2D.Raycast(from, delta.normalized, delta.magnitude, _obstacleMask);
        return hit.collider != null ? hit.point - delta.normalized * 0.2f : target;
    }

    // Arc point at t (0..1): floor position + parabola height
    public static Vector2 ArcPoint(Vector2 from, Vector2 to, float t, float arcHeight) =>
        Vector2.Lerp(from, to, t) + Vector2.up * HeightAt(t, arcHeight);

    // Parabola: 0 at launch, arcHeight halfway, 0 on landing
    private static float HeightAt(float t, float arcHeight) => 4f * arcHeight * t * (1f - t);

    public void Launch(Vector2 from, Vector2 target)
    {
        _from = from;
        _to = ResolveTarget(from, target);
        _arcHeight = ArcHeightFor(Vector2.Distance(_from, _to));
        _flightT = 0f;
        _landed = false;
        transform.position = from;
    }

    private void Update()
    {
        if (!_landed) Fly();
        else Fade();
    }

    private void Fly()
    {
        _flightT = Mathf.Min(_flightT + Time.deltaTime / _flightTime, 1f);
        transform.position = Vector2.Lerp(_from, _to, _flightT);

        _body.localPosition = new Vector3(0f, HeightAt(_flightT, _arcHeight), 0f);

        if (_flightT >= 1f)
        {
            _landed = true;
            Sfx.PlayRandom(_audio, _landSounds, _landVolume);
        }
    }

    private void Fade()
    {
        _age += Time.deltaTime;
        float t = _age / _lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float k = _fade.Evaluate(t);
        _light.intensity = _baseIntensity * k;
        SetAlpha(_bodyRenderer, k);
        SetAlpha(_shadow, _shadowAlpha * k);
    }

    protected override bool Collect(Character character)
    {
        // In the air or just landed it can't be picked up: step off and back on it
        if (!_landed || _age < _pickupDelay) return false;

        var health = character.GetComponent<HealthSystem>();
        if (health.CurrentHealth >= health.MaxHealth) return false; // at full health it stays, like the potion

        health.Heal(_healAmount);
        return true;
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        var color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
