using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the bookcase_browsing scene with
/// a room, bookcase full of books, drawers, perfume bottles,
/// coffee table books, item inspector, and first-person browse camera.
/// Menu: Window > Iris > Build Bookcase Browsing Scene
/// </summary>
public static class BookcaseSceneBuilder
{
    private const string BooksLayerName = "Books";
    private const string DrawersLayerName = "Drawers";
    private const string PerfumesLayerName = "Perfumes";
    private const string CoffeeTableBooksLayerName = "CoffeeTableBooks";

    // Bookcase dimensions
    private const float CaseWidth = 2.4f;
    private const float CaseHeight = 2.2f;
    private const float CaseDepth = 0.35f;
    private const float CaseCenterZ = 0f;
    private const int ShelfCount = 5;       // 5 shelves = 4 usable rows
    private const float ShelfThickness = 0.02f;
    private const float SidePanelThickness = 0.03f;

    // Drawer dimensions
    private const float DrawerHeight = 0.25f;
    private const float DrawerSlideDistance = 0.3f;

    // Book generation
    private const float MinBookThickness = 0.02f;
    private const float MaxBookThickness = 0.04f;
    private const float BookGap = 0.003f;

    // All 15 books packed together on a single row
    private const int BookRow = 1; // row 1 (second shelf from bottom)

    // Spine colors — 10 muted library tones
    private static readonly Color[] SpineColors =
    {
        new Color(0.55f, 0.15f, 0.15f),    // dark red
        new Color(0.12f, 0.15f, 0.40f),    // navy
        new Color(0.12f, 0.35f, 0.15f),    // forest green
        new Color(0.70f, 0.62f, 0.48f),    // tan
        new Color(0.45f, 0.20f, 0.40f),    // plum
        new Color(0.25f, 0.25f, 0.25f),    // charcoal
        new Color(0.85f, 0.82f, 0.75f),    // cream
        new Color(0.50f, 0.12f, 0.18f),    // burgundy
        new Color(0.10f, 0.10f, 0.25f),    // midnight
        new Color(0.35f, 0.38f, 0.18f),    // olive
    };

    // 5 books — content is placeholder, replace with real copy
    private static readonly string[] BookTitles =
    {
        "The Flower Encyclopedia",
        "Still Life with Vase",
        "Roots in Darkness",
        "Phantom Garden Vol. 1",
        "Book Five",
    };

    private static readonly string[] BookAuthors =
    {
        "Dr. Iris Greenleaf",
        "Olive Stillman",
        "Eleanor Moss",
        "Hana Sakurai",
        "TBD",
    };

    private static readonly string[][] BookPages =
    {
        // Book 0 — The Flower Encyclopedia (flower encyclopedia)
        new[] {
            "(Flower encyclopedia content — page 1)",
            "(Flower encyclopedia content — page 2)",
        },
        // Book 1 — Still Life with Vase (coffee table book)
        new[] {
            "(Coffee table book content — page 1)",
            "(Coffee table book content — page 2)",
        },
        // Book 2 — Roots in Darkness (novel)
        new[] {
            "(Novel content — page 1)",
            "(Novel content — page 2)",
        },
        // Book 3 — Phantom Garden Vol. 1 (manga)
        new[] {
            "(Manga content — page 1)",
            "(Manga content — page 2)",
        },
        // Book 4 — Book Five (TBD)
        new[] {
            "(TBD content — page 1)",
            "(TBD content — page 2)",
        },
    };

    // Perfume data
    private static readonly string[] PerfumeNames = { "Twilight Bloom", "Morning Dew", "Cedar Ember" };
    private static readonly string[] PerfumeDescriptions =
    {
        "A dusky floral scent that shifts the room toward warm evening tones.",
        "Light and crisp, like sunlight through wet leaves on a spring morning.",
        "Smoky and grounding, with hints of resin and dried wood."
    };
    private static readonly Color[] PerfumeBottleColors =
    {
        new Color(0.6f, 0.3f, 0.7f, 0.8f),
        new Color(0.3f, 0.7f, 0.5f, 0.8f),
        new Color(0.7f, 0.4f, 0.2f, 0.8f),
    };
    private static readonly Color[] PerfumeSprayColors =
    {
        new Color(0.8f, 0.5f, 0.9f, 0.4f),
        new Color(0.5f, 0.9f, 0.7f, 0.4f),
        new Color(0.9f, 0.6f, 0.3f, 0.4f),
    };
    private static readonly Color[] PerfumeLightColors =
    {
        new Color(0.9f, 0.7f, 0.95f),
        new Color(0.8f, 1f, 0.9f),
        new Color(1f, 0.85f, 0.65f),
    };
    private static readonly float[] PerfumeLightIntensities = { 0.8f, 1.2f, 0.9f };

    // Coffee table book data (5 books)
    private static readonly string[] CoffeeBookTitles =
    {
        "Arrangements",
        "Petal Studies",
        "Botanical Illustrations",
        "Indoor Gardens",
        "Pressed Flower Almanac",
    };
    private static readonly string[] CoffeeBookAuthors =
    {
        "Yuki Tanaka",
        "Lena Bloom",
        "Rosa Thornfield",
        "Fern Whitley",
        "Violet Stemworth",
    };
    private static readonly string[] CoffeeBookDescriptions =
    {
        "A heavy book of ikebana arrangements, each page a meditation.",
        "Macro photographs of petals — textures you've never noticed.",
        "Hand-drawn plates of roses, ferns, and orchids in exacting detail.",
        "A guide to keeping green things alive in small, dark apartments.",
        "Seasonal pressed flowers with notes on drying technique and meaning.",
    };
    private static readonly Color[] CoffeeBookColors =
    {
        new Color(0.15f, 0.25f, 0.35f),
        new Color(0.5f, 0.2f, 0.3f),
        new Color(0.20f, 0.35f, 0.20f),
        new Color(0.35f, 0.30f, 0.15f),
        new Color(0.40f, 0.15f, 0.30f),
    };

    [MenuItem("Window/Iris/Archived Builders/Build Bookcase Browsing Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int booksLayer = EnsureLayer(BooksLayerName);
        int drawersLayer = EnsureLayer(DrawersLayerName);
        int perfumesLayer = EnsureLayer(PerfumesLayerName);
        int coffeeTableBooksLayer = EnsureLayer(CoffeeTableBooksLayerName);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        light.color = new Color(1f, 0.95f, 0.85f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera + BookcaseBrowseCamera ──────────────────────
        var camGO = BuildCamera();

        // ── 3. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 4. Coffee table (scene-specific room furniture) ──────────
        BuildCoffeeTable();

        // ── 5. Bookcase unit (frame, books, perfumes, drawers, coffee books)
        float coffeeTableTopY = 0.415f;
        Vector3 ctBase = new Vector3(0.8f, coffeeTableTopY, 0.8f);
        Quaternion ctRot = Quaternion.Euler(0f, 5f, 90f);

        var bookcaseRoot = BuildBookcaseUnit(booksLayer, drawersLayer,
            perfumesLayer, coffeeTableBooksLayer, ctBase, ctRot);
        bookcaseRoot.transform.position = new Vector3(0f, 0f, 1.8f);

        // ── 7. Environment mood controller ───────────────────────────
        BuildEnvironmentMood(lightGO);

        // ── 8. Item Inspector ────────────────────────────────────────
        var inspector = BuildItemInspector(camGO);

        // ── 9. BookInteractionManager + UI ───────────────────────────
        BuildInteractionManager(camGO, inspector, booksLayer, drawersLayer,
            perfumesLayer, coffeeTableBooksLayer);

        // ── 10. Save scene ───────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/bookcase_browsing.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[BookcaseSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared Bookcase Unit — builds the complete bookcase geometry
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a self-contained bookcase unit at local origin (0,0,0) with all items:
    /// frame, books, perfume bottles, drawers, and coffee table books.
    /// coffeeTableBase/Rotation are the world-space target for books sent to the coffee table.
    /// </summary>
    public static GameObject BuildBookcaseUnit(int booksLayer, int drawersLayer,
        int perfumesLayer, int coffeeTableBooksLayer,
        Vector3 coffeeTableBase, Quaternion coffeeTableRotation)
    {
        var bookcaseRoot = new GameObject("Bookcase");

        BuildBookcaseFrame(bookcaseRoot);
        BuildBooks(bookcaseRoot, booksLayer);
        BuildPerfumeShelf(bookcaseRoot, perfumesLayer);
        BuildDrawers(bookcaseRoot, drawersLayer);
        BuildCoffeeTableBooks(bookcaseRoot, coffeeTableBooksLayer, coffeeTableBase, coffeeTableRotation);

        return bookcaseRoot;
    }

    // ════════════════════════════════════════════════════════════════════
    // Camera
    // ════════════════════════════════════════════════════════════════════

    private static GameObject BuildCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.06f, 0.05f);
        cam.fieldOfView = 60f;
        camGO.transform.position = new Vector3(0f, 1.5f, 0f);
        camGO.transform.rotation = Quaternion.identity;

        camGO.AddComponent<BookcaseBrowseCamera>();

        // Reading anchor — child of camera, positioned in front
        var anchorGO = new GameObject("ReadingAnchor");
        anchorGO.transform.SetParent(camGO.transform);
        anchorGO.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
        anchorGO.transform.localRotation = Quaternion.identity;

        // Inspect anchor — child of camera, slightly closer
        var inspectAnchor = new GameObject("InspectAnchor");
        inspectAnchor.transform.SetParent(camGO.transform);
        inspectAnchor.transform.localPosition = new Vector3(0f, 0f, 0.4f);
        inspectAnchor.transform.localRotation = Quaternion.identity;

        return camGO;
    }

    // ════════════════════════════════════════════════════════════════════
    // Room
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.025f, 1f), new Vector3(6f, 0.05f, 4f),
            new Color(0.30f, 0.20f, 0.12f));

        CreateBox("BackWall", parent.transform,
            new Vector3(0f, 1.5f, 2.2f), new Vector3(6f, 3f, 0.1f),
            new Color(0.85f, 0.78f, 0.68f));

        CreateBox("SideWall_L", parent.transform,
            new Vector3(-3f, 1.5f, 1f), new Vector3(0.1f, 3f, 4f),
            new Color(0.82f, 0.75f, 0.65f));

        CreateBox("SideWall_R", parent.transform,
            new Vector3(3f, 1.5f, 1f), new Vector3(0.1f, 3f, 4f),
            new Color(0.82f, 0.75f, 0.65f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Bookcase Frame (original geometry — drawers are separate below)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildBookcaseFrame(GameObject bookcaseRoot)
    {
        var frame = new GameObject("BookcaseFrame");
        frame.transform.SetParent(bookcaseRoot.transform);

        Color darkBrown = new Color(0.25f, 0.15f, 0.08f);
        float caseX = 0f;
        float caseBottomY = 0f;

        // Side panels
        CreateBox("SidePanel_L", frame.transform,
            new Vector3(caseX - CaseWidth / 2f, caseBottomY + CaseHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, CaseHeight, CaseDepth),
            darkBrown);

        CreateBox("SidePanel_R", frame.transform,
            new Vector3(caseX + CaseWidth / 2f, caseBottomY + CaseHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, CaseHeight, CaseDepth),
            darkBrown);

        // Back panel
        CreateBox("BackPanel", frame.transform,
            new Vector3(caseX, caseBottomY + CaseHeight / 2f, CaseCenterZ + CaseDepth / 2f - 0.01f),
            new Vector3(CaseWidth, CaseHeight, 0.015f),
            new Color(0.20f, 0.12f, 0.06f));

        // Shelves — 5 horizontal planks (including top and bottom)
        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float rowHeight = CaseHeight / ShelfCount;

        int surfacesLayer = EnsureLayer("Surfaces");

        for (int i = 0; i <= ShelfCount - 1; i++)
        {
            float shelfY = caseBottomY + i * rowHeight;
            var shelfGO = CreateBox($"Shelf_{i}", frame.transform,
                new Vector3(caseX, shelfY, CaseCenterZ),
                new Vector3(innerWidth, ShelfThickness, CaseDepth),
                darkBrown);

            // Rows 0 and 4 are empty display shelves — add PlacementSurface
            if (i == 0 || i == 4)
            {
                shelfGO.layer = surfacesLayer;
                shelfGO.isStatic = false;
                var surface = shelfGO.AddComponent<PlacementSurface>();
                var surfSO = new SerializedObject(surface);
                surfSO.FindProperty("localBounds").boundsValue = new Bounds(
                    Vector3.zero,
                    new Vector3(innerWidth, 0.05f, CaseDepth));
                surfSO.FindProperty("normalAxis").enumValueIndex = (int)PlacementSurface.SurfaceAxis.Up;
                surfSO.FindProperty("surfaceLayerIndex").intValue = surfacesLayer;
                surfSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Books (10 packed on row 1 shelf + 5 stacked flat on floor)
    // Row 0: empty display shelf
    // Row 1: 10 normal books (packed upright)
    // Row 2: 5 coffee table books (stacked flat, disheveled pile)
    // Row 3: 3 perfumes
    // Row 4: empty display shelf (top)
    // Floor beside bookcase: 5 books stacked flat
    // ════════════════════════════════════════════════════════════════════

    // All books go on the shelf (no floor stack)
    private const int ShelfBookCount = 5;

    private static void BuildBooks(GameObject bookcaseRoot, int booksLayer)
    {
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var booksParent = new GameObject("Books");
        booksParent.transform.SetParent(bookcaseRoot.transform);

        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float rowHeight = CaseHeight / ShelfCount;
        float caseLeftX = -innerWidth / 2f;

        float shelfTopY = BookRow * rowHeight + ShelfThickness / 2f;
        float availableHeight = rowHeight - ShelfThickness;

        // Deterministic seed for visual variety that's reproducible
        Random.State savedState = Random.state;
        Random.InitState(42);

        float xCursor = caseLeftX + 0.01f;

        for (int bookIndex = 0; bookIndex < BookTitles.Length; bookIndex++)
        {
            float thickness = Mathf.Lerp(MinBookThickness, MaxBookThickness,
                Random.Range(0f, 1f));
            float heightFrac = Random.Range(0.70f, 0.95f);
            float bookHeight = availableHeight * heightFrac;
            float depthFrac = Random.Range(0.70f, 0.95f);
            float bookDepth = CaseDepth * depthFrac;
            Color color = SpineColors[bookIndex % SpineColors.Length];

            string title = BookTitles[bookIndex];
            string author = BookAuthors[bookIndex];
            string[] pages = BookPages[bookIndex];

            var def = ScriptableObject.CreateInstance<BookDefinition>();
            def.title = title;
            def.author = author;
            def.pageTexts = pages;
            def.spineColor = color;
            def.heightScale = heightFrac;
            def.thicknessScale = thickness;

            // Hidden items in select books
            if (bookIndex == 0) // "The Flower Encyclopedia" — pressed flower
            {
                def.hasHiddenItem = true;
                def.hiddenItemDescription = "A pressed daisy, flattened between the pages.";
                def.hiddenItemPage = 0;
            }

            string defPath = $"{defDir}/Book_{bookIndex:D3}_{title.Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(def, defPath);

            Vector3 bookPos;
            Vector3 bookScale;
            Quaternion bookRot = Quaternion.identity;

            // All books on shelf, upright
            {
                float bookX = xCursor + thickness / 2f;
                float bookY = shelfTopY + bookHeight / 2f;
                float bookZ = CaseCenterZ - (CaseDepth - bookDepth) / 2f * 0.5f;

                bookPos = new Vector3(bookX, bookY, bookZ);
                bookScale = new Vector3(thickness, bookHeight, bookDepth);
                xCursor += thickness + BookGap;
            }

            var bookGO = CreateBox($"Book_{bookIndex}", booksParent.transform,
                bookPos, bookScale, color);
            bookGO.transform.localRotation = bookRot;
            bookGO.isStatic = false;
            bookGO.layer = booksLayer;

            var volume = bookGO.AddComponent<BookVolume>();
            var pagesRoot = BuildBookPages(bookGO.transform, thickness, bookHeight, bookDepth);

            var so = new SerializedObject(volume);
            so.FindProperty("definition").objectReferenceValue = def;
            so.FindProperty("pagesRoot").objectReferenceValue = pagesRoot;

            // Wire page labels (left + right — first 2 TMP_Text in page GOs)
            var pageCanvas = pagesRoot.transform.GetChild(0); // PageCanvas
            var leftText = pageCanvas.Find("PageLeft/Text");
            var rightText = pageCanvas.Find("PageRight/Text");
            var labelsProperty = so.FindProperty("pageLabels");
            labelsProperty.arraySize = 2;
            labelsProperty.GetArrayElementAtIndex(0).objectReferenceValue =
                leftText != null ? leftText.GetComponent<TMP_Text>() : null;
            labelsProperty.GetArrayElementAtIndex(1).objectReferenceValue =
                rightText != null ? rightText.GetComponent<TMP_Text>() : null;

            // Wire navigation elements
            var indicatorT = pageCanvas.Find("PageIndicator");
            var navLeftT = pageCanvas.Find("NavLeft");
            var navRightT = pageCanvas.Find("NavRight");
            var hiddenT = pageCanvas.Find("HiddenItemLabel");
            so.FindProperty("pageIndicator").objectReferenceValue =
                indicatorT != null ? indicatorT.GetComponent<TMP_Text>() : null;
            so.FindProperty("navLeft").objectReferenceValue =
                navLeftT != null ? navLeftT.GetComponent<TMP_Text>() : null;
            so.FindProperty("navRight").objectReferenceValue =
                navRightT != null ? navRightT.GetComponent<TMP_Text>() : null;
            so.FindProperty("hiddenItemLabel").objectReferenceValue =
                hiddenT != null ? hiddenT.GetComponent<TMP_Text>() : null;

            so.ApplyModifiedPropertiesWithoutUndo();

            bookGO.AddComponent<InteractableHighlight>();

            BuildBookSpineTitle(bookGO.transform, title, thickness, bookHeight, bookDepth);
        }

        Random.state = savedState;
        Debug.Log($"[BookcaseSceneBuilder] Created {BookTitles.Length} books on row {BookRow}.");
    }

    private static GameObject BuildBookPages(Transform bookTransform, float thickness, float height, float depth)
    {
        var pagesRoot = new GameObject("Pages");
        pagesRoot.transform.SetParent(bookTransform);
        pagesRoot.transform.localPosition = Vector3.zero;
        pagesRoot.transform.localRotation = Quaternion.identity;

        var canvasGO = new GameObject("PageCanvas");
        canvasGO.transform.SetParent(pagesRoot.transform);
        canvasGO.transform.localPosition = new Vector3(0f, 0f, -depth / 2f - 0.02f);
        canvasGO.transform.localRotation = Quaternion.identity;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(900f, 400f);
        canvasRT.localScale = new Vector3(0.0005f, 0.0005f, 0.0005f);

        // Two-page spread: left + right (wider pages now)
        string[] pageNames = { "PageLeft", "PageRight" };
        float pageWidth = 380f;
        float[] xPositions = { -200f, 200f };

        for (int i = 0; i < 2; i++)
        {
            var pageGO = new GameObject(pageNames[i]);
            pageGO.transform.SetParent(canvasGO.transform);

            var rt = pageGO.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(xPositions[i], 0f);
            rt.sizeDelta = new Vector2(pageWidth, 380f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var bg = pageGO.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.95f, 0.93f, 0.88f, 0.95f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(pageGO.transform);

            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(12f, 12f);
            textRT.offsetMax = new Vector2(-12f, -12f);
            textRT.localScale = Vector3.one;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.12f, 0.10f, 0.08f);
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        // Page indicator (bottom center, hidden when single spread)
        var indicatorGO = new GameObject("PageIndicator");
        indicatorGO.transform.SetParent(canvasGO.transform);
        var indRT = indicatorGO.AddComponent<RectTransform>();
        indRT.anchoredPosition = new Vector2(0f, -190f);
        indRT.sizeDelta = new Vector2(100f, 30f);
        indRT.localScale = Vector3.one;
        var indTMP = indicatorGO.AddComponent<TextMeshProUGUI>();
        indTMP.text = "";
        indTMP.fontSize = 14f;
        indTMP.alignment = TextAlignmentOptions.Center;
        indTMP.color = new Color(0.4f, 0.38f, 0.35f);
        indicatorGO.SetActive(false);

        // Nav arrows (at page edges, hidden when not needed)
        var navLeftGO = new GameObject("NavLeft");
        navLeftGO.transform.SetParent(canvasGO.transform);
        var nlRT = navLeftGO.AddComponent<RectTransform>();
        nlRT.anchoredPosition = new Vector2(-420f, 0f);
        nlRT.sizeDelta = new Vector2(40f, 60f);
        nlRT.localScale = Vector3.one;
        var nlTMP = navLeftGO.AddComponent<TextMeshProUGUI>();
        nlTMP.text = "<";
        nlTMP.fontSize = 28f;
        nlTMP.alignment = TextAlignmentOptions.Center;
        nlTMP.color = new Color(0.3f, 0.28f, 0.25f);
        navLeftGO.SetActive(false);

        var navRightGO = new GameObject("NavRight");
        navRightGO.transform.SetParent(canvasGO.transform);
        var nrRT = navRightGO.AddComponent<RectTransform>();
        nrRT.anchoredPosition = new Vector2(420f, 0f);
        nrRT.sizeDelta = new Vector2(40f, 60f);
        nrRT.localScale = Vector3.one;
        var nrTMP = navRightGO.AddComponent<TextMeshProUGUI>();
        nrTMP.text = ">";
        nrTMP.fontSize = 28f;
        nrTMP.alignment = TextAlignmentOptions.Center;
        nrTMP.color = new Color(0.3f, 0.28f, 0.25f);
        navRightGO.SetActive(false);

        // Hidden item overlay (shown on correct spread)
        var hiddenGO = new GameObject("HiddenItemLabel");
        hiddenGO.transform.SetParent(canvasGO.transform);
        var hRT = hiddenGO.AddComponent<RectTransform>();
        hRT.anchoredPosition = new Vector2(200f, -140f);
        hRT.sizeDelta = new Vector2(300f, 50f);
        hRT.localScale = Vector3.one;
        var hTMP = hiddenGO.AddComponent<TextMeshProUGUI>();
        hTMP.text = "";
        hTMP.fontSize = 14f;
        hTMP.fontStyle = FontStyles.Italic;
        hTMP.alignment = TextAlignmentOptions.Center;
        hTMP.color = new Color(0.5f, 0.35f, 0.2f);
        hiddenGO.SetActive(false);

        pagesRoot.SetActive(false);
        return pagesRoot;
    }

    private static void BuildBookSpineTitle(Transform bookTransform, string title, float thickness, float height, float depth)
    {
        // Spine is the narrow front face (-Z direction). The visible face is thickness × height.
        // We use a high PPM (pixels-per-meter) canvas so text is crisp on the tiny spine.
        const float ppm = 2000f; // pixels per world-meter

        var spineCanvas = new GameObject("SpineTitle");
        spineCanvas.transform.SetParent(bookTransform);
        spineCanvas.transform.localPosition = new Vector3(0f, 0f, -depth / 2f - 0.0005f);
        spineCanvas.transform.localRotation = Quaternion.identity;

        var canvas = spineCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5;

        var canvasRT = spineCanvas.GetComponent<RectTransform>();
        // sizeDelta in canvas-pixels = world size × PPM
        canvasRT.sizeDelta = new Vector2(thickness * ppm, height * ppm);
        // localScale converts canvas-pixels back to world units
        float worldScale = 1f / ppm;
        canvasRT.localScale = new Vector3(worldScale, worldScale, worldScale);

        var textGO = new GameObject("Title");
        textGO.transform.SetParent(spineCanvas.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(2f, 4f);
        textRT.offsetMax = new Vector2(-2f, -4f);
        textRT.localScale = Vector3.one;
        // Rotate text 90° so it reads bottom-to-top along the height of the spine
        textRT.localRotation = Quaternion.Euler(0f, 0f, 90f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = thickness * ppm * 0.65f; // font fills ~65% of the spine width
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.92f, 0.85f);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = thickness * ppm * 0.65f;
    }

    // ════════════════════════════════════════════════════════════════════
    // Perfume Shelf (row 3)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildPerfumeShelf(GameObject bookcaseRoot, int perfumesLayer)
    {
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var parent = new GameObject("PerfumeBottles");
        parent.transform.SetParent(bookcaseRoot.transform);

        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float rowHeight = CaseHeight / ShelfCount;
        float row3ShelfTopY = 3f * rowHeight + ShelfThickness / 2f;
        float bottleHeight = 0.15f;
        float bottleWidth = 0.05f;
        float bottleDepth = 0.05f;

        // Center 3 perfumes on the top shelf
        float totalPerfumeWidth = PerfumeNames.Length * bottleWidth + (PerfumeNames.Length - 1) * 0.15f;
        float startX = -totalPerfumeWidth / 2f;
        float spacing = bottleWidth + 0.15f;

        for (int i = 0; i < PerfumeNames.Length; i++)
        {
            var def = ScriptableObject.CreateInstance<PerfumeDefinition>();
            def.perfumeName = PerfumeNames[i];
            def.description = PerfumeDescriptions[i];
            def.bottleColor = PerfumeBottleColors[i];
            def.sprayColor = PerfumeSprayColors[i];
            def.lightingColor = PerfumeLightColors[i];
            def.lightIntensity = PerfumeLightIntensities[i];
            def.moodParticleColor = PerfumeSprayColors[i];

            string defPath = $"{defDir}/Perfume_{i:D2}_{PerfumeNames[i].Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(def, defPath);

            float bottleX = startX + i * spacing;
            float bottleY = row3ShelfTopY + bottleHeight / 2f;

            var bottleGO = CreateBox($"Perfume_{i}", parent.transform,
                new Vector3(bottleX, bottleY, CaseCenterZ),
                new Vector3(bottleWidth, bottleHeight, bottleDepth),
                PerfumeBottleColors[i]);
            bottleGO.isStatic = false;
            bottleGO.layer = perfumesLayer;

            var bottle = bottleGO.AddComponent<PerfumeBottle>();
            bottleGO.AddComponent<InteractableHighlight>();

            // Spray particle system child
            var sprayGO = new GameObject("SprayParticles");
            sprayGO.transform.SetParent(bottleGO.transform);
            sprayGO.transform.localPosition = new Vector3(0f, bottleHeight / 2f + 0.02f, 0f);
            sprayGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var ps = sprayGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 1f;
            main.startSize = 0.02f;
            main.maxParticles = 50;
            main.startColor = PerfumeSprayColors[i];
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.01f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var bottleSO = new SerializedObject(bottle);
            bottleSO.FindProperty("definition").objectReferenceValue = def;
            bottleSO.FindProperty("sprayParticles").objectReferenceValue = ps;
            bottleSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log($"[BookcaseSceneBuilder] Created {PerfumeNames.Length} perfume bottles on row 3.");
    }

    // ════════════════════════════════════════════════════════════════════
    // Drawers (separate unit below bookcase, from Y=-DrawerHeight to Y=0)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildDrawers(GameObject bookcaseRoot, int drawersLayer)
    {
        var parent = new GameObject("Drawers");
        parent.transform.SetParent(bookcaseRoot.transform);

        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float drawerWidth = innerWidth / 2f - 0.02f;
        float drawerY = -DrawerHeight / 2f;
        float drawerDepth = CaseDepth - 0.04f;

        Color drawerColor = new Color(0.30f, 0.20f, 0.12f);
        Color frameBrown = new Color(0.25f, 0.15f, 0.08f);

        // Drawer unit frame (sits below the bookcase)
        CreateBox("DrawerFrame_Bottom", parent.transform,
            new Vector3(0f, -DrawerHeight, CaseCenterZ),
            new Vector3(innerWidth, ShelfThickness, CaseDepth),
            frameBrown);
        CreateBox("DrawerFrame_SideL", parent.transform,
            new Vector3(-CaseWidth / 2f, -DrawerHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, DrawerHeight, CaseDepth),
            frameBrown);
        CreateBox("DrawerFrame_SideR", parent.transform,
            new Vector3(CaseWidth / 2f, -DrawerHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, DrawerHeight, CaseDepth),
            frameBrown);
        CreateBox("DrawerFrame_Back", parent.transform,
            new Vector3(0f, -DrawerHeight / 2f, CaseCenterZ + CaseDepth / 2f - 0.01f),
            new Vector3(CaseWidth, DrawerHeight, 0.015f),
            new Color(0.20f, 0.12f, 0.06f));
        CreateBox("DrawerFrame_Divider", parent.transform,
            new Vector3(0f, -DrawerHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, DrawerHeight - 0.02f, CaseDepth - 0.02f),
            frameBrown);

        for (int d = 0; d < 2; d++)
        {
            float drawerX = (d == 0) ? -innerWidth / 4f : innerWidth / 4f;

            var drawerGO = CreateBox($"Drawer_{d}", parent.transform,
                new Vector3(drawerX, drawerY, CaseCenterZ),
                new Vector3(drawerWidth, DrawerHeight - 0.02f, drawerDepth),
                drawerColor);
            drawerGO.isStatic = false;
            drawerGO.layer = drawersLayer;

            var drawer = drawerGO.AddComponent<DrawerController>();
            drawerGO.AddComponent<InteractableHighlight>();

            var contentsRoot = new GameObject($"DrawerContents_{d}");
            contentsRoot.transform.SetParent(drawerGO.transform);
            contentsRoot.transform.localPosition = Vector3.zero;

            // Add PlacementSurface so ObjectGrabber can detect the open drawer as a
            // valid drop target. PlacementSurface auto-creates its own trigger collider.
            int surfacesLayer = EnsureLayer("Surfaces");
            var surface = contentsRoot.AddComponent<PlacementSurface>();
            var surfSO = new SerializedObject(surface);
            surfSO.FindProperty("localBounds").boundsValue = new Bounds(
                Vector3.zero,
                new Vector3(drawerWidth - 0.02f, 0.05f, drawerDepth - 0.02f));
            surfSO.FindProperty("normalAxis").enumValueIndex = (int)PlacementSurface.SurfaceAxis.Up;
            surfSO.FindProperty("surfaceLayerIndex").intValue = surfacesLayer;
            surfSO.ApplyModifiedPropertiesWithoutUndo();

            contentsRoot.SetActive(false);

            var drawerSO = new SerializedObject(drawer);
            drawerSO.FindProperty("slideDistance").floatValue = DrawerSlideDistance;
            drawerSO.FindProperty("contentsRoot").objectReferenceValue = contentsRoot;
            drawerSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("[BookcaseSceneBuilder] Created 2 drawers.");
    }

    // ════════════════════════════════════════════════════════════════════
    // Coffee Table (in room, in front of bookcase — scene-specific)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildCoffeeTable()
    {
        var parent = new GameObject("CoffeeTable");

        // Small table in front of the bookcase, at Z=0.8
        CreateBox("TableTop", parent.transform,
            new Vector3(0.8f, 0.4f, 0.8f), new Vector3(0.6f, 0.03f, 0.4f),
            new Color(0.35f, 0.22f, 0.12f));

        // Legs
        float legSize = 0.03f;
        float legHeight = 0.39f;
        Color legColor = new Color(0.30f, 0.18f, 0.10f);
        CreateBox("Leg_FL", parent.transform, new Vector3(0.52f, legHeight / 2f, 0.62f), new Vector3(legSize, legHeight, legSize), legColor);
        CreateBox("Leg_FR", parent.transform, new Vector3(1.08f, legHeight / 2f, 0.62f), new Vector3(legSize, legHeight, legSize), legColor);
        CreateBox("Leg_BL", parent.transform, new Vector3(0.52f, legHeight / 2f, 0.98f), new Vector3(legSize, legHeight, legSize), legColor);
        CreateBox("Leg_BR", parent.transform, new Vector3(1.08f, legHeight / 2f, 0.98f), new Vector3(legSize, legHeight, legSize), legColor);
    }

    // ════════════════════════════════════════════════════════════════════
    // Coffee Table Books (5 stacked flat on row 2, disheveled pile)
    // Laid flat on shelf, stacked on top of each other with slight offsets.
    // Click to send flat to coffee table.
    // ════════════════════════════════════════════════════════════════════

    private const int CoffeeBookRow = 2; // row 2 (third shelf from bottom)

    // Varying sizes — bigger spines, taller on shelf, varied rectangle proportions
    private static readonly float[] CoffeeBookThicknesses = { 0.05f, 0.07f, 0.045f, 0.08f, 0.055f };
    private static readonly float[] CoffeeBookHeightFracs = { 0.95f, 0.88f, 0.82f, 0.97f, 0.90f };
    private static readonly float[] CoffeeBookDepthFracs  = { 0.95f, 0.70f, 0.92f, 0.65f, 0.85f };

    private static void BuildCoffeeTableBooks(GameObject bookcaseRoot, int coffeeTableBooksLayer,
        Vector3 coffeeTableBase, Quaternion coffeeTableRotation)
    {
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var parent = new GameObject("CoffeeTableBooks");
        parent.transform.SetParent(bookcaseRoot.transform);

        float rowHeight = CaseHeight / ShelfCount;
        float shelfTopY = CoffeeBookRow * rowHeight + ShelfThickness / 2f;
        float availableHeight = rowHeight - ShelfThickness;

        // Deterministic seed for disheveled offsets
        Random.State savedState = Random.state;
        Random.InitState(99);

        // Stack books flat: Y cursor tracks the top of the pile
        float yCursor = shelfTopY;

        // Create ShelfBookStack GO with BookStack component for shelf pile collapse
        var shelfStackGO = new GameObject("ShelfBookStack");
        shelfStackGO.transform.SetParent(parent.transform);
        shelfStackGO.transform.localPosition = Vector3.zero;
        var shelfBookStack = shelfStackGO.AddComponent<BookStack>();
        var shelfStackSO = new SerializedObject(shelfBookStack);
        shelfStackSO.FindProperty("_stackBase").vector3Value = new Vector3(0f, shelfTopY, CaseCenterZ);
        shelfStackSO.FindProperty("_stackRotation").quaternionValue = Quaternion.identity;
        shelfStackSO.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < CoffeeBookTitles.Length; i++)
        {
            float thickness = CoffeeBookThicknesses[i];
            float bookHeight = availableHeight * CoffeeBookHeightFracs[i];
            float bookDepth = CaseDepth * CoffeeBookDepthFracs[i];

            var def = ScriptableObject.CreateInstance<CoffeeTableBookDefinition>();
            def.title = CoffeeBookTitles[i];
            def.description = CoffeeBookDescriptions[i];
            def.coverColor = CoffeeBookColors[i];
            def.itemID = $"coffeebook_{i}";
            def.size = new Vector2(bookDepth, bookHeight); // x=depth, y=height
            def.thickness = thickness;

            string defPath = $"{defDir}/CoffeeBook_{i:D2}_{CoffeeBookTitles[i].Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(def, defPath);

            // Flat on shelf: depth=X, thickness=Y, height=Z
            // Each book sits on top of the previous one
            float bookY = yCursor + thickness / 2f;

            // Slight random XZ offset for disheveled look
            float xOffset = Random.Range(-0.01f, 0.01f);
            float zOffset = Random.Range(-0.01f, 0.01f);
            float bookX = xOffset;
            float bookZ = CaseCenterZ + zOffset;

            // Random yaw and roll for natural crooked pile
            float yaw = Random.Range(-8f, 8f);
            float roll = Random.Range(-3f, 3f);

            var bookGO = CreateBox($"CoffeeBook_{i}", parent.transform,
                new Vector3(bookX, bookY, bookZ),
                new Vector3(bookDepth, thickness, bookHeight),
                CoffeeBookColors[i]);
            bookGO.transform.localRotation = Quaternion.Euler(roll, yaw, 0f);
            bookGO.isStatic = false;
            bookGO.layer = coffeeTableBooksLayer;

            yCursor += thickness;

            var coffeeBook = bookGO.AddComponent<CoffeeTableBook>();
            bookGO.AddComponent<InteractableHighlight>();

            // ReactableTag — always active and public (open shelves are public)
            var tag = bookGO.AddComponent<ReactableTag>();
            var tagSO = new SerializedObject(tag);
            tagSO.FindProperty("displayName").stringValue = def.title;
            var tagsProp = tagSO.FindProperty("tags");
            tagsProp.arraySize = 2;
            tagsProp.GetArrayElementAtIndex(0).stringValue = "coffee_book";
            tagsProp.GetArrayElementAtIndex(1).stringValue = def.title.ToLower().Replace(" ", "_");
            tagSO.FindProperty("isActive").boolValue = true;
            tagSO.FindProperty("isPrivate").boolValue = false;
            tagSO.ApplyModifiedPropertiesWithoutUndo();

            var cbSO = new SerializedObject(coffeeBook);
            cbSO.FindProperty("definition").objectReferenceValue = def;
            cbSO.FindProperty("coffeeTableBase").vector3Value = coffeeTableBase;
            cbSO.FindProperty("coffeeTableRotation").quaternionValue = coffeeTableRotation;
            cbSO.FindProperty("_shelfStack").objectReferenceValue = shelfBookStack;
            // First book starts on coffee table
            if (i == 0)
                cbSO.FindProperty("startsOnCoffeeTable").boolValue = true;
            cbSO.ApplyModifiedPropertiesWithoutUndo();

            // Spine title — use depth as the visible "spine" width when flat
            BuildBookSpineTitle(bookGO.transform, def.title, bookDepth, thickness, bookHeight);
        }

        Random.state = savedState;
        Debug.Log($"[BookcaseSceneBuilder] Created {CoffeeBookTitles.Length} coffee table books stacked flat on row {CoffeeBookRow}.");
    }

    // ════════════════════════════════════════════════════════════════════
    // Environment Mood Controller
    // ════════════════════════════════════════════════════════════════════

    private static void BuildEnvironmentMood(GameObject lightGO)
    {
        var moodGO = new GameObject("EnvironmentMoodController");
        var mood = moodGO.AddComponent<EnvironmentMoodController>();

        var moodSO = new SerializedObject(mood);
        moodSO.FindProperty("directionalLight").objectReferenceValue = lightGO.GetComponent<Light>();
        moodSO.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // Item Inspector
    // ════════════════════════════════════════════════════════════════════

    private static ItemInspector BuildItemInspector(GameObject camGO)
    {
        var inspectorGO = new GameObject("ItemInspector");
        var inspector = inspectorGO.AddComponent<ItemInspector>();

        var inspectAnchor = camGO.transform.Find("InspectAnchor");
        var browseCamera = camGO.GetComponent<BookcaseBrowseCamera>();

        // Description panel UI
        var uiCanvasGO = new GameObject("InspectUI_Canvas");
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 15;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var descPanel = new GameObject("DescriptionPanel");
        descPanel.transform.SetParent(uiCanvasGO.transform);

        var panelRT = descPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(450f, 100f);
        panelRT.anchoredPosition = new Vector2(0f, 80f);
        panelRT.localScale = Vector3.one;

        var panelBg = descPanel.AddComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.7f);

        // Name text
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(descPanel.transform);

        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.6f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(10f, 0f);
        nameRT.offsetMax = new Vector2(-10f, -5f);
        nameRT.localScale = Vector3.one;

        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Item Name";
        nameTMP.fontSize = 22f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.white;
        nameTMP.fontStyle = FontStyles.Bold;

        // Description text
        var descGO = new GameObject("DescriptionText");
        descGO.transform.SetParent(descPanel.transform);

        var descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.6f);
        descRT.offsetMin = new Vector2(10f, 5f);
        descRT.offsetMax = new Vector2(-10f, 0f);
        descRT.localScale = Vector3.one;

        var descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = "Description";
        descTMP.fontSize = 16f;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.color = new Color(0.8f, 0.8f, 0.8f);
        descTMP.enableWordWrapping = true;

        descPanel.SetActive(false);

        var inspSO = new SerializedObject(inspector);
        inspSO.FindProperty("inspectAnchor").objectReferenceValue = inspectAnchor;
        inspSO.FindProperty("browseCamera").objectReferenceValue = browseCamera;
        inspSO.FindProperty("descriptionPanel").objectReferenceValue = descPanel;
        inspSO.FindProperty("nameText").objectReferenceValue = nameTMP;
        inspSO.FindProperty("descriptionText").objectReferenceValue = descTMP;
        inspSO.ApplyModifiedPropertiesWithoutUndo();

        return inspector;
    }

    // ════════════════════════════════════════════════════════════════════
    // Interaction Manager + UI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildInteractionManager(GameObject camGO, ItemInspector inspector,
        int booksLayer, int drawersLayer, int perfumesLayer, int coffeeTableBooksLayer)
    {
        var managerGO = new GameObject("BookInteractionManager");
        var manager = managerGO.AddComponent<BookInteractionManager>();

        // Screen-space overlay canvas for title hint
        var uiCanvasGO = new GameObject("UI_Canvas");
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var hintPanel = new GameObject("TitleHintPanel");
        hintPanel.transform.SetParent(uiCanvasGO.transform);

        var panelRT = hintPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(400f, 50f);
        panelRT.anchoredPosition = new Vector2(0f, 30f);
        panelRT.localScale = Vector3.one;

        var bg = hintPanel.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        var textGO = new GameObject("TitleText");
        textGO.transform.SetParent(hintPanel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Book Title";
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Italic;

        hintPanel.SetActive(false);

        var readingAnchor = camGO.transform.Find("ReadingAnchor");
        var cam = camGO.GetComponent<UnityEngine.Camera>();
        var browseCamera = camGO.GetComponent<BookcaseBrowseCamera>();

        // Find mood controller
        var moodController = Object.FindFirstObjectByType<EnvironmentMoodController>();

        var so = new SerializedObject(manager);
        so.FindProperty("readingAnchor").objectReferenceValue = readingAnchor;
        so.FindProperty("mainCamera").objectReferenceValue = cam;
        so.FindProperty("browseCamera").objectReferenceValue = browseCamera;
        so.FindProperty("itemInspector").objectReferenceValue = inspector;
        so.FindProperty("moodController").objectReferenceValue = moodController;
        so.FindProperty("titleHintPanel").objectReferenceValue = hintPanel;
        so.FindProperty("titleHintText").objectReferenceValue = tmp;
        so.FindProperty("booksLayerMask").intValue = 1 << booksLayer;
        so.FindProperty("drawersLayerMask").intValue = 1 << drawersLayer;
        so.FindProperty("perfumesLayerMask").intValue = 1 << perfumesLayer;
        so.FindProperty("coffeeTableBooksLayerMask").intValue = 1 << coffeeTableBooksLayer;
        so.FindProperty("maxRayDistance").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();
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
        go.transform.localPosition = position;
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

    public static int EnsureLayer(string layerName)
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
                Debug.Log($"[BookcaseSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[BookcaseSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
