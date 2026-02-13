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

    private DatePersonalDefinition _personalDef;
    private CommercialAdDefinition _commercialDef;
    private bool _isPersonalAd;
    private Rect _phoneNumberBoundsUV;

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
        NewspaperManager.Instance?.SelectPersonalAd(_personalDef);
        // Progress visual stays filled briefly — SelectPersonalAd triggers poof + state change
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
            adLabel.SetText(def.adText);

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
}
