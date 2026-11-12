using UnityEditor;
using UnityEngine;

// The Level Builder's "place" mode: after pressing a button, a preview follows the mouse in the
// scene (snapped to the grid) and a click places the element there.
//   Click: place · Shift+click: place and keep placing · Esc: cancel · R: rotate (if the element rotates)
public static class ScenePlacement
{
    public class Mode
    {
        public string Label;
        public System.Func<Vector3, Vector3> Snap = p => p;
        public System.Action<Vector3> Draw;            // draws the preview at the already snapped position
        public System.Func<Vector3, string> Status;    // extra text next to the cursor (optional)
        public System.Action<Vector3> Place;           // creates the element
        public System.Action OnRotate;                 // R key (optional)
        public bool KeepPlacing;                       // keep placing without Shift
    }

    private static Mode _mode;
    private static GUIStyle _style;

    public static Mode Current => _mode;
    public static event System.Action Changed;

    public static void Begin(Mode mode)
    {
        Cancel();
        _mode = mode;
        SceneView.duringSceneGui += OnSceneGUI;
        var view = SceneView.lastActiveSceneView;
        if (view != null) view.Focus();
        SceneView.RepaintAll();
        Changed?.Invoke();
    }

    public static void Cancel()
    {
        if (_mode == null) return;
        _mode = null;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
        Changed?.Invoke();
    }

    private static void OnSceneGUI(SceneView view)
    {
        var mode = _mode;
        if (mode == null) return;

        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id); // so the click doesn't change the selection

        var world = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        world.z = 0f;
        var snapped = mode.Snap(world);

        switch (e.type)
        {
            case EventType.MouseMove:
            case EventType.MouseDrag:
                view.Repaint();
                break;

            case EventType.Repaint:
                mode.Draw?.Invoke(snapped);
                DrawHint(mode, snapped, e.mousePosition);
                break;

            case EventType.MouseDown when e.button == 0 && !e.alt:
                mode.Place(snapped);
                if (!(mode.KeepPlacing || e.shift) && _mode == mode) Cancel();
                e.Use();
                break;

            case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                Cancel();
                e.Use();
                break;

            case EventType.KeyDown when e.keyCode == KeyCode.R && mode.OnRotate != null:
                mode.OnRotate();
                view.Repaint();
                e.Use();
                break;
        }
    }

    private static void DrawHint(Mode mode, Vector3 snapped, Vector2 mouse)
    {
        _style ??= new GUIStyle(EditorStyles.helpBox) { fontSize = 11, richText = true };
        string status = mode.Status?.Invoke(snapped);
        string keys = "Click: place · Shift+click: place and continue · Esc: cancel" + (mode.OnRotate != null ? " · R: rotate" : "");
        var text = $"<b>{mode.Label}</b>" + (string.IsNullOrEmpty(status) ? "" : $"\n{status}") + $"\n{keys}";

        Handles.BeginGUI();
        var content = new GUIContent(text);
        var size = _style.CalcSize(content);
        GUI.Label(new Rect(mouse.x + 18f, mouse.y + 18f, size.x + 8f, size.y + 4f), content, _style);
        Handles.EndGUI();
    }

    // Drawing helpers for the previews
    public static void DrawCellRect(RectInt rect, Color fill, Color outline)
    {
        var corners = new[]
        {
            new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMin, rect.yMax),
            new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMax, rect.yMin),
        };
        Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
    }

    public static void DrawDottedRect(RectInt rect, Color color, float dash = 4f)
    {
        Handles.color = color;
        var a = new Vector3(rect.xMin, rect.yMin);
        var b = new Vector3(rect.xMin, rect.yMax);
        var c = new Vector3(rect.xMax, rect.yMax);
        var d = new Vector3(rect.xMax, rect.yMin);
        Handles.DrawDottedLine(a, b, dash);
        Handles.DrawDottedLine(b, c, dash);
        Handles.DrawDottedLine(c, d, dash);
        Handles.DrawDottedLine(d, a, dash);
    }

    public static void DrawMouth(Vector2 point, Vector2Int dir, int width, Color color)
    {
        Vector3 p = point;
        Vector3 f = (Vector2)dir;
        Vector3 across = new Vector3(-f.y, f.x) * (width * 0.5f);
        Handles.color = color;
        Handles.DrawLine(p - across, p + across, 3f);
        Handles.DrawLine(p, p + f, 3f);
        Handles.DrawLine(p + f, p + f * 0.6f + across.normalized * 0.3f, 3f);
        Handles.DrawLine(p + f, p + f * 0.6f - across.normalized * 0.3f, 3f);
    }
}
