using UnityEngine;

/// <summary>
/// Lightweight companion to PlaceableObject for books on shelves.
/// References a BookDefinition and optionally drops a hidden item on first pickup.
/// </summary>
public class BookItem : MonoBehaviour
{
    [Header("Book Content")]
    [Tooltip("The book definition (title, pages, hidden item info).")]
    [SerializeField] private BookDefinition _definition;

    [Header("Hidden Item")]
    [Tooltip("Optional prefab dropped at the book's position on first pickup.")]
    [SerializeField] private GameObject _hiddenItemPrefab;

    [Tooltip("Minimum day number before the hidden item can appear (0 = always).")]
    [SerializeField] private int _hiddenItemMinDay;

    public BookDefinition Definition => _definition;

    private bool _hiddenItemDropped;

    /// <summary>
    /// Called by ObjectGrabber after PlaceableObject.OnPickedUp().
    /// Checks for hidden item conditions and spawns if eligible.
    /// </summary>
    public void OnBookPickedUp()
    {
        if (_hiddenItemDropped) return;
        if (_definition == null || !_definition.hasHiddenItem) return;
        if (_hiddenItemPrefab == null) return;

        // Check day condition
        if (_hiddenItemMinDay > 0 && GameClock.Instance != null
            && GameClock.Instance.CurrentDay < _hiddenItemMinDay)
            return;

        _hiddenItemDropped = true;

        // Spawn hidden item at book's last resting position
        Instantiate(_hiddenItemPrefab, transform.position, Quaternion.identity);

        // Show caption
        string desc = !string.IsNullOrEmpty(_definition.hiddenItemDescription)
            ? _definition.hiddenItemDescription
            : "Something fell out of the book...";
        CaptionDisplay.Show(desc, 3f);

        Debug.Log($"[BookItem] Hidden item dropped from '{_definition.title}'.");
    }
}
