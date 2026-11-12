using UnityEngine;

// "Room X" folder for a room's EnemySpawns. The Level Builder creates it on every Build (F6): the
// level's rooms go inside "EnemySpawns" and a pocket's rooms inside the pocket (so they move
// with it). The object sits at the room's center: select it and press F in the scene to frame it.
public class RoomGroup : MonoBehaviour
{
    [SerializeField] private int _number;
    [SerializeField] private Vector2Int _size = Vector2Int.one;

    public int Number => _number;

    // World cells covered by the room (follows the object if the pocket moves)
    public RectInt Rect
    {
        get
        {
            var p = transform.position;
            return new RectInt(Mathf.RoundToInt(p.x - _size.x * 0.5f), Mathf.RoundToInt(p.y - _size.y * 0.5f), _size.x, _size.y);
        }
    }

    public void Setup(int number, Vector2Int size)
    {
        _number = number;
        _size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }

    private void OnDrawGizmosSelected()
    {
        var r = Rect;
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(r.center.x, r.center.y), new Vector3(r.width, r.height));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(new Vector3(r.xMin + 0.2f, r.yMax - 0.2f), name);
#endif
    }
}
