using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

// With a door selected, Unity's move tool is replaced by one that snaps cell by cell,
// and buttons appear to rotate it in 90° steps (also Shift+R).
[InitializeOnLoad]
public static class DoorSceneTool
{
    // in SessionState rather than a static: it survives script reloads and doesn't leave the tools hidden
    private const string HidToolsKey = "Dungeon.LevelBuilder.DoorHidTools";

    private static readonly Color Valid = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color Invalid = new Color(1f, 0.3f, 0.25f);

    static DoorSceneTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += RestoreTools;
    }

    public static LockedDoor ActiveDoor()
    {
        if (Selection.transforms.Length != 1 || Selection.activeTransform == null) return null;
        return Selection.activeTransform.GetComponentInParent<LockedDoor>();
    }

    private static void OnSceneGUI(SceneView view)
    {
        var door = ActiveDoor();
        bool moving = Tools.current == Tool.Move || Tools.current == Tool.Rotate || Tools.current == Tool.Transform;
        if (door == null || !moving)
        {
            RestoreTools();
            return;
        }

        Tools.hidden = true;
        SessionState.SetBool(HidToolsKey, true);

        var floor = DoorPlacement.FindFloor();
        var problem = DoorPlacement.FindProblem(door, floor);
        if (floor != null) DrawFootprint(door, floor, problem == null ? Valid : Invalid);

        var t = door.transform;
        EditorGUI.BeginChangeCheck();
        var moved = Handles.PositionHandle(t.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t, "Move door");
            t.position = DoorPlacement.SnapPosition(door, moved, t.eulerAngles.z);
        }

        DrawButtons(door, problem);
    }

    private static void DrawFootprint(LockedDoor door, UnityEngine.Tilemaps.Tilemap floor, Color color)
    {
        var r = DoorPlacement.FootprintCells(door, floor);
        var min = floor.CellToWorld(new Vector3Int(r.xMin, r.yMin, 0));
        var max = floor.CellToWorld(new Vector3Int(r.xMax, r.yMax, 0));
        var corners = new[] { new Vector3(min.x, min.y), new Vector3(min.x, max.y), new Vector3(max.x, max.y), new Vector3(max.x, min.y) };
        Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.15f), color);
    }

    private static void DrawButtons(LockedDoor door, string problem)
    {
        Handles.BeginGUI();
        var screen = HandleUtility.WorldToGUIPoint(door.transform.position);
        float height = problem != null ? 64f : 24f;
        GUILayout.BeginArea(new Rect(screen.x - 110f, screen.y + 40f, 220f, height));
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Rotate left", "90° counterclockwise"))) DoorPlacement.Rotate(door, 90f);
            if (GUILayout.Button(new GUIContent("Rotate right", "90° clockwise (Shift+R)"))) DoorPlacement.Rotate(door, -90f);
            if (!DoorPlacement.IsSnapped(door) && GUILayout.Button(new GUIContent("Snap", "Move it onto the grid"))) DoorPlacement.Snap(door);
        }
        if (problem != null) GUILayout.Label(problem, EditorStyles.helpBox);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    [Shortcut("Dungeon/Rotate Door 90°", typeof(SceneView), KeyCode.R, ShortcutModifiers.Shift)]
    private static void RotateShortcut()
    {
        var door = ActiveDoor();
        if (door == null) return;
        DoorPlacement.Rotate(door, -90f);
        SceneView.RepaintAll();
    }

    private static void RestoreTools()
    {
        if (!SessionState.GetBool(HidToolsKey, false)) return;
        Tools.hidden = false;
        SessionState.SetBool(HidToolsKey, false);
    }
}
