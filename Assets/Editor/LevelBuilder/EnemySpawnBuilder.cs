using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Instantiates each EnemySpawn's enemy and assigns its waypoints
public static class EnemySpawnBuilder
{
    public const string EnemiesRootName = "Enemies";
    public const string SpawnsRootName = "EnemySpawns";

    public static EnemySpawn[] FindSpawns() => Object.FindObjectsByType<EnemySpawn>(FindObjectsInactive.Include);

    // With variants, picks one by weight using the marker's seed (same result on every Build)
    public static GameObject ResolvePrefab(EnemySpawn spawn)
    {
        var options = (spawn.Variants ?? new EnemySpawn.Variant[0]).Where(v => v.Prefab != null && v.Weight > 0f).ToArray();
        if (options.Length == 0) return spawn.Prefab;

        float roll = (float)new System.Random(spawn.Seed).NextDouble() * options.Sum(v => v.Weight);
        foreach (var option in options)
        {
            roll -= option.Weight;
            if (roll < 0f) return option.Prefab;
        }
        return options[options.Length - 1].Prefab;
    }

    public static void Reroll(EnemySpawn spawn)
    {
        var so = new SerializedObject(spawn);
        so.FindProperty("_seed").intValue = Random.Range(1, int.MaxValue);
        so.ApplyModifiedProperties();
    }

    public static float GetDetectRange(EnemySpawn spawn)
    {
        // the instance may have an override; if it doesn't exist yet, the prefab's value applies
        var source = spawn.Instance != null ? spawn.Instance : ResolvePrefab(spawn);
        var ai = source != null ? source.GetComponent<EnemyAI>() : null;
        return ai != null ? new SerializedObject(ai).FindProperty("_detectRange").floatValue : 0f;
    }

    public static void SyncAll(LevelBuildReport report)
    {
        var spawns = FindSpawns();
        foreach (var spawn in spawns) Sync(spawn, report);
        report.Info($"Enemies synced: {spawns.Length}.");
    }

    public static void Sync(EnemySpawn spawn, LevelBuildReport report)
    {
        if (spawn.Variants != null && spawn.Variants.Length > 0 && spawn.Variants.All(v => v.Prefab == null || v.Weight <= 0f))
            report.Warn($"{spawn.name}: the variants have no prefab or weight 0; using the main Prefab.", spawn);

        var prefab = ResolvePrefab(spawn);
        if (prefab == null)
        {
            report.Warn($"{spawn.name} has no prefab assigned.", spawn);
            return;
        }

        var instance = spawn.Instance;
        if (!spawn.LootImported) ImportExistingLoot(spawn, report);

        // if you changed the prefab or another variant came up, replace the enemy
        if (instance != null && PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
        {
            Undo.DestroyObjectImmediate(instance);
            instance = null;
        }

        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, spawn.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn enemy");
            instance.transform.SetParent(GetOrCreateRoot(EnemiesRootName, spawn.gameObject.scene).transform);
            instance.name = spawn.name;
            SetInstance(spawn, instance);
        }

        Undo.RecordObject(instance.transform, "Move enemy");
        instance.transform.position = spawn.transform.position;
        ApplyLoot(spawn, instance, report);

        var ai = instance.GetComponent<EnemyAI>();
        if (ai == null)
        {
            report.Warn($"The prefab {prefab.name} has no EnemyAI.", spawn);
            return;
        }

        var aiSo = new SerializedObject(ai);
        var points = aiSo.FindProperty("_patrolPoints");
        points.arraySize = spawn.transform.childCount;
        for (int i = 0; i < spawn.transform.childCount; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = spawn.transform.GetChild(i);
        aiSo.ApplyModifiedProperties();
    }

    // The enemy's drops become the prefab's plus the loot ticked on the spawn, with chance 1
    private static void ApplyLoot(EnemySpawn spawn, GameObject instance, LevelBuildReport report)
    {
        var loot = (spawn.Loot ?? new ItemData[0]).Where(p => p != null).ToArray();
        var dropper = instance.GetComponent<LootDropper>();
        if (dropper == null)
        {
            if (loot.Length > 0) report.Warn($"{spawn.name} has loot but {instance.name} has no LootDropper: it won't drop anything.", spawn);
            return;
        }

        var so = new SerializedObject(dropper);
        var drops = so.FindProperty("_drops");
        if (loot.Length == 0)
        {
            // with no loot of its own, it goes back to whatever the prefab says
            if (drops.prefabOverride) PrefabUtility.RevertPropertyOverride(drops, InteractionMode.UserAction);
            return;
        }

        var baseDrops = BaseDrops(dropper);
        drops.arraySize = baseDrops.Count + loot.Length;
        for (int i = 0; i < drops.arraySize; i++)
        {
            var element = drops.GetArrayElementAtIndex(i);
            bool fromPrefab = i < baseDrops.Count;
            element.FindPropertyRelative("Item").objectReferenceValue = fromPrefab ? baseDrops[i].item : loot[i - baseDrops.Count];
            element.FindPropertyRelative("Chance").floatValue = fromPrefab ? baseDrops[i].chance : 1f;
        }
        so.ApplyModifiedProperties();
    }

    private static List<(ItemData item, float chance)> BaseDrops(LootDropper dropper)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(dropper);
        return source != null ? ReadDrops(source) : new List<(ItemData, float)>();
    }

    public static List<(ItemData item, float chance)> ReadDrops(LootDropper dropper)
    {
        var drops = new SerializedObject(dropper).FindProperty("_drops");
        var result = new List<(ItemData, float)>();
        for (int i = 0; i < drops.arraySize; i++)
        {
            var element = drops.GetArrayElementAtIndex(i);
            result.Add((element.FindPropertyRelative("Item").objectReferenceValue as ItemData, element.FindPropertyRelative("Chance").floatValue));
        }
        return result;
    }

    // When converting an enemy, the drops you added by hand become toggles on the spawn
    private static ItemData[] ImportLoot(GameObject enemy, LevelBuildReport report)
    {
        var dropper = enemy.GetComponent<LootDropper>();
        if (dropper == null) return new ItemData[0];

        var remaining = BaseDrops(dropper);
        var loot = new List<ItemData>();
        foreach (var drop in ReadDrops(dropper))
        {
            int match = remaining.IndexOf(drop);
            if (match >= 0) { remaining.RemoveAt(match); continue; } // comes from the prefab
            if (drop.item == null) continue;

            if (!LootCatalog.Items.Contains(drop.item))
                report.Warn($"{enemy.name}: {drop.item.name} isn't in {LootCatalog.ItemsFolder}; moving it over as guaranteed loot anyway.", enemy);
            else if (drop.chance < 1f)
                report.Info($"{enemy.name}: {drop.item.name} had chance {drop.chance:0.##}; now it always drops.");
            if (!loot.Contains(drop.item)) loot.Add(drop.item);
        }
        return loot.ToArray();
    }

    // A spawn created before loot existed: whatever was set by hand on the enemy moves to the toggles,
    // so Build doesn't erase it. It happens only once; after that the toggles rule.
    public static void ImportExistingLoot(EnemySpawn spawn, LevelBuildReport report)
    {
        var loot = (spawn.Loot ?? new ItemData[0]).Where(p => p != null).ToList();
        if (spawn.Instance != null)
            foreach (var item in ImportLoot(spawn.Instance, report))
                if (!loot.Contains(item)) loot.Add(item);

        var so = new SerializedObject(spawn);
        SetLootProperty(so, loot.ToArray());
        so.FindProperty("_lootImported").boolValue = true;
        so.ApplyModifiedProperties();
        if (loot.Count > 0) report.Info($"{spawn.name}: moved its loot ({string.Join(", ", loot.Select(l => l.name))}) to the spawn's toggles.");
    }

    private static void SetLootProperty(SerializedObject spawn, ItemData[] loot)
    {
        var property = spawn.FindProperty("_loot");
        property.arraySize = loot.Length;
        for (int i = 0; i < loot.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = loot[i];
    }

    // Hand-placed enemies (no EnemySpawn): creates a marker for each and moves their waypoints to it.
    // The enemy is kept as is, with its overrides (for example, the loot).
    public static List<EnemyAI> FindLooseEnemies()
    {
        var owned = new HashSet<GameObject>(FindSpawns().Select(s => s.Instance).Where(i => i != null));
        return Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include).Where(ai => !owned.Contains(ai.gameObject)).ToList();
    }

    public static void ConvertLooseEnemies(LevelBuildReport report)
    {
        var loose = FindLooseEnemies();
        if (loose.Count == 0)
        {
            report.Info("No loose enemies to convert.");
            return;
        }

        // a waypoint shared by several enemies is copied instead of moved
        var usage = new Dictionary<Transform, int>();
        foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
            foreach (var point in GetPatrolPoints(ai).Where(p => p != null))
                usage[point] = usage.TryGetValue(point, out var n) ? n + 1 : 1;

        foreach (var ai in loose)
        {
            var enemy = ai.gameObject;
            var prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(enemy) == enemy ? PrefabUtility.GetCorrespondingObjectFromSource(enemy) : null;
            if (prefab == null)
            {
                report.Warn($"{enemy.name} isn't a prefab instance: it can't be converted.", enemy);
                continue;
            }

            var scene = enemy.scene;
            var marker = new GameObject(enemy.name);
            Undo.RegisterCreatedObjectUndo(marker, "Convert enemy");
            marker.transform.SetParent(GetOrCreateRoot(SpawnsRootName, scene).transform);
            marker.transform.position = enemy.transform.position;

            var spawn = Undo.AddComponent<EnemySpawn>(marker);
            var so = new SerializedObject(spawn);
            so.FindProperty("_prefab").objectReferenceValue = prefab;
            so.FindProperty("_seed").intValue = Random.Range(1, int.MaxValue);
            so.FindProperty("_instance").objectReferenceValue = enemy;
            SetLootProperty(so, ImportLoot(enemy, report));
            so.FindProperty("_lootImported").boolValue = true;
            so.ApplyModifiedProperties();

            foreach (var point in GetPatrolPoints(ai))
            {
                if (point == null) continue;
                if (usage[point] == 1 && point.parent != marker.transform)
                {
                    Undo.SetTransformParent(point, marker.transform, "Move waypoint");
                }
                else
                {
                    var copy = new GameObject(point.name);
                    Undo.RegisterCreatedObjectUndo(copy, "Copy waypoint");
                    copy.transform.SetParent(marker.transform);
                    copy.transform.position = point.position;
                }
            }

            Undo.SetTransformParent(enemy.transform, GetOrCreateRoot(EnemiesRootName, scene).transform, "Move enemy");
            report.Info($"{enemy.name} now has an EnemySpawn with {marker.transform.childCount} waypoints.");
        }
    }

    private static IEnumerable<Transform> GetPatrolPoints(EnemyAI ai)
    {
        var points = new SerializedObject(ai).FindProperty("_patrolPoints");
        for (int i = 0; i < points.arraySize; i++)
            yield return points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
    }

    // Checks that spawns and waypoints are on the floor and that the NavMesh connects them
    public static void Validate(Tilemap floor, LevelBuildReport report)
    {
        foreach (var spawn in FindSpawns())
        {
            CheckOnFloor(floor, spawn.transform, report);

            var path = new NavMeshPath();
            var from = spawn.transform.position;
            foreach (Transform point in spawn.transform)
            {
                CheckOnFloor(floor, point, report);
                NavMesh.CalculatePath(from, point.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathComplete)
                    report.Warn($"{spawn.name}: no path to {point.name} (a wall or a closed door in the way?).", point);
                from = point.position;
            }
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null) CheckOnFloor(floor, player.transform, report);

        var loose = FindLooseEnemies();
        if (loose.Count > 0)
            report.Warn($"{loose.Count} enemies have no EnemySpawn ({string.Join(", ", loose.Select(e => e.name))}): the Level Builder doesn't manage them. Use 'Convert loose enemies'.", loose[0]);
    }

    private static void CheckOnFloor(Tilemap floor, Transform target, LevelBuildReport report)
    {
        if (!floor.HasTile(floor.WorldToCell(target.position)))
            report.Warn($"{target.name} is off the floor.", target);
    }

    private static void SetInstance(EnemySpawn spawn, GameObject instance)
    {
        var so = new SerializedObject(spawn);
        so.FindProperty("_instance").objectReferenceValue = instance;
        so.ApplyModifiedProperties();
    }

    public static GameObject GetOrCreateRoot(string name, Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
        if (root != null) return root;

        root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Create " + name);
        return root;
    }
}
