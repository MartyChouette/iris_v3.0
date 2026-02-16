using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the stem_sound scene: one flower with a tall stem, ScissorStation,
/// CuttingPlaneController, and StemSoundGuide for audio-based proximity feedback.
/// Menu: Window > Iris > Build Stem Sound Scene
/// </summary>
public static class StemSoundSceneBuilder
{
    [MenuItem("Window/Iris/Archived Builders/Build Stem Sound Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.95f, 0.85f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera ────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        cam.fieldOfView = 60f;
        camGO.transform.position = new Vector3(0f, 0.8f, -1.5f);
        camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        camGO.AddComponent<AudioListener>();

        // ── 3. Room geometry ──────────────────────────────────────────
        var room = new GameObject("Room");
        CreateBox("Floor", room.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(4f, 0.1f, 4f),
            new Color(0.25f, 0.25f, 0.25f));
        CreateBox("BackWall", room.transform,
            new Vector3(0f, 1f, 2f), new Vector3(4f, 2f, 0.1f),
            new Color(0.3f, 0.3f, 0.32f));

        // ── 4. Flower (tall stem for clear Y travel) ──────────────────
        float stemHeight = 1.5f;
        var flowerResult = BuildFlower("SoundFlower", stemHeight, 3, 2,
            new Vector3(0f, stemHeight * 0.5f, 0f));

        // ── 5. ScissorStation + CuttingPlaneController ────────────────
        var scissorGO = new GameObject("ScissorStation");
        var scissorStation = scissorGO.AddComponent<ScissorStation>();
        var planeControllerGO = new GameObject("CuttingPlaneController");
        planeControllerGO.transform.position = new Vector3(0f, stemHeight * 0.5f, 0f);
        var planeController = planeControllerGO.AddComponent<CuttingPlaneController>();

        // Wire scissor -> planeController
        var ssSO = new SerializedObject(scissorStation);
        ssSO.FindProperty("planeController").objectReferenceValue = planeController;
        ssSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 6. StemSoundGuide on Managers ─────────────────────────────
        var managersGO = new GameObject("Managers");
        var soundGuide = managersGO.AddComponent<StemSoundGuide>();

        // AudioSource child
        var audioGO = new GameObject("ToneSource");
        audioGO.transform.SetParent(managersGO.transform);
        var audioSource = audioGO.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;

        // Wire StemSoundGuide
        var sgSO = new SerializedObject(soundGuide);
        sgSO.FindProperty("stem").objectReferenceValue = flowerResult.stem;
        sgSO.FindProperty("ideal").objectReferenceValue = flowerResult.ideal;
        sgSO.FindProperty("planeController").objectReferenceValue = planeController;
        sgSO.FindProperty("toneSource").objectReferenceValue = audioSource;
        sgSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 7. Minimal UI ─────────────────────────────────────────────
        var uiCanvas = CreateScreenCanvas("UI_Canvas");
        CreateLabel("HintLabel", uiCanvas.transform,
            new Vector2(0f, -200f), new Vector2(400f, 50f),
            "Listen for the tone", 20f, TextAlignmentOptions.Center);

        // FlowerGradingUI
        var gradingGO = new GameObject("FlowerGradingUI");
        gradingGO.transform.SetParent(uiCanvas.transform);
        var gradingUI = gradingGO.AddComponent<FlowerGradingUI>();
        var gradingSO = new SerializedObject(gradingUI);
        gradingSO.FindProperty("session").objectReferenceValue = flowerResult.session;
        gradingSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 8. Save ───────────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/stem_sound.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[StemSoundSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Flower builder (inline, creates cylinder stems + cube parts)
    // ════════════════════════════════════════════════════════════════════

    private struct FlowerBuildResult
    {
        public GameObject root;
        public FlowerStemRuntime stem;
        public FlowerGameBrain brain;
        public FlowerSessionController session;
        public IdealFlowerDefinition ideal;
    }

    private static FlowerBuildResult BuildFlower(string name, float stemHeight,
        int leafCount, int petalCount, Vector3 position)
    {
        var root = new GameObject($"{name}_Flower");
        root.transform.position = position;

        // Stem
        var stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemGO.name = "Stem";
        stemGO.transform.SetParent(root.transform, false);
        stemGO.transform.localPosition = Vector3.zero;
        stemGO.transform.localScale = new Vector3(0.05f, stemHeight * 0.5f, 0.05f);
        SetColor(stemGO, new Color(0.2f, 0.55f, 0.15f));

        var stemRuntime = stemGO.AddComponent<FlowerStemRuntime>();
        var stemRb = stemGO.AddComponent<Rigidbody>();
        stemRb.isKinematic = true;

        // Stem child transforms
        var anchor = new GameObject("StemAnchor");
        anchor.transform.SetParent(stemGO.transform, false);
        anchor.transform.localPosition = Vector3.up * 0.5f; // top of unit cylinder
        stemRuntime.StemAnchor = anchor.transform;

        var tip = new GameObject("StemTip");
        tip.transform.SetParent(stemGO.transform, false);
        tip.transform.localPosition = Vector3.down * 0.5f; // bottom of unit cylinder
        stemRuntime.StemTip = tip.transform;

        var cutNormal = new GameObject("CutNormalRef");
        cutNormal.transform.SetParent(stemGO.transform, false);
        stemRuntime.cutNormalRef = cutNormal.transform;

        // Crown
        var crownGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crownGO.name = "Crown";
        crownGO.transform.SetParent(root.transform, false);
        crownGO.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
        crownGO.transform.localScale = Vector3.one * 0.08f;
        SetColor(crownGO, new Color(0.9f, 0.8f, 0.2f));

        var crownRb = crownGO.AddComponent<Rigidbody>();
        crownRb.isKinematic = false;
        crownRb.interpolation = RigidbodyInterpolation.Interpolate;

        var crownPart = crownGO.AddComponent<FlowerPartRuntime>();
        var crownSO = new SerializedObject(crownPart);
        crownSO.FindProperty("PartId").stringValue = "Crown";
        crownSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Crown;
        crownSO.FindProperty("canCauseGameOver").boolValue = true;
        crownSO.ApplyModifiedPropertiesWithoutUndo();

        // Parts list
        var partsList = new List<FlowerPartRuntime> { crownPart };

        // Leaves
        for (int i = 0; i < leafCount; i++)
        {
            var leafGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafGO.name = $"Leaf_{i}";
            leafGO.transform.SetParent(root.transform, false);

            float angle = i * (360f / leafCount) * Mathf.Deg2Rad;
            float height = Mathf.Lerp(0.2f, 0.6f, leafCount > 1 ? (float)i / (leafCount - 1) : 0.5f) * stemHeight;
            leafGO.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 0.12f,
                -stemHeight * 0.5f + height,
                Mathf.Sin(angle) * 0.12f);
            leafGO.transform.localScale = new Vector3(0.08f, 0.02f, 0.12f);
            SetColor(leafGO, new Color(0.25f, 0.6f, 0.2f));

            var leafRb = leafGO.AddComponent<Rigidbody>();
            leafRb.isKinematic = false;
            leafRb.interpolation = RigidbodyInterpolation.Interpolate;

            var leafPart = leafGO.AddComponent<FlowerPartRuntime>();
            var leafPartSO = new SerializedObject(leafPart);
            leafPartSO.FindProperty("PartId").stringValue = $"Leaf_{i}";
            leafPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Leaf;
            leafPartSO.FindProperty("contributesToScore").boolValue = true;
            leafPartSO.ApplyModifiedPropertiesWithoutUndo();

            var tether = leafGO.AddComponent<XYTetherJoint>();
            var tetherSO = new SerializedObject(tether);
            tetherSO.FindProperty("connectedBody").objectReferenceValue = stemRb;
            tetherSO.FindProperty("breakForce").floatValue = 800f;
            tetherSO.FindProperty("spring").floatValue = 1200f;
            tetherSO.FindProperty("damper").floatValue = 60f;
            tetherSO.ApplyModifiedPropertiesWithoutUndo();

            partsList.Add(leafPart);
        }

        // Petals
        for (int i = 0; i < petalCount; i++)
        {
            var petalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            petalGO.name = $"Petal_{i}";
            petalGO.transform.SetParent(root.transform, false);

            float angle = i * (360f / petalCount) * Mathf.Deg2Rad;
            float height = Mathf.Lerp(0.7f, 0.9f, petalCount > 1 ? (float)i / (petalCount - 1) : 0.5f) * stemHeight;
            petalGO.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 0.08f,
                -stemHeight * 0.5f + height,
                Mathf.Sin(angle) * 0.08f);
            petalGO.transform.localScale = new Vector3(0.06f, 0.015f, 0.1f);
            SetColor(petalGO, new Color(0.9f, 0.4f, 0.5f));

            var petalRb = petalGO.AddComponent<Rigidbody>();
            petalRb.isKinematic = false;
            petalRb.interpolation = RigidbodyInterpolation.Interpolate;

            var petalPart = petalGO.AddComponent<FlowerPartRuntime>();
            var petalPartSO = new SerializedObject(petalPart);
            petalPartSO.FindProperty("PartId").stringValue = $"Petal_{i}";
            petalPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Petal;
            petalPartSO.FindProperty("contributesToScore").boolValue = true;
            petalPartSO.ApplyModifiedPropertiesWithoutUndo();

            var tether = petalGO.AddComponent<XYTetherJoint>();
            var tetherSO = new SerializedObject(tether);
            tetherSO.FindProperty("connectedBody").objectReferenceValue = stemRb;
            tetherSO.FindProperty("breakForce").floatValue = 800f;
            tetherSO.FindProperty("spring").floatValue = 1200f;
            tetherSO.FindProperty("damper").floatValue = 60f;
            tetherSO.ApplyModifiedPropertiesWithoutUndo();

            partsList.Add(petalPart);
        }

        // Brain
        var brain = root.AddComponent<FlowerGameBrain>();
        var brainSO = new SerializedObject(brain);
        brainSO.FindProperty("stem").objectReferenceValue = stemRuntime;

        var partsArrayProp = brainSO.FindProperty("parts");
        partsArrayProp.ClearArray();
        for (int i = 0; i < partsList.Count; i++)
        {
            partsArrayProp.InsertArrayElementAtIndex(i);
            partsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = partsList[i];
        }

        // Create IdealFlowerDefinition SO
        EnsureFolder("Assets/ScriptableObjects", "Flowers");
        string idealPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/Flowers/Ideal_{name}.asset");
        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        var idealSO = new SerializedObject(ideal);
        idealSO.FindProperty("idealStemLength").floatValue = stemHeight * 0.5f;
        idealSO.FindProperty("idealCutAngleDeg").floatValue = 45f;
        idealSO.FindProperty("stemScoreWeight").floatValue = 0.3f;
        idealSO.FindProperty("cutAngleScoreWeight").floatValue = 0.3f;

        // Part rules
        var rulesProp = idealSO.FindProperty("partRules");
        rulesProp.ClearArray();
        for (int i = 0; i < partsList.Count; i++)
        {
            rulesProp.InsertArrayElementAtIndex(i);
            var elem = rulesProp.GetArrayElementAtIndex(i);
            var partSO2 = new SerializedObject(partsList[i]);
            elem.FindPropertyRelative("partId").stringValue = partSO2.FindProperty("PartId").stringValue;
            elem.FindPropertyRelative("kind").enumValueIndex = partSO2.FindProperty("kind").enumValueIndex;
            elem.FindPropertyRelative("contributesToScore").boolValue = true;
            elem.FindPropertyRelative("canCauseGameOver").boolValue =
                partSO2.FindProperty("kind").enumValueIndex == (int)FlowerPartKind.Crown;
            elem.FindPropertyRelative("scoreWeight").floatValue = 1f;
        }
        idealSO.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(ideal, idealPath);

        brainSO.FindProperty("ideal").objectReferenceValue = ideal;
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // Session
        var session = root.AddComponent<FlowerSessionController>();
        var sessionSO = new SerializedObject(session);
        sessionSO.FindProperty("brain").objectReferenceValue = brain;

        // FlowerTypeDefinition
        string typePath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/Flowers/Type_{name}.asset");
        var typeDef = ScriptableObject.CreateInstance<FlowerTypeDefinition>();
        var typeSO = new SerializedObject(typeDef);
        typeSO.FindProperty("flowerId").stringValue = name.ToLowerInvariant();
        typeSO.FindProperty("displayName").stringValue = name;
        typeSO.FindProperty("ideal").objectReferenceValue = ideal;
        typeSO.FindProperty("basePerfectScore").floatValue = 100f;
        typeSO.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(typeDef, typePath);

        sessionSO.FindProperty("FlowerType").objectReferenceValue = typeDef;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        return new FlowerBuildResult
        {
            root = root,
            stem = stemRuntime,
            brain = brain,
            session = session,
            ideal = ideal,
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared Helpers
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
        SetColor(go, color);
        return go;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
    }

    private static GameObject CreateScreenCanvas(string name)
    {
        var canvasGO = new GameObject(name);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return canvasGO;
    }

    private static GameObject CreateLabel(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, string text, float fontSize,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return go;
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder($"{parentFolder}/{newFolder}"))
        {
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                string[] parts = parentFolder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }
}
