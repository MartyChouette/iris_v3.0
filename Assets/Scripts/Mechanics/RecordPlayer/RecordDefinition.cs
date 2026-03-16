using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Record Definition")]
public class RecordDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Title of the record/album.")]
    public string title = "Untitled";

    [Tooltip("Artist or band name.")]
    public string artist = "Unknown";

    [Tooltip("Genre (e.g. DnB, K-pop, Lo-fi, Sexy, Wedding).")]
    public string genre = "";

    [Tooltip("Flavor text — Nema's thoughts on this record.")]
    [TextArea(2, 4)]
    public string description = "";

    [Header("Visuals")]
    [Tooltip("Color of the record label (center circle).")]
    public Color labelColor = new Color(0.8f, 0.2f, 0.2f);

    [Tooltip("Color of the record sleeve/cover.")]
    public Color coverColor = Color.white;

    [Header("Mood Machine")]
    [Tooltip("Mood value this record pushes toward (0 = sunny, 1 = stormy).")]
    [Range(0f, 1f)]
    public float moodValue = 0.3f;

    [Header("Audio")]
    [Tooltip("Path to music clip in Resources folder (e.g. 'Music/MyTrack'). Loaded on demand to avoid bloating scene load.")]
    public string musicClipPath;

    [Tooltip("Playback volume (0-1).")]
    [Range(0f, 1f)]
    public float volume = 0.7f;

    private AudioClip _cachedClip;

    /// <summary>
    /// Lazy-loads the music clip from Resources on first access.
    /// Avoids loading all record audio during scene deserialization.
    /// </summary>
    public AudioClip MusicClip
    {
        get
        {
            if (_cachedClip == null && !string.IsNullOrEmpty(musicClipPath))
                _cachedClip = Resources.Load<AudioClip>(musicClipPath);
            return _cachedClip;
        }
    }
}
