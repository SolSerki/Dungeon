using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Pedazo de slime: vuela en arco, cae, ilumina el piso y se apaga de a poco.
// Si el slime lo pisa antes de que se apague, lo reabsorbe y se cura.
public class SlimeChunk : Pickup
{
    [SerializeField] private Transform _body;              // hijo con el sprite: sube y baja en el arco
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private SpriteRenderer _shadow;       // queda siempre en el piso
    [SerializeField] private Light2D _light;
    [SerializeField] private LayerMask _obstacleMask;      // paredes: el pedazo no las atraviesa
    [SerializeField] private float _flightTime = 0.4f;
    [SerializeField] private float _arcHeightPerUnit = 0.25f; // la altura del arco crece con la distancia
    [SerializeField] private float _minArcHeight = 0.3f;
    [SerializeField] private float _lifetime = 8f;
    [SerializeField] private AnimationCurve _fade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float _healAmount = 1f;       // medio corazón
    [SerializeField] private float _pickupDelay = 0.5f;    // para no reabsorberlo apenas cae

    [Header("Sonidos")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _landSounds;      // al chocar contra el piso
    [SerializeField, Range(0f, 1f)] private float _landVolume = 0.5f;

    // Pedazos que existen ahora: los enemigos los buscan acá en vez de usar FindObjectsByType cada frame
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
    public bool Lures { get; private set; } = true; // si atrae a los enemigos (distracción)

    private void Awake()
    {
        _baseIntensity = _light.intensity;
        _shadowAlpha = _shadow.color.a;
    }

    private void OnEnable() => _active.Add(this);
    private void OnDisable() => _active.Remove(this);

    // Altura del arco para un tiro de esta distancia (la usa también la vista previa)
    public float ArcHeightFor(float distance) => Mathf.Max(_minArcHeight, distance * _arcHeightPerUnit);

    // Dónde cae de verdad: si hay una pared en el camino, justo antes (la usa también la vista previa)
    public Vector2 ResolveTarget(Vector2 from, Vector2 target)
    {
        Vector2 delta = target - from;
        if (delta.sqrMagnitude <= 0.0001f) return target;

        var hit = Physics2D.Raycast(from, delta.normalized, delta.magnitude, _obstacleMask);
        return hit.collider != null ? hit.point - delta.normalized * 0.2f : target;
    }

    // Punto del arco en t (0..1): posición en el piso + altura de la parábola
    public static Vector2 ArcPoint(Vector2 from, Vector2 to, float t, float arcHeight) =>
        Vector2.Lerp(from, to, t) + Vector2.up * HeightAt(t, arcHeight);

    // Parábola: 0 al salir, arcHeight a mitad de camino, 0 al caer
    private static float HeightAt(float t, float arcHeight) => 4f * arcHeight * t * (1f - t);

    public void Launch(Vector2 from, Vector2 target, bool lures = true)
    {
        Lures = lures;
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
        // En el aire o recién caído no se agarra: hay que salir y volver a pisarlo
        if (!_landed || _age < _pickupDelay) return false;

        var health = character.GetComponent<HealthSystem>();
        if (health.CurrentHealth >= health.MaxHealth) return false; // con vida llena se queda, como la poción

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
