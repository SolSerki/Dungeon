using UnityEngine;
using UnityEngine.Rendering.Universal;

// Dims the slime's light while it walks through this zone (e.g. into a building).
// The light fades with how far the slime has gone along the zone's local up axis:
// _fade(0) at the bottom edge, _fade(1) at the top edge. Walking back restores it.
[RequireComponent(typeof(BoxCollider2D))]
public class LightDimZone : MonoBehaviour
{
    // Multiplier of the light's intensity. It drops fast at first because
    // a bright light still looks almost full at half intensity.
    [SerializeField] private AnimationCurve _fade = new AnimationCurve(
        new Keyframe(0f, 1f, -2f, -2f),
        new Keyframe(1f, 0f, 0f, 0f));

    private BoxCollider2D _zone;
    private Transform _character;
    private Light2D _light;
    private float _baseIntensity;
    private int _contacts; // the slime has two colliders, so Enter/Exit fire twice

    private void Awake()
    {
        _zone = GetComponent<BoxCollider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();
        if (character == null) return;
        if (_contacts++ > 0) return;

        _character = character.transform;
        _light = character.GetComponentInChildren<Light2D>();
        if (_light != null) _baseIntensity = _light.intensity;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Character>() == null) return;
        if (--_contacts > 0) return;
        Restore();
    }

    private void Update()
    {
        if (_light == null) return;
        _light.intensity = _baseIntensity * _fade.Evaluate(Progress());
    }

    // 0 at the zone's bottom edge, 1 at its top edge
    private float Progress()
    {
        Vector2 local = (Vector2)transform.InverseTransformPoint(_character.position) - _zone.offset;
        float half = _zone.size.y / 2f;
        return Mathf.InverseLerp(-half, half, local.y);
    }

    private void Restore()
    {
        if (_light != null) _light.intensity = _baseIntensity;
        _light = null;
        _character = null;
        _contacts = 0;
    }

    private void OnDisable()
    {
        Restore();
    }
}
