using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;
using Iris.Apartment;

[CustomEditor(typeof(CameraPresetDefinition))]
public class CameraPresetDefinitionEditor : Editor
{
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preset = (CameraPresetDefinition)target;
        if (preset.areaConfigs == null || preset.areaConfigs.Length == 0) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Scene View Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Aim the Scene View camera, then click a button to capture position + rotation + lens into that area slot.",
            MessageType.Info);

        for (int i = 0; i < preset.areaConfigs.Length; i++)
        {
            string label = string.IsNullOrEmpty(preset.areaConfigs[i].areaLabel)
                ? $"Area {i}"
                : preset.areaConfigs[i].areaLabel;

            if (GUILayout.Button($"Capture Scene View → {label}"))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv == null)
                {
                    Debug.LogWarning("[CameraPresetEditor] No active Scene View found.");
                    continue;
                }

                Undo.RecordObject(preset, "Capture Camera Preset");
                var config = preset.areaConfigs[i];
                config.position = sv.camera.transform.position;
                config.rotation = sv.camera.transform.eulerAngles;

                var lens = config.lens;
                lens.FieldOfView = sv.camera.fieldOfView;
                lens.NearClipPlane = sv.camera.nearClipPlane;
                lens.FarClipPlane = sv.camera.farClipPlane;
                if (sv.orthographic)
                {
                    lens.OrthographicSize = sv.camera.orthographicSize;
                    lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
                }
                else
                {
                    lens.ModeOverride = LensSettings.OverrideModes.None;
                }
                config.lens = lens;

                preset.areaConfigs[i] = config;
                EditorUtility.SetDirty(preset);

                Debug.Log($"[CameraPresetEditor] Captured '{label}': pos={config.position}, rot={config.rotation}, fov={lens.FieldOfView:F1}");
            }
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        var preset = target as CameraPresetDefinition;
        if (preset == null || preset.areaConfigs == null) return;

        for (int i = 0; i < preset.areaConfigs.Length; i++)
        {
            var config = preset.areaConfigs[i];
            Vector3 pos = config.position;
            Quaternion rot = Quaternion.Euler(config.rotation);

            // Sphere at camera position
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.2f, EventType.Repaint);

            // Label
            string label = string.IsNullOrEmpty(config.areaLabel) ? $"Area {i}" : config.areaLabel;
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white }
            };
            Handles.Label(pos + Vector3.up * 0.35f, $"{preset.label} — {label}", style);

            // Draw frustum
            bool isOrtho = config.lens.ModeOverride == LensSettings.OverrideModes.Orthographic;
            float fov = isOrtho ? 60f : Mathf.Max(config.lens.FieldOfView, 5f);
            float near = Mathf.Max(config.lens.NearClipPlane, 0.1f);
            float far = Mathf.Min(config.lens.FarClipPlane, 20f); // clamp for visibility
            float aspect = 16f / 9f;

            if (isOrtho)
            {
                float size = config.lens.OrthographicSize;
                DrawOrthoFrustum(pos, rot, size, aspect, near, far);
            }
            else
            {
                DrawPerspectiveFrustum(pos, rot, fov, aspect, near, far);
            }
        }
    }

    private static void DrawPerspectiveFrustum(Vector3 pos, Quaternion rot, float fov, float aspect, float near, float far)
    {
        float halfFovRad = fov * 0.5f * Mathf.Deg2Rad;

        float nearH = Mathf.Tan(halfFovRad) * near;
        float nearW = nearH * aspect;
        float farH = Mathf.Tan(halfFovRad) * far;
        float farW = farH * aspect;

        Vector3 forward = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;

        Vector3 nc = pos + forward * near;
        Vector3 fc = pos + forward * far;

        Vector3[] nearCorners = new Vector3[4];
        nearCorners[0] = nc + up * nearH - right * nearW; // top-left
        nearCorners[1] = nc + up * nearH + right * nearW; // top-right
        nearCorners[2] = nc - up * nearH + right * nearW; // bottom-right
        nearCorners[3] = nc - up * nearH - right * nearW; // bottom-left

        Vector3[] farCorners = new Vector3[4];
        farCorners[0] = fc + up * farH - right * farW;
        farCorners[1] = fc + up * farH + right * farW;
        farCorners[2] = fc - up * farH + right * farW;
        farCorners[3] = fc - up * farH - right * farW;

        // Near plane
        Handles.color = new Color(1f, 1f, 0f, 0.6f);
        Handles.DrawLine(nearCorners[0], nearCorners[1]);
        Handles.DrawLine(nearCorners[1], nearCorners[2]);
        Handles.DrawLine(nearCorners[2], nearCorners[3]);
        Handles.DrawLine(nearCorners[3], nearCorners[0]);

        // Far plane
        Handles.color = new Color(1f, 0.5f, 0f, 0.4f);
        Handles.DrawLine(farCorners[0], farCorners[1]);
        Handles.DrawLine(farCorners[1], farCorners[2]);
        Handles.DrawLine(farCorners[2], farCorners[3]);
        Handles.DrawLine(farCorners[3], farCorners[0]);

        // Connecting edges
        Handles.color = new Color(1f, 0.8f, 0f, 0.3f);
        for (int j = 0; j < 4; j++)
            Handles.DrawLine(nearCorners[j], farCorners[j]);
    }

    private static void DrawOrthoFrustum(Vector3 pos, Quaternion rot, float orthoSize, float aspect, float near, float far)
    {
        float h = orthoSize;
        float w = h * aspect;

        Vector3 forward = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;

        Vector3 nc = pos + forward * near;
        Vector3 fc = pos + forward * far;

        Vector3[] nearCorners = new Vector3[4];
        nearCorners[0] = nc + up * h - right * w;
        nearCorners[1] = nc + up * h + right * w;
        nearCorners[2] = nc - up * h + right * w;
        nearCorners[3] = nc - up * h - right * w;

        Vector3[] farCorners = new Vector3[4];
        farCorners[0] = fc + up * h - right * w;
        farCorners[1] = fc + up * h + right * w;
        farCorners[2] = fc - up * h + right * w;
        farCorners[3] = fc - up * h - right * w;

        Handles.color = new Color(0f, 1f, 1f, 0.6f);
        Handles.DrawLine(nearCorners[0], nearCorners[1]);
        Handles.DrawLine(nearCorners[1], nearCorners[2]);
        Handles.DrawLine(nearCorners[2], nearCorners[3]);
        Handles.DrawLine(nearCorners[3], nearCorners[0]);

        Handles.color = new Color(0f, 1f, 1f, 0.4f);
        Handles.DrawLine(farCorners[0], farCorners[1]);
        Handles.DrawLine(farCorners[1], farCorners[2]);
        Handles.DrawLine(farCorners[2], farCorners[3]);
        Handles.DrawLine(farCorners[3], farCorners[0]);

        Handles.color = new Color(0f, 1f, 1f, 0.3f);
        for (int j = 0; j < 4; j++)
            Handles.DrawLine(nearCorners[j], farCorners[j]);
    }
}
