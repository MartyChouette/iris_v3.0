using UnityEngine;

/// <summary>
/// Lightweight companion to PlaceableObject for vinyl records.
/// References a RecordDefinition (audio clip, mood value, colors).
/// Captures spawn position as shelf home — RecordSlot returns records here on eject.
/// Click-to-select: clicking a record on its shelf sends it directly to the turntable.
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
    private Material _artMat;

    private void Awake()
    {
        // Capture shelf position before anything moves
        _homePosition = transform.position;
        _homeRotation = transform.rotation;

        // Auto-configure PlaceableObject home settings
        var placeable = GetComponent<PlaceableObject>();
        if (placeable != null)
            placeable.ConfigureHome(useSpawnAsHome: true);

        ApplyAlbumArt();
    }

    private void OnDestroy()
    {
        if (_artMat != null)
            Destroy(_artMat);
    }

    /// <summary>
    /// Apply album art texture to the record's renderer, scaled to fit.
    /// Creates an instance material so each record can have its own art.
    /// </summary>
    private void ApplyAlbumArt()
    {
        if (_definition == null || _definition.albumArt == null) return;

        var rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        _artMat = new Material(rend.sharedMaterial);
        _artMat.mainTexture = _definition.albumArt;
        _artMat.mainTextureScale = Vector2.one;
        _artMat.mainTextureOffset = Vector2.zero;
        _artMat.color = Color.white;
        rend.material = _artMat;
    }

    /// <summary>
    /// Called by ObjectGrabber when the player clicks this record on the shelf.
    /// Sends it directly to the turntable — no physical grab.
    /// </summary>
    public void SelectForPlayback()
    {
        if (RecordSlot.Instance == null)
        {
            Debug.LogWarning("[RecordItem] No RecordSlot in scene.");
            return;
        }

        // If this record is already on the turntable, toggle playback instead
        if (RecordSlot.Instance.CurrentRecord == _definition)
        {
            RecordSlot.Instance.TogglePlayback();
            return;
        }

        RecordSlot.Instance.SelectRecord(this);
    }
}
