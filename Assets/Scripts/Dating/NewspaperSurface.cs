using System.Collections.Generic;
using UnityEngine;

public class NewspaperSurface : MonoBehaviour
{
    [Header("Surface")]
    [Tooltip("Width of the cut mask texture (pixels).")]
    [SerializeField] private int textureWidth = 512;

    [Tooltip("Height of the cut mask texture (pixels).")]
    [SerializeField] private int textureHeight = 360;

    [Tooltip("Base newspaper color (fully opaque).")]
    [SerializeField] private Color paperColor = new Color(0.92f, 0.90f, 0.85f, 1f);

    [Tooltip("Width of cut line in UV space.")]
    [SerializeField] private float cutWidth = 0.005f;

    [Header("Cut Piece Visual")]
    [Tooltip("How fast cut pieces fall away (world units/sec).")]
    [SerializeField] private float cutPieceDropSpeed = 0.5f;

    [Header("Cut Line")]
    [Tooltip("Color of the visible cut line drawn by scissors.")]
    [SerializeField] private Color cutLineColor = new Color(0.3f, 0.25f, 0.2f, 1f);

    [Header("Paper Poof")]
    [Tooltip("Number of paper scraps in the burst.")]
    [SerializeField] private int burstCount = 40;

    [Tooltip("Lifetime of each scrap particle in seconds.")]
    [SerializeField] private float scrapLifetime = 1.5f;

    [Tooltip("Initial velocity spread of scraps.")]
    [SerializeField] private float scrapSpeed = 1.5f;

    [Tooltip("World-space size of each scrap particle.")]
    [SerializeField] private float scrapSize = 0.03f;

    private Texture2D _cutMask;
    private Color32[] _pixels;
    private Material _matInstance;
    private Renderer _renderer;

    // Pre-cached shader (looked up once in Awake, never again)
    private Shader _cachedShader;

    // Pre-pooled cut piece (created once, repositioned and shown on cut)
    private GameObject _cutPiecePool;
    private CutPieceAnimator _cutPieceAnimator;

    // Pre-created poof particle system
    private ParticleSystem _poofParticles;

    private static readonly Color32 s_transparent = new Color32(0, 0, 0, 0);
    private Color32 _cutLineColor32;

    // Reusable list for scanline fill (avoids per-call allocation)
    private readonly List<float> _scanlineXIntersections = new List<float>(32);

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
        int halfWidthX = Mathf.Max(1, Mathf.RoundToInt(cutWidth * textureWidth * 0.5f));

        for (int i = fromIndex; i < toIndex; i++)
        {
            Vector2 a = uvPoints[i];
            Vector2 b = uvPoints[i + 1];

            // Bresenham-style line stamp
            int steps = Mathf.Max(
                Mathf.Abs(Mathf.RoundToInt((b.x - a.x) * textureWidth)),
                Mathf.Abs(Mathf.RoundToInt((b.y - a.y) * textureHeight)));
            steps = Mathf.Max(steps, 1);

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                Vector2 p = Vector2.Lerp(a, b, t);
                StampCircle(p, halfWidthX);
                dirty = true;
            }
        }

        if (dirty) ApplyTexture();
    }

    /// <summary>
    /// Fill the enclosed polygon area with transparent pixels using scanline fill.
    /// O(height * edges) instead of O(width * height * edges).
    /// </summary>
    public void FillCutPolygon(List<Vector2> uvPolygon)
    {
        if (uvPolygon == null || uvPolygon.Count < 3 || _pixels == null) return;

        int polyCount = uvPolygon.Count;

        // Find bounding box in pixel space
        float minY = 1f, maxY = 0f;
        for (int i = 0; i < polyCount; i++)
        {
            float py = uvPolygon[i].y;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }

        int pxMinY = Mathf.Max(0, Mathf.FloorToInt(minY * textureHeight));
        int pxMaxY = Mathf.Min(textureHeight - 1, Mathf.CeilToInt(maxY * textureHeight));

        // Scanline fill: for each row, find edge intersections, sort, fill spans
        for (int y = pxMinY; y <= pxMaxY; y++)
        {
            float scanY = (float)y / textureHeight;
            _scanlineXIntersections.Clear();

            for (int i = 0, j = polyCount - 1; i < polyCount; j = i++)
            {
                float yi = uvPolygon[i].y;
                float yj = uvPolygon[j].y;

                // Skip horizontal edges and edges that don't cross this scanline
                if ((yi > scanY) == (yj > scanY)) continue;

                float dy = yj - yi;
                if (Mathf.Abs(dy) < 1e-8f) continue;

                float xIntersect = uvPolygon[i].x + (scanY - yi) / dy * (uvPolygon[j].x - uvPolygon[i].x);
                _scanlineXIntersections.Add(xIntersect);
            }

            // Sort intersections (insertion sort — typically 2-4 elements)
            int n = _scanlineXIntersections.Count;
            for (int i = 1; i < n; i++)
            {
                float key = _scanlineXIntersections[i];
                int k = i - 1;
                while (k >= 0 && _scanlineXIntersections[k] > key)
                {
                    _scanlineXIntersections[k + 1] = _scanlineXIntersections[k];
                    k--;
                }
                _scanlineXIntersections[k + 1] = key;
            }

            // Fill between pairs of intersections
            int rowOffset = y * textureWidth;
            for (int i = 0; i + 1 < n; i += 2)
            {
                int xStart = Mathf.Max(0, Mathf.FloorToInt(_scanlineXIntersections[i] * textureWidth));
                int xEnd = Mathf.Min(textureWidth - 1, Mathf.CeilToInt(_scanlineXIntersections[i + 1] * textureWidth));

                for (int x = xStart; x <= xEnd; x++)
                    _pixels[rowOffset + x] = _cutLineColor32;
            }
        }

        ApplyTexture();
    }

    /// <summary>
    /// Show the pre-pooled cut piece at the polygon centroid and animate it falling.
    /// </summary>
    public void SpawnCutPiece(List<Vector2> uvPolygon)
    {
        if (uvPolygon == null || uvPolygon.Count < 3) return;
        if (_cutPiecePool == null) return;

        // Compute centroid in UV space
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < uvPolygon.Count; i++)
            centroid += uvPolygon[i];
        centroid /= uvPolygon.Count;

        // Position relative to newspaper
        var myTransform = transform;
        Vector3 localOffset = new Vector3(
            (centroid.x - 0.5f) * myTransform.localScale.x,
            (centroid.y - 0.5f) * myTransform.localScale.y,
            -0.001f);

        _cutPiecePool.transform.position = myTransform.position + myTransform.rotation * localOffset;
        _cutPiecePool.transform.rotation = myTransform.rotation;
        _cutPiecePool.transform.localScale = myTransform.localScale * 0.15f;
        _cutPiecePool.SetActive(true);

        if (_cutPieceAnimator != null)
            _cutPieceAnimator.Play(cutPieceDropSpeed);
    }

    /// <summary>
    /// Reset surface to opaque white (new day).
    /// </summary>
    public void ResetSurface()
    {
        if (_pixels == null) return;

        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = s_transparent;

        ApplyTexture();

        // Hide cut piece
        if (_cutPiecePool != null)
            _cutPiecePool.SetActive(false);

        // Reset poof particles and re-show renderer
        if (_poofParticles != null)
        {
            _poofParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _poofParticles.gameObject.SetActive(false);
        }
        if (_renderer != null)
            _renderer.enabled = true;

        Debug.Log("[NewspaperSurface] Surface reset.");
    }

    /// <summary>
    /// Burst the newspaper into paper scraps and hide the surface renderer.
    /// </summary>
    public void PlayPoofEffect()
    {
        if (_poofParticles == null) return;

        _poofParticles.transform.position = transform.position;
        _poofParticles.gameObject.SetActive(true);
        _poofParticles.Play();

        // Hide the newspaper surface quad
        if (_renderer != null)
            _renderer.enabled = false;
    }

    /// <summary>
    /// Check if a specific pixel has been cut.
    /// </summary>
    public bool IsPixelCut(int x, int y)
    {
        if (_pixels == null) return false;
        if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight) return false;
        return _pixels[y * textureWidth + x].a > 0;
    }

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        _renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

        // Cache shader once — never call Shader.Find at runtime again
        _cachedShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        _cutLineColor32 = cutLineColor;

        InitTexture();
        ApplyToMaterial();
        PreCreateCutPiece();
        PreCreatePoofParticles();
    }

    private void OnDestroy()
    {
        if (_cutMask != null) Destroy(_cutMask);
        if (_matInstance != null) Destroy(_matInstance);
        if (_poofParticles != null) Destroy(_poofParticles.gameObject);
    }

    // ─── Internals ────────────────────────────────────────────────

    private void InitTexture()
    {
        _cutMask = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        _cutMask.filterMode = FilterMode.Bilinear;
        _cutMask.wrapMode = TextureWrapMode.Clamp;

        _pixels = new Color32[textureWidth * textureHeight];
        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = s_transparent;

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

        _matInstance = new Material(_cachedShader);
        _matInstance.SetTexture("_BaseMap", _cutMask);
        _matInstance.color = Color.white;

        _matInstance.SetFloat("_Surface", 1f);
        _matInstance.SetFloat("_Blend", 0f);
        _matInstance.SetFloat("_AlphaClip", 0f);
        _matInstance.SetOverrideTag("RenderType", "Transparent");
        _matInstance.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _matInstance.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _matInstance.SetInt("_ZWrite", 0);
        _matInstance.SetInt("_Cull", 0);
        _matInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _matInstance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        _renderer.sharedMaterial = _matInstance;
    }

    private void PreCreateCutPiece()
    {
        _cutPiecePool = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _cutPiecePool.name = "CutPiece_Pool";
        Object.Destroy(_cutPiecePool.GetComponent<Collider>());

        var rend = _cutPiecePool.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(_cachedShader);
            mat.color = paperColor;
            rend.sharedMaterial = mat;
        }

        _cutPieceAnimator = _cutPiecePool.AddComponent<CutPieceAnimator>();
        _cutPiecePool.SetActive(false);
    }

    private void PreCreatePoofParticles()
    {
        var go = new GameObject("PaperPoofParticles");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        _poofParticles = go.AddComponent<ParticleSystem>();

        var main = _poofParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = scrapLifetime;
        main.startSpeed = scrapSpeed;
        main.startSize = scrapSize;
        main.startColor = paperColor;
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = burstCount + 10;

        var emission = _poofParticles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

        var shape = _poofParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var rotation = _poofParticles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        go.SetActive(false);
    }

    private void StampCircle(Vector2 uvCenter, int radius)
    {
        int cx = Mathf.RoundToInt(uvCenter.x * (textureWidth - 1));
        int cy = Mathf.RoundToInt(uvCenter.y * (textureHeight - 1));
        int rSq = radius * radius;

        int xMin = Mathf.Max(cx - radius, 0);
        int xMax = Mathf.Min(cx + radius, textureWidth - 1);
        int yMin = Mathf.Max(cy - radius, 0);
        int yMax = Mathf.Min(cy + radius, textureHeight - 1);

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= rSq)
                    _pixels[y * textureWidth + x] = _cutLineColor32;
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
/// Animates a cut piece falling. Pre-created and pooled by NewspaperSurface.
/// </summary>
public class CutPieceAnimator : MonoBehaviour
{
    private float _dropSpeed;
    private float _rotateSpeed;
    private float _elapsed;
    private bool _playing;
    private const float Lifetime = 2f;

    public void Play(float dropSpeed)
    {
        _dropSpeed = dropSpeed;
        _rotateSpeed = Random.Range(-90f, 90f);
        _elapsed = 0f;
        _playing = true;
    }

    private void Update()
    {
        if (!_playing) return;

        _elapsed += Time.deltaTime;

        transform.position += Vector3.down * (_dropSpeed * Time.deltaTime);
        transform.Rotate(Vector3.forward, _rotateSpeed * Time.deltaTime);

        if (_elapsed >= Lifetime)
        {
            _playing = false;
            gameObject.SetActive(false);
        }
    }
}
