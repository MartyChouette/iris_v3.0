using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ApartmentAreaDefinition))]
public class ApartmentAreaDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var area = (ApartmentAreaDefinition)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Scene View Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Aim the Scene View camera where you want, then click a button below to copy its transform into this SO.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Selected Camera"))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv == null)
                {
                    Debug.LogWarning("[ApartmentAreaEditor] No active Scene View found.");
                    return;
                }

                Undo.RecordObject(area, "Capture Selected Camera");
                area.selectedPosition = sv.camera.transform.position;
                area.selectedRotation = sv.camera.transform.eulerAngles;
                area.selectedFOV = sv.camera.fieldOfView;
                EditorUtility.SetDirty(area);

                Debug.Log($"[ApartmentAreaEditor] Captured selected camera for '{area.areaName}': " +
                    $"pos={area.selectedPosition}, rot={area.selectedRotation}, fov={area.selectedFOV:F1}");
            }

            if (GUILayout.Button("Capture Look-At Point"))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv == null)
                {
                    Debug.LogWarning("[ApartmentAreaEditor] No active Scene View found.");
                    return;
                }

                Undo.RecordObject(area, "Capture Look-At Point");
                // Use the Scene View pivot as the look-at target
                area.lookAtPosition = sv.pivot;
                EditorUtility.SetDirty(area);

                Debug.Log($"[ApartmentAreaEditor] Captured look-at for '{area.areaName}': {area.lookAtPosition}");
            }
        }
    }
}
