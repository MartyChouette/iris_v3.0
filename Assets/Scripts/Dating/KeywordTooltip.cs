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

        // Position tooltip near mouse, clamped to screen
        Vector2 mousePos = Input.mousePosition;
        var parentCanvas = _tooltipPanel.GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            // World-space canvas — convert screen pos to local canvas pos
            RectTransform canvasRT = parentCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, mousePos, _mainCamera, out Vector2 localPoint);
            rt.localPosition = new Vector3(localPoint.x + 10f, localPoint.y - 20f, 0f);
        }
        else
        {
            // Screen-space overlay
            rt.position = new Vector3(mousePos.x + 15f, mousePos.y - 25f, 0f);
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
