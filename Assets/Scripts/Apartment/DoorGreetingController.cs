using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Scene-scoped singleton that handles the door knock → answer → phase transition sequence.
/// When TriggerKnock() is called, shows "knock knock" text above the door.
/// Player clicks the door to answer, triggering a fade-to-black phase title transition.
/// </summary>
public class DoorGreetingController : MonoBehaviour
{
    public static DoorGreetingController Instance { get; private set; }

    [Header("References")]
    [Tooltip("Layer mask for the door collider.")]
    [SerializeField] private LayerMask _doorLayer;

    [Tooltip("Main camera for raycasting.")]
    [SerializeField] private Camera _mainCamera;

    [Tooltip("World-space TMP text shown above the door ('knock knock').")]
    [SerializeField] private TMP_Text _knockText;

    [Header("Audio")]
    [Tooltip("SFX played when the knock occurs.")]
    [SerializeField] private AudioClip _knockSFX;

    [Tooltip("SFX played when the door is answered.")]
    [SerializeField] private AudioClip _doorOpenSFX;

    private InputAction _clickAction;
    private InputAction _mousePositionAction;
    private bool _knockActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DoorGreetingController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("DoorClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePositionAction = new InputAction("DoorMousePos", InputActionType.Value, "<Mouse>/position");

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_knockText != null)
            _knockText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _mousePositionAction.Enable();
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _mousePositionAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_knockActive) return;
        if (ObjectGrabber.IsHoldingObject) return;

        if (_clickAction.WasPressedThisFrame() && CheckDoorRaycast())
        {
            _knockActive = false;
            StartCoroutine(DoorAnsweredSequence());
        }
    }

    /// <summary>Show "knock knock" text and enable door click detection.</summary>
    public void TriggerKnock()
    {
        _knockActive = true;

        if (_knockText != null)
        {
            _knockText.gameObject.SetActive(true);
            _knockText.text = "knock knock";
        }

        if (_knockSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_knockSFX);

        Debug.Log("[DoorGreetingController] Knock triggered — waiting for player to click door.");
    }

    private IEnumerator DoorAnsweredSequence()
    {
        // Hide knock text
        if (_knockText != null)
            _knockText.gameObject.SetActive(false);

        // Play door open SFX
        if (_doorOpenSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_doorOpenSFX);

        // Delegate to DateSessionManager — it owns the Phase 1 fade transition
        DateSessionManager.Instance?.OnDateCharacterArrived();
        yield break;
    }

    private bool CheckDoorRaycast()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return false;

        Vector2 screenPos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);

        return Physics.Raycast(ray, out _, 100f, _doorLayer);
    }
}
