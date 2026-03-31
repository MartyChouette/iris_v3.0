using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent (DDoL) cursor manager that switches the hardware cursor
/// based on what the mouse is hovering over. Auto-spawns on scene load.
///
/// Cursor contexts (highest priority first):
///   1. Watering   — hovering a WaterablePlant
///   2. Interact   — hovering an InteractableHighlight, PlaceableObject, or flower tag
///   3. Default    — OS cursor (null)
///
/// Loads "Cursors/pinch" from Resources for the interact cursor.
/// Generates a procedural watering pail texture at runtime.
/// </summary>
public class GlobalCursorManager : MonoBehaviour
{
    public static GlobalCursorManager Instance { get; private set; }

    // ── Cursor textures ──
    private Texture2D _interactCursor;   // pinch.png — for general interactables
    private Texture2D _wateringCursor;   // procedural watering pail
    private Vector2 _interactHotSpot;
    private Vector2 _wateringHotSpot;

    // ── State ──
    private CursorType _currentType = CursorType.Default;
    private Camera _cachedCamera;

    private enum CursorType { Default, Interact, Watering }

    // ── Layers for raycast ──
    // Raycast against everything except UI (layer 5) and Ignore Raycast (layer 2)
    private const int RaycastMask = ~((1 << 5) | (1 << 2));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;

        var go = new GameObject("GlobalCursorManager");
        go.AddComponent<GlobalCursorManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCursorTextures();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Restore OS cursor on destroy
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Instance = null;
        }

        if (_wateringCursor != null)
            Destroy(_wateringCursor);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cachedCamera = null; // force re-cache
    }

    private void LoadCursorTextures()
    {
        // Load pinch cursor from Resources
        _interactCursor = Resources.Load<Texture2D>("Cursors/pinch");
        if (_interactCursor != null)
        {
            // Hotspot at top-left (matches original CursorContext in flower scenes)
            _interactHotSpot = Vector2.zero;
        }
        else
        {
            Debug.LogWarning("[GlobalCursorManager] Cursors/pinch not found in Resources.");
        }

        // Generate watering pail cursor procedurally
        _wateringCursor = GenerateWateringCursor(32, 32);
        _wateringHotSpot = new Vector2(6f, 2f); // tip of spout
    }

    private void Update()
    {
        if (_cachedCamera == null)
            _cachedCamera = Camera.main;
        if (_cachedCamera == null)
        {
            ApplyCursor(CursorType.Default);
            return;
        }

        // Don't change cursor when grabbing an object
        if (ObjectGrabber.IsHoldingObject)
        {
            ApplyCursor(CursorType.Default);
            return;
        }

        // Raycast from cursor position
        Vector2 cursorPos = IrisInput.CursorPosition;
        Ray ray = _cachedCamera.ScreenPointToRay(cursorPos);

        CursorType desired = CursorType.Default;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, RaycastMask))
        {
            var go = hit.collider.gameObject;

            // Priority 1: Watering — hovering a WaterablePlant
            if (go.GetComponent<WaterablePlant>() != null
                || go.GetComponentInParent<WaterablePlant>() != null)
            {
                desired = CursorType.Watering;
            }
            // Priority 2: Interact — hovering an interactable
            else if (go.GetComponent<InteractableHighlight>() != null
                  || go.GetComponentInParent<InteractableHighlight>() != null
                  || go.GetComponent<PlaceableObject>() != null
                  || go.GetComponentInParent<PlaceableObject>() != null
                  || go.GetComponent<RecordSlot>() != null
                  || go.GetComponentInParent<RecordSlot>() != null
                  || go.GetComponent<CleanableSurface>() != null
                  || HasFlowerTag(go))
            {
                desired = CursorType.Interact;
            }
        }

        ApplyCursor(desired);
    }

    private static bool HasFlowerTag(GameObject go)
    {
        return go.CompareTag("Petal")
            || go.CompareTag("Leaf")
            || go.CompareTag("Crown");
    }

    private void ApplyCursor(CursorType type)
    {
        if (type == _currentType) return;
        _currentType = type;

        switch (type)
        {
            case CursorType.Watering:
                Cursor.SetCursor(_wateringCursor, _wateringHotSpot, CursorMode.Auto);
                break;
            case CursorType.Interact:
                Cursor.SetCursor(_interactCursor, _interactHotSpot, CursorMode.Auto);
                break;
            default:
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Procedural watering pail cursor (32x32)
    // ──────────────────────────────────────────────────────────────

    private static Texture2D GenerateWateringCursor(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // Clear to transparent
        var pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        var body = new Color32(110, 160, 200, 255);   // steel blue
        var dark = new Color32(70, 110, 150, 255);     // darker steel
        var handle = new Color32(90, 140, 180, 255);   // handle color
        var water = new Color32(100, 180, 230, 180);   // water drops
        var rim = new Color32(130, 180, 210, 255);     // rim highlight

        // Pail body (bucket shape) — rows 10-24, cols 8-24
        for (int y = 10; y <= 24; y++)
        {
            // Taper: wider at top, narrower at bottom
            float t = (y - 10f) / 14f;
            int left = (int)Mathf.Lerp(10, 12, t);
            int right = (int)Mathf.Lerp(24, 22, t);
            for (int x = left; x <= right; x++)
            {
                pixels[y * w + x] = (y == 24 || x == left || x == right) ? dark : body;
            }
        }

        // Rim at top — row 24-25
        for (int x = 9; x <= 25; x++)
        {
            pixels[24 * w + x] = rim;
            pixels[25 * w + x] = rim;
        }

        // Handle (arc above pail) — rows 25-30
        for (int y = 26; y <= 30; y++)
        {
            int offset = y - 25;
            int xl = 13 - offset;
            int xr = 21 + offset;
            if (xl >= 0 && xl < w) pixels[y * w + xl] = handle;
            if (xl + 1 >= 0 && xl + 1 < w) pixels[y * w + xl + 1] = handle;
            if (xr >= 0 && xr < w) pixels[y * w + xr] = handle;
            if (xr - 1 >= 0 && xr - 1 < w) pixels[y * w + xr - 1] = handle;
        }
        // Top of handle
        for (int x = 8; x <= 26; x++)
            if (x >= 0 && x < w) pixels[30 * w + x] = handle;

        // Spout (left side, pointing down-left) — rows 16-22
        for (int y = 16; y <= 22; y++)
        {
            int sx = 10 - (y - 16);
            if (sx >= 0 && sx < w && y < h)
            {
                pixels[y * w + sx] = dark;
                if (sx + 1 < w) pixels[y * w + sx + 1] = body;
            }
        }

        // Water drops from spout
        if (4 < w && 6 < h) pixels[6 * w + 4] = water;
        if (3 < w && 4 < h) pixels[4 * w + 3] = water;
        if (5 < w && 2 < h) pixels[2 * w + 5] = water;

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
