/**
 * @file LocalizationTable.cs
 * @brief ScriptableObject storing key-value string pairs for one language.
 *
 * @details
 * Each asset represents a single language's translations. Create one per
 * language in a Resources folder so LocalizationManager can discover them
 * at startup via Resources.LoadAll.
 *
 * @ingroup framework
 */

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLocalizationTable", menuName = "Iris/Localization Table")]
public class LocalizationTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("Dot-separated key, e.g. 'ui.pause.title'.")]
        public string key;

        [Tooltip("Translated text for this key.")]
        [TextArea(1, 4)]
        public string value;
    }

    [Header("Language")]
    [Tooltip("ISO 639-1 code, e.g. 'en', 'es', 'ja'.")]
    public string languageCode = "en";

    [Tooltip("Display name shown in language selector, e.g. 'English'.")]
    public string displayName = "English";

    [Header("Entries")]
    [Tooltip("All key-value pairs for this language.")]
    public Entry[] entries = Array.Empty<Entry>();
}
