using System.Collections.Generic;
using UnityEngine;

// What the Level Builder found: shown in the window and in the console.
// A warning can carry the exact place of the problem: the window marks it in the scene
// and the "Show" button moves the camera there.
public class LevelBuildReport
{
    public readonly List<(string message, Object context, Vector3? place)> Warnings = new List<(string, Object, Vector3?)>();
    public readonly List<string> Infos = new List<string>();

    public void Warn(string message, Object context = null, Vector3? place = null)
    {
        Warnings.Add((message, context, place));
        Debug.LogWarning("[Level Builder] " + message, context);
    }

    public void Info(string message) => Infos.Add(message);
}
