using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// All of the level's floor: the main Floor plus every modular pocket's (each has its own
// Grid and can be moved). Cells are handled in world coordinates; each one knows which
// tilemap it lives in, so the auto-tiler writes edges and walls to the right tilemap.
public class LevelFloors
{
    public class Layer
    {
        public Tilemap Floor;
        public Tilemap Walls;
        public Vector2Int Offset; // world cell = tilemap cell + Offset
        public Pocket Pocket;     // null = the level
    }

    public readonly List<Layer> Layers = new();
    private readonly Dictionary<Vector2Int, Layer> _owner = new();

    public Layer Main => Layers[0];
    public Tilemap MainFloor => Main.Floor;
    public IEnumerable<Vector2Int> Cells => _owner.Keys;
    public int Count => _owner.Count;

    public static LevelFloors Collect(Tilemap mainFloor, Tilemap mainWalls)
    {
        var floors = new LevelFloors();
        floors.Layers.Add(new Layer { Floor = mainFloor, Walls = mainWalls, Offset = Vector2Int.zero });
        foreach (var pocket in Object.FindObjectsByType<Pocket>(FindObjectsInactive.Exclude).Where(p => p.IsModular).OrderBy(p => p.name))
            floors.Layers.Add(new Layer { Floor = pocket.Floor, Walls = pocket.Walls != null ? pocket.Walls : mainWalls, Offset = pocket.CellOffset, Pocket = pocket });
        floors.Refresh();
        return floors;
    }

    // Re-reads which cells have floor (after painting or erasing)
    public void Refresh()
    {
        _owner.Clear();
        foreach (var layer in Layers)
        {
            layer.Offset = layer.Pocket != null ? layer.Pocket.CellOffset : Vector2Int.zero;
            layer.Floor.CompressBounds();
            foreach (var p in layer.Floor.cellBounds.allPositionsWithin)
                if (layer.Floor.HasTile(p)) _owner[(Vector2Int)p + layer.Offset] = layer; // pockets cover the level
        }
    }

    public bool Has(Vector2Int cell) => _owner.ContainsKey(cell);
    public Layer OwnerOf(Vector2Int cell) => _owner.TryGetValue(cell, out var layer) ? layer : null;

    public TileBase Get(Vector2Int cell) =>
        _owner.TryGetValue(cell, out var layer) ? layer.Floor.GetTile((Vector3Int)(cell - layer.Offset)) : null;

    // Cells painted both in the level and in a pocket (they overlap)
    public List<(Vector2Int cell, Pocket pocket)> Overlaps()
    {
        var list = new List<(Vector2Int, Pocket)>();
        foreach (var layer in Layers.Skip(1))
            foreach (var (cell, owner) in _owner)
                if (owner == layer && MainFloor.HasTile((Vector3Int)cell)) list.Add((cell, layer.Pocket));
        return list;
    }

    // A temporary tilemap (hidden, never saved) with all the floor merged, for the validators that
    // take a single tilemap. Destroy it with DestroyUnion when done.
    // includeGenerated = false leaves out the pockets' rotated sheets and the tunnels'
    // continuations: they're copies, not new places (so rooms aren't counted twice)
    public Tilemap CreateUnion(bool includeGenerated = true)
    {
        var skip = includeGenerated ? new HashSet<Vector2Int>() : GeneratedCells();
        var go = new GameObject("~Piso unido (temporal)") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(MainFloor.transform.parent, false);
        var union = go.AddComponent<Tilemap>();
        foreach (var cell in Cells.ToList())
            if (!skip.Contains(cell)) union.SetTile((Vector3Int)cell, Get(cell));
        return union;
    }

    // Floor painted by Build (pockets and tunnels)
    public static HashSet<Vector2Int> GeneratedCells()
    {
        var cells = new HashSet<Vector2Int>();
        foreach (var root in Object.FindObjectsByType<MazeGenerated>(FindObjectsInactive.Exclude)) cells.UnionWith(root.FloorCells);
        foreach (var root in Object.FindObjectsByType<MazeConnections>(FindObjectsInactive.Exclude)) cells.UnionWith(root.GeneratedFloor);
        foreach (var pocket in Object.FindObjectsByType<Pocket>(FindObjectsInactive.Exclude)) cells.UnionWith(pocket.GeneratedCells);
        return cells;
    }

    public static void DestroyUnion(Tilemap union)
    {
        if (union != null) Object.DestroyImmediate(union.gameObject);
    }

    public Object[] UndoTargets() =>
        Layers.SelectMany(l => new Object[] { l.Floor, l.Walls }).Where(o => o != null).Distinct().ToArray();
}
