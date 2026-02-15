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
            "Aim the Scene View camera where you want, then click the button below to copy its transform into this SO.",
            MessageType.Info);

        if (GUILayout.Button("Capture Camera Position"))
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Debug.LogWarning("[ApartmentAreaEditor] No active Scene View found.");
                return;
            }

            Undo.RecordObject(area, "Capture Camera Position");
            area.cameraPosition = sv.camera.transform.position;
            area.cameraRotation = sv.camera.transform.eulerAngles;
            area.cameraFOV = sv.camera.fieldOfView;
            EditorUtility.SetDirty(area);

            Debug.Log($"[ApartmentAreaEditor] Captured camera for '{area.areaName}': " +
                $"pos={area.cameraPosition}, rot={area.cameraRotation}, fov={area.cameraFOV:F1}");
        }
    }
}
