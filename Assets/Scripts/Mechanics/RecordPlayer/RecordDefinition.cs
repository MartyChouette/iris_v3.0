using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Record Definition")]
public class RecordDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Title of the record/album.")]
    public string title = "Untitled";

    [Tooltip("Artist or band name.")]
    public string artist = "Unknown";

    [Header("Visuals")]
    [Tooltip("Color of the record label (center circle).")]
    public Color labelColor = new Color(0.8f, 0.2f, 0.2f);

    [Header("Mood Machine")]
    [Tooltip("Mood value this record pushes toward (0 = sunny, 1 = stormy).")]
    [Range(0f, 1f)]
    public float moodValue = 0.3f;

    [Header("Audio")]
    [Tooltip("Music clip to play. Can be null for silent placeholder.")]
    public AudioClip musicClip;

    [Tooltip("Playback volume (0-1).")]
    [Range(0f, 1f)]
    public float volume = 0.7f;
}
