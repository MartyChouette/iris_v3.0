using System.Collections;
using UnityEngine;

/// <summary>
/// A flat book that toggles between its bookcase position and a coffee table position.
/// Click to move, tracked via ItemStateRegistry.
/// </summary>
public class CoffeeTableBook : MonoBehaviour
{
    public enum State { OnBookcase, OnCoffeeTable, Moving, Inspecting }

    [Header("Definition")]
    [Tooltip("ScriptableObject defining this coffee table book.")]
    [SerializeField] private CoffeeTableBookDefinition definition;

    [Header("Positions")]
    [Tooltip("Position on the bookcase shelf (world space, set by scene builder).")]
    [SerializeField] private Vector3 bookcasePosition;

    [Tooltip("Rotation on the bookcase shelf.")]
    [SerializeField] private Quaternion bookcaseRotation = Quaternion.identity;

    [Tooltip("Position on the coffee table (world space, set by scene builder).")]
    [SerializeField] private Vector3 coffeeTablePosition;

    [Tooltip("Rotation on the coffee table.")]
    [SerializeField] private Quaternion coffeeTableRotation = Quaternion.identity;

    public CoffeeTableBookDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.OnBookcase;
    public bool IsMoving => CurrentState == State.Moving;

    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private const float MoveDuration = 0.4f;

    private void Awake()
    {
        bookcasePosition = transform.position;
        bookcaseRotation = transform.rotation;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }
    }

    public void SetDefinition(CoffeeTableBookDefinition def) => definition = def;

    public void SetCoffeeTablePosition(Vector3 pos, Quaternion rot)
    {
        coffeeTablePosition = pos;
        coffeeTableRotation = rot;
    }

    public void OnHoverEnter()
    {
        if (_isHovered || CurrentState == State.Moving) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;
    }

    /// <summary>
    /// Toggle between bookcase and coffee table.
    /// </summary>
    public void TogglePlacement()
    {
        if (CurrentState == State.Moving) return;

        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        if (CurrentState == State.OnBookcase)
            StartCoroutine(MoveRoutine(coffeeTablePosition, coffeeTableRotation, State.OnCoffeeTable));
        else if (CurrentState == State.OnCoffeeTable)
            StartCoroutine(MoveRoutine(bookcasePosition, bookcaseRotation, State.OnBookcase));
    }

    private IEnumerator MoveRoutine(Vector3 targetPos, Quaternion targetRot, State endState)
    {
        CurrentState = State.Moving;

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

        // Update item state registry
        if (definition != null && !string.IsNullOrEmpty(definition.itemID))
        {
            var displayState = endState == State.OnCoffeeTable
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
