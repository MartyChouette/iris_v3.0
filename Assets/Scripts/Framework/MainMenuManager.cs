using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main menu controller with three panels:
/// ModeSelect → GamePanel → SaveSlots.
///
/// Static ActiveConfig persists across scene loads so GameClock / DayPhaseManager
/// can read the selected mode's pacing values on Start().
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ── Static state (survives scene load) ───────────────────────
    public static GameModeConfig ActiveConfig { get; private set; }

    private enum MenuState { ModeSelect, GamePanel, SaveSlots }

    // ── Scene ────────────────────────────────────────────────────
    [Header("Scene")]
    [Tooltip("Build index of the apartment scene. -1 = next scene after this one.")]
    [SerializeField] private int _apartmentSceneIndex = -1;

    [Header("Fade")]
    [Tooltip("Fade duration before loading. 0 = instant.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    // ── Game Mode Configs ────────────────────────────────────────
    [Header("Game Mode Configs")]
    [SerializeField] private GameModeConfig _demoConfig;
    [SerializeField] private GameModeConfig _showcaseConfig;
    [SerializeField] private GameModeConfig _fullConfig;

    // ── Panels ───────────────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private GameObject _modeSelectPanel;
    [SerializeField] private GameObject _gamePanel;
    [SerializeField] private GameObject _saveSlotPanel;

    // ── Mode Select Buttons ──────────────────────────────────────
    [Header("Mode Select")]
    [SerializeField] private Button _demoButton;
    [SerializeField] private Button _showcaseButton;
    [SerializeField] private Button _fullButton;

    // ── Game Panel ───────────────────────────────────────────────
    [Header("Game Panel")]
    [SerializeField] private TMP_Text _modeNameLabel;
    [SerializeField] private TMP_Text _modeDescLabel;
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _loadSaveButton;
    [SerializeField] private Button _gamePanelBackButton;
    [SerializeField] private Button _quitButton;

    // ── Save Slots ───────────────────────────────────────────────
    [Header("Save Slots")]
    [SerializeField] private Button _slot0Button;
    [SerializeField] private Button _slot1Button;
    [SerializeField] private Button _slot2Button;
    [SerializeField] private TMP_Text _slot0Label;
    [SerializeField] private TMP_Text _slot1Label;
    [SerializeField] private TMP_Text _slot2Label;
    [SerializeField] private Button _saveSlotBackButton;

    // ── Tutorial ─────────────────────────────────────────────────
    [Header("Tutorial")]
    [SerializeField] private TutorialCard _tutorialCard;

    // ── Runtime ──────────────────────────────────────────────────
    private MenuState _state;
    private GameModeConfig _selectedConfig;
    private bool _loading;

    // ═══════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Start()
    {
        // Wire button listeners at runtime
        if (_demoButton != null) _demoButton.onClick.AddListener(OnDemoClicked);
        if (_showcaseButton != null) _showcaseButton.onClick.AddListener(OnShowcaseClicked);
        if (_fullButton != null) _fullButton.onClick.AddListener(OnFullClicked);

        if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGame);
        if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
        if (_loadSaveButton != null) _loadSaveButton.onClick.AddListener(OnLoadSave);
        if (_gamePanelBackButton != null) _gamePanelBackButton.onClick.AddListener(OnGamePanelBack);
        if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);

        if (_slot0Button != null) _slot0Button.onClick.AddListener(() => OnSlotClicked(0));
        if (_slot1Button != null) _slot1Button.onClick.AddListener(() => OnSlotClicked(1));
        if (_slot2Button != null) _slot2Button.onClick.AddListener(() => OnSlotClicked(2));
        if (_saveSlotBackButton != null) _saveSlotBackButton.onClick.AddListener(OnSaveSlotBack);

        ShowPanel(MenuState.ModeSelect);

        // Fade in from black
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.FadeIn(_fadeDuration);
    }

    // ═══════════════════════════════════════════════════════════════
    // Panel management
    // ═══════════════════════════════════════════════════════════════

    private void ShowPanel(MenuState state)
    {
        _state = state;
        if (_modeSelectPanel != null) _modeSelectPanel.SetActive(state == MenuState.ModeSelect);
        if (_gamePanel != null) _gamePanel.SetActive(state == MenuState.GamePanel);
        if (_saveSlotPanel != null) _saveSlotPanel.SetActive(state == MenuState.SaveSlots);
    }

    // ═══════════════════════════════════════════════════════════════
    // Mode Select
    // ═══════════════════════════════════════════════════════════════

    public void OnDemoClicked() => SelectMode(_demoConfig);
    public void OnShowcaseClicked() => SelectMode(_showcaseConfig);
    public void OnFullClicked() => SelectMode(_fullConfig);

    private void SelectMode(GameModeConfig config)
    {
        if (config == null) return;
        _selectedConfig = config;

        // Update game panel labels
        if (_modeNameLabel != null) _modeNameLabel.text = config.modeName;
        if (_modeDescLabel != null) _modeDescLabel.text = config.modeDescription;

        // Continue button — visible only if a matching save exists
        int continueSlot = FindMostRecentSlot(config.modeName);
        if (_continueButton != null)
            _continueButton.gameObject.SetActive(continueSlot >= 0);

        ShowPanel(MenuState.GamePanel);
    }

    // ═══════════════════════════════════════════════════════════════
    // Game Panel
    // ═══════════════════════════════════════════════════════════════

    public void OnNewGame()
    {
        if (_loading) return;

        ActiveConfig = _selectedConfig;

        // Find first empty slot
        int slot = FindFirstEmptySlot();
        if (slot < 0) slot = 0; // overwrite slot 0 if all full
        SaveManager.ActiveSlot = slot;

        // Delete any existing save in this slot so NameEntryScreen starts fresh
        SaveManager.DeleteSlot(slot);

        // Clear all static registries to prevent stale state from previous games
        DateHistory.LoadFrom(null);
        ItemStateRegistry.Clear();
        PlayerData.PlayerName = "Nema";

        if (_tutorialCard != null)
        {
            _tutorialCard.Show(() => LoadApartment());
        }
        else
        {
            LoadApartment();
        }
    }

    public void OnContinue()
    {
        if (_loading || _selectedConfig == null) return;

        int slot = FindMostRecentSlot(_selectedConfig.modeName);
        if (slot < 0) return;

        ActiveConfig = _selectedConfig;
        SaveManager.ActiveSlot = slot;
        LoadApartment();
    }

    public void OnLoadSave()
    {
        RefreshSlotLabels();
        ShowPanel(MenuState.SaveSlots);
    }

    public void OnGamePanelBack()
    {
        ShowPanel(MenuState.ModeSelect);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // ═══════════════════════════════════════════════════════════════
    // Save Slots
    // ═══════════════════════════════════════════════════════════════

    private void RefreshSlotLabels()
    {
        TMP_Text[] labels = { _slot0Label, _slot1Label, _slot2Label };
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (labels[i] == null) continue;

            var data = SaveManager.PeekSlot(i);
            if (data != null)
            {
                string mode = string.IsNullOrEmpty(data.gameModeName) ? "Unknown" : data.gameModeName;
                labels[i].text = $"Slot {i + 1}: {mode} \u2014 Day {data.currentDay}";
            }
            else
            {
                labels[i].text = $"Slot {i + 1}: Empty";
            }
        }
    }

    public void OnSlotClicked(int slot)
    {
        if (_loading) return;
        if (!SaveManager.HasSave(slot)) return;

        ActiveConfig = _selectedConfig;
        SaveManager.ActiveSlot = slot;
        LoadApartment();
    }

    public void OnSaveSlotBack()
    {
        ShowPanel(MenuState.GamePanel);
    }

    // ═══════════════════════════════════════════════════════════════
    // Scene loading
    // ═══════════════════════════════════════════════════════════════

    private void LoadApartment()
    {
        if (_loading) return;
        _loading = true;

        int targetIndex = _apartmentSceneIndex >= 0
            ? _apartmentSceneIndex
            : SceneManager.GetActiveScene().buildIndex + 1;

        if (ScreenFade.Instance != null && _fadeDuration > 0f)
            StartCoroutine(FadeAndLoad(targetIndex));
        else
            SceneManager.LoadScene(targetIndex);
    }

    private IEnumerator FadeAndLoad(int sceneIndex)
    {
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);
        SceneManager.LoadScene(sceneIndex);
    }

    // ═══════════════════════════════════════════════════════════════
    // Slot helpers
    // ═══════════════════════════════════════════════════════════════

    private static int FindFirstEmptySlot()
    {
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (!SaveManager.HasSave(i)) return i;
        }
        return -1;
    }

    private static int FindMostRecentSlot(string modeName)
    {
        int bestSlot = -1;
        int bestDay = -1;

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            var data = SaveManager.PeekSlot(i);
            if (data == null) continue;
            if (data.gameModeName != modeName) continue;

            if (data.currentDay > bestDay)
            {
                bestDay = data.currentDay;
                bestSlot = i;
            }
        }

        return bestSlot;
    }
}
