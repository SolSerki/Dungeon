using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemySpawn))]
public class EnemySpawnEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var spawn = (EnemySpawn)target;

        if (spawn.Variants != null && spawn.Variants.Length > 0)
        {
            var rolled = EnemySpawnBuilder.ResolvePrefab(spawn);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Rolled", rolled != null ? rolled.name : "(none)", EditorStyles.boldLabel);
                if (GUILayout.Button("Reroll", GUILayout.Width(120))) Reroll(spawn);
            }
        }

        DrawLoot(spawn);

        EditorGUILayout.Space();
        if (GUILayout.Button("Add waypoint"))
        {
            var parent = spawn.transform;
            var last = parent.childCount > 0 ? parent.GetChild(parent.childCount - 1).position : parent.position;
            EnemySpawnSceneTool.AddWaypoint(spawn, EnemySpawnSceneTool.Snap(last + Vector3.right), select: true);
        }

        LevelBuilderPrefs.SnapWaypoints = EditorGUILayout.Toggle("Snap to cell", LevelBuilderPrefs.SnapWaypoints);

        int count = spawn.transform.childCount;
        EditorGUILayout.HelpBox(
            $"{count} waypoints: the children of this object, in order. " +
            (count < 2 ? "With fewer than 2 the enemy stands still until it sees the player." : "The enemy patrols them in a loop.") +
            "\n\nIn the Scene view: drag the yellow points, Shift+click adds one at the end, Ctrl+click on one deletes it." +
            "\nThe yellow line is the real NavMesh path; red where there's no path.",
            MessageType.None);

        if (spawn.Instance != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Enemy", spawn.Instance, typeof(GameObject), true);
        }
        else
        {
            EditorGUILayout.HelpBox("Not in the scene yet: press Build (F6) in the Level Builder.", MessageType.Info);
        }
    }

    // One toggle per ItemData in Assets/Data/Items: ticked items always drop on death, on top of the prefab's loot
    private static void DrawLoot(EnemySpawn spawn)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Loot", EditorStyles.boldLabel);

        var items = LootCatalog.Items;
        if (items.Length == 0)
        {
            EditorGUILayout.HelpBox($"No ItemData assets in {LootCatalog.ItemsFolder}.", MessageType.Info);
            return;
        }

        // spawn from before loot existed: first pull in whatever the enemy has set, so the toggles show it
        if (!spawn.LootImported) EnemySpawnBuilder.ImportExistingLoot(spawn, new LevelBuildReport());

        var current = spawn.Loot ?? new ItemData[0];
        foreach (var item in items)
        {
            bool has = current.Contains(item);
            if (EditorGUILayout.Toggle(item.name, has) != has) SetLoot(spawn, item, !has);
        }
    }

    private static void SetLoot(EnemySpawn spawn, ItemData item, bool carry)
    {
        var current = spawn.Loot ?? new ItemData[0];
        // in catalog order, keeping whatever is no longer in the folder
        var loot = LootCatalog.Items.Where(i => i == item ? carry : current.Contains(i))
            .Concat(current.Where(i => i != null && !LootCatalog.Items.Contains(i)))
            .ToArray();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var so = new SerializedObject(spawn);
        var property = so.FindProperty("_loot");
        property.arraySize = loot.Length;
        for (int i = 0; i < loot.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = loot[i];
        so.ApplyModifiedProperties();

        // if the enemy is already in the scene, hand it the loot right away
        if (spawn.Instance != null) EnemySpawnBuilder.Sync(spawn, new LevelBuildReport());
        Undo.CollapseUndoOperations(group);
    }

    private static void Reroll(EnemySpawn spawn)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(spawn, "Reroll");
        EnemySpawnBuilder.Reroll(spawn);
        // if the enemy was already in the scene, replace it now to show the result
        if (spawn.Instance != null) EnemySpawnBuilder.Sync(spawn, new LevelBuildReport());
        Undo.CollapseUndoOperations(group);
    }
}
