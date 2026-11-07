using UnityEngine;
using UnityEngine.Rendering.Universal;

// Una "miga" de luz del rastro del sprint: nace con la intensidad y el tamaño del prefab
// y se apaga y achica a lo largo de su vida. Cada miga lleva su propia edad, así que las
// más viejas siempre se apagan antes que las nuevas.
[RequireComponent(typeof(Light2D))]
public class TrailCrumb : MonoBehaviour
{
    [SerializeField] private AnimationCurve _fade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField, Range(0f, 1f)] private float _endScale = 0.5f; // tamaño final relativo al inicial

    private Light2D _light;
    private float _baseIntensity;
    private Vector3 _baseScale;
    private float _age;
    private float _lifetime = 1f;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _baseIntensity = _light.intensity;
        _baseScale = transform.localScale;
    }

    public void Spawn(Vector2 position, float lifetime, Color color)
    {
        transform.position = position;
        _light.color = color;
        _age = 0f;
        _lifetime = Mathf.Max(0.01f, lifetime);
        Apply(0f);
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float t = _age / _lifetime;
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            return;
        }
        Apply(t);
    }

    private void Apply(float t)
    {
        _light.intensity = _baseIntensity * _fade.Evaluate(t);
        transform.localScale = _baseScale * Mathf.Lerp(1f, _endScale, t);
    }
}
