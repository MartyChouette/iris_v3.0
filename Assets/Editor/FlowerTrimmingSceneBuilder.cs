using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using TMPro;
using UnityEngine.UI;
using DynamicMeshCutter;

/// <summary>
/// Editor utility that builds the FlowerTrimming scene used for additive loading
/// during the date-to-flower transition.
/// Menu: Window > Iris > Build Flower Trimming Scene
/// </summary>
public static class FlowerTrimmingSceneBuilder
{
    [MenuItem("Window/Iris/Build Flower Trimming Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
                "Build Flower Trimming Scene",
                "This will create a new FlowerTrimming scene.\n\n" +
                "Any unsaved changes to the current scene will be lost.\n\n" +
                "Continue?",
                "Build", "Cancel"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Camera (priority 40 — wins over all apartment cameras) ──
        var camGO = new GameObject("FlowerCamera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
        camGO.AddComponent<CinemachineBrain>();

        var vcamGO = new GameObject("FlowerCinemachineCamera");
        var vcam = vcamGO.AddComponent<CinemachineCamera>();
        vcam.Priority = 40;
        vcam.Lens = new LensSettings
        {
            FieldOfView = 50f,
            NearClipPlane = 0.1f,
            FarClipPlane = 100f
        };
        vcamGO.transform.position = new Vector3(0f, 1.2f, -0.8f);
        vcamGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

        // ── 2. Directional Light ──
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        light.color = new Color(1f, 0.96f, 0.9f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 3. Table surface ──
        var table = CreateBox("Table", null,
            new Vector3(0f, 0.4f, 0f), new Vector3(1.5f, 0.05f, 1.0f),
            new Color(0.50f, 0.35f, 0.22f));
        var tableLeg1 = CreateBox("TableLeg1", table.transform,
            new Vector3(-0.65f, -0.2f, -0.4f), new Vector3(0.05f, 0.4f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        var tableLeg2 = CreateBox("TableLeg2", table.transform,
            new Vector3(0.65f, -0.2f, -0.4f), new Vector3(0.05f, 0.4f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        var tableLeg3 = CreateBox("TableLeg3", table.transform,
            new Vector3(-0.65f, -0.2f, 0.4f), new Vector3(0.05f, 0.4f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        var tableLeg4 = CreateBox("TableLeg4", table.transform,
            new Vector3(0.65f, -0.2f, 0.4f), new Vector3(0.05f, 0.4f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));

        // ── 4. Flower Spawn Point ──
        var spawnPoint = new GameObject("FlowerSpawnPoint");
        spawnPoint.transform.position = new Vector3(0f, 0.45f, 0f);

        // ── 5. Scissor Station ──
        var stationGO = new GameObject("ScissorStation");
        stationGO.transform.position = new Vector3(0f, 0.45f, 0f);
        var station = stationGO.AddComponent<ScissorStation>();

        // Inactive scissors (visual placeholder on table)
        var scissorsInactive = CreateBox("Scissors_Inactive", stationGO.transform,
            new Vector3(0.5f, 0f, 0.3f), new Vector3(0.15f, 0.02f, 0.04f),
            new Color(0.7f, 0.7f, 0.72f));

        // Active scissors (held, starts hidden)
        var scissorsActive = CreateBox("Scissors_Active", stationGO.transform,
            new Vector3(0f, 0.15f, 0f), new Vector3(0.02f, 0.3f, 0.02f),
            new Color(0.75f, 0.75f, 0.78f));
        scissorsActive.SetActive(false);

        // ── 6. Cutting Plane Controller ──
        var cpcGO = new GameObject("CuttingPlaneController");
        cpcGO.transform.position = new Vector3(0f, 0.45f, 0f);
        var cpc = cpcGO.AddComponent<CuttingPlaneController>();

        // Plane Behaviour (visual plane indicator)
        var planeGO = new GameObject("CutPlane");
        planeGO.transform.SetParent(cpcGO.transform);
        planeGO.transform.localPosition = Vector3.zero;
        var planeBehaviour = planeGO.AddComponent<PlaneBehaviour>();

        // PlaneAngleTiltController
        var tiltGO = new GameObject("PlaneAngleTilt");
        tiltGO.transform.SetParent(cpcGO.transform);
        tiltGO.transform.localPosition = Vector3.zero;
        var tilt = tiltGO.AddComponent<PlaneAngleTiltController>();

        // Wire CuttingPlaneController fields via SerializedObject
        var cpcSO = new SerializedObject(cpc);
        var planeProp = cpcSO.FindProperty("planeBehaviour");
        if (planeProp != null) planeProp.objectReferenceValue = planeBehaviour;
        var tiltProp = cpcSO.FindProperty("planeAngleTilt");
        if (tiltProp != null) tiltProp.objectReferenceValue = tilt;
        cpcSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 7. EventSystem ──
        var eventSysGO = new GameObject("EventSystem");
        eventSysGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSysGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // ── 8. Grading UI Canvas ──
        BuildGradingUI();

        // ── 9. Save scene ──
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/FlowerTrimming.unity";
        EditorSceneManager.SaveScene(scene, path);

        // ── 10. Add to Build Settings ──
        AddSceneToBuildSettings(path);

        Debug.Log($"[FlowerTrimmingSceneBuilder] Scene saved to {path} and added to Build Settings.");
    }

    private static void BuildGradingUI()
    {
        var canvasGO = new GameObject("FlowerGradingCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Add FlowerGradingUI component if it exists
        var gradingType = System.Type.GetType("FlowerGradingUI");
        if (gradingType != null)
            canvasGO.AddComponent(gradingType);

        // Score text (center-top)
        var scoreGO = new GameObject("ScoreText");
        scoreGO.transform.SetParent(canvasGO.transform, false);
        var scoreRT = scoreGO.AddComponent<RectTransform>();
        scoreRT.anchorMin = new Vector2(0.5f, 1f);
        scoreRT.anchorMax = new Vector2(0.5f, 1f);
        scoreRT.pivot = new Vector2(0.5f, 1f);
        scoreRT.sizeDelta = new Vector2(400f, 60f);
        scoreRT.anchoredPosition = new Vector2(0f, -20f);
        var scoreTMP = scoreGO.AddComponent<TextMeshProUGUI>();
        scoreTMP.text = "Trim the flower";
        scoreTMP.fontSize = 28f;
        scoreTMP.alignment = TextAlignmentOptions.Center;
        scoreTMP.color = new Color(0.9f, 0.9f, 0.9f);

        // Instructions (bottom-center)
        var instrGO = new GameObject("InstructionsText");
        instrGO.transform.SetParent(canvasGO.transform, false);
        var instrRT = instrGO.AddComponent<RectTransform>();
        instrRT.anchorMin = new Vector2(0.5f, 0f);
        instrRT.anchorMax = new Vector2(0.5f, 0f);
        instrRT.pivot = new Vector2(0.5f, 0f);
        instrRT.sizeDelta = new Vector2(600f, 40f);
        instrRT.anchoredPosition = new Vector2(0f, 20f);
        var instrTMP = instrGO.AddComponent<TextMeshProUGUI>();
        instrTMP.text = "Press E when finished";
        instrTMP.fontSize = 20f;
        instrTMP.alignment = TextAlignmentOptions.Center;
        instrTMP.color = new Color(0.7f, 0.7f, 0.7f);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Check if already added
        foreach (var s in scenes)
        {
            if (s.path == scenePath)
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[FlowerTrimmingSceneBuilder] Added '{scenePath}' to Build Settings.");
    }

    private static GameObject CreateBox(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent);
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
}
