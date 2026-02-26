using UnityEngine;

/// <summary>
/// Lightweight companion to PlaceableObject for disco ball bulbs.
/// References a DiscoBulbDefinition (pattern, colors, mood).
/// Captures spawn position as home — DiscoBallController returns bulbs here on eject.
/// </summary>
public class DiscoBallBulb : MonoBehaviour
{
    [Header("Bulb Content")]
    [Tooltip("The bulb definition (pattern, light color, mood).")]
    [SerializeField] private DiscoBulbDefinition _definition;

    public DiscoBulbDefinition Definition => _definition;

    /// <summary>Home position captured at Awake — DiscoBallController returns the bulb here.</summary>
    public Vector3 HomePosition => _homePosition;

    /// <summary>Home rotation captured at Awake.</summary>
    public Quaternion HomeRotation => _homeRotation;

    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Awake()
    {
        _homePosition = transform.position;
        _homeRotation = transform.rotation;

        // Auto-configure PlaceableObject home settings
        var placeable = GetComponent<PlaceableObject>();
        if (placeable != null)
            placeable.ConfigureHome(useSpawnAsHome: true);

        // Auto-setup ReactableTag from definition tags
        if (_definition != null && _definition.reactionTags != null && _definition.reactionTags.Length > 0)
        {
            var reactable = GetComponent<ReactableTag>();
            if (reactable != null)
                reactable.Setup(_definition.reactionTags, _definition.bulbName);
        }
    }
}
