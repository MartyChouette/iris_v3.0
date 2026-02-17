using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Kitchen wall calendar. Click the 3D object to open a screen-space overlay
/// showing a 30-day month grid with date history. Fully self-constructing —
/// builds its own UI in Awake(). Just drop the component on a GO with a collider.
/// </summary>
public class ApartmentCalendar : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Layer mask for calendar clickable object.")]
    [SerializeField] private LayerMask _calendarLayer;

    [Tooltip("Max raycast distance.")]
    [SerializeField] private float _maxRayDistance = 10f;

    // ─── Constants ──────────────────────────────────────────────
    private const int DaysPerPage = 30;
    private const int Columns = 5;
    private const int Rows = 6;
    private const float CellWidth = 160f;
    private const float CellHeight = 90f;
    private const float CellSpacing = 6f;
    private const float PanelWidth = (CellWidth + CellSpacing) * Columns + 40f;
    private const float PanelHeight = 680f;

    // ─── Built UI ───────────────────────────────────────────────
    private GameObject _canvasGO;
    private GameObject _panelRoot;
    private TMP_Text _headerText;
    private TMP_Text _detailText;
    private TMP_Text[] _cellTexts;
    private Image[] _cellBGs;
    private Button[] _cellButtons;
    private Button _prevButton;
    private Button _nextButton;
    private Button _closeButton;

    // ─── Input ──────────────────────────────────────────────────
    private InputAction _clickAction;
    private InputAction _pointerAction;
    private InputAction _escapeAction;

    // ─── State ──────────────────────────────────────────────────
    private Camera _cachedCamera;
    private bool _isOpen;
    private int _currentPage; // page 0 = days 1-30, page 1 = 31-60, etc.

    // ─── Lifecycle ──────────────────────────────────────────────

    private void Awake()
    {
        _clickAction = new InputAction("CalendarClick", InputActionType.Button, "<Mouse>/leftButton");
        _pointerAction = new InputAction("CalendarPointer", InputActionType.Value, "<Mouse>/position");
        _escapeAction = new InputAction("CalendarEscape", InputActionType.Button, "<Keyboard>/escape");

        BuildUI();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _pointerAction.Enable();
        _escapeAction.Enable();
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _pointerAction.Disable();
        _escapeAction.Disable();
    }

    private void OnDestroy()
    {
        _clickAction?.Dispose();
        _pointerAction?.Dispose();
        _escapeAction?.Dispose();
        if (_canvasGO != null) Destroy(_canvasGO);
    }

    private void Update()
    {
        if (_isOpen)
        {
            if (_escapeAction.WasPressedThisFrame())
                CloseCalendar();
            return;
        }

        if (_clickAction.WasPressedThisFrame())
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;

            Vector2 mousePos = _pointerAction.ReadValue<Vector2>();
            Ray ray = _cachedCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _calendarLayer))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    OpenCalendar();
            }
        }
    }

    // ─── Public API ─────────────────────────────────────────────

    public void OpenCalendar()
    {
        _isOpen = true;

        // Auto-show page containing current day
        int currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        _currentPage = (currentDay - 1) / DaysPerPage;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        PopulateGrid();
        ClearDetail();
        Debug.Log("[ApartmentCalendar] Opened.");
    }

    public void CloseCalendar()
    {
        _isOpen = false;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        Debug.Log("[ApartmentCalendar] Closed.");
    }

    // ─── UI Construction ────────────────────────────────────────

    private void BuildUI()
    {
        // Screen-space overlay canvas
        _canvasGO = new GameObject("CalendarCanvas");
        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;
        var scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        _canvasGO.AddComponent<GraphicRaycaster>();

        // Panel root (dark semi-transparent backdrop)
        _panelRoot = new GameObject("CalendarPanel");
        _panelRoot.transform.SetParent(_canvasGO.transform, false);
        var panelRT = _panelRoot.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRT.anchoredPosition = Vector2.zero;
        var panelImg = _panelRoot.AddComponent<Image>();
        panelImg.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        // Header: "Days 1-30" + page arrows
        float headerY = PanelHeight * 0.5f - 30f;

        var prevGO = CreateButton("PrevPage", _panelRoot.transform,
            new Vector2(-PanelWidth * 0.5f + 50f, headerY), new Vector2(60f, 36f), "<");
        _prevButton = prevGO.GetComponent<Button>();
        _prevButton.onClick.AddListener(PrevPage);

        _headerText = CreateTMPElement("Header", _panelRoot.transform,
            new Vector2(0f, headerY), new Vector2(300f, 36f),
            "Days 1\u201330", 24f, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(0.95f, 0.95f, 0.95f));

        var nextGO = CreateButton("NextPage", _panelRoot.transform,
            new Vector2(PanelWidth * 0.5f - 50f, headerY), new Vector2(60f, 36f), ">");
        _nextButton = nextGO.GetComponent<Button>();
        _nextButton.onClick.AddListener(NextPage);

        // Close button (top-right)
        var closeGO = CreateButton("Close", _panelRoot.transform,
            new Vector2(PanelWidth * 0.5f - 30f, PanelHeight * 0.5f - 30f),
            new Vector2(40f, 40f), "X");
        _closeButton = closeGO.GetComponent<Button>();
        _closeButton.onClick.AddListener(CloseCalendar);

        // Grid cells (6 rows x 5 cols)
        _cellTexts = new TMP_Text[DaysPerPage];
        _cellBGs = new Image[DaysPerPage];
        _cellButtons = new Button[DaysPerPage];

        float gridStartX = -((Columns - 1) * (CellWidth + CellSpacing)) * 0.5f;
        float gridStartY = headerY - 60f;

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int idx = row * Columns + col;
                float x = gridStartX + col * (CellWidth + CellSpacing);
                float y = gridStartY - row * (CellHeight + CellSpacing);

                var cellGO = new GameObject($"Cell_{idx}");
                cellGO.transform.SetParent(_panelRoot.transform, false);
                var cellRT = cellGO.AddComponent<RectTransform>();
                cellRT.anchorMin = new Vector2(0.5f, 0.5f);
                cellRT.anchorMax = new Vector2(0.5f, 0.5f);
                cellRT.anchoredPosition = new Vector2(x, y);
                cellRT.sizeDelta = new Vector2(CellWidth, CellHeight);

                _cellBGs[idx] = cellGO.AddComponent<Image>();
                _cellBGs[idx].color = new Color(0.15f, 0.15f, 0.18f, 0.8f);

                _cellButtons[idx] = cellGO.AddComponent<Button>();
                var colors = _cellButtons[idx].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.8f, 0.8f, 1f);
                colors.pressedColor = new Color(0.6f, 0.6f, 0.8f);
                _cellButtons[idx].colors = colors;

                int capturedDay = idx; // captured for closure
                _cellButtons[idx].onClick.AddListener(() => OnCellClicked(capturedDay));

                // Cell text
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(cellGO.transform, false);
                var textRT = textGO.AddComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = new Vector2(4f, 4f);
                textRT.offsetMax = new Vector2(-4f, -4f);
                _cellTexts[idx] = textGO.AddComponent<TextMeshProUGUI>();
                _cellTexts[idx].fontSize = 14f;
                _cellTexts[idx].alignment = TextAlignmentOptions.Center;
                _cellTexts[idx].color = new Color(0.85f, 0.85f, 0.85f);
                _cellTexts[idx].enableWordWrapping = true;
                _cellTexts[idx].overflowMode = TextOverflowModes.Ellipsis;
                _cellTexts[idx].raycastTarget = false;
            }
        }

        // Detail panel (bottom)
        float detailY = gridStartY - Rows * (CellHeight + CellSpacing) - 10f;
        var detailBG = new GameObject("DetailBG");
        detailBG.transform.SetParent(_panelRoot.transform, false);
        var detailBGRT = detailBG.AddComponent<RectTransform>();
        detailBGRT.anchorMin = new Vector2(0.5f, 0.5f);
        detailBGRT.anchorMax = new Vector2(0.5f, 0.5f);
        detailBGRT.anchoredPosition = new Vector2(0f, detailY);
        detailBGRT.sizeDelta = new Vector2(PanelWidth - 40f, 60f);
        detailBG.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.6f);

        _detailText = CreateTMPElement("DetailText", _panelRoot.transform,
            new Vector2(0f, detailY), new Vector2(PanelWidth - 60f, 50f),
            "Click a day to see details.", 16f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));
    }

    // ─── Grid Population ────────────────────────────────────────

    private void PopulateGrid()
    {
        int currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        int pageStart = _currentPage * DaysPerPage + 1;
        int pageEnd = pageStart + DaysPerPage - 1;

        // Update header
        if (_headerText != null)
            _headerText.text = $"Days {pageStart}\u2013{pageEnd}";

        // Build day → entry lookup from thin DateHistory
        var dayToEntry = new Dictionary<int, DateHistory.DateHistoryEntry>();
        foreach (var entry in DateHistory.Entries)
        {
            dayToEntry[entry.day] = entry;
        }

        for (int i = 0; i < DaysPerPage; i++)
        {
            int day = pageStart + i;

            if (_cellTexts[i] == null) continue;

            if (day == currentDay)
            {
                _cellTexts[i].text = $"<b>Day {day}</b>\n<color=#FFD700>TODAY</color>";
                _cellBGs[i].color = new Color(0.25f, 0.22f, 0.08f, 0.9f); // gold tint
            }
            else if (dayToEntry.TryGetValue(day, out var entry))
            {
                _cellTexts[i].text = $"Day {day}\n{entry.name}\n<color={GetGradeColor(entry.grade)}>{entry.grade}</color>";
                _cellBGs[i].color = new Color(0.15f, 0.15f, 0.18f, 0.8f);
            }
            else if (day < currentDay)
            {
                _cellTexts[i].text = $"Day {day}\n<color=#666>\u2014</color>";
                _cellBGs[i].color = new Color(0.12f, 0.12f, 0.14f, 0.6f);
            }
            else
            {
                _cellTexts[i].text = $"<color=#444>Day {day}</color>";
                _cellBGs[i].color = new Color(0.10f, 0.10f, 0.12f, 0.4f);
            }
        }

        // Update page button interactability
        if (_prevButton != null) _prevButton.interactable = _currentPage > 0;
        if (_nextButton != null) _nextButton.interactable = pageEnd < currentDay + DaysPerPage;
    }

    private void OnCellClicked(int cellIndex)
    {
        int pageStart = _currentPage * DaysPerPage + 1;
        int day = pageStart + cellIndex;
        SelectDay(day);
    }

    private void SelectDay(int day)
    {
        if (_detailText == null) return;

        DateHistory.DateHistoryEntry match = null;
        foreach (var entry in DateHistory.Entries)
        {
            if (entry.day == day)
            {
                match = entry;
                break;
            }
        }

        if (match == null)
        {
            _detailText.text = $"Day {day} \u2014 No date recorded.";
            return;
        }

        _detailText.text = $"<b>Day {day} \u2014 {match.name}</b>   " +
                           $"Grade: <color={GetGradeColor(match.grade)}>{match.grade}</color>   " +
                           $"Affection: {match.affection:F0}%";
    }

    private void ClearDetail()
    {
        if (_detailText != null)
            _detailText.text = "Click a day to see details.";
    }

    // ─── Pagination ─────────────────────────────────────────────

    private void PrevPage()
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            PopulateGrid();
            ClearDetail();
        }
    }

    private void NextPage()
    {
        _currentPage++;
        PopulateGrid();
        ClearDetail();
    }

    // ─── Helpers ────────────────────────────────────────────────

    private static TMP_Text CreateTMPElement(string name, Transform parent,
        Vector2 pos, Vector2 size, string text, float fontSize,
        FontStyles style, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject CreateButton(string name, Transform parent,
        Vector2 pos, Vector2 size, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
        go.AddComponent<Button>();

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.9f, 0.9f);
        tmp.raycastTarget = false;

        return go;
    }

    private static string GetGradeColor(string grade) => grade switch
    {
        "S" => "#FFD700",
        "A" => "#66FF66",
        "B" => "#66CCFF",
        "C" => "#FFCC66",
        _ => "#FF6666"
    };
}
