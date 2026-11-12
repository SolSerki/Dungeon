using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// From the painted floor: fixes the floor edges and regenerates all walls and the fill.
// It looks at all the floor together (the level's and every modular pocket's), so where a pocket touches
// the level no extra walls appear. Each edge and each wall is written to the tilemap of the floor
// that produces it: a modular pocket's walls end up in its "Pocket Walls" and move with it.
public static class LevelAutoTiler
{
    public static void Build(Tilemap floor, Tilemap walls, LevelTileRules rules, int margin, LevelBuildReport report) =>
        Build(LevelFloors.Collect(floor, walls), rules, margin, report);

    public static void Build(LevelFloors floors, LevelTileRules rules, int margin, LevelBuildReport report)
    {
        floors.Refresh();
        var cells = new HashSet<Vector2Int>(floors.Cells);
        var floor = floors.MainFloor;
        var walls = floors.Main.Walls;

        if (cells.Count == 0)
        {
            report.Warn("The floor tilemap is empty: paint the floor first.", floor);
            return;
        }

        foreach (var (cell, pocket) in floors.Overlaps().GroupBy(o => o.pocket).Select(g => g.First()))
            report.Warn($"{pocket.name}: floor is painted both in the level's Floor and in the pocket's (for example at {cell}). " +
                        "They overlap: erase it from the level's Floor.", pocket, (Vector3)(cell + new Vector2(0.5f, 0.5f)));

        bool F(int x, int y) => cells.Contains(new Vector2Int(x, y));

        // Edge and wall variants alternate by position. Inside a maze
        // piece (PieceInstance) they're counted from the piece's origin: that way every copy of
        // a piece picks the same variants, wherever it is, and the seams don't show.
        var origins = PieceOrigins();
        Vector2Int Rel(int x, int y)
        {
            var c = new Vector2Int(x, y);
            return origins.TryGetValue(c, out var origin) ? c - origin : c;
        }

        var targets = floors.UndoTargets();
        Undo.RegisterCompleteObjectUndo(targets, "Generate walls");

        int narrow = 0;
        foreach (var c in cells)
        {
            var layer = floors.OwnerOf(c);
            var p = (Vector3Int)(c - layer.Offset);
            var current = layer.Floor.GetTile(p);
            var tile = FloorTile(F, Rel, c.x, c.y, current, rules);
            if (tile != null && tile != current) layer.Floor.SetTile(p, tile);

            // the NavMeshAgent has radius 0.5: it can't fit through a 1-tile gap
            bool sealedX = !F(c.x - 1, c.y) && !F(c.x + 1, c.y);
            bool sealedY = !F(c.x, c.y - 1) && !F(c.x, c.y + 1);
            if (sealedX || sealedY) narrow++;
        }

        foreach (var w in floors.Layers.Select(l => l.Walls).Where(w => w != null).Distinct()) w.ClearAllTiles();

        // A wall goes to the tilemap of the floor that produces it (the one below first, as in WallTile); the fill, to the level's
        LevelFloors.Layer WallOwner(int x, int y)
        {
            foreach (var n in WallNeighbors)
            {
                var layer = floors.OwnerOf(new Vector2Int(x + n.x, y + n.y));
                if (layer != null) return layer;
            }
            return floors.Main;
        }

        int minX = cells.Min(c => c.x), maxX = cells.Max(c => c.x), minY = cells.Min(c => c.y), maxY = cells.Max(c => c.y);
        int conflicts = 0;
        for (int x = minX - margin; x <= maxX + margin; x++)
        {
            for (int y = minY - margin; y <= maxY + margin; y++)
            {
                if (F(x, y)) continue;

                bool up = F(x, y + 1), dn = F(x, y - 1), lf = F(x - 1, y), rt = F(x + 1, y);
                if ((up && dn) || (lf && rt && !up && !dn))
                {
                    conflicts++;
                    report.Warn($"1-tile wall between two floor areas at cell ({x}, {y}): leave at least 2 tiles between them.", walls, new Vector3(x + 0.5f, y + 0.5f));
                }
                var owner = WallOwner(x, y);
                var ownerWalls = owner.Walls != null ? owner.Walls : walls;
                ownerWalls.SetTile((Vector3Int)(new Vector2Int(x, y) - (owner.Walls != null ? owner.Offset : Vector2Int.zero)), WallTile(F, Rel, x, y, rules));
            }
        }

        foreach (var t in targets.OfType<Tilemap>())
        {
            t.CompressBounds();
            EditorUtility.SetDirty(t);
        }

        if (narrow > 0) report.Warn($"{narrow} floor cells are 1 tile wide: enemies can't pass through there (use corridors 2 or more wide).", floor);
        report.Info($"Floor: {cells.Count} cells. Walls and fill: {walls.cellBounds.size.x}x{walls.cellBounds.size.y} tiles.");
    }

    private static readonly Vector2Int[] WallNeighbors =
    {
        new(0, -1), new(0, 1), new(1, 0), new(-1, 0), new(1, -1), new(-1, -1), new(1, 1), new(-1, 1),
    };

    // Cell -> origin of the maze piece that contains it
    private static Dictionary<Vector2Int, Vector2Int> PieceOrigins()
    {
        var map = new Dictionary<Vector2Int, Vector2Int>();
        foreach (var piece in Object.FindObjectsByType<PieceInstance>(FindObjectsInactive.Exclude))
        {
            if (piece.HasCellList)
                foreach (var c in piece.CellList) map[c] = piece.Origin;
            else
                foreach (var c in piece.Rect.allPositionsWithin) map[c] = piece.Origin;
        }
        return map;
    }

    private static TileBase FloorTile(System.Func<int, int, bool> F, System.Func<int, int, Vector2Int> rel, int x, int y, TileBase current, LevelTileRules r)
    {
        bool u = !F(x, y + 1), d = !F(x, y - 1), l = !F(x - 1, y), rt = !F(x + 1, y);
        if (u && l) return r.floorTopLeft;
        if (u && rt) return r.floorTopRight;
        if (d && l) return r.floorBottomLeft;
        if (d && rt) return r.floorBottomRight;
        if (u) return LevelTileRules.Pick(r.floorTop, rel(x, y).x);
        if (d) return LevelTileRules.Pick(r.floorBottom, rel(x, y).x);
        if (l) return r.floorLeft;
        if (rt) return r.floorRight;

        // interior: keep the variant you painted, unless it's an edge that ended up inside
        return r.IsFloorEdge(current) ? r.floorInterior : current;
    }

    // A wall's variant is counted from the piece of the floor that produces it
    private static TileBase WallTile(System.Func<int, int, bool> F, System.Func<int, int, Vector2Int> rel, int x, int y, LevelTileRules r)
    {
        bool up = F(x, y + 1), dn = F(x, y - 1), lf = F(x - 1, y), rt = F(x + 1, y);

        // floor below: top wall of a room (takes priority where it meets corridors)
        if (dn)
        {
            bool leftEnd = !F(x - 1, y - 1), rightEnd = !F(x + 1, y - 1);
            if (leftEnd && !rightEnd) return r.wallTopLeftEnd;
            if (rightEnd && !leftEnd) return r.wallTopRightEnd;
            return LevelTileRules.Pick(r.wallTop, rel(x, y - 1).x);
        }

        // floor above: bottom wall
        if (up)
        {
            if (rt && !lf) return r.bottomJoinFloorRight;
            if (lf && !rt) return r.bottomJoinFloorLeft;
            bool leftEnd = !F(x - 1, y + 1), rightEnd = !F(x + 1, y + 1);
            if (leftEnd && !rightEnd) return r.wallBottomLeftEnd;
            if (rightEnd && !leftEnd) return r.wallBottomRightEnd;
            return LevelTileRules.Pick(r.wallBottom, rel(x, y + 1).x);
        }

        if (rt) return LevelTileRules.Pick(r.wallLeft, rel(x + 1, y).y);
        if (lf) return LevelTileRules.Pick(r.wallRight, rel(x - 1, y).y);

        // floor only diagonally: outer corners
        if (F(x + 1, y - 1)) return r.cornerTopLeft;
        if (F(x - 1, y - 1)) return r.cornerTopRight;
        if (F(x + 1, y + 1)) return r.cornerBottomLeft;
        if (F(x - 1, y + 1)) return r.cornerBottomRight;

        return r.voidTile;
    }
}
