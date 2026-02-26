using UnityEngine;

/// <summary>
/// Lightweight companion to PlaceableObject for vinyl records.
/// References a RecordDefinition (audio clip, mood value, colors).
/// Captures spawn position as shelf home — RecordSlot returns records here on eject.
/// </summary>
public class RecordItem : MonoBehaviour
{
    [Header("Record Content")]
    [Tooltip("The record definition (title, artist, audio, mood).")]
    [SerializeField] private RecordDefinition _definition;

    public RecordDefinition Definition => _definition;

    /// <summary>Shelf position captured at Awake — RecordSlot returns the record here.</summary>
    public Vector3 HomePosition => _homePosition;

    /// <summary>Shelf rotation captured at Awake.</summary>
    public Quaternion HomeRotation => _homeRotation;

    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Awake()
    {
        // Capture shelf position before anything moves
        _homePosition = transform.position;
        _homeRotation = transform.rotation;

        // Auto-configure PlaceableObject home settings
        var placeable = GetComponent<PlaceableObject>();
        if (placeable != null)
            placeable.ConfigureHome(useSpawnAsHome: true);
    }
}
