using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Modular pockets: they have their own Grid and child tilemaps, so they move as a whole and can be
// saved as prefabs. This creates them, converts legacy ones and picks their floor for painting.
public static class PocketModular
{
    public const string FloorName = "Pocket Floor";
    public const string WallsName = "Pocket Walls";
    public const string PropsName = "Pocket Props";

    public static Tilemap FindLevelTilemap(string name) =>
        Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == name);

    public static Pocket Create(Vector3 at, Tilemap levelFloor, Tilemap levelWalls, Tilemap levelProps)
    {
        var go = new GameObject(GameObjectUtility.GetUniqueNameForSibling(null, "Pocket"));
        Undo.RegisterCreatedObjectUndo(go, "New pocket");
        go.transform.position = new Vector3(Mathf.Round(at.x), Mathf.Round(at.y), 0f);
        var pocket = Undo.AddComponent<Pocket>(go);
        AddTilemaps(pocket, levelFloor, levelWalls, levelProps);
        return pocket;
    }

    // Grid + child tilemaps with the same settings as the level's (sorting order, collision,
    // Obstacles layer, NavMesh modifier...)
    private static void AddTilemaps(Pocket pocket, Tilemap levelFloor, Tilemap levelWalls, Tilemap levelProps)
    {
        var go = pocket.gameObject;
        if (!go.TryGetComponent<Grid>(out var grid)) grid = Undo.AddComponent<Grid>(go);
        if (levelFloor.layoutGrid != null)
        {
            grid.cellSize = levelFloor.layoutGrid.cellSize;
            grid.cellGap = levelFloor.layoutGrid.cellGap;
            grid.cellLayout = levelFloor.layoutGrid.cellLayout;
        }

        var floor = CreateLike(levelFloor, FloorName, pocket.transform);
        var walls = levelWalls != null ? CreateLike(levelWalls, WallsName, pocket.transform) : null;
        var props = levelProps != null ? CreateLike(levelProps, PropsName, pocket.transform) : null;
        Undo.RecordObject(pocket, "Modular pocket");
        pocket.SetTilemaps(floor, walls, props);
        EditorUtility.SetDirty(pocket);
    }

    private static Tilemap CreateLike(Tilemap source, string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.layer = source.gameObject.layer;
        go.tag = source.gameObject.tag;
        go.transform.SetParent(parent, false);

        var tilemap = go.AddComponent<Tilemap>();
        tilemap.tileAnchor = source.tileAnchor;
        tilemap.orientation = source.orientation;
        tilemap.color = source.color;
        foreach (var component in source.GetComponents<Component>())
        {
            if (component == null || component is Transform || component is Tilemap) continue;
            var copy = go.GetComponent(component.GetType());
            if (copy == null) copy = go.AddComponent(component.GetType());
            if (copy != null) EditorUtility.CopySerialized(component, copy);
        }
        return tilemap;
    }

    // A legacy pocket (a rectangle over the level floor) gets its own tilemaps: the floor and props
    // inside the rectangle move into them. Run Build (F6) afterwards.
    public static void ConvertToModular(Pocket pocket, Tilemap levelFloor, Tilemap levelWalls, Tilemap levelProps, LevelBuildReport report)
    {
        if (pocket.IsModular)
        {
            report.Info($"{pocket.name} is already modular.");
            return;
        }

        PocketBuilder.ClearLegacy(pocket, levelFloor); // its old sheets and overlaps were in the level's Floor
        var area = pocket.Area;

        Undo.RecordObject(pocket.transform, "Convert pocket");
        var p = pocket.transform.position;
        pocket.transform.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f);
        AddTilemaps(pocket, levelFloor, levelWalls, levelProps);

        var offset = pocket.CellOffset;
        var undo = new Object[] { levelFloor, levelWalls, levelProps, pocket.Floor, pocket.Walls, pocket.Props }.Where(o => o != null).ToArray();
        Undo.RegisterCompleteObjectUndo(undo, "Convert pocket");

        int floorCount = 0, propCount = 0;
        foreach (var c in area.allPositionsWithin)
        {
            var world = (Vector3Int)c;
            var local = (Vector3Int)(c - offset);
            var tile = levelFloor.GetTile(world);
            if (tile != null)
            {
                pocket.Floor.SetTile(local, tile);
                levelFloor.SetTile(world, null);
                floorCount++;
            }
            if (levelProps != null && pocket.Props != null && levelProps.HasTile(world))
            {
                pocket.Props.SetTile(new TileChangeData(local, levelProps.GetTile(world), levelProps.GetColor(world), levelProps.GetTransformMatrix(world)), true);
                levelProps.SetTile(world, null);
                propCount++;
            }
            if (levelWalls != null) levelWalls.SetTile(world, null); // F6 regenerates them in the pocket
        }

        foreach (var o in undo) EditorUtility.SetDirty(o);
        report.Info($"{pocket.name} is now modular: {floorCount} floor cells and {propCount} props moved to its own tilemaps. " +
                    "Run Build (F6) to regenerate walls, sheets and seams.");
    }

    // Sets the Tile Palette to paint on the pocket's floor
    public static void PaintInto(Pocket pocket)
    {
        if (!pocket.IsModular) return;
        GridPaintingState.scenePaintTarget = pocket.Floor.gameObject;
        Selection.activeGameObject = pocket.Floor.gameObject;
        EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
    }

    [MenuItem("Tools/Dungeon/Convert Selected Pocket to Modular")]
    private static void ConvertSelected()
    {
        var pocket = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Pocket>() : null;
        if (pocket == null)
        {
            Debug.LogWarning("[Level Builder] Select a pocket to convert it.");
            return;
        }
        var report = new LevelBuildReport();
        ConvertToModular(pocket, FindLevelTilemap("Floor"), FindLevelTilemap("Walls"), FindLevelTilemap("Props"), report);
        foreach (var info in report.Infos) Debug.Log("[Level Builder] " + info);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pocket.gameObject.scene);
    }
}
