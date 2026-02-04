using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the cleaning minigame scene:
/// a kitchen counter with a spill surface, sponge wipe controller, and UI.
/// Menu: Window > Iris > Build Cleaning Scene
/// </summary>
public static class CleaningSceneBuilder
{
    private const string SpillLayerName = "Spill";

    [MenuItem("Window/Iris/Build Cleaning Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int spillLayer = EnsureLayer(SpillLayerName);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera (fixed, looking down at counter) ────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cam.fieldOfView = 60f;
        camGO.transform.position = new Vector3(0f, 1.4f, -0.3f);
        camGO.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

        // ── 3. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 4. Counter with spill surface ──────────────────────────────
        BuildCounter(spillLayer);

        // ── 5. Wipe system (controller + sponge) ───────────────────────
        BuildWipeSystem(spillLayer);

        // ── 6. Screen-space UI ─────────────────────────────────────────
        BuildUI();

        // ── 7. Save scene ──────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/cleaning_scene.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[CleaningSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor
        CreateBox("Floor", parent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(3f, 0.1f, 3f),
            new Color(0.25f, 0.25f, 0.25f));

        // Walls
        CreateBox("Wall_Back", parent.transform,
            new Vector3(0f, 1f, 1.5f), new Vector3(3f, 2f, 0.1f),
            new Color(0.75f, 0.73f, 0.70f));

        CreateBox("Wall_Left", parent.transform,
            new Vector3(-1.5f, 1f, 0f), new Vector3(0.1f, 2f, 3f),
            new Color(0.72f, 0.70f, 0.67f));

        CreateBox("Wall_Right", parent.transform,
            new Vector3(1.5f, 1f, 0f), new Vector3(0.1f, 2f, 3f),
            new Color(0.72f, 0.70f, 0.67f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Counter + Spill Surface
    // ════════════════════════════════════════════════════════════════════

    private static void BuildCounter(int spillLayer)
    {
        var parent = new GameObject("Counter");

        // Countertop
        CreateBox("Countertop", parent.transform,
            new Vector3(0f, 0.75f, 0.3f), new Vector3(1.8f, 0.06f, 0.7f),
            new Color(0.82f, 0.80f, 0.76f));

        // Spill surface — a Quad lying face-up on the counter
        var spill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        spill.name = "SpillSurface";
        spill.transform.SetParent(parent.transform);
        spill.transform.position = new Vector3(0f, 0.781f, 0.3f);
        spill.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        spill.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        spill.layer = spillLayer;
        spill.isStatic = false;

        // KEEP the default MeshCollider — RaycastHit.textureCoord requires it
        // (do NOT replace with BoxCollider)

        // Add SpillSurface component
        spill.AddComponent<SpillSurface>();
    }

    // ════════════════════════════════════════════════════════════════════
    // Wipe System
    // ════════════════════════════════════════════════════════════════════

    private static void BuildWipeSystem(int spillLayer)
    {
        var parent = new GameObject("WipeSystem");

        // WipeController
        var controllerGO = new GameObject("WipeController");
        controllerGO.transform.SetParent(parent.transform);
        var controller = controllerGO.AddComponent<WipeController>();

        // Sponge visual — small yellow cube
        var sponge = CreateBox("SpongeVisual", parent.transform,
            Vector3.zero, new Vector3(0.06f, 0.02f, 0.04f),
            new Color(0.95f, 0.85f, 0.20f));
        sponge.isStatic = false;

        // Wire WipeController serialized fields
        var spillSurface = Object.FindAnyObjectByType<SpillSurface>();

        var so = new SerializedObject(controller);
        so.FindProperty("spillSurface").objectReferenceValue = spillSurface;
        so.FindProperty("spongeVisual").objectReferenceValue = sponge.transform;
        so.FindProperty("spillLayer").intValue = 1 << spillLayer;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // UI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildUI()
    {
        // Screen-space overlay canvas
        var canvasGO = new GameObject("UI_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Progress panel — top-center
        var panel = new GameObject("ProgressPanel");
        panel.transform.SetParent(canvasGO.transform);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 1f);
        panelRT.anchorMax = new Vector2(0.5f, 1f);
        panelRT.pivot = new Vector2(0.5f, 1f);
        panelRT.sizeDelta = new Vector2(250f, 50f);
        panelRT.anchoredPosition = new Vector2(0f, -10f);
        panelRT.localScale = Vector3.one;

        // Semi-transparent background
        var bg = panel.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);

        // Progress text
        var textGO = new GameObject("ProgressText");
        textGO.transform.SetParent(panel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Clean: 0%";
        tmp.fontSize = 32f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Wire the progress text to the WipeController
        var wipeController = Object.FindAnyObjectByType<WipeController>();
        if (wipeController != null)
        {
            var so = new SerializedObject(wipeController);
            so.FindProperty("progressText").objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════════

    private static GameObject CreateBox(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.isStatic = true;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
        return go;
    }

    /// <summary>
    /// Ensure a layer exists by name. Returns the layer index.
    /// </summary>
    private static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");

        // Check if the layer already exists
        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layerName)
                return i;
        }

        // Find first empty user layer (8+) and assign
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[CleaningSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[CleaningSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
