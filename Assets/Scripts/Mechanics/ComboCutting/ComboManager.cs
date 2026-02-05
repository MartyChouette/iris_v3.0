using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-scoped singleton that tracks combo chains across flower evaluations.
/// Successful evaluations increment the combo and multiplier; game-overs or
/// timer expiry break the chain.
/// </summary>
[DisallowMultipleComponent]
public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [Header("Discovery")]
    [Tooltip("Automatically find and register all FlowerSessionControllers in the scene on Start.")]
    public bool autoDiscoverSessions = true;

    [Header("Combo Settings")]
    [Tooltip("Seconds the player has to score again before the combo breaks.")]
    public float comboWindow = 8f;

    [Tooltip("Multiplier added per successful combo.")]
    public float multiplierPerCombo = 0.25f;

    [Tooltip("Maximum multiplier cap.")]
    public float maxMultiplier = 4f;

    [Header("Events")]
    [Tooltip("Fired when the combo changes. Args: (comboCount, multiplier).")]
    public UnityEvent<int, float> OnComboChanged;

    [Tooltip("Fired when the combo chain breaks.")]
    public UnityEvent OnComboBroken;

    [Header("Runtime (Read-Only)")]
    public int comboCount;
    public float comboMultiplier = 1f;
    public float comboTimer;

    // Tracked sessions
    private List<FlowerSessionController> _sessions = new List<FlowerSessionController>();

    public float GetMultiplier() => comboMultiplier;

    void Start()
    {
        if (autoDiscoverSessions)
        {
            var allSessions = FindObjectsByType<FlowerSessionController>(FindObjectsSortMode.None);
            for (int i = 0; i < allSessions.Length; i++)
                RegisterFlower(allSessions[i]);

            if (_sessions.Count > 0)
                Debug.Log($"[ComboManager] Auto-discovered {_sessions.Count} flower session(s).");
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        // Unsubscribe from all sessions
        for (int i = 0; i < _sessions.Count; i++)
        {
            if (_sessions[i] != null)
                _sessions[i].OnResult.RemoveListener(OnFlowerResult);
        }

        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Register a flower session so the combo manager tracks its results.
    /// Call this for each flower in the scene.
    /// </summary>
    public void RegisterFlower(FlowerSessionController session)
    {
        if (session == null || _sessions.Contains(session)) return;
        _sessions.Add(session);
        session.OnResult.AddListener(OnFlowerResult);
    }

    void Update()
    {
        if (comboCount <= 0) return;

        comboTimer += Time.deltaTime;
        if (comboTimer >= comboWindow)
        {
            BreakCombo();
        }
    }

    private void OnFlowerResult(FlowerGameBrain.EvaluationResult result, int finalScore, int daysAlive)
    {
        if (result.isGameOver)
        {
            BreakCombo();
        }
        else
        {
            IncrementCombo();
        }
    }

    private void IncrementCombo()
    {
        comboCount++;
        comboMultiplier = Mathf.Min(1f + comboCount * multiplierPerCombo, maxMultiplier);
        comboTimer = 0f;

        OnComboChanged?.Invoke(comboCount, comboMultiplier);
        Debug.Log($"[ComboManager] Combo x{comboCount}, multiplier {comboMultiplier:F2}x");
    }

    private void BreakCombo()
    {
        if (comboCount <= 0) return;

        comboCount = 0;
        comboMultiplier = 1f;
        comboTimer = 0f;

        OnComboBroken?.Invoke();
        Debug.Log("[ComboManager] Combo broken.");
    }
}
