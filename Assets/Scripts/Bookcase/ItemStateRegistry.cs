using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility tracking display state of inspectable items (perfumes, coffee books).
/// Future date-likability system queries GetAllStates() to evaluate apartment presentation.
/// </summary>
public static class ItemStateRegistry
{
    public enum ItemDisplayState { PutAway, OnDisplay }

    private static readonly Dictionary<string, ItemDisplayState> s_states =
        new Dictionary<string, ItemDisplayState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_states.Clear();
    }

    public static void SetState(string itemID, ItemDisplayState state)
    {
        s_states[itemID] = state;
    }

    public static ItemDisplayState GetState(string itemID)
    {
        return s_states.TryGetValue(itemID, out var state) ? state : ItemDisplayState.PutAway;
    }

    public static Dictionary<string, ItemDisplayState> GetAllStates()
    {
        return new Dictionary<string, ItemDisplayState>(s_states);
    }

    public static void Clear()
    {
        s_states.Clear();
    }

    /// <summary>Return all states as serializable records for save.</summary>
    public static List<ItemDisplayRecord> GetAllForSave()
    {
        var list = new List<ItemDisplayRecord>();
        foreach (var kvp in s_states)
        {
            list.Add(new ItemDisplayRecord
            {
                itemId = kvp.Key,
                displayState = (int)kvp.Value
            });
        }
        return list;
    }

    /// <summary>Restore all states from loaded save data.</summary>
    public static void LoadFrom(List<ItemDisplayRecord> records)
    {
        s_states.Clear();
        if (records == null) return;
        foreach (var r in records)
            s_states[r.itemId] = (ItemDisplayState)r.displayState;
    }
}
