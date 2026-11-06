using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// Divide el piso en salas y cuenta qué enemigos hay en cada una.
// Una sala es una zona donde hay celdas rodeadas de piso por los 8 lados: los pasillos de 1 o 2 tiles
// no cumplen eso, así que separan salas; sus celdas se reparten a la sala más cercana.
public static class LevelRoomAnalyzer
{
    public class Room
    {
        public int Number;
        public RectInt Bounds;
        // nombres y no los EnemyAI: al entrar a Play mode Unity destruye los objetos y la lista quedaría rota
        public readonly List<string> EnemyNames = new List<string>();
        public float TotalHealth;
        public bool HasPlayer;
    }

    private static readonly Vector2Int[] Neighbors4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public static List<Room> Analyze(Tilemap floor)
    {
        floor.CompressBounds();
        var cells = new HashSet<Vector2Int>();
        foreach (var p in floor.cellBounds.allPositionsWithin)
            if (floor.HasTile(p)) cells.Add(new Vector2Int(p.x, p.y));

        // 1. núcleos: celdas con piso alrededor, agrupadas en zonas conectadas
        var core = new HashSet<Vector2Int>(cells.Where(c => IsSurrounded(cells, c)));
        var coreId = new Dictionary<Vector2Int, int>();
        var coreBounds = new List<RectInt>();
        foreach (var start in core)
        {
            if (coreId.ContainsKey(start)) continue;
            int id = coreBounds.Count;
            int xMin = start.x, xMax = start.x, yMin = start.y, yMax = start.y;
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            coreId[start] = id;
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                xMin = Mathf.Min(xMin, c.x); xMax = Mathf.Max(xMax, c.x);
                yMin = Mathf.Min(yMin, c.y); yMax = Mathf.Max(yMax, c.y);
                foreach (var d in Neighbors4)
                {
                    var n = c + d;
                    if (core.Contains(n) && !coreId.ContainsKey(n)) { coreId[n] = id; stack.Push(n); }
                }
            }
            // el núcleo no incluye el anillo de borde de la sala: lo sumamos
            coreBounds.Add(new RectInt(xMin - 1, yMin - 1, xMax - xMin + 3, yMax - yMin + 3));
        }

        // 2. el resto del piso (bordes y pasillos) va a la sala más cercana caminando
        var roomOf = new Dictionary<Vector2Int, int>(coreId);
        var queue = new Queue<Vector2Int>(coreId.Keys);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in Neighbors4)
            {
                var n = c + d;
                if (cells.Contains(n) && !roomOf.ContainsKey(n)) { roomOf[n] = roomOf[c]; queue.Enqueue(n); }
            }
        }

        // 3. numeradas de arriba hacia abajo y de izquierda a derecha
        var order = Enumerable.Range(0, coreBounds.Count)
            .OrderByDescending(i => coreBounds[i].yMax).ThenBy(i => coreBounds[i].xMin).ToList();
        var rooms = new Room[coreBounds.Count];
        for (int n = 0; n < order.Count; n++)
            rooms[order[n]] = new Room { Number = n + 1, Bounds = coreBounds[order[n]] };

        foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
        {
            var cell = (Vector2Int)floor.WorldToCell(ai.transform.position);
            if (!roomOf.TryGetValue(cell, out int id)) continue;
            rooms[id].EnemyNames.Add(ai.name);
            var health = ai.GetComponent<HealthSystem>();
            if (health != null) rooms[id].TotalHealth += health.MaxHealth;
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null && roomOf.TryGetValue((Vector2Int)floor.WorldToCell(player.transform.position), out int playerRoom))
            rooms[playerRoom].HasPlayer = true;

        return rooms.OrderBy(r => r.Number).ToList();
    }

    private static bool IsSurrounded(HashSet<Vector2Int> cells, Vector2Int c)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (!cells.Contains(new Vector2Int(c.x + dx, c.y + dy))) return false;
        return true;
    }
}
