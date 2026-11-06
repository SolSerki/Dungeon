using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// Revisa que las llaves alcancen para las puertas, probando todos los órdenes en que el jugador puede abrirlas.
// El piso se divide en zonas separadas por las puertas cerradas; cada puerta cuesta una llave y abre las zonas que toca.
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
            report.Info("Hay demasiadas puertas para revisar las llaves.");
            return;
        }

        var zoneOf = SplitZones(floor, doors);
        foreach (var door in doors)
            foreach (var cell in door.Cells)
                foreach (var d in Neighbors4)
                    if (zoneOf.TryGetValue(cell + d, out int zone)) door.Zones.Add(zone);

        if (!zoneOf.TryGetValue(ToCell(floor, player.transform.position), out int start)) return; // ya avisa EnemySpawnBuilder

        var keysInZone = CountKeys(floor, zoneOf, report);
        Search(doors, keysInZone, start, report);
    }

    // Zonas conectadas del piso, con las puertas como paredes
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
        return zoneOf;
    }

    // Llaves que caen seguro de enemigos y llaves sueltas en el piso
    private static Dictionary<int, int> CountKeys(Tilemap floor, Dictionary<Vector2Int, int> zoneOf, LevelBuildReport report)
    {
        var keys = new Dictionary<int, int>();
        void Add(Transform at, string what)
        {
            if (zoneOf.TryGetValue(ToCell(floor, at.position), out int zone)) keys[zone] = keys.TryGetValue(zone, out int n) ? n + 1 : 1;
            else report.Warn($"{what} está fuera del piso o encima de una puerta: no la cuento.", at);
        }

        foreach (var dropper in Object.FindObjectsByType<LootDropper>(FindObjectsInactive.Exclude))
        {
            foreach (var (prefab, chance) in EnemySpawnBuilder.ReadDrops(dropper))
            {
                if (!LootCatalog.IsKey(prefab)) continue;
                if (chance < 1f) report.Warn($"{dropper.name} suelta {prefab.name} con chance {chance:0.##}: puede no caer, no la cuento.", dropper);
                else Add(dropper.transform, $"La llave de {dropper.name}");
            }
        }
        foreach (var key in Object.FindObjectsByType<KeyPickup>(FindObjectsInactive.Exclude))
            Add(key.transform, key.name);

        return keys;
    }

    // Recorre todas las combinaciones de puertas abiertas a las que el jugador puede llegar
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
        report.Info($"Llaves: {totalKeys} para {doors.Count} puertas.");
        if (parent.Count >= MaxStates) report.Info("Hay muchas combinaciones de puertas: revisé solo una parte.");

        var reachable = Reached(doors, AllBits(openable), start);
        for (int i = 0; i < doors.Count; i++)
        {
            if (openable[i]) continue;
            if (doors[i].Zones.Overlaps(reachable))
                report.Warn($"{doors[i].Target.name}: no hay llave para abrirla. Marcá una llave en el loot de un enemigo que esté antes de esta puerta.", doors[i].Target);
            else
                report.Warn($"{doors[i].Target.name}: no se puede llegar desde el inicio.", doors[i].Target);
        }

        // trabado: sin llaves frente a una puerta que en otro orden sí se podía abrir.
        // La búsqueda va por niveles, así que el primero es el que abre menos puertas.
        foreach (var (stuck, frontier) in stuckStates)
        {
            var blocked = frontier.Where(i => openable[i]).ToList();
            if (blocked.Count == 0) continue;

            var path = new List<string>();
            for (ulong s = stuck; parent[s].door >= 0; s = parent[s].from) path.Insert(0, doors[parent[s].door].Target.name);
            report.Warn($"El jugador se puede trabar: si abre {string.Join(" y después ", path)}, se queda sin llaves para {string.Join(", ", blocked.Select(i => doors[i].Target.name))}.",
                doors[blocked[0]].Target);
            break;
        }
    }

    private static HashSet<int> Reached(List<Door> doors, ulong opened, int start)
    {
        // cada puerta abierta tocaba lo alcanzado cuando se abrió: alcanza con sumar sus zonas
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
