using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-scoped singleton tracking all living plants in the apartment.
/// Spawns plants from flower trimming results, advances their health each day,
/// and feeds MoodMachine with average plant health.
/// </summary>
public class LivingFlowerPlantManager : MonoBehaviour
{
    public static LivingFlowerPlantManager Instance { get; private set; }

    [Header("Plant Slots")]
    [Tooltip("Pre-placed transforms where living plants can be spawned.")]
    [SerializeField] private Transform[] _plantSlots;

    private readonly List<LivingFlowerPlant> _activePlants = new();
    private int _nextSlotIndex;

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LivingFlowerPlantManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ───────────────────────────────────────────────

    /// <summary>
    /// Spawn a new living plant at the next available slot.
    /// Called after flower trimming completes successfully.
    /// </summary>
    public void SpawnPlant(string characterName, int daysAlive)
    {
        if (_plantSlots == null || _plantSlots.Length == 0)
        {
            Debug.LogWarning("[LivingFlowerPlantManager] No plant slots configured.");
            return;
        }

        if (daysAlive <= 0)
        {
            Debug.Log("[LivingFlowerPlantManager] Plant has 0 days alive — not spawning.");
            return;
        }

        // Find next available slot (cycle through slots)
        Transform slot = null;
        for (int i = 0; i < _plantSlots.Length; i++)
        {
            int idx = (_nextSlotIndex + i) % _plantSlots.Length;
            if (_plantSlots[idx] != null)
            {
                slot = _plantSlots[idx];
                _nextSlotIndex = (idx + 1) % _plantSlots.Length;
                break;
            }
        }

        if (slot == null)
        {
            Debug.LogWarning("[LivingFlowerPlantManager] All plant slots are null.");
            return;
        }

        // Create procedural plant GO
        var plantGO = CreateProceduralPlant(characterName);
        plantGO.transform.position = slot.position;
        plantGO.transform.rotation = slot.rotation;

        var plant = plantGO.AddComponent<LivingFlowerPlant>();
        int currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        plant.Initialize(characterName, currentDay, daysAlive);

        // Add ReactableTag for date NPC reactions
        var reactable = plantGO.AddComponent<ReactableTag>();
        var tagsField = typeof(ReactableTag).GetField("tags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (tagsField != null)
            tagsField.SetValue(reactable, new[] { "plant", "flower", "gift" });

        _activePlants.Add(plant);

        // Feed MoodMachine
        UpdateMoodSource();

        Debug.Log($"[LivingFlowerPlantManager] Spawned plant from {characterName} " +
                  $"({daysAlive} days) at {slot.position}");
    }

    /// <summary>Called by GameClock.OnDayStarted event each morning.</summary>
    public void AdvanceAllPlants()
    {
        for (int i = _activePlants.Count - 1; i >= 0; i--)
        {
            if (_activePlants[i] == null || _activePlants[i].IsDead)
            {
                _activePlants.RemoveAt(i);
                continue;
            }

            _activePlants[i].AdvanceDay();

            if (_activePlants[i].IsDead)
                _activePlants.RemoveAt(i);
        }

        UpdateMoodSource();

        Debug.Log($"[LivingFlowerPlantManager] Advanced plants. {_activePlants.Count} alive.");
    }

    /// <summary>Get all living plant records for save data.</summary>
    public List<LivingPlantRecord> GetRecordsForSave()
    {
        var records = new List<LivingPlantRecord>();
        foreach (var plant in _activePlants)
        {
            if (plant != null && !plant.IsDead)
                records.Add(plant.ToRecord());
        }
        return records;
    }

    /// <summary>Restore plants from save data.</summary>
    public void LoadFromRecords(List<LivingPlantRecord> records)
    {
        if (records == null) return;

        foreach (var record in records)
        {
            var plantGO = CreateProceduralPlant(record.characterName);
            plantGO.transform.position = new Vector3(record.px, record.py, record.pz);

            var plant = plantGO.AddComponent<LivingFlowerPlant>();
            plant.Initialize(record.characterName, record.spawnDay, record.totalDaysAlive);
            plant.SetHealth(record.currentHealth);

            var reactable = plantGO.AddComponent<ReactableTag>();
            var tagsField = typeof(ReactableTag).GetField("tags",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tagsField != null)
                tagsField.SetValue(reactable, new[] { "plant", "flower", "gift" });

            _activePlants.Add(plant);
        }

        UpdateMoodSource();
    }

    // ─── Internal ─────────────────────────────────────────────────

    private void UpdateMoodSource()
    {
        if (MoodMachine.Instance == null) return;

        if (_activePlants.Count == 0)
        {
            MoodMachine.Instance.RemoveSource("LivingPlants");
            return;
        }

        float totalHealth = 0f;
        foreach (var plant in _activePlants)
        {
            if (plant != null && !plant.IsDead)
                totalHealth += plant.Health;
        }

        float avgHealth = totalHealth / _activePlants.Count;
        MoodMachine.Instance.SetSource("LivingPlants", avgHealth);
    }

    private static GameObject CreateProceduralPlant(string characterName)
    {
        var plantGO = new GameObject($"LivingPlant_{characterName}");

        // Pot (brown cube)
        var pot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pot.name = "Pot";
        pot.transform.SetParent(plantGO.transform);
        pot.transform.localPosition = Vector3.zero;
        pot.transform.localScale = new Vector3(0.12f, 0.10f, 0.12f);
        var potRend = pot.GetComponent<Renderer>();
        if (potRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.55f, 0.35f, 0.20f);
            potRend.sharedMaterial = mat;
        }

        // Stem (green cylinder)
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(plantGO.transform);
        stem.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        stem.transform.localScale = new Vector3(0.02f, 0.10f, 0.02f);
        var stemRend = stem.GetComponent<Renderer>();
        if (stemRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.3f, 0.7f, 0.2f);
            stemRend.sharedMaterial = mat;
        }

        // Flower head (colored sphere)
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "FlowerHead";
        head.transform.SetParent(plantGO.transform);
        head.transform.localPosition = new Vector3(0f, 0.24f, 0f);
        head.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
        var headRend = head.GetComponent<Renderer>();
        if (headRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            // Vary color by character name hash for visual variety
            int hash = characterName != null ? characterName.GetHashCode() : 0;
            float hue = Mathf.Abs(hash % 360) / 360f;
            mat.color = Color.HSVToRGB(hue, 0.6f, 0.9f);
            headRend.sharedMaterial = mat;
        }

        return plantGO;
    }
}
