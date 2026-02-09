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

    public void UpdateDisplay(string title, string artist, bool isPlaying)
    {
        if (titleText != null)
            titleText.text = title;

        if (artistText != null)
            artistText.text = artist;

        if (stateText != null)
            stateText.text = isPlaying ? "Playing" : "Stopped";

        if (hintsText != null)
            hintsText.text = isPlaying
                ? "Enter  Stop"
                : "A / D  Browse    |    Enter  Play";
    }
}
