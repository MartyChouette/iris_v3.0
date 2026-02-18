using UnityEngine;

/// <summary>
/// Scene-scoped singleton that aggregates per-area tidiness from three signals:
///   1. Stain cleanliness (CleaningManager)
///   2. Object mess (misplaced PlaceableObjects with non-General category)
///   3. Smell (sum of ReactableTag.SmellAmount in area)
///
/// Each area score ranges 0 (filthy) to 1 (spotless).
/// </summary>
public class TidyScorer : MonoBehaviour
{
    public static TidyScorer Instance { get; private set; }

    [Header("Weights")]
    [Tooltip("Weight for stain cleanliness in the tidiness formula.")]
    [SerializeField] private float _stainWeight = 0.4f;

    [Tooltip("Weight for object mess in the tidiness formula.")]
    [SerializeField] private float _objectWeight = 0.4f;

    [Tooltip("Weight for smell cleanliness in the tidiness formula.")]
    [SerializeField] private float _smellWeight = 0.2f;

    [Header("Thresholds")]
    [Tooltip("Maximum expected mess items per area before objectClean = 0.")]
    [SerializeField] private int _maxExpectedMess = 4;

    [Tooltip("Smell amount per area above which smellClean = 0.")]
    [SerializeField] private float _smellThreshold = 1.5f;

    [Header("Area Bounds (world-space X)")]
    [Tooltip("X boundary between Kitchen (lower) and LivingRoom (higher).")]
    [SerializeField] private float _kitchenLivingBoundaryX = -1f;

    [Tooltip("Z boundary: Entrance is Z > this value.")]
    [SerializeField] private float _entranceBoundaryZ = 4f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TidyScorer] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Tidiness of a specific area (0 = filthy, 1 = spotless).</summary>
    public float GetAreaTidiness(ApartmentArea area)
    {
        float stainClean = GetStainClean(area);
        float objectClean = GetObjectClean(area);
        float smellClean = GetSmellClean(area);

        return stainClean * _stainWeight + objectClean * _objectWeight + smellClean * _smellWeight;
    }

    /// <summary>Average tidiness across all areas.</summary>
    public float OverallTidiness
    {
        get
        {
            float sum = 0f;
            int count = 0;
            foreach (ApartmentArea area in System.Enum.GetValues(typeof(ApartmentArea)))
            {
                sum += GetAreaTidiness(area);
                count++;
            }
            return count > 0 ? sum / count : 1f;
        }
    }

    /// <summary>Assign an area based on an object's world position.</summary>
    public ApartmentArea ClassifyPosition(Vector3 worldPos)
    {
        if (worldPos.z > _entranceBoundaryZ)
            return ApartmentArea.Entrance;
        if (worldPos.x < _kitchenLivingBoundaryX)
            return ApartmentArea.Kitchen;
        return ApartmentArea.LivingRoom;
    }

    private float GetStainClean(ApartmentArea area)
    {
        if (CleaningManager.Instance == null) return 1f;
        return CleaningManager.Instance.GetAreaCleanPercent(area);
    }

    private float GetObjectClean(ApartmentArea area)
    {
        int messCount = 0;
        var placeables = FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None);
        foreach (var p in placeables)
        {
            if (p.Category == ItemCategory.General) continue;
            if (p.IsAtHome) continue;
            if (p.CurrentState == PlaceableObject.State.Held) continue;

            // Check if this object is in the target area
            if (ClassifyPosition(p.transform.position) == area)
                messCount++;
        }

        return 1f - Mathf.Clamp01((float)messCount / _maxExpectedMess);
    }

    private float GetSmellClean(ApartmentArea area)
    {
        float totalSmell = 0f;
        foreach (var tag in ReactableTag.All)
        {
            if (tag.SmellAmount <= 0f) continue;
            if (ClassifyPosition(tag.transform.position) == area)
                totalSmell += tag.SmellAmount;
        }

        return 1f - Mathf.Clamp01(totalSmell / _smellThreshold);
    }
}
