using System.Collections.Generic;
using UnityEngine;

// Marks a group of cells as ONE COPY of a logical maze piece.
// All copies of the same piece share _pieceId: they differ in where they are (_origin) and
// how much they're rotated (_rotation). Whatever is stored "in the piece" (the trail) shows up in
// all its copies, each one rotated accordingly.
//
// The cells are the _cells rectangle, unless the copy has an exact cell list
// (the ones the Level Builder generates for pockets), which takes precedence over the rectangle.
//
// Assumes the Grid at (0,0) with 1-unit cells, as in every level.
public class PieceInstance : MonoBehaviour
{
    [SerializeField] private string _pieceId = "C1";
    [Tooltip("Physical cells covered by this copy (may be only part of the piece).")]
    [SerializeField] private RectInt _cells = new(0, 0, 2, 2);
    [Tooltip("Physical cell that matches the piece's cell (0,0).")]
    [SerializeField] private Vector2Int _origin;
    [Tooltip("Clockwise 90° turns relative to the original piece.")]
    [SerializeField, Range(0, 3)] private int _rotation;
    [SerializeField] private Color _gizmoColor = new(0f, 1f, 1f, 0.8f);
    [SerializeField, HideInInspector] private Vector2Int[] _cellList = new Vector2Int[0];

    private HashSet<Vector2Int> _cellSet; // built the first time it's queried

    public string PieceId => _pieceId;
    public int Rotation => _rotation;
    public Vector2Int Origin => _origin;
    public RectInt Rect => _cells;
    public bool HasCellList => _cellList != null && _cellList.Length > 0;
    public IReadOnlyList<Vector2Int> CellList => _cellList;

    // Rotates around the center of the origin cell
    private Vector2 Pivot => (Vector2)_origin + new Vector2(0.5f, 0.5f);

    public bool Contains(Vector2 world) => Contains(Vector2Int.FloorToInt(world));

    public bool Contains(Vector2Int cell)
    {
        if (!HasCellList) return _cells.Contains(cell);
        _cellSet ??= new HashSet<Vector2Int>(_cellList);
        return _cellSet.Contains(cell);
    }

    public Vector2 WorldToLocal(Vector2 world) => RotateCCW(world - Pivot, _rotation);
    public Vector2 LocalToWorld(Vector2 local) => Pivot + RotateCW(local, _rotation);

    // Used by the Level Builder when generating pockets
    public void Setup(string pieceId, Vector2Int origin, int rotation, Vector2Int[] cells, Color gizmoColor)
    {
        _pieceId = pieceId;
        _origin = origin;
        _rotation = ((rotation % 4) + 4) % 4;
        _cellList = cells;
        _cellSet = null;
        _gizmoColor = gizmoColor;

        if (cells.Length == 0) return;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }
        _cells = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // 90° clockwise: (x, y) -> (y, -x)
    public static Vector2 RotateCW(Vector2 v, int times)
    {
        for (int i = 0; i < times; i++) v = new Vector2(v.y, -v.x);
        return v;
    }

    public static Vector2 RotateCCW(Vector2 v, int times)
    {
        for (int i = 0; i < times; i++) v = new Vector2(-v.y, v.x);
        return v;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _gizmoColor;
        Gizmos.DrawWireCube(new Vector3(_cells.center.x, _cells.center.y), new Vector3(_cells.width, _cells.height));

        // Arrow: where the piece's "north" points in this copy
        Vector3 from = Pivot;
        Vector3 north = RotateCW(Vector2.up, _rotation);
        Gizmos.DrawLine(from, from + north * 1.5f);
        Gizmos.DrawWireSphere(from, 0.15f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = _gizmoColor;
        UnityEditor.Handles.Label(new Vector3(_cells.xMin, _cells.yMax + 0.6f), $"{name}  ({_rotation * 90}°)");
#endif
    }
}
