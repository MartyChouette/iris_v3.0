using UnityEngine;

/// <summary>
/// Nema's emotional wellbeing — the flower chart. Five petals representing
/// life satisfaction categories. Each petal blooms (0-1) based on how
/// the player is doing in that area.
///
/// Read-only to the player — this is the "output" showing how Nema feels.
/// Updated automatically from game state each frame/day.
/// </summary>
public class NemaWellbeing : MonoBehaviour
{
    public static NemaWellbeing Instance { get; private set; }

    public const int PetalCount = 5;

    public enum Petal { Comfort, Romance, Expression, Nature, Social }

    public static readonly string[] PetalNames = { "Comfort", "Romance", "Expression", "Nature", "Social" };
    public static readonly string[] PetalDescriptions =
    {
        "Clean, decorated, smells good",
        "Dates going well, affection",
        "Music, art, books she likes",
        "Living plants, flowers, window",
        "Date frequency, connections"
    };

    // Current petal values (0 = wilted, 1 = blooming)
    [Header("Petal Values (read-only at runtime)")]
    [Range(0, 1)] public float comfort;
    [Range(0, 1)] public float romance;
    [Range(0, 1)] public float expression;
    [Range(0, 1)] public float nature;
    [Range(0, 1)] public float social;

    // Smoothed display values (lerp toward actual for nice animation)
    private float[] _displayValues = new float[PetalCount];
    private const float LerpSpeed = 2f;

    public float GetPetal(int index) => index switch
    {
        0 => comfort, 1 => romance, 2 => expression,
        3 => nature, 4 => social, _ => 0f
    };

    public float GetDisplayPetal(int index) =>
        index >= 0 && index < PetalCount ? _displayValues[index] : 0f;

    /// <summary>Overall wellbeing (average of all petals).</summary>
    public float Overall => (comfort + romance + expression + nature + social) / PetalCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[NemaWellbeing]");
        go.AddComponent<NemaWellbeing>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        for (int i = 0; i < PetalCount; i++)
            _displayValues[i] = GetPetal(i);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Smooth display values toward actual
        for (int i = 0; i < PetalCount; i++)
            _displayValues[i] = Mathf.MoveTowards(_displayValues[i], GetPetal(i), LerpSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Recalculate all petal values from current game state.
    /// Call once per day or after significant events.
    /// </summary>
    public void Recalculate()
    {
        // ── Comfort: apartment cleanliness + smell ──
        if (TidyScorer.Instance != null)
            comfort = TidyScorer.Instance.OverallTidiness;
        else
            comfort = 0.5f;

        // ── Romance: recent date affection ──
        float dateAffection = 0.5f;
        var entries = DateHistory.Entries;
        if (entries != null && entries.Count > 0)
        {
            float total = 0f;
            int count = 0;
            for (int i = entries.Count - 1; i >= 0 && count < 3; i--)
            {
                total += entries[i].affection;
                count++;
            }
            dateAffection = count > 0 ? Mathf.Clamp01(total / count) : 0.5f;
        }
        romance = dateAffection;

        // ── Expression: music playing + items with art/music tags ──
        float musicScore = 0f;
        if (AudioManager.Instance != null && AudioManager.Instance.musicSource != null
            && AudioManager.Instance.musicSource.isPlaying)
            musicScore = 0.5f;

        int artItems = 0;
        var allTags = ReactableTag.All;
        for (int i = 0; i < allTags.Count; i++)
        {
            if (allTags[i] == null || !allTags[i].IsActive) continue;
            foreach (string tag in allTags[i].Tags)
            {
                if (tag == "vinyl" || tag == "art" || tag == "book" || tag == "music")
                { artItems++; break; }
            }
        }
        float artScore = Mathf.Clamp01(artItems / 5f); // 5 art items = full
        expression = Mathf.Clamp01(musicScore + artScore * 0.5f);

        // ── Nature: living plants health ──
        if (LivingFlowerPlantManager.Instance != null
            && LivingFlowerPlantManager.Instance.ActivePlants.Count > 0)
        {
            float totalHealth = 0f;
            int count = 0;
            foreach (var plant in LivingFlowerPlantManager.Instance.ActivePlants)
            {
                if (plant != null && !plant.IsDead)
                {
                    totalHealth += plant.Health;
                    count++;
                }
            }
            nature = count > 0 ? totalHealth / count : 0f;
        }
        else
        {
            nature = 0.2f; // no plants = low but not zero
        }

        // ── Social: date count + variety ──
        int totalDates = DateHistory.Entries != null ? DateHistory.Entries.Count : 0;
        social = Mathf.Clamp01(totalDates / 5f); // 5 dates = full

        Debug.Log($"[NemaWellbeing] Recalculated: C={comfort:F2} R={romance:F2} E={expression:F2} N={nature:F2} S={social:F2} Overall={Overall:F2}");
    }
}
