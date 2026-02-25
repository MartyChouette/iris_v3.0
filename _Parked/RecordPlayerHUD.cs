using UnityEngine;
using TMPro;

public class RecordPlayerHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text showing the record title.")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("Text showing the artist name.")]
    [SerializeField] private TMP_Text artistText;

    [Tooltip("Text showing play state (Playing / Stopped).")]
    [SerializeField] private TMP_Text stateText;

    [Tooltip("Hint text for controls.")]
    [SerializeField] private TMP_Text hintsText;

    /// <summary>Legacy display method (kept for backward compatibility).</summary>
    public void UpdateDisplay(string title, string artist, bool isPlaying)
    {
        if (titleText != null)
            titleText.text = title;
        if (artistText != null)
            artistText.text = artist;
        if (stateText != null)
            stateText.text = isPlaying ? "Playing" : "";
        if (hintsText != null)
            hintsText.text = "";
    }

    /// <summary>Browse mode: show hovered record title near the stack.</summary>
    public void ShowBrowseMode(string hoveredTitle)
    {
        if (titleText != null)
            titleText.text = hoveredTitle;
        if (artistText != null)
            artistText.text = "";
        if (stateText != null)
            stateText.text = "";
        if (hintsText != null)
            hintsText.text = !string.IsNullOrEmpty(hoveredTitle)
                ? "Click to select  |  Scroll to browse  |  RMB Cancel"
                : "Hover over a sleeve to browse";
    }

    /// <summary>Selected mode: show full info + controls.</summary>
    public void ShowSelectedMode(string title, string artist, string moodDesc)
    {
        if (titleText != null)
            titleText.text = title;
        if (artistText != null)
            artistText.text = $"{artist}  ({moodDesc})";
        if (stateText != null)
            stateText.text = "Selected";
        if (hintsText != null)
            hintsText.text = "Click turntable to play  |  RMB Cancel";
    }

    /// <summary>Playing mode: show title + artist + playing state.</summary>
    public void ShowPlayingMode(string title, string artist)
    {
        if (titleText != null)
            titleText.text = title;
        if (artistText != null)
            artistText.text = artist;
        if (stateText != null)
            stateText.text = "Playing";
        if (hintsText != null)
            hintsText.text = "Click turntable to stop  |  Click stack to browse";
    }
}
