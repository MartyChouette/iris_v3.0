using UnityEngine;

/// <summary>
/// Scene-scoped singleton that aggregates per-area tidiness from three signals:
///   1. Stain cleanliness (CleaningManager)
///   2. Object mess (misplaced PlaceableObjects with non-General category)
///   3. Smell (sum of ReactableTag.SmellAmount in area)
///
/// Each area score ranges 0 (filthy) to 1 (spotless).
/// Uses PlaceableObject.All static registry instead of FindObjectsByType.
/// </summary>
public class TidyScorer : MonoBehaviour
{
    public static TidyScorer Instance { get; private set; }

    [Header("Weights")]
    [Tooltip("Weight for stain cleanliness in the tidiness formula.")]
    [SerializeField] private float _stainWeight = 0.45f;

    [Tooltip("Weight for object mess in the tidiness formula.")]
    [SerializeField] private float _objectWeight = 0.25f;

    [Tooltip("Weight for smell cleanliness in the tidiness formula.")]
    [SerializeField] private float _smellWeight = 0.15f;

    [Tooltip("Weight for floor clutter in the tidiness formula.")]
    [SerializeField] private float _clutterWeight = 0.15f;

    [Header("Thresholds")]
    [Tooltip("Maximum expected mess items per area before objectClean = 0.")]
    [SerializeField] private int _maxExpectedMess = 3;

    [Tooltip("Maximum expected floor items per area before clutterClean = 0.")]
    [SerializeField] private int _maxExpectedClutter = 3;

    [Tooltip("Smell amount per area above which smellClean = 0.")]
    [SerializeField] private float _smellThreshold = 1.5f;

    [Header("Area Bounds (world-space X)")]
    [Tooltip("X boundary between Kitchen (lower) and LivingRoom (higher).")]
    [SerializeField] private float _kitchenLivingBoundaryX = -1f;

    [Tooltip("Z boundary: Entrance is Z < this value (entrance is at negative Z).")]
    [SerializeField] private float _entranceBoundaryZ = -4.5f;

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
        float clutterClean = GetClutterClean(area);

        return stainClean * _stainWeight
             + objectClean * _objectWeight
             + smellClean * _smellWeight
             + clutterClean * _clutterWeight;
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
        if (worldPos.z < _entranceBoundaryZ)
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
        var placeables = PlaceableObject.All;
        for (int i = 0; i < placeables.Count; i++)
        {
            var p = placeables[i];
            if (p.Category == ItemCategory.General) continue;
            if (p.IsAtHome) continue;
            if (p.CurrentState == PlaceableObject.State.Held) continue;
            if (ClassifyPosition(p.transform.position) == area)
                messCount++;
        }

        return 1f - Mathf.Clamp01((float)messCount / _maxExpectedMess);
    }

    /// <summary>Clutter cleanliness for an area: items on the floor that aren't on surfaces.</summary>
    public float GetClutterClean(ApartmentArea area)
    {
        int floorItems = 0;
        var placeables = PlaceableObject.All;
        for (int i = 0; i < placeables.Count; i++)
        {
            var p = placeables[i];
            if (!p.IsOnFloor) continue;
            if (p.IsAtHome) continue;
            if (ClassifyPosition(p.transform.position) == area)
                floorItems++;
        }

        return 1f - Mathf.Clamp01((float)floorItems / _maxExpectedClutter);
    }

    /// <summary>Count of floor items in an area (for debug overlay).</summary>
    public int GetFloorItemCount(ApartmentArea area)
    {
        int count = 0;
        var placeables = PlaceableObject.All;
        for (int i = 0; i < placeables.Count; i++)
        {
            var p = placeables[i];
            if (!p.IsOnFloor) continue;
            if (p.IsAtHome) continue;
            if (ClassifyPosition(p.transform.position) == area)
                count++;
        }
        return count;
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
