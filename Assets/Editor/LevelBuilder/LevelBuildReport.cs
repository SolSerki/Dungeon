using System.Collections.Generic;
using UnityEngine;

// Lo que encontró el Level Builder: se muestra en la ventana y en la consola
public class LevelBuildReport
{
    public readonly List<(string message, Object context)> Warnings = new List<(string, Object)>();
    public readonly List<string> Infos = new List<string>();

    public void Warn(string message, Object context = null)
    {
        Warnings.Add((message, context));
        Debug.LogWarning("[Level Builder] " + message, context);
    }

    public void Info(string message) => Infos.Add(message);
}
