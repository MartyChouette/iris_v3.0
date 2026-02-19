using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Physical calendar object in the apartment. Click to view a 7-day grid
/// showing past dates with character names, grades, and learned preferences.
/// Reads from DateHistory — purely a viewer, no own data.
/// </summary>
public class ApartmentCalendar : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Root panel for the calendar overlay.")]
    [SerializeField] private GameObject _panelRoot;

    [Tooltip("7 day slot text elements (index 0 = Day 1).")]
    [SerializeField] private TMP_Text[] _daySlots = new TMP_Text[7];

    [Tooltip("Detail panel text for selected day.")]
    [SerializeField] private TMP_Text _detailText;

    [Tooltip("CanvasGroup for fade.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Input")]
    [Tooltip("Layer mask for calendar clickable object.")]
    [SerializeField] private LayerMask _calendarLayer;

    [Tooltip("Max raycast distance.")]
    [SerializeField] private float _maxRayDistance = 10f;

    private InputAction _clickAction;
    private InputAction _pointerAction;
    private InputAction _escapeAction;
    private Camera _cachedCamera;
    private bool _isOpen;
    private int _selectedDay = -1;

    private void Awake()
    {
        _clickAction = new InputAction("CalendarClick", InputActionType.Button, "<Mouse>/leftButton");
        _pointerAction = new InputAction("CalendarPointer", InputActionType.Value, "<Mouse>/position");
        _escapeAction = new InputAction("CalendarEscape", InputActionType.Button, "<Keyboard>/escape");

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
    }

    private void Update()
    {
        if (_isOpen)
        {
            if (_escapeAction.WasPressedThisFrame())
                CloseCalendar();
            return;
        }

        // Check for click on calendar object
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

    /// <summary>Open the calendar overlay and populate it.</summary>
    public void OpenCalendar()
    {
        _isOpen = true;
        _selectedDay = -1;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        PopulateCalendar();
        Debug.Log("[ApartmentCalendar] Opened.");
    }

    /// <summary>Close the calendar overlay.</summary>
    public void CloseCalendar()
    {
        _isOpen = false;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        Debug.Log("[ApartmentCalendar] Closed.");
    }

    private void PopulateCalendar()
    {
        int currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        var entries = DateHistory.Entries;

        // Build lookup: day → entry
        var dayToEntry = new Dictionary<int, RichDateHistoryEntry>();
        foreach (var entry in entries)
        {
            dayToEntry[entry.day] = entry; // latest entry for that day wins
        }

        for (int i = 0; i < _daySlots.Length; i++)
        {
            if (_daySlots[i] == null) continue;

            int day = i + 1;
            string text;

            if (day == currentDay)
            {
                text = $"<b>Day {day}</b>\n<color=#FFD700>TODAY</color>";
            }
            else if (dayToEntry.TryGetValue(day, out var entry))
            {
                text = $"Day {day}\n{entry.characterName}\n<color={GetGradeColor(entry.grade)}>{entry.grade}</color>";
            }
            else if (day < currentDay)
            {
                text = $"Day {day}\n<color=#888>No date</color>";
            }
            else
            {
                text = $"Day {day}\n<color=#555>???</color>";
            }

            _daySlots[i].text = text;
        }

        if (_detailText != null)
            _detailText.text = "Click a day to see details.";
    }

    /// <summary>Called when a day slot is clicked (wired via UI Button).</summary>
    public void SelectDay(int day)
    {
        _selectedDay = day;

        if (_detailText == null) return;

        var entries = DateHistory.GetEntriesForCharacter(""); // we need day-based lookup
        RichDateHistoryEntry match = null;
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
            _detailText.text = $"Day {day}\nNo date recorded.";
            return;
        }

        string detail = $"<b>Day {day} — {match.characterName}</b>\n" +
                        $"Grade: <color={GetGradeColor(match.grade)}>{match.grade}</color>\n" +
                        $"Affection: {match.finalAffection:F0}%\n\n";

        // Add learned preferences
        var prefs = LearnedPreferenceRegistry.GetPreferencesFor(match.characterId);
        if (prefs.Count > 0)
        {
            detail += "<b>Learned:</b>\n";
            foreach (var pref in prefs)
            {
                string icon = pref.reaction == "Like" || pref.reaction == "Love" ? "<color=#FF69B4>\u2665</color>" : "<color=#6495ED>\u2639</color>";
                detail += $"  {icon} {pref.tag}\n";
            }
        }

        // Add key reactions
        if (match.reactions != null && match.reactions.Count > 0)
        {
            detail += $"\n<b>Reactions:</b> {match.reactions.Count} total";
        }

        _detailText.text = detail;
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
