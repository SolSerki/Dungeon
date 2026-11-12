using UnityEditor;

// Level Builder options remembered between sessions (per user, not stored in the project)
public static class LevelBuilderPrefs
{
    private const string Prefix = "Dungeon.LevelBuilder.";

    public static bool SnapWaypoints
    {
        get => EditorPrefs.GetBool(Prefix + "SnapWaypoints", true);
        set => EditorPrefs.SetBool(Prefix + "SnapWaypoints", value);
    }

    public static bool ShowAllRanges
    {
        get => EditorPrefs.GetBool(Prefix + "ShowAllRanges", false);
        set => EditorPrefs.SetBool(Prefix + "ShowAllRanges", value);
    }

    public static bool ShowRooms
    {
        get => EditorPrefs.GetBool(Prefix + "ShowRooms", true);
        set => EditorPrefs.SetBool(Prefix + "ShowRooms", value);
    }
}
