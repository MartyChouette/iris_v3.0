using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A large book that stands upright on the bookcase shelf and lays flat on the
/// coffee table. Click to toggle placement. All instances auto-register in a
/// static list; when any book moves, coffee table positions recalculate so
/// flat-stacked books settle without overlap.
///
/// On bookcase: each book remembers its own shelf position from scene build.
/// On coffee table: flat-stacked (Y offset) at the serialized target position.
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

    [Header("Coffee Table Target")]
    [Tooltip("World position of the coffee table stack base (set by scene builder).")]
    [SerializeField] private Vector3 coffeeTableBase;

    [Tooltip("World rotation for flat books on the coffee table (set by scene builder).")]
    [SerializeField] private Quaternion coffeeTableRotation = Quaternion.identity;

    /// <summary>Fired when a coffee table book is moved between bookcase and coffee table.</summary>
    public static event System.Action OnBookMoved;

    public CoffeeTableBookDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.OnBookcase;
    public bool IsMoving => CurrentState == State.Moving;
    public float Thickness => definition != null ? definition.thickness : 0.03f;

    /// <summary>Placement flag independent of animation state.</summary>
    public bool IsOnCoffeeTable { get; private set; }

    /// <summary>All active CoffeeTableBook instances.</summary>
    public static List<CoffeeTableBook> All { get; } = new List<CoffeeTableBook>();

    // Each book remembers where it was placed on the shelf by the scene builder
    private Vector3 _shelfPosition;
    private Quaternion _shelfRotation;

    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;
    private Vector3 _hoverBasePos;

    private const float MoveDuration = 0.4f;
    private const float HoverSlideDistance = 0.03f;

    private void Awake()
    {
        // Save the position the scene builder placed this book at
        _shelfPosition = transform.position;
        _shelfRotation = transform.rotation;

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

            // Move to coffee table immediately
            RecalculateCoffeeTableStack();
        }
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
            transform.position = _hoverBasePos + Vector3.up * HoverSlideDistance;
        else
            transform.position = _hoverBasePos - transform.forward * HoverSlideDistance;

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

        if (_isHovered)
        {
            _isHovered = false;
            transform.position = _hoverBasePos;
        }
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        IsOnCoffeeTable = !IsOnCoffeeTable;
        OnBookMoved?.Invoke();

        if (IsOnCoffeeTable)
        {
            // Animate this book to coffee table, recalculate coffee table stack
            RecalculateCoffeeTableStack();
        }
        else
        {
            // Animate this book back to its shelf position
            StopAllCoroutines();
            StartCoroutine(AnimateToPosition(_shelfPosition, _shelfRotation));
            // Recalculate remaining coffee table books to close gaps
            RecalculateCoffeeTableStack();
        }
    }

    /// <summary>
    /// Recalculates positions for all books currently on the coffee table.
    /// Books on the bookcase stay at their saved shelf positions.
    /// </summary>
    public static void RecalculateCoffeeTableStack()
    {
        // Find coffee table target from any book (all have the same serialized values)
        Vector3 tableBase = Vector3.zero;
        Quaternion tableRot = Quaternion.identity;
        bool foundTarget = false;

        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] != null)
            {
                tableBase = All[i].coffeeTableBase;
                tableRot = All[i].coffeeTableRotation;
                foundTarget = true;
                break;
            }
        }

        if (!foundTarget) return;

        float yOffset = 0f;

        for (int i = 0; i < All.Count; i++)
        {
            var book = All[i];
            if (book == null || !book.IsOnCoffeeTable) continue;

            Vector3 targetPos = tableBase + Vector3.up * (yOffset + book.Thickness / 2f);
            Quaternion targetRot = tableRot;
            yOffset += book.Thickness;

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
