using UnityEngine;
using UnityEditor;

/// <summary>
/// One-click editor tool that creates 4 DiscoBulbDefinition SOs
/// and spawns the disco ball + bulb GameObjects in the current scene.
/// </summary>
public static class DiscoBallSetup
{
    // --- Configurable positions (tweak these, then re-run) ---
    static readonly Vector3 DiscoBallPos = new Vector3(0f, 2.4f, 1f);
    static readonly Vector3 BulbRowStart = new Vector3(-0.2f, 0.85f, 2f);
    const float BulbSpacing = 0.12f;
    const string SOFolder = "Assets/ScriptableObjects/DiscoBall";

    [MenuItem("Window/Iris/Setup Disco Ball")]
    public static void Setup()
    {
        if (!EditorUtility.DisplayDialog("Setup Disco Ball",
            "This will create 4 DiscoBulbDefinition SOs in\n" +
            SOFolder + "\n\nand spawn DiscoBall + 4 bulb GameObjects in the current scene.\n\nContinue?",
            "Create", "Cancel"))
            return;

        EnsureFolder(SOFolder);

        // ---- Step 1: Create ScriptableObject assets ----
        var soColorCircles = CreateBulbSO("Bulb_ColorCircles", CookiePattern.ColorCircles,
            Color.white, true, MakeRainbowGradient(), 0.5f, 2f, 3f, 90f, 0.7f);

        var soMirrorClassic = CreateBulbSO("Bulb_MirrorClassic", CookiePattern.MirrorGrid,
            new Color(0.95f, 0.9f, 0.8f), false, null, 0.5f, 4f, 3f, 90f, 0.5f);

        var soPinpoints = CreateBulbSO("Bulb_Pinpoints", CookiePattern.Pinpoints,
            new Color(1f, 0.7f, 0.3f), false, null, 0.5f, 1.5f, 3f, 90f, 0.4f);

        var soPrism = CreateBulbSO("Bulb_Prism", CookiePattern.Prism,
            Color.white, true, MakePrismGradient(), 0.5f, 1f, 3f, 90f, 0.6f);

        // ---- Step 2: Spawn scene GameObjects ----
        var discoBall = CreateDiscoBallGO();

        var defs   = new[] { soColorCircles, soMirrorClassic, soPinpoints, soPrism };
        var colors = new[]
        {
            Color.white,
            new Color(0.95f, 0.9f, 0.8f),
            new Color(1f, 0.7f, 0.3f),
            new Color(0.8f, 0.6f, 1f)
        };
        var names = new[] { "Bulb_ColorCircles", "Bulb_MirrorClassic", "Bulb_Pinpoints", "Bulb_Prism" };

        for (int i = 0; i < 4; i++)
        {
            CreateBulbGO(names[i], defs[i], colors[i],
                BulbRowStart + Vector3.right * (BulbSpacing * i));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = discoBall;

        Debug.Log("[DiscoBallSetup] Created 4 DiscoBulbDefinition SOs and spawned scene objects. Adjust positions as needed.");
    }

    // ----------------------------------------------------------------
    //  SO creation
    // ----------------------------------------------------------------

    static DiscoBulbDefinition CreateBulbSO(string soName, CookiePattern pattern,
        Color lightColor, bool cycleColors, Gradient gradient,
        float cycleSpeed, float rotSpeed, float intensity, float spotAngle, float mood)
    {
        string path = $"{SOFolder}/{soName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<DiscoBulbDefinition>(path);

        if (existing != null)
        {
            ApplyBulbValues(existing, soName, pattern, lightColor, cycleColors, gradient,
                cycleSpeed, rotSpeed, intensity, spotAngle, mood);
            EditorUtility.SetDirty(existing);
            Debug.Log($"[DiscoBallSetup] Updated existing SO: {path}");
            return existing;
        }

        var so = ScriptableObject.CreateInstance<DiscoBulbDefinition>();
        ApplyBulbValues(so, soName, pattern, lightColor, cycleColors, gradient,
            cycleSpeed, rotSpeed, intensity, spotAngle, mood);
        AssetDatabase.CreateAsset(so, path);
        Debug.Log($"[DiscoBallSetup] Created SO: {path}");
        return so;
    }

    static void ApplyBulbValues(DiscoBulbDefinition so, string soName, CookiePattern pattern,
        Color lightColor, bool cycleColors, Gradient gradient,
        float cycleSpeed, float rotSpeed, float intensity, float spotAngle, float mood)
    {
        so.bulbName = soName;
        so.pattern = pattern;
        so.lightColor = lightColor;
        so.cycleColors = cycleColors;
        if (gradient != null) so.colorGradient = gradient;
        so.colorCycleSpeed = cycleSpeed;
        so.rotationSpeed = rotSpeed;
        so.lightIntensity = intensity;
        so.spotAngle = spotAngle;
        so.moodValue = mood;
    }

    // ----------------------------------------------------------------
    //  Scene object creation — Disco Ball
    // ----------------------------------------------------------------

    static GameObject CreateDiscoBallGO()
    {
        var root = new GameObject("DiscoBall");
        root.transform.position = DiscoBallPos;

        // --- Child: visual sphere ---
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "BallVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.15f;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var ballMat = new Material(shader);
        ballMat.color = new Color(0.8f, 0.8f, 0.85f);
        ballMat.SetFloat("_Metallic", 1f);
        ballMat.SetFloat("_Smoothness", 0.9f);
        visual.GetComponent<Renderer>().sharedMaterial = ballMat;

        // Remove auto-generated sphere collider from the primitive
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        // --- Child: bulb snap point ---
        var snapPoint = new GameObject("BulbSnapPoint");
        snapPoint.transform.SetParent(root.transform, false);
        snapPoint.transform.localPosition = new Vector3(0f, -0.1f, 0f);

        // --- Child: spotlight ---
        var lightGO = new GameObject("Spotlight");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        var spotlight = lightGO.AddComponent<Light>();
        spotlight.type = LightType.Spot;
        spotlight.spotAngle = 90f;
        spotlight.intensity = 3f;
        spotlight.enabled = false; // starts off — controller enables on TurnOn

        // --- Root components ---
        var controller = root.AddComponent<DiscoBallController>();
        root.AddComponent<InteractableHighlight>();
        var col = root.AddComponent<SphereCollider>();
        col.radius = 0.1f;
        root.AddComponent<PlacementSurface>();

        // Wire DiscoBallController private fields
        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("_spotlight").objectReferenceValue = spotlight;
        controllerSO.FindProperty("_ballVisual").objectReferenceValue = visual.transform;
        controllerSO.FindProperty("_bulbSnapPoint").objectReferenceValue = snapPoint.transform;
        controllerSO.ApplyModifiedProperties();

        // Wire ReactableTag
        var reactable = root.AddComponent<ReactableTag>();
        var reactableSO = new SerializedObject(reactable);
        reactableSO.FindProperty("displayName").stringValue = "Disco Ball";
        var tagsProp = reactableSO.FindProperty("tags");
        tagsProp.arraySize = 2;
        tagsProp.GetArrayElementAtIndex(0).stringValue = "light";
        tagsProp.GetArrayElementAtIndex(1).stringValue = "disco";
        reactableSO.FindProperty("isActive").boolValue = false;
        reactableSO.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(root, "Create Disco Ball");
        return root;
    }

    // ----------------------------------------------------------------
    //  Scene object creation — Bulbs
    // ----------------------------------------------------------------

    static void CreateBulbGO(string bulbName, DiscoBulbDefinition def, Color color, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = bulbName;
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.05f;

        // Colored material
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Smoothness", 0.6f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Replace auto-generated collider with a clean one
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.AddComponent<SphereCollider>();

        // Components
        go.AddComponent<PlaceableObject>();
        var bulb = go.AddComponent<DiscoBallBulb>();
        go.AddComponent<ReactableTag>(); // tags auto-set from definition in Awake
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // Wire DiscoBallBulb._definition
        var bulbSO = new SerializedObject(bulb);
        bulbSO.FindProperty("_definition").objectReferenceValue = def;
        bulbSO.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(go, $"Create Bulb {bulbName}");
    }

    // ----------------------------------------------------------------
    //  Gradient helpers
    // ----------------------------------------------------------------

    static Gradient MakeRainbowGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red,                        0.00f),
                new GradientColorKey(new Color(1f, 0.5f, 0f),         0.14f), // orange
                new GradientColorKey(Color.yellow,                     0.28f),
                new GradientColorKey(Color.green,                      0.42f),
                new GradientColorKey(Color.cyan,                       0.57f),
                new GradientColorKey(Color.blue,                       0.71f),
                new GradientColorKey(new Color(0.5f, 0f, 1f),         0.85f), // violet
                new GradientColorKey(Color.red,                        1.00f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return g;
    }

    static Gradient MakePrismGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red,                        0.00f),
                new GradientColorKey(Color.yellow,                     0.17f),
                new GradientColorKey(Color.green,                      0.33f),
                new GradientColorKey(Color.cyan,                       0.50f),
                new GradientColorKey(Color.blue,                       0.67f),
                new GradientColorKey(Color.magenta,                    0.83f),
                new GradientColorKey(Color.red,                        1.00f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return g;
    }

    // ----------------------------------------------------------------
    //  Folder helper
    // ----------------------------------------------------------------

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        // Walk the path creating each segment
        string[] parts = path.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
