using UnityEngine;
using UnityEngine.Tilemaps;

// A maze pocket.
//
// Modular (the usual kind): the object has its own Grid and child tilemaps ("Pocket Floor",
// "Pocket Walls", "Pocket Props"). What you paint in its Floor is sheet 0. Moving the
// object moves the whole pocket, and it can be saved as a prefab to reuse it.
//
// Legacy (no tilemaps of its own): sheet 0 is the level floor inside a rectangle
// of _size cells centered on the object. The Level Builder can convert it to modular.
//
// In both cases: mouths (PocketMouth, children of this object) at the ends of corridors, joined
// in pairs, and Build (F6) generates the rotated sheets, overlaps and seams in "Pockets (generated)".
[ExecuteAlways]
public class Pocket : MonoBehaviour
{
    [Tooltip("Size in cells, centered on this object. In a modular pocket it's only a painting guide (dotted line); " +
             "in a legacy one it's the pocket's area.")]
    [SerializeField] private Vector2Int _size = new(30, 30);
    [Tooltip("Cells copied behind each mouth. Must exceed half a screen (the camera sees ~10.8 cells to each side).")]
    [SerializeField, Min(4)] private int _overlap = 13;
    [Tooltip("Empty cells between the generated sheets and the rest of the level (more than a screen, so one can't be seen from the other).")]
    [SerializeField, Min(4)] private int _sheetGap = 24;
    [Header("Modular pocket")]
    [SerializeField] private Tilemap _floor;
    [SerializeField] private Tilemap _walls;
    [SerializeField] private Tilemap _props;
    [SerializeField, HideInInspector] private Vector2Int[] _generatedCells = new Vector2Int[0]; // legacy pockets only

    public int Overlap => _overlap;
    public int SheetGap => _sheetGap;
    public string PieceId => "Pocket " + name;

    public bool IsModular => _floor != null;
    public Tilemap Floor => _floor;
    public Tilemap Walls => _walls;
    public Tilemap Props => _props;

    // World cell that matches cell (0,0) of the pocket's tilemaps
    public Vector2Int CellOffset => Vector2Int.RoundToInt(transform.position);

    public void SetTilemaps(Tilemap floor, Tilemap walls, Tilemap props)
    {
        _floor = floor;
        _walls = walls;
        _props = props;
    }

    // For legacy pockets: floor painted by Build, so it can be erased before the next one
    public Vector2Int[] GeneratedCells
    {
        get => _generatedCells;
        set => _generatedCells = value ?? new Vector2Int[0];
    }

    public Vector2Int GuideSize
    {
        get => _size;
        set => _size = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
    }

    // The _size rectangle centered on the object (the same math the placement preview uses)
    public static RectInt GuideRectAt(Vector3 center, Vector2Int size) => new(
        Mathf.RoundToInt(center.x - size.x * 0.5f), Mathf.RoundToInt(center.y - size.y * 0.5f),
        Mathf.Max(1, size.x), Mathf.Max(1, size.y));

    public RectInt GuideRect => GuideRectAt(transform.position, _size);

    // World cells covered by sheet 0
    public RectInt Area
    {
        get
        {
            if (!IsModular) return GuideRect;
            _floor.CompressBounds();
            var b = _floor.cellBounds;
            if (b.size.x <= 0 || b.size.y <= 0) return GuideRect; // not painted yet: the guide
            var offset = CellOffset;
            return new RectInt(b.xMin + offset.x, b.yMin + offset.y, b.size.x, b.size.y);
        }
    }

    // Is this world cell sheet 0 floor?
    public bool HasFloor(Vector2Int worldCell, Tilemap levelFloor) =>
        IsModular ? _floor.HasTile((Vector3Int)(worldCell - CellOffset))
                  : Area.Contains(worldCell) && levelFloor != null && levelFloor.HasTile((Vector3Int)worldCell);

#if UNITY_EDITOR
    // A modular pocket moves in whole cells and never rotates (3/4 art can't be rotated)
    private void Update()
    {
        if (Application.isPlaying || !IsModular) return;
        var p = transform.position;
        var snapped = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f);
        if (p != snapped) transform.position = snapped;
        if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
        if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
    }
#endif

    private void OnDrawGizmos()
    {
        var a = Area;
        Gizmos.color = new Color(1f, 0.4f, 1f, IsModular ? 0.35f : 0.6f);
        Gizmos.DrawWireCube(new Vector3(a.center.x, a.center.y), new Vector3(a.width, a.height));
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        string label = IsModular ? $"{name} (modular: paint in \"{_floor.name}\")" : $"{name} (sheet 0: edit it here)";
        UnityEditor.Handles.Label(new Vector3(a.xMin, a.yMax + 0.8f), label);

        // Modular: the dotted size guide, to know how far to paint
        if (IsModular)
        {
            var g = GuideRect;
            UnityEditor.Handles.color = new Color(1f, 0.4f, 1f, 0.5f);
            var p0 = new Vector3(g.xMin, g.yMin);
            var p1 = new Vector3(g.xMin, g.yMax);
            var p2 = new Vector3(g.xMax, g.yMax);
            var p3 = new Vector3(g.xMax, g.yMin);
            UnityEditor.Handles.DrawDottedLines(new[] { p0, p1, p1, p2, p2, p3, p3, p0 }, 4f);
        }
#endif
    }
}
