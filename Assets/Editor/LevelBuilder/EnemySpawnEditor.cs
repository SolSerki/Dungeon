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
                EditorGUILayout.LabelField("Sorteado", rolled != null ? rolled.name : "(ninguno)", EditorStyles.boldLabel);
                if (GUILayout.Button("Volver a sortear", GUILayout.Width(120))) Reroll(spawn);
            }
        }

        DrawLoot(spawn);

        EditorGUILayout.Space();
        if (GUILayout.Button("Agregar waypoint"))
        {
            var parent = spawn.transform;
            var last = parent.childCount > 0 ? parent.GetChild(parent.childCount - 1).position : parent.position;
            EnemySpawnSceneTool.AddWaypoint(spawn, EnemySpawnSceneTool.Snap(last + Vector3.right), select: true);
        }

        LevelBuilderPrefs.SnapWaypoints = EditorGUILayout.Toggle("Ajustar a la celda", LevelBuilderPrefs.SnapWaypoints);

        int count = spawn.transform.childCount;
        EditorGUILayout.HelpBox(
            $"{count} waypoints: son los hijos de este objeto, en orden. " +
            (count < 2 ? "Con menos de 2 el enemigo se queda quieto hasta ver al jugador." : "El enemigo los recorre en bucle.") +
            "\n\nEn la Scene view: arrastrá los puntos amarillos, Shift+clic agrega uno al final, Ctrl+clic sobre uno lo borra." +
            "\nLa línea amarilla es la ruta real por el NavMesh; en rojo, donde no hay camino.",
            MessageType.None);

        if (spawn.Instance != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Enemigo", spawn.Instance, typeof(GameObject), true);
        }
        else
        {
            EditorGUILayout.HelpBox("Todavía no está en la escena: apretá Build (F6) en el Level Builder.", MessageType.Info);
        }
    }

    // Un toggle por objeto de Assets/Prefabs/Items: lo marcado cae siempre al morir, además del loot del prefab
    private static void DrawLoot(EnemySpawn spawn)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Loot", EditorStyles.boldLabel);

        var items = LootCatalog.Items;
        if (items.Length == 0)
        {
            EditorGUILayout.HelpBox("No hay prefabs con Pickup en Assets/Prefabs/Items.", MessageType.Info);
            return;
        }

        // spawn de antes del loot: primero traemos lo que tenga cargado el enemigo, para que los toggles lo muestren
        if (!spawn.LootImported) EnemySpawnBuilder.ImportExistingLoot(spawn, new LevelBuildReport());

        var current = spawn.Loot ?? new GameObject[0];
        foreach (var item in items)
        {
            bool has = current.Contains(item);
            if (EditorGUILayout.Toggle(item.name, has) != has) SetLoot(spawn, item, !has);
        }
    }

    private static void SetLoot(EnemySpawn spawn, GameObject item, bool carry)
    {
        var current = spawn.Loot ?? new GameObject[0];
        // en el orden del catálogo, conservando lo que ya no está en la carpeta
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

        // si el enemigo ya está en la escena, le pasamos el loot ya mismo
        if (spawn.Instance != null) EnemySpawnBuilder.Sync(spawn, new LevelBuildReport());
        Undo.CollapseUndoOperations(group);
    }

    private static void Reroll(EnemySpawn spawn)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(spawn, "Volver a sortear");
        EnemySpawnBuilder.Reroll(spawn);
        // si el enemigo ya estaba en la escena, lo cambiamos ya para ver el resultado
        if (spawn.Instance != null) EnemySpawnBuilder.Sync(spawn, new LevelBuildReport());
        Undo.CollapseUndoOperations(group);
    }
}
