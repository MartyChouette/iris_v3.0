using UnityEngine;

/// <summary>
/// Procedural face texture with an overlay for makeup painting.
/// Base texture has skin tone, facial features, and pimples.
/// Overlay texture starts transparent — all painting goes here.
/// Adapted from <see cref="SpillSurface"/> pattern.
/// </summary>
public class FaceCanvas : MonoBehaviour
{
    [Header("Face Generation")]
    [Tooltip("Texture resolution (square).")]
    [SerializeField] private int _textureSize = 512;

    [Tooltip("Base skin tone colour.")]
    [SerializeField] private Color _skinColor = new Color(0.92f, 0.76f, 0.65f);

    [Tooltip("Eyes, brows, nose colour.")]
    [SerializeField] private Color _featureColor = new Color(0.22f, 0.18f, 0.15f);

    [Tooltip("Natural lip colour.")]
    [SerializeField] private Color _lipColor = new Color(0.82f, 0.45f, 0.45f);

    [Tooltip("Pimple dot colour.")]
    [SerializeField] private Color _pimpleColor = new Color(0.85f, 0.3f, 0.25f);

    [Header("Pimples")]
    [Tooltip("How many pimples to scatter.")]
    [SerializeField] private int _pimpleCount = 12;

    [Tooltip("UV radius of each pimple dot.")]
    [SerializeField] private float _pimpleRadius = 0.008f;

    [Header("Imported Head")]
    [Tooltip("Skip procedural base texture — use the model's existing material.")]
    [SerializeField] private bool _useExternalBase;

    [Tooltip("Optional manual pimple UV positions for imported heads (overrides random scatter).")]
    [SerializeField] private Vector2[] _manualPimpleUVs;

    [Header("References")]
    [Tooltip("The face quad renderer (opaque base).")]
    [SerializeField] private Renderer _baseRenderer;

    [Tooltip("The overlay quad renderer (transparent painting surface).")]
    [SerializeField] private Renderer _overlayRenderer;

    // Base texture (read-only after init)
    private Texture2D _baseTex;
    private Material _baseMat;

    // Overlay texture (all makeup goes here)
    private Texture2D _overlayTex;
    private Material _overlayMat;
    private Color32[] _overlayPixels;

    // Pimple tracking
    private Vector2[] _pimpleUVs;
    private bool[] _pimpleCovered;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>Total number of pimples on the face.</summary>
    public int TotalPimpleCount => _pimpleCount;

    /// <summary>UV positions of all pimples.</summary>
    public Vector2[] PimpleUVs => _pimpleUVs;

    /// <summary>How many pimples have been covered by makeup.</summary>
    public int CoveredPimpleCount
    {
        get
        {
            if (_pimpleCovered == null) return 0;
            int count = 0;
            for (int i = 0; i < _pimpleCovered.Length; i++)
                if (_pimpleCovered[i]) count++;
            return count;
        }
    }

    /// <summary>
    /// Returns true if a pimple at the given index would be visible at the current head yaw.
    /// Pimples near UV edges require the head to be turned to see them.
    /// </summary>
    public bool IsPimpleVisible(int index, float headYaw)
    {
        if (_pimpleUVs == null || index < 0 || index >= _pimpleUVs.Length) return false;

        float uvX = _pimpleUVs[index].x;

        // Edge pimples require head rotation to expose
        if (uvX < 0.32f)
            return headYaw < -5f; // need negative yaw (head turned right)
        if (uvX > 0.68f)
            return headYaw > 5f;  // need positive yaw (head turned left)

        // Center pimples always visible
        return true;
    }

    /// <summary>
    /// Stamp a circle of coloured pixels onto the overlay.
    /// </summary>
    public void Paint(Vector2 uv, Color color, float radius, float opacity, bool softEdge)
    {
        if (PaintInternal(uv, color, radius, opacity, softEdge))
        {
            _overlayTex.SetPixels32(_overlayPixels);
            _overlayTex.Apply();
            CheckPimpleCoverage();
        }
    }

    /// <summary>
    /// Paint a continuous stroke between two UV positions with interpolated stamps.
    /// Uses half-brush-radius spacing to eliminate gaps during fast mouse movement.
    /// </summary>
    public void PaintStroke(Vector2 fromUV, Vector2 toUV, Color color, float radius, float opacity, bool softEdge)
    {
        float dist = Vector2.Distance(fromUV, toUV);
        float step = Mathf.Max(radius * 0.5f, 0.001f);
        int stamps = Mathf.Max(1, Mathf.CeilToInt(dist / step));

        bool dirty = false;
        for (int i = 0; i <= stamps; i++)
        {
            float t = stamps > 0 ? (float)i / stamps : 0f;
            Vector2 uv = Vector2.Lerp(fromUV, toUV, t);
            dirty |= PaintInternal(uv, color, radius, opacity, softEdge);
        }

        if (dirty)
        {
            _overlayTex.SetPixels32(_overlayPixels);
            _overlayTex.Apply();
            CheckPimpleCoverage();
        }
    }

    /// <summary>
    /// Internal pixel-writing loop — stamps a circle but does NOT call Apply or coverage check.
    /// Returns true if any pixels were modified.
    /// </summary>
    private bool PaintInternal(Vector2 uv, Color color, float radius, float opacity, bool softEdge)
    {
        int cx = Mathf.RoundToInt(uv.x * (_textureSize - 1));
        int cy = Mathf.RoundToInt(uv.y * (_textureSize - 1));
        int r = Mathf.Max(1, Mathf.RoundToInt(radius * _textureSize));
        int rSq = r * r;

        int xMin = Mathf.Max(cx - r, 0);
        int xMax = Mathf.Min(cx + r, _textureSize - 1);
        int yMin = Mathf.Max(cy - r, 0);
        int yMax = Mathf.Min(cy + r, _textureSize - 1);

        bool dirty = false;

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                int distSq = dx * dx + dy * dy;
                if (distSq > rSq) continue;

                float alpha = opacity;
                if (softEdge)
                {
                    float dist = Mathf.Sqrt(distSq);
                    alpha *= Mathf.Clamp01(1f - dist / r);
                }

                int idx = y * _textureSize + x;
                Color32 existing = _overlayPixels[idx];
                Color blended = Color.Lerp(
                    new Color(existing.r / 255f, existing.g / 255f, existing.b / 255f, existing.a / 255f),
                    color,
                    alpha
                );
                blended.a = Mathf.Max(blended.a, alpha);

                _overlayPixels[idx] = new Color32(
                    (byte)(blended.r * 255f),
                    (byte)(blended.g * 255f),
                    (byte)(blended.b * 255f),
                    (byte)(blended.a * 255f)
                );
                dirty = true;
            }
        }

        return dirty;
    }

    /// <summary>
    /// Smear existing overlay paint in the drag direction.
    /// Reads pixels behind the drag and pushes them forward.
    /// </summary>
    public void Smear(Vector2 uv, Vector2 dragDir, float radius)
    {
        int cx = Mathf.RoundToInt(uv.x * (_textureSize - 1));
        int cy = Mathf.RoundToInt(uv.y * (_textureSize - 1));
        int r = Mathf.Max(1, Mathf.RoundToInt(radius * _textureSize));
        int rSq = r * r;

        // Sample offset in pixel space (pull from behind)
        int sampleOffsetX = Mathf.RoundToInt(-dragDir.x * _textureSize * 2f);
        int sampleOffsetY = Mathf.RoundToInt(-dragDir.y * _textureSize * 2f);

        int xMin = Mathf.Max(cx - r, 0);
        int xMax = Mathf.Min(cx + r, _textureSize - 1);
        int yMin = Mathf.Max(cy - r, 0);
        int yMax = Mathf.Min(cy + r, _textureSize - 1);

        bool dirty = false;

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy > rSq) continue;

                int srcX = Mathf.Clamp(x + sampleOffsetX, 0, _textureSize - 1);
                int srcY = Mathf.Clamp(y + sampleOffsetY, 0, _textureSize - 1);
                int srcIdx = srcY * _textureSize + srcX;
                int dstIdx = y * _textureSize + x;

                Color32 srcPixel = _overlayPixels[srcIdx];
                if (srcPixel.a == 0) continue;

                Color32 dstPixel = _overlayPixels[dstIdx];
                Color blended = Color.Lerp(
                    new Color(dstPixel.r / 255f, dstPixel.g / 255f, dstPixel.b / 255f, dstPixel.a / 255f),
                    new Color(srcPixel.r / 255f, srcPixel.g / 255f, srcPixel.b / 255f, srcPixel.a / 255f),
                    0.5f
                );

                _overlayPixels[dstIdx] = new Color32(
                    (byte)(blended.r * 255f),
                    (byte)(blended.g * 255f),
                    (byte)(blended.b * 255f),
                    (byte)(Mathf.Max(dstPixel.a / 255f, blended.a) * 255f)
                );
                dirty = true;
            }
        }

        if (dirty)
        {
            _overlayTex.SetPixels32(_overlayPixels);
            _overlayTex.Apply();
        }
    }

    /// <summary>
    /// Stamp a 5-pointed star shape onto the overlay.
    /// </summary>
    public void StampStar(Vector2 uv, float size, Color color)
    {
        int cx = Mathf.RoundToInt(uv.x * (_textureSize - 1));
        int cy = Mathf.RoundToInt(uv.y * (_textureSize - 1));
        int r = Mathf.Max(2, Mathf.RoundToInt(size * _textureSize));
        int rSq = r * r;

        // Precompute star arm angles (5 outer, 5 inner points)
        float innerRatio = 0.4f;
        Vector2[] starPoints = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 10f;
            float dist = (i % 2 == 0) ? 1f : innerRatio;
            starPoints[i] = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
        }

        int xMin = Mathf.Max(cx - r, 0);
        int xMax = Mathf.Min(cx + r, _textureSize - 1);
        int yMin = Mathf.Max(cy - r, 0);
        int yMax = Mathf.Min(cy + r, _textureSize - 1);

        bool dirty = false;

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy > rSq) continue;

                // Check if point is inside the star polygon
                float px = (float)dx / r;
                float py = (float)dy / r;
                if (!IsInsidePolygon(px, py, starPoints)) continue;

                int idx = y * _textureSize + x;
                _overlayPixels[idx] = new Color32(
                    (byte)(color.r * 255f),
                    (byte)(color.g * 255f),
                    (byte)(color.b * 255f),
                    255
                );
                dirty = true;
            }
        }

        if (dirty)
        {
            _overlayTex.SetPixels32(_overlayPixels);
            _overlayTex.Apply();
            CheckPimpleCoverage();
        }
    }

    // ── Lifecycle ───────────────────────────────────────────────────

    void Awake()
    {
        GeneratePimplePositions();
        if (!_useExternalBase)
            GenerateBaseTexture();
        GenerateOverlayTexture();
    }

    void OnDestroy()
    {
        if (!_useExternalBase)
        {
            if (_baseTex != null) Destroy(_baseTex);
            if (_baseMat != null) Destroy(_baseMat);
        }
        if (_overlayTex != null) Destroy(_overlayTex);
        if (_overlayMat != null) Destroy(_overlayMat);
    }

    // ── Face generation ─────────────────────────────────────────────

    private void GeneratePimplePositions()
    {
        // Use manual pimple UVs if provided (for imported heads)
        if (_manualPimpleUVs != null && _manualPimpleUVs.Length > 0)
        {
            _pimpleCount = _manualPimpleUVs.Length;
            _pimpleUVs = new Vector2[_pimpleCount];
            _pimpleCovered = new bool[_pimpleCount];
            System.Array.Copy(_manualPimpleUVs, _pimpleUVs, _pimpleCount);
            return;
        }

        _pimpleUVs = new Vector2[_pimpleCount];
        _pimpleCovered = new bool[_pimpleCount];

        // Bias toward cheeks, forehead, and edges
        for (int i = 0; i < _pimpleCount; i++)
        {
            float x, y;
            if (i < 3)
            {
                // Edge pimples — only visible when head turned
                x = Random.value > 0.5f ? Random.Range(0.68f, 0.75f) : Random.Range(0.25f, 0.32f);
                y = Random.Range(0.3f, 0.7f);
            }
            else if (i < 7)
            {
                // Cheek area
                x = Random.value > 0.5f ? Random.Range(0.33f, 0.42f) : Random.Range(0.58f, 0.67f);
                y = Random.Range(0.35f, 0.55f);
            }
            else
            {
                // Forehead and random
                x = Random.Range(0.35f, 0.65f);
                y = Random.Range(0.62f, 0.72f);
            }

            _pimpleUVs[i] = new Vector2(x, y);
            _pimpleCovered[i] = false;
        }
    }

    private void GenerateBaseTexture()
    {
        _baseTex = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
        _baseTex.filterMode = FilterMode.Bilinear;
        _baseTex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[_textureSize * _textureSize];
        Color32 skin = ToColor32(_skinColor);

        // Fill with skin tone
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = skin;

        // Eyes — dark ovals
        PaintOval(pixels, 0.40f, 0.58f, 0.06f, 0.025f, _featureColor);
        PaintOval(pixels, 0.60f, 0.58f, 0.06f, 0.025f, _featureColor);

        // Pupils — smaller darker circles
        Color pupilColor = new Color(0.08f, 0.06f, 0.05f);
        PaintCircle(pixels, 0.40f, 0.58f, 0.018f, pupilColor);
        PaintCircle(pixels, 0.60f, 0.58f, 0.018f, pupilColor);

        // Eyebrows — arcs above eyes
        PaintOval(pixels, 0.40f, 0.64f, 0.08f, 0.012f, _featureColor);
        PaintOval(pixels, 0.60f, 0.64f, 0.08f, 0.012f, _featureColor);

        // Nose — subtle shadow
        Color noseShadow = Color.Lerp(_skinColor, _featureColor, 0.2f);
        PaintOval(pixels, 0.5f, 0.48f, 0.02f, 0.04f, noseShadow);

        // Mouth / lips
        PaintOval(pixels, 0.5f, 0.38f, 0.08f, 0.025f, _lipColor);

        // Ear bumps at edges
        Color earColor = Color.Lerp(_skinColor, _featureColor, 0.08f);
        PaintOval(pixels, 0.28f, 0.52f, 0.035f, 0.06f, earColor);
        PaintOval(pixels, 0.72f, 0.52f, 0.035f, 0.06f, earColor);

        // Pimples
        for (int i = 0; i < _pimpleCount; i++)
            PaintCircle(pixels, _pimpleUVs[i].x, _pimpleUVs[i].y, _pimpleRadius, _pimpleColor);

        _baseTex.SetPixels32(pixels);
        _baseTex.Apply();

        // Apply to base renderer
        if (_baseRenderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            _baseMat = new Material(shader);
            _baseMat.SetTexture("_BaseMap", _baseTex);
            _baseMat.color = Color.white;
            _baseRenderer.sharedMaterial = _baseMat;
        }

        Debug.Log($"[FaceCanvas] Base texture generated with {_pimpleCount} pimples.");
    }

    private void GenerateOverlayTexture()
    {
        _overlayTex = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
        _overlayTex.filterMode = FilterMode.Bilinear;
        _overlayTex.wrapMode = TextureWrapMode.Clamp;

        _overlayPixels = new Color32[_textureSize * _textureSize];
        // Start fully transparent
        var clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < _overlayPixels.Length; i++)
            _overlayPixels[i] = clear;

        _overlayTex.SetPixels32(_overlayPixels);
        _overlayTex.Apply();

        // Apply to overlay renderer with transparent material
        if (_overlayRenderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            _overlayMat = new Material(shader);
            _overlayMat.SetTexture("_BaseMap", _overlayTex);
            _overlayMat.color = Color.white;

            // Transparent setup (same as SpillSurface)
            _overlayMat.SetFloat("_Surface", 1f);
            _overlayMat.SetFloat("_Blend", 0f);
            _overlayMat.SetFloat("_AlphaClip", 0f);
            _overlayMat.SetOverrideTag("RenderType", "Transparent");
            _overlayMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _overlayMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _overlayMat.SetInt("_ZWrite", 0);
            _overlayMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _overlayMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            _overlayRenderer.sharedMaterial = _overlayMat;
        }
    }

    // ── Pimple coverage ─────────────────────────────────────────────

    private void CheckPimpleCoverage()
    {
        if (_pimpleUVs == null || _overlayPixels == null) return;

        float checkRadius = _pimpleRadius * 2f;
        int checkR = Mathf.Max(1, Mathf.RoundToInt(checkRadius * _textureSize));

        for (int i = 0; i < _pimpleUVs.Length; i++)
        {
            if (_pimpleCovered[i]) continue;

            int px = Mathf.RoundToInt(_pimpleUVs[i].x * (_textureSize - 1));
            int py = Mathf.RoundToInt(_pimpleUVs[i].y * (_textureSize - 1));

            // Check if any overlay pixel near the pimple has paint
            bool found = false;
            for (int dy = -checkR; dy <= checkR && !found; dy++)
            {
                for (int dx = -checkR; dx <= checkR && !found; dx++)
                {
                    int sx = px + dx;
                    int sy = py + dy;
                    if (sx < 0 || sx >= _textureSize || sy < 0 || sy >= _textureSize) continue;
                    if (dx * dx + dy * dy > checkR * checkR) continue;

                    int idx = sy * _textureSize + sx;
                    if (_overlayPixels[idx].a > 100)
                        found = true;
                }
            }

            if (found)
            {
                _pimpleCovered[i] = true;
                Debug.Log($"[FaceCanvas] Pimple {i} covered! ({CoveredPimpleCount}/{_pimpleCount})");
            }
        }
    }

    // ── Drawing helpers ─────────────────────────────────────────────

    private void PaintCircle(Color32[] pixels, float uvX, float uvY, float uvRadius, Color color)
    {
        int cx = Mathf.RoundToInt(uvX * (_textureSize - 1));
        int cy = Mathf.RoundToInt(uvY * (_textureSize - 1));
        int r = Mathf.Max(1, Mathf.RoundToInt(uvRadius * _textureSize));
        int rSq = r * r;
        Color32 c32 = ToColor32(color);

        for (int y = Mathf.Max(cy - r, 0); y <= Mathf.Min(cy + r, _textureSize - 1); y++)
        {
            for (int x = Mathf.Max(cx - r, 0); x <= Mathf.Min(cx + r, _textureSize - 1); x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= rSq)
                    pixels[y * _textureSize + x] = c32;
            }
        }
    }

    private void PaintOval(Color32[] pixels, float uvX, float uvY, float uvRadiusX, float uvRadiusY, Color color)
    {
        int cx = Mathf.RoundToInt(uvX * (_textureSize - 1));
        int cy = Mathf.RoundToInt(uvY * (_textureSize - 1));
        int rx = Mathf.Max(1, Mathf.RoundToInt(uvRadiusX * _textureSize));
        int ry = Mathf.Max(1, Mathf.RoundToInt(uvRadiusY * _textureSize));
        Color32 c32 = ToColor32(color);

        for (int y = Mathf.Max(cy - ry, 0); y <= Mathf.Min(cy + ry, _textureSize - 1); y++)
        {
            for (int x = Mathf.Max(cx - rx, 0); x <= Mathf.Min(cx + rx, _textureSize - 1); x++)
            {
                float dx = (float)(x - cx) / rx;
                float dy = (float)(y - cy) / ry;
                if (dx * dx + dy * dy <= 1f)
                    pixels[y * _textureSize + x] = c32;
            }
        }
    }

    private static bool IsInsidePolygon(float px, float py, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;
        for (int i = 0; i < polygon.Length; i++)
        {
            if ((polygon[i].y > py) != (polygon[j].y > py) &&
                px < (polygon[j].x - polygon[i].x) * (py - polygon[i].y) /
                     (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    private static Color32 ToColor32(Color c)
    {
        return new Color32(
            (byte)(c.r * 255f),
            (byte)(c.g * 255f),
            (byte)(c.b * 255f),
            (byte)(c.a * 255f)
        );
    }
}
