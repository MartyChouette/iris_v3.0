using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Book Definition")]
public class BookDefinition : ScriptableObject
{
    // ──────────────────────────────────────────────────────────────
    // Identity
    // ──────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Book title displayed on hover hint and reading pages.")]
    public string title = "Untitled";

    [Tooltip("Author name displayed on the reading pages.")]
    public string author = "Unknown";

    [TextArea(3, 8)]
    [Tooltip("Text content for pages. Displayed as 2-page spreads (pairs of left+right).")]
    public string[] pageTexts = new string[2];

    // ──────────────────────────────────────────────────────────────
    // Hidden Items
    // ──────────────────────────────────────────────────────────────
    [Header("Hidden Items")]
    [Tooltip("Whether this book contains a hidden item (pressed flower, note, etc.).")]
    public bool hasHiddenItem;

    [Tooltip("Description of the hidden item displayed when found.")]
    public string hiddenItemDescription = "";

    [Tooltip("Which page spread the hidden item appears on (-1 = none, 0+ = spread index).")]
    public int hiddenItemPage = -1;

    // ──────────────────────────────────────────────────────────────
    // Date Reactions
    // ──────────────────────────────────────────────────────────────
    [Header("Date Reactions")]
    [Tooltip("Tags for ReactableTag (e.g. 'poetry', 'manga'). Matched against DatePreferences.")]
    public string[] reactionTags = { "book" };

    // ──────────────────────────────────────────────────────────────
    // Visuals
    // ──────────────────────────────────────────────────────────────
    [Header("Visuals")]
    [Tooltip("Color of the book spine and cover.")]
    public Color spineColor = new Color(0.4f, 0.2f, 0.15f);

    [Tooltip("Height multiplier relative to the shelf row height (0.7–0.95).")]
    [Range(0.5f, 1f)]
    public float heightScale = 0.85f;

    [Tooltip("Thickness in meters (0.02–0.06).")]
    [Range(0.02f, 0.06f)]
    public float thicknessScale = 0.03f;
}
