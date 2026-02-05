using UnityEngine;

/// <summary>
/// ScriptableObject defining a client's flower order: requirements and payout.
/// The <see cref="ClientQueueManager"/> interprets these against evaluation results.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Client Order")]
public class ClientOrder : ScriptableObject
{
    [Header("Client")]
    [Tooltip("Name of the client placing the order.")]
    public string clientName;

    [Tooltip("Display description of what the client wants.")]
    [TextArea(2, 4)]
    public string orderText;

    [Header("Payout")]
    [Tooltip("Base money earned on success.")]
    public int payout = 50;

    [Header("Difficulty")]
    public FlowerTypeDefinition.Difficulty difficulty = FlowerTypeDefinition.Difficulty.Normal;

    [Header("Requirements")]
    [Tooltip("Ideal stem length the client wants.")]
    public float idealStemLength = 0.5f;

    [Tooltip("Ideal cut angle the client wants (degrees).")]
    public float idealCutAngle = 45f;

    [Tooltip("Minimum leaves required (0 = any).")]
    public int minLeavesRequired;

    [Tooltip("Maximum leaves allowed (20 = any).")]
    public int maxLeavesAllowed = 20;

    [Tooltip("All petals must be present.")]
    public bool requireAllPetals;

    [Tooltip("Withered parts are acceptable.")]
    public bool allowWithered = true;
}
