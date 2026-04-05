using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows a fading eye icon (open/closed) next to items when they are placed
/// or when their visibility to the date changes (e.g. cubby door closes).
/// Icons are world-space sprites that always face the camera and fade out
/// over a few seconds. Rendered on top of everything so they show through doors.
/// </summary>
public class VisibilityEyeIndicator : MonoBehaviour
{
    public static VisibilityEyeIndicator Instance { get; private set; }

    [Header("Timing")]
    [Tooltip("Seconds before the icon starts fading.")]
    [SerializeField] private float _holdDuration = 1.0f;

    [Tooltip("Seconds for the fade-out.")]
    [SerializeField] private float _fadeDuration = 1.5f;

    [Header("Layout")]
    [Tooltip("Offset from the item's center (world units).")]
    [SerializeField] private Vector3 _offset = new Vector3(0.15f, 0.15f, 0f);

    [Tooltip("World-space size of the icon.")]
    [SerializeField] private float _iconSize = 0.08f;

    // ── Runtime ──
    private Texture2D _openEyeTex;
    private Texture2D _closedEyeTex;
    private Sprite _openEyeSprite;
    private Sprite _closedEyeSprite;
    private Camera _cam;

    private readonly List<IconInstance> _active = new();
    private readonly Queue<IconInstance> _pool = new();

    private struct IconInstance
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Transform target;
        public float timer;
        public float totalLife;
    }

    /// <summary>Auto-spawn if not present in scene.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("VisibilityEyeIndicator");
        go.AddComponent<VisibilityEyeIndicator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _openEyeTex = GenOpenEye();
        _closedEyeTex = GenClosedEye();
        _openEyeSprite = Sprite.Create(_openEyeTex,
            new Rect(0, 0, _openEyeTex.width, _openEyeTex.height),
            new Vector2(0.5f, 0.5f), 128f);
        _closedEyeSprite = Sprite.Create(_closedEyeTex,
            new Rect(0, 0, _closedEyeTex.width, _closedEyeTex.height),
            new Vector2(0.5f, 0.5f), 128f);
    }

    private void OnEnable()
    {
        ObjectGrabber.OnObjectPlaced += OnItemPlaced;
        ReactableTag.OnPrivacyChanged += OnPrivacyChanged;
    }

    private void OnDisable()
    {
        ObjectGrabber.OnObjectPlaced -= OnItemPlaced;
        ReactableTag.OnPrivacyChanged -= OnPrivacyChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_openEyeSprite != null) Destroy(_openEyeSprite);
        if (_closedEyeSprite != null) Destroy(_closedEyeSprite);
        if (_openEyeTex != null) Destroy(_openEyeTex);
        if (_closedEyeTex != null) Destroy(_closedEyeTex);
    }

    private void OnItemPlaced()
    {
        // Find the item that was just placed (most recently held)
        // ObjectGrabber clears _held before firing, so check all PlaceableObjects
        // for the one in Placed state closest to the cursor
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector2 cursorScreen = IrisInput.CursorPosition;
        Ray ray = _cam.ScreenPointToRay(cursorScreen);

        PlaceableObject best = null;
        float bestDist = float.MaxValue;
        var all = PlaceableObject.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].CurrentState != PlaceableObject.State.Placed) continue;
            float d = Vector3.Cross(ray.direction, all[i].transform.position - ray.origin).magnitude;
            if (d < bestDist) { bestDist = d; best = all[i]; }
        }

        if (best == null) return;

        var tag = best.GetComponent<ReactableTag>();
        bool isPrivate = tag != null && tag.IsPrivate;
        ShowIcon(best.transform, isPrivate);
    }

    private void OnPrivacyChanged(ReactableTag tag, bool isPrivate)
    {
        // Only show for items that are placed in the apartment (not held, not in drawers)
        var placeable = tag.GetComponent<PlaceableObject>();
        if (placeable == null) return;
        if (!tag.gameObject.activeInHierarchy) return;

        ShowIcon(tag.transform, isPrivate);
    }

    private void ShowIcon(Transform target, bool isPrivate)
    {
        // Recycle any existing icon for the same target
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].target == target)
            {
                _active[i].go.SetActive(false);
                _pool.Enqueue(_active[i]);
                _active.RemoveAt(i);
            }
        }

        var icon = GetOrCreateIcon();
        icon.target = target;
        icon.timer = 0f;
        icon.totalLife = _holdDuration + _fadeDuration;
        icon.sr.sprite = isPrivate ? _closedEyeSprite : _openEyeSprite;
        icon.sr.color = Color.white;
        icon.go.SetActive(true);

        _active.Add(icon);
    }

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        float dt = Time.unscaledDeltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var icon = _active[i];
            icon.timer += dt;

            if (icon.timer >= icon.totalLife || icon.target == null)
            {
                icon.go.SetActive(false);
                _pool.Enqueue(icon);
                _active.RemoveAt(i);
                continue;
            }

            // Position: offset from target, billboard toward camera
            Vector3 camRight = _cam.transform.right;
            Vector3 camUp = _cam.transform.up;
            icon.go.transform.position = icon.target.position
                + camRight * _offset.x + camUp * _offset.y + _cam.transform.forward * _offset.z;
            icon.go.transform.rotation = _cam.transform.rotation;
            icon.go.transform.localScale = Vector3.one * _iconSize;

            // Fade
            float alpha = 1f;
            if (icon.timer > _holdDuration)
            {
                float fadeT = (icon.timer - _holdDuration) / _fadeDuration;
                alpha = 1f - Mathf.Clamp01(fadeT);
            }
            var c = icon.sr.color;
            c.a = alpha;
            icon.sr.color = c;

            _active[i] = icon;
        }
    }

    private IconInstance GetOrCreateIcon()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        var go = new GameObject("VisEyeIcon");
        go.transform.SetParent(transform);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 999;
        // Render on top of everything (visible through doors/walls)
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.material.renderQueue = 4000;
        go.SetActive(false);

        return new IconInstance { go = go, sr = sr };
    }

    // ── Procedural eye textures (16x16 pixel art) ──

    private static Texture2D GenOpenEye()
    {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[S * S];

        var white = new Color32(240, 240, 235, 255);
        var outline = new Color32(60, 55, 50, 255);
        var iris = new Color32(90, 130, 90, 255);
        var pupil = new Color32(30, 25, 20, 255);

        // Eye outline (almond shape)
        // Row 6-9: the eye body
        for (int x = 3; x <= 12; x++) Set(px, S, x, 6, outline);
        for (int x = 2; x <= 13; x++) Set(px, S, x, 7, white);
        for (int x = 2; x <= 13; x++) Set(px, S, x, 8, white);
        for (int x = 3; x <= 12; x++) Set(px, S, x, 9, outline);

        // Corners
        Set(px, S, 2, 7, outline); Set(px, S, 13, 7, outline);
        Set(px, S, 2, 8, outline); Set(px, S, 13, 8, outline);
        Set(px, S, 1, 7, outline); Set(px, S, 14, 7, outline);
        Set(px, S, 1, 8, outline); Set(px, S, 14, 8, outline);

        // Iris (center)
        for (int y = 6; y <= 9; y++)
            for (int x = 6; x <= 9; x++)
                Set(px, S, x, y, iris);

        // Pupil (center of iris)
        Set(px, S, 7, 7, pupil); Set(px, S, 8, 7, pupil);
        Set(px, S, 7, 8, pupil); Set(px, S, 8, 8, pupil);

        // Highlight
        Set(px, S, 9, 6, new Color32(255, 255, 255, 200));

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D GenClosedEye()
    {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[S * S];

        var outline = new Color32(60, 55, 50, 255);
        var lash = new Color32(80, 75, 70, 255);

        // Closed eye — horizontal line with slight curve
        for (int x = 2; x <= 13; x++) Set(px, S, x, 8, outline);
        Set(px, S, 1, 8, outline); Set(px, S, 14, 8, outline);

        // Slight downward curve at edges
        Set(px, S, 2, 7, outline); Set(px, S, 13, 7, outline);

        // Eyelashes below the line
        Set(px, S, 4, 7, lash); Set(px, S, 6, 6, lash); Set(px, S, 8, 6, lash);
        Set(px, S, 10, 6, lash); Set(px, S, 12, 7, lash);

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static void Set(Color32[] px, int w, int x, int y, Color32 c)
    {
        if (x >= 0 && x < w && y >= 0 && y < w)
            px[y * w + x] = c;
    }
}
