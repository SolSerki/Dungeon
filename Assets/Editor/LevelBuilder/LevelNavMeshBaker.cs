using System.IO;
using NavMeshPlus.Components;
using UnityEditor;
using UnityEditor.SceneManagement;

// Hornea el NavMesh en un asset propio de la escena: Assets/Scenes/<Escena>/NavMesh-<Objeto>.asset
public static class LevelNavMeshBaker
{
    public static void Bake(NavMeshSurface surface, LevelBuildReport report)
    {
        var scene = surface.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path))
        {
            report.Warn("Guardá la escena antes de hornear: el NavMesh se guarda en una carpeta con su nombre.");
            return;
        }

        string parent = Path.GetDirectoryName(scene.path).Replace('\\', '/');
        string folder = $"{parent}/{scene.name}";
        string path = $"{folder}/NavMesh-{surface.name}.asset";

        // Una escena duplicada sigue apuntando al NavMesh de la original. El Bake de NavMeshPlus
        // borra el asset al que apunta, así que lo desvinculamos antes para no romper el otro nivel.
        var old = surface.navMeshData;
        string oldPath = old != null ? AssetDatabase.GetAssetPath(old) : null;
        if (!string.IsNullOrEmpty(oldPath) && oldPath != path)
            report.Info($"El NavMesh apuntaba a {oldPath}; ese asset no se tocó.");

        surface.RemoveData();
        surface.navMeshData = null;
        surface.BuildNavMesh();

        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, scene.name);
        AssetDatabase.CreateAsset(surface.navMeshData, path); // reemplaza el de esta escena si ya existía

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        report.Info($"NavMesh horneado en {path}.");
    }
}
