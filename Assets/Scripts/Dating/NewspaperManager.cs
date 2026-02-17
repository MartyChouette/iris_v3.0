using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

public class NewspaperManager : MonoBehaviour, IStationManager
{
    public enum State { ReadingPaper, Calling, Done }

    public static NewspaperManager Instance { get; private set; }

    public bool IsAtIdleState => CurrentState == State.Done;

    // ─── References ───────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private NewspaperSurface surface;
    [SerializeField] private Camera mainCamera;

    [Tooltip("WorldSpace Canvas overlay with newspaper text + images (shown when reading).")]
    [SerializeField] private GameObject newspaperOverlay;

    // ─── Cameras (Cinemachine 3) ──────────────────────────────────
    [Header("Cameras")]
    [Tooltip("First-person newspaper reading view (held up in front of player).")]
    [SerializeField] private CinemachineCamera readCamera;

    [SerializeField] private CinemachineBrain brain;

    // ─── Newspaper Object ─────────────────────────────────────────
    [Header("Newspaper Object")]
    [Tooltip("The newspaper quad/plane.")]
    [SerializeField] private Transform newspaperTransform;

    // ─── Dynamic Layout ─────────────────────────────────────────
    [Header("Dynamic Layout")]
    [Tooltip("RectTransform parent for dynamically built ad slots (inside the canvas).")]
    [SerializeField] private RectTransform _contentParent;

    [Tooltip("Background Image on the canvas (for swappable sprite from pool SO).")]
    [SerializeField] private Image _backgroundImage;

    // ─── Calling Phase ────────────────────────────────────────────
    [Header("Calling Phase")]
    [SerializeField] private float callingDuration = 2f;
    [SerializeField] private AudioClip phoneRingSFX;

    [Tooltip("SFX played when the player selects a date's personal ad.")]
    [SerializeField] private AudioClip dateSelectedSFX;

    // ─── UI ───────────────────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private GameObject callingUI;
    [SerializeField] private TMP_Text callingText;

    // ─── Events ───────────────────────────────────────────────────
    [Header("Events")]
    public UnityEvent<DatePersonalDefinition> OnDateSelected;
    public UnityEvent OnNewspaperDone;

    // ─── Input ────────────────────────────────────────────────────
    private InputAction _clickAction;
    private InputAction _mousePositionAction;

    // ─── State ────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Done;

    /// <summary>The read camera for external priority control (DayPhaseManager).</summary>
    public CinemachineCamera ReadCamera => readCamera;

    /// <summary>The newspaper quad transform for repositioning (tossed position).</summary>
    public Transform NewspaperTransform => newspaperTransform;

    private DatePersonalDefinition _selectedDefinition;

    // Dynamic slot tracking
    private readonly List<NewspaperAdSlot> _personalSlotsList = new List<NewspaperAdSlot>();
    private readonly List<NewspaperAdSlot> _commercialSlotsList = new List<NewspaperAdSlot>();

    // Shared tooltip panel (created once, reused across rebuilds)
    private GameObject _tooltipPanel;
    private TMP_Text _tooltipText;

    // Layout constants
    private const float CanvasWidth = 500f;
    private const float CanvasHeight = 700f;
    private const float HeaderHeight = 70f;
    private const float Padding = 15f;
    private const float SlotSpacing = 8f;
    private const float PersonalWeight = 2f;
    private const float DefaultCommercialWeight = 1f;

    // Cached coroutine waits
    private static readonly WaitForSeconds s_waitCutPause = new WaitForSeconds(0.3f);
    private WaitForSeconds _waitCallingDuration;

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NewspaperManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("Click", InputActionType.Button, "<Mouse>/leftButton");
        _mousePositionAction = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");

        HideAllUI();
        _waitCallingDuration = new WaitForSeconds(callingDuration);

        if (dayManager != null)
            dayManager.OnNewNewspaper.AddListener(OnNewNewspaper);
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _mousePositionAction.Enable();
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _mousePositionAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // No-op — state is managed by OnNewNewspaper / DayPhaseManager
    }

    // ─── State Transitions ────────────────────────────────────────

    private void EnterReadingPaper()
    {
        CurrentState = State.ReadingPaper;
        Debug.Log("[NewspaperManager] Reading paper. Click a personal ad to select!");

        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(true);
    }

    private void EnterDone()
    {
        CurrentState = State.Done;
        Debug.Log("[NewspaperManager] Newspaper done — handing off to DayPhaseManager.");

        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(false);

        OnNewspaperDone?.Invoke();
    }

    /// <summary>
    /// Called by NewspaperAdSlot when the player holds a personal ad.
    /// </summary>
    public void SelectPersonalAd(DatePersonalDefinition def)
    {
        if (CurrentState != State.ReadingPaper) return;
        if (def == null) return;

        _selectedDefinition = def;
        OnDateSelected?.Invoke(def);

        if (dateSelectedSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(dateSelectedSFX);

        if (newspaperOverlay != null)
            newspaperOverlay.SetActive(false);

        if (surface != null)
            surface.PlayPoofEffect();

        Debug.Log($"[NewspaperManager] Selected {def.characterName}'s ad!");
        StartCoroutine(SelectionThenCalling());
    }

    private IEnumerator SelectionThenCalling()
    {
        yield return s_waitCutPause;
        EnterCalling();
    }

    private void EnterCalling()
    {
        CurrentState = State.Calling;

        if (phoneRingSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phoneRingSFX);

        if (callingUI != null) callingUI.SetActive(true);
        if (callingText != null)
            callingText.text = $"Calling {_selectedDefinition.characterName}...";

        StartCoroutine(CallingSequence());
    }

    private IEnumerator CallingSequence()
    {
        yield return _waitCallingDuration;

        if (callingUI != null) callingUI.SetActive(false);

        DateSessionManager.Instance?.ScheduleDate(_selectedDefinition);
        PhoneController.Instance?.SetPendingDate(_selectedDefinition);

        EnterDone();
    }

    // ─── Newspaper Regeneration (Self-Constructing Layout) ──────

    private void OnNewNewspaper()
    {
        if (dayManager == null) return;

        Debug.Log($"[NewspaperManager] Generating newspaper for day {dayManager.CurrentDay}.");

        // Resize canvas to half-page on first call
        if (_contentParent != null)
            _contentParent.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        // Destroy old dynamic slots
        ClearDynamicSlots();

        // Apply background sprite if pool provides one
        var pool = dayManager.Pool;
        if (_backgroundImage != null && pool != null && pool.backgroundSprite != null)
            _backgroundImage.sprite = pool.backgroundSprite;

        // Build header (title + day number)
        BuildHeader(pool?.newspaperTitle ?? "The Daily Bloom", dayManager.CurrentDay);

        // Build mixed ad column: Personal, Commercial, Personal, Commercial, Personal
        var personals = dayManager.TodayPersonals;
        var commercials = dayManager.TodayCommercials;
        BuildMixedAdColumn(personals, commercials);

        // Reset cut surface
        if (surface != null)
            surface.ResetSurface();

        _selectedDefinition = null;
        HideAllUI();

        EnterReadingPaper();
    }

    private void ClearDynamicSlots()
    {
        _personalSlotsList.Clear();
        _commercialSlotsList.Clear();

        if (_contentParent == null) return;

        // Destroy all children except the background image
        for (int i = _contentParent.childCount - 1; i >= 0; i--)
        {
            var child = _contentParent.GetChild(i);
            if (_backgroundImage != null && child.gameObject == _backgroundImage.gameObject)
                continue;
            Destroy(child.gameObject);
        }
    }

    private void BuildHeader(string title, int day)
    {
        if (_contentParent == null) return;

        // Title text
        var titleGO = CreateTMP("Header_Title", _contentParent,
            new Vector2(0f, CanvasHeight * 0.5f - 25f),
            new Vector2(CanvasWidth - Padding * 2f, 35f),
            title, 26f, FontStyles.Bold, TextAlignmentOptions.Center);

        // Day label
        CreateTMP("Header_Day", _contentParent,
            new Vector2(0f, CanvasHeight * 0.5f - 55f),
            new Vector2(CanvasWidth - Padding * 2f, 22f),
            $"Day {day}", 18f, FontStyles.Italic, TextAlignmentOptions.Center);

        // Rule line under header
        var ruleGO = new GameObject("Header_Rule");
        ruleGO.transform.SetParent(_contentParent, false);
        var ruleRT = ruleGO.AddComponent<RectTransform>();
        ruleRT.anchorMin = new Vector2(0.5f, 0.5f);
        ruleRT.anchorMax = new Vector2(0.5f, 0.5f);
        ruleRT.anchoredPosition = new Vector2(0f, CanvasHeight * 0.5f - HeaderHeight);
        ruleRT.sizeDelta = new Vector2(CanvasWidth - Padding * 2f, 2f);
        ruleRT.localScale = Vector3.one;
        ruleGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
    }

    private void BuildMixedAdColumn(List<DatePersonalDefinition> personals, List<CommercialAdDefinition> commercials)
    {
        if (_contentParent == null) return;

        // Interleave: P, C, P, C, P (up to what's available)
        var slotEntries = new List<SlotEntry>();
        int pIdx = 0, cIdx = 0;
        int pCount = personals?.Count ?? 0;
        int cCount = commercials?.Count ?? 0;

        while (pIdx < pCount || cIdx < cCount)
        {
            if (pIdx < pCount)
                slotEntries.Add(new SlotEntry { isPersonal = true, personalIndex = pIdx++ });
            if (cIdx < cCount)
                slotEntries.Add(new SlotEntry { isPersonal = false, commercialIndex = cIdx++ });
        }

        if (slotEntries.Count == 0) return;

        // Compute weights and total height
        float totalWeight = 0f;
        for (int i = 0; i < slotEntries.Count; i++)
        {
            float w;
            if (slotEntries[i].isPersonal)
                w = PersonalWeight;
            else
                w = commercials[slotEntries[i].commercialIndex].sizeWeight;
            slotEntries[i] = new SlotEntry
            {
                isPersonal = slotEntries[i].isPersonal,
                personalIndex = slotEntries[i].personalIndex,
                commercialIndex = slotEntries[i].commercialIndex,
                weight = w
            };
            totalWeight += w;
        }

        float contentTop = CanvasHeight * 0.5f - HeaderHeight - 5f;
        float contentBottom = -CanvasHeight * 0.5f + Padding;
        float availableHeight = contentTop - contentBottom;
        float totalSpacing = SlotSpacing * (slotEntries.Count - 1);
        float usableHeight = availableHeight - totalSpacing;

        // Ensure tooltip panel exists
        EnsureTooltipPanel();

        float currentY = contentTop;
        for (int i = 0; i < slotEntries.Count; i++)
        {
            var entry = slotEntries[i];
            float slotH = usableHeight * (entry.weight / totalWeight);
            float slotCenterY = currentY - slotH * 0.5f;
            float slotW = CanvasWidth - Padding * 2f;

            if (entry.isPersonal && personals != null && entry.personalIndex < personals.Count)
            {
                var slot = CreatePersonalSlotUI(entry.personalIndex, personals[entry.personalIndex],
                    new Vector2(0f, slotCenterY), new Vector2(slotW, slotH));
                _personalSlotsList.Add(slot);
            }
            else if (!entry.isPersonal && commercials != null && entry.commercialIndex < commercials.Count)
            {
                var slot = CreateCommercialSlotUI(entry.commercialIndex, commercials[entry.commercialIndex],
                    new Vector2(0f, slotCenterY), new Vector2(slotW, slotH));
                _commercialSlotsList.Add(slot);
            }

            currentY -= slotH + SlotSpacing;
        }
    }

    private struct SlotEntry
    {
        public bool isPersonal;
        public int personalIndex;
        public int commercialIndex;
        public float weight;
    }

    // ─── Slot UI Builders ───────────────────────────────────────

    private NewspaperAdSlot CreatePersonalSlotUI(int index, DatePersonalDefinition def,
        Vector2 center, Vector2 size)
    {
        string prefix = $"Personal_{index}";
        float nameFontSize = Mathf.Clamp(size.y * 0.14f, 14f, 26f);
        float bodyFontSize = Mathf.Clamp(size.y * 0.10f, 10f, 16f);
        float phoneFontSize = Mathf.Clamp(size.y * 0.11f, 12f, 20f);
        float portraitSize = Mathf.Clamp(size.y * 0.32f, 28f, 48f);

        // Slot container
        var containerGO = new GameObject(prefix);
        containerGO.transform.SetParent(_contentParent, false);
        var containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = center;
        containerRT.sizeDelta = size;
        containerRT.localScale = Vector3.one;

        // Background
        var bgImg = containerGO.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.06f);
        bgImg.raycastTarget = true;

        // Name
        float nameOffsetY = size.y * 0.35f;
        var nameGO = CreateTMP($"{prefix}_Name", containerRT,
            new Vector2(-portraitSize * 0.3f, nameOffsetY),
            new Vector2(size.x - portraitSize - 20f, nameFontSize + 6f),
            "Name", nameFontSize, FontStyles.Bold, TextAlignmentOptions.Left);
        var nameTMP = nameGO.GetComponent<TMP_Text>();

        // Portrait
        var portraitGO = new GameObject($"{prefix}_Portrait");
        portraitGO.transform.SetParent(containerRT, false);
        var portraitRT = portraitGO.AddComponent<RectTransform>();
        portraitRT.anchorMin = new Vector2(0.5f, 0.5f);
        portraitRT.anchorMax = new Vector2(0.5f, 0.5f);
        portraitRT.anchoredPosition = new Vector2(size.x * 0.5f - portraitSize * 0.5f - 5f, nameOffsetY);
        portraitRT.sizeDelta = new Vector2(portraitSize, portraitSize);
        portraitRT.localScale = Vector3.one;
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        portraitImg.raycastTarget = false;
        portraitGO.SetActive(false);

        // Ad body
        var adGO = CreateTMP($"{prefix}_Ad", containerRT,
            new Vector2(0f, 0f),
            new Vector2(size.x - 20f, size.y * 0.4f),
            "Ad text...", bodyFontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        var adTMP = adGO.GetComponent<TMP_Text>();

        // Add KeywordTooltip to the ad text
        var kwTooltip = adGO.AddComponent<KeywordTooltip>();
        kwTooltip.InitReferences(adTMP, _tooltipPanel, _tooltipText, mainCamera);

        // Phone number
        float phoneOffsetY = -size.y * 0.35f;
        var phoneGO = CreateTMP($"{prefix}_Phone", containerRT,
            new Vector2(0f, phoneOffsetY),
            new Vector2(size.x - 20f, phoneFontSize + 6f),
            "555-0000", phoneFontSize, FontStyles.Italic, TextAlignmentOptions.Left);
        var phoneTMP = phoneGO.GetComponent<TMP_Text>();

        // Hold progress fill
        var fillGO = new GameObject($"{prefix}_Fill");
        fillGO.transform.SetParent(containerRT, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 0f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = new Vector2(0f, 4f);
        fillRT.localScale = Vector3.one;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.6f, 1f, 0.7f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        fillImg.raycastTarget = false;
        fillGO.SetActive(false);

        // NewspaperAdSlot component
        var slot = containerGO.AddComponent<NewspaperAdSlot>();
        slot.InitReferences(containerRT, nameTMP, adTMP, phoneTMP,
            portraitImg, null, fillImg, PersonalWeight);

        // Assign data
        slot.AssignPersonal(def);

        return slot;
    }

    private NewspaperAdSlot CreateCommercialSlotUI(int index, CommercialAdDefinition def,
        Vector2 center, Vector2 size)
    {
        string prefix = $"Commercial_{index}";
        float nameFontSize = Mathf.Clamp(size.y * 0.18f, 12f, 22f);
        float bodyFontSize = Mathf.Clamp(size.y * 0.14f, 10f, 16f);

        // Slot container
        var containerGO = new GameObject(prefix);
        containerGO.transform.SetParent(_contentParent, false);
        var containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = center;
        containerRT.sizeDelta = size;
        containerRT.localScale = Vector3.one;

        // Background (slightly different tint for commercials)
        var bgImg = containerGO.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.10f, 0.08f, 0.08f);
        bgImg.raycastTarget = false;

        // Name
        float nameOffsetY = size.y * 0.25f;
        var nameGO = CreateTMP($"{prefix}_Name", containerRT,
            new Vector2(0f, nameOffsetY),
            new Vector2(size.x - 20f, nameFontSize + 4f),
            "Business", nameFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        var nameTMP = nameGO.GetComponent<TMP_Text>();

        // Ad body
        var adGO = CreateTMP($"{prefix}_Ad", containerRT,
            new Vector2(0f, -5f),
            new Vector2(size.x - 20f, size.y * 0.5f),
            "Ad text...", bodyFontSize, FontStyles.Italic, TextAlignmentOptions.Center);
        var adTMP = adGO.GetComponent<TMP_Text>();

        // Logo placeholder (hidden by default)
        var logoGO = new GameObject($"{prefix}_Logo");
        logoGO.transform.SetParent(containerRT, false);
        var logoRT = logoGO.AddComponent<RectTransform>();
        logoRT.anchorMin = new Vector2(0.5f, 0.5f);
        logoRT.anchorMax = new Vector2(0.5f, 0.5f);
        logoRT.anchoredPosition = new Vector2(size.x * 0.35f, 0f);
        logoRT.sizeDelta = new Vector2(size.y * 0.4f, size.y * 0.4f);
        logoRT.localScale = Vector3.one;
        var logoImg = logoGO.AddComponent<Image>();
        logoImg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        logoImg.raycastTarget = false;
        logoGO.SetActive(false);

        // NewspaperAdSlot component
        var slot = containerGO.AddComponent<NewspaperAdSlot>();
        slot.InitReferences(containerRT, nameTMP, adTMP, null,
            null, logoImg, null, def.sizeWeight);

        // Assign data
        slot.AssignCommercial(def);

        return slot;
    }

    // ─── Tooltip Panel ──────────────────────────────────────────

    private void EnsureTooltipPanel()
    {
        if (_tooltipPanel != null) return;
        if (_contentParent == null) return;

        _tooltipPanel = new GameObject("KeywordTooltip");
        _tooltipPanel.transform.SetParent(_contentParent, false);
        var tooltipRT = _tooltipPanel.AddComponent<RectTransform>();
        tooltipRT.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRT.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRT.sizeDelta = new Vector2(220f, 60f);
        tooltipRT.anchoredPosition = Vector2.zero;
        tooltipRT.localScale = Vector3.one;
        var tooltipBg = _tooltipPanel.AddComponent<Image>();
        tooltipBg.color = new Color(0.08f, 0.08f, 0.10f, 0.9f);
        tooltipBg.raycastTarget = false;

        var tooltipTextGO = new GameObject("TooltipText");
        tooltipTextGO.transform.SetParent(_tooltipPanel.transform, false);
        var tooltipTextRT = tooltipTextGO.AddComponent<RectTransform>();
        tooltipTextRT.anchorMin = Vector2.zero;
        tooltipTextRT.anchorMax = Vector2.one;
        tooltipTextRT.offsetMin = new Vector2(6f, 4f);
        tooltipTextRT.offsetMax = new Vector2(-6f, -4f);
        tooltipTextRT.localScale = Vector3.one;
        _tooltipText = tooltipTextGO.AddComponent<TextMeshProUGUI>();
        _tooltipText.text = "";
        _tooltipText.fontSize = 12f;
        _tooltipText.color = new Color(0.9f, 0.9f, 0.9f);
        _tooltipText.alignment = TextAlignmentOptions.TopLeft;
        _tooltipText.enableWordWrapping = true;

        _tooltipPanel.SetActive(false);
    }

    // ─── Helpers ────────────────────────────────────────────────

    private static GameObject CreateTMP(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, string text,
        float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = new Color(0.12f, 0.12f, 0.12f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return go;
    }

    private void HideAllUI()
    {
        if (callingUI != null) callingUI.SetActive(false);
    }
}
