using System.IO;
using NavMeshPlus.Components;
using UnityEditor;
using UnityEditor.SceneManagement;

// Bakes the NavMesh into an asset owned by the scene: Assets/Scenes/<Scene>/NavMesh-<Object>.asset
public static class LevelNavMeshBaker
{
    public static void Bake(NavMeshSurface surface, LevelBuildReport report)
    {
        var scene = surface.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path))
        {
            report.Warn("Save the scene before baking: the NavMesh is stored in a folder named after it.");
            return;
        }

        string parent = Path.GetDirectoryName(scene.path).Replace('\\', '/');
        string folder = $"{parent}/{scene.name}";
        string path = $"{folder}/NavMesh-{surface.name}.asset";

        // A duplicated scene still points at the original's NavMesh. NavMeshPlus's Bake
        // deletes the asset it points at, so unlink it first to avoid breaking the other level.
        var old = surface.navMeshData;
        string oldPath = old != null ? AssetDatabase.GetAssetPath(old) : null;
        if (!string.IsNullOrEmpty(oldPath) && oldPath != path)
            report.Info($"The NavMesh pointed at {oldPath}; that asset was left untouched.");

        surface.RemoveData();
        surface.navMeshData = null;
        surface.BuildNavMesh();

        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, scene.name);
        AssetDatabase.CreateAsset(surface.navMeshData, path); // replaces this scene's one if it already existed

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        report.Info($"NavMesh baked to {path}.");
    }
}
