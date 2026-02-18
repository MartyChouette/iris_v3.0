using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a single drawer below the bookcase. Click to slide open,
/// revealing trinkets inside. Re-click or ESC to close.
/// </summary>
public class DrawerController : MonoBehaviour
{
    public enum State { Closed, Opening, Open, Closing }

    [Header("Settings")]
    [Tooltip("Distance the drawer slides forward (local -Z).")]
    [SerializeField] private float slideDistance = 0.3f;

    [Tooltip("Time to open/close in seconds.")]
    [SerializeField] private float slideDuration = 0.3f;

    [Header("Contents")]
    [Tooltip("Root GameObject containing TrinketVolume children (activated on open).")]
    [SerializeField] private GameObject contentsRoot;

    [Header("Storage")]
    [Tooltip("Maximum number of PlaceableObjects that can be stored in this drawer.")]
    [SerializeField] private int _maxCapacity = 3;

    public State CurrentState { get; private set; } = State.Closed;
    public bool HasCapacity => _storedItems.Count < _maxCapacity;

    private readonly List<PlaceableObject> _storedItems = new();

    private Vector3 _closedPosition;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private void Awake()
    {
        _closedPosition = transform.localPosition;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }

        if (contentsRoot != null)
            contentsRoot.SetActive(false);
    }

    public void SetContentsRoot(GameObject root)
    {
        contentsRoot = root;
    }

    public void OnHoverEnter()
    {
        if (_isHovered) return;
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

    public void Open()
    {
        if (CurrentState != State.Closed) return;
        StartCoroutine(SlideRoutine(true));
    }

    public void Close()
    {
        if (CurrentState != State.Open) return;
        StartCoroutine(SlideRoutine(false));
    }

    private IEnumerator SlideRoutine(bool opening)
    {
        CurrentState = opening ? State.Opening : State.Closing;

        // Clear hover visual
        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        Vector3 openPosition = _closedPosition - transform.parent.InverseTransformDirection(transform.forward) * slideDistance;
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = opening ? openPosition : _closedPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / slideDuration);
            transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.localPosition = endPos;

        if (opening)
        {
            CurrentState = State.Open;
            if (contentsRoot != null)
                contentsRoot.SetActive(true);
            PositionStoredItems();
        }
        else
        {
            CurrentState = State.Closed;
            HideStoredItems();
            if (contentsRoot != null)
                contentsRoot.SetActive(false);
        }
    }

    // ── Item storage ────────────────────────────────────────────────

    /// <summary>Store a PlaceableObject inside this drawer. Hides it and marks ReactableTag as private.</summary>
    public void StoreItem(PlaceableObject item)
    {
        if (item == null || _storedItems.Contains(item)) return;
        if (!HasCapacity)
        {
            Debug.Log($"[DrawerController] Drawer full — cannot store {item.name}.");
            return;
        }

        _storedItems.Add(item);
        item.gameObject.SetActive(false);

        if (contentsRoot != null)
            item.transform.SetParent(contentsRoot.transform, true);

        var tag = item.GetComponent<ReactableTag>();
        if (tag != null)
        {
            tag.IsPrivate = true;
            tag.IsActive = false;
        }

        Debug.Log($"[DrawerController] Stored {item.name}. Count: {_storedItems.Count}/{_maxCapacity}");
    }

    /// <summary>Remove a PlaceableObject from storage (called on pickup from drawer).</summary>
    public void RemoveStoredItem(PlaceableObject item)
    {
        if (item == null || !_storedItems.Remove(item)) return;

        item.gameObject.SetActive(true);
        item.transform.SetParent(null);

        var tag = item.GetComponent<ReactableTag>();
        if (tag != null)
        {
            tag.IsPrivate = false;
            tag.IsActive = true;
        }

        Debug.Log($"[DrawerController] Removed {item.name} from storage. Count: {_storedItems.Count}/{_maxCapacity}");
    }

    private void PositionStoredItems()
    {
        if (contentsRoot == null) return;

        for (int i = 0; i < _storedItems.Count; i++)
        {
            var item = _storedItems[i];
            if (item == null) continue;

            item.gameObject.SetActive(true);
            // Grid layout inside drawer
            float x = (i % 3 - 1) * 0.12f;
            float z = (i / 3) * 0.12f;
            item.transform.localPosition = new Vector3(x, 0.02f, z);
        }
    }

    private void HideStoredItems()
    {
        for (int i = 0; i < _storedItems.Count; i++)
        {
            if (_storedItems[i] != null)
                _storedItems[i].gameObject.SetActive(false);
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
