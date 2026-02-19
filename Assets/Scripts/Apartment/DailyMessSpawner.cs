using UnityEngine;

/// <summary>
/// Scene-scoped singleton. Each morning, spawns trash items at random positions
/// and repositions entrance items (shoes, coat, hat) to random wrong locations.
/// Fisher-Yates shuffle pattern matching ApartmentStainSpawner.
/// </summary>
public class DailyMessSpawner : MonoBehaviour
{
    public static DailyMessSpawner Instance { get; private set; }

    [Header("Trash Spawning")]
    [Tooltip("Pre-placed disabled trash GameObjects at various apartment positions.")]
    [SerializeField] private GameObject[] _trashSlots;

    [Tooltip("How many trash items to activate each day.")]
    [SerializeField, Range(1, 8)] private int _trashPerDay = 3;

    [Header("Entrance Items")]
    [Tooltip("Shoes PlaceableObject — repositioned each morning.")]
    [SerializeField] private PlaceableObject _shoes;

    [Tooltip("Coat PlaceableObject — repositioned each morning.")]
    [SerializeField] private PlaceableObject _coat;

    [Tooltip("Hat PlaceableObject — repositioned each morning.")]
    [SerializeField] private PlaceableObject _hat;

    [Header("Misplacement Positions")]
    [Tooltip("Transforms marking random wrong positions where entrance items can be placed.")]
    [SerializeField] private Transform[] _wrongPositions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DailyMessSpawner] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Auto-spawn when DayPhaseManager isn't driving the flow (e.g. jumping
        // directly into the apartment scene from the editor).
        if (DayPhaseManager.Instance == null)
        {
            Debug.Log("[DailyMessSpawner] No DayPhaseManager — auto-spawning mess.");
            SpawnDailyMess();
        }
    }

    /// <summary>
    /// Spawn daily mess: activate trash subset + misplace entrance items.
    /// Called by DayPhaseManager during ExplorationTransition.
    /// </summary>
    public void SpawnDailyMess()
    {
        SpawnTrash();
        MisplaceEntranceItems();
        Debug.Log("[DailyMessSpawner] Daily mess spawned.");
    }

    private void SpawnTrash()
    {
        if (_trashSlots == null || _trashSlots.Length == 0) return;

        // Deactivate all
        for (int i = 0; i < _trashSlots.Length; i++)
        {
            if (_trashSlots[i] != null)
                _trashSlots[i].SetActive(false);
        }

        // Fisher-Yates shuffle indices
        int[] indices = new int[_trashSlots.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = Mathf.Min(_trashPerDay, _trashSlots.Length);
        for (int i = 0; i < count; i++)
        {
            var slot = _trashSlots[indices[i]];
            if (slot != null)
            {
                slot.SetActive(true);

                // Reset PlaceableObject state
                var placeable = slot.GetComponent<PlaceableObject>();
                if (placeable != null)
                    placeable.IsAtHome = false;
            }
        }

        Debug.Log($"[DailyMessSpawner] Activated {count} trash items.");
    }

    private void MisplaceEntranceItems()
    {
        if (_wrongPositions == null || _wrongPositions.Length == 0) return;

        // Fisher-Yates shuffle wrong positions
        int[] indices = new int[_wrongPositions.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int idx = 0;
        MisplaceItem(_shoes, ref idx, indices);
        MisplaceItem(_coat, ref idx, indices);
        MisplaceItem(_hat, ref idx, indices);
    }

    private void MisplaceItem(PlaceableObject item, ref int posIndex, int[] shuffledIndices)
    {
        if (item == null || _wrongPositions == null) return;
        if (posIndex >= shuffledIndices.Length) return;

        var targetTransform = _wrongPositions[shuffledIndices[posIndex]];
        posIndex++;

        if (targetTransform == null) return;

        item.gameObject.SetActive(true);
        item.transform.position = targetTransform.position;
        item.transform.rotation = targetTransform.rotation;
        item.IsAtHome = false;

        // Reset rigidbody
        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Debug.Log($"[DailyMessSpawner] Misplaced {item.name} to {targetTransform.name}.");
    }
}
