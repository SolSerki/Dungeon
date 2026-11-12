using UnityEngine;

// Maze tunnels: loose mouths (PocketMouths that aren't children of a Pocket) joined in pairs.
// They connect two corridors anywhere in the level without building a pocket. Since crossing doesn't rotate the
// world, the two mouths must face opposite directions (one right and the other
// left, or one up and the other down).
// Build (F6) paints the other corridor's continuation behind each mouth and creates the seams. What it
// paints is recorded here so it can be erased before the next Build.
public class MazeConnections : MonoBehaviour
{
    [Tooltip("Cells painted behind each mouth. Must exceed half a screen (the camera sees ~10.8 cells to each side).")]
    [SerializeField, Min(4)] private int _overlap = 13;
    [SerializeField, HideInInspector] private Vector2Int[] _generatedFloor = new Vector2Int[0];
    [SerializeField, HideInInspector] private Vector2Int[] _generatedProps = new Vector2Int[0];

    public int Overlap => _overlap;

    public Vector2Int[] GeneratedFloor
    {
        get => _generatedFloor;
        set => _generatedFloor = value ?? new Vector2Int[0];
    }

    public Vector2Int[] GeneratedProps
    {
        get => _generatedProps;
        set => _generatedProps = value ?? new Vector2Int[0];
    }
}
