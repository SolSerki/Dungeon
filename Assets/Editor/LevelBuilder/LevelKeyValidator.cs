using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// Checks there are enough keys for the doors, trying every order in which the player can open them.
// The floor is split into zones separated by locked doors; each door costs one key and opens the zones it touches.
public static class LevelKeyValidator
{
    private const int MaxStates = 50000;

    private static readonly Vector2Int[] Neighbors4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private class Door
    {
        public LockedDoor Target;
        public HashSet<Vector2Int> Cells;
        public HashSet<int> Zones = new HashSet<int>();
    }

    public static void Validate(Tilemap floor, LevelBuildReport report)
    {
        var player = GameObject.FindWithTag("Player");
        if (floor == null || player == null) return;

        var doors = DoorPlacement.FindDoors()
            .Select(d => new Door { Target = d, Cells = CellsOf(DoorPlacement.FootprintCells(d, floor)) })
            .ToList();
        if (doors.Count > 63)
        {
            report.Info("Too many doors to check the keys.");
            return;
        }

        var zoneOf = SplitZones(floor, doors);
        foreach (var door in doors)
            foreach (var cell in door.Cells)
                foreach (var d in Neighbors4)
                    if (zoneOf.TryGetValue(cell + d, out int zone)) door.Zones.Add(zone);

        if (!zoneOf.TryGetValue(ToCell(floor, player.transform.position), out int start)) return; // EnemySpawnBuilder already warns about it

        var keysInZone = CountKeys(floor, zoneOf, report);
        Search(doors, keysInZone, start, report);
    }

    // Connected floor zones, with doors acting as walls
    private static Dictionary<Vector2Int, int> SplitZones(Tilemap floor, List<Door> doors)
    {
        var blocked = new HashSet<Vector2Int>(doors.SelectMany(d => d.Cells));
        var cells = new HashSet<Vector2Int>();
        floor.CompressBounds();
        foreach (var p in floor.cellBounds.allPositionsWithin)
            if (floor.HasTile(p) && !blocked.Contains((Vector2Int)p)) cells.Add((Vector2Int)p);

        var zoneOf = new Dictionary<Vector2Int, int>();
        int next = 0;
        foreach (var seed in cells)
        {
            if (zoneOf.ContainsKey(seed)) continue;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(seed);
            zoneOf[seed] = next;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in Neighbors4)
                {
                    var n = c + d;
                    if (cells.Contains(n) && !zoneOf.ContainsKey(n)) { zoneOf[n] = next; queue.Enqueue(n); }
                }
            }
            next++;
        }

        // Maze seams (tunnels and pocket mouths) join the zone on one side with the one on the other
        var root = Enumerable.Range(0, next).ToArray();
        int Find(int z)
        {
            while (root[z] != z) z = root[z] = root[root[z]];
            return z;
        }
        Vector2Int Inside(PortalSeam s) => Vector2Int.FloorToInt((Vector2)s.transform.position - s.Forward.normalized * 0.5f);
        bool merged = false;
        foreach (var seam in Object.FindObjectsByType<PortalSeam>(FindObjectsInactive.Exclude))
        {
            if (seam.Partner == null) continue;
            if (!zoneOf.TryGetValue(Inside(seam), out int a) || !zoneOf.TryGetValue(Inside(seam.Partner), out int b)) continue;
            a = Find(a);
            b = Find(b);
            if (a == b) continue;
            root[a] = b;
            merged = true;
        }
        if (merged)
            foreach (var cell in zoneOf.Keys.ToList()) zoneOf[cell] = Find(zoneOf[cell]);

        return zoneOf;
    }

    // Keys guaranteed to drop from enemies and keys lying on the floor
    private static Dictionary<int, int> CountKeys(Tilemap floor, Dictionary<Vector2Int, int> zoneOf, LevelBuildReport report)
    {
        var keys = new Dictionary<int, int>();
        void Add(Transform at, string what)
        {
            if (zoneOf.TryGetValue(ToCell(floor, at.position), out int zone)) keys[zone] = keys.TryGetValue(zone, out int n) ? n + 1 : 1;
            else report.Warn($"{what} is off the floor or on top of a door: not counted.", at);
        }

        foreach (var dropper in Object.FindObjectsByType<LootDropper>(FindObjectsInactive.Exclude))
        {
            foreach (var (item, chance) in EnemySpawnBuilder.ReadDrops(dropper))
            {
                if (!LootCatalog.IsKey(item)) continue;
                if (chance < 1f) report.Warn($"{dropper.name} drops {item.name} with chance {chance:0.##}: it might not drop, not counted.", dropper);
                else Add(dropper.transform, $"{dropper.name}'s key");
            }
        }
        foreach (var key in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude).Where(p => LootCatalog.IsKey(p.Item)))
            Add(key.transform, key.name);

        return keys;
    }

    // Walks every combination of opened doors the player can reach
    private static void Search(List<Door> doors, Dictionary<int, int> keysInZone, int start, LevelBuildReport report)
    {
        int Keys(ulong opened)
        {
            var zones = Reached(doors, opened, start);
            return zones.Sum(z => keysInZone.TryGetValue(z, out int n) ? n : 0) - CountBits(opened);
        }

        var openable = new bool[doors.Count];
        var parent = new Dictionary<ulong, (ulong from, int door)>();
        var queue = new Queue<ulong>();
        queue.Enqueue(0UL);
        parent[0UL] = (0UL, -1);
        var stuckStates = new List<(ulong opened, List<int> frontier)>();

        while (queue.Count > 0 && parent.Count < MaxStates)
        {
            ulong opened = queue.Dequeue();
            var zones = Reached(doors, opened, start);
            var frontier = Enumerable.Range(0, doors.Count)
                .Where(i => (opened & (1UL << i)) == 0 && doors[i].Zones.Overlaps(zones)).ToList();
            if (frontier.Count == 0) continue;

            if (Keys(opened) <= 0)
            {
                stuckStates.Add((opened, frontier));
                continue;
            }

            foreach (int i in frontier)
            {
                openable[i] = true;
                ulong next = opened | (1UL << i);
                if (parent.ContainsKey(next)) continue;
                parent[next] = (opened, i);
                queue.Enqueue(next);
            }
        }

        int totalKeys = keysInZone.Values.Sum();
        report.Info($"Keys: {totalKeys} for {doors.Count} doors.");
        if (parent.Count >= MaxStates) report.Info("Too many door combinations: only some were checked.");

        var reachable = Reached(doors, AllBits(openable), start);
        for (int i = 0; i < doors.Count; i++)
        {
            if (openable[i]) continue;
            if (doors[i].Zones.Overlaps(reachable))
                report.Warn($"{doors[i].Target.name}: there's no key to open it. Tick a key in the loot of an enemy placed before this door.", doors[i].Target);
            else
                report.Warn($"{doors[i].Target.name}: can't be reached from the start.", doors[i].Target);
        }

        // stuck: no keys left in front of a door that could be opened in another order.
        // The search goes level by level, so the first one found opens the fewest doors.
        foreach (var (stuck, frontier) in stuckStates)
        {
            var blocked = frontier.Where(i => openable[i]).ToList();
            if (blocked.Count == 0) continue;

            var path = new List<string>();
            for (ulong s = stuck; parent[s].door >= 0; s = parent[s].from) path.Insert(0, doors[parent[s].door].Target.name);
            report.Warn($"The player can get stuck: if they open {string.Join(" and then ", path)}, they run out of keys for {string.Join(", ", blocked.Select(i => doors[i].Target.name))}.",
                doors[blocked[0]].Target);
            break;
        }
    }

    private static HashSet<int> Reached(List<Door> doors, ulong opened, int start)
    {
        // each opened door touched the reached area when it was opened: adding its zones is enough
        var zones = new HashSet<int> { start };
        for (int i = 0; i < doors.Count; i++)
            if ((opened & (1UL << i)) != 0) zones.UnionWith(doors[i].Zones);
        return zones;
    }

    private static ulong AllBits(bool[] flags)
    {
        ulong mask = 0;
        for (int i = 0; i < flags.Length; i++) if (flags[i]) mask |= 1UL << i;
        return mask;
    }

    private static int CountBits(ulong value)
    {
        int count = 0;
        for (; value != 0; value &= value - 1) count++;
        return count;
    }

    private static Vector2Int ToCell(Tilemap floor, Vector3 position) => (Vector2Int)floor.WorldToCell(position);

    private static HashSet<Vector2Int> CellsOf(RectInt rect)
    {
        var cells = new HashSet<Vector2Int>();
        foreach (var c in rect.allPositionsWithin) cells.Add(c);
        return cells;
    }
}
