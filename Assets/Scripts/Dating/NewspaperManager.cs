using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Cinemachine;

public class NewspaperManager : MonoBehaviour, IStationManager
{
    public enum State { ReadingPaper, Cutting, Calling, Done }

    public static NewspaperManager Instance { get; private set; }

    public bool IsAtIdleState => CurrentState == State.Done;

    // ─── References ───────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private ScissorsCutController scissorsController;
    [SerializeField] private NewspaperSurface surface;
    [SerializeField] private CutPathEvaluator evaluator;
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

    private const int PriorityActive = 30;
    private const int PriorityInactive = 0;

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

        // Subscribe to day manager early so we don't miss DayManager.Start() firing OnNewNewspaper
        if (dayManager != null)
            dayManager.OnNewNewspaper.AddListener(OnNewNewspaper);
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _mousePositionAction.Enable();

        // Subscribe to scissors cut completion
        if (scissorsController != null)
            scissorsController.OnCutComplete.AddListener(OnCutCompleted);
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _mousePositionAction.Disable();

        if (scissorsController != null)
            scissorsController.OnCutComplete.RemoveListener(OnCutCompleted);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Start in Done state — OnNewNewspaper will activate reading
        if (readCamera != null)
            readCamera.Priority = PriorityInactive;

        if (scissorsController != null)
            scissorsController.SetEnabled(false);
    }

    // ─── State Updates ────────────────────────────────────────────
    // ReadingPaper and Done need no per-frame updates.
    // Cutting and Calling are coroutine-driven.

    // ─── State Transitions ────────────────────────────────────────

    private void EnterReadingPaper()
    {
        CurrentState = State.ReadingPaper;
        Debug.Log("[NewspaperManager] Reading paper. Draw to cut!");

        // Raise camera priority
        if (readCamera != null)
            readCamera.Priority = PriorityActive;

        // Show text overlay
        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(true);

        // Enable scissors
        if (scissorsController != null)
            scissorsController.SetEnabled(true);
    }

    private void EnterDone()
    {
        CurrentState = State.Done;
        Debug.Log("[NewspaperManager] Newspaper done — free-roam begins.");

        // Lower camera priority
        if (readCamera != null)
            readCamera.Priority = PriorityInactive;

        // Hide overlay
        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(false);

        // Disable scissors
        if (scissorsController != null)
            scissorsController.SetEnabled(false);

        OnNewspaperDone?.Invoke();
    }

    private void OnCutCompleted(List<Vector2> cutPolygonUV)
    {
        if (CurrentState != State.ReadingPaper) return;

        // Evaluate which personal ad was cut
        if (evaluator == null || personalSlots == null) return;

        var winner = evaluator.Evaluate(cutPolygonUV, personalSlots);
        if (winner == null)
        {
            Debug.Log("[NewspaperManager] Cut didn't cover any phone number sufficiently.");
            return;
        }

        _selectedDefinition = winner;
        OnDateSelected?.Invoke(winner);

        // Hide overlay so cut-out animation is visible
        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(false);

        // Disable scissors
        if (scissorsController != null)
            scissorsController.SetEnabled(false);

        StartCoroutine(CuttingThenCalling());
    }

    private IEnumerator CuttingThenCalling()
    {
        // Brief pause for cut-out animation
        CurrentState = State.Cutting;
        Debug.Log($"[NewspaperManager] Cut out {_selectedDefinition.characterName}'s ad!");
        yield return new WaitForSeconds(0.3f);

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
        yield return new WaitForSeconds(callingDuration);

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

        // Reset cut surface for new newspaper
        if (surface != null)
            surface.ResetSurface();

        // Reset state
        _selectedDefinition = null;
        HideAllUI();

        // Enter reading state — newspaper is held up, ready to cut
        EnterReadingPaper();
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private void HideAllUI()
    {
        if (callingUI != null) callingUI.SetActive(false);
    }
}
