using UnityEngine;

/// <summary>
/// Makes an item pairable with another item. Shoes pair with a specific partner
/// (side-by-side), dishes and bowls pair with any of the same category (stacked).
/// When paired, the secondary item parents to the primary and they move as one.
///
/// For SpecificPartner mode (shoes): when one shoe is picked up, the partner
/// flashes its highlight so the player can find it easily.
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
    private bool _partnerHighlightActive;

    public bool IsPaired => _isPaired;
    public PairableItem PairedChild => _pairedChild;
    public PairMode Mode => _pairMode;
    public PairableItem SpecificPartner => _specificPartner;

    private void Awake()
    {
        _placeable = GetComponent<PlaceableObject>();
    }

    /// <summary>
    /// Called when this item is picked up. Resets pairing state and
    /// unparents any paired child. Flashes partner highlight for shoes.
    /// </summary>
    public void OnPickedUp()
    {
        // Unpair: detach child and re-enable its collider/placeable
        if (_isPaired && _pairedChild != null)
        {
            var childCol = _pairedChild.GetComponent<Collider>();
            if (childCol != null) childCol.enabled = true;
            if (_pairedChild._placeable != null) _pairedChild._placeable.enabled = true;

            _pairedChild.transform.SetParent(null);
            _pairedChild._isPaired = false;
            _pairedChild = null;
        }
        _isPaired = false;

        if (_pairMode != PairMode.SpecificPartner || _specificPartner == null) return;
        if (_specificPartner._isPaired) return;

        var partnerHL = _specificPartner.GetComponent<InteractableHighlight>();
        if (partnerHL != null)
        {
            partnerHL.SetHighlighted(true);
            _partnerHighlightActive = true;
        }
    }

    /// <summary>
    /// Called when this item is put down or pairing completes. Stop flashing partner.
    /// </summary>
    public void OnPutDown()
    {
        if (!_partnerHighlightActive) return;
        if (_specificPartner == null) return;

        var partnerHL = _specificPartner.GetComponent<InteractableHighlight>();
        if (partnerHL != null)
            partnerHL.SetHighlighted(false);

        _partnerHighlightActive = false;
    }

    /// <summary>
    /// Can the held item pair with this placed item?
    /// </summary>
    public bool CanPairWith(PairableItem held)
    {
        if (held == null || held == this) return false;

        if (_pairMode == PairMode.SpecificPartner)
        {
            // Specific partners can only pair once
            if (_isPaired || held._isPaired) return false;
            return held == _specificPartner || held._specificPartner == this;
        }

        // AnyOfCategory — unlimited stacking (no _isPaired check)
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

        // Stop partner highlight since we're pairing now
        held.OnPutDown();

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

        // Find the topmost item in the stack to parent onto
        Transform stackTop = FindStackTop(transform);

        if (_snapMode == SnapMode.Stacked)
        {
            // Stack in world space — place flat on top of the topmost plate's mesh
            var renderer = stackTop.GetComponentInChildren<Renderer>();
            Vector3 stackPos;
            if (renderer != null)
                stackPos = renderer.bounds.center + Vector3.up * renderer.bounds.extents.y;
            else
                stackPos = stackTop.position + Vector3.up * _snapOffset.y;

            // Keep world rotation from the root plate (all plates face the same way)
            Quaternion stackRot = transform.rotation;

            // Parent, then set world pos/rot (worldPositionStays=false would lose it)
            held.transform.SetParent(stackTop, true);
            held.transform.position = stackPos;
            held.transform.rotation = stackRot;
        }
        else
        {
            // SideBySide (shoes) — use local offset as before
            held.transform.SetParent(stackTop, true);
            held.transform.localPosition = _snapOffset;
            held.transform.localRotation = Quaternion.identity;
        }

        // Disable held item's standalone behavior
        var heldCol = held.GetComponent<Collider>();
        if (heldCol != null) heldCol.enabled = false;

        if (held._placeable != null)
            held._placeable.enabled = false;

        Debug.Log($"[PairableItem] {held.name} snapped to {stackTop.name} (stack depth)");
    }

    /// <summary>Walk the transform hierarchy to find the topmost stacked PairableItem.</summary>
    private static Transform FindStackTop(Transform root)
    {
        var current = root;
        while (true)
        {
            bool found = false;
            foreach (Transform child in current)
            {
                if (child.GetComponent<PairableItem>() != null)
                {
                    current = child;
                    found = true;
                    break;
                }
            }
            if (!found) break;
        }
        return current;
    }
}
