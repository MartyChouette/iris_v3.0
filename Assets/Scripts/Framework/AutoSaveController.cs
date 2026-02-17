using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Triggers auto-saves at end of day, end of date, and on application quit.
/// Gathers state from all active registries into IrisSaveData and writes via SaveManager.
/// Shows a brief "Saving..." indicator in the bottom-right corner.
/// </summary>
public class AutoSaveController : MonoBehaviour
{
    public static AutoSaveController Instance { get; private set; }

    /// <summary>After RestoreFromSave, holds the day phase to restore to.</summary>
    public int RestoredDayPhase { get; private set; } = -1;

    /// <summary>After RestoreFromSave, holds the saved day number.</summary>
    public int RestoredDay { get; private set; }

    // ── Save indicator UI (self-constructed) ──────────────────────
    private GameObject _indicatorCanvas;
    private CanvasGroup _indicatorGroup;
    private TMP_Text _indicatorText;
    private Coroutine _fadeCoroutine;

    private const float ShowDuration = 1.2f;
    private const float FadeDuration = 0.6f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AutoSaveController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildIndicatorUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_indicatorCanvas != null) Destroy(_indicatorCanvas);
    }

    private void OnApplicationQuit()
    {
        PerformSave("application_quit");
    }

    /// <summary>Trigger a save with a reason tag for debugging.</summary>
    public void PerformSave(string reason = "manual")
    {
        var data = GatherSaveData();
        SaveManager.SaveGame(data);
        ShowIndicator();
        Debug.Log($"[AutoSaveController] Auto-saved slot {SaveManager.ActiveSlot} ({reason}).");
    }

    private IrisSaveData GatherSaveData()
    {
        return new IrisSaveData
        {
            playerName = PlayerData.PlayerName,
            gameModeName = MainMenuManager.ActiveConfig != null
                ? MainMenuManager.ActiveConfig.modeName : "",
            currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1,
            currentHour = GameClock.Instance != null ? GameClock.Instance.CurrentHour : 8f,
            dayPhase = DayPhaseManager.Instance != null
                ? (int)DayPhaseManager.Instance.CurrentPhase
                : 0,
            dateHistory = DateHistory.GetAllForSave(),
            itemDisplayStates = ItemStateRegistry.GetAllForSave(),
            objectPositions = GatherObjectPositions()
        };
    }

    private List<PlaceablePositionRecord> GatherObjectPositions()
    {
        var records = new List<PlaceablePositionRecord>();
        var placeables = FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None);
        foreach (var p in placeables)
        {
            var t = p.transform;
            records.Add(new PlaceablePositionRecord
            {
                objectName = p.gameObject.name,
                px = t.position.x, py = t.position.y, pz = t.position.z,
                rx = t.rotation.x, ry = t.rotation.y, rz = t.rotation.z, rw = t.rotation.w
            });
        }

        // Fridge door state
        var fridge = FindFirstObjectByType<FridgeController>();
        if (fridge != null)
        {
            var ft = fridge.transform;
            records.Add(new PlaceablePositionRecord
            {
                objectName = "__FridgeController__",
                px = ft.position.x, py = ft.position.y, pz = ft.position.z,
                rx = ft.rotation.x, ry = ft.rotation.y, rz = ft.rotation.z, rw = ft.rotation.w
            });
        }

        return records;
    }

    /// <summary>Load saved game data and restore all registries.</summary>
    public void RestoreFromSave()
    {
        var data = SaveManager.LoadGame();
        if (data == null)
        {
            Debug.Log("[AutoSaveController] No save data found.");
            RestoredDayPhase = -1;
            return;
        }

        PlayerData.PlayerName = data.playerName;
        GameClock.Instance?.RestoreFromSave(data.currentDay, data.currentHour);
        DateHistory.LoadFrom(data.dateHistory);
        ItemStateRegistry.LoadFrom(data.itemDisplayStates);
        RestoreObjectPositions(data.objectPositions);

        RestoredDayPhase = data.dayPhase;
        RestoredDay = data.currentDay;

        Debug.Log($"[AutoSaveController] Restored slot {SaveManager.ActiveSlot} — " +
                  $"Day {data.currentDay}, phase {(DayPhaseManager.DayPhase)data.dayPhase}, " +
                  $"{data.dateHistory?.Count ?? 0} date records, " +
                  $"{data.objectPositions?.Count ?? 0} object positions.");
    }

    private void RestoreObjectPositions(List<PlaceablePositionRecord> records)
    {
        if (records == null || records.Count == 0) return;

        // Build name→PlaceableObject lookup
        var placeables = FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None);
        var lookup = new Dictionary<string, Transform>();
        foreach (var p in placeables)
        {
            if (!lookup.ContainsKey(p.gameObject.name))
                lookup[p.gameObject.name] = p.transform;
        }

        // Fridge
        var fridge = FindFirstObjectByType<FridgeController>();
        if (fridge != null)
            lookup["__FridgeController__"] = fridge.transform;

        int restored = 0;
        foreach (var r in records)
        {
            if (lookup.TryGetValue(r.objectName, out var target))
            {
                target.position = new Vector3(r.px, r.py, r.pz);
                target.rotation = new Quaternion(r.rx, r.ry, r.rz, r.rw);
                restored++;
            }
        }

        Debug.Log($"[AutoSaveController] Restored {restored}/{records.Count} object positions.");
    }

    // ── Save Indicator ──────────────────────────────────────────────

    private void BuildIndicatorUI()
    {
        // Screen-space overlay canvas at high sort order
        _indicatorCanvas = new GameObject("SaveIndicatorCanvas");
        _indicatorCanvas.transform.SetParent(transform);

        var canvas = _indicatorCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = _indicatorCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // CanvasGroup for fade
        _indicatorGroup = _indicatorCanvas.AddComponent<CanvasGroup>();
        _indicatorGroup.alpha = 0f;
        _indicatorGroup.blocksRaycasts = false;
        _indicatorGroup.interactable = false;

        // Text — bottom-right corner
        var textGO = new GameObject("SaveText");
        textGO.transform.SetParent(_indicatorCanvas.transform, false);

        var rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-30f, 20f);
        rt.sizeDelta = new Vector2(200f, 40f);

        _indicatorText = textGO.AddComponent<TextMeshProUGUI>();
        _indicatorText.text = "Saving...";
        _indicatorText.fontSize = 20f;
        _indicatorText.alignment = TextAlignmentOptions.BottomRight;
        _indicatorText.color = new Color(0.9f, 0.9f, 0.85f, 1f);
        _indicatorText.fontStyle = FontStyles.Italic;
    }

    private void ShowIndicator()
    {
        if (_indicatorGroup == null) return;

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(IndicatorFadeSequence());
    }

    private IEnumerator IndicatorFadeSequence()
    {
        // Snap visible
        _indicatorGroup.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(ShowDuration);

        // Fade out
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _indicatorGroup.alpha = 1f - Mathf.Clamp01(elapsed / FadeDuration);
            yield return null;
        }

        _indicatorGroup.alpha = 0f;
        _fadeCoroutine = null;
    }
}
