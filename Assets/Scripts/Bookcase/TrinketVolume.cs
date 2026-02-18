using System.Collections;
using UnityEngine;

/// <summary>
/// A trinket that can be picked from a drawer and placed on a display slot,
/// or returned to the drawer. Click to toggle placement.
/// </summary>
public class TrinketVolume : MonoBehaviour
{
    public enum State { InDrawer, MovingToDisplay, OnDisplay, MovingToDrawer, Inspecting }

    [Header("Definition")]
    [Tooltip("ScriptableObject defining this trinket's data.")]
    [SerializeField] private TrinketDefinition definition;

    [Header("Positions")]
    [Tooltip("Display position on the bookcase shelf (world space, set by scene builder).")]
    [SerializeField] private Vector3 displayPosition;

    [Tooltip("Display rotation (world space).")]
    [SerializeField] private Quaternion displayRotation = Quaternion.identity;

    public TrinketDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.InDrawer;

    private Vector3 _drawerPosition;
    private Quaternion _drawerRotation;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private const float MoveDuration = 0.3f;

    private void Awake()
    {
        _drawerPosition = transform.position;
        _drawerRotation = transform.rotation;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }

        if (definition != null && definition.startsInDrawer)
            CurrentState = State.InDrawer;
        else
            CurrentState = State.OnDisplay;
    }

    public void SetDefinition(TrinketDefinition def) => definition = def;

    public void SetDisplayPosition(Vector3 pos, Quaternion rot)
    {
        displayPosition = pos;
        displayRotation = rot;
    }

    public void OnHoverEnter()
    {
        if (_isHovered) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.3f;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;
    }

    /// <summary>
    /// Toggle between drawer and display positions.
    /// </summary>
    public void TogglePlacement()
    {
        if (CurrentState == State.InDrawer)
            StartCoroutine(MoveRoutine(displayPosition, displayRotation, State.MovingToDisplay, State.OnDisplay));
        else if (CurrentState == State.OnDisplay)
            StartCoroutine(MoveRoutine(_drawerPosition, _drawerRotation, State.MovingToDrawer, State.InDrawer));
    }

    private IEnumerator MoveRoutine(Vector3 targetPos, Quaternion targetRot,
        State transitState, State endState)
    {
        CurrentState = transitState;

        // Clear hover
        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        // Unparent during move so world position is straightforward
        Transform originalParent = transform.parent;
        transform.SetParent(null, true);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < MoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / MoveDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        CurrentState = endState;

        // Sync ReactableTag visibility with placement state
        var reactable = GetComponent<ReactableTag>();
        if (reactable != null)
        {
            reactable.IsPrivate = (endState == State.InDrawer);
            reactable.IsActive = (endState == State.OnDisplay);
        }

        // Update item state registry
        if (definition != null && !string.IsNullOrEmpty(definition.itemID))
        {
            var displayState = endState == State.OnDisplay
                ? ItemStateRegistry.ItemDisplayState.OnDisplay
                : ItemStateRegistry.ItemDisplayState.PutAway;
            ItemStateRegistry.SetState(definition.itemID, displayState);
        }
    }

    private void OnDestroy()
    {
        if (_instanceMaterial != null)
            Destroy(_instanceMaterial);
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
