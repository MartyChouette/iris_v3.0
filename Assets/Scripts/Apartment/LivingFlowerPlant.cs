using UnityEngine;

/// <summary>
/// A living plant decoration spawned from a successful flower trimming.
/// Health decreases each day by 1/totalDaysAlive. Visual color and scale
/// degrade as health drops. When health reaches 0, the plant dies.
/// </summary>
public class LivingFlowerPlant : MonoBehaviour
{
    [Header("Lifespan")]
    [Tooltip("Day this plant was spawned.")]
    [SerializeField] private int _spawnDay;

    [Tooltip("Total days this plant will survive (from flower trimming score).")]
    [SerializeField] private int _totalDaysAlive;

    [Tooltip("Current health (1.0 = fresh, 0.0 = dead).")]
    [SerializeField] private float _health = 1f;

    [Header("Character")]
    [Tooltip("Name of the date character who gave this flower.")]
    [SerializeField] private string _characterName;

    // ─── Visual Settings ──────────────────────────────────────────
    private static readonly Color HealthyColor = new Color(0.3f, 0.7f, 0.2f);
    private static readonly Color WiltingColor = new Color(0.8f, 0.7f, 0.2f);
    private static readonly Color DeadColor    = new Color(0.4f, 0.25f, 0.1f);

    private const float MinScale = 0.8f;
    private const float MaxScale = 1.0f;

    private Renderer[] _renderers;
    private Color[] _originalColors;
    private Vector3 _baseScale;
    private bool _isDead;

    // ─── Public API ───────────────────────────────────────────────

    public int SpawnDay => _spawnDay;
    public int TotalDaysAlive => _totalDaysAlive;
    public float Health => _health;
    public string CharacterName => _characterName;
    public bool IsDead => _isDead;

    public void Initialize(string characterName, int spawnDay, int totalDaysAlive)
    {
        _characterName = characterName;
        _spawnDay = spawnDay;
        _totalDaysAlive = Mathf.Max(1, totalDaysAlive);
        _health = 1f;
        _isDead = false;
        UpdateVisuals();
    }

    public void SetHealth(float health)
    {
        _health = Mathf.Clamp01(health);
        if (_health <= 0f) Die();
        else UpdateVisuals();
    }

    /// <summary>Called each morning by LivingFlowerPlantManager.</summary>
    public void AdvanceDay()
    {
        if (_isDead) return;

        _health -= 1f / _totalDaysAlive;
        _health = Mathf.Max(0f, _health);

        if (_health <= 0f)
            Die();
        else
            UpdateVisuals();
    }

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _originalColors[i] = _renderers[i].material.color;
        }
        _baseScale = transform.localScale;
    }

    // ─── Internal ─────────────────────────────────────────────────

    private void UpdateVisuals()
    {
        // Wilt tint: lerps from white (healthy) → yellowish → brown (dead)
        Color wiltTint;
        if (_health > 0.5f)
            wiltTint = Color.Lerp(WiltingColor, HealthyColor, (_health - 0.5f) * 2f);
        else
            wiltTint = Color.Lerp(DeadColor, WiltingColor, _health * 2f);

        // Apply tint to all child renderers, preserving each one's original color
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].material.color = _originalColors[i] * wiltTint;
            }
        }

        // Scale shrinks as health drops
        float scale = Mathf.Lerp(MinScale, MaxScale, _health);
        transform.localScale = _baseScale * scale;
    }

    private void Die()
    {
        _isDead = true;
        _health = 0f;

        Debug.Log($"[LivingFlowerPlant] Plant from {_characterName} has died.");

        // Deactivate ReactableTag so date NPCs don't react to dead plants
        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = false;

        // Remove MoodMachine source
        MoodMachine.Instance?.RemoveSource($"Plant_{_characterName}");

        gameObject.SetActive(false);
    }

    /// <summary>Create a serializable record for save data.</summary>
    public LivingPlantRecord ToRecord()
    {
        return new LivingPlantRecord
        {
            characterName = _characterName,
            spawnDay = _spawnDay,
            totalDaysAlive = _totalDaysAlive,
            currentHealth = _health,
            px = transform.position.x,
            py = transform.position.y,
            pz = transform.position.z
        };
    }
}
