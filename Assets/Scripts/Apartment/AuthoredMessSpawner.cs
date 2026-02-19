using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-scoped singleton. Each morning, filters MessBlueprint SOs by
/// date outcome conditions and spawns an authored subset of stains and objects.
/// Replaces the random stain/trash spawning of ApartmentStainSpawner + DailyMessSpawner.SpawnTrash.
/// </summary>
public class AuthoredMessSpawner : MonoBehaviour
{
    public static AuthoredMessSpawner Instance { get; private set; }

    [Header("Blueprints")]
    [Tooltip("All available mess blueprints loaded from ScriptableObjects.")]
    [SerializeField] private MessBlueprint[] _allBlueprints;

    [Header("Stain Slots")]
    [Tooltip("Pre-placed disabled CleanableSurface quads for stain messes.")]
    [SerializeField] private CleanableSurface[] _stainSlots;

    [Header("Object Slots")]
    [Tooltip("Transforms marking positions where object messes can be placed.")]
    [SerializeField] private Transform[] _objectSlots;

    [Header("Limits")]
    [Tooltip("Maximum stain messes to spawn per day.")]
    [SerializeField, Range(1, 8)] private int _maxStainsPerDay = 4;

    [Tooltip("Maximum object messes to spawn per day.")]
    [SerializeField, Range(1, 8)] private int _maxObjectsPerDay = 3;

    [Header("References")]
    [Tooltip("CleaningManager to update with active stain surfaces.")]
    [SerializeField] private CleaningManager _cleaningManager;

    [Tooltip("Layer for spawned mess objects (placeableLayer).")]
    [SerializeField] private int _objectLayer;

    // Track spawned objects for debug overlay
    private readonly List<string> _spawnedBlueprintNames = new();
    private readonly List<GameObject> _spawnedObjects = new();

    public IReadOnlyList<string> SpawnedBlueprintNames => _spawnedBlueprintNames;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AuthoredMessSpawner] Duplicate instance destroyed.");
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
        // Auto-spawn when DayPhaseManager isn't driving the flow
        if (DayPhaseManager.Instance == null)
        {
            Debug.Log("[AuthoredMessSpawner] No DayPhaseManager — auto-spawning mess.");
            SpawnDailyMess();
        }
    }

    /// <summary>
    /// Filter blueprints by conditions, then spawn a weighted random subset.
    /// Called by DayPhaseManager during ExplorationTransition.
    /// </summary>
    public void SpawnDailyMess()
    {
        _spawnedBlueprintNames.Clear();
        CleanUpPreviousObjects();

        var outcome = DateOutcomeCapture.LastOutcome;
        int currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;

        // Filter eligible blueprints
        var eligibleStains = new List<MessBlueprint>();
        var eligibleObjects = new List<MessBlueprint>();

        if (_allBlueprints != null)
        {
            foreach (var bp in _allBlueprints)
            {
                if (bp == null) continue;
                if (!IsEligible(bp, outcome, currentDay)) continue;

                if (bp.messType == MessBlueprint.MessType.Stain)
                    eligibleStains.Add(bp);
                else
                    eligibleObjects.Add(bp);
            }
        }

        // Deactivate all stain slots
        if (_stainSlots != null)
        {
            for (int i = 0; i < _stainSlots.Length; i++)
            {
                if (_stainSlots[i] != null)
                    _stainSlots[i].gameObject.SetActive(false);
            }
        }

        // Spawn stains
        var selectedStains = WeightedSelect(eligibleStains, _maxStainsPerDay);
        var activeSlots = new List<CleanableSurface>();
        int stainSlotIdx = 0;

        foreach (var bp in selectedStains)
        {
            if (_stainSlots == null || stainSlotIdx >= _stainSlots.Length) break;
            var slot = _stainSlots[stainSlotIdx++];
            if (slot == null) continue;
            if (bp.spillDefinition == null) continue;

            slot.SetDefinition(bp.spillDefinition);
            slot.gameObject.SetActive(true);
            slot.Regenerate();
            activeSlots.Add(slot);
            _spawnedBlueprintNames.Add(bp.messName);
        }

        // Update CleaningManager with active stain slots
        if (_cleaningManager != null)
            _cleaningManager.SetSurfaces(activeSlots.ToArray());

        // Spawn objects
        var selectedObjects = WeightedSelect(eligibleObjects, _maxObjectsPerDay);
        int objSlotIdx = 0;

        foreach (var bp in selectedObjects)
        {
            if (_objectSlots == null || objSlotIdx >= _objectSlots.Length) break;
            var slotTransform = _objectSlots[objSlotIdx++];
            if (slotTransform == null) continue;

            var go = SpawnMessObject(bp, slotTransform.position);
            if (go != null)
            {
                _spawnedObjects.Add(go);
                _spawnedBlueprintNames.Add(bp.messName);
            }
        }

        // Clear date outcome after spawning
        DateOutcomeCapture.ClearForNewDay();

        Debug.Log($"[AuthoredMessSpawner] Spawned {selectedStains.Count} stains, " +
                  $"{selectedObjects.Count} objects from {_spawnedBlueprintNames.Count} blueprints.");
    }

    private bool IsEligible(MessBlueprint bp, DateOutcomeCapture.DateOutcome outcome, int currentDay)
    {
        // Day check
        if (currentDay < bp.minDay) return false;

        // Flower trim conditions
        if (bp.requireBadFlowerTrim)
        {
            if (!outcome.hadFlowerTrim || outcome.flowerScore >= 40) return false;
        }
        if (bp.requireGoodFlowerTrim)
        {
            if (!outcome.hadFlowerTrim || outcome.flowerScore < 80) return false;
        }

        // DateAftermath conditions
        if (bp.category == MessBlueprint.MessCategory.DateAftermath)
        {
            if (!outcome.hadDate) return false;
            if (bp.requireDateSuccess && !outcome.succeeded) return false;
            if (bp.requireDateFailure && outcome.succeeded) return false;
            if (outcome.affection < bp.minAffection) return false;
            if (outcome.affection > bp.maxAffection) return false;

            if (!string.IsNullOrEmpty(bp.requireReactionTag))
            {
                bool found = false;
                if (outcome.reactionTags != null)
                {
                    foreach (var tag in outcome.reactionTags)
                    {
                        if (tag.Contains(bp.requireReactionTag, System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                }
                if (!found) return false;
            }
        }

        return true;
    }

    /// <summary>Weighted random selection without replacement using Fisher-Yates.</summary>
    private List<MessBlueprint> WeightedSelect(List<MessBlueprint> pool, int maxCount)
    {
        var result = new List<MessBlueprint>();
        if (pool.Count == 0) return result;

        // Build weighted list (repeat entries proportional to weight)
        var weighted = new List<MessBlueprint>();
        foreach (var bp in pool)
        {
            int copies = Mathf.Max(1, Mathf.RoundToInt(bp.weight * 2f));
            for (int i = 0; i < copies; i++)
                weighted.Add(bp);
        }

        // Fisher-Yates shuffle
        for (int i = weighted.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (weighted[i], weighted[j]) = (weighted[j], weighted[i]);
        }

        // Take unique entries up to maxCount
        var seen = new HashSet<MessBlueprint>();
        foreach (var bp in weighted)
        {
            if (seen.Contains(bp)) continue;
            seen.Add(bp);
            result.Add(bp);
            if (result.Count >= maxCount) break;
        }

        return result;
    }

    private GameObject SpawnMessObject(MessBlueprint bp, Vector3 position)
    {
        GameObject go;

        if (bp.objectPrefab != null)
        {
            go = Instantiate(bp.objectPrefab, position, Quaternion.identity);
        }
        else
        {
            // Procedural box
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = bp.objectScale;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = bp.objectColor;
            }
        }

        go.name = bp.messName.Replace(" ", "_");
        if (_objectLayer > 0)
            go.layer = _objectLayer;

        // Add Rigidbody
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.05f;

        // Add PlaceableObject as Trash
        var po = go.GetComponent<PlaceableObject>();
        if (po == null) po = go.AddComponent<PlaceableObject>();

        // Use description from blueprint
        var poSO = new UnitySerializedHelper(po);
        poSO.SetEnum("_itemCategory", (int)ItemCategory.Trash);
        poSO.SetString("_homeZoneName", "TrashCan");
        poSO.SetString("_itemDescription", !string.IsNullOrEmpty(bp.description) ? bp.description : bp.messName);

        // Add InteractableHighlight
        if (go.GetComponent<InteractableHighlight>() == null)
            go.AddComponent<InteractableHighlight>();

        // Add ReactableTag
        var reactable = go.GetComponent<ReactableTag>();
        if (reactable == null)
        {
            reactable = go.AddComponent<ReactableTag>();
            // tags is a private serialized field — set via reflection at runtime
            var tagsField = typeof(ReactableTag).GetField("tags",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tagsField != null) tagsField.SetValue(reactable, new[] { "trash", "mess" });
            reactable.IsPrivate = true;
            reactable.SmellAmount = 0.2f;
        }

        return go;
    }

    private void CleanUpPreviousObjects()
    {
        foreach (var go in _spawnedObjects)
        {
            if (go != null)
                Destroy(go);
        }
        _spawnedObjects.Clear();
    }

    /// <summary>
    /// Minimal runtime helper for setting serialized private fields on components.
    /// In builds, falls back to reflection.
    /// </summary>
    private class UnitySerializedHelper
    {
        private readonly Component _target;

        public UnitySerializedHelper(Component target) => _target = target;

        public void SetEnum(string fieldName, int value)
        {
            var field = _target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(_target, value);
        }

        public void SetString(string fieldName, string value)
        {
            var field = _target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(_target, value);
        }
    }
}
