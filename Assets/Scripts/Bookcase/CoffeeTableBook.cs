using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A flat book that toggles between its bookcase stack and a coffee table stack.
/// Click to move. All instances auto-register in a static list; when any book
/// moves, all stacks recalculate positions so books settle without overlap.
/// </summary>
public class CoffeeTableBook : MonoBehaviour
{
    public enum State { OnBookcase, OnCoffeeTable, Moving }

    [Header("Definition")]
    [Tooltip("ScriptableObject defining this coffee table book.")]
    [SerializeField] private CoffeeTableBookDefinition definition;

    [Header("Settings")]
    [Tooltip("If true, this book starts on the coffee table instead of the bookcase.")]
    [SerializeField] private bool startsOnCoffeeTable;

    public CoffeeTableBookDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.OnBookcase;
    public bool IsMoving => CurrentState == State.Moving;
    public float Thickness => definition != null ? definition.thickness : 0.03f;

    /// <summary>Placement flag independent of animation state.</summary>
    public bool IsOnCoffeeTable { get; private set; }

    /// <summary>Static base position for the bookcase stack (set by scene builder).</summary>
    public static Vector3 BookcaseStackBase { get; set; }

    /// <summary>Static base rotation for the bookcase stack (set by scene builder).</summary>
    public static Quaternion BookcaseStackRotation { get; set; } = Quaternion.identity;

    /// <summary>Static base position for the coffee table stack (set by scene builder).</summary>
    public static Vector3 CoffeeTableStackBase { get; set; }

    /// <summary>Static base rotation for the coffee table stack (set by scene builder).</summary>
    public static Quaternion CoffeeTableStackRotation { get; set; } = Quaternion.identity;

    /// <summary>All active CoffeeTableBook instances.</summary>
    public static List<CoffeeTableBook> All { get; } = new List<CoffeeTableBook>();

    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;
    private Vector3 _hoverBasePos;

    private const float MoveDuration = 0.4f;
    private const float HoverSlideUp = 0.02f;

    private void Awake()
    {
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }
    }

    private void Start()
    {
        if (startsOnCoffeeTable)
        {
            IsOnCoffeeTable = true;
            CurrentState = State.OnCoffeeTable;

            var tag = GetComponent<ReactableTag>();
            if (tag != null)
            {
                tag.IsActive = true;
                tag.IsPrivate = false;
            }

            if (definition != null && !string.IsNullOrEmpty(definition.itemID))
                ItemStateRegistry.SetState(definition.itemID, ItemStateRegistry.ItemDisplayState.OnDisplay);
        }

        RecalculateAllStacks();
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public void SetDefinition(CoffeeTableBookDefinition def) => definition = def;

    public void OnHoverEnter()
    {
        if (_isHovered || CurrentState == State.Moving) return;
        _isHovered = true;
        _hoverBasePos = transform.position;

        // Slide up for flat-stacked books
        transform.position = _hoverBasePos + Vector3.up * HoverSlideUp;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        transform.position = _hoverBasePos;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;
    }

    /// <summary>
    /// Toggle between bookcase stack and coffee table stack.
    /// </summary>
    public void TogglePlacement()
    {
        if (CurrentState == State.Moving) return;

        // Clear hover
        if (_isHovered)
        {
            _isHovered = false;
            transform.position = _hoverBasePos;
        }
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        IsOnCoffeeTable = !IsOnCoffeeTable;
        RecalculateAllStacks();
    }

    /// <summary>
    /// Recalculates positions for all books in both stacks and animates them.
    /// </summary>
    public static void RecalculateAllStacks()
    {
        int bookcaseIndex = 0;
        int coffeeTableIndex = 0;

        for (int i = 0; i < All.Count; i++)
        {
            var book = All[i];
            if (book == null) continue;

            Vector3 targetPos;
            Quaternion targetRot;

            if (book.IsOnCoffeeTable)
            {
                targetPos = CoffeeTableStackBase + Vector3.up * (coffeeTableIndex * book.Thickness);
                targetRot = CoffeeTableStackRotation;
                coffeeTableIndex++;
            }
            else
            {
                targetPos = BookcaseStackBase + Vector3.up * (bookcaseIndex * book.Thickness);
                targetRot = BookcaseStackRotation;
                bookcaseIndex++;
            }

            book.StopAllCoroutines();
            book.StartCoroutine(book.AnimateToPosition(targetPos, targetRot));
        }
    }

    private IEnumerator AnimateToPosition(Vector3 targetPos, Quaternion targetRot)
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
        CurrentState = IsOnCoffeeTable ? State.OnCoffeeTable : State.OnBookcase;

        // Toggle ReactableTag visibility based on placement
        var tag = GetComponent<ReactableTag>();
        if (tag != null)
        {
            tag.IsActive = IsOnCoffeeTable;
            tag.IsPrivate = !IsOnCoffeeTable;
        }

        // Update item state registry
        if (definition != null && !string.IsNullOrEmpty(definition.itemID))
        {
            var displayState = IsOnCoffeeTable
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
