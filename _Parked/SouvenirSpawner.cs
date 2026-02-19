using UnityEngine;

/// <summary>
/// Spawns date souvenirs the morning after a date.
/// Reads last RichDateHistoryEntry → selects souvenir based on affection outcome.
/// Player discovers it at predefined apartment locations.
/// </summary>
public class SouvenirSpawner : MonoBehaviour
{
    [Header("Spawn Locations")]
    [Tooltip("Possible positions where souvenirs can appear.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Settings")]
    [Tooltip("Affection threshold for 'good date' souvenirs.")]
    [SerializeField] private float _goodDateThreshold = 60f;

    [Tooltip("Affection threshold below which 'bad date' souvenirs appear.")]
    [SerializeField] private float _badDateThreshold = 40f;

    private GameObject _currentSouvenir;

    /// <summary>
    /// Called during morning transition. Spawns a souvenir based on last date outcome.
    /// </summary>
    public void SpawnSouvenirFromLastDate()
    {
        // Clean up previous souvenir
        if (_currentSouvenir != null)
        {
            Destroy(_currentSouvenir);
            _currentSouvenir = null;
        }

        var lastEntry = DateHistory.GetLastEntry();
        if (lastEntry == null) return;

        // Find the DatePersonalDefinition for this character
        var allPersonals = Resources.FindObjectsOfTypeAll<DatePersonalDefinition>();
        DatePersonalDefinition dateDef = null;
        foreach (var def in allPersonals)
        {
            if (def.name == lastEntry.characterId)
            {
                dateDef = def;
                break;
            }
        }

        if (dateDef == null) return;

        // Select souvenir based on affection outcome
        SouvenirDefinition souvenir = SelectSouvenir(dateDef, lastEntry.finalAffection);
        if (souvenir == null) return;

        // Pick a spawn point
        if (_spawnPoints == null || _spawnPoints.Length == 0) return;
        Transform spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        if (spawnPoint == null) return;

        // Create souvenir object (simple colored cube for prototype)
        _currentSouvenir = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _currentSouvenir.name = $"Souvenir_{souvenir.displayName}";
        _currentSouvenir.transform.position = spawnPoint.position;
        _currentSouvenir.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

        var rend = _currentSouvenir.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = souvenir.souvenirColor;

        // Make it a reactable tag so the next date can notice it
        var tag = _currentSouvenir.AddComponent<ReactableTag>();
        // Tags would be set via SerializedObject in a real build

        Debug.Log($"[SouvenirSpawner] Spawned '{souvenir.displayName}' at {spawnPoint.name} (affection was {lastEntry.finalAffection:F0}%).");
    }

    private SouvenirDefinition SelectSouvenir(DatePersonalDefinition dateDef, float affection)
    {
        SouvenirDefinition[] pool;

        if (affection >= _goodDateThreshold)
            pool = dateDef.goodDateSouvenirs;
        else if (affection < _badDateThreshold)
            pool = dateDef.badDateSouvenirs;
        else
            return null; // Average date = no souvenir

        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }
}
