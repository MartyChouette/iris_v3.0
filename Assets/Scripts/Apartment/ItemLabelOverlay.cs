using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Scene-scoped singleton. While MMB is held, shows world-space name labels
/// above all PlaceableObjects in the scene.
/// </summary>
public class ItemLabelOverlay : MonoBehaviour
{
    public static ItemLabelOverlay Instance { get; private set; }

    [Header("Config")]
    [Tooltip("Layer mask for finding placeable objects.")]
    [SerializeField] private LayerMask _placeableLayer;

    [Tooltip("Vertical offset above object bounds top.")]
    [SerializeField] private float _heightOffset = 0.15f;

    private InputAction _mmbAction;
    private readonly List<GameObject> _labelPool = new List<GameObject>();
    private int _activeCount;
    private bool _showing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _mmbAction = new InputAction("MMB", InputActionType.Button, "<Mouse>/middleButton");
    }

    private void OnEnable()
    {
        _mmbAction?.Enable();
    }

    private void OnDisable()
    {
        _mmbAction?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _mmbAction?.Dispose();
    }

    private void Update()
    {
        if (_mmbAction == null) return;

        if (_mmbAction.WasPressedThisFrame())
            ShowLabels();
        else if (_mmbAction.WasReleasedThisFrame())
            HideLabels();

        if (_showing)
            UpdateLabelPositions();
    }

    private void ShowLabels()
    {
        _showing = true;
        var cam = Camera.main;
        if (cam == null) return;

        var placeables = Object.FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None);
        _activeCount = 0;

        Vector3 camForward = cam.transform.forward;
        Vector3 camPos = cam.transform.position;

        foreach (var p in placeables)
        {
            if (p == null) continue;

            // Only show labels for objects roughly in front of camera
            Vector3 toObj = p.transform.position - camPos;
            if (Vector3.Dot(toObj.normalized, camForward) < 0.3f)
                continue;

            // Get bounds top
            float topY = p.transform.position.y;
            var rend = p.GetComponent<Renderer>();
            if (rend != null)
                topY = rend.bounds.max.y;

            Vector3 labelPos = new Vector3(p.transform.position.x, topY + _heightOffset, p.transform.position.z);

            var labelGO = GetOrCreateLabel(_activeCount);
            labelGO.SetActive(true);
            labelGO.transform.position = labelPos;

            // Billboard toward camera
            labelGO.transform.rotation = Quaternion.LookRotation(labelGO.transform.position - camPos);

            var tmp = labelGO.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                tmp.text = p.ItemDescription;

            _activeCount++;
        }

        // Hide unused labels
        for (int i = _activeCount; i < _labelPool.Count; i++)
            _labelPool[i].SetActive(false);
    }

    private void HideLabels()
    {
        _showing = false;
        for (int i = 0; i < _labelPool.Count; i++)
            _labelPool[i].SetActive(false);
        _activeCount = 0;
    }

    private void UpdateLabelPositions()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < _activeCount; i++)
        {
            if (i >= _labelPool.Count) break;
            var go = _labelPool[i];
            if (go == null || !go.activeSelf) continue;
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - camPos);
        }
    }

    private GameObject GetOrCreateLabel(int index)
    {
        if (index < _labelPool.Count)
            return _labelPool[index];

        // Create world-space canvas label
        var go = new GameObject($"ItemLabel_{index}");
        go.transform.SetParent(transform);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        var canvasRT = go.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(200f, 40f);
        canvasRT.localScale = new Vector3(0.002f, 0.002f, 0.002f);

        // Background pill
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgRT.localScale = Vector3.one;
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        bgImg.raycastTarget = false;

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(4f, 2f);
        textRT.offsetMax = new Vector2(-4f, -2f);
        textRT.localScale = Vector3.one;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        _labelPool.Add(go);
        return go;
    }
}
