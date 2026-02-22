using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Editor utility that programmatically builds a lighting/shader test scene.
/// Includes: room geometry with windows, imported FBX furniture (sofa, book),
/// 8 switchable cameras (4 perspective + 4 ortho), NatureBox sky, weather system,
/// PSX toggle, ObjectGrabber with grid snap, DrawerController cubby, and debug HUD.
/// Menu: Window > Iris > Build Lighting Test Scene
/// </summary>
public static class LightingTestSceneBuilder
{
    private const string PlaceableLayerName = "Placeable";
    private const string SurfacesLayerName = "Surfaces";
    private const string DrawersLayerName = "Drawers";

    // ── Room dimensions ──────────────────────────────────────────
    private const float RoomW = 20f;
    private const float RoomH = 5f;
    private const float RoomD = 20f;
    private const float WallThick = 0.15f;

    // ── Window config (3 windows in +Z wall) ─────────────────────
    private const float WindowY = 1.5f;
    private const float WindowW = 2.4f;
    private const float WindowH = 2.2f;
    private const float WindowSpacing = 5.0f;

    // ── Furniture positions ──────────────────────────────────────
    private static readonly Vector3 SofaPos = new Vector3(-4f, 0f, 5f);
    private static readonly Vector3 BedPos = new Vector3(5f, 0.25f, 5f);
    private static readonly Vector3 BedSize = new Vector3(3f, 0.5f, 2.5f);
    private static readonly Vector3 BookPos = new Vector3(5f, 0.55f, 5f);
    private static readonly Vector3 DividerPos = new Vector3(0f, 2f, 0f);
    private static readonly Vector3 DividerSize = new Vector3(6f, 4f, 0.2f);

    // ── Cubby config ─────────────────────────────────────────────
    private static readonly Vector3 CubbyPos = new Vector3(-7f, 0.4f, -7f);
    private static readonly Vector3 CubbySize = new Vector3(1.2f, 0.8f, 0.6f);
    private const float CubbyDoorThick = 0.05f;
    private const float CubbySlideDistance = 0.6f;

    [MenuItem("Window/Iris/Build Lighting Test Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
                "Build Lighting Test Scene",
                "This will CREATE A NEW SCENE from scratch.\n\n" +
                "Any unsaved changes to the current scene will be lost.\n\n" +
                "Are you sure?",
                "Build", "Cancel"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);
        int surfacesLayer = EnsureLayer(SurfacesLayerName);
        int drawersLayer = EnsureLayer(DrawersLayerName);

        // ── 1. Main Camera + CinemachineBrain ──
        var camGO = BuildMainCamera();

        // ── 2. EventSystem ──
        var eventSysGO = new GameObject("EventSystem");
        eventSysGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSysGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // ── 3. Room geometry ──
        var roomParent = new GameObject("Room");
        BuildRoomGeometry(roomParent.transform);

        // ── 4. Lighting rig ──
        BuildLightingRig();

        // ── 5. Furniture & Props ──
        var sofaGO = BuildSofa(placeableLayer, surfacesLayer);
        var bookGO = BuildBook(placeableLayer);
        var testCubes = BuildTestCubes(placeableLayer);

        // ── 5b. Picture frame (wall-mountable) ──
        BuildPictureFrame(placeableLayer);

        // ── 6. Placement surfaces ──
        BuildPlacementSurfaces(roomParent.transform, surfacesLayer);

        // ── 7. Privacy cubby with DrawerController ──
        BuildCubby(surfacesLayer, drawersLayer);

        // ── 8. ObjectGrabber ──
        BuildObjectGrabber(placeableLayer, surfacesLayer);

        // ── 9. 8 CinemachineCameras ──
        var cameras = BuildCameras();

        // ── 10. NatureBox + WeatherSystem + GameClock ──
        BuildNatureBox();
        BuildWeatherSystem();
        BuildGameClock();

        // ── 11. PSXRenderController ──
        var psxGO = new GameObject("PSXRenderController");
        psxGO.AddComponent<PSXRenderController>();

        // ── 12. AudioManager ──
        BuildAudioManager();

        // ── 13. Debug HUD + LightingTestController ──
        BuildControllerAndHUD(cameras);

        Debug.Log("[LightingTestSceneBuilder] Lighting test scene built successfully.");
    }

    // ══════════════════════════════════════════════════════════════
    // Main Camera
    // ══════════════════════════════════════════════════════════════

    private static GameObject BuildMainCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        var brain = camGO.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);
        camGO.AddComponent<AudioListener>();
        return camGO;
    }

    // ══════════════════════════════════════════════════════════════
    // Room Geometry
    // ══════════════════════════════════════════════════════════════

    private static void BuildRoomGeometry(Transform parent)
    {
        float hw = RoomW * 0.5f;
        float hd = RoomD * 0.5f;

        var grey = MakeColor(0.45f, 0.45f, 0.48f);
        var wallColor = MakeColor(0.55f, 0.53f, 0.50f);
        var dividerColor = MakeColor(0.50f, 0.48f, 0.45f);

        // Floor
        CreateBox("Floor", parent, Vector3.zero,
            new Vector3(RoomW, WallThick, RoomD), grey, true);

        // Ceiling (open-top — skip)

        // ── Back wall (-Z) — solid ──
        CreateBox("Wall_Back", parent,
            new Vector3(0f, RoomH * 0.5f, -hd),
            new Vector3(RoomW, RoomH, WallThick), wallColor, true);

        // ── Left wall (-X) — solid ──
        CreateBox("Wall_Left", parent,
            new Vector3(-hw, RoomH * 0.5f, 0f),
            new Vector3(WallThick, RoomH, RoomD), wallColor, true);

        // ── Right wall (+X) — solid ──
        CreateBox("Wall_Right", parent,
            new Vector3(hw, RoomH * 0.5f, 0f),
            new Vector3(WallThick, RoomH, RoomD), wallColor, true);

        // ── Front wall (+Z) — 3 windows ──
        // Build as segments around the window openings
        BuildWindowWall(parent, hd, wallColor);

        // ── Internal divider wall ──
        CreateBox("Divider", parent, DividerPos, DividerSize, dividerColor, true);

        // ── Raised platform / bed ──
        CreateBox("Bed_Platform", parent, BedPos, BedSize, MakeColor(0.35f, 0.30f, 0.28f), true);

        Debug.Log("[LightingTestSceneBuilder] Room geometry built.");
    }

    private static void BuildWindowWall(Transform parent, float hd, Color wallColor)
    {
        float wallZ = hd;
        float wallCenterY = RoomH * 0.5f;

        // Window bottom edge and top edge
        float winBot = WindowY;
        float winTop = WindowY + WindowH;

        // 3 window X centers
        float[] windowXs = { -WindowSpacing, 0f, WindowSpacing };

        // Bottom strip (below all windows)
        CreateBox("FrontWall_Bottom", parent,
            new Vector3(0f, winBot * 0.5f, wallZ),
            new Vector3(RoomW, winBot, WallThick), wallColor, true);

        // Top strip (above all windows)
        float topStripH = RoomH - winTop;
        CreateBox("FrontWall_Top", parent,
            new Vector3(0f, winTop + topStripH * 0.5f, wallZ),
            new Vector3(RoomW, topStripH, WallThick), wallColor, true);

        // Pillars between and beside windows
        float halfWinW = WindowW * 0.5f;
        float pillarY = winBot + WindowH * 0.5f;

        // Far left pillar
        float leftEdge = -RoomW * 0.5f;
        float leftPillarEnd = windowXs[0] - halfWinW;
        float leftPillarW = leftPillarEnd - leftEdge;
        if (leftPillarW > 0.01f)
        {
            CreateBox("FrontWall_PillarL", parent,
                new Vector3(leftEdge + leftPillarW * 0.5f, pillarY, wallZ),
                new Vector3(leftPillarW, WindowH, WallThick), wallColor, true);
        }

        // Pillars between windows
        for (int i = 0; i < windowXs.Length - 1; i++)
        {
            float pStart = windowXs[i] + halfWinW;
            float pEnd = windowXs[i + 1] - halfWinW;
            float pW = pEnd - pStart;
            if (pW > 0.01f)
            {
                CreateBox($"FrontWall_Pillar{i}", parent,
                    new Vector3(pStart + pW * 0.5f, pillarY, wallZ),
                    new Vector3(pW, WindowH, WallThick), wallColor, true);
            }
        }

        // Far right pillar
        float rightPillarStart = windowXs[2] + halfWinW;
        float rightEdge = RoomW * 0.5f;
        float rightPillarW = rightEdge - rightPillarStart;
        if (rightPillarW > 0.01f)
        {
            CreateBox("FrontWall_PillarR", parent,
                new Vector3(rightPillarStart + rightPillarW * 0.5f, pillarY, wallZ),
                new Vector3(rightPillarW, WindowH, WallThick), wallColor, true);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Lighting Rig
    // ══════════════════════════════════════════════════════════════

    private static void BuildLightingRig()
    {
        // Directional light (sun)
        var sunGO = new GameObject("Sun_Directional");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.color = new Color(1f, 0.95f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // Warm orange point light
        var warmGO = new GameObject("Point_WarmFill");
        var warmLight = warmGO.AddComponent<Light>();
        warmLight.type = LightType.Point;
        warmLight.intensity = 0.8f;
        warmLight.range = 8f;
        warmLight.color = new Color(1f, 0.75f, 0.45f);
        warmLight.shadows = LightShadows.Soft;
        warmGO.transform.position = new Vector3(-3f, 3f, 2f);

        // Cool blue point light
        var coolGO = new GameObject("Point_CoolFill");
        var coolLight = coolGO.AddComponent<Light>();
        coolLight.type = LightType.Point;
        coolLight.intensity = 0.6f;
        coolLight.range = 7f;
        coolLight.color = new Color(0.45f, 0.65f, 1f);
        coolLight.shadows = LightShadows.Soft;
        coolGO.transform.position = new Vector3(3f, 2.5f, -2f);

        // Spot light through window (backlight test)
        var backlightGO = new GameObject("Spot_WindowBacklight");
        var backlight = backlightGO.AddComponent<Light>();
        backlight.type = LightType.Spot;
        backlight.intensity = 1.5f;
        backlight.range = 15f;
        backlight.spotAngle = 50f;
        backlight.color = new Color(1f, 0.92f, 0.8f);
        backlight.shadows = LightShadows.Soft;
        backlightGO.transform.position = new Vector3(0f, 3.5f, 7f);
        backlightGO.transform.rotation = Quaternion.Euler(30f, 180f, 0f);

        // Spot light — reading light on bed area
        var readGO = new GameObject("Spot_ReadingLight");
        var readLight = readGO.AddComponent<Light>();
        readLight.type = LightType.Spot;
        readLight.intensity = 1.2f;
        readLight.range = 6f;
        readLight.spotAngle = 40f;
        readLight.color = new Color(1f, 0.95f, 0.85f);
        readLight.shadows = LightShadows.Soft;
        readGO.transform.position = new Vector3(3f, 3.5f, 3f);
        readGO.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

        Debug.Log("[LightingTestSceneBuilder] Lighting rig built.");
    }

    // ══════════════════════════════════════════════════════════════
    // Furniture & Props
    // ══════════════════════════════════════════════════════════════

    private static GameObject BuildSofa(int placeableLayer, int surfacesLayer)
    {
        var sofaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets/sofa_resized_MAT.fbx");
        GameObject sofaGO;

        if (sofaPrefab != null)
        {
            sofaGO = (GameObject)PrefabUtility.InstantiatePrefab(sofaPrefab);
            sofaGO.name = "Sofa";
        }
        else
        {
            Debug.LogWarning("[LightingTestSceneBuilder] sofa_resized_MAT.fbx not found — using placeholder box.");
            sofaGO = CreateBox("Sofa", null, SofaPos,
                new Vector3(2f, 0.7f, 0.8f), MakeColor(0.5f, 0.35f, 0.3f), false);
        }

        sofaGO.transform.position = SofaPos;

        // Add Rigidbody (kinematic — not grabbable, just scenery with highlight)
        var rb = sofaGO.GetComponent<Rigidbody>();
        if (rb == null) rb = sofaGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // Ensure collider — use fitted collider for FBX imports
        if (sofaGO.GetComponent<Collider>() == null && sofaGO.GetComponentInChildren<Collider>() == null)
            AddFittedBoxCollider(sofaGO);

        // InteractableHighlight
        sofaGO.AddComponent<InteractableHighlight>();

        // PlacementSurface on top of sofa so items can be placed on it
        var sofaCol = sofaGO.GetComponent<BoxCollider>();
        if (sofaCol != null)
        {
            float topY = sofaGO.transform.position.y +
                (sofaCol.center.y + sofaCol.size.y * 0.5f) * sofaGO.transform.lossyScale.y;
            float sofaW = sofaCol.size.x * Mathf.Abs(sofaGO.transform.lossyScale.x);
            float sofaD = sofaCol.size.z * Mathf.Abs(sofaGO.transform.lossyScale.z);
            BuildSurface("SofaSurface", null,
                new Vector3(sofaGO.transform.position.x, topY + 0.01f, sofaGO.transform.position.z),
                new Bounds(Vector3.zero, new Vector3(sofaW - 0.1f, 0.1f, sofaD - 0.1f)),
                PlacementSurface.SurfaceAxis.Up, surfacesLayer);
        }

        Debug.Log("[LightingTestSceneBuilder] Sofa built.");
        return sofaGO;
    }

    private static GameObject BuildBook(int placeableLayer)
    {
        var bookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets/book_resized_MAT.fbx");
        GameObject bookGO;

        if (bookPrefab != null)
        {
            bookGO = (GameObject)PrefabUtility.InstantiatePrefab(bookPrefab);
            bookGO.name = "Book";
        }
        else
        {
            Debug.LogWarning("[LightingTestSceneBuilder] book_resized_MAT.fbx not found — using placeholder box.");
            bookGO = CreateBox("Book", null, BookPos,
                new Vector3(0.2f, 0.05f, 0.28f), MakeColor(0.6f, 0.2f, 0.2f), false);
        }

        bookGO.transform.position = BookPos;
        SetLayerRecursive(bookGO, placeableLayer);

        // Rigidbody (kinematic for PlaceableObject)
        var rb = bookGO.GetComponent<Rigidbody>();
        if (rb == null) rb = bookGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // Collider — use fitted collider for FBX imports (default 1x1x1 on root without MeshFilter)
        if (bookGO.GetComponent<Collider>() == null && bookGO.GetComponentInChildren<Collider>() == null)
            AddFittedBoxCollider(bookGO);

        // PlaceableObject
        var placeable = bookGO.AddComponent<PlaceableObject>();
        var placeableSO = new SerializedObject(placeable);
        placeableSO.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.General;
        placeableSO.FindProperty("canWallMount").boolValue = false;
        placeableSO.FindProperty("_itemDescription").stringValue = "A well-worn book";
        placeableSO.ApplyModifiedPropertiesWithoutUndo();

        // InteractableHighlight
        bookGO.AddComponent<InteractableHighlight>();

        // ReactableTag
        var tag = bookGO.AddComponent<ReactableTag>();
        var tagSO = new SerializedObject(tag);
        tagSO.FindProperty("displayName").stringValue = "Book";
        var tagsProp = tagSO.FindProperty("tags");
        tagsProp.arraySize = 2;
        tagsProp.GetArrayElementAtIndex(0).stringValue = "book";
        tagsProp.GetArrayElementAtIndex(1).stringValue = "reading";
        tagSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[LightingTestSceneBuilder] Book built.");
        return bookGO;
    }

    private static GameObject[] BuildTestCubes(int placeableLayer)
    {
        var cubes = new GameObject[3];
        Color[] colors = {
            new Color(0.95f, 0.15f, 0.1f),  // vivid red
            new Color(0.1f, 0.85f, 0.2f),   // vivid green
            new Color(0.1f, 0.25f, 0.95f)   // vivid blue
        };
        string[] names = { "TestCube_Red", "TestCube_Green", "TestCube_Blue" };
        Vector3[] positions = {
            new Vector3(-3f, 0.25f, -4f),
            new Vector3(0f, 0.25f, -4f),
            new Vector3(3f, 0.25f, -4f)
        };

        for (int i = 0; i < 3; i++)
        {
            var cube = CreateBox(names[i], null, positions[i],
                Vector3.one * 0.3f, colors[i], false);
            cube.layer = placeableLayer;

            var rb = cube.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var placeable = cube.AddComponent<PlaceableObject>();
            var so = new SerializedObject(placeable);
            so.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.General;
            so.FindProperty("canWallMount").boolValue = false;
            so.FindProperty("_itemDescription").stringValue = names[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            cube.AddComponent<InteractableHighlight>();

            cubes[i] = cube;
        }

        Debug.Log("[LightingTestSceneBuilder] Test cubes built.");
        return cubes;
    }

    // ══════════════════════════════════════════════════════════════
    // Picture Frame (wall-mountable)
    // ══════════════════════════════════════════════════════════════

    private static void BuildPictureFrame(int placeableLayer)
    {
        // Flat box representing a framed picture
        var frameGO = CreateBox("PictureFrame", null,
            new Vector3(0f, 2.5f, -9.8f),  // near back wall
            new Vector3(0.8f, 0.6f, 0.04f),
            MakeColor(0.25f, 0.20f, 0.15f), false);
        frameGO.layer = placeableLayer;

        var rb = frameGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var placeable = frameGO.AddComponent<PlaceableObject>();
        var so = new SerializedObject(placeable);
        so.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.General;
        so.FindProperty("canWallMount").boolValue = true;
        so.FindProperty("_itemDescription").stringValue = "A picture frame";
        so.ApplyModifiedPropertiesWithoutUndo();

        frameGO.AddComponent<InteractableHighlight>();

        // Small colored "canvas" inset to make it look like a picture
        var canvasInset = CreateBox("Canvas", frameGO.transform,
            new Vector3(0f, 2.5f, -9.78f),
            new Vector3(0.65f, 0.45f, 0.02f),
            MakeColor(0.55f, 0.40f, 0.35f), false);
        canvasInset.layer = placeableLayer;
        // Remove collider from inset so it doesn't interfere
        var insetCol = canvasInset.GetComponent<Collider>();
        if (insetCol != null) Object.DestroyImmediate(insetCol);

        Debug.Log("[LightingTestSceneBuilder] Picture frame built.");
    }

    // ══════════════════════════════════════════════════════════════
    // Placement Surfaces
    // ══════════════════════════════════════════════════════════════

    private static void BuildPlacementSurfaces(Transform roomParent, int surfacesLayer)
    {
        // Floor surface (covers the whole room)
        BuildSurface("FloorSurface", null,
            new Vector3(0f, WallThick * 0.5f + 0.01f, 0f),
            new Bounds(Vector3.zero, new Vector3(RoomW - 1f, 0.1f, RoomD - 1f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        // Bed top surface
        BuildSurface("BedSurface", null,
            BedPos + Vector3.up * (BedSize.y * 0.5f + 0.01f),
            new Bounds(Vector3.zero, new Vector3(BedSize.x - 0.2f, 0.1f, BedSize.z - 0.2f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        // Internal wall surface (forward-facing, for wall mount tests)
        BuildSurface("DividerSurface_Front", null,
            DividerPos + new Vector3(0f, 0f, DividerSize.z * 0.5f + 0.01f),
            new Bounds(Vector3.zero, new Vector3(DividerSize.x - 0.3f, DividerSize.y - 0.3f, 0.1f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);

        // Divider back face surface
        BuildSurface("DividerSurface_Back", null,
            DividerPos - new Vector3(0f, 0f, DividerSize.z * 0.5f + 0.01f),
            new Bounds(Vector3.zero, new Vector3(DividerSize.x - 0.3f, DividerSize.y - 0.3f, 0.1f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);

        // Back wall surface (for picture frames)
        float hw = RoomW * 0.5f;
        float hd = RoomD * 0.5f;
        BuildSurface("BackWallSurface", null,
            new Vector3(0f, RoomH * 0.5f, -hd + WallThick * 0.5f + 0.01f),
            new Bounds(Vector3.zero, new Vector3(RoomW - 1f, RoomH - 0.5f, 0.1f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);

        // Left wall surface — rotate so Forward points +X (inward)
        BuildSurface("LeftWallSurface", null,
            new Vector3(-hw + WallThick * 0.5f + 0.01f, RoomH * 0.5f, 0f),
            new Bounds(Vector3.zero, new Vector3(RoomD - 1f, RoomH - 0.5f, 0.1f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);
        GameObject.Find("LeftWallSurface").transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        // Right wall surface — rotate so Forward points -X (inward)
        BuildSurface("RightWallSurface", null,
            new Vector3(hw - WallThick * 0.5f - 0.01f, RoomH * 0.5f, 0f),
            new Bounds(Vector3.zero, new Vector3(RoomD - 1f, RoomH - 0.5f, 0.1f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);
        GameObject.Find("RightWallSurface").transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        Debug.Log("[LightingTestSceneBuilder] Placement surfaces built.");
    }

    private static void BuildSurface(string name, Transform parent, Vector3 position,
        Bounds localBounds, PlacementSurface.SurfaceAxis axis, int layerIndex)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent);
        go.transform.position = position;

        // If it's a back-facing wall, rotate 180 so Forward points the other way
        if (name.Contains("_Back"))
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var surface = go.AddComponent<PlacementSurface>();
        var so = new SerializedObject(surface);
        so.FindProperty("localBounds").boundsValue = localBounds;
        so.FindProperty("normalAxis").enumValueIndex = (int)axis;
        so.FindProperty("surfaceLayerIndex").intValue = layerIndex;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════════
    // Privacy Cubby (DrawerController)
    // ══════════════════════════════════════════════════════════════

    private static void BuildCubby(int surfacesLayer, int drawersLayer)
    {
        var cubbyParent = new GameObject("PrivacyCubby");
        cubbyParent.transform.position = CubbyPos;

        var shellColor = MakeColor(0.38f, 0.35f, 0.32f);
        var doorColor = MakeColor(0.48f, 0.42f, 0.38f);

        // Shell — open front box (back + left + right + top + bottom)
        float hw = CubbySize.x * 0.5f;
        float hh = CubbySize.y * 0.5f;
        float hd = CubbySize.z * 0.5f;

        CreateBox("Cubby_Back", cubbyParent.transform,
            CubbyPos + new Vector3(0f, 0f, -hd),
            new Vector3(CubbySize.x, CubbySize.y, CubbyDoorThick), shellColor, true);

        CreateBox("Cubby_Left", cubbyParent.transform,
            CubbyPos + new Vector3(-hw, 0f, 0f),
            new Vector3(CubbyDoorThick, CubbySize.y, CubbySize.z), shellColor, true);

        CreateBox("Cubby_Right", cubbyParent.transform,
            CubbyPos + new Vector3(hw, 0f, 0f),
            new Vector3(CubbyDoorThick, CubbySize.y, CubbySize.z), shellColor, true);

        CreateBox("Cubby_Top", cubbyParent.transform,
            CubbyPos + new Vector3(0f, hh, 0f),
            new Vector3(CubbySize.x, CubbyDoorThick, CubbySize.z), shellColor, true);

        CreateBox("Cubby_Bottom", cubbyParent.transform,
            CubbyPos + new Vector3(0f, -hh, 0f),
            new Vector3(CubbySize.x, CubbyDoorThick, CubbySize.z), shellColor, true);

        // Sliding door panel
        var doorGO = CreateBox("CubbyDoor", cubbyParent.transform,
            CubbyPos + new Vector3(0f, 0f, hd),
            new Vector3(CubbySize.x, CubbySize.y, CubbyDoorThick), doorColor, false);
        doorGO.layer = drawersLayer;

        // Contents root (inside the cubby)
        var contentsGO = new GameObject("CubbyContents");
        contentsGO.transform.SetParent(cubbyParent.transform);
        contentsGO.transform.position = CubbyPos;

        // DrawerController on door
        var drawer = doorGO.AddComponent<DrawerController>();
        var drawerSO = new SerializedObject(drawer);
        drawerSO.FindProperty("slideDistance").floatValue = CubbySlideDistance;
        drawerSO.FindProperty("slideDuration").floatValue = 0.4f;
        drawerSO.FindProperty("contentsRoot").objectReferenceValue = contentsGO;
        drawerSO.FindProperty("_maxCapacity").intValue = 3;
        drawerSO.ApplyModifiedPropertiesWithoutUndo();

        // Interior PlacementSurface (so ObjectGrabber can store items)
        BuildSurface("CubbySurface", cubbyParent.transform,
            CubbyPos + new Vector3(0f, -hh + 0.05f, 0f),
            new Bounds(Vector3.zero, new Vector3(CubbySize.x - 0.1f, 0.1f, CubbySize.z - 0.1f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        Debug.Log("[LightingTestSceneBuilder] Privacy cubby built.");
    }

    // ══════════════════════════════════════════════════════════════
    // ObjectGrabber
    // ══════════════════════════════════════════════════════════════

    private static void BuildObjectGrabber(int placeableLayer, int surfacesLayer)
    {
        var go = new GameObject("ObjectGrabber");
        var grabber = go.AddComponent<ObjectGrabber>();
        var so = new SerializedObject(grabber);
        so.FindProperty("placeableLayer").intValue = 1 << placeableLayer;
        so.FindProperty("surfaceLayer").intValue = 1 << surfacesLayer;
        so.FindProperty("gridSize").floatValue = 0.2f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Auto-enable (no DayPhaseManager in this scene, so SetEnabled must be forced)
        // ObjectGrabber checks _isEnabled — we need to call SetEnabled(true) at runtime.
        // Add a small helper to do this.
        var enabler = go.AddComponent<ObjectGrabberAutoEnabler>();

        Debug.Log("[LightingTestSceneBuilder] ObjectGrabber built.");
    }

    // ══════════════════════════════════════════════════════════════
    // 8 Cinemachine Cameras
    // ══════════════════════════════════════════════════════════════

    private static CinemachineCamera[] BuildCameras()
    {
        var cameras = new CinemachineCamera[8];
        var cameraParent = new GameObject("Cameras");

        // Room spans x:[-10,10] y:[0,5] z:[-10,10]. Perspective cams inside room.
        // 4 Perspective cameras
        cameras[0] = BuildCinemachineCamera("Cam1_FrontOverview", cameraParent.transform,
            new Vector3(0f, 3f, -8f), Quaternion.LookRotation(new Vector3(0f, -0.15f, 1f)),
            60f, false, 20);

        cameras[1] = BuildCinemachineCamera("Cam2_WindowBacklight", cameraParent.transform,
            new Vector3(-7f, 2.5f, 2f), Quaternion.LookRotation(new Vector3(1f, -0.1f, 0.4f)),
            55f, false, 0);

        cameras[2] = BuildCinemachineCamera("Cam3_CloseInterior", cameraParent.transform,
            new Vector3(3f, 1.5f, 3f), Quaternion.LookRotation(new Vector3(0.5f, -0.2f, 0.5f)),
            45f, false, 0);

        cameras[3] = BuildCinemachineCamera("Cam4_HighAngle", cameraParent.transform,
            new Vector3(0f, 4.5f, 0f), Quaternion.LookRotation(new Vector3(0.05f, -1f, 0.15f)),
            70f, false, 0);

        // 4 Orthographic cameras (outside room — elevation/plan views)
        cameras[4] = BuildCinemachineCamera("Cam5_OrthoFront", cameraParent.transform,
            new Vector3(0f, 2.5f, -15f), Quaternion.LookRotation(Vector3.forward),
            10f, true, 0);

        cameras[5] = BuildCinemachineCamera("Cam6_OrthoSide", cameraParent.transform,
            new Vector3(-15f, 2.5f, 0f), Quaternion.LookRotation(Vector3.right),
            10f, true, 0);

        cameras[6] = BuildCinemachineCamera("Cam7_OrthoTop", cameraParent.transform,
            new Vector3(0f, 15f, 0f), Quaternion.LookRotation(Vector3.down),
            12f, true, 0);

        cameras[7] = BuildCinemachineCamera("Cam8_OrthoClose", cameraParent.transform,
            new Vector3(4f, 1.5f, 2f), Quaternion.LookRotation(Vector3.forward),
            3f, true, 0);

        Debug.Log("[LightingTestSceneBuilder] 8 cameras built.");
        return cameras;
    }

    private static CinemachineCamera BuildCinemachineCamera(string name, Transform parent,
        Vector3 position, Quaternion rotation, float fovOrSize, bool orthographic, int priority)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = rotation;

        var vcam = go.AddComponent<CinemachineCamera>();
        vcam.Priority = priority;

        var lens = new LensSettings
        {
            FieldOfView = orthographic ? 60f : fovOrSize,
            OrthographicSize = orthographic ? fovOrSize : 5f,
            NearClipPlane = 0.1f,
            FarClipPlane = 300f,
            ModeOverride = orthographic
                ? LensSettings.OverrideModes.Orthographic
                : LensSettings.OverrideModes.None
        };
        vcam.Lens = lens;

        return vcam;
    }

    // ══════════════════════════════════════════════════════════════
    // NatureBox + Weather + GameClock
    // ══════════════════════════════════════════════════════════════

    private static void BuildNatureBox()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "NatureBox";
        go.transform.position = Vector3.zero;
        go.transform.localScale = Vector3.one * 200f;

        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/nature.mat");
        if (mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;
        else
            Debug.LogWarning("[LightingTestSceneBuilder] nature.mat not found — NatureBox will use default material.");

        go.AddComponent<NatureBoxController>();
        Debug.Log("[LightingTestSceneBuilder] NatureBox built.");
    }

    private static void BuildWeatherSystem()
    {
        var go = new GameObject("WeatherSystem");
        go.AddComponent<WeatherSystem>();
        Debug.Log("[LightingTestSceneBuilder] WeatherSystem built.");
    }

    private static void BuildGameClock()
    {
        var go = new GameObject("GameClock");
        go.AddComponent<GameClock>();
        Debug.Log("[LightingTestSceneBuilder] GameClock built.");
    }

    private static void BuildAudioManager()
    {
        // AudioManager is DDoL — only create if not already in scene
        var existing = Object.FindFirstObjectByType<AudioManager>();
        if (existing != null) return;

        var go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
        Debug.Log("[LightingTestSceneBuilder] AudioManager built.");
    }

    // ══════════════════════════════════════════════════════════════
    // Controller + Debug HUD
    // ══════════════════════════════════════════════════════════════

    private static void BuildControllerAndHUD(CinemachineCamera[] cameras)
    {
        // LightingTestController builds its own HUD at runtime in Start().
        // We only need to create the GO, add the component, and wire the cameras array.
        var controllerGO = new GameObject("LightingTestController");
        var controller = controllerGO.AddComponent<LightingTestController>();

        var controllerSO = new SerializedObject(controller);
        var camerasProp = controllerSO.FindProperty("_cameras");
        camerasProp.arraySize = cameras.Length;
        for (int i = 0; i < cameras.Length; i++)
            camerasProp.GetArrayElementAtIndex(i).objectReferenceValue = cameras[i];
        controllerSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[LightingTestSceneBuilder] Controller built (HUD created at runtime).");
    }

    // ══════════════════════════════════════════════════════════════
    // Shared Helpers
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a BoxCollider fitted to actual renderer bounds.
    /// AddComponent&lt;BoxCollider&gt;() on a parent without MeshFilter gives a default 1x1x1 box —
    /// this method computes bounds from child renderers and fits the collider properly.
    /// </summary>
    private static BoxCollider AddFittedBoxCollider(GameObject go)
    {
        var bc = go.AddComponent<BoxCollider>();
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return bc;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // Convert world-space bounds to local space
        bc.center = go.transform.InverseTransformPoint(bounds.center);
        var scale = go.transform.lossyScale;
        bc.size = new Vector3(
            bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
            bounds.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.001f));
        return bc;
    }

    private static GameObject CreateBox(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color, bool isStatic)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.isStatic = isStatic;

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

    private static Color MakeColor(float r, float g, float b)
    {
        return new Color(r, g, b);
    }

    private static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layerName)
                return i;
        }

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[LightingTestSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[LightingTestSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
