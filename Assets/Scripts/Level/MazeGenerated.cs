using UnityEngine;

// Root for what the Level Builder generates for pockets: rotated sheets, overlaps, pieces and seams.
// It lives outside the pockets so a modular pocket can be saved as a prefab without taking
// anything generated with it. It's cleared and rebuilt on every Build.
public class MazeGenerated : MonoBehaviour
{
    [SerializeField, HideInInspector] private Vector2Int[] _floorCells = new Vector2Int[0];

    // Floor painted by Build in the level's Floor
    public Vector2Int[] FloorCells
    {
        get => _floorCells;
        set => _floorCells = value ?? new Vector2Int[0];
    }
}
