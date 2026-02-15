using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the bookcase_browsing scene with
/// a room, bookcase full of books, drawers with trinkets, perfume bottles,
/// coffee table books, item inspector, and first-person browse camera.
/// Menu: Window > Iris > Build Bookcase Browsing Scene
/// </summary>
public static class BookcaseSceneBuilder
{
    private const string BooksLayerName = "Books";
    private const string DrawersLayerName = "Drawers";
    private const string PerfumesLayerName = "Perfumes";
    private const string TrinketsLayerName = "Trinkets";
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
    private const int MinBooksPerRow = 8;
    private const int MaxBooksPerRow = 14;
    private const float MinBookThickness = 0.02f;
    private const float MaxBookThickness = 0.06f;
    private const float MinGap = 0.001f;
    private const float MaxGap = 0.005f;

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

    // Sample book data
    private static readonly string[] BookTitles =
    {
        "Roots in Darkness",
        "The Quiet Garden",
        "Letters to Soil",
        "Pressing Petals",
        "On Wilting",
        "Stem Theory",
        "A Gentle Thorn",
        "Bloom & Fade",
        "The Potting Shed",
        "Chlorophyll Dreams",
        "Under the Canopy",
        "Seed Catalog No. 7",
        "Still Life with Vase",
        "Water & Light",
        "The Pruner's Almanac",
    };

    private static readonly string[] BookAuthors =
    {
        "Eleanor Moss",
        "H. Fernwood",
        "Clara Rootley",
        "Jasper Thorn",
        "Wren Dewdrop",
        "P.L. Greenshaw",
        "Ivy Ashcroft",
        "Basil Harrow",
        "Rose Underhill",
        "Silas Compost",
        "Hazel Canopy",
        "Dahlia Greer",
        "Olive Stillman",
        "Linden Clearwell",
        "Sage Draper",
    };

    private static readonly string[][] BookPages =
    {
        new[] {
            "The roots do not ask permission. They push through clay and stone, searching for what they need in total darkness.",
            "I have often wondered if the flower knows it is beautiful, or if beauty is simply a side effect of reaching toward light.",
            "When the last petal falls, the stem stands bare — not empty, but unburdened."
        },
        new[] {
            "A garden is never quiet. Listen closely: the earthworms turning, the slow exhale of opening buds, the patient drip.",
            "She planted marigolds along the fence not for their color, but because they reminded her of someone she'd rather not forget.",
            "By September the garden had its own ideas. She learned to stop arguing with it."
        },
        new[] {
            "Dear Soil, I am writing to apologize. I have taken so much from you and returned so little.",
            "The compost heap is a love letter written in eggshells and coffee grounds. Decomposition as devotion.",
            "Perhaps we are all just soil, waiting patiently for something to take root."
        },
        new[] {
            "To press a flower is to stop time — or at least, to press pause. The color fades, but the shape remembers.",
            "Page 47 of her journal: a flattened daisy, brown at the edges, still holding its circular argument.",
            "Some flowers are better preserved in memory than in books. But we press them anyway."
        },
        new[] {
            "Wilting is not failure. It is the flower's way of saying: I have given everything I had to give.",
            "The drooping head of a sunflower in October carries more dignity than any spring bloom.",
            "We fear wilting because we see ourselves in it. But the plant does not fear. It simply returns."
        },
        new[] {
            "Chapter 1: The stem is not merely a support structure. It is a highway, a messenger, a spine.",
            "Consider the hollow stem of the dandelion — empty inside, yet strong enough to hold a wish.",
            "All architecture aspires to the condition of the stem: vertical, purposeful, alive."
        },
        new[] {
            "The thorn exists not out of cruelty but out of memory. Every wound the rose has received is recorded there.",
            "To hold a thorned stem gently requires practice. Most people never bother to learn.",
            "Gentleness, she decided, was not the absence of thorns but the willingness to reach past them."
        },
        new[] {
            "The bloom arrives without announcement. One morning the bud is closed; the next, it has made its decision.",
            "Fading is the bloom's longest act. We celebrate the opening, but the slow close deserves its own ovation.",
            "Between bloom and fade there is a single perfect day. The flower does not know which one it is."
        },
        new[] {
            "The potting shed smelled of damp earth and turpentine. Tools hung on nails, each one a different kind of patience.",
            "He repotted the fern for the third time that year. It kept outgrowing its container. He respected that.",
            "In the corner of the shed, a spider had built a web between two terra cotta pots. He left it there."
        },
        new[] {
            "Chlorophyll is the green dream of sunlight made solid. Every leaf is a sleeping solar panel.",
            "The plant does not choose to photosynthesize. It simply faces the light and lets chemistry do the rest.",
            "At night, the chloroplasts rest. Even the most dedicated workers need darkness to make sense of the day."
        },
        new[] {
            "The canopy is a ceiling made of arguments — each branch competing for its share of sky.",
            "Under the canopy, the light comes in fragments. The forest floor learns to make do with scraps.",
            "She sat under the oak for an hour. When she stood, she understood something she could not put into words."
        },
        new[] {
            "Item 7A: Moonflower seeds. Plant in evening. Do not expect results before midnight.",
            "Item 12C: Forget-me-nots. Self-explanatory. Plant near things you wish to remember.",
            "Item 23F: Ghost orchid. Availability uncertain. May not exist. Order anyway."
        },
        new[] {
            "The vase held three stems: one straight, one curved, one broken. Together they made a sentence.",
            "Still life painters know: the flower in the painting will outlast the flower on the table.",
            "She arranged the flowers not for beauty but for conversation. The lily had something to say to the rose."
        },
        new[] {
            "Water finds the root the way memory finds the dreamer — without effort, by gravity, in the dark.",
            "Too much light bleaches. Too little starves. The plant's whole life is a negotiation between extremes.",
            "The best gardeners understand that water and light are not gifts. They are partnerships."
        },
        new[] {
            "January: Sharpen the shears. A clean cut heals faster than a ragged one. This applies to most things.",
            "June: The roses will try to take over. Let them think they're winning, then prune on the solstice.",
            "October: Let the dead heads stand. The goldfinches need the seeds more than you need tidiness."
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

    // Trinket data
    private static readonly string[] TrinketNames =
    {
        "Ceramic Cat", "Brass Key", "Glass Marble", "Tiny Cactus"
    };
    private static readonly string[] TrinketDescriptions =
    {
        "A small painted cat, one ear chipped. It looks smug.",
        "An old brass key that doesn't fit any lock you own.",
        "Swirls of blue and green trapped in a perfect sphere.",
        "A fake cactus, barely an inch tall. Perpetually cheerful."
    };
    private static readonly Color[] TrinketColors =
    {
        new Color(0.9f, 0.85f, 0.75f),
        new Color(0.75f, 0.65f, 0.3f),
        new Color(0.3f, 0.5f, 0.8f),
        new Color(0.4f, 0.7f, 0.3f),
    };

    // Coffee table book data
    private static readonly string[] CoffeeBookTitles = { "Arrangements", "Petal Studies" };
    private static readonly string[] CoffeeBookDescriptions =
    {
        "A heavy book of ikebana arrangements, each page a meditation.",
        "Macro photographs of petals — textures you've never noticed."
    };
    private static readonly Color[] CoffeeBookColors =
    {
        new Color(0.15f, 0.25f, 0.35f),
        new Color(0.5f, 0.2f, 0.3f),
    };

    [MenuItem("Window/Iris/Build Bookcase Browsing Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int booksLayer = EnsureLayer(BooksLayerName);
        int drawersLayer = EnsureLayer(DrawersLayerName);
        int perfumesLayer = EnsureLayer(PerfumesLayerName);
        int trinketsLayer = EnsureLayer(TrinketsLayerName);
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

        // ── 4. Bookcase unit (frame, books, perfumes, trinkets, drawers, coffee books)
        var bookcaseRoot = BuildBookcaseUnit(booksLayer, drawersLayer,
            perfumesLayer, trinketsLayer, coffeeTableBooksLayer);
        bookcaseRoot.transform.position = new Vector3(0f, 0f, 1.8f);

        // ── 5. Coffee table (scene-specific room furniture) ──────────
        BuildCoffeeTable();

        // ── 6. Wire coffee table book targets ────────────────────────
        var coffeeBooks = bookcaseRoot.GetComponentsInChildren<CoffeeTableBook>();
        float coffeeTableTopY = 0.415f;
        for (int i = 0; i < coffeeBooks.Length; i++)
        {
            float tableX = 0.65f + i * 0.25f;
            var cbSO = new SerializedObject(coffeeBooks[i]);
            cbSO.FindProperty("coffeeTablePosition").vector3Value =
                new Vector3(tableX, coffeeTableTopY, 0.8f);
            cbSO.FindProperty("coffeeTableRotation").quaternionValue =
                Quaternion.Euler(0f, 15f * (i + 1), 0f);
            cbSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 7. Environment mood controller ───────────────────────────
        BuildEnvironmentMood(lightGO);

        // ── 8. Item Inspector ────────────────────────────────────────
        var inspector = BuildItemInspector(camGO);

        // ── 9. BookInteractionManager + UI ───────────────────────────
        BuildInteractionManager(camGO, inspector, booksLayer, drawersLayer,
            perfumesLayer, trinketsLayer, coffeeTableBooksLayer);

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
    /// frame, books, perfume bottles, trinket display slots, drawers with trinkets,
    /// and coffee table books. Caller positions/rotates the returned root and wires
    /// coffee table book targets to scene-specific furniture.
    /// </summary>
    public static GameObject BuildBookcaseUnit(int booksLayer, int drawersLayer,
        int perfumesLayer, int trinketsLayer, int coffeeTableBooksLayer)
    {
        var bookcaseRoot = new GameObject("Bookcase");

        BuildBookcaseFrame(bookcaseRoot);
        BuildBooks(bookcaseRoot, booksLayer);
        BuildPerfumeShelf(bookcaseRoot, perfumesLayer);
        BuildTrinketDisplaySlots(bookcaseRoot, trinketsLayer);
        BuildDrawers(bookcaseRoot, drawersLayer, trinketsLayer);
        BuildCoffeeTableBooks(bookcaseRoot, coffeeTableBooksLayer);

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

        for (int i = 0; i <= ShelfCount - 1; i++)
        {
            float shelfY = caseBottomY + i * rowHeight;
            CreateBox($"Shelf_{i}", frame.transform,
                new Vector3(caseX, shelfY, CaseCenterZ),
                new Vector3(innerWidth, ShelfThickness, CaseDepth),
                darkBrown);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Books (all 4 rows, with reserved space for new items on some rows)
    // ════════════════════════════════════════════════════════════════════

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

        int bookIndex = 0;

        // All 4 usable rows — some rows reserve right-side space for new items
        for (int row = 0; row < ShelfCount - 1; row++)
        {
            float shelfTopY = row * rowHeight + ShelfThickness / 2f;
            float availableHeight = rowHeight - ShelfThickness;

            // Reserve right-side space on specific rows for new items:
            //   Row 0: reserve right third for trinket display slots
            //   Row 1: reserve right third for perfume bottles
            //   Row 2: reserve right quarter for coffee table books (flat)
            //   Row 3: full width for books
            float maxX;
            if (row == 0 || row == 1)
                maxX = innerWidth / 2f - innerWidth / 3f;
            else if (row == 2)
                maxX = innerWidth / 2f - innerWidth / 4f;
            else
                maxX = innerWidth / 2f - 0.01f;

            int bookCount = Random.Range(MinBooksPerRow, MaxBooksPerRow + 1);
            float xCursor = caseLeftX + 0.01f;

            for (int b = 0; b < bookCount; b++)
            {
                float thickness = Random.Range(MinBookThickness, MaxBookThickness);
                float heightFrac = Random.Range(0.70f, 0.95f);
                float bookHeight = availableHeight * heightFrac;
                float depthFrac = Random.Range(0.70f, 0.95f);
                float bookDepth = CaseDepth * depthFrac;
                Color color = SpineColors[Random.Range(0, SpineColors.Length)];

                if (xCursor + thickness > maxX)
                    break;

                int dataIndex = bookIndex % BookTitles.Length;
                string title = BookTitles[dataIndex];
                string author = BookAuthors[dataIndex];
                string[] pages = BookPages[dataIndex];

                var def = ScriptableObject.CreateInstance<BookDefinition>();
                def.title = title;
                def.author = author;
                def.pageTexts = pages;
                def.spineColor = color;
                def.heightScale = heightFrac;
                def.thicknessScale = thickness;

                string defPath = $"{defDir}/Book_{bookIndex:D3}_{title.Replace(" ", "_")}.asset";
                AssetDatabase.CreateAsset(def, defPath);

                float bookX = xCursor + thickness / 2f;
                float bookY = shelfTopY + bookHeight / 2f;
                float bookZ = CaseCenterZ - (CaseDepth - bookDepth) / 2f * 0.5f;

                var bookGO = CreateBox($"Book_{bookIndex}", booksParent.transform,
                    new Vector3(bookX, bookY, bookZ),
                    new Vector3(thickness, bookHeight, bookDepth),
                    color);
                bookGO.isStatic = false;
                bookGO.layer = booksLayer;

                var volume = bookGO.AddComponent<BookVolume>();
                var pagesRoot = BuildBookPages(bookGO.transform, thickness, bookHeight, bookDepth);

                var so = new SerializedObject(volume);
                so.FindProperty("definition").objectReferenceValue = def;
                so.FindProperty("pagesRoot").objectReferenceValue = pagesRoot;

                var pageLabels = pagesRoot.GetComponentsInChildren<TMP_Text>(true);
                var labelsProperty = so.FindProperty("pageLabels");
                labelsProperty.arraySize = Mathf.Min(pageLabels.Length, 3);
                for (int p = 0; p < labelsProperty.arraySize; p++)
                    labelsProperty.GetArrayElementAtIndex(p).objectReferenceValue = pageLabels[p];

                so.ApplyModifiedPropertiesWithoutUndo();

                bookGO.AddComponent<InteractableHighlight>();

                // Title text on the spine (front face, visible on shelf)
                BuildBookSpineTitle(bookGO.transform, title, thickness, bookHeight, bookDepth);

                xCursor += thickness + Random.Range(MinGap, MaxGap);
                bookIndex++;
            }
        }

        Debug.Log($"[BookcaseSceneBuilder] Created {bookIndex} books across all 4 rows.");
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

        string[] pageNames = { "PageLeft", "PageCenter", "PageRight" };
        float pageWidth = 280f;
        float[] xPositions = { -300f, 0f, 300f };

        for (int i = 0; i < 3; i++)
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
            textRT.offsetMin = new Vector2(8f, 8f);
            textRT.offsetMax = new Vector2(-8f, -8f);
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

        pagesRoot.SetActive(false);
        return pagesRoot;
    }

    private static void BuildBookSpineTitle(Transform bookTransform, string title, float thickness, float height, float depth)
    {
        // Spine title on the front face (-Z), rotated 90° so text reads bottom-to-top
        var spineCanvas = new GameObject("SpineTitle");
        spineCanvas.transform.SetParent(bookTransform);
        spineCanvas.transform.localPosition = new Vector3(0f, 0f, -depth / 2f - 0.0005f);
        spineCanvas.transform.localRotation = Quaternion.identity;

        var canvas = spineCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRT = spineCanvas.GetComponent<RectTransform>();
        // Canvas sized to the spine face (width = book thickness, height = book height)
        canvasRT.sizeDelta = new Vector2(400f, 200f);
        float canvasScale = height / 400f * 0.9f; // fit to spine height with padding
        canvasRT.localScale = new Vector3(canvasScale, canvasScale, canvasScale);

        var textGO = new GameObject("Title");
        textGO.transform.SetParent(spineCanvas.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(4f, 4f);
        textRT.offsetMax = new Vector2(-4f, -4f);
        textRT.localScale = Vector3.one;
        textRT.localRotation = Quaternion.Euler(0f, 0f, 90f); // Rotate text to read bottom-to-top

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = 32f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.92f, 0.85f); // Light text on colored spine
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
    }

    // ════════════════════════════════════════════════════════════════════
    // Perfume Shelf (row 1, right side — next to books)
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
        float row1ShelfTopY = 1f * rowHeight + ShelfThickness / 2f;
        float bottleHeight = 0.15f;
        float bottleWidth = 0.05f;
        float bottleDepth = 0.05f;

        // Place on the right third of row 1, next to where books end
        float rightZoneStart = innerWidth / 2f - innerWidth / 3f + 0.04f;
        float spacing = 0.2f;

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

            float bottleX = rightZoneStart + i * spacing;
            float bottleY = row1ShelfTopY + bottleHeight / 2f;

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

        Debug.Log($"[BookcaseSceneBuilder] Created {PerfumeNames.Length} perfume bottles on row 1.");
    }

    // ════════════════════════════════════════════════════════════════════
    // Trinket Display Slots (row 0, right side — next to books)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildTrinketDisplaySlots(GameObject bookcaseRoot, int trinketsLayer)
    {
        var parent = new GameObject("TrinketDisplaySlots");
        parent.transform.SetParent(bookcaseRoot.transform);

        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float rowHeight = CaseHeight / ShelfCount;
        float row0ShelfTopY = 0f * rowHeight + ShelfThickness / 2f;

        // 4 display positions in the right third of row 0
        float rightZoneStart = innerWidth / 2f - innerWidth / 3f + 0.02f;
        float spacing = (innerWidth / 3f - 0.04f) / 4f;

        for (int i = 0; i < 4; i++)
        {
            float slotX = rightZoneStart + i * spacing + spacing / 2f;
            float slotY = row0ShelfTopY + 0.01f;

            var slotGO = CreateBox($"DisplaySlot_{i}", parent.transform,
                new Vector3(slotX, slotY, CaseCenterZ),
                new Vector3(0.06f, 0.005f, 0.06f),
                new Color(0.35f, 0.25f, 0.15f, 0.5f));
            slotGO.isStatic = true;
        }

        Debug.Log("[BookcaseSceneBuilder] Created 4 trinket display slots on row 0 (right side).");
    }

    // ════════════════════════════════════════════════════════════════════
    // Drawers (separate unit below bookcase, from Y=-DrawerHeight to Y=0)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildDrawers(GameObject bookcaseRoot, int drawersLayer, int trinketsLayer)
    {
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var parent = new GameObject("Drawers");
        parent.transform.SetParent(bookcaseRoot.transform);

        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float drawerWidth = innerWidth / 2f - 0.02f;
        float drawerY = -DrawerHeight / 2f;  // centered in the drawer section
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

        // Row 0 display slot positions for trinket targets (right third)
        float rowHeight = CaseHeight / ShelfCount;
        float displayY = 0f * rowHeight + ShelfThickness / 2f + 0.04f;
        float displayRightStart = innerWidth / 2f - innerWidth / 3f + 0.02f;
        float displaySpacing = (innerWidth / 3f - 0.04f) / 4f;
        float displayStartX = displayRightStart + displaySpacing / 2f;

        int trinketIndex = 0;

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

            // Trinket contents inside this drawer
            var contentsRoot = new GameObject($"DrawerContents_{d}");
            contentsRoot.transform.SetParent(drawerGO.transform);
            contentsRoot.transform.localPosition = Vector3.zero;

            // 2 trinkets per drawer
            int trinketsInDrawer = 2;
            for (int t = 0; t < trinketsInDrawer && trinketIndex < TrinketNames.Length; t++)
            {
                var trinketDef = ScriptableObject.CreateInstance<TrinketDefinition>();
                trinketDef.trinketName = TrinketNames[trinketIndex];
                trinketDef.description = TrinketDescriptions[trinketIndex];
                trinketDef.color = TrinketColors[trinketIndex];
                trinketDef.itemID = $"trinket_{trinketIndex}";
                trinketDef.startsInDrawer = true;

                string trinketDefPath = $"{defDir}/Trinket_{trinketIndex:D2}_{TrinketNames[trinketIndex].Replace(" ", "_")}.asset";
                AssetDatabase.CreateAsset(trinketDef, trinketDefPath);

                float localX = (t == 0) ? -0.08f : 0.08f;
                var trinketGO = CreateBox($"Trinket_{trinketIndex}", contentsRoot.transform,
                    new Vector3(drawerX + localX, drawerY + 0.04f, CaseCenterZ),
                    new Vector3(0.06f, 0.06f, 0.06f),
                    TrinketColors[trinketIndex]);
                trinketGO.isStatic = false;
                trinketGO.layer = trinketsLayer;

                var trinketVol = trinketGO.AddComponent<TrinketVolume>();
                trinketGO.AddComponent<InteractableHighlight>();

                // Display slot position for this trinket
                float displayX = displayStartX + trinketIndex * displaySpacing;

                var trinketSO = new SerializedObject(trinketVol);
                trinketSO.FindProperty("definition").objectReferenceValue = trinketDef;
                trinketSO.FindProperty("displayPosition").vector3Value =
                    new Vector3(displayX, displayY, CaseCenterZ);
                trinketSO.FindProperty("displayRotation").quaternionValue = Quaternion.identity;
                trinketSO.ApplyModifiedPropertiesWithoutUndo();

                trinketIndex++;
            }

            contentsRoot.SetActive(false);

            var drawerSO = new SerializedObject(drawer);
            drawerSO.FindProperty("slideDistance").floatValue = DrawerSlideDistance;
            drawerSO.FindProperty("contentsRoot").objectReferenceValue = contentsRoot;
            drawerSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log($"[BookcaseSceneBuilder] Created 2 drawers with {trinketIndex} trinkets.");
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
    // Coffee Table Books (on bookcase row 2, right side)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildCoffeeTableBooks(GameObject bookcaseRoot, int coffeeTableBooksLayer)
    {
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var parent = new GameObject("CoffeeTableBooks");
        parent.transform.SetParent(bookcaseRoot.transform);

        // Row 2 — right side of shelf
        float rowHeight = CaseHeight / ShelfCount;
        float row2ShelfTopY = 2f * rowHeight + ShelfThickness / 2f;
        float innerWidth = CaseWidth - SidePanelThickness * 2f;

        for (int i = 0; i < CoffeeBookTitles.Length; i++)
        {
            var def = ScriptableObject.CreateInstance<CoffeeTableBookDefinition>();
            def.title = CoffeeBookTitles[i];
            def.description = CoffeeBookDescriptions[i];
            def.coverColor = CoffeeBookColors[i];
            def.itemID = $"coffeebook_{i}";
            def.size = new Vector2(0.20f, 0.16f);
            def.thickness = 0.025f;

            string defPath = $"{defDir}/CoffeeBook_{i:D2}_{CoffeeBookTitles[i].Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(def, defPath);

            // Bookcase position: flat (lying down) on right quarter of row 2
            float rightZoneStart = innerWidth / 2f - innerWidth / 4f + 0.05f;
            float bookcaseX = rightZoneStart + i * 0.22f;
            float bookcaseY = row2ShelfTopY + def.thickness / 2f;

            var bookGO = CreateBox($"CoffeeBook_{i}", parent.transform,
                new Vector3(bookcaseX, bookcaseY, CaseCenterZ),
                new Vector3(def.size.x, def.thickness, def.size.y),
                CoffeeBookColors[i]);
            bookGO.isStatic = false;
            bookGO.layer = coffeeTableBooksLayer;

            var coffeeBook = bookGO.AddComponent<CoffeeTableBook>();
            bookGO.AddComponent<InteractableHighlight>();

            var cbSO = new SerializedObject(coffeeBook);
            cbSO.FindProperty("definition").objectReferenceValue = def;
            cbSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log($"[BookcaseSceneBuilder] Created {CoffeeBookTitles.Length} coffee table books.");
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
        int booksLayer, int drawersLayer, int perfumesLayer, int trinketsLayer, int coffeeTableBooksLayer)
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
        so.FindProperty("trinketsLayerMask").intValue = 1 << trinketsLayer;
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
