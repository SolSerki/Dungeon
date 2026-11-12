using System.Collections.Generic;
using System.Linq;
using NavMeshPlus.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds the pockets (Pocket). From the painted sheet 0 and the joined mouths:
// - works out which rotations can be reached by crossing mouths (1, 2 or 4 sheets),
// - paints the rotated sheets far from the level (Build re-tiles the walls, so the 3/4 view stays right),
// - paints an overlap behind each mouth: a copy of what's on the other side, so the crossing can't be noticed,
// - creates the pieces (PieceInstance) for the trail and the seams (PortalSeam) for the teleport.
// Runs in Build before the walls because it paints floor. Everything generated is cleared and rebuilt.
public static class PocketBuilder
{
    private const string GeneratedName = "Generated";
    private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private static readonly Vector2 Half = new(0.5f, 0.5f);
    private static readonly Color[] SheetColors =
    {
        new(0f, 1f, 1f, 0.8f), new(1f, 0.85f, 0.2f, 0.8f), new(0.5f, 1f, 0.3f, 0.8f), new(1f, 0.5f, 0.3f, 0.8f),
    };

    private class Mouth
    {
        public PocketMouth Component;
        public Vector2Int Dir;          // pointing out of the corridor
        public List<Vector2Int> Cells;  // left to right, looking outward
        public Vector2 Point;           // center of the mouth line
        public string Name => Component.name;
    }

    // Rigid motion on the grid: cell L -> T + Rot(L, R). Sheets, overlaps and the
    // "glue" between two mouths are all of this type, so they compose with each other.
    private readonly struct Rigid
    {
        public readonly Vector2Int T;
        public readonly int R;

        public Rigid(Vector2Int t, int r)
        {
            T = t;
            R = Mod4(r);
        }

        public Vector2Int Cell(Vector2Int c) => T + Rot(c, R);
        public Vector2 Point(Vector2 p) => T + Half + Rot(p - Half, R);
        public Rigid Then(Rigid next) => new(next.T + Rot(T, next.R), R + next.R); // this first, then next
        public Rigid Inverse() => new(-Rot(T, -R), -R);
    }

    private class Extension
    {
        public string Name;
        public Rigid Map;                   // sheet 0 -> behind the mouth (still in sheet 0's frame)
        public List<Vector2Int> Sources;    // sheet 0 cells that get copied
    }

    public const string ConnectionsRootName = "Tunnels";

    public const string GeneratedRootName = "Pockets (generated)";

    public static void BuildAll(Tilemap floor, LevelBuildReport report) => BuildAll(LevelFloors.Collect(floor, null), report);

    public static void BuildAll(LevelFloors floors, LevelBuildReport report)
    {
        var floor = floors.MainFloor;
        var pockets = Object.FindObjectsByType<Pocket>(FindObjectsInactive.Exclude).OrderBy(p => p.name).ToArray();
        var loose = Object.FindObjectsByType<PocketMouth>(FindObjectsInactive.Exclude)
            .Where(m => m.GetComponentInParent<Pocket>() == null).OrderBy(m => m.name).ToList();
        var connectionRoots = Object.FindObjectsByType<MazeConnections>(FindObjectsInactive.Exclude);
        var generatedRoots = Object.FindObjectsByType<MazeGenerated>(FindObjectsInactive.Exclude);
        if (pockets.Length == 0 && loose.Count == 0 && connectionRoots.Length == 0 && generatedRoots.Length == 0) return;

        var props = FindLevelProps();
        Undo.RegisterCompleteObjectUndo(props != null ? new Object[] { floor, props } : new Object[] { floor }, "Pockets and tunnels");
        foreach (var root in connectionRoots) ClearConnections(root, floor, props);
        foreach (var root in generatedRoots) ClearGenerated(root, floor);
        foreach (var pocket in pockets) Clear(pocket, floor);
        floors.Refresh();

        foreach (var group in pockets.GroupBy(p => p.name).Where(g => g.Count() > 1))
            report.Warn($"{group.Count()} pockets are named \"{group.Key}\". The trail is stored by pocket name, " +
                        "so it would get mixed up between them. Give each one its own name.", group.First());

        // Tunnels are part of the level: they go before the rotated sheets, which are placed to the right of everything
        if (loose.Count > 0)
        {
            BuildConnections(connectionRoots.Length > 0 ? connectionRoots[0] : GetOrCreateConnectionsRoot(), loose, floors, props, pockets, report);
            floors.Refresh();
        }
        if (pockets.Length == 0) return;

        // Generated pocket content lives outside the pockets (so a modular pocket can be saved as a prefab)
        var generated = generatedRoots.Length > 0 ? generatedRoots[0] : CreateGeneratedRoot();
        var generatedCells = new List<Vector2Int>();
        int nextX = floors.Count > 0 ? floors.Cells.Max(c => c.x) : 0;
        foreach (var pocket in pockets) Build(pocket, floors, props, generated.transform, generatedCells, ref nextX, report);

        Undo.RecordObject(generated, "Pockets");
        generated.FloorCells = generatedCells.ToArray();
    }

    public static Tilemap FindLevelProps() =>
        Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "Props");

    private static MazeGenerated CreateGeneratedRoot()
    {
        var go = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(go, "Pockets");
        return Undo.AddComponent<MazeGenerated>(go);
    }

    private static void ClearGenerated(MazeGenerated root, Tilemap floor)
    {
        foreach (var c in root.FloorCells) floor.SetTile((Vector3Int)c, null);
        if (root.FloorCells.Length > 0)
        {
            Undo.RecordObject(root, "Pockets");
            root.FloorCells = null;
        }
        foreach (Transform child in root.transform.Cast<Transform>().ToList()) Undo.DestroyObjectImmediate(child.gameObject);
    }

    // The "Tunnels" object that groups the loose mouths and keeps what they paint
    public static MazeConnections GetOrCreateConnectionsRoot()
    {
        var existing = Object.FindAnyObjectByType<MazeConnections>(FindObjectsInactive.Exclude);
        if (existing != null) return existing;
        var go = new GameObject(ConnectionsRootName);
        Undo.RegisterCreatedObjectUndo(go, "Tunnels");
        return Undo.AddComponent<MazeConnections>(go);
    }

    private static void ClearConnections(MazeConnections root, Tilemap floor, Tilemap props)
    {
        foreach (var c in root.GeneratedFloor) floor.SetTile((Vector3Int)c, null);
        if (props != null)
            foreach (var c in root.GeneratedProps) props.SetTile((Vector3Int)c, null);
        if (root.GeneratedFloor.Length > 0 || root.GeneratedProps.Length > 0)
        {
            Undo.RecordObject(root, "Tunnels");
            root.GeneratedFloor = null;
            root.GeneratedProps = null;
        }
        var old = root.transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    // Tunnels: loose mouths joined in pairs, anywhere in the level. With no rotation there's no need for
    // sheets: painting the other corridor's continuation behind each mouth is enough.
    private static void BuildConnections(MazeConnections root, List<PocketMouth> loose, LevelFloors floors, Tilemap props, Pocket[] pockets, LevelBuildReport report)
    {
        const string owner = "Tunnels";
        var floor = floors.MainFloor;
        int overlap = root.Overlap;
        // A legacy pocket covers its whole rectangle; a modular one, only its floor
        Pocket PocketAt(Vector2Int c) => pockets.FirstOrDefault(p => p.IsModular ? p.HasFloor(c, floor) : p.Area.Contains(c));
        bool IsFloor(Vector2Int c) => floors.Has(c) && PocketAt(c) == null;
        Pocket PocketNear(Vector2 p)
        {
            var center = Vector2Int.FloorToInt(p);
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    var found = PocketAt(center + new Vector2Int(dx, dy));
                    if (found != null) return found;
                }
            return null;
        }

        // 1. Mouths
        var mouths = new Dictionary<PocketMouth, Mouth>();
        foreach (var component in loose)
        {
            var inPocket = PocketNear(component.transform.position);
            if (inPocket != null)
            {
                report.Warn($"{owner}/{component.name} is on the floor of {inPocket.name}, but it isn't a child of the pocket. If it belongs to the " +
                            $"pocket, drag it into {inPocket.name} in the hierarchy; if it's a tunnel, put it in a level corridor.",
                            component, component.transform.position);
                continue;
            }
            var mouth = Resolve(component, IsFloor);
            if (mouth == null)
            {
                report.Warn($"{owner}/{component.name}: {Diagnose(component, null, IsFloor)}", component, component.transform.position);
                continue;
            }
            Undo.RecordObject(component.transform, "Snap mouth");
            Undo.RecordObject(component, "Snap mouth");
            component.transform.position = new Vector3(mouth.Point.x, mouth.Point.y, 0f);
            component.SetShape(mouth.Dir, mouth.Cells.Count);
            mouths[component] = mouth;
        }

        // 2. Pairs
        var pairs = MakePairs(mouths, owner, report);

        var generatedRoot = new GameObject(GeneratedName);
        Undo.RegisterCreatedObjectUndo(generatedRoot, "Generate tunnels");
        generatedRoot.transform.SetParent(root.transform, false);
        generatedRoot.transform.position = Vector3.zero;

        var generatedFloor = new List<Vector2Int>();
        var generatedSet = new HashSet<Vector2Int>();
        var generatedProps = new List<Vector2Int>();
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        int built = 0;

        foreach (var (a, b, glue) in pairs)
        {
            if (glue.R != 0)
            {
                string turn = glue.R == 2 ? "180°" : "90°";
                report.Warn($"{owner}: {a.Name} is {DirName(a.Dir)} and {b.Name} is {DirName(b.Dir)}. They don't face opposite directions, so " +
                            $"crossing would rotate the whole world {turn}, and that's only possible inside a pocket. Put one mouth at the end of a corridor that " +
                            "opens to the right and the other at the end of one that opens to the left (or up and down). If you want the rotation, " +
                            "build it inside a pocket.", a.Component, a.Point);
                continue;
            }

            // There must be room behind each mouth: that's where the other corridor's continuation is painted
            bool blocked = false;
            foreach (var (at, other) in new[] { (a, b), (b, a) })
            {
                var behind = BoxCells(at, overlap, inside: false).Where(c => floors.Has(c) || floor.HasTile((Vector3Int)c))
                    .OrderBy(c => (c + Half - at.Point).sqrMagnitude).ToList();
                if (behind.Count == 0) continue;
                blocked = true;
                report.Warn($"{owner}: behind {at.Name} (where its arrow points) there's floor from another part of the level within {overlap} cells. " +
                            $"The continuation of {other.Name}'s corridor goes there, so it can't be painted and the jump would show when crossing. " +
                            $"Leave at least {overlap} empty cells behind the mouth.", at.Component, (Vector3)(behind[0] + Half));
            }
            if (blocked) continue;

            string id = $"Tunnel {a.Name} ↔ {b.Name}";
            var pairGo = new GameObject(id);
            pairGo.transform.SetParent(generatedRoot.transform, false);

            var insideA = BoxCells(a, overlap, inside: true).Where(IsFloor).ToList();
            var insideB = BoxCells(b, overlap, inside: true).Where(IsFloor).ToList();
            var inverse = glue.Inverse();

            // Behind A goes what's inside B, and behind B what's inside A
            List<Vector2Int> PaintCopy(List<Vector2Int> sources, Rigid map)
            {
                var painted = new List<Vector2Int>();
                foreach (var src in sources)
                {
                    var target = map.Cell(src);
                    if (floors.Has(target) || floor.HasTile((Vector3Int)target)) continue;
                    floor.SetTile((Vector3Int)target, floors.Get(src));
                    if (generatedSet.Add(target)) generatedFloor.Add(target);
                    painted.Add(target);
                }
                return painted;
            }

            // Props (torches, decoration) too: with no rotation they keep the right orientation
            void CopyProps(Mouth from, Rigid map)
            {
                if (props == null) return;
                foreach (var src in BoxCells(from, overlap, inside: true))
                {
                    var tile = props.GetTile((Vector3Int)src);
                    if (tile == null) continue;
                    var target = (Vector3Int)map.Cell(src);
                    if (props.HasTile(target)) continue;
                    props.SetTile(new TileChangeData(target, tile, props.GetColor((Vector3Int)src), props.GetTransformMatrix((Vector3Int)src)), true);
                    generatedProps.Add((Vector2Int)target);
                }
            }

            var behindA = PaintCopy(insideB, glue);
            var behindB = PaintCopy(insideA, inverse);
            CopyProps(b, glue);
            CopyProps(a, inverse);

            // Pieces: both real sides and their copies share the same id, so the trail shows up in both
            var color = SheetColors[built % SheetColors.Length];
            var identity = new Rigid(Vector2Int.zero, 0);
            CreatePiece(pairGo.transform, $"Side {a.Name}", id, identity, Vector2Int.zero, insideA, color);
            CreatePiece(pairGo.transform, $"Side {b.Name}", id, identity, Vector2Int.zero, insideB, color);
            if (behindA.Count > 0) CreatePiece(pairGo.transform, $"Continuation behind {a.Name}", id, glue, Vector2Int.zero, behindA, color);
            if (behindB.Count > 0) CreatePiece(pairGo.transform, $"Continuation behind {b.Name}", id, inverse, Vector2Int.zero, behindB, color);

            var seamA = CreateSeam(pairGo.transform, $"{a.Name} → {b.Name}", a.Point, a.Dir, a.Cells.Count, ignoreRaycast);
            var seamB = CreateSeam(pairGo.transform, $"{b.Name} → {a.Name}", b.Point, b.Dir, b.Cells.Count, ignoreRaycast);
            Link(seamA, seamB);
            Link(seamB, seamA);
            built++;
        }

        Undo.RecordObject(root, "Tunnels");
        root.GeneratedFloor = generatedFloor.ToArray();
        root.GeneratedProps = generatedProps.ToArray();
        if (pairs.Count > 0) report.Info($"Tunnels: {built} of {pairs.Count} built ({built * 2} seam ends).");
    }

    // Every cell in a mouth's box, on one side or the other
    private static IEnumerable<Vector2Int> BoxCells(Mouth m, int overlap, bool inside)
    {
        int r = overlap + m.Cells.Count + 1;
        var center = Vector2Int.FloorToInt(m.Point);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                var c = center + new Vector2Int(x, y);
                if (InBox(m, c, overlap, inside)) yield return c;
            }
        }
    }

    private static void Clear(Pocket pocket, Tilemap floor) => ClearLegacy(pocket, floor);

    // What pockets generated before "Pockets (generated)": floor recorded on the pocket and a "Generated" child
    public static void ClearLegacy(Pocket pocket, Tilemap floor)
    {
        foreach (var c in pocket.GeneratedCells) floor.SetTile((Vector3Int)c, null);
        if (pocket.GeneratedCells.Length > 0)
        {
            Undo.RecordObject(pocket, "Pocket");
            pocket.GeneratedCells = new Vector2Int[0];
        }
        var old = pocket.transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    private static void Build(Pocket pocket, LevelFloors floors, Tilemap levelProps, Transform generatedParent, List<Vector2Int> generatedCells,
        ref int nextX, LevelBuildReport report)
    {
        var floor = floors.MainFloor;
        var area = pocket.Area;
        var cells0 = new HashSet<Vector2Int>();
        System.Func<Vector2Int, TileBase> sourceTile;
        Tilemap props;
        List<Vector2Int> touching = null;
        if (pocket.IsModular)
        {
            // Modular pocket: sheet 0 is its own Floor, wherever it is
            var offset = pocket.CellOffset;
            pocket.Floor.CompressBounds();
            foreach (var p in pocket.Floor.cellBounds.allPositionsWithin)
                if (pocket.Floor.HasTile(p)) cells0.Add((Vector2Int)p + offset);
            sourceTile = c => pocket.Floor.GetTile((Vector3Int)(c - offset));
            props = pocket.Props;
            if (cells0.Count == 0)
            {
                report.Warn($"{pocket.name}: its \"{pocket.Floor.name}\" is empty. Select the pocket, press \"Paint in {pocket.name}\" in the " +
                            "window and paint the pocket's floor.", pocket, pocket.transform.position);
                return;
            }

            // If it touches floor elsewhere and the pocket rotates, that exit only exists in sheet 0 (warned further down)
            touching = cells0.SelectMany(c => Dirs.Select(d => c + d)).Where(n => !cells0.Contains(n) && floors.Has(n)).ToList();
        }
        else
        {
            foreach (var c in area.allPositionsWithin)
                if (floor.HasTile((Vector3Int)c)) cells0.Add(c);
            sourceTile = c => floor.GetTile((Vector3Int)c);
            props = levelProps;
            if (cells0.Count == 0)
            {
                report.Warn($"{pocket.name}: there's no floor painted inside its violet rectangle. Paint the pocket inside the rectangle, " +
                            "or move the pocket (or change its Size) so it sits over the floor you painted.", pocket, Center(area));
                return;
            }

            // Floor touching the pocket but outside the rectangle: it doesn't exist in the rotated sheets
            var outside = Flood(cells0.SelectMany(c => Dirs.Select(d => c + d)).Where(n => !area.Contains(n) && floor.HasTile((Vector3Int)n)),
                c => !area.Contains(c) && floor.HasTile((Vector3Int)c), 2000);
            if (outside.Count > 0)
            {
                var first = outside.OrderBy(c => (Center(area) - (Vector3)(c + Half)).sqrMagnitude).First();
                var sides = outside.Select(c => SideOf(area, c)).Distinct();
                report.Warn($"{pocket.name}: {outside.Count} floor cells touch the pocket but are outside its violet rectangle " +
                            $"({string.Join(" and ", sides)}). Everything that belongs to the pocket must stay inside: increase Size on the " +
                            "Pocket component, move the pocket, or convert it to modular. If it's an intentional exit, note that it only exists in " +
                            "sheet 0; in the rotated sheets that corridor ends in a wall (better use stairs or a door with a fade).",
                            pocket, (Vector3)(first + Half));
            }
        }

        // 1. Mouths: snap to the open end of the corridor
        var mouths = new Dictionary<PocketMouth, Mouth>();
        foreach (var component in pocket.GetComponentsInChildren<PocketMouth>())
        {
            var mouth = Resolve(component, c => cells0.Contains(c));
            if (mouth == null)
            {
                string why = pocket.IsModular
                    ? Diagnose(component, null, c => cells0.Contains(c))
                    : Diagnose(component, area, c => floor.HasTile((Vector3Int)c));
                report.Warn($"{pocket.name}/{component.name}: {why}", component, component.transform.position);
                continue;
            }
            Undo.RecordObject(component.transform, "Snap mouth");
            Undo.RecordObject(component, "Snap mouth");
            component.transform.position = new Vector3(mouth.Point.x, mouth.Point.y, 0f);
            component.SetShape(mouth.Dir, mouth.Cells.Count);
            mouths[component] = mouth;
        }

        // 2. Pairs: the glue maps B's inside to just behind A
        var pairs = MakePairs(mouths, pocket.name, report);
        var used = new HashSet<PocketMouth>(pairs.SelectMany(p => new[] { p.A.Component, p.B.Component }));

        // 3. Overlaps: behind each mouth goes a copy of what's inside its partner
        int overlap = pocket.Overlap;
        var extensions = new List<Extension>();
        var extTargets = new HashSet<Vector2Int>();
        foreach (var (a, b, glue) in pairs)
        {
            AddExtension(a, b, glue, $"Overlap behind {a.Name}");
            AddExtension(b, a, glue.Inverse(), $"Overlap behind {b.Name}");
        }

        void AddExtension(Mouth at, Mouth from, Rigid map, string name)
        {
            var sources = new List<Vector2Int>();
            int clashes = 0;
            foreach (var src in Inside(from, cells0, overlap))
            {
                var target = map.Cell(src);
                if (cells0.Contains(target) || !extTargets.Add(target))
                {
                    clashes++;
                    continue;
                }
                sources.Add(src);
            }
            var behind = Behind(at, cells0, overlap).OrderBy(c => (c + Half - at.Point).sqrMagnitude).ToList();
            if (clashes > 0 || behind.Count > 0)
            {
                Vector3 place = behind.Count > 0 ? (Vector3)(behind[0] + Half) : (Vector3)at.Point;
                report.Warn($"{pocket.name}: on the other side of {at.Name} (where its arrow points) there's floor of the same pocket within {overlap} cells. " +
                            $"Build has to paint the continuation of the corridor entering through {from.Name} there, so they clash and the jump will show when crossing. " +
                            "Move that floor away from the mouth or move the mouth to another end.", at.Component, place);
            }
            extensions.Add(new Extension { Name = name, Map = map, Sources = sources });
        }

        // 4. Rotations reachable by crossing mouths
        var rotations = new SortedSet<int> { 0 };
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (var (_, _, glue) in pairs)
                foreach (int r in rotations.ToList())
                    changed |= rotations.Add(Mod4(r + glue.R)) | rotations.Add(Mod4(r - glue.R));
        }

        if (touching != null && touching.Count > 0 && rotations.Count > 1)
            report.Warn($"{pocket.name}: the pocket's floor touches floor from another part of the level, and the pocket rotates. That exit only exists in " +
                        "sheet 0: in the rotated sheets there's a wall there. Better to enter and leave through stairs or a door with a fade.",
                        pocket, (Vector3)(touching[0] + Half));

        // Props are drawn in 3/4 view (a wall torch faces south): they can't be rotated, so they
        // aren't copied to the rotated sheets
        if (props != null && rotations.Count > 1)
        {
            int count = 0;
            Vector2Int first = default;
            if (pocket.IsModular)
            {
                props.CompressBounds();
                foreach (var p in props.cellBounds.allPositionsWithin)
                {
                    if (!props.HasTile(p)) continue;
                    if (count++ == 0) first = (Vector2Int)p + pocket.CellOffset;
                }
            }
            else
            {
                foreach (var c in area.allPositionsWithin)
                {
                    if (!props.HasTile((Vector3Int)c)) continue;
                    if (count++ == 0) first = c;
                }
            }
            if (count > 0)
                report.Warn($"{pocket.name}: has {count} props (torches, decoration) in the Props layer. They only appear in sheet 0, not in the " +
                            "rotated sheets, because 3/4 art can't be rotated. If the player can see a prop near a mouth, it vanishes " +
                            "suddenly when crossing; keep props away from the mouths or take them out of the pocket.", pocket, (Vector3)(first + Half));
        }

        // 5. Where each sheet goes: sheet 0 stays where you painted it, the rotated ones to the right of the level
        var footprint = cells0.Concat(extensions.SelectMany(e => e.Sources.Select(e.Map.Cell))).ToList();
        var sheets = new Dictionary<int, Rigid>();
        nextX = Mathf.Max(nextX, footprint.Max(c => c.x));
        foreach (int r in rotations)
        {
            if (r == 0)
            {
                sheets[0] = new Rigid(Vector2Int.zero, 0);
                continue;
            }
            var turned = footprint.Select(c => Rot(c, r)).ToList();
            int minX = turned.Min(c => c.x), maxX = turned.Max(c => c.x);
            int midY = (turned.Min(c => c.y) + turned.Max(c => c.y)) / 2;
            var shift = new Vector2Int(nextX + pocket.SheetGap - minX, Mathf.RoundToInt(area.center.y) - midY);
            sheets[r] = new Rigid(shift, r);
            nextX = maxX + shift.x;
        }

        // 6. Paint and create the pieces
        var root = new GameObject(pocket.name);
        Undo.RegisterCreatedObjectUndo(root, "Generate pocket");
        root.transform.SetParent(generatedParent, false);
        root.transform.position = Vector3.zero;

        var generated = generatedCells;
        var generatedSet = new HashSet<Vector2Int>();
        int levelClashes = 0;
        Vector2Int firstClash = default;
        bool Paint(Vector2Int target, Vector2Int source)
        {
            var t = (Vector3Int)target;
            if ((floors.Has(target) || floor.HasTile(t)) && !generatedSet.Contains(target))
            {
                if (levelClashes++ == 0) firstClash = target; // never paint over level floor
                return false;
            }
            floor.SetTile(t, sourceTile(source));
            if (generatedSet.Add(target)) generated.Add(target);
            return true;
        }

        var min = area.min;
        foreach (int r in rotations)
        {
            var sheet = sheets[r];
            var color = SheetColors[r];
            var sheetGo = new GameObject($"Sheet {r} ({r * 90}°)");
            sheetGo.transform.SetParent(root.transform, false);

            var mainCells = new List<Vector2Int>();
            foreach (var c in cells0)
            {
                var target = sheet.Cell(c);
                if (r == 0 || Paint(target, c)) mainCells.Add(target);
            }
            CreatePiece(sheetGo.transform, $"Sheet {r}", pocket.PieceId, sheet, min, mainCells, color);

            foreach (var ext in extensions)
            {
                var map = ext.Map.Then(sheet);
                var cells = new List<Vector2Int>();
                foreach (var src in ext.Sources)
                {
                    var target = map.Cell(src);
                    if (Paint(target, src)) cells.Add(target);
                }
                if (cells.Count > 0) CreatePiece(sheetGo.transform, $"{ext.Name} (sheet {r})", pocket.PieceId, map, min, cells, color);
            }
        }
        if (levelClashes > 0)
            report.Warn($"{pocket.name}: behind the mouths Build paints the continuation of each corridor ({overlap} cells), but {levelClashes} " +
                        "of those cells land on floor from another part of the level, so they weren't painted and the crossing will show. " +
                        $"Leave at least {overlap} empty cells between the mouths and the rest of the level.", pocket, (Vector3)(firstClash + Half));

        // 7. Seams: mouth A of sheet r is stitched to mouth B of sheet r + rotation
        var seamsRoot = new GameObject("Seams");
        seamsRoot.transform.SetParent(root.transform, false);
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var (a, b, glue) in pairs)
        {
            foreach (int r in rotations)
            {
                int s = Mod4(r + glue.R);
                var seamA = CreateSeam(seamsRoot.transform, $"{a.Name} → {b.Name} (sheet {r})", sheets[r].Point(a.Point), Rot(a.Dir, r), a.Cells.Count, ignoreRaycast);
                var seamB = CreateSeam(seamsRoot.transform, $"{b.Name} → {a.Name} (sheet {s})", sheets[s].Point(b.Point), Rot(b.Dir, s), b.Cells.Count, ignoreRaycast);
                Link(seamA, seamB);
                Link(seamB, seamA);
            }
        }

        WarnLevelInView(pocket, c => floors.Has(c) || floor.HasTile((Vector3Int)c), cells0, generatedSet,
            mouths.Values.Where(m => used.Contains(m.Component)), overlap, report);
        report.Info($"{pocket.name}: {rotations.Count} {(rotations.Count == 1 ? "sheet" : "sheets")} ({string.Join("°, ", rotations.Select(r => r * 90))}°), {pairs.Count} mouth pairs, {pairs.Count * rotations.Count * 2} seam ends.");
    }

    // A mouth is narrower than this: anything wider is the side of a corridor or a room
    private const int MaxMouthWidth = 8;
    private const float SnapDistance = 2f;

    // Joins the mouths in pairs. "owner" is the pocket name (or "Tunnels") for the messages.
    private static List<(Mouth A, Mouth B, Rigid Glue)> MakePairs(Dictionary<PocketMouth, Mouth> mouths, string owner, LevelBuildReport report)
    {
        var pairs = new List<(Mouth A, Mouth B, Rigid Glue)>();
        var used = new HashSet<PocketMouth>();
        foreach (var a in mouths.Values)
        {
            var pm = a.Component;
            if (used.Contains(pm)) continue;
            Vector3 at = a.Point;
            if (pm.Partner == null)
            {
                report.Warn($"{owner}/{pm.name} isn't joined to any other mouth, so that corridor is a dead end. " +
                            "To join it: select this mouth and its partner and press \"Join\" in the window.", pm, at);
                continue;
            }
            if (pm.Partner == pm || !mouths.TryGetValue(pm.Partner, out var b))
            {
                var myPocket = pm.GetComponentInParent<Pocket>();
                var theirPocket = pm.Partner.GetComponentInParent<Pocket>();
                string why = pm.Partner == pm ? "is joined to itself"
                    : myPocket == null && theirPocket != null ? $"is a loose mouth (tunnel) and is joined to {pm.Partner.name}, which belongs to {theirPocket.name}"
                    : myPocket != null && theirPocket == null ? $"belongs to a pocket and is joined to {pm.Partner.name}, which is a loose mouth (tunnel)"
                    : myPocket != theirPocket ? $"is joined to {pm.Partner.name}, which belongs to another pocket"
                    : $"is joined to {pm.Partner.name}, which has its own problem (see its warning)";
                report.Warn($"{owner}/{pm.name} {why}. A pocket's mouths join mouths of the same pocket, and loose mouths join other loose mouths.", pm, at);
                continue;
            }
            if (used.Contains(b.Component))
            {
                report.Warn($"{owner}/{pm.name} is joined to {b.Name}, but {b.Name} is already joined to {b.Component.Partner?.name}. " +
                            "Each mouth has a single partner: select the two that belong together and press \"Join\".", pm, at);
                continue;
            }
            if (b.Component.Partner != pm)
                report.Warn($"{owner}: {pm.name} points to {b.Name}, but {b.Name} points to {(b.Component.Partner != null ? b.Component.Partner.name : "none")}. " +
                            $"Using {pm.name} ↔ {b.Name}; to tidy it up, select both and press \"Join\".", b.Component, b.Point);
            if (a.Cells.Count != b.Cells.Count)
            {
                report.Warn($"{owner}: {pm.name} is {a.Cells.Count} cells wide and {b.Name} is {b.Cells.Count}. Joined mouths " +
                            "must be the same width, because the corridor continues from one to the other: widen or narrow one of the two corridors.", pm, at);
                continue;
            }
            used.Add(pm);
            used.Add(b.Component);
            pairs.Add((a, b, Glue(a, b)));
        }
        return pairs;
    }

    // For the "place mouth" preview: where a mouth placed at p would snap (or null if there's no corridor end nearby)
    public static (Vector2 point, Vector2Int dir, int width)? PreviewOpening(Vector2 p, System.Func<Vector2Int, bool> isFloor)
    {
        var found = NearestOpening(p, isFloor, 3);
        if (found == null || Vector2.Distance(found.Value.point, p) > SnapDistance) return null;
        return (found.Value.point, found.Value.dir, found.Value.cells.Count);
    }

    // The mouth snaps to the nearest corridor end (within 2 cells) and takes its width
    private static Mouth Resolve(PocketMouth component, System.Func<Vector2Int, bool> isFloor)
    {
        Vector2 p = component.transform.position;
        var found = NearestOpening(p, isFloor, 3);
        if (found == null || Vector2.Distance(found.Value.point, p) > SnapDistance) return null;
        var (cells, dir, point) = found.Value;
        return new Mouth { Component = component, Dir = dir, Cells = cells, Point = point };
    }

    // Corridor end nearest to p: a strip of floor with void ahead, closed on both sides
    // and up to MaxMouthWidth cells wide (so the long side of a corridor doesn't count as a mouth).
    private static (List<Vector2Int> cells, Vector2Int dir, Vector2 point)? NearestOpening(Vector2 p, System.Func<Vector2Int, bool> isFloor, int radius)
    {
        var near = Vector2Int.FloorToInt(p);
        (List<Vector2Int>, Vector2Int, Vector2)? best = null;
        float bestDistance = float.MaxValue;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var c = near + new Vector2Int(dx, dy);
                if (!isFloor(c)) continue;
                foreach (var d in Dirs)
                {
                    if (isFloor(c + d)) continue;
                    var cells = Opening(c, d, isFloor);
                    if (cells == null) continue;
                    Vector2 point = Vector2.zero;
                    foreach (var cell in cells) point += cell + Half;
                    point = point / cells.Count + (Vector2)d * 0.5f;
                    float distance = Vector2.Distance(point, p);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = (cells, d, point);
                    }
                }
            }
        }
        return best;
    }

    private static List<Vector2Int> Opening(Vector2Int start, Vector2Int dir, System.Func<Vector2Int, bool> isFloor)
    {
        var left = Left(dir);
        var cells = new List<Vector2Int> { start };
        var c = start + left;
        for (; isFloor(c) && !isFloor(c + dir) && cells.Count <= MaxMouthWidth; c += left) cells.Add(c);
        if (isFloor(c)) return null; // not closed: it's part of a longer edge or a corner
        c = start - left;
        for (; isFloor(c) && !isFloor(c + dir) && cells.Count <= MaxMouthWidth; c -= left) cells.Add(c);
        if (isFloor(c) || cells.Count > MaxMouthWidth) return null;
        return cells.OrderByDescending(cell => Dot(cell, left)).ToList();
    }

    // Why a mouth couldn't snap, in words, and where the nearest corridor end is
    // area: the pocket rectangle, or null for a loose mouth (tunnel)
    private static string Diagnose(PocketMouth component, RectInt? area, System.Func<Vector2Int, bool> IsFloor)
    {
        Vector2 p = component.transform.position;
        var nearestEnd = NearestOpening(p, IsFloor, 30);
        string hint = "";
        if (nearestEnd != null)
        {
            var end = nearestEnd.Value.point;
            bool endInside = area == null || area.Value.Contains(Vector2Int.FloorToInt(end - (Vector2)nearestEnd.Value.dir * 0.5f));
            hint = $" The nearest corridor end is at ({end.x:0.#}, {end.y:0.#}), {DirName(nearestEnd.Value.dir)}" +
                   (endInside ? ": drag the mouth there." : ", but it's also outside the rectangle: enlarge the pocket area to include it and drag the mouth there.");
        }

        if (area != null && !area.Value.Contains(Vector2Int.FloorToInt(p)))
            return "is outside the pocket's violet rectangle. Mouths must be inside, against the end of a corridor " +
                   "(the last painted cell before the void)." + hint;

        bool floorNear = false;
        var cell = Vector2Int.FloorToInt(p);
        for (int dx = -2; dx <= 2 && !floorNear; dx++)
            for (int dy = -2; dy <= 2 && !floorNear; dy++)
                floorNear = IsFloor(cell + new Vector2Int(dx, dy));
        if (!floorNear)
            return "there's no floor nearby (within 2 cells). A mouth goes against the end of a corridor: the last painted cell before the void." + hint;

        return "is in the middle or on the side of a corridor, and the corridor doesn't end there. A mouth goes where the corridor stops: " +
               $"the last painted cell before the void, up to {MaxMouthWidth} cells wide." + hint;
    }

    private static string DirName(Vector2Int d) =>
        d == Vector2Int.up ? "open upward" : d == Vector2Int.down ? "open downward" : d == Vector2Int.left ? "open to the left" : "open to the right";

    private static string SideOf(RectInt area, Vector2Int c) =>
        c.x >= area.xMax ? "right" : c.x < area.xMin ? "left" : c.y >= area.yMax ? "above" : "below";

    private static Vector3 Center(RectInt area) => new(area.center.x, area.center.y, 0f);

    // Connected cells that meet the condition, starting from the seeds (capped)
    private static List<Vector2Int> Flood(IEnumerable<Vector2Int> seeds, System.Func<Vector2Int, bool> accept, int max)
    {
        var seen = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        foreach (var s in seeds)
            if (accept(s) && seen.Add(s)) queue.Enqueue(s);
        while (queue.Count > 0 && seen.Count < max)
        {
            var c = queue.Dequeue();
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (accept(n) && seen.Add(n)) queue.Enqueue(n);
            }
        }
        return seen.ToList();
    }

    // Leaving through A (towards A.Dir) is entering through B (towards -B.Dir). The glue rotates -B.Dir onto A.Dir and moves
    // the cell of B that is on the left when entering to just in front of A's left cell.
    private static Rigid Glue(Mouth a, Mouth b)
    {
        int g = Enumerable.Range(0, 4).First(k => Rot(-b.Dir, k) == a.Dir);
        var aOutLeft = a.Cells[0] + a.Dir;
        var bInRight = b.Cells[b.Cells.Count - 1];
        return new Rigid(aOutLeft - Rot(bInRight, g), g);
    }

    // Sheet 0 floor visible from the mouth, on the inside
    private static IEnumerable<Vector2Int> Inside(Mouth m, HashSet<Vector2Int> cells0, int overlap) =>
        cells0.Where(c => InBox(m, c, overlap, inside: true));

    // Sheet 0 floor that would be on the other side of the mouth (clashes with the overlap)
    private static IEnumerable<Vector2Int> Behind(Mouth m, HashSet<Vector2Int> cells0, int overlap) =>
        cells0.Where(c => InBox(m, c, overlap, inside: false));

    private static bool InBox(Mouth m, Vector2Int c, int overlap, bool inside)
    {
        Vector2 v = c + Half - m.Point;
        float depth = -Dot(v, m.Dir); // positive into the corridor
        float lateral = Mathf.Abs(Dot(v, Left(m.Dir)));
        if (lateral >= overlap + m.Cells.Count * 0.5f) return false;
        return inside ? depth > 0f && depth < overlap : depth < 0f && depth > -overlap;
    }

    // If floor from the rest of the level is visible near a sheet 0 mouth, it won't be there on the other side
    private static void WarnLevelInView(Pocket pocket, System.Func<Vector2Int, bool> isLevelFloor, HashSet<Vector2Int> cells0, HashSet<Vector2Int> generated,
        IEnumerable<Mouth> mouths, int overlap, LevelBuildReport report)
    {
        foreach (var m in mouths)
        {
            var center = Vector2Int.FloorToInt(m.Point);
            Vector2Int? nearest = null;
            for (int x = -overlap; x <= overlap; x++)
            {
                for (int y = -overlap; y <= overlap; y++)
                {
                    var c = center + new Vector2Int(x, y);
                    if (cells0.Contains(c) || generated.Contains(c) || !isLevelFloor(c)) continue;
                    if (nearest == null || (c - center).sqrMagnitude < (nearest.Value - center).sqrMagnitude) nearest = c;
                }
            }
            if (nearest == null) continue;

            int distance = Mathf.RoundToInt(Vector2.Distance(nearest.Value + Half, m.Point));
            report.Warn($"{pocket.name}: floor from another part of the level is visible from {m.Name}, {distance} cells away. On the other side of the seam " +
                        "that part doesn't exist, so it would vanish suddenly when crossing. " +
                        $"Leave at least {overlap} empty cells between the mouths and the rest of the level.", m.Component, (Vector3)(nearest.Value + Half));
        }
    }

    private static void CreatePiece(Transform parent, string name, string pieceId, Rigid map, Vector2Int min, List<Vector2Int> cells, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var piece = go.AddComponent<PieceInstance>();
        // The piece uses local coordinates = sheet 0 cell - area corner
        piece.Setup(pieceId, map.T + Rot(min, map.R), map.R, cells.ToArray(), color);
        go.transform.position = new Vector3(piece.Rect.center.x, piece.Rect.center.y, 0f);
    }

    private static PortalSeam CreateSeam(Transform parent, string name, Vector2 position, Vector2Int forward, int width, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.layer = layer;

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = forward.x != 0 ? new Vector2(1.5f, width + 0.2f) : new Vector2(width + 0.2f, 1.5f);
        go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        var seam = go.AddComponent<PortalSeam>();
        var so = new SerializedObject(seam);
        so.FindProperty("_forward").vector2Value = forward;
        so.ApplyModifiedPropertiesWithoutUndo();
        return seam;
    }

    private static void Link(PortalSeam seam, PortalSeam partner)
    {
        var so = new SerializedObject(seam);
        so.FindProperty("_partner").objectReferenceValue = partner;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int Mod4(int r) => ((r % 4) + 4) % 4;
    private static Vector2Int Left(Vector2Int d) => new(-d.y, d.x);
    private static float Dot(Vector2 v, Vector2Int d) => v.x * d.x + v.y * d.y;
    private static int Dot(Vector2Int v, Vector2Int d) => v.x * d.x + v.y * d.y;

    // 90° clockwise, k times (negative = counterclockwise)
    private static Vector2Int Rot(Vector2Int v, int k)
    {
        for (int i = 0; i < Mod4(k); i++) v = new Vector2Int(v.y, -v.x);
        return v;
    }

    private static Vector2 Rot(Vector2 v, int k)
    {
        for (int i = 0; i < Mod4(k); i++) v = new Vector2(v.y, -v.x);
        return v;
    }
}
