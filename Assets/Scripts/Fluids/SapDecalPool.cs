/**
 * @file SapDecalPool.cs
 * @brief Object pool for sap decals to avoid runtime instantiation.
 *
 * @details
 * Manages a pool of SapDecal objects. Decals are reused instead of
 * instantiated/destroyed to minimize GC pressure.
 *
 * Features:
 * - Pre-warms pool on startup
 * - Auto-expands up to max limit
 * - Oldest decal recycling when pool is exhausted
 *
 * @ingroup fluids
 */

using System.Collections.Generic;
using UnityEngine;

public class SapDecalPool : MonoBehaviour
{
    public static SapDecalPool Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("Decal prefab. Should have SapDecal component.")]
    public GameObject decalPrefab;

    [Header("Pool Settings")]
    public int initialPoolSize = 50;
    public int maxPoolSize = 200;

    [Header("Auto-Create Prefab")]
    [Tooltip("If no prefab assigned, create a simple quad decal")]
    public bool autoCreatePrefab = true;
    public Material decalMaterial;
    public Sprite decalSprite;

    private List<SapDecal> _pool;
    private Queue<SapDecal> _freeQueue;
    private Queue<SapDecal> _activeQueue; // For recycling oldest when pool exhausted
    private Transform _poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        InitializePool();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void InitializePool()
    {
        _pool = new List<SapDecal>(maxPoolSize);
        _freeQueue = new Queue<SapDecal>(maxPoolSize);
        _activeQueue = new Queue<SapDecal>(maxPoolSize);

        // Create pool root
        var rootObj = new GameObject("SapDecalPool");
        rootObj.transform.SetParent(transform);
        _poolRoot = rootObj.transform;

        // Create prefab if needed
        if (decalPrefab == null && autoCreatePrefab)
        {
            CreateDefaultPrefab();
        }

        if (decalPrefab == null)
        {
            Debug.LogError("[SapDecalPool] No decal prefab available! Decals will NOT spawn.");
            return;
        }

        // Pre-warm pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateDecal();
        }
    }

    private void CreateDefaultPrefab()
    {
        var go = new GameObject("DefaultSapDecal");

        // Build a procedural quad mesh for URP-compatible decals
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        // Procedural quad
        var mesh = new Mesh { name = "SapDecalQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;

        // Procedural soft-circle texture
        int texSize = 32;
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = texSize * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float a = Mathf.Clamp01((radius - dist) / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        // URP Particles/Unlit shader with Sprites/Default fallback
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        var mat = new Material(shader);
        mat.mainTexture = tex;
        mat.color = new Color(0.2f, 0.7f, 0.1f, 0.8f);
        // Enable transparency
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.renderQueue = 3000;
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Add SapDecal component
        var decal = go.AddComponent<SapDecal>();
        decal.decalColor = new Color(0.2f, 0.7f, 0.1f, 0.8f);

        go.SetActive(false);
        decalPrefab = go;
    }

    private SapDecal CreateDecal()
    {
        var go = Instantiate(decalPrefab, _poolRoot);
        go.name = $"SapDecal_{_pool.Count}";
        go.SetActive(false);

        var decal = go.GetComponent<SapDecal>();
        if (decal == null)
        {
            decal = go.AddComponent<SapDecal>();
        }

        _pool.Add(decal);
        _freeQueue.Enqueue(decal);

        return decal;
    }

    /// <summary>
    /// Get a decal from the pool.
    /// </summary>
    public SapDecal Get()
    {
        SapDecal decal = null;

        // Try free queue first
        while (_freeQueue.Count > 0)
        {
            decal = _freeQueue.Dequeue();
            if (decal != null && !decal.gameObject.activeInHierarchy)
            {
                _activeQueue.Enqueue(decal);
                return decal;
            }
        }

        // Try to expand pool
        if (_pool.Count < maxPoolSize)
        {
            decal = CreateDecal();
            _activeQueue.Enqueue(decal);
            return decal;
        }

        // Pool exhausted - recycle oldest active decal
        if (_activeQueue.Count > 0)
        {
            decal = _activeQueue.Dequeue();
            if (decal != null)
            {
                decal.Deactivate();
                _activeQueue.Enqueue(decal);
                return decal;
            }
        }

        Debug.LogWarning("[SapDecalPool] Pool exhausted and no decals to recycle!");
        return null;
    }

    /// <summary>
    /// Return a decal to the pool.
    /// </summary>
    public void Return(SapDecal decal)
    {
        if (decal == null) return;

        decal.gameObject.SetActive(false);
        _freeQueue.Enqueue(decal);
    }

    /// <summary>
    /// Clear all active decals (useful for scene reset).
    /// </summary>
    public void ClearAll()
    {
        foreach (var decal in _pool)
        {
            if (decal != null)
            {
                decal.gameObject.SetActive(false);
            }
        }

        _freeQueue.Clear();
        _activeQueue.Clear();

        foreach (var decal in _pool)
        {
            if (decal != null)
            {
                _freeQueue.Enqueue(decal);
            }
        }
    }
}
