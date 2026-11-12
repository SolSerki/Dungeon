using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// Splits the floor into rooms and counts the enemies in each one.
// A room is an area with cells surrounded by floor on all 8 sides: corridors 1 or 2 tiles wide
// don't meet that, so they separate rooms; their cells go to the nearest room.
public static class LevelRoomAnalyzer
{
    public class Room
    {
        public int Number;
        public RectInt Bounds;
        // names rather than the EnemyAI: entering Play mode destroys the objects and the list would break
        public readonly List<string> EnemyNames = new List<string>();
        public float TotalHealth;
        public bool HasPlayer;
        // all the floor that belongs to it: the core, its edge and the nearest corridors
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
    }

    private static readonly Vector2Int[] Neighbors4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public static List<Room> Analyze(Tilemap floor)
    {
        floor.CompressBounds();
        var cells = new HashSet<Vector2Int>();
        foreach (var p in floor.cellBounds.allPositionsWithin)
            if (floor.HasTile(p)) cells.Add(new Vector2Int(p.x, p.y));

        // 1. cores: cells with floor all around, grouped into connected areas
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
            // the core doesn't include the room's edge ring: add it
            coreBounds.Add(new RectInt(xMin - 1, yMin - 1, xMax - xMin + 3, yMax - yMin + 3));
        }

        // 2. the rest of the floor (edges and corridors) goes to the nearest room by walking distance
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

        // 3. numbered top to bottom and left to right
        var order = Enumerable.Range(0, coreBounds.Count)
            .OrderByDescending(i => coreBounds[i].yMax).ThenBy(i => coreBounds[i].xMin).ToList();
        var rooms = new Room[coreBounds.Count];
        for (int n = 0; n < order.Count; n++)
            rooms[order[n]] = new Room { Number = n + 1, Bounds = coreBounds[order[n]] };
        foreach (var (cell, id) in roomOf) rooms[id].Cells.Add(cell);

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
