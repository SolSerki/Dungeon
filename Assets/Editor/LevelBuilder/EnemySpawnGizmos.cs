using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// What each EnemySpawn shows in the Scene view: name, loot, detection range and patrol route
public static class EnemySpawnGizmos
{
    private static readonly Color MarkerColor = new Color(1f, 0.3f, 0.3f);
    private static readonly Color PathColor = new Color(1f, 0.8f, 0.2f);
    private static readonly Color BrokenColor = new Color(1f, 0.15f, 0.15f);
    private static readonly NavMeshPath Path = new NavMeshPath();
    private static readonly Color LootColor = new Color(1f, 0.85f, 0.25f);
    private static GUIStyle _labelStyle;
    private static GUIStyle _lootStyle;

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    private static void Draw(EnemySpawn spawn, GizmoType type)
    {
        bool selected = EnemySpawnSceneTool.ActiveSpawn() == spawn;
        var position = spawn.transform.position;

        // clickable sphere to select the marker from the scene
        Gizmos.color = new Color(MarkerColor.r, MarkerColor.g, MarkerColor.b, 0.25f);
        Gizmos.DrawSphere(position, 0.3f);
        Gizmos.color = MarkerColor;
        Gizmos.DrawWireSphere(position, 0.4f);

        var prefab = EnemySpawnBuilder.ResolvePrefab(spawn);
        _labelStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = MarkerColor }, alignment = TextAnchor.MiddleCenter };
        Handles.Label(position + Vector3.up * 0.75f, prefab != null ? prefab.name : "(no prefab)", _labelStyle);

        // guaranteed drops, below the marker
        var loot = spawn.Loot;
        if (loot != null && loot.Length > 0)
        {
            _lootStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = LootColor }, alignment = TextAnchor.MiddleCenter };
            Handles.Label(position + Vector3.down * 0.55f, string.Join(", ", loot.Where(l => l != null).Select(l => l.name)), _lootStyle);
        }

        if (selected || LevelBuilderPrefs.ShowAllRanges)
        {
            float range = EnemySpawnBuilder.GetDetectRange(spawn);
            if (range > 0f)
            {
                Handles.color = new Color(MarkerColor.r, MarkerColor.g, MarkerColor.b, selected ? 0.06f : 0.03f);
                Handles.DrawSolidDisc(position, Vector3.forward, range);
                Handles.color = new Color(MarkerColor.r, MarkerColor.g, MarkerColor.b, selected ? 0.9f : 0.35f);
                Handles.DrawWireDisc(position, Vector3.forward, range);
            }
        }

        // real NavMesh path: start -> WP1 -> ... -> WPn -> WP1 (the patrol is a loop)
        var t = spawn.transform;
        int count = t.childCount;
        if (count == 0) return;

        float alpha = selected ? 1f : 0.45f;
        DrawLeg(position, t.GetChild(0).position, alpha);
        for (int i = 0; i < count - 1; i++) DrawLeg(t.GetChild(i).position, t.GetChild(i + 1).position, alpha);
        if (count > 2) DrawLeg(t.GetChild(count - 1).position, t.GetChild(0).position, alpha);

        Gizmos.color = new Color(PathColor.r, PathColor.g, PathColor.b, alpha);
        for (int i = 0; i < count; i++) Gizmos.DrawWireCube(t.GetChild(i).position, Vector3.one * 0.3f);
    }

    private static void DrawLeg(Vector3 from, Vector3 to, float alpha)
    {
        // no path (off the NavMesh or not baked): dotted red line
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, Path))
        {
            Handles.color = new Color(BrokenColor.r, BrokenColor.g, BrokenColor.b, alpha);
            Handles.DrawDottedLine(from, to, 4f);
            return;
        }

        var corners = Path.corners;
        Handles.color = new Color(PathColor.r, PathColor.g, PathColor.b, alpha);
        Handles.DrawAAPolyLine(3f, corners);

        // partial path: goes as far as it can and the rest is red
        if (Path.status != NavMeshPathStatus.PathComplete)
        {
            Handles.color = new Color(BrokenColor.r, BrokenColor.g, BrokenColor.b, alpha);
            Handles.DrawDottedLine(corners.Length > 0 ? corners[corners.Length - 1] : from, to, 4f);
        }
    }
}
