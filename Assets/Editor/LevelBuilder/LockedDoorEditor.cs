using UnityEditor;
using UnityEngine;

// Adds the same controls shown in the Scene view to the door's Inspector
[CustomEditor(typeof(LockedDoor))]
public class LockedDoorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var door = (LockedDoor)target;

        // the prefab asset has no grid or floor: this only makes sense in the scene
        if (!door.gameObject.scene.IsValid()) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rotate left")) DoorPlacement.Rotate(door, 90f);
            if (GUILayout.Button("Rotate right (Shift+R)")) DoorPlacement.Rotate(door, -90f);
        }
        using (new EditorGUI.DisabledScope(DoorPlacement.IsSnapped(door)))
        {
            if (GUILayout.Button(DoorPlacement.IsSnapped(door) ? "On the grid" : "Snap to grid")) DoorPlacement.Snap(door);
        }

        var problem = DoorPlacement.FindProblem(door, DoorPlacement.FindFloor());
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        else EditorGUILayout.HelpBox("Drag it in the Scene view: it snaps cell by cell. The green box shows the cells it covers.", MessageType.None);
    }
}
