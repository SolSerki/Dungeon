using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Sorts the EnemySpawns into "Room X" folders (RoomGroup): one per room, inside "EnemySpawns" or, if the
// room belongs to a pocket, inside the pocket (so it moves with it and travels in its prefab). Runs on every
// Build with the same rooms and numbers as the window's room summary.
public static class SpawnRooms
{
    public static void Sort(List<LevelRoomAnalyzer.Room> rooms, Tilemap floor, LevelBuildReport report)
    {
        var scene = SceneManager.GetActiveScene();
        var spawnsRoot = EnemySpawnBuilder.GetOrCreateRoot(EnemySpawnBuilder.SpawnsRootName, scene).transform;
        var pockets = Object.FindObjectsByType<Pocket>(FindObjectsInactive.Exclude);

        // A room belongs to a pocket if more than half of its floor is the pocket's
        Pocket OwnerOf(LevelRoomAnalyzer.Room room)
        {
            Pocket best = null;
            int bestCount = 0;
            foreach (var pocket in pockets)
            {
                int count = room.Cells.Count(c => pocket.IsModular ? pocket.HasFloor(c, floor) : pocket.Area.Contains(c));
                if (count > bestCount)
                {
                    best = pocket;
                    bestCount = count;
                }
            }
            return bestCount * 2 > room.Cells.Count ? best : null;
        }

        // 1. One folder per room
        var groupOfCell = new Dictionary<Vector2Int, RoomGroup>();
        var current = new HashSet<RoomGroup>();
        int inPockets = 0;
        foreach (var room in rooms)
        {
            var pocket = OwnerOf(room);
            if (pocket != null) inPockets++;
            var group = GetOrCreateGroup(pocket != null ? pocket.transform : spawnsRoot, room);
            if (group == null) continue;
            current.Add(group);
            foreach (var c in room.Cells) groupOfCell[c] = group;
        }

        // 2. Each spawn into its room's folder. Spawns you put in a folder of your own (not "Room X") are left alone.
        int moved = 0, locked = 0;
        var offFloor = new List<EnemySpawn>();
        var custom = new HashSet<string>();
        foreach (var spawn in EnemySpawnBuilder.FindSpawns())
        {
            var parent = spawn.transform.parent;
            bool sortable = parent == null || parent == spawnsRoot || parent.GetComponent<RoomGroup>() != null || parent.GetComponent<Pocket>() != null;
            if (!sortable)
            {
                custom.Add(parent.name);
                continue;
            }

            var cell = (Vector2Int)floor.WorldToCell(spawn.transform.position);
            if (!groupOfCell.TryGetValue(cell, out var group))
            {
                offFloor.Add(spawn);
                continue;
            }
            if (spawn.transform.parent == group.transform) continue;
            if (!CanEdit(spawn.gameObject))
            {
                locked++;
                continue;
            }
            Undo.SetTransformParent(spawn.transform, group.transform, "Sort spawns by room");
            moved++;
        }

        // 3. Folders that no longer match any room and are empty
        foreach (var old in Object.FindObjectsByType<RoomGroup>(FindObjectsInactive.Include))
            if (!current.Contains(old) && old.transform.childCount == 0 && CanEdit(old.gameObject))
                Undo.DestroyObjectImmediate(old.gameObject);

        // 4. In the hierarchy, Room 1, Room 2... at the end of their parent
        foreach (var group in current.OrderBy(g => g.Number))
            if (CanEdit(group.gameObject)) group.transform.SetAsLastSibling();

        report.Info($"Rooms: {rooms.Count} \"Room X\" folders ({inPockets} inside pockets), {moved} spawns moved to their room.");
        if (custom.Count > 0)
            report.Info($"Spawns in your own folders were left alone ({string.Join(", ", custom)}). To have them sorted automatically, move them out to \"{EnemySpawnBuilder.SpawnsRootName}\".");
        if (locked > 0)
            report.Warn($"{locked} spawns are part of a prefab and couldn't be moved to their room. Move them in the prefab or unpack it.");
        if (offFloor.Count > 0)
            report.Warn($"{offFloor.Count} spawns aren't on any room's floor ({string.Join(", ", offFloor.Take(5).Select(s => s.name))}), " +
                        "so they were left where they were. Move them inside a room.", offFloor[0], offFloor[0].transform.position);
    }

    // That parent's "Room N" folder (creates or updates it). It sits at the room's center.
    private static RoomGroup GetOrCreateGroup(Transform parent, LevelRoomAnalyzer.Room room)
    {
        var center = new Vector3(room.Bounds.center.x, room.Bounds.center.y, 0f);
        var group = parent.Cast<Transform>().Select(t => t.GetComponent<RoomGroup>()).FirstOrDefault(g => g != null && g.Number == room.Number);

        // A hand-made "Room N" folder is reused instead of creating a duplicate
        if (group == null)
        {
            var manual = parent.Cast<Transform>().FirstOrDefault(t => t.name == $"Room {room.Number}" && t.GetComponent<RoomGroup>() == null);
            if (manual != null && CanEdit(manual.gameObject)) group = Undo.AddComponent<RoomGroup>(manual.gameObject);
        }

        if (group == null)
        {
            if (!CanEditChildren(parent.gameObject)) return null;
            var go = new GameObject($"Room {room.Number}");
            Undo.RegisterCreatedObjectUndo(go, "Room folder");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            group = Undo.AddComponent<RoomGroup>(go);
        }
        else if (Vector3.Distance(group.transform.position, center) > 0.001f && CanEdit(group.gameObject))
        {
            // The room moved: move the folder without moving its contents
            var children = group.transform.Cast<Transform>().ToList();
            var positions = children.Select(c => c.position).ToList();
            Undo.RecordObject(group.transform, "Room folder");
            group.transform.position = center;
            for (int i = 0; i < children.Count; i++)
            {
                Undo.RecordObject(children[i], "Room folder");
                children[i].position = positions[i];
            }
        }

        Undo.RecordObject(group, "Room folder");
        group.Setup(room.Number, room.Bounds.size);
        if (group.name != $"Room {room.Number}" && CanEdit(group.gameObject))
        {
            Undo.RecordObject(group.gameObject, "Room folder");
            group.name = $"Room {room.Number}";
        }
        return group;
    }

    // Anything coming from a prefab (for example, inside a pocket saved as a prefab) can't be moved;
    // anything added to the instance can
    private static bool CanEdit(GameObject go) =>
        !PrefabUtility.IsPartOfPrefabInstance(go) || PrefabUtility.IsAddedGameObjectOverride(go);

    private static bool CanEditChildren(GameObject parent) => true; // adding children to a prefab instance is allowed

    // For "Place spawn": the folder of the room containing that position (by the room's rectangle), or null
    public static Transform GroupAt(Vector3 position)
    {
        var cell = Vector2Int.FloorToInt(position);
        var group = Object.FindObjectsByType<RoomGroup>(FindObjectsInactive.Exclude)
            .Where(g => g.Rect.Contains(cell))
            .OrderBy(g => g.Rect.width * g.Rect.height) // the smallest one, if they overlap
            .FirstOrDefault();
        return group != null ? group.transform : null;
    }
}
