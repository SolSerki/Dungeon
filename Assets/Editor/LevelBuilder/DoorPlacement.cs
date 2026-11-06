using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Coloca las puertas sobre la grilla y revisa que tapen el hueco.
// El tamaño en celdas sale del BoxCollider2D de la puerta (2x1 en Puerta.prefab).
public static class DoorPlacement
{
    public const string DoorsRootName = "Puertas";

    public static LockedDoor[] FindDoors() => Object.FindObjectsByType<LockedDoor>(FindObjectsInactive.Include);

    public static GameObject[] FindDoorPrefabs() =>
        AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(go => go.GetComponent<LockedDoor>() != null)
            .ToArray();

    public static Tilemap FindFloor() =>
        Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "Piso");

    // Cuántas celdas ocupa con ese giro: a 90° o 270° se intercambian ancho y alto
    public static Vector2Int Footprint(LockedDoor door, float angle)
    {
        var cell = CellSize();
        var box = door.GetComponent<BoxCollider2D>();
        var size = box != null ? Vector2.Scale(box.size, door.transform.lossyScale) : (Vector2)cell;
        var cells = new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.x) / cell.x)),
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.y) / cell.y)));
        bool sideways = Mathf.RoundToInt(angle / 90f) % 2 != 0;
        return sideways ? new Vector2Int(cells.y, cells.x) : cells;
    }

    public static float SnapAngle(float angle) => Mathf.Repeat(Mathf.Round(angle / 90f) * 90f, 360f);

    public static Vector3 SnapPosition(LockedDoor door, Vector3 position, float angle)
    {
        var footprint = Footprint(door, angle);
        var grid = Object.FindAnyObjectByType<Grid>();
        var cell = CellSize();
        var origin = grid != null ? grid.transform.position : Vector3.zero;
        return new Vector3(
            SnapAxis(position.x - origin.x, cell.x, footprint.x) + origin.x,
            SnapAxis(position.y - origin.y, cell.y, footprint.y) + origin.y,
            position.z);
    }

    // Con un tamaño par el centro cae sobre una línea de la grilla; con uno impar, en el centro de una celda
    private static float SnapAxis(float value, float cell, int cells)
    {
        float offset = cells % 2 == 0 ? 0f : 0.5f;
        return (Mathf.Round(value / cell - offset) + offset) * cell;
    }

    public static bool IsSnapped(LockedDoor door)
    {
        var t = door.transform;
        float angle = t.eulerAngles.z;
        return Mathf.Abs(Mathf.DeltaAngle(angle, SnapAngle(angle))) < 0.01f &&
               Vector3.Distance(t.position, SnapPosition(door, t.position, angle)) < 0.001f;
    }

    public static bool Snap(LockedDoor door)
    {
        if (IsSnapped(door)) return false;

        var t = door.transform;
        float angle = SnapAngle(t.eulerAngles.z);
        Undo.RecordObject(t, "Ajustar puerta");
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.position = SnapPosition(door, t.position, angle);
        return true;
    }

    // Deja fija la celda de abajo a la izquierda: a 180° tapa las mismas celdas y nunca se va corriendo
    public static void Rotate(LockedDoor door, float degrees)
    {
        var t = door.transform;
        var cell = CellSize();
        var before = Footprint(door, t.eulerAngles.z);
        var corner = t.position - new Vector3(before.x * cell.x, before.y * cell.y) * 0.5f;

        float angle = SnapAngle(t.eulerAngles.z + degrees);
        var after = Footprint(door, angle);
        Undo.RecordObject(t, "Girar puerta");
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.position = SnapPosition(door, corner + new Vector3(after.x * cell.x, after.y * cell.y) * 0.5f, angle);
    }

    public static GameObject Create(GameObject prefab, Vector3 position, Scene scene)
    {
        var root = EnemySpawnBuilder.GetOrCreateRoot(DoorsRootName, scene);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(go, "Nueva puerta");
        go.transform.SetParent(root.transform);
        go.name = GameObjectUtility.GetUniqueNameForSibling(root.transform, prefab.name);
        go.transform.rotation = Quaternion.identity;

        var door = go.GetComponent<LockedDoor>();
        position.z = 0f;
        go.transform.position = SnapPosition(door, position, 0f);
        return go;
    }

    public static void SnapAll(LevelBuildReport report)
    {
        int moved = FindDoors().Count(Snap);
        if (moved > 0) report.Info($"Ajusté {moved} puertas a la grilla.");
    }

    public static void Validate(Tilemap floor, LevelBuildReport report)
    {
        foreach (var door in FindDoors())
        {
            var problem = FindProblem(door, floor);
            if (problem != null) report.Warn($"{door.name}: {problem}", door);
        }
    }

    // Celdas que tapa la puerta, en coordenadas del tilemap
    public static RectInt FootprintCells(LockedDoor door, Tilemap floor)
    {
        var size = Footprint(door, door.transform.eulerAngles.z);
        var cell = CellSize();
        var corner = door.transform.position - new Vector3(size.x * cell.x, size.y * cell.y) * 0.5f;
        var min = floor.WorldToCell(corner + new Vector3(cell.x, cell.y) * 0.5f);
        return new RectInt(min.x, min.y, size.x, size.y);
    }

    // null si la puerta tapa bien el hueco
    public static string FindProblem(LockedDoor door, Tilemap floor)
    {
        if (floor == null) return null;

        var r = FootprintCells(door, floor);
        if (!AnyFloor(floor, r, all: true)) return "hay celdas sin piso debajo. Pintá piso en todo el hueco de la puerta.";
        if (r.width == r.height) return null;

        // puntas: la celda que sigue en cada extremo del lado largo; costados: de un lado y del otro del lado corto
        bool horizontal = r.width > r.height;
        var endA = horizontal ? new RectInt(r.xMin - 1, r.yMin, 1, r.height) : new RectInt(r.xMin, r.yMin - 1, r.width, 1);
        var endB = horizontal ? new RectInt(r.xMax, r.yMin, 1, r.height) : new RectInt(r.xMin, r.yMax, r.width, 1);
        var sideA = horizontal ? new RectInt(r.xMin, r.yMin - 1, r.width, 1) : new RectInt(r.xMin - 1, r.yMin, 1, r.height);
        var sideB = horizontal ? new RectInt(r.xMin, r.yMax, r.width, 1) : new RectInt(r.xMax, r.yMin, 1, r.height);

        if (!AnyFloor(floor, endA) && !AnyFloor(floor, endB)) return null;
        if (!AnyFloor(floor, sideA) && !AnyFloor(floor, sideB)) return "parece girada al revés: girala 90° (Shift+R).";
        return "hay piso en alguna punta de la puerta: se puede pasar por al lado sin abrirla.";
    }

    private static bool AnyFloor(Tilemap floor, RectInt cells, bool all = false)
    {
        foreach (var c in cells.allPositionsWithin)
        {
            bool has = floor.HasTile((Vector3Int)c);
            if (all && !has) return false;
            if (!all && has) return true;
        }
        return all;
    }

    private static Vector3 CellSize()
    {
        var grid = Object.FindAnyObjectByType<Grid>();
        return grid != null ? grid.cellSize : Vector3.one;
    }
}
