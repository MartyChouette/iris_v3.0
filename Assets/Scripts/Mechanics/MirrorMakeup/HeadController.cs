using UnityEngine;

/// <summary>
/// Maps mouse screen position to head rotation for a parallax inspection feel.
/// Rotate the face left/right and up/down to reveal different angles.
/// </summary>
public class HeadController : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Max horizontal rotation in degrees.")]
    [SerializeField] private float _maxYaw = 25f;

    [Tooltip("Max vertical rotation in degrees.")]
    [SerializeField] private float _maxPitch = 15f;

    [Tooltip("Lerp speed for smooth following.")]
    [SerializeField] private float _smoothSpeed = 8f;

    [Header("References")]
    [Tooltip("Main camera for screen-space mapping.")]
    [SerializeField] private Camera _mainCamera;

    [Tooltip("The parent transform to rotate (the head).")]
    [SerializeField] private Transform _headTransform;

    private float _currentYaw;
    private float _currentPitch;
    private Vector2 _normalizedMousePos;
    private Quaternion _baseRotation;

    /// <summary>Current horizontal rotation in degrees.</summary>
    public float CurrentYaw => _currentYaw;

    /// <summary>Current vertical rotation in degrees.</summary>
    public float CurrentPitch => _currentPitch;

    /// <summary>Normalized mouse position from -1 to 1.</summary>
    public Vector2 NormalizedMousePos => _normalizedMousePos;

    void Awake()
    {
        if (_headTransform != null)
            _baseRotation = _headTransform.localRotation;
    }

    void Update()
    {
        if (_mainCamera == null || _headTransform == null) return;

        Vector3 mouseScreen = Input.mousePosition;
        _normalizedMousePos = new Vector2(
            (mouseScreen.x / Screen.width) * 2f - 1f,
            (mouseScreen.y / Screen.height) * 2f - 1f
        );

        float targetYaw = _normalizedMousePos.x * _maxYaw;
        float targetPitch = -_normalizedMousePos.y * _maxPitch;

        _currentYaw = Mathf.Lerp(_currentYaw, targetYaw, _smoothSpeed * Time.deltaTime);
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, _smoothSpeed * Time.deltaTime);

        _headTransform.localRotation = _baseRotation * Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    }
}
