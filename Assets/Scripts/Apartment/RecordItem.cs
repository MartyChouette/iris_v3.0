using UnityEngine;

/// <summary>
/// Lightweight companion to PlaceableObject for vinyl records.
/// References a RecordDefinition (audio clip, mood value, colors).
/// </summary>
public class RecordItem : MonoBehaviour
{
    [Header("Record Content")]
    [Tooltip("The record definition (title, artist, audio, mood).")]
    [SerializeField] private RecordDefinition _definition;

    public RecordDefinition Definition => _definition;
}
