using TMPro;
using UnityEngine;

/// <summary>
/// UI for the client order queue. Shows available orders, active order details,
/// and running money/reputation totals.
/// </summary>
[DisallowMultipleComponent]
public class ClientOrderUI : MonoBehaviour
{
    [Header("References")]
    public ClientQueueManager queue;
    public ComboManager combo;

    [Header("Economy Display")]
    public TMP_Text moneyLabel;
    public TMP_Text repLabel;

    [Header("Order Slots")]
    [Tooltip("Text elements for each queue slot (up to 3).")]
    public TMP_Text[] orderSlotTexts;

    [Header("Active Order")]
    public TMP_Text activeOrderText;
    public CanvasGroup orderPanel;

    void Update()
    {
        if (queue == null) return;

        // Money & Rep
        if (moneyLabel != null)
            moneyLabel.SetText("${0}", queue.money);

        if (repLabel != null)
            repLabel.SetText("Rep: {0}", queue.reputation);

        // Queue slots
        var currentQueue = queue.CurrentQueue;
        for (int i = 0; i < orderSlotTexts.Length; i++)
        {
            if (orderSlotTexts[i] == null) continue;

            if (i < currentQueue.Count)
            {
                var order = currentQueue[i];
                orderSlotTexts[i].text = $"{order.clientName}\n{order.orderText}\n${order.payout}";
                orderSlotTexts[i].gameObject.SetActive(true);
            }
            else
            {
                orderSlotTexts[i].gameObject.SetActive(false);
            }
        }

        // Active order
        if (activeOrderText != null)
        {
            if (queue.activeOrder != null)
            {
                activeOrderText.text = $"ORDER: {queue.activeOrder.clientName}\n{queue.activeOrder.orderText}";
                activeOrderText.gameObject.SetActive(true);
            }
            else
            {
                activeOrderText.text = "Select an order...";
            }
        }
    }

    /// <summary>Called by UI buttons to accept an order at the given index.</summary>
    public void OnOrderSlotClicked(int index)
    {
        if (queue != null)
            queue.AcceptOrder(index);
    }
}
