using System.Collections.Generic;
using System.Linq;
using NavMeshPlus.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class LevelBuilderWindow : EditorWindow
{
    private const string RulesPath = "Assets/Editor/LevelBuilder/DungeonTileRules.asset";

    [SerializeField] private Tilemap _floor;
    [SerializeField] private Tilemap _walls;
    [SerializeField] private LevelTileRules _rules;
    [SerializeField] private int _margin = 2;
    [SerializeField] private int _prefabIndex;
    [SerializeField] private int _doorPrefabIndex;

    private GameObject[] _enemyPrefabs = new GameObject[0];
    private GameObject[] _doorPrefabs = new GameObject[0];
    private LevelBuildReport _report;
    private List<LevelRoomAnalyzer.Room> _rooms;
    private Vector2 _scroll;
    private GUIStyle _roomLabelStyle;

    [MenuItem("Tools/Dungeon/Level Builder")]
    private static void Open() => GetWindow<LevelBuilderWindow>("Level Builder");

    [MenuItem("Tools/Dungeon/Build nivel _F6")]
    private static void BuildFromMenu() => GetWindow<LevelBuilderWindow>("Level Builder").BuildAll();

    private void OnEnable()
    {
        FindReferences();
        EditorSceneManager.activeSceneChangedInEditMode += HandleSceneChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        SceneView.duringSceneGui += DrawRoomsInScene;
    }

    private void OnDisable()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= HandleSceneChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        SceneView.duringSceneGui -= DrawRoomsInScene;
    }

    // Al entrar y salir de Play mode Unity recarga la escena: las referencias viejas quedan destruidas
    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
        _floor = null;
        _walls = null;
        _rooms = null;
        FindReferences();
        Repaint();
    }

    private void HandleSceneChanged(Scene previous, Scene current)
    {
        _floor = null;
        _walls = null;
        _report = null;
        _rooms = null;
        FindReferences();
        Repaint();
    }

    private void FindReferences()
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        if (_floor == null) _floor = tilemaps.FirstOrDefault(t => t.name == "Piso");
        if (_walls == null) _walls = tilemaps.FirstOrDefault(t => t.name == "Paredes");
        if (_rules == null) _rules = LoadOrCreateRules();

        _enemyPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(go => go.GetComponent<EnemyAI>() != null)
            .ToArray();
        _prefabIndex = Mathf.Clamp(_prefabIndex, 0, Mathf.Max(0, _enemyPrefabs.Length - 1));

        _doorPrefabs = DoorPlacement.FindDoorPrefabs();
        _doorPrefabIndex = Mathf.Clamp(_doorPrefabIndex, 0, Mathf.Max(0, _doorPrefabs.Length - 1));
    }

    private static LevelTileRules LoadOrCreateRules()
    {
        var rules = AssetDatabase.LoadAssetAtPath<LevelTileRules>(RulesPath);
        if (rules != null) return rules;

        rules = CreateInstance<LevelTileRules>();
        rules.FillDungeonTilesetDefaults();
        AssetDatabase.CreateAsset(rules, RulesPath);
        AssetDatabase.SaveAssets();
        return rules;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "1. Pintá solo el piso en 'Piso' (cualquier tile de piso).\n" +
            "2. Los props van a mano en 'Props': el Level Builder no los toca.\n" +
            "3. Enemigos: 'Nuevo spawn' y después Shift+clic en la escena para sus waypoints.\n" +
            "4. Puertas: 'Nueva puerta' y arrastrala al hueco; salta de a celdas.\n" +
            "5. Build (F6): bordes, paredes, relleno, enemigos, puertas y NavMesh.",
            MessageType.Info);

        EditorGUILayout.LabelField("Referencias", EditorStyles.boldLabel);
        _floor = (Tilemap)EditorGUILayout.ObjectField("Piso", _floor, typeof(Tilemap), true);
        _walls = (Tilemap)EditorGUILayout.ObjectField("Paredes", _walls, typeof(Tilemap), true);
        _rules = (LevelTileRules)EditorGUILayout.ObjectField("Reglas de tiles", _rules, typeof(LevelTileRules), false);
        _margin = EditorGUILayout.IntSlider(new GUIContent("Margen de relleno", "Tiles de relleno oscuro alrededor del piso"), _margin, 0, 10);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_floor == null || _walls == null || _rules == null))
        {
            if (GUILayout.Button("Build nivel completo (F6)", GUILayout.Height(32))) BuildAll();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Solo paredes")) Run(r => LevelAutoTiler.Build(_floor, _walls, _rules, _margin, r));
                if (GUILayout.Button("Solo enemigos")) Run(EnemySpawnBuilder.SyncAll);
                if (GUILayout.Button("Solo NavMesh")) Run(BakeNavMesh);
            }
        }

        DrawEnemiesSection();
        DrawDoorsSection();
        DrawRoomsSection();
        DrawReport();
        EditorGUILayout.EndScrollView();
    }

    private void DrawEnemiesSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemigos", EditorStyles.boldLabel);
        if (_enemyPrefabs.Length == 0)
        {
            EditorGUILayout.HelpBox("No encontré prefabs con EnemyAI en Assets/Prefabs.", MessageType.Warning);
        }
        else
        {
            _prefabIndex = EditorGUILayout.Popup("Prefab", _prefabIndex, _enemyPrefabs.Select(p => p.name).ToArray());
            if (GUILayout.Button("Nuevo spawn en el centro de la vista")) CreateSpawn(_enemyPrefabs[_prefabIndex]);
        }

        int loose = EnemySpawnBuilder.FindLooseEnemies().Count;
        using (new EditorGUI.DisabledScope(loose == 0))
        {
            if (GUILayout.Button(loose == 0 ? "No hay enemigos sueltos" : $"Convertir {loose} enemigos sueltos en spawns"))
                Run(EnemySpawnBuilder.ConvertLooseEnemies);
        }

        EditorGUI.BeginChangeCheck();
        LevelBuilderPrefs.SnapWaypoints = EditorGUILayout.Toggle("Ajustar waypoints a la celda", LevelBuilderPrefs.SnapWaypoints);
        LevelBuilderPrefs.ShowAllRanges = EditorGUILayout.Toggle(new GUIContent("Rango de todos", "Muestra el rango de detección de todos los spawns, no solo del seleccionado"), LevelBuilderPrefs.ShowAllRanges);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
    }

    private void DrawDoorsSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Puertas", EditorStyles.boldLabel);
        if (_doorPrefabs.Length == 0)
        {
            EditorGUILayout.HelpBox("No encontré prefabs con LockedDoor en Assets/Prefabs.", MessageType.Warning);
            return;
        }

        _doorPrefabIndex = EditorGUILayout.Popup("Prefab", _doorPrefabIndex, _doorPrefabs.Select(p => p.name).ToArray());
        if (GUILayout.Button("Nueva puerta en el centro de la vista")) CreateDoor(_doorPrefabs[_doorPrefabIndex]);
        EditorGUILayout.LabelField("Seleccionada, se arrastra de a celdas y se gira con los botones de la escena o Shift+R. Girar cambia entre la puerta de frente y la de costado.", EditorStyles.wordWrappedMiniLabel);

        int rotated = DoorPlacement.FindRotatedWithCounterpart().Length;
        if (rotated > 0 && GUILayout.Button($"Cambiar {rotated} puertas giradas por la de costado"))
            Run(DoorPlacement.ReplaceRotated);
    }

    private void DrawRoomsSection()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Salas", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_floor == null))
            {
                if (GUILayout.Button("Actualizar", GUILayout.Width(80))) RefreshRooms();
            }
        }

        EditorGUI.BeginChangeCheck();
        LevelBuilderPrefs.ShowRooms = EditorGUILayout.Toggle("Mostrar en la escena", LevelBuilderPrefs.ShowRooms);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        if (_rooms == null)
        {
            EditorGUILayout.LabelField("Apretá Actualizar o Build para ver el resumen.", EditorStyles.miniLabel);
            return;
        }

        float maxHealth = _rooms.Count > 0 ? _rooms.Max(r => r.TotalHealth) : 0f;
        foreach (var room in _rooms)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string names = room.EnemyNames.Count == 0 ? "sin enemigos" : string.Join(", ", room.EnemyNames.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));
                string tag = room.HasPlayer ? "  (inicio)" : "";
                EditorGUILayout.LabelField(
                    $"Sala {room.Number}{tag} — {room.Bounds.width}x{room.Bounds.height}\n" +
                    $"{room.EnemyNames.Count} enemigos, vida total {room.TotalHealth:0}: {names}",
                    EditorStyles.wordWrappedLabel);

                // barra relativa a la sala más cargada, para comparar de un vistazo
                var bar = GUILayoutUtility.GetRect(60, 16, GUILayout.Width(60));
                EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.2f));
                if (maxHealth > 0f) EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * room.TotalHealth / maxHealth, bar.height), new Color(0.9f, 0.35f, 0.3f));

                if (GUILayout.Button("Ver", GUILayout.Width(40))) FrameRoom(room);
            }
        }
    }

    private void BuildAll()
    {
        FindReferences();
        if (_floor == null || _walls == null || _rules == null)
        {
            Debug.LogWarning("[Level Builder] Faltan los tilemaps 'Piso' y 'Paredes' en la escena.");
            return;
        }

        Run(report =>
        {
            LevelAutoTiler.Build(_floor, _walls, _rules, _margin, report);
            DoorPlacement.SnapAll(report); // antes del NavMesh: las hojas de la puerta lo cortan
            EnemySpawnBuilder.SyncAll(report);
            BakeNavMesh(report);
            EnemySpawnBuilder.Validate(_floor, report);
            DoorPlacement.Validate(_floor, report);
            LevelKeyValidator.Validate(_floor, report);
        });
        RefreshRooms();
    }

    private void RefreshRooms()
    {
        if (_floor == null) return;
        _rooms = LevelRoomAnalyzer.Analyze(_floor);
        SceneView.RepaintAll();
        Repaint();
    }

    private void BakeNavMesh(LevelBuildReport report)
    {
        var surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            report.Warn("No hay un NavMeshSurface en la escena: los enemigos no van a poder moverse.");
            return;
        }
        LevelNavMeshBaker.Bake(surface, report);
    }

    // Agrupa todo en un solo Ctrl+Z
    private void Run(System.Action<LevelBuildReport> action)
    {
        _report = new LevelBuildReport();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Level Builder");
        int group = Undo.GetCurrentGroup();

        action(_report);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Repaint();
    }

    private void CreateSpawn(GameObject prefab)
    {
        var view = SceneView.lastActiveSceneView;
        var position = view != null ? view.pivot : Vector3.zero;
        if (_floor != null) position = _floor.GetCellCenterWorld(_floor.WorldToCell(position));
        position.z = 0f;

        var root = EnemySpawnBuilder.GetOrCreateRoot(EnemySpawnBuilder.SpawnsRootName, SceneManager.GetActiveScene());
        var go = new GameObject(GameObjectUtility.GetUniqueNameForSibling(root.transform, prefab.name));
        Undo.RegisterCreatedObjectUndo(go, "Nuevo spawn");
        go.transform.SetParent(root.transform);
        go.transform.position = position;

        var spawn = Undo.AddComponent<EnemySpawn>(go);
        var so = new SerializedObject(spawn);
        so.FindProperty("_prefab").objectReferenceValue = prefab;
        so.FindProperty("_seed").intValue = Random.Range(1, int.MaxValue);
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
    }

    private void CreateDoor(GameObject prefab)
    {
        var view = SceneView.lastActiveSceneView;
        var position = view != null ? view.pivot : Vector3.zero;
        Selection.activeGameObject = DoorPlacement.Create(prefab, position, SceneManager.GetActiveScene());
    }

    private void FrameRoom(LevelRoomAnalyzer.Room room)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || _floor == null) return;

        var min = _floor.CellToWorld(new Vector3Int(room.Bounds.xMin, room.Bounds.yMin, 0));
        var max = _floor.CellToWorld(new Vector3Int(room.Bounds.xMax, room.Bounds.yMax, 0));
        var bounds = new Bounds((min + max) / 2f, max - min);
        view.Frame(bounds, false);
    }

    private void DrawRoomsInScene(SceneView view)
    {
        if (_rooms == null || _floor == null || !LevelBuilderPrefs.ShowRooms) return;

        _roomLabelStyle ??= new GUIStyle(EditorStyles.whiteBoldLabel) { fontSize = 13 };
        foreach (var room in _rooms)
        {
            var min = _floor.CellToWorld(new Vector3Int(room.Bounds.xMin, room.Bounds.yMin, 0));
            var max = _floor.CellToWorld(new Vector3Int(room.Bounds.xMax, room.Bounds.yMax, 0));
            var corners = new[] { new Vector3(min.x, min.y), new Vector3(min.x, max.y), new Vector3(max.x, max.y), new Vector3(max.x, min.y) };
            Handles.DrawSolidRectangleWithOutline(corners, new Color(0.3f, 0.7f, 1f, 0.04f), new Color(0.3f, 0.7f, 1f, 0.6f));
            Handles.Label(new Vector3(min.x + 0.2f, max.y - 0.2f), $"Sala {room.Number}: {room.EnemyNames.Count} enemigos · vida {room.TotalHealth:0}", _roomLabelStyle);
        }
    }

    private void DrawReport()
    {
        if (_report == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
        foreach (var info in _report.Infos) EditorGUILayout.LabelField(info, EditorStyles.wordWrappedMiniLabel);

        if (_report.Warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("Todo en orden.", MessageType.Info);
            return;
        }

        foreach (var (message, context) in _report.Warnings)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (context != null && GUILayout.Button("Ver", GUILayout.Width(40), GUILayout.Height(38)))
                {
                    Selection.activeObject = context;
                    EditorGUIUtility.PingObject(context);
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }
    }
}
