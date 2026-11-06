using UnityEditor;
using UnityEngine;

// Suma al Inspector de la puerta los mismos controles que aparecen en la Scene view
[CustomEditor(typeof(LockedDoor))]
public class LockedDoorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var door = (LockedDoor)target;

        // en el asset del prefab no hay grilla ni piso: solo tiene sentido en la escena
        if (!door.gameObject.scene.IsValid()) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colocación", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Girar izq.")) DoorPlacement.Rotate(door, 90f);
            if (GUILayout.Button("Girar der. (Shift+R)")) DoorPlacement.Rotate(door, -90f);
        }
        using (new EditorGUI.DisabledScope(DoorPlacement.IsSnapped(door)))
        {
            if (GUILayout.Button(DoorPlacement.IsSnapped(door) ? "En la grilla" : "Ajustar a la grilla")) DoorPlacement.Snap(door);
        }

        var problem = DoorPlacement.FindProblem(door, DoorPlacement.FindFloor());
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        else EditorGUILayout.HelpBox("Arrastrala en la Scene view: salta de a celdas. El recuadro verde son las celdas que tapa.", MessageType.None);
    }
}
