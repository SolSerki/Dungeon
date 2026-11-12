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
    [SerializeField] private Vector2Int _pocketSize = new(30, 30);

    private GameObject[] _enemyPrefabs = new GameObject[0];
    private GameObject[] _doorPrefabs = new GameObject[0];
    private LevelBuildReport _report;
    private List<LevelRoomAnalyzer.Room> _rooms;
    private Vector2 _scroll;
    private GUIStyle _roomLabelStyle;
    private GUIStyle _warningLabelStyle;

    [MenuItem("Tools/Dungeon/Level Builder")]
    private static void Open() => GetWindow<LevelBuilderWindow>("Level Builder");

    [MenuItem("Tools/Dungeon/Build Level _F6")]
    private static void BuildFromMenu() => GetWindow<LevelBuilderWindow>("Level Builder").BuildAll();

    private void OnEnable()
    {
        FindReferences();
        EditorSceneManager.activeSceneChangedInEditMode += HandleSceneChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        SceneView.duringSceneGui += DrawRoomsInScene;
        SceneView.duringSceneGui += DrawWarningsInScene;
        ScenePlacement.Changed += Repaint;
    }

    private void OnDisable()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= HandleSceneChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        SceneView.duringSceneGui -= DrawRoomsInScene;
        SceneView.duringSceneGui -= DrawWarningsInScene;
        ScenePlacement.Changed -= Repaint;
        ScenePlacement.Cancel(); // placement previews use this window
    }

    // Entering and leaving Play mode reloads the scene: old references end up destroyed
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
        if (_floor == null) _floor = tilemaps.FirstOrDefault(t => t.name == "Floor");
        if (_walls == null) _walls = tilemaps.FirstOrDefault(t => t.name == "Walls");
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

        // While placing something: a reminder and a way to cancel
        var placing = ScenePlacement.Current;
        if (placing != null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox($"{placing.Label}: click in the scene. Shift+click to keep placing, Esc to finish.", MessageType.None);
                if (GUILayout.Button("Cancel", GUILayout.Width(70), GUILayout.Height(38))) ScenePlacement.Cancel();
            }
        }

        EditorGUILayout.HelpBox(
            "1. Paint only the floor in 'Floor' (any floor tile).\n" +
            "2. Props go by hand in 'Props': the Level Builder never touches them.\n" +
            "3. The \"Place…\" buttons show a preview in the scene: click to place, Shift+click to keep going, Esc to exit.\n" +
            "4. Enemies: place the spawn, then Shift+click in the scene to add its waypoints.\n" +
            "5. Doors: place it in the gap (R rotates it); it snaps cell by cell.\n" +
            "6. Pockets: paint sheet 0 and join mouths in pairs. Tunnels: two loose mouths that connect corridors.\n" +
            "7. Build (F6): pockets, tunnels, edges, walls, fill, enemies, doors and NavMesh.",
            MessageType.Info);

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        _floor = (Tilemap)EditorGUILayout.ObjectField("Floor", _floor, typeof(Tilemap), true);
        _walls = (Tilemap)EditorGUILayout.ObjectField("Walls", _walls, typeof(Tilemap), true);
        _rules = (LevelTileRules)EditorGUILayout.ObjectField("Tile rules", _rules, typeof(LevelTileRules), false);
        _margin = EditorGUILayout.IntSlider(new GUIContent("Fill margin", "Dark fill tiles around the floor"), _margin, 0, 10);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_floor == null || _walls == null || _rules == null))
        {
            if (GUILayout.Button("Build Level (F6)", GUILayout.Height(32))) BuildAll();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Walls only"))
                {
                    Run(r =>
                    {
                        var floors = LevelFloors.Collect(_floor, _walls);
                        PocketBuilder.BuildAll(floors, r);
                        LevelAutoTiler.Build(floors, _rules, _margin, r);
                    });
                }
                if (GUILayout.Button("Enemies only")) Run(EnemySpawnBuilder.SyncAll);
                if (GUILayout.Button("NavMesh only")) Run(BakeNavMesh);
            }
        }

        DrawEnemiesSection();
        DrawDoorsSection();
        DrawPocketsSection();
        DrawRoomsSection();
        DrawReport();
        EditorGUILayout.EndScrollView();
    }

    private void DrawEnemiesSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
        if (_enemyPrefabs.Length == 0)
        {
            EditorGUILayout.HelpBox("No prefabs with EnemyAI found in Assets/Prefabs.", MessageType.Warning);
        }
        else
        {
            _prefabIndex = EditorGUILayout.Popup("Prefab", _prefabIndex, _enemyPrefabs.Select(p => p.name).ToArray());
            if (GUILayout.Button("Place spawn in the scene…")) BeginSpawnPlacement(_enemyPrefabs[_prefabIndex]);
        }

        int loose = EnemySpawnBuilder.FindLooseEnemies().Count;
        using (new EditorGUI.DisabledScope(loose == 0))
        {
            if (GUILayout.Button(loose == 0 ? "No loose enemies" : $"Convert {loose} loose enemies to spawns"))
                Run(EnemySpawnBuilder.ConvertLooseEnemies);
        }

        EditorGUI.BeginChangeCheck();
        LevelBuilderPrefs.SnapWaypoints = EditorGUILayout.Toggle("Snap waypoints to cells", LevelBuilderPrefs.SnapWaypoints);
        LevelBuilderPrefs.ShowAllRanges = EditorGUILayout.Toggle(new GUIContent("Show all ranges", "Shows the detection range of every spawn, not just the selected one"), LevelBuilderPrefs.ShowAllRanges);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
    }

    private void DrawDoorsSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Doors", EditorStyles.boldLabel);
        if (_doorPrefabs.Length == 0)
        {
            EditorGUILayout.HelpBox("No prefabs with LockedDoor found in Assets/Prefabs.", MessageType.Warning);
            return;
        }

        _doorPrefabIndex = EditorGUILayout.Popup("Prefab", _doorPrefabIndex, _doorPrefabs.Select(p => p.name).ToArray());
        if (GUILayout.Button("Place door in the scene…")) BeginDoorPlacement(_doorPrefabs[_doorPrefabIndex]);
        EditorGUILayout.LabelField("When selected, it drags cell by cell and rotates with the scene buttons or Shift+R. Rotating swaps between the front door and the side door.", EditorStyles.wordWrappedMiniLabel);

        int rotated = DoorPlacement.FindRotatedWithCounterpart().Length;
        if (rotated > 0 && GUILayout.Button($"Replace {rotated} rotated doors with the side door"))
            Run(DoorPlacement.ReplaceRotated);
    }

    private void OnSelectionChange() => Repaint(); // the mouth buttons depend on the selection

    private void DrawPocketsSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pockets and tunnels", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Each pocket has its own floor: move it and it moves as a whole, and it can be saved as a prefab. Paint its sheet 0 with " +
            "\"Paint in…\", put a mouth at the end of each corridor that crosses over and join them in pairs: leaving through one is entering through the other. " +
            "F6 generates the rotated sheets, the overlaps and the seams (after moving a pocket, press F6 again).",
            EditorStyles.wordWrappedMiniLabel);

        using (new EditorGUI.DisabledScope(_floor == null))
        {
            _pocketSize = EditorGUILayout.Vector2IntField(new GUIContent("Pocket size", "Size guide in cells: shown while placing it and kept as a dotted outline so you know how far to paint."), _pocketSize);
            _pocketSize = new Vector2Int(Mathf.Clamp(_pocketSize.x, 4, 200), Mathf.Clamp(_pocketSize.y, 4, 200));
            if (GUILayout.Button("Place pocket in the scene…")) BeginPocketPlacement();
        }

        var pocket = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Pocket>() : null;
        if (pocket != null && pocket.IsModular)
        {
            if (GUILayout.Button($"Paint in {pocket.name}")) PocketModular.PaintInto(pocket);
        }
        else if (pocket != null && _floor != null)
        {
            if (GUILayout.Button($"Convert {pocket.name} to modular (moves its floor and props to its own tilemaps)"))
            {
                var target = pocket;
                Run(r => PocketModular.ConvertToModular(target, _floor, _walls, PocketBuilder.FindLevelProps(), r));
            }
        }
        if (pocket != null)
        {
            if (GUILayout.Button($"Place mouth in {pocket.name}…")) BeginMouthPlacement(pocket, tunnel: false);
        }
        else
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Place tunnel (2 mouths)…")) BeginMouthPlacement(null, tunnel: true);
                if (GUILayout.Button("Place loose mouth…")) BeginMouthPlacement(null, tunnel: false);
            }
        }
        EditorGUILayout.LabelField(
            "Tunnels connect two corridors anywhere in the level. \"Place tunnel\" takes two clicks (one mouth, then the other) and joins them " +
            "automatically; they must face opposite directions (one opens right and the other left, or up and down).",
            EditorStyles.wordWrappedMiniLabel);

        var selected = Selection.gameObjects.Select(g => g.GetComponent<PocketMouth>()).Where(m => m != null).ToArray();
        using (new EditorGUI.DisabledScope(selected.Length != 2))
        {
            if (GUILayout.Button(selected.Length == 2 ? $"Join {selected[0].name} ↔ {selected[1].name}" : "Join 2 mouths (select two)"))
                JoinMouths(selected[0], selected[1]);
        }
    }

    private Pocket CreatePocket(Vector3 at, Vector2Int size)
    {
        var pocket = PocketModular.Create(at, _floor, _walls, PocketBuilder.FindLevelProps());
        Undo.RecordObject(pocket, "New pocket");
        pocket.GuideSize = size;
        Selection.activeGameObject = pocket.gameObject;
        return pocket;
    }

    // With a pocket the mouth belongs to it; without one it's a loose mouth (tunnel) under "Tunnels"
    private static PocketMouth CreateMouth(Pocket pocket, Vector3 at)
    {
        var parent = pocket != null ? pocket.transform : PocketBuilder.GetOrCreateConnectionsRoot().transform;
        int index = parent.GetComponentsInChildren<PocketMouth>().Length;
        var go = new GameObject(pocket != null ? $"Mouth {(char)('A' + index % 26)}" : $"Tunnel {index / 2 + 1}{(index % 2 == 0 ? "A" : "B")}");
        Undo.RegisterCreatedObjectUndo(go, "New mouth");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(at.x, at.y, 0f);
        var mouth = Undo.AddComponent<PocketMouth>(go);
        Selection.activeGameObject = go;
        return mouth;
    }

    // ---- "Place" mode: a preview follows the mouse and a click places the element ----

    private static readonly Color Ok = new(0.35f, 1f, 0.45f, 0.95f);
    private static readonly Color Bad = new(1f, 0.35f, 0.3f, 0.95f);
    private static readonly Color Careful = new(1f, 0.75f, 0.2f, 0.95f);

    private HashSet<Vector2Int> FloorSnapshot() =>
        _floor != null ? new HashSet<Vector2Int>(LevelFloors.Collect(_floor, _walls).Cells) : new HashSet<Vector2Int>();

    private static Vector3 CellCenter(Vector3 p) => new(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f, 0f);
    private static Vector2Int CellOf(Vector3 p) => Vector2Int.FloorToInt(p);

    private void BeginSpawnPlacement(GameObject prefab)
    {
        var floor = FloorSnapshot();
        var ai = prefab.GetComponent<EnemyAI>();
        float range = ai != null ? new SerializedObject(ai).FindProperty("_detectRange").floatValue : 0f;
        ScenePlacement.Begin(new ScenePlacement.Mode
        {
            Label = $"Place spawn: {prefab.name}",
            Snap = CellCenter,
            Draw = p =>
            {
                bool onFloor = floor.Contains(CellOf(p));
                Handles.color = onFloor ? Ok : Bad;
                Handles.DrawWireDisc(p, Vector3.forward, 0.45f, 3f);
                Handles.DrawSolidDisc(p, Vector3.forward, 0.12f);
                if (range > 0f)
                {
                    Handles.color = new Color(1f, 0.8f, 0.2f, 0.45f);
                    Handles.DrawWireDisc(p, Vector3.forward, range, 2f);
                }
            },
            Status = p => floor.Contains(CellOf(p)) ? $"Detection range: {range:0.#} cells (yellow circle)" : "<color=#ff6655>Off the floor</color>",
            Place = p => CreateSpawn(prefab, p),
        });
    }

    private void BeginDoorPlacement(GameObject prefab)
    {
        var floor = FloorSnapshot();
        var current = prefab;
        var scene = SceneManager.GetActiveScene();

        RectInt Cells(Vector3 center)
        {
            var size = DoorPlacement.Footprint(current.GetComponent<LockedDoor>(), 0f);
            return new RectInt(Mathf.RoundToInt(center.x - size.x * 0.5f), Mathf.RoundToInt(center.y - size.y * 0.5f), size.x, size.y);
        }
        bool OnFloor(RectInt r)
        {
            foreach (var c in r.allPositionsWithin)
                if (!floor.Contains(c)) return false;
            return true;
        }

        ScenePlacement.Begin(new ScenePlacement.Mode
        {
            Label = "Place door",
            Snap = p => DoorPlacement.SnapPosition(current.GetComponent<LockedDoor>(), p, 0f),
            Draw = p =>
            {
                var cells = Cells(p);
                bool ok = OnFloor(cells);
                ScenePlacement.DrawCellRect(cells, (ok ? Ok : Careful) * new Color(1f, 1f, 1f, 0.25f), ok ? Ok : Careful);
            },
            Status = p =>
            {
                var cells = Cells(p);
                string where = OnFloor(cells) ? "on the floor" : "<color=#ffbb33>not fully on the floor</color>";
                return $"{current.name} ({cells.width}x{cells.height}), {where}";
            },
            OnRotate = () =>
            {
                // Rotate = swap to the side version (2x1 <-> 1x2), same as in the scene
                var size = DoorPlacement.Footprint(current.GetComponent<LockedDoor>(), 0f);
                var other = DoorPlacement.FindDoorPrefabs().FirstOrDefault(d => d != current &&
                    DoorPlacement.Footprint(d.GetComponent<LockedDoor>(), 0f) == new Vector2Int(size.y, size.x));
                if (other != null) current = other;
            },
            Place = p => Selection.activeGameObject = DoorPlacement.Create(current, p, scene),
        });
    }

    private void BeginPocketPlacement()
    {
        var floor = FloorSnapshot();
        var size = _pocketSize;
        const int margin = 13; // what gets copied behind each mouth: best kept empty
        Vector3 cachedAt = new(float.NaN, 0f);
        int clash = 0, near = 0;

        void Measure(Vector3 at)
        {
            if (at == cachedAt) return;
            cachedAt = at;
            var rect = Pocket.GuideRectAt(at, size);
            var outer = new RectInt(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2, rect.height + margin * 2);
            clash = near = 0;
            foreach (var c in floor)
            {
                if (rect.Contains(c)) clash++;
                else if (outer.Contains(c)) near++;
            }
        }

        ScenePlacement.Begin(new ScenePlacement.Mode
        {
            Label = $"Place pocket {size.x}x{size.y}",
            Snap = p => new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f),
            Draw = p =>
            {
                Measure(p);
                var rect = Pocket.GuideRectAt(p, size);
                var outer = new RectInt(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2, rect.height + margin * 2);
                var color = clash > 0 ? Bad : near > 0 ? Careful : new Color(1f, 0.4f, 1f, 0.95f);
                ScenePlacement.DrawCellRect(rect, color * new Color(1f, 1f, 1f, 0.12f), color);
                ScenePlacement.DrawDottedRect(outer, color * new Color(1f, 1f, 1f, 0.7f));
                Handles.Label(new Vector3(rect.xMin, rect.yMax + 0.8f), $"{size.x}x{size.y}", EditorStyles.whiteBoldLabel);

                // Floor that clashes with the pocket
                if (clash > 0)
                    foreach (var c in floor.Where(rect.Contains).Take(600))
                        ScenePlacement.DrawCellRect(new RectInt(c, Vector2Int.one), new Color(1f, 0.2f, 0.2f, 0.45f), Color.clear);
            },
            Status = p =>
            {
                Measure(p);
                if (clash > 0) return $"<color=#ff6655>Overlaps {clash} level floor cells</color>";
                if (near > 0) return $"<color=#ffbb33>Floor within {margin} cells (dotted): it could be seen from the mouths</color>";
                return $"Clear. The dotted line is the area best left empty around it ({margin} cells)";
            },
            Place = p => CreatePocket(p, size),
        });
    }

    // Pocket mouth, loose mouth or tunnel (two clicks: the second mouth joins the first automatically)
    private void BeginMouthPlacement(Pocket pocket, bool tunnel)
    {
        HashSet<Vector2Int> floor;
        if (pocket != null && pocket.IsModular)
        {
            floor = new HashSet<Vector2Int>();
            pocket.Floor.CompressBounds();
            foreach (var c in pocket.Floor.cellBounds.allPositionsWithin)
                if (pocket.Floor.HasTile(c)) floor.Add((Vector2Int)c + pocket.CellOffset);
        }
        else if (pocket != null)
        {
            var area = pocket.Area;
            floor = new HashSet<Vector2Int>(FloorSnapshot().Where(area.Contains));
        }
        else
        {
            // Loose mouths: the level floor, without the pockets
            var pockets = FindObjectsByType<Pocket>(FindObjectsInactive.Exclude);
            floor = new HashSet<Vector2Int>(FloorSnapshot().Where(c => !pockets.Any(p => p.IsModular ? p.HasFloor(c, _floor) : p.Area.Contains(c))));
        }

        PocketMouth first = null;
        Vector2Int firstDir = default;
        string Label() => tunnel ? (first == null ? "Place tunnel: first mouth" : $"Place tunnel: second mouth (joins {first.name})")
                                 : pocket != null ? $"Place mouth in {pocket.name}" : "Place loose mouth";

        var mode = new ScenePlacement.Mode
        {
            Label = Label(),
            Snap = p => p,
            Draw = p =>
            {
                var found = PocketBuilder.PreviewOpening(p, floor.Contains);
                if (found == null)
                {
                    Handles.color = Bad;
                    Handles.DrawWireDisc(p, Vector3.forward, 0.4f, 3f);
                    return;
                }
                bool opposite = first == null || found.Value.dir == -firstDir;
                ScenePlacement.DrawMouth(found.Value.point, found.Value.dir, found.Value.width, opposite ? Ok : Careful);
                if (first != null)
                {
                    Handles.color = new Color(1f, 0.4f, 1f, 0.5f);
                    Handles.DrawDottedLine(first.transform.position, found.Value.point, 4f);
                }
            },
            Status = p =>
            {
                var found = PocketBuilder.PreviewOpening(p, floor.Contains);
                if (found == null) return "<color=#ff6655>No corridor end here: move it closer to the open end of one</color>";
                if (first != null && found.Value.dir != -firstDir)
                    return "<color=#ffbb33>Doesn't face opposite to the first one: the tunnel would rotate the world (only possible inside a pocket)</color>";
                return $"Snaps to the end of a corridor {found.Value.width} wide";
            },
        };

        mode.Place = p =>
        {
            var found = PocketBuilder.PreviewOpening(p, floor.Contains);
            var at = found != null ? (Vector3)found.Value.point : p;
            var mouth = CreateMouth(pocket, at);
            if (found != null) mouth.SetShape(found.Value.dir, found.Value.width);
            if (!tunnel) return;

            if (first == null)
            {
                first = mouth;
                firstDir = found?.dir ?? Vector2Int.zero;
                mode.Label = Label();
                mode.KeepPlacing = true; // the second mouth is still missing
            }
            else
            {
                JoinMouths(first, mouth);
                Selection.objects = new Object[] { first.gameObject, mouth.gameObject };
                ScenePlacement.Cancel();
            }
        };
        ScenePlacement.Begin(mode);
    }

    private static void JoinMouths(PocketMouth a, PocketMouth b)
    {
        var touched = new List<Object> { a, b };
        if (a.Partner != null && a.Partner != b) touched.Add(a.Partner);
        if (b.Partner != null && b.Partner != a) touched.Add(b.Partner);
        Undo.RecordObjects(touched.ToArray(), "Join mouths");

        // The old partners are left unpaired
        if (a.Partner != null && a.Partner != b && a.Partner.Partner == a) a.Partner.Partner = null;
        if (b.Partner != null && b.Partner != a && b.Partner.Partner == b) b.Partner.Partner = null;
        a.Partner = b;
        b.Partner = a;
        foreach (var o in touched) EditorUtility.SetDirty(o);
        SceneView.RepaintAll();
    }

    private void DrawRoomsSection()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Rooms", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_floor == null))
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(80))) RefreshRooms();
            }
        }

        EditorGUI.BeginChangeCheck();
        LevelBuilderPrefs.ShowRooms = EditorGUILayout.Toggle("Show in scene", LevelBuilderPrefs.ShowRooms);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        if (_rooms == null)
        {
            EditorGUILayout.LabelField("Press Refresh or Build to see the summary.", EditorStyles.miniLabel);
            return;
        }

        float maxHealth = _rooms.Count > 0 ? _rooms.Max(r => r.TotalHealth) : 0f;
        foreach (var room in _rooms)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string names = room.EnemyNames.Count == 0 ? "no enemies" : string.Join(", ", room.EnemyNames.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));
                string tag = room.HasPlayer ? "  (start)" : "";
                EditorGUILayout.LabelField(
                    $"Room {room.Number}{tag} — {room.Bounds.width}x{room.Bounds.height}\n" +
                    $"{room.EnemyNames.Count} enemies, total health {room.TotalHealth:0}: {names}",
                    EditorStyles.wordWrappedLabel);

                // bar relative to the most loaded room, to compare at a glance
                var bar = GUILayoutUtility.GetRect(60, 16, GUILayout.Width(60));
                EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.2f));
                if (maxHealth > 0f) EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * room.TotalHealth / maxHealth, bar.height), new Color(0.9f, 0.35f, 0.3f));

                if (GUILayout.Button("Show", GUILayout.Width(40))) FrameRoom(room);
            }
        }
    }

    private void BuildAll()
    {
        FindReferences();
        if (_floor == null || _walls == null || _rules == null)
        {
            Debug.LogWarning("[Level Builder] The scene is missing the 'Floor' and 'Walls' tilemaps.");
            return;
        }

        Run(report =>
        {
            // All the floor: the level's and every modular pocket's
            var floors = LevelFloors.Collect(_floor, _walls);
            PocketBuilder.BuildAll(floors, report); // paints the rotated sheets and the tunnels: before the walls
            LevelAutoTiler.Build(floors, _rules, _margin, report);
            DoorPlacement.SnapAll(report); // before the NavMesh: the door leaves carve it
            EnemySpawnBuilder.SyncAll(report);
            BakeNavMesh(report);
            floors.Refresh();

            // Rooms: without the generated copies (rotated sheets, tunnel continuations), so nothing is counted twice.
            // Each room gets a "Room X" folder and each spawn goes into its room's folder.
            var roomFloor = floors.CreateUnion(includeGenerated: false);
            try
            {
                _rooms = LevelRoomAnalyzer.Analyze(roomFloor);
                SpawnRooms.Sort(_rooms, roomFloor, report);
            }
            finally
            {
                LevelFloors.DestroyUnion(roomFloor);
            }

            // The validators take a single tilemap: they get a temporary one with all the floor merged
            var union = floors.CreateUnion();
            try
            {
                EnemySpawnBuilder.Validate(union, report);
                DoorPlacement.Validate(union, report);
                LevelKeyValidator.Validate(union, report);
            }
            finally
            {
                LevelFloors.DestroyUnion(union);
            }
        });
        RefreshRooms();
    }

    private void RefreshRooms()
    {
        if (_floor == null) return;
        var union = LevelFloors.Collect(_floor, _walls).CreateUnion(includeGenerated: false);
        try
        {
            _rooms = LevelRoomAnalyzer.Analyze(union);
        }
        finally
        {
            LevelFloors.DestroyUnion(union);
        }
        SceneView.RepaintAll();
        Repaint();
    }

    private void BakeNavMesh(LevelBuildReport report)
    {
        var surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            report.Warn("There's no NavMeshSurface in the scene: enemies won't be able to move.");
            return;
        }
        LevelNavMeshBaker.Bake(surface, report);
    }

    // Groups everything into a single Ctrl+Z
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
        SceneView.RepaintAll(); // warning markers
    }

    private void CreateSpawn(GameObject prefab, Vector3 position)
    {
        if (_floor != null) position = _floor.GetCellCenterWorld(_floor.WorldToCell(position));
        position.z = 0f;

        // Straight into its room's folder if it exists (F6 sorts it anyway if it lands in a corridor)
        var parent = SpawnRooms.GroupAt(position);
        if (parent == null) parent = EnemySpawnBuilder.GetOrCreateRoot(EnemySpawnBuilder.SpawnsRootName, SceneManager.GetActiveScene()).transform;
        var go = new GameObject(GameObjectUtility.GetUniqueNameForSibling(parent, prefab.name));
        Undo.RegisterCreatedObjectUndo(go, "New spawn");
        go.transform.SetParent(parent);
        go.transform.position = position;

        var spawn = Undo.AddComponent<EnemySpawn>(go);
        var so = new SerializedObject(spawn);
        so.FindProperty("_prefab").objectReferenceValue = prefab;
        so.FindProperty("_seed").intValue = Random.Range(1, int.MaxValue);
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
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
            Handles.Label(new Vector3(min.x + 0.2f, max.y - 0.2f), $"Room {room.Number}: {room.EnemyNames.Count} enemies · health {room.TotalHealth:0}", _roomLabelStyle);
        }
    }

    private void DrawReport()
    {
        if (_report == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
        foreach (var info in _report.Infos) EditorGUILayout.LabelField(info, EditorStyles.wordWrappedMiniLabel);

        if (_report.Warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("All good.", MessageType.Info);
            return;
        }

        for (int i = 0; i < _report.Warnings.Count; i++)
        {
            var (message, context, place) = _report.Warnings[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                // the number matches the red marker in the scene
                EditorGUILayout.HelpBox(place.HasValue ? $"{i + 1}. {message}" : message, MessageType.Warning);
                if ((context != null || place.HasValue) && GUILayout.Button("Show", GUILayout.Width(40), GUILayout.Height(38)))
                    ShowWarning(context, place);
            }
        }
    }

    private static void ShowWarning(Object context, Vector3? place)
    {
        if (context != null)
        {
            Selection.activeObject = context;
            EditorGUIUtility.PingObject(context);
        }

        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        if (place.HasValue) view.Frame(new Bounds(place.Value, new Vector3(16f, 16f, 0f)), false);
        else view.FrameSelected();
    }

    // Numbered red markers where each problem is
    private void DrawWarningsInScene(SceneView view)
    {
        if (_report == null) return;

        _warningLabelStyle ??= new GUIStyle(EditorStyles.whiteBoldLabel) { fontSize = 13, normal = { textColor = new Color(1f, 0.45f, 0.4f) } };
        for (int i = 0; i < _report.Warnings.Count; i++)
        {
            var place = _report.Warnings[i].place;
            if (!place.HasValue) continue;
            var p = place.Value;
            Handles.color = new Color(1f, 0.3f, 0.25f, 0.9f);
            Handles.DrawWireDisc(p, Vector3.forward, 0.8f, 3f);
            Handles.DrawWireDisc(p, Vector3.forward, 0.15f, 3f);
            Handles.Label(p + new Vector3(0.9f, 0.9f), $"{i + 1}", _warningLabelStyle);
        }
    }
}
