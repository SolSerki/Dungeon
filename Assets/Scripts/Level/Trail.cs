using System.Collections.Generic;
using UnityEngine;

// The trail's goo: while the slime leaves a trail (space bar) it drops stains that never go away.
// Each stain is stored "stuck" to the piece it landed on (local position inside the piece) and
// shows up in EVERY copy of that piece, rotated like the copy. Outside the pieces it goes
// straight into the world. It lives in the scene: dying reloads it and the trail starts empty.
public class Trail : MonoBehaviour
{
    [SerializeField] private Character _slime;
    [SerializeField] private LightTrail _trail;       // if empty, it's looked up on the slime
    [SerializeField] private Sprite[] _sprites;       // SlimeGoo_dot / drop / puddle / splat
    [SerializeField] private Material _material;      // unlit with an HDR tint: glows without light and blooms
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private int _sortingOrder = 0;   // above the floor, below the slime
    [SerializeField] private float _spacing = 0.2f;   // distance between stains
    [SerializeField] private float _scatter = 0.15f;  // scatter around the path
    [SerializeField] private int _maxPerCell = 6;     // going over the same spot doesn't pile up forever
    [Tooltip("The slime's goo particles (Goo Trail): redundant with the trail, they're turned off on start.")]
    [SerializeField] private ParticleSystem _gooToReplace;

    private readonly Dictionary<(string piece, Vector2Int cell), int> _count = new();
    private PieceInstance[] _pieces;
    private Vector2 _lastDrop;
    private bool _wasActive;

    private void Awake()
    {
        if (_trail == null && _slime != null) _trail = _slime.GetComponent<LightTrail>();
        _pieces = FindObjectsByType<PieceInstance>(FindObjectsInactive.Exclude);
        if (_gooToReplace != null) _gooToReplace.gameObject.SetActive(false);
    }

    private void OnEnable() => PortalSeam.Warped += OnWarped;
    private void OnDisable() => PortalSeam.Warped -= OnWarped;

    // When crossing a seam, the last stain "travels" with the slime: no gap is left
    private void OnWarped(PortalSeam seam, Rigidbody2D rb, Vector2 delta)
    {
        if (_slime != null && rb.transform == _slime.transform) _lastDrop += delta;
    }

    private void Update()
    {
        if (_slime == null || _sprites == null || _sprites.Length == 0) return;
        if (_trail == null || !_trail.IsActive)
        {
            _wasActive = false;
            return;
        }

        Vector2 pos = _slime.transform.position;
        if (!_wasActive)
        {
            _lastDrop = pos;
            _wasActive = true;
            Drop(pos);
            return;
        }
        if ((pos - _lastDrop).sqrMagnitude < _spacing * _spacing) return;

        _lastDrop = pos;
        Drop(pos + Random.insideUnitCircle * _scatter);
    }

    private void Drop(Vector2 world)
    {
        var piece = FindPiece(world);
        string id = piece != null ? piece.PieceId : "";            // "" = outside the pieces
        Vector2 local = piece != null ? piece.WorldToLocal(world) : world;

        // Cap per cell: if there are already many stains here, don't add more
        var key = (id, Vector2Int.RoundToInt(local));
        _count.TryGetValue(key, out int n);
        if (n >= _maxPerCell) return;
        _count[key] = n + 1;

        int sprite = Random.Range(0, _sprites.Length);
        int turns = Random.Range(0, 4); // variety, always in 90° turns

        if (piece == null)
        {
            Spawn(world, sprite, turns);
            return;
        }

        // The same stain in every copy of the piece, rotated like that copy
        foreach (var copy in _pieces)
        {
            if (copy.PieceId != id) continue;
            Vector2 at = copy.LocalToWorld(local);
            if (copy.Contains(at)) Spawn(at, sprite, turns + copy.Rotation);
        }
    }

    private void Spawn(Vector2 at, int sprite, int turns)
    {
        var go = new GameObject("Goo");
        go.transform.SetParent(transform);
        // 90° turns only: pixel art rotated at other angles gets distorted
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, -90f * (turns % 4)));

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sprites[sprite];
        sr.color = _color;
        sr.sortingOrder = _sortingOrder;
        if (_material != null) sr.sharedMaterial = _material;
    }

    private PieceInstance FindPiece(Vector2 world)
    {
        foreach (var p in _pieces)
        {
            if (p.Contains(world)) return p;
        }
        return null;
    }
}
