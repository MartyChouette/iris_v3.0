using UnityEngine;

/// <summary>
/// Manages procedural stain placement each day.
/// Pre-placed disabled CleanableSurface quads are randomly activated with
/// random SpillDefinitions. CleaningManager surfaces array is updated.
/// </summary>
public class ApartmentStainSpawner : MonoBehaviour
{
    public static ApartmentStainSpawner Instance { get; private set; }

    [Header("Stain Slots")]
    [Tooltip("Pre-placed disabled CleanableSurface quads at various apartment positions.")]
    [SerializeField] private CleanableSurface[] _stainSlots;

    [Header("Spill Pool")]
    [Tooltip("Variety of dirt/spill types to randomly assign.")]
    [SerializeField] private SpillDefinition[] _spillPool;

    [Header("Settings")]
    [Tooltip("How many stains to activate each day.")]
    [SerializeField, Range(1, 8)] private int _stainsPerDay = 4;

    [Header("References")]
    [Tooltip("CleaningManager to update with active surfaces.")]
    [SerializeField] private CleaningManager _cleaningManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ApartmentStainSpawner] Duplicate instance destroyed.");
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
        // Auto-spawn when DayPhaseManager isn't driving the flow, or when it
        // exists but is already past Morning (editor play / jumped into scene).
        bool dpmPresent = DayPhaseManager.Instance != null;
        bool dpmPastMorning = dpmPresent
            && DayPhaseManager.Instance.CurrentPhase != DayPhaseManager.DayPhase.Morning;

        if (!dpmPresent || dpmPastMorning)
        {
            Debug.Log("[ApartmentStainSpawner] Auto-spawning stains (no DPM or already past morning).");
            SpawnDailyStains();
        }
    }

    /// <summary>
    /// Deactivate all slots, pick a random subset, assign random SpillDefinitions,
    /// regenerate textures, activate, and update CleaningManager.
    /// </summary>
    public void SpawnDailyStains()
    {
        if (_stainSlots == null || _stainSlots.Length == 0) return;
        if (_spillPool == null || _spillPool.Length == 0) return;

        // Deactivate all
        for (int i = 0; i < _stainSlots.Length; i++)
        {
            if (_stainSlots[i] != null)
                _stainSlots[i].gameObject.SetActive(false);
        }

        // Fisher-Yates shuffle indices
        int[] indices = new int[_stainSlots.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = Mathf.Min(_stainsPerDay, _stainSlots.Length);
        var activeSlots = new CleanableSurface[count];

        for (int i = 0; i < count; i++)
        {
            var slot = _stainSlots[indices[i]];
            if (slot == null) continue;

            // Assign random definition
            var def = _spillPool[Random.Range(0, _spillPool.Length)];
            slot.SetDefinition(def);

            // Activate and regenerate
            slot.gameObject.SetActive(true);
            slot.Regenerate();

            activeSlots[i] = slot;
        }

        // Update CleaningManager surfaces
        if (_cleaningManager != null)
            _cleaningManager.SetSurfaces(activeSlots);

        Debug.Log($"[ApartmentStainSpawner] Spawned {count} stains for today.");
    }
}
