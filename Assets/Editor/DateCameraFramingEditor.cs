using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DateCameraFraming))]
public class DateCameraFramingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var framing = (DateCameraFraming)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Capture from Scene View", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Position the Scene View camera to frame Nema + date character, then click a button to capture.",
            MessageType.Info);

        DrawCaptureButton(framing, "Arrival", ref framing.arrival);
        DrawCaptureButton(framing, "Kitchen", ref framing.kitchen);
        DrawCaptureButton(framing, "Couch",   ref framing.couch);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Clear All Captures", GUILayout.Height(24)))
        {
            Undo.RecordObject(framing, "Clear Camera Captures");
            framing.arrival.captured = false;
            framing.kitchen.captured = false;
            framing.couch.captured = false;
            EditorUtility.SetDirty(framing);
        }
    }

    private void DrawCaptureButton(DateCameraFraming framing, string label, ref DateCameraFraming.PhaseFrame frame)
    {
        EditorGUILayout.BeginHorizontal();

        string status = frame.captured ? "captured" : "not set";
        Color statusColor = frame.captured ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.4f, 0.3f);
        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = statusColor } };

        EditorGUILayout.LabelField($"  {label}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"[{status}]", style, GUILayout.Width(70));

        if (GUILayout.Button($"Capture → {label}", GUILayout.Height(22)))
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                Undo.RecordObject(framing, $"Capture {label} Camera");
                frame.position = sv.camera.transform.position;
                frame.rotation = sv.camera.transform.eulerAngles;
                frame.fov = sv.camera.fieldOfView;
                frame.captured = true;
                EditorUtility.SetDirty(framing);
                Debug.Log($"[DateCameraFraming] Captured {label}: pos={frame.position}, rot={frame.rotation}, fov={frame.fov:F1}");
            }
            else
            {
                Debug.LogWarning("[DateCameraFraming] No active Scene View.");
            }
        }

        if (GUILayout.Button("Preview", GUILayout.Width(60), GUILayout.Height(22)))
        {
            if (frame.captured)
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    sv.pivot = frame.position + Quaternion.Euler(frame.rotation) * Vector3.forward * 5f;
                    sv.rotation = Quaternion.Euler(frame.rotation);
                    sv.size = 5f;
                    sv.Repaint();
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }
}
