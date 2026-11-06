using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Qué tile usa el Level Builder para cada caso. Los arrays se alternan según la posición.
[CreateAssetMenu(menuName = "Dungeon/Level Tile Rules")]
public class LevelTileRules : ScriptableObject
{
    [Header("Piso: bordes")]
    public TileBase floorTopLeft;
    public TileBase[] floorTop;
    public TileBase floorTopRight;
    public TileBase floorLeft;
    public TileBase floorRight;
    public TileBase floorBottomLeft;
    public TileBase[] floorBottom;
    public TileBase floorBottomRight;

    [Header("Piso: interior")]
    public TileBase floorInterior; // reemplaza a un borde que quedó adentro al agrandar una sala

    [Header("Paredes")]
    public TileBase wallTopLeftEnd;
    public TileBase[] wallTop;
    public TileBase wallTopRightEnd;
    public TileBase wallBottomLeftEnd;
    public TileBase[] wallBottom;
    public TileBase wallBottomRightEnd;
    public TileBase[] wallLeft;  // pared con piso a su derecha
    public TileBase[] wallRight; // pared con piso a su izquierda

    [Header("Esquinas y uniones")]
    public TileBase cornerTopLeft;
    public TileBase cornerTopRight;
    public TileBase cornerBottomLeft;
    public TileBase cornerBottomRight;
    public TileBase bottomJoinFloorRight; // pared de abajo con un pasillo a la derecha
    public TileBase bottomJoinFloorLeft;  // pared de abajo con un pasillo a la izquierda

    [Header("Relleno")]
    public TileBase voidTile;

    public bool IsFloorEdge(TileBase tile) =>
        tile != null &&
        (tile == floorTopLeft || tile == floorTopRight || tile == floorLeft || tile == floorRight ||
         tile == floorBottomLeft || tile == floorBottomRight ||
         floorTop.Contains(tile) || floorBottom.Contains(tile));

    public static TileBase Pick(TileBase[] tiles, int index) =>
        tiles == null || tiles.Length == 0 ? null : tiles[((index % tiles.Length) + tiles.Length) % tiles.Length];

    // Valores por defecto para Dungeon_Tileset (índice = fila * 10 + columna)
    public void FillDungeonTilesetDefaults()
    {
        floorTopLeft = T(11); floorTop = Ts(12, 13); floorTopRight = T(14);
        floorLeft = T(21); floorRight = T(24);
        floorBottomLeft = T(31); floorBottom = Ts(32, 33); floorBottomRight = T(34);
        floorInterior = T(18);

        wallTopLeftEnd = T(1); wallTop = Ts(2, 3); wallTopRightEnd = T(4);
        wallBottomLeftEnd = T(41); wallBottom = Ts(42, 43); wallBottomRightEnd = T(44);
        wallLeft = Ts(10, 20, 30); wallRight = Ts(15, 25, 35);

        cornerTopLeft = T(0); cornerTopRight = T(5);
        cornerBottomLeft = T(40); cornerBottomRight = T(45);
        bottomJoinFloorRight = T(55); bottomJoinFloorLeft = T(54);

        voidTile = T(78);
    }

    private static TileBase T(int index) =>
        AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Art/Tiles/Dungeon_Tileset_{index}.asset");

    private static TileBase[] Ts(params int[] indices) => indices.Select(T).ToArray();
}
