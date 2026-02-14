using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Click-to-open fridge door. Visual interaction during Selected apartment state.
/// The door tweens open when clicked; DrinkMaking manager is already active from Selected state.
/// </summary>
public class FridgeController : MonoBehaviour
{
    public static FridgeController Instance { get; private set; }

    [Header("Door")]
    [Tooltip("Empty at the hinge edge — door mesh is a child of this.")]
    [SerializeField] private Transform _doorPivot;

    [Tooltip("Degrees to rotate (negative = opens outward).")]
    [SerializeField] private float _openAngle = -110f;

    [Tooltip("Seconds for the open / close tween.")]
    [SerializeField] private float _tweenDuration = 0.6f;

    [Header("Interaction")]
    [Tooltip("Layer mask for the fridge door collider.")]
    [SerializeField] private LayerMask _fridgeLayer;

    [Tooltip("Main camera used for raycasting.")]
    [SerializeField] private Camera _mainCamera;

    [Header("Audio")]
    [Tooltip("Played when the door opens.")]
    [SerializeField] private AudioClip _openSFX;

    [Tooltip("Played when the door closes.")]
    [SerializeField] private AudioClip _closeSFX;

    // Inline InputActions (project convention)
    private InputAction _clickAction;
    private InputAction _mousePositionAction;

    private enum DoorState { Closed, Opening, Open, Closing }
    private DoorState _state = DoorState.Closed;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[FridgeController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("FridgeClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePositionAction = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");

        if (_doorPivot != null)
        {
            _closedRotation = _doorPivot.localRotation;
            _openRotation = _closedRotation * Quaternion.Euler(0f, _openAngle, 0f);
        }
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
        if (_state != DoorState.Closed) return;
        if (!_clickAction.WasPressedThisFrame()) return;

        // Only respond during Browsing apartment state
        if (ApartmentManager.Instance == null) return;
        if (ApartmentManager.Instance.CurrentState != ApartmentManager.State.Browsing)
            return;

        if (_mainCamera == null) return;

        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
        var ray = _mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out var hit, 20f, _fridgeLayer))
        {
            Debug.Log("[FridgeController] Fridge clicked — opening door.");
            StartCoroutine(OpenDoorSequence());
        }
    }

    private IEnumerator OpenDoorSequence()
    {
        _state = DoorState.Opening;

        if (_openSFX != null)
            AudioManager.Instance?.PlaySFX(_openSFX);

        yield return TweenDoor(_closedRotation, _openRotation);
        _state = DoorState.Open;

        // DrinkMaking manager is already soft-activated from Selected state — no ForceEnter needed.
    }

    /// <summary>
    /// Called when exiting the DrinkMaking station to close the fridge door.
    /// </summary>
    public void CloseDoor()
    {
        if (_state != DoorState.Open) return;
        StartCoroutine(CloseDoorSequence());
    }

    private IEnumerator CloseDoorSequence()
    {
        _state = DoorState.Closing;

        if (_closeSFX != null)
            AudioManager.Instance?.PlaySFX(_closeSFX);

        yield return TweenDoor(_openRotation, _closedRotation);
        _state = DoorState.Closed;
    }

    private IEnumerator TweenDoor(Quaternion from, Quaternion to)
    {
        if (_doorPivot == null) yield break;

        float elapsed = 0f;
        while (elapsed < _tweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _tweenDuration);
            // Smooth step for a nicer feel
            t = t * t * (3f - 2f * t);
            _doorPivot.localRotation = Quaternion.Lerp(from, to, t);
            yield return null;
        }
        _doorPivot.localRotation = to;
    }
}
