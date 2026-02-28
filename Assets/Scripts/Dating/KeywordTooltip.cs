using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to a TMP_Text with <link> tagged keywords.
/// Shows a tooltip panel on hover. Tracks visited links and recolors them.
/// </summary>
public class KeywordTooltip : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The TMP_Text containing <link> tagged keywords.")]
    [SerializeField] private TMP_Text _targetText;

    [Tooltip("Tooltip panel (child with background + text). Positioned near hovered link.")]
    [SerializeField] private GameObject _tooltipPanel;

    [Tooltip("TMP_Text inside the tooltip panel for commentary text.")]
    [SerializeField] private TMP_Text _tooltipText;

    [Tooltip("Main camera used for screen-space calculations.")]
    [SerializeField] private Camera _mainCamera;

    [Header("Colors")]
    [Tooltip("Color for unvisited keyword links.")]
    [SerializeField] private Color _unvisitedColor = new Color(0.27f, 0.53f, 1f);

    [Tooltip("Color for visited keyword links.")]
    [SerializeField] private Color _visitedColor = new Color(0.13f, 0.13f, 0.13f);

    // Maps link ID → commentary text
    private Dictionary<string, string> _commentaryMap = new Dictionary<string, string>();
    private HashSet<string> _visitedLinks = new HashSet<string>();
    private int _lastHoveredLinkIndex = -1;

    /// <summary>
    /// Wire references at runtime (used by NewspaperManager dynamic layout).
    /// </summary>
    public void InitReferences(TMP_Text targetText, GameObject tooltipPanel, TMP_Text tooltipText, Camera mainCamera)
    {
        _targetText = targetText;
        _tooltipPanel = tooltipPanel;
        _tooltipText = tooltipText;
        _mainCamera = mainCamera;
    }

    /// <summary>
    /// Register a keyword's link ID and its commentary text.
    /// Called by NewspaperAdSlot after wrapping keywords in links.
    /// </summary>
    public void RegisterKeyword(string linkId, string commentary)
    {
        _commentaryMap[linkId] = commentary;
    }

    /// <summary>
    /// Clear all registered keywords and visited state (for newspaper regeneration).
    /// </summary>
    public void Clear()
    {
        _commentaryMap.Clear();
        _visitedLinks.Clear();
        _lastHoveredLinkIndex = -1;
        if (_tooltipPanel != null)
            _tooltipPanel.SetActive(false);
    }

    private void Awake()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.SetActive(false);
    }

    private void Update()
    {
        if (_targetText == null || _commentaryMap.Count == 0) return;

        // Find link under mouse
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_targetText, Input.mousePosition, _mainCamera);

        if (linkIndex != -1 && linkIndex < _targetText.textInfo.linkCount)
        {
            var linkInfo = _targetText.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();

            if (_commentaryMap.TryGetValue(linkId, out string commentary))
            {
                // Show tooltip
                if (_tooltipPanel != null)
                {
                    _tooltipPanel.SetActive(true);
                    PositionTooltip();
                }
                if (_tooltipText != null)
                    _tooltipText.text = commentary;

                // Mark as visited on first hover
                if (!_visitedLinks.Contains(linkId))
                {
                    _visitedLinks.Add(linkId);
                    RecolorVisitedLink(linkId);
                }

                _lastHoveredLinkIndex = linkIndex;
                return;
            }
        }

        // No link hovered — hide tooltip
        if (_tooltipPanel != null && _tooltipPanel.activeSelf)
            _tooltipPanel.SetActive(false);
        _lastHoveredLinkIndex = -1;
    }

    private void PositionTooltip()
    {
        if (_tooltipPanel == null) return;

        var rt = _tooltipPanel.GetComponent<RectTransform>();
        if (rt == null) return;

        // Ensure tooltip renders on top of sibling elements
        _tooltipPanel.transform.SetAsLastSibling();

        // Dynamic sizing: fit to text content
        if (_tooltipText != null)
        {
            _tooltipText.ForceMeshUpdate();
            float textW = _tooltipText.preferredWidth;
            float textH = _tooltipText.preferredHeight;
            float padX = 12f;
            float padY = 8f;
            float maxW = 280f;
            float w = Mathf.Min(textW + padX * 2f, maxW);
            // If clamped to maxW, recalculate height with wrapping
            if (textW + padX * 2f > maxW)
            {
                var textRT = _tooltipText.GetComponent<RectTransform>();
                if (textRT != null)
                {
                    textRT.offsetMin = new Vector2(padX, padY);
                    textRT.offsetMax = new Vector2(-padX, -padY);
                }
                _tooltipText.ForceMeshUpdate();
                textH = _tooltipText.preferredHeight;
            }
            float h = textH + padY * 2f;
            rt.sizeDelta = new Vector2(w, h);
        }

        // Position tooltip near mouse with bounds clamping
        Vector2 mousePos = Input.mousePosition;
        float offsetX = 20f;
        float offsetY = -30f;

        var parentCanvas = _tooltipPanel.GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            RectTransform canvasRT = parentCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, mousePos, _mainCamera, out Vector2 localPoint);

            float tipW = rt.sizeDelta.x;
            float tipH = rt.sizeDelta.y;
            float canvasW = canvasRT.sizeDelta.x;
            float canvasH = canvasRT.sizeDelta.y;

            float x = localPoint.x + offsetX;
            float y = localPoint.y + offsetY;

            // Flip left if overflowing right
            if (x + tipW * 0.5f > canvasW * 0.5f)
                x = localPoint.x - offsetX - tipW;
            // Flip above if overflowing bottom
            if (y - tipH < -canvasH * 0.5f)
                y = localPoint.y - offsetY;

            rt.localPosition = new Vector3(x, y, 0f);
        }
        else
        {
            float tipW = rt.sizeDelta.x;
            float tipH = rt.sizeDelta.y;

            float x = mousePos.x + offsetX;
            float y = mousePos.y + offsetY;

            // Flip left if overflowing right
            if (x + tipW > Screen.width)
                x = mousePos.x - offsetX - tipW;
            // Flip above if overflowing bottom
            if (y - tipH < 0f)
                y = mousePos.y - offsetY;

            rt.position = new Vector3(x, y, 0f);
        }
    }

    private void RecolorVisitedLink(string linkId)
    {
        if (_targetText == null) return;

        // Replace the link color from unvisited to visited in the source text
        string hex = ColorUtility.ToHtmlStringRGB(_visitedColor);
        string oldTag = $"<link=\"{linkId}\"><color=#{ColorUtility.ToHtmlStringRGB(_unvisitedColor)}>";
        string newTag = $"<link=\"{linkId}\"><color=#{hex}>";

        string current = _targetText.text;
        if (current.Contains(oldTag))
        {
            _targetText.text = current.Replace(oldTag, newTag);
        }
    }
}
