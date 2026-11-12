using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Places doors on the grid and checks that they seal the gap.
// The size in cells comes from the door's BoxCollider2D (2x1 in Door.prefab, 1x2 in SideDoor.prefab).
public static class DoorPlacement
{
    public const string DoorsRootName = "Doors";

    public static LockedDoor[] FindDoors() => Object.FindObjectsByType<LockedDoor>(FindObjectsInactive.Include);

    public static GameObject[] FindDoorPrefabs() =>
        AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(go => go.GetComponent<LockedDoor>() != null)
            .ToArray();

    public static Tilemap FindFloor() =>
        Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "Floor");

    // How many cells it covers with that rotation: at 90° or 270° width and height swap
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

    // With an even size the center falls on a grid line; with an odd one, on a cell center
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
        Undo.RecordObject(t, "Snap door");
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.position = SnapPosition(door, t.position, angle);
        return true;
    }

    // The door version for the other orientation: the prefab with width and height swapped
    // (Door 2x1 <-> SideDoor 1x2). null if there's none.
    public static GameObject FindCounterpart(LockedDoor door)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(door);
        if (source == null) return null;

        var size = Footprint(source, 0f);
        return FindDoorPrefabs().FirstOrDefault(p =>
        {
            var other = p.GetComponent<LockedDoor>();
            return other != source && Footprint(other, 0f) == new Vector2Int(size.y, size.x);
        });
    }

    // Keeps the bottom-left cell fixed: at 180° it covers the same cells and never drifts.
    // If the door has a version for the other orientation, a quarter turn swaps to it instead of rotating the sprite.
    public static void Rotate(LockedDoor door, float degrees)
    {
        bool quarterTurn = Mathf.RoundToInt(degrees / 90f) % 2 != 0;
        var counterpart = quarterTurn ? FindCounterpart(door) : null;
        if (counterpart != null)
        {
            var anchor = Corner(door);
            door = Swap(door, counterpart);
            var size = Footprint(door, 0f);
            var cellSize = CellSize();
            door.transform.position = SnapPosition(door, anchor + new Vector3(size.x * cellSize.x, size.y * cellSize.y) * 0.5f, 0f);
            return;
        }

        var t = door.transform;
        var cell = CellSize();
        var before = Footprint(door, t.eulerAngles.z);
        var corner = t.position - new Vector3(before.x * cell.x, before.y * cell.y) * 0.5f;

        float angle = SnapAngle(t.eulerAngles.z + degrees);
        var after = Footprint(door, angle);
        Undo.RecordObject(t, "Rotate door");
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.position = SnapPosition(door, corner + new Vector3(after.x * cell.x, after.y * cell.y) * 0.5f, angle);
    }

    private static Vector3 Corner(LockedDoor door)
    {
        var size = Footprint(door, door.transform.eulerAngles.z);
        var cell = CellSize();
        return door.transform.position - new Vector3(size.x * cell.x, size.y * cell.y) * 0.5f;
    }

    // Swaps the door's prefab without losing the object: keeps its name, parent and overrides
    private static LockedDoor Swap(LockedDoor door, GameObject prefab)
    {
        var go = door.gameObject;
        var oldName = PrefabUtility.GetCorrespondingObjectFromSource(go).name;
        var settings = new PrefabReplacingSettings { changeRootNameToAssetName = false, objectMatchMode = ObjectMatchMode.ByHierarchy };
        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, prefab, settings, InteractionMode.UserAction);

        // "Door (2)" becomes "SideDoor (2)"; a hand-written name is left as is
        Undo.RecordObject(go, "Swap door");
        if (go.name.StartsWith(oldName)) go.name = prefab.name + go.name.Substring(oldName.Length);
        Undo.RecordObject(go.transform, "Swap door");
        go.transform.rotation = Quaternion.identity;
        return go.GetComponent<LockedDoor>();
    }

    // Doors rotated 90° or 270° that have a side version
    public static LockedDoor[] FindRotatedWithCounterpart() =>
        FindDoors().Where(d => Mathf.RoundToInt(SnapAngle(d.transform.eulerAngles.z) / 90f) % 2 != 0 && FindCounterpart(d) != null).ToArray();

    // They already cover the same cells as the side version: swapping them and clearing the rotation is enough
    public static void ReplaceRotated(LevelBuildReport report)
    {
        var doors = FindRotatedWithCounterpart();
        foreach (var door in doors)
        {
            var position = door.transform.position;
            var swapped = Swap(door, FindCounterpart(door));
            swapped.transform.position = SnapPosition(swapped, position, 0f);
        }
        if (doors.Length > 0) report.Info($"Replaced {doors.Length} rotated doors with their side version.");
    }

    public static GameObject Create(GameObject prefab, Vector3 position, Scene scene)
    {
        var root = EnemySpawnBuilder.GetOrCreateRoot(DoorsRootName, scene);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(go, "New door");
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
        if (moved > 0) report.Info($"Snapped {moved} doors to the grid.");
    }

    public static void Validate(Tilemap floor, LevelBuildReport report)
    {
        foreach (var door in FindDoors())
        {
            var problem = FindProblem(door, floor);
            if (problem != null) report.Warn($"{door.name}: {problem}", door);
        }
    }

    // Cells covered by the door, in tilemap coordinates
    public static RectInt FootprintCells(LockedDoor door, Tilemap floor)
    {
        var size = Footprint(door, door.transform.eulerAngles.z);
        var cell = CellSize();
        var corner = door.transform.position - new Vector3(size.x * cell.x, size.y * cell.y) * 0.5f;
        var min = floor.WorldToCell(corner + new Vector3(cell.x, cell.y) * 0.5f);
        return new RectInt(min.x, min.y, size.x, size.y);
    }

    // null if the door seals the gap properly
    public static string FindProblem(LockedDoor door, Tilemap floor)
    {
        if (floor == null) return null;

        var r = FootprintCells(door, floor);
        if (!AnyFloor(floor, r, all: true)) return "some cells have no floor underneath. Paint floor across the whole door gap.";
        if (r.width == r.height) return null;

        // ends: the next cell past each end of the long side; sides: either side of the short side
        bool horizontal = r.width > r.height;
        var endA = horizontal ? new RectInt(r.xMin - 1, r.yMin, 1, r.height) : new RectInt(r.xMin, r.yMin - 1, r.width, 1);
        var endB = horizontal ? new RectInt(r.xMax, r.yMin, 1, r.height) : new RectInt(r.xMin, r.yMax, r.width, 1);
        var sideA = horizontal ? new RectInt(r.xMin, r.yMin - 1, r.width, 1) : new RectInt(r.xMin - 1, r.yMin, 1, r.height);
        var sideB = horizontal ? new RectInt(r.xMin, r.yMax, r.width, 1) : new RectInt(r.xMax, r.yMin, 1, r.height);

        if (!AnyFloor(floor, endA) && !AnyFloor(floor, endB)) return null;
        if (!AnyFloor(floor, sideA) && !AnyFloor(floor, sideB)) return "it looks rotated the wrong way: rotate it 90° (Shift+R).";
        return "there's floor at one end of the door: you can walk around it without opening it.";
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
