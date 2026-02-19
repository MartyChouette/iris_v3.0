using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Coffee Table Book Definition")]
public class CoffeeTableBookDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Title of the coffee table book.")]
    public string title = "Untitled";

    [TextArea(2, 4)]
    [Tooltip("Short description shown during inspection.")]
    public string description = "";

    [Tooltip("Unique ID for ItemStateRegistry tracking.")]
    public string itemID = "";

    [Header("Visuals")]
    [Tooltip("Cover color of the book.")]
    public Color coverColor = new Color(0.3f, 0.4f, 0.5f);

    [Tooltip("Width and height of the book (flat, landscape orientation).")]
    public Vector2 size = new Vector2(0.25f, 0.20f);

    [Tooltip("Thickness of the book (spine width when upright).")]
    [Range(0.02f, 0.08f)]
    public float thickness = 0.04f;
}
