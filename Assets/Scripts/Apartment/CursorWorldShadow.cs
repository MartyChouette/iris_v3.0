using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Projects a shadow + reflection disc onto world surfaces under the mouse cursor.
/// The disc aligns to the surface normal and follows the cursor each frame.
/// </summary>
public class CursorWorldShadow : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Layers the cursor shadow can project onto.")]
    [SerializeField] private LayerMask _surfaceLayers = ~0;

    [Tooltip("Max raycast distance from camera.")]
    [SerializeField] private float _maxDistance = 50f;

    [Header("Shadow Size")]
    [Tooltip("World-space diameter of the shadow disc.")]
    [SerializeField] private float _diameter = 0.3f;

    [Tooltip("Offset from surface along normal to prevent z-fighting.")]
    [SerializeField] private float _surfaceOffset = 0.005f;

    [Header("Smoothing")]
    [Tooltip("How quickly the shadow follows the cursor (0 = instant).")]
    [SerializeField] private float _smoothSpeed = 25f;

    private Camera _cam;
    private InputAction _mousePosition;
    private GameObject _shadowQuad;
    private MeshRenderer _shadowRenderer;
    private Material _shadowMat;
    private Vector3 _currentPos;
    private Quaternion _currentRot;
    private bool _hasTarget;

    private void Awake()
    {
        _mousePosition = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");
        BuildQuad();
    }

    private void OnEnable()
    {
        _mousePosition.Enable();
    }

    private void OnDisable()
    {
        _mousePosition.Disable();
    }

    private void OnDestroy()
    {
        if (_shadowMat != null) Destroy(_shadowMat);
        if (_shadowQuad != null) Destroy(_shadowQuad);
    }

    private void BuildQuad()
    {
        _shadowQuad = new GameObject("CursorShadowQuad");
        _shadowQuad.transform.SetParent(transform);
        _shadowQuad.hideFlags = HideFlags.HideAndDontSave;

        var mf = _shadowQuad.AddComponent<MeshFilter>();
        _shadowRenderer = _shadowQuad.AddComponent<MeshRenderer>();

        // Simple quad mesh
        var mesh = new Mesh { name = "CursorShadowMesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;

        // Use the cursor shadow shader
        var shader = Shader.Find("Iris/CursorShadow");
        if (shader == null)
        {
            // Fallback: transparent unlit
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
        _shadowMat = new Material(shader);
        _shadowRenderer.sharedMaterial = _shadowMat;
        _shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _shadowRenderer.receiveShadows = false;

        _shadowQuad.SetActive(false);
    }

    private void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        // Don't show when holding an object (ObjectGrabber has its own shadow)
        if (ObjectGrabber.IsHoldingObject)
        {
            _shadowQuad.SetActive(false);
            return;
        }

        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = _cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _surfaceLayers))
        {
            Vector3 targetPos = hit.point + hit.normal * _surfaceOffset;
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal);

            if (!_hasTarget)
            {
                // First frame — snap immediately
                _currentPos = targetPos;
                _currentRot = targetRot;
                _hasTarget = true;
            }
            else
            {
                // Smooth follow
                float dt = Time.unscaledDeltaTime;
                _currentPos = Vector3.Lerp(_currentPos, targetPos, _smoothSpeed * dt);
                _currentRot = Quaternion.Slerp(_currentRot, targetRot, _smoothSpeed * dt);
            }

            _shadowQuad.transform.position = _currentPos;
            _shadowQuad.transform.rotation = _currentRot;
            _shadowQuad.transform.localScale = new Vector3(_diameter, 1f, _diameter);
            _shadowQuad.SetActive(true);
        }
        else
        {
            _shadowQuad.SetActive(false);
            _hasTarget = false;
        }
    }
}
