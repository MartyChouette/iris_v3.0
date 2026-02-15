using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Cinemachine;

public class NewspaperManager : MonoBehaviour, IStationManager
{
    public enum State { ReadingPaper, Calling, Done }

    public static NewspaperManager Instance { get; private set; }

    public bool IsAtIdleState => CurrentState == State.Done;

    // ─── References ───────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private NewspaperSurface surface;
    [SerializeField] private Camera mainCamera;

    [Tooltip("WorldSpace Canvas overlay with newspaper text + images (shown when reading).")]
    [SerializeField] private GameObject newspaperOverlay;

    // ─── Cameras (Cinemachine 3) ──────────────────────────────────
    [Header("Cameras")]
    [Tooltip("First-person newspaper reading view (held up in front of player).")]
    [SerializeField] private CinemachineCamera readCamera;

    [SerializeField] private CinemachineBrain brain;

    // ─── Newspaper Object ─────────────────────────────────────────
    [Header("Newspaper Object")]
    [Tooltip("The newspaper quad/plane.")]
    [SerializeField] private Transform newspaperTransform;

    // ─── Ad Slots ─────────────────────────────────────────────────
    [Header("Ad Slots")]
    [SerializeField] private NewspaperAdSlot[] personalSlots;
    [SerializeField] private NewspaperAdSlot[] commercialSlots;

    [Tooltip("Nema's own ad slot (decorative, non-interactive).")]
    [SerializeField] private NewspaperAdSlot nemaAdSlot;

    [Tooltip("Optional portrait sprite for Nema's ad.")]
    [SerializeField] private Sprite nemaPortrait;

    // ─── Calling Phase ────────────────────────────────────────────
    [Header("Calling Phase")]
    [SerializeField] private float callingDuration = 2f;
    [SerializeField] private AudioClip phoneRingSFX;

    // ─── UI ───────────────────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private GameObject callingUI;
    [SerializeField] private TMP_Text callingText;

    // ─── Events ───────────────────────────────────────────────────
    [Header("Events")]
    public UnityEvent<DatePersonalDefinition> OnDateSelected;
    public UnityEvent OnNewspaperDone;

    // ─── Input ────────────────────────────────────────────────────
    private InputAction _clickAction;
    private InputAction _mousePositionAction;

    // ─── State ────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Done;

    /// <summary>The read camera for external priority control (DayPhaseManager).</summary>
    public CinemachineCamera ReadCamera => readCamera;

    /// <summary>The newspaper quad transform for repositioning (tossed position).</summary>
    public Transform NewspaperTransform => newspaperTransform;

    private DatePersonalDefinition _selectedDefinition;

    // Cached coroutine waits (avoid per-call allocation)
    private static readonly WaitForSeconds s_waitCutPause = new WaitForSeconds(0.3f);
    private WaitForSeconds _waitCallingDuration;

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NewspaperManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Inline input actions
        _clickAction = new InputAction("Click", InputActionType.Button, "<Mouse>/leftButton");
        _mousePositionAction = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");

        HideAllUI();
        _waitCallingDuration = new WaitForSeconds(callingDuration);

        // Subscribe to day manager early so we don't miss DayManager.Start() firing OnNewNewspaper
        if (dayManager != null)
            dayManager.OnNewNewspaper.AddListener(OnNewNewspaper);
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

    private void Start()
    {
        // No-op — state is managed by OnNewNewspaper / DayPhaseManager
    }

    // ─── State Updates ────────────────────────────────────────────
    // ReadingPaper and Done need no per-frame updates.
    // Calling is coroutine-driven.

    // ─── State Transitions ────────────────────────────────────────

    private void EnterReadingPaper()
    {
        CurrentState = State.ReadingPaper;
        Debug.Log("[NewspaperManager] Reading paper. Click a personal ad to select!");

        // Camera priority is owned by DayPhaseManager — not touched here.

        // Show text overlay
        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(true);
    }

    private void EnterDone()
    {
        CurrentState = State.Done;
        Debug.Log("[NewspaperManager] Newspaper done — handing off to DayPhaseManager.");

        // Camera priority is owned by DayPhaseManager — not touched here.

        // Hide overlay
        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(false);

        OnNewspaperDone?.Invoke();
    }

    /// <summary>
    /// Called by NewspaperAdSlot buttons when the player clicks a personal ad.
    /// Replaces the former scissors-cutting mechanic.
    /// </summary>
    public void SelectPersonalAd(DatePersonalDefinition def)
    {
        if (CurrentState != State.ReadingPaper) return;
        if (def == null) return;

        _selectedDefinition = def;
        OnDateSelected?.Invoke(def);

        // Hide overlay
        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(false);

        // Burst newspaper into paper scraps (visual flair)
        if (surface != null)
            surface.PlayPoofEffect();

        Debug.Log($"[NewspaperManager] Selected {def.characterName}'s ad!");

        StartCoroutine(SelectionThenCalling());
    }

    private IEnumerator SelectionThenCalling()
    {
        // Brief pause for poof animation
        yield return s_waitCutPause;

        // Transition to calling
        EnterCalling();
    }

    private void EnterCalling()
    {
        CurrentState = State.Calling;

        // Play phone ring SFX
        if (phoneRingSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phoneRingSFX);

        // Show calling UI
        if (callingUI != null) callingUI.SetActive(true);
        if (callingText != null)
            callingText.text = $"Calling {_selectedDefinition.characterName}...";

        StartCoroutine(CallingSequence());
    }

    private IEnumerator CallingSequence()
    {
        yield return _waitCallingDuration;

        if (callingUI != null) callingUI.SetActive(false);

        // Schedule date on DateSessionManager — it owns the arrival timer now
        DateSessionManager.Instance?.ScheduleDate(_selectedDefinition);
        PhoneController.Instance?.SetPendingDate(_selectedDefinition);

        EnterDone();
    }

    // ─── Newspaper Regeneration ───────────────────────────────────

    private void OnNewNewspaper()
    {
        if (dayManager == null) return;

        Debug.Log($"[NewspaperManager] Generating newspaper for day {dayManager.CurrentDay}.");

        // Assign personal ads to slots
        var personals = dayManager.TodayPersonals;
        if (personalSlots != null)
        {
            for (int i = 0; i < personalSlots.Length; i++)
            {
                if (personalSlots[i] == null) continue;

                if (i < personals.Count)
                    personalSlots[i].AssignPersonal(personals[i]);
                else
                    personalSlots[i].Clear();
            }
        }

        // Assign commercial ads to slots
        var commercials = dayManager.TodayCommercials;
        if (commercialSlots != null)
        {
            for (int i = 0; i < commercialSlots.Length; i++)
            {
                if (commercialSlots[i] == null) continue;

                if (i < commercials.Count)
                    commercialSlots[i].AssignCommercial(commercials[i]);
                else
                    commercialSlots[i].Clear();
            }
        }

        // Populate Nema's own ad slot
        if (nemaAdSlot != null)
            nemaAdSlot.AssignPlayerAd(nemaPortrait);

        // Reset cut surface for new newspaper
        if (surface != null)
            surface.ResetSurface();

        // Reset state
        _selectedDefinition = null;
        HideAllUI();

        // Enter reading state — newspaper is held up, click an ad to select
        EnterReadingPaper();
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private void HideAllUI()
    {
        if (callingUI != null) callingUI.SetActive(false);
    }
}
