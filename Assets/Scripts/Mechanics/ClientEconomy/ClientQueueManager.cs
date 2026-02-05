using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-scoped singleton managing a queue of client orders. The player picks
/// an order, trims a flower, and gets evaluated against the order's requirements.
/// Money and reputation are tracked; rep reaching 0 ends the game.
/// </summary>
[DisallowMultipleComponent]
public class ClientQueueManager : MonoBehaviour
{
    public static ClientQueueManager Instance { get; private set; }

    [Header("Order Pool")]
    [Tooltip("Available orders to draw from.")]
    public List<ClientOrder> orderPool = new List<ClientOrder>();

    [Tooltip("Number of orders shown at once.")]
    public int queueSize = 3;

    [Header("Economy")]
    [Tooltip("Starting money.")]
    public int money;

    [Tooltip("Starting reputation (0-100).")]
    [Range(0, 100)]
    public int reputation = 50;

    [Tooltip("Reputation gained on a successful order.")]
    public int repGainOnSuccess = 5;

    [Tooltip("Reputation lost on a failed order.")]
    public int repLossOnFail = 15;

    [Header("Active Order")]
    [Tooltip("The currently accepted order (null if browsing).")]
    public ClientOrder activeOrder;

    [Header("Events")]
    public UnityEvent<int> OnMoneyChanged;
    public UnityEvent<int> OnReputationChanged;
    public UnityEvent OnShopClosed;

    // Runtime
    private List<ClientOrder> _currentQueue = new List<ClientOrder>();
    public List<ClientOrder> CurrentQueue => _currentQueue;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    // Auto-subscribed sessions
    private List<FlowerSessionController> _subscribedSessions = new List<FlowerSessionController>();

    void OnDestroy()
    {
        for (int i = 0; i < _subscribedSessions.Count; i++)
        {
            if (_subscribedSessions[i] != null)
                _subscribedSessions[i].OnResult.RemoveListener(OnFlowerResult);
        }
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        FillQueue();

        // Auto-subscribe to all flower sessions in the scene
        var allSessions = FindObjectsByType<FlowerSessionController>(FindObjectsSortMode.None);
        for (int i = 0; i < allSessions.Length; i++)
        {
            allSessions[i].OnResult.AddListener(OnFlowerResult);
            _subscribedSessions.Add(allSessions[i]);
        }
    }

    private void OnFlowerResult(FlowerGameBrain.EvaluationResult result, int finalScore, int daysAlive)
    {
        EvaluateAgainstOrder(result, finalScore);
    }

    /// <summary>Accept an order from the queue by index.</summary>
    public void AcceptOrder(int index)
    {
        if (index < 0 || index >= _currentQueue.Count) return;
        if (activeOrder != null) return; // already have one active

        activeOrder = _currentQueue[index];
        Debug.Log($"[ClientQueueManager] Accepted order from {activeOrder.clientName}: {activeOrder.orderText}");
    }

    /// <summary>
    /// Evaluate the current flower result against the active order.
    /// Called after a flower session completes.
    /// </summary>
    public void EvaluateAgainstOrder(FlowerGameBrain.EvaluationResult result, int finalScore)
    {
        if (activeOrder == null) return;

        bool success = !result.isGameOver && result.scoreNormalized >= 0.5f;

        if (success)
        {
            float multiplier = ComboManager.Instance != null ? ComboManager.Instance.GetMultiplier() : 1f;
            int earned = Mathf.RoundToInt(activeOrder.payout * multiplier);
            money += earned;
            reputation = Mathf.Min(100, reputation + repGainOnSuccess);

            Debug.Log($"[ClientQueueManager] Order SUCCESS! +${earned} (x{multiplier:F1}), rep {reputation}");
        }
        else
        {
            reputation = Mathf.Max(0, reputation - repLossOnFail);
            Debug.Log($"[ClientQueueManager] Order FAILED. Rep {reputation}");
        }

        OnMoneyChanged?.Invoke(money);
        OnReputationChanged?.Invoke(reputation);

        // Remove from queue and refill
        _currentQueue.Remove(activeOrder);
        activeOrder = null;
        FillQueue();

        // Check game over
        if (reputation <= 0)
        {
            OnShopClosed?.Invoke();
            Debug.Log("[ClientQueueManager] Shop closed! Reputation hit zero.");
        }
    }

    private void FillQueue()
    {
        if (orderPool.Count == 0) return;

        while (_currentQueue.Count < queueSize)
        {
            var order = orderPool[Random.Range(0, orderPool.Count)];
            _currentQueue.Add(order);
        }
    }
}
