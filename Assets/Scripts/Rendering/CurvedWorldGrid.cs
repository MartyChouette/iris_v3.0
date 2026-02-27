using UnityEngine;

/// <summary>
/// Applies the Iris/CurvedWorld shader to a ground plane, bending the
/// horizon downward from an isometric camera angle for better visibility.
/// Attach to a large quad/plane in the scene.
/// </summary>
public class CurvedWorldGrid : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Grid line color.")]
    [SerializeField] private Color _gridColor = new Color(0.35f, 0.35f, 0.35f, 0.3f);

    [Tooltip("World-space grid cell size (smaller = denser grid).")]
    [SerializeField] private float _gridScale = 2f;

    [Tooltip("Grid line thickness.")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float _gridThickness = 0.02f;

    [Header("Curve")]
    [Tooltip("How aggressively the ground curves downward.")]
    [SerializeField] private float _curveStrength = 0.003f;

    [Tooltip("Distance from camera before the curve begins.")]
    [SerializeField] private float _curveOffset = 8f;

    [Header("Surface")]
    [Tooltip("Base tint color of the ground.")]
    [SerializeField] private Color _baseColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

    private Material _mat;
    private Renderer _renderer;

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogWarning("[CurvedWorldGrid] No Renderer found.");
            return;
        }

        var shader = Shader.Find("Iris/CurvedWorld");
        if (shader == null)
        {
            Debug.LogWarning("[CurvedWorldGrid] Iris/CurvedWorld shader not found.");
            return;
        }

        _mat = new Material(shader);
        ApplyProperties();
        _renderer.material = _mat;
    }

    private void ApplyProperties()
    {
        if (_mat == null) return;
        _mat.SetColor("_BaseColor", _baseColor);
        _mat.SetColor("_GridColor", _gridColor);
        _mat.SetFloat("_GridScale", _gridScale);
        _mat.SetFloat("_GridThickness", _gridThickness);
        _mat.SetFloat("_CurveStrength", _curveStrength);
        _mat.SetFloat("_CurveOffset", _curveOffset);
    }

    public float CurveStrength
    {
        get => _curveStrength;
        set { _curveStrength = value; if (_mat != null) _mat.SetFloat("_CurveStrength", value); }
    }

    public float CurveOffset
    {
        get => _curveOffset;
        set { _curveOffset = value; if (_mat != null) _mat.SetFloat("_CurveOffset", value); }
    }

    public float GridScale
    {
        get => _gridScale;
        set { _gridScale = value; if (_mat != null) _mat.SetFloat("_GridScale", value); }
    }

    private void OnDestroy()
    {
        if (_mat != null)
            Destroy(_mat);
    }

    private void OnValidate()
    {
        ApplyProperties();
    }
}
