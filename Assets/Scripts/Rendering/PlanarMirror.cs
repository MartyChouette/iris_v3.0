using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Planar mirror that renders a real-time reflection to a low-res RenderTexture.
/// Attach to a quad facing the player. The reflection camera is created automatically.
/// The PSX post-process double-pixelates the result for authentic retro look.
/// </summary>
public class PlanarMirror : MonoBehaviour
{
    [Header("Resolution")]
    [Tooltip("Reflection texture width in pixels. Lower = more retro.")]
    [SerializeField] private int _textureWidth = 256;

    [Tooltip("Reflection texture height in pixels.")]
    [SerializeField] private int _textureHeight = 192;

    [Header("Rendering")]
    [Tooltip("Layer mask for what the reflection camera sees.")]
    [SerializeField] private LayerMask _reflectionLayers = ~0;

    [Tooltip("Clip geometry this close to the mirror plane to avoid artifacts.")]
    [SerializeField] private float _clipPlaneOffset = 0.05f;

    [Header("Performance")]
    [Tooltip("Skip frames between reflection updates (0 = every frame).")]
    [SerializeField, Range(0, 3)] private int _skipFrames = 0;

    private Camera _mainCamera;
    private Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private Material _mirrorMaterial;
    private int _frameCounter;

    private static readonly int ReflectionTexID = Shader.PropertyToID("_ReflectionTex");

    private void OnEnable()
    {
        CreateReflectionResources();
    }

    private void OnDisable()
    {
        CleanupResources();
    }

    private void CreateReflectionResources()
    {
        // Reflection texture — point filtering for pixel-perfect retro look
        _reflectionTexture = new RenderTexture(_textureWidth, _textureHeight, 16);
        _reflectionTexture.filterMode = FilterMode.Point;
        _reflectionTexture.useMipMap = false;

        // Create reflection camera as child
        var camGO = new GameObject("ReflectionCamera");
        camGO.transform.SetParent(transform, false);
        camGO.hideFlags = HideFlags.HideAndDontSave;

        _reflectionCamera = camGO.AddComponent<Camera>();
        _reflectionCamera.enabled = false; // We render manually
        _reflectionCamera.targetTexture = _reflectionTexture;
        _reflectionCamera.cullingMask = _reflectionLayers;

        // Add URP camera data
        var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
        camData.renderShadows = false;
        camData.requiresColorOption = CameraOverrideOption.Off;
        camData.requiresDepthOption = CameraOverrideOption.Off;

        // Wire the reflection texture to the mirror material
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _mirrorMaterial = rend.material; // Instance copy
            _mirrorMaterial.SetTexture(ReflectionTexID, _reflectionTexture);
        }
    }

    private void CleanupResources()
    {
        if (_reflectionCamera != null)
            DestroyImmediate(_reflectionCamera.gameObject);

        if (_reflectionTexture != null)
        {
            _reflectionTexture.Release();
            DestroyImmediate(_reflectionTexture);
        }

        if (_mirrorMaterial != null)
            DestroyImmediate(_mirrorMaterial);
    }

    private void LateUpdate()
    {
        if (_reflectionCamera == null) return;

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        // Frame skipping for performance
        if (_skipFrames > 0)
        {
            _frameCounter++;
            if (_frameCounter % (_skipFrames + 1) != 0) return;
        }

        RenderReflection();
    }

    private void RenderReflection()
    {
        // Mirror plane in world space
        Vector3 mirrorPos = transform.position;
        Vector3 mirrorNormal = transform.forward;

        // Match main camera settings
        _reflectionCamera.orthographic = _mainCamera.orthographic;
        if (_mainCamera.orthographic)
            _reflectionCamera.orthographicSize = _mainCamera.orthographicSize;
        else
            _reflectionCamera.fieldOfView = _mainCamera.fieldOfView;
        _reflectionCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _reflectionCamera.farClipPlane = _mainCamera.farClipPlane;
        _reflectionCamera.aspect = (float)_textureWidth / _textureHeight;

        // Reflect the camera position and rotation across the mirror plane
        float d = -Vector3.Dot(mirrorNormal, mirrorPos);
        Vector4 plane = new Vector4(mirrorNormal.x, mirrorNormal.y, mirrorNormal.z, d);

        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(plane);

        Vector3 reflectedPos = reflectionMatrix.MultiplyPoint(_mainCamera.transform.position);
        Vector3 reflectedFwd = reflectionMatrix.MultiplyVector(_mainCamera.transform.forward);
        Vector3 reflectedUp = reflectionMatrix.MultiplyVector(_mainCamera.transform.up);

        _reflectionCamera.transform.position = reflectedPos;
        _reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedFwd, reflectedUp);

        // Oblique near clipping plane so nothing behind the mirror is rendered
        Vector4 clipPlane = CameraSpacePlane(_reflectionCamera, mirrorPos + mirrorNormal * _clipPlaneOffset, mirrorNormal);
        _reflectionCamera.projectionMatrix = _reflectionCamera.CalculateObliqueMatrix(clipPlane);

        // Flip culling winding (reflection reverses triangle winding)
        GL.invertCulling = true;
        _reflectionCamera.Render();
        GL.invertCulling = false;
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 m;
        m.m00 = 1f - 2f * plane.x * plane.x;
        m.m01 =    - 2f * plane.x * plane.y;
        m.m02 =    - 2f * plane.x * plane.z;
        m.m03 =    - 2f * plane.x * plane.w;
        m.m10 =    - 2f * plane.y * plane.x;
        m.m11 = 1f - 2f * plane.y * plane.y;
        m.m12 =    - 2f * plane.y * plane.z;
        m.m13 =    - 2f * plane.y * plane.w;
        m.m20 =    - 2f * plane.z * plane.x;
        m.m21 =    - 2f * plane.z * plane.y;
        m.m22 = 1f - 2f * plane.z * plane.z;
        m.m23 =    - 2f * plane.z * plane.w;
        m.m30 = 0f;
        m.m31 = 0f;
        m.m32 = 0f;
        m.m33 = 1f;
        return m;
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
    {
        Matrix4x4 worldToCam = cam.worldToCameraMatrix;
        Vector3 cPos = worldToCam.MultiplyPoint(pos);
        Vector3 cNormal = worldToCam.MultiplyVector(normal).normalized;
        return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
    }
}
