using System.Collections;
using UnityEngine;

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

    [Header("Wall Occlusion")]
    [Tooltip("Layer mask for walls that can block fridge clicks (prevents clicking through walls).")]
    [SerializeField] private LayerMask _wallOcclusionLayer;

    [Header("Item Deposit")]
    [Tooltip("DropZone for accepting items (milk cartons). Assign the DropZone on this GO or a child.")]
    [SerializeField] private DropZone _depositZone;

    [Header("Light")]
    [Tooltip("Point light inside the fridge — on when open, off when closed.")]
    [SerializeField] private Light _interiorLight;

    [Header("Audio")]
    [Tooltip("Played when the door opens.")]
    [SerializeField] private AudioClip _openSFX;

    [Tooltip("Played when the door closes.")]
    [SerializeField] private AudioClip _closeSFX;

    // Input managed by IrisInput singleton

    private enum DoorState { Closed, Opening, Open, Closing }
    private DoorState _state = DoorState.Closed;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;

    private Coroutine _blinkCoroutine;
    private InteractableHighlight _highlight;

    private float _rejectCooldown;
    private const float RejectCooldownDuration = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[FridgeController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_doorPivot != null)
        {
            _closedRotation = _doorPivot.localRotation;
            _openRotation = _closedRotation * Quaternion.Euler(0f, _openAngle, 0f);
        }

        if (_interiorLight != null)
            _interiorLight.enabled = false;
    }

    // Input managed by IrisInput singleton — no local enable/disable needed.

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (DayPhaseManager.Instance != null && !DayPhaseManager.Instance.IsInteractionPhase) return;

        // Fridge door only opens during Phase 2 (drink making)
        bool isDrinkPhase = DateSessionManager.Instance != null
            && DateSessionManager.Instance.CurrentDatePhase == DateSessionManager.DatePhase.BackgroundJudging;

        // Start blink guide when drink phase begins and fridge is closed
        if (isDrinkPhase)
            UpdateBlinkGuide();

        // Item deposit is handled by ObjectGrabber's DropZone proximity search

        if (_rejectCooldown > 0f)
            _rejectCooldown -= Time.deltaTime;

        // Outside drink phase: show rejection text if player clicks the fridge
        // (but not if they're depositing an item — ObjectGrabber handles that)
        if (!isDrinkPhase)
        {
            if (IrisInput.Instance != null && IrisInput.Instance.Click.WasPressedThisFrame()
                && !ObjectGrabber.IsHoldingObject && !ObjectGrabber.ClickConsumedThisFrame
                && _rejectCooldown <= 0f && _mainCamera != null)
            {
                Vector2 rejectMousePos = IrisInput.CursorPosition;
                var rejectRay = _mainCamera.ScreenPointToRay(rejectMousePos);
                if (Physics.Raycast(rejectRay, out var rejectHit, 20f, _fridgeLayer))
                {
                    // Wall occlusion — don't show if clicking through a wall
                    bool blocked = _wallOcclusionLayer.value != 0
                        && Physics.Raycast(rejectRay, out var rejectWallHit, 20f, _wallOcclusionLayer)
                        && rejectWallHit.distance < rejectHit.distance;

                    if (!blocked)
                    {
                        PickupDescriptionHUD.Instance?.Show("I don't want anything right now.");
                        _rejectCooldown = RejectCooldownDuration;
                    }
                }
            }
            return;
        }

        if (_state != DoorState.Closed && _state != DoorState.Open) return;
        if (IrisInput.Instance == null || !IrisInput.Instance.Click.WasPressedThisFrame()) return;

        // Only respond during Browsing apartment state
        if (ApartmentManager.Instance == null) return;
        if (ApartmentManager.Instance.CurrentState != ApartmentManager.State.Browsing)
            return;

        // Don't toggle fridge while actively pouring or scoring a drink
        if (SimpleDrinkManager.Instance != null
            && SimpleDrinkManager.Instance.CurrentState != SimpleDrinkManager.State.ChoosingRecipe)
            return;

        // Wall occlusion raycast (below) handles cross-room blocking — no area gate needed

        if (ObjectGrabber.IsHoldingObject) return;
        if (ObjectGrabber.ClickConsumedThisFrame) return;

        if (_mainCamera == null) return;

        Vector2 mousePos = IrisInput.CursorPosition;
        var ray = _mainCamera.ScreenPointToRay(mousePos);

        // Two-pass raycast: check if a wall is closer than the fridge (prevents clicking through walls)
        if (!Physics.Raycast(ray, out var fridgeHit, 20f, _fridgeLayer))
            return;

        if (_wallOcclusionLayer.value != 0
            && Physics.Raycast(ray, out var wallHit, 20f, _wallOcclusionLayer)
            && wallHit.distance < fridgeHit.distance)
            return;

        if (_state == DoorState.Closed)
        {
            Debug.Log("[FridgeController] Fridge clicked — opening door.");
            StartCoroutine(OpenDoorSequence());
        }
        else if (_state == DoorState.Open)
        {
            Debug.Log("[FridgeController] Fridge clicked — closing door.");
            StartCoroutine(CloseDoorSequence());
        }
    }

    private void TryAcceptHeldItem()
    {
        var held = ObjectGrabber.HeldObject;
        if (held == null) return;

        // Check if held item's home zone matches the fridge's DropZone
        var zone = _depositZone;
        if (zone == null) zone = GetComponent<DropZone>();
        if (zone == null) zone = GetComponentInChildren<DropZone>();
        if (zone == null) return;

        bool matches = (!string.IsNullOrEmpty(held.HomeZoneName) && zone.ZoneName == held.HomeZoneName)
                    || (!string.IsNullOrEmpty(held.AltHomeZoneName) && zone.ZoneName == held.AltHomeZoneName);
        if (!matches) return;

        // Raycast to make sure player clicked on the fridge
        if (_mainCamera == null) return;
        Vector2 mousePos = IrisInput.CursorPosition;
        var ray = _mainCamera.ScreenPointToRay(mousePos);
        if (!Physics.Raycast(ray, out var hit, 20f, _fridgeLayer)) return;

        // Accept the item — release from grabber, then deposit (shrink+destroy)
        ObjectGrabber.ForceReleaseHeld();
        zone.RegisterDeposit(held);
        ObjectGrabber.ConsumeClickExternal();
        Debug.Log($"[FridgeController] Accepted {held.name} into fridge.");
    }

    private IEnumerator OpenDoorSequence()
    {
        _state = DoorState.Opening;
        StopBlinkGuide();

        if (_openSFX != null)
            AudioManager.Instance?.PlaySFX(_openSFX);

        yield return TweenDoor(_closedRotation, _openRotation);
        _state = DoorState.Open;

        if (_interiorLight != null)
            _interiorLight.enabled = true;

        SimpleDrinkManager.Instance?.ShowRecipePanel();
    }

    /// <summary>
    /// Called when exiting the DrinkMaking station to close the fridge door.
    /// </summary>
    public void CloseDoor()
    {
        if (_state != DoorState.Open) return;
        StartCoroutine(CloseDoorSequence());
    }

    /// <summary>
    /// Immediately snap the fridge shut regardless of current state.
    /// Used by sleep/reset sequences where we can't wait for tweens.
    /// </summary>
    public void ForceClose()
    {
        StopAllCoroutines();
        _state = DoorState.Closed;
        if (_doorPivot != null)
            _doorPivot.localRotation = _closedRotation;
        if (_interiorLight != null)
            _interiorLight.enabled = false;
        SimpleDrinkManager.Instance?.HideRecipePanel();
    }

    private IEnumerator CloseDoorSequence()
    {
        _state = DoorState.Closing;

        if (_closeSFX != null)
            AudioManager.Instance?.PlaySFX(_closeSFX);

        yield return TweenDoor(_openRotation, _closedRotation);
        _state = DoorState.Closed;

        if (_interiorLight != null)
            _interiorLight.enabled = false;

        SimpleDrinkManager.Instance?.HideRecipePanel();
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

    // ── Rim Guide ─────────────────────────────────────────────
    // During drink phase (BackgroundJudging), steady rimlight on
    // the fridge until the player selects a recipe.

    private bool _guideActive;

    private void UpdateBlinkGuide()
    {
        bool shouldGuide = DateSessionManager.Instance != null
            && DateSessionManager.Instance.CurrentDatePhase == DateSessionManager.DatePhase.BackgroundJudging
            && (SimpleDrinkManager.Instance == null || SimpleDrinkManager.Instance.CurrentState == SimpleDrinkManager.State.ChoosingRecipe)
            && (SimpleDrinkManager.Instance == null || SimpleDrinkManager.Instance.ActiveRecipe == null);

        if (shouldGuide && !_guideActive)
        {
            if (_highlight == null)
                _highlight = GetComponent<InteractableHighlight>();
            if (_highlight != null)
                _highlight.SetHighlighted(true);
            _guideActive = true;
        }
        else if (!shouldGuide && _guideActive)
        {
            StopBlinkGuide();
        }
    }

    private void StopBlinkGuide()
    {
        if (_highlight != null)
            _highlight.SetHighlighted(false);
        _guideActive = false;
    }
}
