using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

// Con una puerta seleccionada, la herramienta de mover de Unity se reemplaza por una que salta de a celdas,
// y aparecen botones para girarla de a 90° (también Shift+R).
[InitializeOnLoad]
public static class DoorSceneTool
{
    // en SessionState y no en un static: sobrevive a la recarga de scripts y no deja las herramientas escondidas
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
            Undo.RecordObject(t, "Mover puerta");
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
            if (GUILayout.Button(new GUIContent("Girar izq.", "90° a la izquierda"))) DoorPlacement.Rotate(door, 90f);
            if (GUILayout.Button(new GUIContent("Girar der.", "90° a la derecha (Shift+R)"))) DoorPlacement.Rotate(door, -90f);
            if (!DoorPlacement.IsSnapped(door) && GUILayout.Button(new GUIContent("Ajustar", "Llevarla a la grilla"))) DoorPlacement.Snap(door);
        }
        if (problem != null) GUILayout.Label(problem, EditorStyles.helpBox);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    [Shortcut("Dungeon/Girar puerta 90°", typeof(SceneView), KeyCode.R, ShortcutModifiers.Shift)]
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
