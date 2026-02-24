using UnityEditor;
using UnityEngine;
using System.Text;

[CustomEditor(typeof(MessBlueprint))]
public class MessBlueprintEditor : Editor
{
    // Category colors matching the editor window
    private static readonly Color CategoryDateAftermath = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color CategoryOffScreen     = new Color(0.3f, 0.5f, 0.9f);
    private static readonly Color CategoryGeneral       = new Color(0.3f, 0.8f, 0.4f);

    private static readonly Color GizmoSelected   = Color.yellow;
    private static readonly Color GizmoUnselected  = new Color(1f, 1f, 1f, 0.5f);

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
        var bp = (MessBlueprint)target;

        DrawConditionSummary(bp);
        DrawValidationWarnings(bp);

        EditorGUILayout.Space(4);
        DrawDefaultInspector();

        if (bp.messType == MessBlueprint.MessType.Object && bp.objectPrefab == null)
        {
            DrawProceduralPreview(bp);
        }

        DrawCaptureButtons(bp);
    }

    // ─────────────────────────────────────────────
    //  Condition Summary
    // ─────────────────────────────────────────────

    private void DrawConditionSummary(MessBlueprint bp)
    {
        var sb = new StringBuilder();

        // Day
        if (bp.minDay > 1)
            sb.Append($"Day {bp.minDay}+");
        else
            sb.Append("Day 1+");

        // Category
        sb.Append($", {bp.category}");

        // Date conditions
        if (bp.category == MessBlueprint.MessCategory.DateAftermath)
        {
            if (bp.requireDateSuccess) sb.Append(", after successful date");
            if (bp.requireDateFailure) sb.Append(", after failed date");
            if (bp.minAffection > 0f) sb.Append($", affection >= {bp.minAffection:F0}");
            if (bp.maxAffection < 100f) sb.Append($", affection <= {bp.maxAffection:F0}");
        }

        // Reaction tag
        if (!string.IsNullOrEmpty(bp.requireReactionTag))
            sb.Append($", with '{bp.requireReactionTag}' served");

        // Flower
        if (bp.requireBadFlowerTrim) sb.Append(", bad flower trim (<40)");
        if (bp.requireGoodFlowerTrim) sb.Append(", good flower trim (>=80)");

        // Areas
        if (bp.allowedAreas != null && bp.allowedAreas.Length > 0)
        {
            sb.Append(". Areas: ");
            for (int i = 0; i < bp.allowedAreas.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(bp.allowedAreas[i]);
            }
        }

        // Type
        string typeStr = bp.messType == MessBlueprint.MessType.Stain ? "Stain" : "Object";
        sb.Insert(0, $"[{typeStr}] ");

        EditorGUILayout.HelpBox(sb.ToString(), MessageType.Info);
    }

    // ─────────────────────────────────────────────
    //  Validation Warnings
    // ─────────────────────────────────────────────

    private void DrawValidationWarnings(MessBlueprint bp)
    {
        bool hasWarning = false;

        // No spawn position
        if (bp.spawnPosition == Vector3.zero)
        {
            EditorGUILayout.HelpBox("No spawn position set! Use 'Capture Position from Scene View' below.", MessageType.Warning);
            hasWarning = true;
        }

        // DateAftermath with no conditions
        if (bp.category == MessBlueprint.MessCategory.DateAftermath
            && !bp.requireDateSuccess && !bp.requireDateFailure
            && bp.minAffection <= 0f && bp.maxAffection >= 100f
            && string.IsNullOrEmpty(bp.requireReactionTag))
        {
            EditorGUILayout.HelpBox("DateAftermath category but no date conditions set — will spawn after any date.", MessageType.Warning);
            hasWarning = true;
        }

        // Impossible: both good and bad flower
        if (bp.requireBadFlowerTrim && bp.requireGoodFlowerTrim)
        {
            EditorGUILayout.HelpBox("Both requireBadFlowerTrim AND requireGoodFlowerTrim are set — this is impossible to satisfy!", MessageType.Error);
            hasWarning = true;
        }

        // Impossible: both success and failure
        if (bp.requireDateSuccess && bp.requireDateFailure)
        {
            EditorGUILayout.HelpBox("Both requireDateSuccess AND requireDateFailure are set — this is impossible to satisfy!", MessageType.Error);
            hasWarning = true;
        }

        // Stain with no SpillDefinition
        if (bp.messType == MessBlueprint.MessType.Stain && bp.spillDefinition == null)
        {
            EditorGUILayout.HelpBox("Stain type but no SpillDefinition assigned.", MessageType.Warning);
            hasWarning = true;
        }

        // Object with no prefab and default scale
        if (bp.messType == MessBlueprint.MessType.Object && bp.objectPrefab == null
            && bp.objectScale == Vector3.one * 0.1f && bp.objectColor == Color.gray)
        {
            EditorGUILayout.HelpBox("Procedural object using default scale + color — consider customizing.", MessageType.Info);
        }

        // No allowed areas
        if (bp.allowedAreas == null || bp.allowedAreas.Length == 0)
        {
            EditorGUILayout.HelpBox("No allowed areas set — TidyScorer won't know which area this mess belongs to.", MessageType.Warning);
            hasWarning = true;
        }
    }

    // ─────────────────────────────────────────────
    //  Procedural Preview
    // ─────────────────────────────────────────────

    private void DrawProceduralPreview(MessBlueprint bp)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Procedural Object Preview", EditorStyles.boldLabel);

        var rect = EditorGUILayout.GetControlRect(false, 24);
        var colorRect = new Rect(rect.x, rect.y, 40, rect.height);
        EditorGUI.DrawRect(colorRect, bp.objectColor);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, colorRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.yMax - 1, colorRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, 1, colorRect.height), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.xMax - 1, colorRect.y, 1, colorRect.height), Color.black);

        var labelRect = new Rect(rect.x + 48, rect.y + 3, rect.width - 48, rect.height);
        EditorGUI.LabelField(labelRect, $"Scale: {bp.objectScale.x:F2} x {bp.objectScale.y:F2} x {bp.objectScale.z:F2}");
    }

    // ─────────────────────────────────────────────
    //  Capture Buttons
    // ─────────────────────────────────────────────

    private void DrawCaptureButtons(MessBlueprint bp)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Scene View Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Capture Position from Scene View"))
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Debug.LogWarning("[MessBlueprintEditor] No active Scene View found.");
            }
            else
            {
                // Raycast from scene camera center to find a surface
                Ray ray = sv.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

                Undo.RecordObject(bp, "Capture Mess Spawn Position");

                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    bp.spawnPosition = hit.point;
                    Debug.Log($"[MessBlueprintEditor] Captured position from surface hit: {hit.point}");
                }
                else
                {
                    // Fallback: intersect with y=0 plane
                    if (ray.direction.y != 0f)
                    {
                        float t = -ray.origin.y / ray.direction.y;
                        if (t > 0f)
                        {
                            bp.spawnPosition = ray.origin + ray.direction * t;
                            Debug.Log($"[MessBlueprintEditor] Captured position from y=0 plane: {bp.spawnPosition}");
                        }
                        else
                        {
                            bp.spawnPosition = sv.camera.transform.position + sv.camera.transform.forward * 3f;
                            Debug.Log($"[MessBlueprintEditor] Captured position ahead of camera: {bp.spawnPosition}");
                        }
                    }
                    else
                    {
                        bp.spawnPosition = sv.camera.transform.position + sv.camera.transform.forward * 3f;
                        Debug.Log($"[MessBlueprintEditor] Captured position ahead of camera: {bp.spawnPosition}");
                    }
                }

                EditorUtility.SetDirty(bp);
            }
        }

        if (GUILayout.Button("Focus Scene View on Spawn"))
        {
            FocusSceneView(bp.spawnPosition);
        }

        EditorGUILayout.EndHorizontal();

        // Position readout
        EditorGUILayout.LabelField($"Spawn: ({bp.spawnPosition.x:F2}, {bp.spawnPosition.y:F2}, {bp.spawnPosition.z:F2})",
            EditorStyles.miniLabel);
    }

    // ─────────────────────────────────────────────
    //  Scene View Gizmo
    // ─────────────────────────────────────────────

    private void OnSceneGUI(SceneView sceneView)
    {
        var bp = target as MessBlueprint;
        if (bp == null) return;

        DrawBlueprintGizmo(bp, true);
    }

    /// <summary>
    /// Draws a single blueprint gizmo in scene view. Public static so MessEditorWindow can reuse it.
    /// </summary>
    public static void DrawBlueprintGizmo(MessBlueprint bp, bool isSelected)
    {
        if (bp == null) return;

        Vector3 pos = bp.spawnPosition;
        Color catColor = GetCategoryColor(bp.category);
        Color drawColor = isSelected ? GizmoSelected : catColor;

        // Draggable position handle (only for selected)
        if (isSelected)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(pos, Quaternion.Euler(bp.spawnRotation));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bp, "Move Mess Spawn Position");
                bp.spawnPosition = newPos;
                EditorUtility.SetDirty(bp);
                pos = newPos;
            }
        }

        Handles.color = drawColor;

        // Shape based on type
        if (bp.messType == MessBlueprint.MessType.Stain)
        {
            // Flat disc for stains
            Handles.DrawWireDisc(pos, Vector3.up, 0.2f);
            if (isSelected)
                Handles.DrawWireDisc(pos, Vector3.up, 0.25f);
        }
        else
        {
            // Wireframe cube for objects
            Vector3 size = bp.objectPrefab != null ? Vector3.one * 0.15f : bp.objectScale;
            DrawWireCube(pos, size);
            if (isSelected)
                DrawWireCube(pos, size * 1.2f);
        }

        // Dotted vertical line from floor
        if (pos.y > 0.05f)
        {
            Handles.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.3f);
            Handles.DrawDottedLine(pos, new Vector3(pos.x, 0f, pos.z), 4f);
        }

        // Label
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = drawColor },
            alignment = TextAnchor.MiddleCenter,
            fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
        };

        string typeTag = bp.messType == MessBlueprint.MessType.Stain ? "[S]" : "[O]";
        string label = $"{typeTag} {bp.messName}";
        Handles.Label(pos + Vector3.up * 0.3f, label, style);
    }

    public static Color GetCategoryColor(MessBlueprint.MessCategory cat)
    {
        switch (cat)
        {
            case MessBlueprint.MessCategory.DateAftermath: return CategoryDateAftermath;
            case MessBlueprint.MessCategory.OffScreen:     return CategoryOffScreen;
            case MessBlueprint.MessCategory.General:       return CategoryGeneral;
            default: return Color.white;
        }
    }

    public static void FocusSceneView(Vector3 position)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.LookAt(position, sv.rotation, 3f);
            sv.Repaint();
        }
    }

    private static void DrawWireCube(Vector3 center, Vector3 size)
    {
        Vector3 half = size * 0.5f;

        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-half.x, -half.y, -half.z);
        corners[1] = center + new Vector3( half.x, -half.y, -half.z);
        corners[2] = center + new Vector3( half.x, -half.y,  half.z);
        corners[3] = center + new Vector3(-half.x, -half.y,  half.z);
        corners[4] = center + new Vector3(-half.x,  half.y, -half.z);
        corners[5] = center + new Vector3( half.x,  half.y, -half.z);
        corners[6] = center + new Vector3( half.x,  half.y,  half.z);
        corners[7] = center + new Vector3(-half.x,  half.y,  half.z);

        // Bottom
        Handles.DrawLine(corners[0], corners[1]);
        Handles.DrawLine(corners[1], corners[2]);
        Handles.DrawLine(corners[2], corners[3]);
        Handles.DrawLine(corners[3], corners[0]);
        // Top
        Handles.DrawLine(corners[4], corners[5]);
        Handles.DrawLine(corners[5], corners[6]);
        Handles.DrawLine(corners[6], corners[7]);
        Handles.DrawLine(corners[7], corners[4]);
        // Verticals
        Handles.DrawLine(corners[0], corners[4]);
        Handles.DrawLine(corners[1], corners[5]);
        Handles.DrawLine(corners[2], corners[6]);
        Handles.DrawLine(corners[3], corners[7]);
    }
}
