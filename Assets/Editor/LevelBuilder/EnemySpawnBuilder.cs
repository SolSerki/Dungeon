using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Instancia el enemigo de cada EnemySpawn y le asigna sus waypoints
public static class EnemySpawnBuilder
{
    public const string EnemiesRootName = "Enemigos";
    public const string SpawnsRootName = "EnemySpawns";

    public static EnemySpawn[] FindSpawns() => Object.FindObjectsByType<EnemySpawn>(FindObjectsInactive.Include);

    // Con variantes, sortea una por peso usando la semilla del marcador (mismo resultado en cada Build)
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
        // la instancia puede tener un override; si todavía no existe, vale lo del prefab
        var source = spawn.Instance != null ? spawn.Instance : ResolvePrefab(spawn);
        var ai = source != null ? source.GetComponent<EnemyAI>() : null;
        return ai != null ? new SerializedObject(ai).FindProperty("_detectRange").floatValue : 0f;
    }

    public static void SyncAll(LevelBuildReport report)
    {
        var spawns = FindSpawns();
        foreach (var spawn in spawns) Sync(spawn, report);
        report.Info($"Enemigos sincronizados: {spawns.Length}.");
    }

    public static void Sync(EnemySpawn spawn, LevelBuildReport report)
    {
        if (spawn.Variants != null && spawn.Variants.Length > 0 && spawn.Variants.All(v => v.Prefab == null || v.Weight <= 0f))
            report.Warn($"{spawn.name}: las variantes no tienen prefab o tienen peso 0; uso el Prefab principal.", spawn);

        var prefab = ResolvePrefab(spawn);
        if (prefab == null)
        {
            report.Warn($"{spawn.name} no tiene prefab asignado.", spawn);
            return;
        }

        var instance = spawn.Instance;
        if (!spawn.LootImported) ImportExistingLoot(spawn, report);

        // si cambiaste el prefab o salió otra variante, reemplazamos el enemigo
        if (instance != null && PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
        {
            Undo.DestroyObjectImmediate(instance);
            instance = null;
        }

        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, spawn.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn enemigo");
            instance.transform.SetParent(GetOrCreateRoot(EnemiesRootName, spawn.gameObject.scene).transform);
            instance.name = spawn.name;
            SetInstance(spawn, instance);
        }

        Undo.RecordObject(instance.transform, "Mover enemigo");
        instance.transform.position = spawn.transform.position;
        ApplyLoot(spawn, instance, report);

        var ai = instance.GetComponent<EnemyAI>();
        if (ai == null)
        {
            report.Warn($"El prefab {prefab.name} no tiene EnemyAI.", spawn);
            return;
        }

        var aiSo = new SerializedObject(ai);
        var points = aiSo.FindProperty("_patrolPoints");
        points.arraySize = spawn.transform.childCount;
        for (int i = 0; i < spawn.transform.childCount; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = spawn.transform.GetChild(i);
        aiSo.ApplyModifiedProperties();
    }

    // Los drops del enemigo quedan como los del prefab más el loot marcado en el spawn, con chance 1
    private static void ApplyLoot(EnemySpawn spawn, GameObject instance, LevelBuildReport report)
    {
        var loot = (spawn.Loot ?? new GameObject[0]).Where(p => p != null).ToArray();
        var dropper = instance.GetComponent<LootDropper>();
        if (dropper == null)
        {
            if (loot.Length > 0) report.Warn($"{spawn.name} tiene loot pero {instance.name} no tiene LootDropper: no va a soltar nada.", spawn);
            return;
        }

        var so = new SerializedObject(dropper);
        var drops = so.FindProperty("_drops");
        if (loot.Length == 0)
        {
            // sin loot propio, vuelve a lo que diga el prefab
            if (drops.prefabOverride) PrefabUtility.RevertPropertyOverride(drops, InteractionMode.UserAction);
            return;
        }

        var baseDrops = BaseDrops(dropper);
        drops.arraySize = baseDrops.Count + loot.Length;
        for (int i = 0; i < drops.arraySize; i++)
        {
            var element = drops.GetArrayElementAtIndex(i);
            bool fromPrefab = i < baseDrops.Count;
            element.FindPropertyRelative("Prefab").objectReferenceValue = fromPrefab ? baseDrops[i].prefab : loot[i - baseDrops.Count];
            element.FindPropertyRelative("Chance").floatValue = fromPrefab ? baseDrops[i].chance : 1f;
        }
        so.ApplyModifiedProperties();
    }

    private static List<(GameObject prefab, float chance)> BaseDrops(LootDropper dropper)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(dropper);
        return source != null ? ReadDrops(source) : new List<(GameObject, float)>();
    }

    public static List<(GameObject prefab, float chance)> ReadDrops(LootDropper dropper)
    {
        var drops = new SerializedObject(dropper).FindProperty("_drops");
        var result = new List<(GameObject, float)>();
        for (int i = 0; i < drops.arraySize; i++)
        {
            var element = drops.GetArrayElementAtIndex(i);
            result.Add((element.FindPropertyRelative("Prefab").objectReferenceValue as GameObject, element.FindPropertyRelative("Chance").floatValue));
        }
        return result;
    }

    // Al convertir un enemigo, los drops que le agregaste a mano pasan a ser toggles del spawn
    private static GameObject[] ImportLoot(GameObject enemy, LevelBuildReport report)
    {
        var dropper = enemy.GetComponent<LootDropper>();
        if (dropper == null) return new GameObject[0];

        var remaining = BaseDrops(dropper);
        var loot = new List<GameObject>();
        foreach (var drop in ReadDrops(dropper))
        {
            int match = remaining.IndexOf(drop);
            if (match >= 0) { remaining.RemoveAt(match); continue; } // viene del prefab
            if (drop.prefab == null) continue;

            if (!LootCatalog.Items.Contains(drop.prefab))
                report.Warn($"{enemy.name}: {drop.prefab.name} no está en Assets/Prefabs/Items; lo paso igual como loot seguro.", enemy);
            else if (drop.chance < 1f)
                report.Info($"{enemy.name}: {drop.prefab.name} tenía chance {drop.chance:0.##}; ahora cae siempre.");
            if (!loot.Contains(drop.prefab)) loot.Add(drop.prefab);
        }
        return loot.ToArray();
    }

    // Un spawn creado antes de que existiera el loot: lo que se le cargó a mano al enemigo pasa a los toggles,
    // así el Build no lo borra. Se hace una sola vez; después mandan los toggles.
    public static void ImportExistingLoot(EnemySpawn spawn, LevelBuildReport report)
    {
        var loot = (spawn.Loot ?? new GameObject[0]).Where(p => p != null).ToList();
        if (spawn.Instance != null)
            foreach (var item in ImportLoot(spawn.Instance, report))
                if (!loot.Contains(item)) loot.Add(item);

        var so = new SerializedObject(spawn);
        SetLootProperty(so, loot.ToArray());
        so.FindProperty("_lootImported").boolValue = true;
        so.ApplyModifiedProperties();
        if (loot.Count > 0) report.Info($"{spawn.name}: traje su loot ({string.Join(", ", loot.Select(l => l.name))}) a los toggles del spawn.");
    }

    private static void SetLootProperty(SerializedObject spawn, GameObject[] loot)
    {
        var property = spawn.FindProperty("_loot");
        property.arraySize = loot.Length;
        for (int i = 0; i < loot.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = loot[i];
    }

    // Enemigos puestos a mano (sin EnemySpawn): les crea un marcador y les pasa sus waypoints.
    // El enemigo se conserva tal cual, con sus overrides (por ejemplo, el loot).
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
            report.Info("No hay enemigos sueltos para convertir.");
            return;
        }

        // un waypoint que usan varios enemigos se copia en vez de moverse
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
                report.Warn($"{enemy.name} no es una instancia de prefab: no lo puedo convertir.", enemy);
                continue;
            }

            var scene = enemy.scene;
            var marker = new GameObject(enemy.name);
            Undo.RegisterCreatedObjectUndo(marker, "Convertir enemigo");
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
                    Undo.SetTransformParent(point, marker.transform, "Mover waypoint");
                }
                else
                {
                    var copy = new GameObject(point.name);
                    Undo.RegisterCreatedObjectUndo(copy, "Copiar waypoint");
                    copy.transform.SetParent(marker.transform);
                    copy.transform.position = point.position;
                }
            }

            Undo.SetTransformParent(enemy.transform, GetOrCreateRoot(EnemiesRootName, scene).transform, "Mover enemigo");
            report.Info($"{enemy.name} ahora tiene un EnemySpawn con {marker.transform.childCount} waypoints.");
        }
    }

    private static IEnumerable<Transform> GetPatrolPoints(EnemyAI ai)
    {
        var points = new SerializedObject(ai).FindProperty("_patrolPoints");
        for (int i = 0; i < points.arraySize; i++)
            yield return points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
    }

    // Revisa que spawns y waypoints estén sobre el piso y que el NavMesh los conecte
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
                    report.Warn($"{spawn.name}: no hay camino hasta {point.name} (¿una pared o una puerta cerrada en el medio?).", point);
                from = point.position;
            }
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null) CheckOnFloor(floor, player.transform, report);

        var loose = FindLooseEnemies();
        if (loose.Count > 0)
            report.Warn($"Hay {loose.Count} enemigos sin EnemySpawn ({string.Join(", ", loose.Select(e => e.name))}): el Level Builder no los maneja. Usá 'Convertir enemigos sueltos'.", loose[0]);
    }

    private static void CheckOnFloor(Tilemap floor, Transform target, LevelBuildReport report)
    {
        if (!floor.HasTile(floor.WorldToCell(target.position)))
            report.Warn($"{target.name} está fuera del piso.", target);
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
        Undo.RegisterCreatedObjectUndo(root, "Crear " + name);
        return root;
    }
}
