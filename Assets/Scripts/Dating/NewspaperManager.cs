using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Cinemachine;

public class NewspaperManager : MonoBehaviour, IStationManager
{
    public enum State { TableView, PickingUp, ReadingPaper, Cutting, Calling, Waiting, DateArrived }

    public static NewspaperManager Instance { get; private set; }

    public bool IsAtIdleState => CurrentState == State.TableView;

    // ─── References ───────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private ScissorsCutController scissorsController;
    [SerializeField] private NewspaperSurface surface;
    [SerializeField] private CutPathEvaluator evaluator;
    [SerializeField] private Camera mainCamera;

    // ─── Cameras (Cinemachine 3) ──────────────────────────────────
    [Header("Cameras")]
    [Tooltip("Looking down at desk with paper.")]
    [SerializeField] private CinemachineCamera tableCamera;

    [Tooltip("Close-up first-person reading view.")]
    [SerializeField] private CinemachineCamera paperCamera;

    [SerializeField] private CinemachineBrain brain;

    // ─── Newspaper Object ─────────────────────────────────────────
    [Header("Newspaper Object")]
    [Tooltip("The newspaper quad/plane on the desk.")]
    [SerializeField] private Transform newspaperTransform;

    [Tooltip("Click target when on table.")]
    [SerializeField] private Collider newspaperClickCollider;

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
    [SerializeField] private GameObject timerUI;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject arrivedUI;
    [SerializeField] private TMP_Text arrivedText;

    // ─── Events ───────────────────────────────────────────────────
    [Header("Events")]
    public UnityEvent<DatePersonalDefinition> OnDateSelected;
    public UnityEvent OnDateArrived;

    // ─── Input ────────────────────────────────────────────────────
    private InputAction _clickAction;
    private InputAction _mousePositionAction;
    private InputAction _cancelAction;

    // ─── State ────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.TableView;

    private DatePersonalDefinition _selectedDefinition;
    private float _timeRemaining;
    private bool _blendWaiting;

    private const int PriorityActive = 20;
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
        _cancelAction = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");

        HideAllUI();
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _mousePositionAction.Enable();
        _cancelAction.Enable();

        // Subscribe to scissors cut completion
        if (scissorsController != null)
            scissorsController.OnCutComplete.AddListener(OnCutCompleted);
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _mousePositionAction.Disable();
        _cancelAction.Disable();

        if (scissorsController != null)
            scissorsController.OnCutComplete.RemoveListener(OnCutCompleted);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Subscribe to day manager
        if (dayManager != null)
            dayManager.OnNewNewspaper.AddListener(OnNewNewspaper);

        // Initial camera setup
        SetCamera(tableCamera);

        if (scissorsController != null)
            scissorsController.SetEnabled(false);
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case State.TableView:
                UpdateTableView();
                break;

            case State.PickingUp:
                UpdatePickingUp();
                break;

            case State.ReadingPaper:
                UpdateReadingPaper();
                break;

            case State.Waiting:
                UpdateWaiting();
                break;
        }
    }

    // ─── State Updates ────────────────────────────────────────────

    private void UpdateTableView()
    {
        if (!_clickAction.WasPressedThisFrame()) return;

        // Raycast to check if player clicked the newspaper
        Vector2 screenPos = _mousePositionAction.ReadValue<Vector2>();
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (newspaperClickCollider != null && hit.collider == newspaperClickCollider)
            {
                EnterPickingUp();
            }
        }
    }

    private void UpdatePickingUp()
    {
        // Wait for Cinemachine blend to complete
        if (brain != null && brain.IsBlending)
            return;

        EnterReadingPaper();
    }

    private void UpdateReadingPaper()
    {
        // Escape → put newspaper down
        if (_cancelAction.WasPressedThisFrame())
        {
            ReturnToTable();
        }
    }

    private void UpdateWaiting()
    {
        _timeRemaining -= Time.deltaTime;

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            UpdateTimerDisplay();
            EnterDateArrived();
        }
        else
        {
            UpdateTimerDisplay();
        }
    }

    // ─── State Transitions ────────────────────────────────────────

    private void EnterPickingUp()
    {
        CurrentState = State.PickingUp;
        Debug.Log("[NewspaperManager] Picking up newspaper...");

        // Blend to paper camera
        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);
        }
        SetCamera(paperCamera);
    }

    private void EnterReadingPaper()
    {
        CurrentState = State.ReadingPaper;
        Debug.Log("[NewspaperManager] Reading paper. Draw to cut!");

        // Enable scissors
        if (scissorsController != null)
            scissorsController.SetEnabled(true);
    }

    private void ReturnToTable()
    {
        CurrentState = State.TableView;
        Debug.Log("[NewspaperManager] Returning to table view.");

        // Disable scissors
        if (scissorsController != null)
            scissorsController.SetEnabled(false);

        // Blend back to table camera
        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);
        }
        SetCamera(tableCamera);
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
        EnterWaiting();
    }

    private void EnterWaiting()
    {
        CurrentState = State.Waiting;
        _timeRemaining = _selectedDefinition.arrivalTimeSec;

        if (timerUI != null) timerUI.SetActive(true);
        UpdateTimerDisplay();
    }

    private void EnterDateArrived()
    {
        CurrentState = State.DateArrived;
        Debug.Log($"[NewspaperManager] {_selectedDefinition.characterName} has arrived!");

        if (timerUI != null) timerUI.SetActive(false);
        if (arrivedUI != null) arrivedUI.SetActive(true);
        if (arrivedText != null)
            arrivedText.text = $"{_selectedDefinition.characterName} has arrived!";

        // Spawn character model if set
        if (_selectedDefinition.characterModelPrefab != null)
            Instantiate(_selectedDefinition.characterModelPrefab);

        OnDateArrived?.Invoke();
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

        // Reset newspaper surface
        if (surface != null)
            surface.ResetSurface();

        // Reset state
        _selectedDefinition = null;
        HideAllUI();

        // Return to table view
        if (scissorsController != null)
            scissorsController.SetEnabled(false);

        SetCamera(tableCamera);
        CurrentState = State.TableView;
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private void SetCamera(CinemachineCamera cam)
    {
        if (cam == null) return;

        if (tableCamera != null)
            tableCamera.Priority = PriorityInactive;
        if (paperCamera != null)
            paperCamera.Priority = PriorityInactive;

        cam.Priority = PriorityActive;
    }

    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(_timeRemaining);
        int min = seconds / 60;
        int sec = seconds % 60;
        timerText.SetText("{0}:{1:00}", min, sec);
    }

    private void HideAllUI()
    {
        if (callingUI != null) callingUI.SetActive(false);
        if (timerUI != null) timerUI.SetActive(false);
        if (arrivedUI != null) arrivedUI.SetActive(false);
    }
}
