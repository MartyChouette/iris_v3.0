using UnityEngine;

/// <summary>
/// Attached to a BookVolume. When the reader flips to a page containing a hidden
/// collectible, reveals it and records the discovery.
/// </summary>
public class BookCollectibleRevealer : MonoBehaviour
{
    [Header("Hidden Collectibles")]
    [Tooltip("Mapping of page indices to collectible SOs hidden in this book.")]
    [SerializeField] private BookCollectibleEntry[] _collectibles;

    [Header("Narrative Gate")]
    [Tooltip("Optional condition that must be met before collectibles appear.")]
    [SerializeField] private NarrativeCondition _gateCondition;

    private BookVolume _volume;

    private void Awake()
    {
        _volume = GetComponent<BookVolume>();
    }

    private void OnEnable()
    {
        BookVolume.OnPageViewed += HandlePageViewed;
    }

    private void OnDisable()
    {
        BookVolume.OnPageViewed -= HandlePageViewed;
    }

    private void HandlePageViewed(BookDefinition book, int pageIndex)
    {
        // Only respond to our own book
        if (_volume == null || _volume.Definition != book) return;

        // Check narrative gate
        if (!NarrativeGateEvaluator.Evaluate(_gateCondition)) return;

        if (_collectibles == null) return;

        foreach (var entry in _collectibles)
        {
            if (entry.pageIndex != pageIndex) continue;

            string collectibleId = null;
            string collectibleType = null;
            string message = null;

            if (entry.photo != null)
            {
                collectibleId = entry.photo.photoId;
                collectibleType = "photo";
                message = $"You found a photograph! {entry.photo.description}";
            }
            else if (entry.pressedFlower != null)
            {
                collectibleId = entry.pressedFlower.flowerId;
                collectibleType = "pressed_flower";
                message = $"You found a pressed flower! {entry.pressedFlower.flowerName} — {entry.pressedFlower.meaning}";
            }

            if (string.IsNullOrEmpty(collectibleId)) continue;

            int day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
            bool isNew = CollectibleRegistry.Discover(collectibleId, collectibleType, day);

            if (isNew)
            {
                Debug.Log($"[BookCollectibleRevealer] {message}");

                // Show via Nema thought bubble if available
                if (NemaThoughtBubble.Instance != null)
                    NemaThoughtBubble.Instance.ShowThought(message, ThoughtMood.Insight);
            }
        }
    }
}

/// <summary>
/// A single collectible entry hidden at a specific page in a book.
/// </summary>
[System.Serializable]
public class BookCollectibleEntry
{
    [Tooltip("Page index (0-based) where this collectible is found.")]
    public int pageIndex;

    [Tooltip("Photo collectible (set one or the other).")]
    public PhotoDefinition photo;

    [Tooltip("Pressed flower collectible (set one or the other).")]
    public PressedFlowerDefinition pressedFlower;
}
