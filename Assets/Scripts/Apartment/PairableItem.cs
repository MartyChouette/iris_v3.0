using UnityEngine;

/// <summary>
/// Makes an item pairable with another item. Shoes pair with a specific partner
/// (side-by-side), dishes and bowls pair with any of the same category (stacked).
/// When paired, the secondary item parents to the primary and they move as one.
/// </summary>
public class PairableItem : MonoBehaviour
{
    public enum PairMode
    {
        SpecificPartner, // shoes — must be this exact partner
        AnyOfCategory    // dishes, bowls — any item with matching ItemCategory
    }

    public enum SnapMode
    {
        SideBySide, // shoes — offset along local X
        Stacked     // dishes, bowls — offset along local Y
    }

    [Header("Pairing")]
    [Tooltip("SpecificPartner for shoes (drag partner). AnyOfCategory for dishes/bowls.")]
    [SerializeField] private PairMode _pairMode = PairMode.AnyOfCategory;

    [Tooltip("Specific partner item (shoes only). Leave null for AnyOfCategory.")]
    [SerializeField] private PairableItem _specificPartner;

    [Header("Snap")]
    [Tooltip("SideBySide for shoes, Stacked for dishes/bowls.")]
    [SerializeField] private SnapMode _snapMode = SnapMode.SideBySide;

    [Tooltip("Local offset when snapped to partner.")]
    [SerializeField] private Vector3 _snapOffset = new Vector3(0.1f, 0f, 0f);

    [Header("Audio")]
    [Tooltip("Sound when items snap together.")]
    [SerializeField] private AudioClip _snapSound;

    private bool _isPaired;
    private PairableItem _pairedChild; // the item that was snapped TO this one
    private PlaceableObject _placeable;

    public bool IsPaired => _isPaired;
    public PairableItem PairedChild => _pairedChild;

    private void Awake()
    {
        _placeable = GetComponent<PlaceableObject>();
    }

    /// <summary>
    /// Can the held item pair with this placed item?
    /// </summary>
    public bool CanPairWith(PairableItem held)
    {
        if (held == null || held == this) return false;

        // Already paired items can't pair again
        if (_isPaired || held._isPaired) return false;

        if (_pairMode == PairMode.SpecificPartner)
            return held == _specificPartner || held._specificPartner == this;

        // AnyOfCategory — both must have PlaceableObject with same category
        if (_placeable == null || held._placeable == null) return false;
        return _placeable.Category == held._placeable.Category;
    }

    /// <summary>
    /// Snap the held item onto this placed item. Called by ObjectGrabber.
    /// 'held' is the item being carried, 'this' is the item on the surface.
    /// </summary>
    public void SnapPair(PairableItem held)
    {
        if (held == null) return;

        _isPaired = true;
        held._isPaired = true;
        _pairedChild = held;

        // Play snap sound
        if (_snapSound != null)
            AudioManager.Instance?.PlaySFX(_snapSound);
        else if (held._snapSound != null)
            AudioManager.Instance?.PlaySFX(held._snapSound);

        // Parent held item to this item
        var heldRb = held.GetComponent<Rigidbody>();
        if (heldRb != null)
        {
            heldRb.isKinematic = true;
            heldRb.linearVelocity = Vector3.zero;
        }

        held.transform.SetParent(transform, true);
        held.transform.localPosition = _snapOffset;
        held.transform.localRotation = Quaternion.identity;

        // Disable held item's standalone behavior
        var heldCol = held.GetComponent<Collider>();
        if (heldCol != null) heldCol.enabled = false;

        if (held._placeable != null)
            held._placeable.enabled = false;

        Debug.Log($"[PairableItem] {held.name} snapped to {name}");
    }
}
