using UnityEngine;

/// <summary>
/// ScriptableObject defining an outfit the player can wear.
/// Style tags are matched against DatePreferences for entrance judgments.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Outfit Definition")]
public class OutfitDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name of the outfit.")]
    public string outfitName = "Default Outfit";

    [TextArea(2, 4)]
    [Tooltip("Flavor text shown in the selection UI.")]
    public string description;

    [Header("Visuals")]
    [Tooltip("Preview sprite shown in the outfit selection grid.")]
    public Sprite previewSprite;

    [Header("Style Tags")]
    [Tooltip("Style tags for reaction evaluation (e.g. 'casual', 'formal', 'floral', 'edgy').")]
    public string[] styleTags = { };
}
