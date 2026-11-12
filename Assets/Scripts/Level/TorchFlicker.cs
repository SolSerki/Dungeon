using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class TorchFlicker : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _intensityVariation = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _radiusVariation = 0.05f;
    [SerializeField] private float _speed = 3f;

    private Light2D _light;
    private float _baseIntensity;
    private float _baseRadius;
    private float _seed;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _baseIntensity = _light.intensity;
        _baseRadius = _light.pointLightOuterRadius;
        // A different seed per torch so they don't flicker in sync
        _seed = Random.value * 100f;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(_seed, Time.time * _speed) * 2f - 1f;
        _light.intensity = _baseIntensity * (1f + noise * _intensityVariation);
        _light.pointLightOuterRadius = _baseRadius * (1f + noise * _radiusVariation);
    }

    private void OnDisable()
    {
        if (_light == null) return;
        _light.intensity = _baseIntensity;
        _light.pointLightOuterRadius = _baseRadius;
    }
}
