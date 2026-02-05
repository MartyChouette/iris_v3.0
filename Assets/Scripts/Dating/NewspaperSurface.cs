using System.Collections.Generic;
using UnityEngine;

public class NewspaperSurface : MonoBehaviour
{
    [Header("Surface")]
    [Tooltip("Resolution of the cut mask texture.")]
    [SerializeField] private int textureSize = 512;

    [Tooltip("Base newspaper color (fully opaque).")]
    [SerializeField] private Color paperColor = new Color(0.92f, 0.90f, 0.85f, 1f);

    [Tooltip("Width of cut line in UV space.")]
    [SerializeField] private float cutWidth = 0.005f;

    [Header("Cut Piece Visual")]
    [Tooltip("How fast cut pieces fall away (world units/sec).")]
    [SerializeField] private float cutPieceDropSpeed = 0.5f;

    private Texture2D _cutMask;
    private Color32[] _pixels;
    private Material _matInstance;
    private Renderer _renderer;

    private static readonly Color32 s_opaque = new Color32(255, 255, 255, 255);
    private static readonly Color32 s_transparent = new Color32(0, 0, 0, 0);

    // ─── Public API ───────────────────────────────────────────────

    /// <summary>
    /// Stamp transparent pixels along a path segment (called as scissors progress).
    /// </summary>
    public void CutAlongPath(List<Vector2> uvPoints, int fromIndex, int toIndex)
    {
        if (uvPoints == null || _pixels == null) return;

        fromIndex = Mathf.Max(0, fromIndex);
        toIndex = Mathf.Min(uvPoints.Count - 1, toIndex);

        bool dirty = false;
        int halfWidth = Mathf.Max(1, Mathf.RoundToInt(cutWidth * textureSize * 0.5f));

        for (int i = fromIndex; i < toIndex; i++)
        {
            Vector2 a = uvPoints[i];
            Vector2 b = uvPoints[i + 1];

            // Bresenham-style line stamp
            int steps = Mathf.Max(
                Mathf.Abs(Mathf.RoundToInt((b.x - a.x) * textureSize)),
                Mathf.Abs(Mathf.RoundToInt((b.y - a.y) * textureSize)));
            steps = Mathf.Max(steps, 1);

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                Vector2 p = Vector2.Lerp(a, b, t);
                StampCircle(p, halfWidth);
                dirty = true;
            }
        }

        if (dirty) ApplyTexture();
    }

    /// <summary>
    /// Fill the enclosed polygon area with transparent pixels (the cut-out area).
    /// </summary>
    public void FillCutPolygon(List<Vector2> uvPolygon)
    {
        if (uvPolygon == null || uvPolygon.Count < 3 || _pixels == null) return;

        // Find bounding box in pixel space
        float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
        for (int i = 0; i < uvPolygon.Count; i++)
        {
            var p = uvPolygon[i];
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        int pxMinX = Mathf.Max(0, Mathf.FloorToInt(minX * textureSize));
        int pxMaxX = Mathf.Min(textureSize - 1, Mathf.CeilToInt(maxX * textureSize));
        int pxMinY = Mathf.Max(0, Mathf.FloorToInt(minY * textureSize));
        int pxMaxY = Mathf.Min(textureSize - 1, Mathf.CeilToInt(maxY * textureSize));

        for (int y = pxMinY; y <= pxMaxY; y++)
        {
            for (int x = pxMinX; x <= pxMaxX; x++)
            {
                Vector2 uv = new Vector2((float)x / textureSize, (float)y / textureSize);
                if (CutPathEvaluator.PointInPolygon(uv, uvPolygon))
                {
                    _pixels[y * textureSize + x] = s_transparent;
                }
            }
        }

        ApplyTexture();
    }

    /// <summary>
    /// Create a child quad with the cut-out region, animate it falling.
    /// </summary>
    public void SpawnCutPiece(List<Vector2> uvPolygon)
    {
        if (uvPolygon == null || uvPolygon.Count < 3) return;

        // Compute centroid in UV space
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < uvPolygon.Count; i++)
            centroid += uvPolygon[i];
        centroid /= uvPolygon.Count;

        // Create a small quad for the cut piece
        var pieceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pieceGO.name = "CutPiece";
        Object.Destroy(pieceGO.GetComponent<Collider>());

        // Position relative to newspaper
        var myTransform = transform;
        Vector3 localOffset = new Vector3(
            (centroid.x - 0.5f) * myTransform.localScale.x,
            0.001f,
            (centroid.y - 0.5f) * myTransform.localScale.z);

        pieceGO.transform.position = myTransform.position + myTransform.rotation * localOffset;
        pieceGO.transform.rotation = myTransform.rotation;
        pieceGO.transform.localScale = myTransform.localScale * 0.15f;

        // Simple material
        var rend = pieceGO.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = paperColor;
            rend.sharedMaterial = mat;
        }

        // Add animator for falling
        var animator = pieceGO.AddComponent<CutPieceAnimator>();
        animator.Init(cutPieceDropSpeed);
    }

    /// <summary>
    /// Restore full opacity (new day).
    /// </summary>
    public void ResetSurface()
    {
        if (_pixels == null) return;

        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = s_opaque;

        ApplyTexture();

        // Destroy any lingering cut pieces
        var pieces = GetComponentsInChildren<CutPieceAnimator>();
        for (int i = 0; i < pieces.Length; i++)
            Destroy(pieces[i].gameObject);

        Debug.Log("[NewspaperSurface] Surface reset.");
    }

    /// <summary>
    /// Check if a specific pixel has been cut.
    /// </summary>
    public bool IsPixelCut(int x, int y)
    {
        if (_pixels == null) return false;
        if (x < 0 || x >= textureSize || y < 0 || y >= textureSize) return false;
        return _pixels[y * textureSize + x].a == 0;
    }

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        InitTexture();
        ApplyToMaterial();
    }

    private void OnDestroy()
    {
        if (_cutMask != null) Destroy(_cutMask);
        if (_matInstance != null) Destroy(_matInstance);
    }

    // ─── Internals ────────────────────────────────────────────────

    private void InitTexture()
    {
        _cutMask = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        _cutMask.filterMode = FilterMode.Bilinear;
        _cutMask.wrapMode = TextureWrapMode.Clamp;

        _pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = s_opaque;

        _cutMask.SetPixels32(_pixels);
        _cutMask.Apply();
    }

    private void ApplyToMaterial()
    {
        if (_renderer == null)
        {
            Debug.LogError("[NewspaperSurface] No Renderer found.");
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");

        _matInstance = new Material(shader);
        _matInstance.SetTexture("_BaseMap", _cutMask);
        _matInstance.color = paperColor;

        // Enable transparent rendering (same as SpillSurface)
        _matInstance.SetFloat("_Surface", 1f);
        _matInstance.SetFloat("_Blend", 0f);
        _matInstance.SetFloat("_AlphaClip", 0f);
        _matInstance.SetOverrideTag("RenderType", "Transparent");
        _matInstance.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _matInstance.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _matInstance.SetInt("_ZWrite", 0);
        _matInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _matInstance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        _renderer.sharedMaterial = _matInstance;
    }

    private void StampCircle(Vector2 uvCenter, int radius)
    {
        int cx = Mathf.RoundToInt(uvCenter.x * (textureSize - 1));
        int cy = Mathf.RoundToInt(uvCenter.y * (textureSize - 1));
        int rSq = radius * radius;

        int xMin = Mathf.Max(cx - radius, 0);
        int xMax = Mathf.Min(cx + radius, textureSize - 1);
        int yMin = Mathf.Max(cy - radius, 0);
        int yMax = Mathf.Min(cy + radius, textureSize - 1);

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= rSq)
                    _pixels[y * textureSize + x] = s_transparent;
            }
        }
    }

    private void ApplyTexture()
    {
        _cutMask.SetPixels32(_pixels);
        _cutMask.Apply();
    }
}

/// <summary>
/// Simple animator that drops a cut piece downward and destroys it.
/// </summary>
public class CutPieceAnimator : MonoBehaviour
{
    private float _dropSpeed;
    private float _rotateSpeed;
    private float _elapsed;
    private const float Lifetime = 2f;

    public void Init(float dropSpeed)
    {
        _dropSpeed = dropSpeed;
        _rotateSpeed = Random.Range(-90f, 90f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        transform.position += Vector3.down * (_dropSpeed * Time.deltaTime);
        transform.Rotate(Vector3.forward, _rotateSpeed * Time.deltaTime);

        if (_elapsed >= Lifetime)
            Destroy(gameObject);
    }
}
