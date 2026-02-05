using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-scoped singleton that manages pest infestation on flower parts.
/// Pests spread to adjacent parts on a timer and the game ends if they reach the crown.
/// </summary>
[DisallowMultipleComponent]
public class PestController : MonoBehaviour
{
    public static PestController Instance { get; private set; }

    [Header("References")]
    [Tooltip("The flower brain whose parts can be infested.")]
    public FlowerGameBrain brain;

    [Header("Timing")]
    [Tooltip("Seconds between spread attempts.")]
    public float spreadInterval = 4f;

    [Tooltip("Chance each infested part spreads per tick (0-1).")]
    [Range(0f, 1f)]
    public float spreadChance = 0.5f;

    [Header("Initial Infection")]
    [Tooltip("How many parts start infested.")]
    public int initialPestCount = 2;

    [Header("Visuals")]
    [Tooltip("Prefab for pest visual. If null, a small dark sphere is created.")]
    public GameObject pestVisualPrefab;

    [Header("Audio")]
    public AudioClip spreadSFX;
    public AudioClip removeSFX;

    // Runtime
    private float _spreadTimer;
    private List<PestInstance> _pests = new List<PestInstance>();

    public int InfestedCount => _pests.Count;
    public int CleanCount
    {
        get
        {
            if (brain == null) return 0;
            int attached = 0;
            for (int i = 0; i < brain.parts.Count; i++)
            {
                if (brain.parts[i] != null && brain.parts[i].isAttached)
                    attached++;
            }
            return attached - InfestedCount;
        }
    }

    public List<FlowerPartRuntime> InfestedParts
    {
        get
        {
            var list = new List<FlowerPartRuntime>();
            for (int i = _pests.Count - 1; i >= 0; i--)
            {
                if (_pests[i] != null && _pests[i].hostPart != null)
                    list.Add(_pests[i].hostPart);
            }
            return list;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (brain == null) return;
        InfestInitial();
    }

    void Update()
    {
        if (brain == null) return;

        // Clean up destroyed pests
        for (int i = _pests.Count - 1; i >= 0; i--)
        {
            if (_pests[i] == null)
                _pests.RemoveAt(i);
        }

        _spreadTimer += Time.deltaTime;
        if (_spreadTimer >= spreadInterval)
        {
            _spreadTimer = 0f;
            DoSpreadTick();
        }
    }

    private void InfestInitial()
    {
        var candidates = new List<FlowerPartRuntime>();
        for (int i = 0; i < brain.parts.Count; i++)
        {
            var part = brain.parts[i];
            if (part == null || !part.isAttached) continue;
            if (part.kind == FlowerPartKind.Crown) continue;
            candidates.Add(part);
        }

        // Shuffle and pick
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int count = Mathf.Min(initialPestCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            InfestPart(candidates[i]);
        }
    }

    private void DoSpreadTick()
    {
        var currentInfested = new List<FlowerPartRuntime>(InfestedParts);

        for (int i = 0; i < currentInfested.Count; i++)
        {
            if (Random.value > spreadChance) continue;

            var neighbor = FindNearestCleanPart(currentInfested[i]);
            if (neighbor == null) continue;

            InfestPart(neighbor);

            if (AudioManager.Instance != null && spreadSFX != null)
                AudioManager.Instance.PlaySFX(spreadSFX);

            // Check crown
            if (neighbor.kind == FlowerPartKind.Crown)
            {
                Debug.Log("[PestController] Pest reached the crown! Game over.");
                var session = brain.GetComponent<FlowerSessionController>();
                if (session != null)
                    session.ForceGameOver("Infestation reached the crown!");
                return;
            }
        }
    }

    private FlowerPartRuntime FindNearestCleanPart(FlowerPartRuntime source)
    {
        float bestDist = float.MaxValue;
        FlowerPartRuntime best = null;

        for (int i = 0; i < brain.parts.Count; i++)
        {
            var part = brain.parts[i];
            if (part == null || !part.isAttached) continue;
            if (IsInfested(part)) continue;

            float dist = Vector3.Distance(source.transform.position, part.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = part;
            }
        }

        return best;
    }

    private bool IsInfested(FlowerPartRuntime part)
    {
        for (int i = 0; i < _pests.Count; i++)
        {
            if (_pests[i] != null && _pests[i].hostPart == part)
                return true;
        }
        return false;
    }

    public void InfestPart(FlowerPartRuntime part)
    {
        if (IsInfested(part)) return;

        // Create visual
        GameObject visual;
        if (pestVisualPrefab != null)
        {
            visual = Instantiate(pestVisualPrefab, part.transform);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "PestVisual";
            visual.transform.SetParent(part.transform, false);
            visual.transform.localScale = Vector3.one * 0.025f;
            visual.transform.localPosition = Vector3.zero;

            // Remove collider so it doesn't interfere
            var col = visual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Dark color
            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.1f, 0.08f);
                rend.material = mat;
            }
        }

        var pest = visual.AddComponent<PestInstance>();
        pest.hostPart = part;
        pest.visual = visual;
        pest.controller = this;
        _pests.Add(pest);

        Debug.Log($"[PestController] Infested {part.name}.");
    }

    public void OnPestRemoved(PestInstance pest)
    {
        _pests.Remove(pest);

        if (AudioManager.Instance != null && removeSFX != null)
            AudioManager.Instance.PlaySFX(removeSFX);
    }
}
