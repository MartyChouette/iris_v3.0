using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class NewspaperAdSlot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Layout")]
    [Tooltip("Positioned within the newspaper's world-space canvas.")]
    [SerializeField] private RectTransform slotRect;

    [Tooltip("Relative height weight for flex layout. Assigned at runtime.")]
    [SerializeField] private float _sizeWeight = 1f;
    public float SizeWeight => _sizeWeight;

    [Tooltip("Label for character name (personal ads) or business name (commercial).")]
    [SerializeField] private TMP_Text nameLabel;

    [Tooltip("Label for the ad body text.")]
    [SerializeField] private TMP_Text adLabel;

    [Tooltip("Phone number label — only visible for personal ads.")]
    [SerializeField] private TMP_Text phoneNumberLabel;

    [Header("Images")]
    [Tooltip("Portrait image for personal ads.")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Logo image for commercial ads.")]
    [SerializeField] private Image logoImage;

    [Header("Hold-to-Clip")]
    [Tooltip("How long the player must hold to clip the ad (seconds).")]
    [SerializeField] private float holdDuration = 0.8f;

    [Tooltip("Fill image that shows clip progress (Image.fillAmount driven).")]
    [SerializeField] private Image progressFill;

    [Header("Runtime — Set by NewspaperManager")]
    [Tooltip("0-1 UV rect of this slot on the newspaper surface.")]
    [SerializeField] private Rect normalizedBounds;

    [Header("Player Ad")]
    [Tooltip("If true, this slot shows the player's own ad (non-interactive).")]
    [SerializeField] private bool _isPlayerAd;

    private DatePersonalDefinition _personalDef;
    private CommercialAdDefinition _commercialDef;
    private bool _isPersonalAd;
    private Rect _phoneNumberBoundsUV;

    private bool _isLocked;
    private Image _lockOverlay;
    private TMP_Text _lockLabel;

    private bool _isHolding;
    private float _holdTimer;
    private bool _completed;

    // ─── Lifecycle ──────────────────────────────────────────────

    private void Awake()
    {
        ResetProgress();
    }

    private void Update()
    {
        if (!_isHolding || _completed) return;

        _holdTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_holdTimer / holdDuration);

        if (progressFill != null)
            progressFill.fillAmount = t;

        if (t >= 1f)
        {
            _completed = true;
            OnHoldComplete();
        }
    }

    // ─── Pointer Events ─────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isLocked) return;
        if (_isPlayerAd) return; // Player's own ad is decorative
        if (!_isPersonalAd || _personalDef == null) return;
        if (NewspaperManager.Instance == null) return;
        if (NewspaperManager.Instance.CurrentState != NewspaperManager.State.ReadingPaper) return;

        _isHolding = true;
        _holdTimer = 0f;
        _completed = false;

        if (progressFill != null)
        {
            progressFill.gameObject.SetActive(true);
            progressFill.fillAmount = 0f;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_completed) return;

        // Released early — cancel
        ResetProgress();
    }

    private void OnHoldComplete()
    {
        _isHolding = false;
        if (_personalDef != null)
            NewspaperManager.Instance?.SelectPersonalAd(_personalDef);
    }

    private void ResetProgress()
    {
        _isHolding = false;
        _holdTimer = 0f;
        _completed = false;

        if (progressFill != null)
        {
            progressFill.fillAmount = 0f;
            progressFill.gameObject.SetActive(false);
        }
    }

    // ─── Public Properties ────────────────────────────────────────

    public bool IsPersonalAd => _isPersonalAd;
    public DatePersonalDefinition PersonalDef => _personalDef;
    public CommercialAdDefinition CommercialDef => _commercialDef;
    public Rect NormalizedBounds => normalizedBounds;
    public Rect PhoneNumberBoundsUV => _phoneNumberBoundsUV;

    // ─── Public API ───────────────────────────────────────────────

    /// <summary>
    /// Lock this slot so the player can read it but not select it.
    /// Shows a semi-transparent overlay with reason text.
    /// </summary>
    public void SetLocked(bool locked, string reason = "Tutorial Date")
    {
        _isLocked = locked;

        if (locked)
        {
            // Create overlay if needed
            if (_lockOverlay == null && slotRect != null)
            {
                var overlayGO = new GameObject("LockOverlay");
                overlayGO.transform.SetParent(slotRect, false);
                var overlayRT = overlayGO.AddComponent<RectTransform>();
                overlayRT.anchorMin = Vector2.zero;
                overlayRT.anchorMax = Vector2.one;
                overlayRT.offsetMin = Vector2.zero;
                overlayRT.offsetMax = Vector2.zero;
                overlayRT.localScale = Vector3.one;
                _lockOverlay = overlayGO.AddComponent<Image>();
                _lockOverlay.color = new Color(0.05f, 0.05f, 0.05f, 0.45f);
                _lockOverlay.raycastTarget = false;

                var labelGO = new GameObject("LockLabel");
                labelGO.transform.SetParent(overlayRT, false);
                var labelRT = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
                labelRT.localScale = Vector3.one;
                _lockLabel = labelGO.AddComponent<TextMeshProUGUI>();
                _lockLabel.alignment = TextAlignmentOptions.Center;
                _lockLabel.fontSize = 14f;
                _lockLabel.fontStyle = FontStyles.Italic;
                _lockLabel.color = new Color(0.85f, 0.75f, 0.65f, 0.9f);
                _lockLabel.raycastTarget = false;
            }

            if (_lockOverlay != null)
                _lockOverlay.gameObject.SetActive(true);
            if (_lockLabel != null)
                _lockLabel.text = reason;
        }
        else
        {
            if (_lockOverlay != null)
                _lockOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Populate this slot with a personal ad.
    /// </summary>
    public void AssignPersonal(DatePersonalDefinition def)
    {
        ResetProgress();
        _personalDef = def;
        _commercialDef = null;
        _isPersonalAd = true;

        if (nameLabel != null)
            nameLabel.SetText(def.characterName);

        if (adLabel != null)
        {
            string adText = def.adText;

            // Wrap keywords in <link> tags for hover tooltips
            var tooltip = adLabel.GetComponent<KeywordTooltip>();
            if (tooltip != null)
            {
                tooltip.Clear();
                if (def.keywords != null)
                {
                    string unvisitedHex = "4588FF";
                    for (int k = 0; k < def.keywords.Length; k++)
                    {
                        var kw = def.keywords[k];
                        if (string.IsNullOrEmpty(kw.keyword)) continue;
                        string linkId = $"kw_{k}";
                        string wrapped = $"<link=\"{linkId}\"><color=#{unvisitedHex}><u>{kw.keyword}</u></color></link>";
                        adText = adText.Replace(kw.keyword, wrapped);
                        tooltip.RegisterKeyword(linkId, kw.commentary);
                    }
                }
            }

            adLabel.text = adText;
        }

        if (phoneNumberLabel != null)
        {
            phoneNumberLabel.gameObject.SetActive(true);
            phoneNumberLabel.SetText("555-" + def.characterName.GetHashCode().ToString("X4").Substring(0, 4));
        }

        // Apply font size override
        if (def.fontSizeOverride > 0 && adLabel != null)
            adLabel.fontSize = def.fontSizeOverride;

        // Show portrait if available, hide logo
        if (portraitImage != null)
        {
            bool hasPortrait = def.portrait != null;
            portraitImage.gameObject.SetActive(hasPortrait);
            if (hasPortrait) portraitImage.sprite = def.portrait;
        }
        if (logoImage != null)
            logoImage.gameObject.SetActive(false);

        ComputePhoneNumberUV();

        // Show learned preference badge if this character has been dated before
        var historyEntry = DateHistory.GetLatestEntry(def.characterName);
        if (historyEntry != null)
            ShowLearnedPreferenceBadge(historyEntry);
    }

    /// <summary>
    /// Populate this slot as the player's own personal ad (decorative, non-interactive).
    /// </summary>
    public void AssignPlayerAd(Sprite portrait)
    {
        ResetProgress();
        _personalDef = null;
        _commercialDef = null;
        _isPersonalAd = false; // Not a selectable personal ad
        _isPlayerAd = true;

        if (nameLabel != null)
            nameLabel.text = PlayerData.PlayerName;

        if (adLabel != null)
            adLabel.text = "Seeking beauty in the everyday. Loves flowers, quiet mornings, and sharp scissors.";

        if (phoneNumberLabel != null)
        {
            phoneNumberLabel.gameObject.SetActive(true);
            phoneNumberLabel.text = "555-" + PlayerData.PlayerName.GetHashCode().ToString("X4").Substring(0, 4);
        }

        if (portraitImage != null)
        {
            bool hasPortrait = portrait != null;
            portraitImage.gameObject.SetActive(hasPortrait);
            if (hasPortrait) portraitImage.sprite = portrait;
        }
        if (logoImage != null)
            logoImage.gameObject.SetActive(false);

        // Hide progress fill for player ad
        if (progressFill != null)
            progressFill.gameObject.SetActive(false);

        _phoneNumberBoundsUV = Rect.zero;
    }

    /// <summary>
    /// Populate this slot with a commercial ad.
    /// </summary>
    public void AssignCommercial(CommercialAdDefinition def)
    {
        ResetProgress();
        _personalDef = null;
        _commercialDef = def;
        _isPersonalAd = false;

        if (nameLabel != null)
            nameLabel.SetText(def.businessName);

        if (adLabel != null)
            adLabel.SetText(def.adText);

        if (phoneNumberLabel != null)
            phoneNumberLabel.gameObject.SetActive(false);

        // Show logo if available, hide portrait
        if (logoImage != null)
        {
            bool hasLogo = def.logo != null;
            logoImage.gameObject.SetActive(hasLogo);
            if (hasLogo) logoImage.sprite = def.logo;
        }
        if (portraitImage != null)
            portraitImage.gameObject.SetActive(false);

        _phoneNumberBoundsUV = Rect.zero;
    }

    /// <summary>
    /// Reset this slot to empty.
    /// </summary>
    public void Clear()
    {
        ResetProgress();
        _personalDef = null;
        _commercialDef = null;
        _isPersonalAd = false;

        if (nameLabel != null) nameLabel.SetText("");
        if (adLabel != null) adLabel.SetText("");
        if (phoneNumberLabel != null)
        {
            phoneNumberLabel.SetText("");
            phoneNumberLabel.gameObject.SetActive(false);
        }
        if (portraitImage != null)
            portraitImage.gameObject.SetActive(false);
        if (logoImage != null)
            logoImage.gameObject.SetActive(false);

        _phoneNumberBoundsUV = Rect.zero;
    }

    /// <summary>
    /// Wire internal UI references at runtime (used by NewspaperManager dynamic layout).
    /// </summary>
    public void InitReferences(RectTransform rect, TMP_Text name, TMP_Text ad, TMP_Text phone,
                               Image portrait, Image logo, Image progress, float weight)
    {
        slotRect = rect;
        nameLabel = name;
        adLabel = ad;
        phoneNumberLabel = phone;
        portraitImage = portrait;
        logoImage = logo;
        progressFill = progress;
        _sizeWeight = weight;
    }

    /// <summary>
    /// Set the UV bounds of this slot on the newspaper surface.
    /// Called by NewspaperManager during slot setup.
    /// </summary>
    public void SetNormalizedBounds(Rect bounds)
    {
        normalizedBounds = bounds;
        if (_isPersonalAd)
            ComputePhoneNumberUV();
    }

    /// <summary>
    /// Sample a grid of points in phoneNumberBoundsUV, return fraction inside polygon.
    /// </summary>
    public float GetPhoneNumberCoverage(List<Vector2> cutPolygonUV)
    {
        if (!_isPersonalAd || _phoneNumberBoundsUV.width <= 0f) return 0f;
        if (cutPolygonUV == null || cutPolygonUV.Count < 3) return 0f;

        int gridRes = 20;
        int insideCount = 0;
        int totalSamples = gridRes * gridRes;

        for (int gy = 0; gy < gridRes; gy++)
        {
            for (int gx = 0; gx < gridRes; gx++)
            {
                float u = _phoneNumberBoundsUV.x + (gx + 0.5f) / gridRes * _phoneNumberBoundsUV.width;
                float v = _phoneNumberBoundsUV.y + (gy + 0.5f) / gridRes * _phoneNumberBoundsUV.height;

                if (CutPathEvaluator.PointInPolygon(new Vector2(u, v), cutPolygonUV))
                    insideCount++;
            }
        }

        return (float)insideCount / totalSamples;
    }

    // ─── Internals ────────────────────────────────────────────────

    private void ComputePhoneNumberUV()
    {
        if (_personalDef == null) return;

        // phoneNumberRect is normalized (0-1) within the slot's own bounds.
        // We map it to the newspaper's UV space using normalizedBounds.
        var pnr = _personalDef.phoneNumberRect;

        _phoneNumberBoundsUV = new Rect(
            normalizedBounds.x + pnr.x * normalizedBounds.width,
            normalizedBounds.y + pnr.y * normalizedBounds.height,
            pnr.width * normalizedBounds.width,
            pnr.height * normalizedBounds.height
        );
    }

    private void ShowLearnedPreferenceBadge(DateHistory.DateHistoryEntry entry)
    {
        if (slotRect == null) return;

        var badgeGO = new GameObject("LearnedPreferenceBadge");
        badgeGO.transform.SetParent(slotRect, false);
        var badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0f, 0f);
        badgeRT.anchorMax = new Vector2(1f, 0.25f);
        badgeRT.offsetMin = new Vector2(4f, 2f);
        badgeRT.offsetMax = new Vector2(-4f, 0f);
        badgeRT.localScale = Vector3.one;

        var badgeTMP = badgeGO.AddComponent<TextMeshProUGUI>();
        badgeTMP.fontSize = 10f;
        badgeTMP.alignment = TextAlignmentOptions.BottomLeft;
        badgeTMP.color = new Color(0.6f, 0.55f, 0.5f, 0.9f);
        badgeTMP.raycastTarget = false;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<i>(Dated Day {entry.day} — {entry.grade})</i>");

        if (entry.learnedLikes.Count > 0)
        {
            sb.Append("\n<color=#4CAF50>");
            foreach (var like in entry.learnedLikes)
                sb.Append($"+ {like} ");
            sb.Append("</color>");
        }

        if (entry.learnedDislikes.Count > 0)
        {
            sb.Append("\n<color=#E57373>");
            foreach (var dislike in entry.learnedDislikes)
                sb.Append($"- {dislike} ");
            sb.Append("</color>");
        }

        badgeTMP.text = sb.ToString();
    }
}
