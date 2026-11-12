using System.Collections.Generic;
using UnityEngine;

// While the throw charges, draws the arc the chunk will follow (dots marching
// towards the target) and a marker on the floor where it will land. Uses the same math as the real flight.
public class TrajectoryPreview : MonoBehaviour
{
    [SerializeField] private SlimeThrower _thrower;
    [SerializeField] private Sprite _dotSprite;
    [SerializeField] private Sprite _markerSprite;
    [SerializeField] private Material _material;               // unlit: visible even in the dark
    [SerializeField] private string _sortingLayer = "Default";
    [SerializeField] private int _sortingOrder = 0;            // between the floor (-1) and the slime (1)
    [SerializeField] private int _maxDots = 14;
    [SerializeField] private float _dotSpacing = 0.35f;        // spacing between dots on the floor
    [SerializeField] private float _marchSpeed = 1.5f;         // how far the dots march per second (in dots)
    [SerializeField, Range(0f, 1f)] private float _startT = 0.12f; // don't draw on top of the slime
    [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.35f; // brightness at 0 charge
    [SerializeField] private float _markerPulseSpeed = 6f;
    [SerializeField] private float _popScale = 1.5f;           // marker "pop" on reaching full charge
    [SerializeField] private float _popTime = 0.15f;

    private readonly List<SpriteRenderer> _dots = new();
    private SpriteRenderer _marker;
    private float _phase;
    private bool _wasFull;
    private float _popStart = -10f;

    private void Awake()
    {
        if (_thrower == null) _thrower = GetComponentInParent<SlimeThrower>();

        for (int i = 0; i < _maxDots; i++)
            _dots.Add(CreateRenderer("Dot " + i, _dotSprite));
        _marker = CreateRenderer("Landing Marker", _markerSprite);
        SetVisible(false);
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (_material != null) sr.sharedMaterial = _material;
        sr.sortingLayerName = _sortingLayer;
        sr.sortingOrder = _sortingOrder;
        return sr;
    }

    private void LateUpdate()
    {
        if (_thrower == null || !_thrower.IsCharging || _thrower.ChunkPrefab == null)
        {
            SetVisible(false);
            _wasFull = false;
            return;
        }

        Vector2 from = _thrower.transform.position;
        Vector2 to = _thrower.PreviewTarget(from);
        float distance = Vector2.Distance(from, to);
        float arcHeight = _thrower.ChunkPrefab.ArcHeightFor(distance);
        float charge = _thrower.ChargeProgress;

        // More charge = brighter dots
        var color = Color.white;
        color.a = Mathf.Lerp(_minAlpha, 1f, charge);

        // Dots marching from the slime to the target: each one shifts its t by the phase
        int count = Mathf.Clamp(Mathf.CeilToInt(distance / _dotSpacing), 1, _maxDots);
        _phase = (_phase + Time.deltaTime * _marchSpeed) % 1f;
        for (int i = 0; i < _dots.Count; i++)
        {
            var dot = _dots[i];
            if (i >= count)
            {
                dot.enabled = false;
                continue;
            }

            float t = Mathf.Lerp(_startT, 1f, (i + _phase) / count);
            dot.transform.position = SlimeChunk.ArcPoint(from, to, t, arcHeight);
            dot.color = color;
            dot.enabled = true;
        }

        // Floor marker: pulses gently and "pops" when the charge is full
        if (charge >= 1f && !_wasFull) _popStart = Time.time;
        _wasFull = charge >= 1f;

        float pop = 1f - Mathf.Clamp01((Time.time - _popStart) / _popTime);
        _marker.transform.position = to;
        _marker.transform.localScale = Vector3.one * Mathf.Lerp(1f, _popScale, pop);
        var markerColor = color;
        markerColor.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * _markerPulseSpeed);
        _marker.color = markerColor;
        _marker.enabled = true;
    }

    private void SetVisible(bool visible)
    {
        foreach (var dot in _dots) dot.enabled = visible;
        if (_marker != null) _marker.enabled = visible;
    }
}
