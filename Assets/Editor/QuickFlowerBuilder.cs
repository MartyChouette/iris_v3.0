using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click flower builder wizard. Drag in a stem, leaf, and petal mesh,
/// specify counts and tuning, and get a fully wired flower hierarchy with
/// all runtime components and ScriptableObjects ready for play mode.
///
/// Menu: Window > Iris > Quick Flower Builder
/// </summary>
public class QuickFlowerBuilder : EditorWindow
{
    // ─────────────── Wizard fields ───────────────

    [Header("Flower Parts")]
    private GameObject stemSource;
    private GameObject leafSource;
    private GameObject petalSource;
    private GameObject crownSource;

    [Header("Counts")]
    private int leafCount = 4;
    private int petalCount = 5;

    [Header("Flower Type")]
    private FlowerTypeDefinition flowerType;
    private string flowerName = "NewFlower";
    private Difficulty difficulty = Difficulty.Normal;

    [Header("Arrangement")]
    private float leafRadius = 0.15f;
    private float petalRadius = 0.1f;
    private Vector2 leafHeightRange = new(0.2f, 0.6f);
    private Vector2 petalHeightRange = new(0.7f, 0.9f);
    private bool randomRotation = true;

    [Header("Physics Defaults")]
    private float breakForce = 800f;
    private float spring = 1200f;
    private float damper = 60f;
    private float maxDistance = 0.3f;

    [Header("Ideal Trimming")]
    private float idealStemLength = 0.5f;
    private float idealCutAngle = 45f;

    // GUI state
    private Vector2 scrollPos;
    private bool physicsFoldout;
    private bool idealFoldout;

    // ─────────────── Menu ───────────────

    [MenuItem("Window/Iris/Quick Flower Builder")]
    public static void ShowWindow()
    {
        GetWindow<QuickFlowerBuilder>("Quick Flower Builder");
    }

    // ─────────────── GUI ───────────────

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("Iris Quick Flower Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag in your stem, leaf, and petal meshes (prefabs or scene objects). " +
            "Set counts and tuning, then click Build Flower to generate a play-ready hierarchy.",
            MessageType.Info);

        EditorGUILayout.Space();

        // Flower name
        flowerName = EditorGUILayout.TextField("Flower Name", flowerName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);

        stemSource = (GameObject)EditorGUILayout.ObjectField("Stem", stemSource, typeof(GameObject), true);
        crownSource = (GameObject)EditorGUILayout.ObjectField("Crown (optional)", crownSource, typeof(GameObject), true);

        EditorGUILayout.BeginHorizontal();
        leafSource = (GameObject)EditorGUILayout.ObjectField("Leaf", leafSource, typeof(GameObject), true);
        leafCount = EditorGUILayout.IntSlider(leafCount, 0, 20, GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        petalSource = (GameObject)EditorGUILayout.ObjectField("Petal", petalSource, typeof(GameObject), true);
        petalCount = EditorGUILayout.IntSlider(petalCount, 0, 20, GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Flower Type", EditorStyles.boldLabel);
        flowerType = (FlowerTypeDefinition)EditorGUILayout.ObjectField(
            "Type (or null = create new)", flowerType, typeof(FlowerTypeDefinition), false);
        difficulty = (Difficulty)EditorGUILayout.EnumPopup("Difficulty", difficulty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Arrangement", EditorStyles.boldLabel);
        leafRadius = EditorGUILayout.FloatField("Leaf Radius", leafRadius);
        petalRadius = EditorGUILayout.FloatField("Petal Radius", petalRadius);
        leafHeightRange = DrawVector2Range("Leaf Heights", leafHeightRange);
        petalHeightRange = DrawVector2Range("Petal Heights", petalHeightRange);
        randomRotation = EditorGUILayout.Toggle("Random Rotation", randomRotation);

        EditorGUILayout.Space();
        physicsFoldout = EditorGUILayout.Foldout(physicsFoldout, "Physics Defaults", true);
        if (physicsFoldout)
        {
            EditorGUI.indentLevel++;
            breakForce = EditorGUILayout.FloatField("Break Force", breakForce);
            spring = EditorGUILayout.FloatField("Spring", spring);
            damper = EditorGUILayout.FloatField("Damper", damper);
            maxDistance = EditorGUILayout.FloatField("Max Distance", maxDistance);
            EditorGUI.indentLevel--;
        }

        idealFoldout = EditorGUILayout.Foldout(idealFoldout, "Ideal Trimming", true);
        if (idealFoldout)
        {
            EditorGUI.indentLevel++;
            idealStemLength = EditorGUILayout.FloatField("Stem Length", idealStemLength);
            idealCutAngle = EditorGUILayout.FloatField("Cut Angle (deg)", idealCutAngle);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        bool canBuild = stemSource != null && !string.IsNullOrWhiteSpace(flowerName);
        if (!canBuild)
            EditorGUILayout.HelpBox("A Stem source and Flower Name are required.", MessageType.Warning);

        EditorGUI.BeginDisabledGroup(!canBuild);
        if (GUILayout.Button("Build Flower", GUILayout.Height(30)))
            BuildFlower();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        if (GUILayout.Button("Validate Existing"))
            ValidateExisting();

        EditorGUILayout.EndScrollView();
    }

    private Vector2 DrawVector2Range(string label, Vector2 value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        value.x = EditorGUILayout.FloatField(value.x);
        EditorGUILayout.LabelField("to", GUILayout.Width(18));
        value.y = EditorGUILayout.FloatField(value.y);
        EditorGUILayout.EndHorizontal();
        return value;
    }

    // ─────────────── Build ───────────────

    private void BuildFlower()
    {
        Undo.SetCurrentGroupName("Quick Flower Builder");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Root
        var root = new GameObject($"{flowerName}_Flower");
        Undo.RegisterCreatedObjectUndo(root, "Create flower root");

        // 2. Stem
        var stemGO = InstantiateSource(stemSource, root.transform, "Stem");
        var stemRuntime = EnsureComponent<FlowerStemRuntime>(stemGO);
        var stemRb = EnsureComponent<Rigidbody>(stemGO);
        stemRb.isKinematic = false;
        stemRb.interpolation = RigidbodyInterpolation.Interpolate;
        EnsureComponent<DynamicMeshCutter.MeshTarget>(stemGO);

        float stemHeight = GetApproxBoundsHeight(stemGO);

        // Stem child transforms
        var anchor = FindOrCreateChild(stemGO.transform, "StemAnchor");
        anchor.localPosition = Vector3.up * stemHeight * 0.5f;
        stemRuntime.StemAnchor = anchor;

        var tip = FindOrCreateChild(stemGO.transform, "StemTip");
        tip.localPosition = Vector3.up * stemHeight * -0.5f;
        stemRuntime.StemTip = tip;

        var cutNormal = FindOrCreateChild(stemGO.transform, "CutNormalRef");
        cutNormal.localRotation = Quaternion.identity;
        stemRuntime.cutNormalRef = cutNormal;

        // 3. Crown
        GameObject crownGO = null;
        if (crownSource != null)
        {
            crownGO = InstantiateSource(crownSource, root.transform, "Crown");
        }
        else
        {
            // Try to find a crown child in the stem
            crownGO = FindChildCaseInsensitive(stemGO.transform, "crown");
            if (crownGO != null)
            {
                // Re-parent to root
                crownGO.transform.SetParent(root.transform, true);
            }
        }

        if (crownGO == null)
        {
            // Create placeholder sphere
            crownGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crownGO.name = "Crown";
            crownGO.transform.SetParent(root.transform, false);
            crownGO.transform.localPosition = Vector3.up * stemHeight * 0.5f;
            crownGO.transform.localScale = Vector3.one * 0.08f;
            Undo.RegisterCreatedObjectUndo(crownGO, "Create crown placeholder");
        }

        SetupPart(crownGO, "Crown", FlowerPartKind.Crown, stemRb, isCrown: true);

        // 4. Leaves
        var partsList = new List<FlowerPartRuntime>();
        partsList.Add(crownGO.GetComponent<FlowerPartRuntime>());

        for (int i = 0; i < leafCount; i++)
        {
            if (leafSource == null) break;
            string leafName = $"Leaf_{i}";
            var leafGO = InstantiateSource(leafSource, root.transform, leafName);

            // Position radially
            PositionRadially(leafGO.transform, stemGO.transform, i, leafCount,
                leafRadius, leafHeightRange, stemHeight);

            string partId = $"Leaf_{leafSource.name}_{i}";
            SetupPart(leafGO, partId, FlowerPartKind.Leaf, stemRb, isCrown: false);
            partsList.Add(leafGO.GetComponent<FlowerPartRuntime>());
        }

        // 5. Petals
        for (int i = 0; i < petalCount; i++)
        {
            if (petalSource == null) break;
            string petalName = $"Petal_{i}";
            var petalGO = InstantiateSource(petalSource, root.transform, petalName);

            PositionRadially(petalGO.transform, stemGO.transform, i, petalCount,
                petalRadius, petalHeightRange, stemHeight);

            string partId = $"Petal_{petalSource.name}_{i}";
            SetupPart(petalGO, partId, FlowerPartKind.Petal, stemRb, isCrown: false);
            partsList.Add(petalGO.GetComponent<FlowerPartRuntime>());
        }

        // 6. Session + Brain on root
        var brain = EnsureComponent<FlowerGameBrain>(root);
        var session = EnsureComponent<FlowerSessionController>(root);

        // Wire brain -> stem
        var brainSO = new SerializedObject(brain);
        SetObjectRef(brainSO, "stem", stemRuntime);

        // Wire brain -> parts
        var partsArrayProp = brainSO.FindProperty("parts");
        partsArrayProp.ClearArray();
        for (int i = 0; i < partsList.Count; i++)
        {
            partsArrayProp.InsertArrayElementAtIndex(i);
            partsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = partsList[i];
        }
        brainSO.ApplyModifiedProperties();

        // Wire session -> brain
        var sessionSO = new SerializedObject(session);
        SetObjectRef(sessionSO, "brain", brain);

        // 7. ScriptableObjects
        EnsureFolder("Assets/ScriptableObjects", "Flowers");

        // IdealFlowerDefinition
        string idealPath = $"Assets/ScriptableObjects/Flowers/Ideal_{flowerName}.asset";
        idealPath = AssetDatabase.GenerateUniqueAssetPath(idealPath);
        var ideal = CreateIdealDefinition(idealPath, partsList);

        // Wire brain -> ideal
        brainSO = new SerializedObject(brain);
        SetObjectRef(brainSO, "ideal", ideal);
        brainSO.ApplyModifiedProperties();

        // FlowerTypeDefinition
        if (flowerType == null)
        {
            string typePath = $"Assets/ScriptableObjects/Flowers/Type_{flowerName}.asset";
            typePath = AssetDatabase.GenerateUniqueAssetPath(typePath);
            flowerType = CreateFlowerTypeDefinition(typePath, ideal);
        }

        // Wire session -> FlowerType
        sessionSO = new SerializedObject(session);
        SetObjectRef(sessionSO, "FlowerType", flowerType);
        sessionSO.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);

        // Select the new root
        Selection.activeGameObject = root;

        Debug.Log($"[QuickFlowerBuilder] Built '{root.name}': 1 stem, 1 crown, " +
                  $"{leafCount} leaves, {petalCount} petals.\n" +
                  $"  Ideal: {idealPath}\n" +
                  $"  Type: {AssetDatabase.GetAssetPath(flowerType)}");
    }

    // ─────────────── Part setup ───────────────

    private void SetupPart(GameObject go, string partId, FlowerPartKind kind,
        Rigidbody stemRb, bool isCrown)
    {
        var rb = EnsureComponent<Rigidbody>(go);
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var part = EnsureComponent<FlowerPartRuntime>(go);
        var partSO = new SerializedObject(part);

        var pidProp = partSO.FindProperty("PartId");
        if (pidProp != null) pidProp.stringValue = partId;

        var kindProp = partSO.FindProperty("kind");
        if (kindProp != null) kindProp.enumValueIndex = (int)kind;

        var gameOverProp = partSO.FindProperty("canCauseGameOver");
        if (gameOverProp != null) gameOverProp.boolValue = isCrown;

        var scoreProp = partSO.FindProperty("contributesToScore");
        if (scoreProp != null) scoreProp.boolValue = true;

        var weightProp = partSO.FindProperty("scoreWeight");
        if (weightProp != null) weightProp.floatValue = 1f;

        partSO.ApplyModifiedProperties();

        // XYTetherJoint + SapOnXYTether for non-crown parts
        if (!isCrown)
        {
            var tether = EnsureComponent<XYTetherJoint>(go);
            var tetherSO = new SerializedObject(tether);
            SetObjectRef(tetherSO, "connectedBody", stemRb);
            SetFloat(tetherSO, "breakForce", breakForce);
            SetFloat(tetherSO, "spring", spring);
            SetFloat(tetherSO, "damper", damper);
            SetFloat(tetherSO, "maxDistance", maxDistance);
            tetherSO.ApplyModifiedProperties();

            var sap = EnsureComponent<SapOnXYTether>(go);
            var sapSO = new SerializedObject(sap);
            var pkProp = sapSO.FindProperty("partKind");
            if (pkProp != null)
                pkProp.enumValueIndex = kind == FlowerPartKind.Petal ? 1 : 0;
            sapSO.ApplyModifiedProperties();
        }
    }

    // ─────────────── Radial placement ───────────────

    private void PositionRadially(Transform part, Transform stem, int index, int total,
        float radius, Vector2 heightRange, float stemHeight)
    {
        float angle = index * (360f / Mathf.Max(total, 1));
        float rad = angle * Mathf.Deg2Rad;

        // Height: distribute evenly across the range
        float t = total > 1 ? (float)index / (total - 1) : 0.5f;
        float height = Mathf.Lerp(heightRange.x, heightRange.y, t) * stemHeight;
        // Center height around stem origin (stem bounds go from -0.5*h to +0.5*h)
        float yPos = -stemHeight * 0.5f + height;

        float x = Mathf.Cos(rad) * radius;
        float z = Mathf.Sin(rad) * radius;

        part.localPosition = stem.localPosition + new Vector3(x, yPos, z);

        // Face outward from stem center
        Vector3 outward = new Vector3(x, 0f, z).normalized;
        if (outward.sqrMagnitude > 0.001f)
        {
            part.localRotation = Quaternion.LookRotation(outward, Vector3.up);
        }

        if (randomRotation)
        {
            float randomZ = Random.Range(-15f, 15f);
            float randomY = Random.Range(-10f, 10f);
            part.localRotation *= Quaternion.Euler(0f, randomY, randomZ);
        }
    }

    // ─────────────── ScriptableObject creation ───────────────

    private IdealFlowerDefinition CreateIdealDefinition(string path,
        List<FlowerPartRuntime> allParts)
    {
        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();

        var idealSO = new SerializedObject(ideal);
        SetFloat(idealSO, "idealStemLength", idealStemLength);
        SetFloat(idealSO, "idealCutAngleDeg", idealCutAngle);
        SetFloat(idealSO, "stemScoreWeight", 0.3f);
        SetFloat(idealSO, "cutAngleScoreWeight", 0.3f);
        SetFloat(idealSO, "stemPerfectDelta", 0.05f);
        SetFloat(idealSO, "stemHardFailDelta", 0.3f);
        SetFloat(idealSO, "cutAnglePerfectDelta", 5f);
        SetFloat(idealSO, "cutAngleHardFailDelta", 45f);

        // Part rules
        var rulesList = idealSO.FindProperty("partRules");
        if (rulesList != null)
        {
            rulesList.ClearArray();
            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                rulesList.InsertArrayElementAtIndex(i);
                var elem = rulesList.GetArrayElementAtIndex(i);

                SetString(elem, "partId", GetPartId(p));
                SetEnum(elem, "kind", (int)GetPartKind(p));
                SetEnum(elem, "idealCondition", (int)FlowerPartCondition.Normal);
                SetBool(elem, "contributesToScore", true);
                SetBool(elem, "canCauseGameOver", GetPartKind(p) == FlowerPartKind.Crown);
                SetFloat(elem, "scoreWeight", 1f);
            }
        }
        idealSO.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(ideal, path);
        AssetDatabase.SaveAssets();
        return ideal;
    }

    private FlowerTypeDefinition CreateFlowerTypeDefinition(string path,
        IdealFlowerDefinition ideal)
    {
        var typeDef = ScriptableObject.CreateInstance<FlowerTypeDefinition>();

        var so = new SerializedObject(typeDef);
        SetString(so, "flowerId", flowerName.ToLowerInvariant().Replace(" ", "_"));
        SetString(so, "displayName", flowerName);
        SetEnum(so, "difficulty", (int)difficulty);
        SetObjectRef(so, "ideal", ideal);
        SetFloat(so, "basePerfectScore", 100f);
        SetFloat(so, "scoreMultiplier", 1f);
        SetInt(so, "minDays", 1);
        SetInt(so, "maxDays", 10);
        so.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(typeDef, path);
        AssetDatabase.SaveAssets();
        return typeDef;
    }

    // ─────────────── Validation ───────────────

    private void ValidateExisting()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("[QuickFlowerBuilder] No GameObject selected. Select a flower root to validate.");
            return;
        }

        int issues = 0;

        var session = selected.GetComponentInChildren<FlowerSessionController>(true);
        if (session == null) { Debug.LogError("[Validate] Missing FlowerSessionController", selected); issues++; }

        var brain = selected.GetComponentInChildren<FlowerGameBrain>(true);
        if (brain == null) { Debug.LogError("[Validate] Missing FlowerGameBrain", selected); issues++; }
        else
        {
            var brainSO = new SerializedObject(brain);
            if (brainSO.FindProperty("ideal")?.objectReferenceValue == null)
            { Debug.LogWarning("[Validate] FlowerGameBrain.ideal is null", brain); issues++; }
            if (brainSO.FindProperty("stem")?.objectReferenceValue == null)
            { Debug.LogError("[Validate] FlowerGameBrain.stem is null", brain); issues++; }

            var partsProp = brainSO.FindProperty("parts");
            if (partsProp == null || partsProp.arraySize == 0)
            { Debug.LogWarning("[Validate] FlowerGameBrain.parts is empty", brain); issues++; }
        }

        var stemRuntime = selected.GetComponentInChildren<FlowerStemRuntime>(true);
        if (stemRuntime == null) { Debug.LogError("[Validate] Missing FlowerStemRuntime", selected); issues++; }
        else
        {
            if (stemRuntime.StemAnchor == null) { Debug.LogError("[Validate] FlowerStemRuntime.StemAnchor is null", stemRuntime); issues++; }
            if (stemRuntime.StemTip == null) { Debug.LogError("[Validate] FlowerStemRuntime.StemTip is null", stemRuntime); issues++; }
        }

        var parts = selected.GetComponentsInChildren<FlowerPartRuntime>(true);
        foreach (var p in parts)
        {
            var pSO = new SerializedObject(p);
            string pid = pSO.FindProperty("PartId")?.stringValue;
            if (string.IsNullOrEmpty(pid))
            { Debug.LogWarning($"[Validate] FlowerPartRuntime on '{p.name}' has empty PartId", p); issues++; }

            if (p.GetComponent<Rigidbody>() == null)
            { Debug.LogError($"[Validate] FlowerPartRuntime '{p.name}' has no Rigidbody", p); issues++; }
        }

        if (session != null)
        {
            var sessionSO = new SerializedObject(session);
            if (sessionSO.FindProperty("FlowerType")?.objectReferenceValue == null)
            { Debug.LogWarning("[Validate] FlowerSessionController.FlowerType is null", session); issues++; }
        }

        if (issues == 0)
            Debug.Log($"[QuickFlowerBuilder] Validation passed — {parts.Length} parts, 0 issues.");
        else
            Debug.LogWarning($"[QuickFlowerBuilder] Validation found {issues} issue(s). Check console.");
    }

    // ─────────────── Helpers ───────────────

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent<T>(out var existing))
        {
            existing = Undo.AddComponent<T>(go);
        }
        return existing;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    private static GameObject FindChildCaseInsensitive(Transform parent, string name)
    {
        string lower = name.ToLowerInvariant();
        foreach (Transform child in parent)
        {
            if (child.name.ToLowerInvariant().Contains(lower))
                return child.gameObject;
        }
        return null;
    }

    private static float GetApproxBoundsHeight(GameObject go)
    {
        var renderer = go.GetComponentInChildren<Renderer>();
        if (renderer != null) return renderer.bounds.size.y;
        return 1f;
    }

    private static GameObject InstantiateSource(GameObject source, Transform parent, string name)
    {
        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(source))
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        }
        else
        {
            instance = Object.Instantiate(source, parent);
        }
        instance.name = name;
        Undo.RegisterCreatedObjectUndo(instance, $"Create {name}");
        return instance;
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder($"{parentFolder}/{newFolder}"))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }

    // ─── SerializedObject / SerializedProperty helpers ───

    private static void SetObjectRef(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }

    private static void SetFloat(SerializedObject so, string propName, float value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string propName, int value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.intValue = value;
    }

    private static void SetString(SerializedObject so, string propName, string value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.stringValue = value;
    }

    private static void SetEnum(SerializedObject so, string propName, int value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.enumValueIndex = value;
    }

    private static void SetString(SerializedProperty parent, string propName, string value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.stringValue = value;
    }

    private static void SetEnum(SerializedProperty parent, string propName, int value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.enumValueIndex = value;
    }

    private static void SetBool(SerializedProperty parent, string propName, bool value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.boolValue = value;
    }

    private static void SetFloat(SerializedProperty parent, string propName, float value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.floatValue = value;
    }

    private static string GetPartId(FlowerPartRuntime p)
    {
        var so = new SerializedObject(p);
        return so.FindProperty("PartId")?.stringValue ?? p.name;
    }

    private static FlowerPartKind GetPartKind(FlowerPartRuntime p)
    {
        var so = new SerializedObject(p);
        var prop = so.FindProperty("kind");
        return prop != null ? (FlowerPartKind)prop.enumValueIndex : FlowerPartKind.Leaf;
    }
}
