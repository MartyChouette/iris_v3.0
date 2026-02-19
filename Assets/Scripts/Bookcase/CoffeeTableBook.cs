using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A large book that stands upright on the bookcase shelf and lays flat on the
/// coffee table. Click to toggle placement. All instances auto-register in a
/// static list; when any book moves, all positions recalculate so books settle
/// without overlap.
///
/// On bookcase: upright, side-by-side (X offset). Hover slides forward.
/// On coffee table: flat-stacked (Y offset). Hover slides up.
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
    public float BookHeight => definition != null ? definition.size.y : 0.20f;

    /// <summary>Placement flag independent of animation state.</summary>
    public bool IsOnCoffeeTable { get; private set; }

    /// <summary>Static base position for the bookcase shelf (left edge). Books stand upright, side-by-side in X.</summary>
    public static Vector3 BookcaseStackBase { get; set; }

    /// <summary>Static base rotation for upright books on the bookcase shelf.</summary>
    public static Quaternion BookcaseStackRotation { get; set; } = Quaternion.identity;

    /// <summary>Static base position for the coffee table. Books lay flat, stacked in Y.</summary>
    public static Vector3 CoffeeTableStackBase { get; set; }

    /// <summary>Static base rotation for flat books on the coffee table.</summary>
    public static Quaternion CoffeeTableStackRotation { get; set; } = Quaternion.identity;

    /// <summary>All active CoffeeTableBook instances.</summary>
    public static List<CoffeeTableBook> All { get; } = new List<CoffeeTableBook>();

    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;
    private Vector3 _hoverBasePos;

    private const float MoveDuration = 0.4f;
    private const float HoverSlideDistance = 0.03f; // forward on shelf, up on table

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

        if (IsOnCoffeeTable)
        {
            // Flat on table — slide up
            transform.position = _hoverBasePos + Vector3.up * HoverSlideDistance;
        }
        else
        {
            // Upright on shelf — slide toward camera (negative local Z)
            transform.position = _hoverBasePos - transform.forward * HoverSlideDistance;
        }

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
    /// Toggle between bookcase shelf (upright) and coffee table (flat).
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
    /// Recalculates positions for all books in both locations and animates them.
    /// Bookcase: upright, side-by-side in local X (cumulative thickness offset).
    /// Coffee table: flat, stacked in Y (cumulative thickness offset).
    /// </summary>
    public static void RecalculateAllStacks()
    {
        float bookcaseXOffset = 0f;
        float coffeeTableYOffset = 0f;

        for (int i = 0; i < All.Count; i++)
        {
            var book = All[i];
            if (book == null) continue;

            Vector3 targetPos;
            Quaternion targetRot;

            if (book.IsOnCoffeeTable)
            {
                // Flat on coffee table: stacked in Y
                targetPos = CoffeeTableStackBase + Vector3.up * (coffeeTableYOffset + book.Thickness / 2f);
                targetRot = CoffeeTableStackRotation;
                coffeeTableYOffset += book.Thickness;
            }
            else
            {
                // Upright on bookcase shelf: side-by-side in X
                targetPos = BookcaseStackBase
                    + BookcaseStackRotation * Vector3.right * (bookcaseXOffset + book.Thickness / 2f)
                    + Vector3.up * (book.BookHeight / 2f);
                targetRot = BookcaseStackRotation;
                bookcaseXOffset += book.Thickness + 0.003f; // small gap between books
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
