using UnityEngine;

/// <summary>
/// Invisible barrier that blocks held items from passing through.
/// Place on a GameObject with a BoxCollider on the Barrier layer.
///
/// For closed cubbies: wire _drawer and the barrier auto-enables when
/// the cubby is closed, disables when open.
///
/// For static barriers: leave _drawer null and the barrier is always active.
///
/// Setup:
///   1. Create empty child GO on the cubby/wall
///   2. Add BoxCollider sized to block the opening
///   3. Set layer to whatever is in ObjectGrabber._barrierLayer
///   4. Add this component
///   5. (Optional) Wire _drawer to auto-toggle with cubby state
/// </summary>
public class ItemBarrier : MonoBehaviour
{
    [Tooltip("Optional DrawerController — barrier activates when closed, deactivates when open. Leave null for always-on barriers.")]
    [SerializeField] private DrawerController _drawer;

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (_drawer == null || _collider == null) return;

        // Enable barrier when cubby is closed, disable when open
        bool shouldBlock = _drawer.CurrentState == DrawerController.State.Closed;
        if (_collider.enabled != shouldBlock)
            _collider.enabled = shouldBlock;
    }
}
