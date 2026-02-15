using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls a date NPC's movement via NavMesh. Spawns at entrance, walks to couch,
/// sits, periodically investigates nearby ReactableTags, returns to couch.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DateCharacterController : MonoBehaviour
{
    public enum CharState { WalkingToJudgmentPoint, WalkingToCouch, Sitting, GettingUp, WalkingToTarget, Investigating, Returning, Dismissed }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Navigation")]
    [Tooltip("Walk speed for the NavMeshAgent.")]
    [SerializeField] private float walkSpeed = 2f;

    [Tooltip("Maximum distance to look for ReactableTags to investigate.")]
    [SerializeField] private float investigateRadius = 6f;

    [Tooltip("Seconds spent investigating an object before returning.")]
    [SerializeField] private float investigateDuration = 4f;

    [Header("Excursions")]
    [Tooltip("Seconds sitting before considering an excursion.")]
    [SerializeField] private float excursionInterval = 15f;

    [Tooltip("Chance of getting up each excursion check (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float excursionChance = 0.4f;

    [Header("Animation Timing")]
    [Tooltip("Seconds for the get-up transition.")]
    [SerializeField] private float getUpDuration = 0.5f;

    // ──────────────────────────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the character reacts to a ReactableTag. DateSessionManager subscribes.
    /// </summary>
    public event Action<ReactableTag, ReactionType> OnReaction;

    /// <summary>
    /// Fired once when the character first sits down on the couch after arriving.
    /// </summary>
    public event Action OnSatDown;

    /// <summary>
    /// Fired when the character reaches the judgment stop point (entrance).
    /// DateSessionManager runs entrance judgments, then calls ContinueToCouch().
    /// </summary>
    public event Action OnReachedJudgmentPoint;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private CharState _state;
    private Transform _couchTarget;
    private Transform _judgmentStopPoint;
    private ReactableTag _currentTarget;
    private float _sitTimer;
    private float _investigateTimer;
    private float _getUpTimer;
    private Action _dismissCallback;
    private Transform _dismissTarget;
    private bool _excursionsEnabled;
    private bool _satDownFired;
    private bool _judgmentPointFired;

    public CharState CurrentState => _state;

    // ──────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────

    /// <summary>Initialize the character after spawning.</summary>
    public void Initialize(Transform couchTarget, Vector3 spawnPos, Transform judgmentPoint = null)
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = walkSpeed;

        // Warp onto NavMesh so agent is active before calling SetDestination
        _agent.enabled = false;
        transform.position = spawnPos;
        _agent.enabled = true;

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
        else
            Debug.LogWarning($"[DateCharacterController] No NavMesh near spawn {spawnPos}");

        _couchTarget = couchTarget;
        _judgmentStopPoint = judgmentPoint;

        if (_judgmentStopPoint != null)
        {
            _state = CharState.WalkingToJudgmentPoint;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(_judgmentStopPoint.position);
            Debug.Log("[DateCharacterController] Walking to judgment point.");
        }
        else
        {
            _state = CharState.WalkingToCouch;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(couchTarget.position);
            Debug.Log("[DateCharacterController] Walking to couch.");
        }
    }

    /// <summary>Resume walking to couch after entrance judgments complete.</summary>
    public void ContinueToCouch()
    {
        _state = CharState.WalkingToCouch;
        if (_couchTarget != null && _agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(_couchTarget.position);
        Debug.Log("[DateCharacterController] Continuing to couch after judgments.");
    }

    /// <summary>Allow the character to start wandering to ReactableTags (Phase 3).</summary>
    public void EnableExcursions()
    {
        _excursionsEnabled = true;
        _sitTimer = 0f;
        Debug.Log("[DateCharacterController] Excursions enabled.");
    }

    /// <summary>Force the character to investigate a specific location (e.g. drink delivery).</summary>
    public void InvestigateSpecific(Transform target)
    {
        if (_state == CharState.Dismissed) return;

        _currentTarget = target.GetComponent<ReactableTag>();
        _state = CharState.GettingUp;
        _getUpTimer = 0f;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(target.position);
    }

    /// <summary>Dismiss the character — walks to exit, then calls onComplete.</summary>
    public void Dismiss(Transform exitPoint, Action onComplete)
    {
        _dismissCallback = onComplete;
        _dismissTarget = exitPoint;
        _state = CharState.Dismissed;

        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            _agent.SetDestination(exitPoint.position);
        else
            onComplete?.Invoke();

        Debug.Log("[DateCharacterController] Dismissing — walking to exit.");
    }

    // ──────────────────────────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        switch (_state)
        {
            case CharState.WalkingToJudgmentPoint:
                if (!_judgmentPointFired && HasArrived())
                {
                    _judgmentPointFired = true;
                    Debug.Log("[DateCharacterController] Reached judgment point.");
                    OnReachedJudgmentPoint?.Invoke();
                    // Stays here until ContinueToCouch() is called
                }
                break;

            case CharState.WalkingToCouch:
                if (HasArrived())
                {
                    _state = CharState.Sitting;
                    _sitTimer = 0f;
                    if (!_satDownFired)
                    {
                        _satDownFired = true;
                        OnSatDown?.Invoke();
                    }
                    Debug.Log("[DateCharacterController] Sat down on couch.");
                }
                break;

            case CharState.Sitting:
                if (!_excursionsEnabled) break;
                _sitTimer += Time.deltaTime;
                if (_sitTimer >= excursionInterval)
                {
                    _sitTimer = 0f;
                    TryExcursion();
                }
                break;

            case CharState.GettingUp:
                _getUpTimer += Time.deltaTime;
                if (_getUpTimer >= getUpDuration)
                {
                    _state = CharState.WalkingToTarget;
                }
                break;

            case CharState.WalkingToTarget:
                if (HasArrived())
                {
                    _state = CharState.Investigating;
                    _investigateTimer = 0f;
                    Debug.Log("[DateCharacterController] Investigating...");
                }
                break;

            case CharState.Investigating:
                _investigateTimer += Time.deltaTime;

                // Notice phase (question mark)
                if (_investigateTimer >= 0.5f && _investigateTimer < 0.5f + Time.deltaTime)
                {
                    // "What's this?" moment — fire ? reaction
                }

                // Opinion phase
                if (_investigateTimer >= 2.0f && _investigateTimer < 2.0f + Time.deltaTime)
                {
                    EvaluateCurrentTarget();
                }

                // Done investigating
                if (_investigateTimer >= investigateDuration)
                {
                    _state = CharState.Returning;
                    _currentTarget = null;
                    if (_couchTarget != null)
                        _agent.SetDestination(_couchTarget.position);
                }
                break;

            case CharState.Returning:
                if (HasArrived())
                {
                    _state = CharState.Sitting;
                    _sitTimer = 0f;
                }
                break;

            case CharState.Dismissed:
                if (HasArrived())
                {
                    _dismissCallback?.Invoke();
                    _dismissCallback = null;
                }
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────────────────

    private bool HasArrived()
    {
        if (_agent.pathPending) return false;
        return _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
    }

    private void TryExcursion()
    {
        if (UnityEngine.Random.value > excursionChance) return;

        // Find a random active ReactableTag within radius
        var candidates = new List<ReactableTag>();
        foreach (var tag in ReactableTag.All)
        {
            if (!tag.IsActive) continue;
            float dist = Vector3.Distance(transform.position, tag.transform.position);
            if (dist <= investigateRadius)
                candidates.Add(tag);
        }

        if (candidates.Count == 0) return;

        _currentTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        _state = CharState.GettingUp;
        _getUpTimer = 0f;

        if (_agent.isOnNavMesh)
            _agent.SetDestination(_currentTarget.transform.position);

        Debug.Log($"[DateCharacterController] Excursion to {_currentTarget.gameObject.name}");
    }

    private void EvaluateCurrentTarget()
    {
        if (_currentTarget == null) return;

        var dateSession = DateSessionManager.Instance;
        if (dateSession == null || dateSession.CurrentDate == null) return;

        var reaction = ReactionEvaluator.EvaluateReactable(_currentTarget, dateSession.CurrentDate.preferences);
        OnReaction?.Invoke(_currentTarget, reaction);

        Debug.Log($"[DateCharacterController] Reacted to {_currentTarget.gameObject.name}: {reaction}");
    }
}
