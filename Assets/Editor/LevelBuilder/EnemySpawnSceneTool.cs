using UnityEditor;
using UnityEngine;

// Waypoint editing in the Scene view when an EnemySpawn (or one of its waypoints) is selected:
// dragging moves, Shift+click adds at the end, Ctrl+click on a waypoint deletes it.
[InitializeOnLoad]
public static class EnemySpawnSceneTool
{
    private const float PickDistance = 12f; // in pixels

    static EnemySpawnSceneTool() => SceneView.duringSceneGui += OnSceneGUI;

    public static EnemySpawn ActiveSpawn()
    {
        var selected = Selection.activeTransform;
        if (selected == null) return null;

        var spawn = selected.GetComponent<EnemySpawn>();
        if (spawn != null) return spawn;
        return selected.parent != null ? selected.parent.GetComponent<EnemySpawn>() : null;
    }

    private static void OnSceneGUI(SceneView view)
    {
        var spawn = ActiveSpawn();
        if (spawn == null) return;

        var e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        bool adding = e.shift && !EditorGUI.actionKey;
        bool removing = EditorGUI.actionKey && !e.shift;

        // with Shift or Ctrl held, the click is ours and doesn't change the selection
        if (e.type == EventType.Layout && (adding || removing)) HandleUtility.AddDefaultControl(controlId);

        var parent = spawn.transform;
        for (int i = 0; i < parent.childCount; i++)
        {
            var point = parent.GetChild(i);
            Handles.color = new Color(1f, 0.8f, 0.2f);
            EditorGUI.BeginChangeCheck();
            float size = HandleUtility.GetHandleSize(point.position) * 0.08f;
            var moved = Handles.FreeMoveHandle(point.position, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(point, "Move waypoint");
                point.position = Snap(moved);
            }
            Handles.Label(point.position + new Vector3(0.2f, 0.35f), (i + 1).ToString(), EditorStyles.whiteBoldLabel);
        }

        if (e.type != EventType.MouseDown || e.button != 0) return;

        if (adding && HandleUtility.nearestControl == controlId)
        {
            AddWaypoint(spawn, Snap(MouseWorldPosition(e)), select: false);
            e.Use();
        }
        else if (removing)
        {
            var target = NearestWaypoint(parent, e.mousePosition);
            if (target != null) RemoveWaypoint(spawn, target);
            e.Use();
        }
    }

    public static void AddWaypoint(EnemySpawn spawn, Vector3 position, bool select)
    {
        var waypoint = new GameObject($"WP{spawn.transform.childCount + 1}");
        Undo.RegisterCreatedObjectUndo(waypoint, "Add waypoint");
        Undo.SetTransformParent(waypoint.transform, spawn.transform, "Add waypoint");
        waypoint.transform.position = position;
        // an icon so it can be grabbed directly in the Scene view
        EditorGUIUtility.SetIconForObject(waypoint, (Texture2D)EditorGUIUtility.IconContent("sv_icon_dot4_pix16_gizmo").image);

        if (select) Selection.activeGameObject = waypoint;
    }

    private static void RemoveWaypoint(EnemySpawn spawn, Transform waypoint)
    {
        // if the selected waypoint is deleted, the selection moves to the marker to keep editing
        if (Selection.activeTransform == waypoint) Selection.activeGameObject = spawn.gameObject;
        Undo.DestroyObjectImmediate(waypoint.gameObject);
        Renumber(spawn);
    }

    private static void Renumber(EnemySpawn spawn)
    {
        for (int i = 0; i < spawn.transform.childCount; i++)
        {
            var child = spawn.transform.GetChild(i).gameObject;
            string name = $"WP{i + 1}";
            if (child.name == name) continue;
            Undo.RecordObject(child, "Rename waypoint");
            child.name = name;
        }
    }

    private static Transform NearestWaypoint(Transform parent, Vector2 mouse)
    {
        Transform best = null;
        float bestDistance = PickDistance;
        foreach (Transform point in parent)
        {
            float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(point.position), mouse);
            if (distance < bestDistance) { best = point; bestDistance = distance; }
        }
        return best;
    }

    private static Vector3 MouseWorldPosition(Event e)
    {
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        return new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
    }

    public static Vector3 Snap(Vector3 position)
    {
        position.z = 0f;
        if (!LevelBuilderPrefs.SnapWaypoints) return position;

        var grid = Object.FindAnyObjectByType<Grid>();
        return grid != null ? grid.GetCellCenterWorld(grid.WorldToCell(position)) : position;
    }
}
