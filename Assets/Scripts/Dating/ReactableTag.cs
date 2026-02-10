using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight marker component that identifies an object as something a date
/// character can notice and react to. Tags describe what the object is
/// (e.g. "vinyl", "plant", "perfume_floral").
/// </summary>
public class ReactableTag : MonoBehaviour
{
    [Tooltip("Tags identifying what this is. E.g. 'vinyl', 'plant', 'perfume_floral'.")]
    [SerializeField] private string[] tags = { };

    [Tooltip("Is this currently active? (e.g. record only active when playing)")]
    [SerializeField] private bool isActive = true;

    public string[] Tags => tags;
    public bool IsActive
    {
        get => isActive;
        set => isActive = value;
    }

    // ──────────────────────────────────────────────────────────────
    // Static registry
    // ──────────────────────────────────────────────────────────────
    private static readonly List<ReactableTag> s_allActive = new List<ReactableTag>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => s_allActive.Clear();

    public static IReadOnlyList<ReactableTag> All => s_allActive;

    private void OnEnable()
    {
        s_allActive.Add(this);
    }

    private void OnDisable()
    {
        s_allActive.Remove(this);
    }
}
