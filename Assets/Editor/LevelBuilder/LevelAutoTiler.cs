using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// A partir del piso pintado: corrige los bordes del piso y regenera todas las paredes y el relleno
public static class LevelAutoTiler
{
    public static void Build(Tilemap floor, Tilemap walls, LevelTileRules rules, int margin, LevelBuildReport report)
    {
        floor.CompressBounds();
        var cells = new HashSet<Vector2Int>();
        foreach (var p in floor.cellBounds.allPositionsWithin)
            if (floor.HasTile(p)) cells.Add(new Vector2Int(p.x, p.y));

        if (cells.Count == 0)
        {
            report.Warn("El tilemap de piso está vacío: pintá el piso primero.", floor);
            return;
        }

        bool F(int x, int y) => cells.Contains(new Vector2Int(x, y));

        Undo.RegisterCompleteObjectUndo(new Object[] { floor, walls }, "Generar paredes");

        int narrow = 0;
        foreach (var c in cells)
        {
            var p = new Vector3Int(c.x, c.y, 0);
            var tile = FloorTile(F, c.x, c.y, floor.GetTile(p), rules);
            if (tile != null && tile != floor.GetTile(p)) floor.SetTile(p, tile);

            // el NavMeshAgent tiene radio 0.5: por un hueco de 1 tile no pasa
            bool sealedX = !F(c.x - 1, c.y) && !F(c.x + 1, c.y);
            bool sealedY = !F(c.x, c.y - 1) && !F(c.x, c.y + 1);
            if (sealedX || sealedY) narrow++;
        }

        walls.ClearAllTiles();
        var b = floor.cellBounds;
        int conflicts = 0;
        for (int x = b.xMin - margin; x < b.xMax + margin; x++)
        {
            for (int y = b.yMin - margin; y < b.yMax + margin; y++)
            {
                if (F(x, y)) continue;

                bool up = F(x, y + 1), dn = F(x, y - 1), lf = F(x - 1, y), rt = F(x + 1, y);
                if ((up && dn) || (lf && rt && !up && !dn))
                {
                    conflicts++;
                    report.Warn($"Pared de 1 tile entre dos zonas de piso en la celda ({x}, {y}): dejá al menos 2 tiles de separación.", walls);
                }
                walls.SetTile(new Vector3Int(x, y, 0), WallTile(F, x, y, rules));
            }
        }

        walls.CompressBounds();
        EditorUtility.SetDirty(floor);
        EditorUtility.SetDirty(walls);

        if (narrow > 0) report.Warn($"Hay {narrow} celdas de piso de 1 tile de ancho: los enemigos no pueden pasar por ahí (usá pasillos de 2 o más).", floor);
        report.Info($"Piso: {cells.Count} celdas. Paredes y relleno: {walls.cellBounds.size.x}x{walls.cellBounds.size.y} tiles.");
    }

    private static TileBase FloorTile(System.Func<int, int, bool> F, int x, int y, TileBase current, LevelTileRules r)
    {
        bool u = !F(x, y + 1), d = !F(x, y - 1), l = !F(x - 1, y), rt = !F(x + 1, y);
        if (u && l) return r.floorTopLeft;
        if (u && rt) return r.floorTopRight;
        if (d && l) return r.floorBottomLeft;
        if (d && rt) return r.floorBottomRight;
        if (u) return LevelTileRules.Pick(r.floorTop, x);
        if (d) return LevelTileRules.Pick(r.floorBottom, x);
        if (l) return r.floorLeft;
        if (rt) return r.floorRight;

        // interior: respetamos la variante que pintaste, salvo que sea un borde que quedó adentro
        return r.IsFloorEdge(current) ? r.floorInterior : current;
    }

    private static TileBase WallTile(System.Func<int, int, bool> F, int x, int y, LevelTileRules r)
    {
        bool up = F(x, y + 1), dn = F(x, y - 1), lf = F(x - 1, y), rt = F(x + 1, y);

        // piso abajo: pared de arriba de una sala (tiene prioridad en las uniones con pasillos)
        if (dn)
        {
            bool leftEnd = !F(x - 1, y - 1), rightEnd = !F(x + 1, y - 1);
            if (leftEnd && !rightEnd) return r.wallTopLeftEnd;
            if (rightEnd && !leftEnd) return r.wallTopRightEnd;
            return LevelTileRules.Pick(r.wallTop, x);
        }

        // piso arriba: pared de abajo
        if (up)
        {
            if (rt && !lf) return r.bottomJoinFloorRight;
            if (lf && !rt) return r.bottomJoinFloorLeft;
            bool leftEnd = !F(x - 1, y + 1), rightEnd = !F(x + 1, y + 1);
            if (leftEnd && !rightEnd) return r.wallBottomLeftEnd;
            if (rightEnd && !leftEnd) return r.wallBottomRightEnd;
            return LevelTileRules.Pick(r.wallBottom, x);
        }

        if (rt) return LevelTileRules.Pick(r.wallLeft, y);
        if (lf) return LevelTileRules.Pick(r.wallRight, y);

        // solo piso en diagonal: esquinas exteriores
        if (F(x + 1, y - 1)) return r.cornerTopLeft;
        if (F(x - 1, y - 1)) return r.cornerTopRight;
        if (F(x + 1, y + 1)) return r.cornerBottomLeft;
        if (F(x - 1, y + 1)) return r.cornerBottomRight;

        return r.voidTile;
    }
}
