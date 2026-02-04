/**
 * @file LocalizationManager.cs
 * @brief Static utility for multi-language text lookups.
 *
 * @details
 * Loads string tables from LocalizationTable ScriptableObjects and provides
 * key-based text retrieval with language switching.
 *
 * Pattern:
 * - Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 * - Persistence: saves language to PlayerPrefs key "Iris_Language".
 * - Fallback: returns the key itself if no translation is found.
 *
 * Usage:
 *   string text = LocalizationManager.Get("ui.pause.title");
 *
 * @ingroup framework
 */

using System.Collections.Generic;
using UnityEngine;

public static class LocalizationManager
{
    private const string PREFS_KEY = "Iris_Language";

    private static string s_currentLanguage = "en";
    private static readonly Dictionary<string, Dictionary<string, string>> s_tables =
        new Dictionary<string, Dictionary<string, string>>();

    public static string CurrentLanguage => s_currentLanguage;

    public static event System.Action OnLanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_currentLanguage = "en";
        s_tables.Clear();
        OnLanguageChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        s_currentLanguage = PlayerPrefs.GetString(PREFS_KEY, "en");
        LoadAllTables();
    }

    public static void SetLanguage(string languageCode)
    {
        if (s_currentLanguage == languageCode) return;

        s_currentLanguage = languageCode;
        PlayerPrefs.SetString(PREFS_KEY, languageCode);
        PlayerPrefs.Save();

        Debug.Log($"[LocalizationManager] Language set to '{languageCode}'.");
        OnLanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";

        if (s_tables.TryGetValue(s_currentLanguage, out var table))
        {
            if (table.TryGetValue(key, out string value))
                return value;
        }

        // Fallback to English
        if (s_currentLanguage != "en" && s_tables.TryGetValue("en", out var fallback))
        {
            if (fallback.TryGetValue(key, out string value))
                return value;
        }

        // Last resort: return the key
        return key;
    }

    public static string[] GetAvailableLanguages()
    {
        var languages = new string[s_tables.Count];
        int i = 0;
        foreach (var kvp in s_tables)
            languages[i++] = kvp.Key;
        return languages;
    }

    public static void RegisterTable(LocalizationTable table)
    {
        if (table == null) return;

        if (!s_tables.ContainsKey(table.languageCode))
            s_tables[table.languageCode] = new Dictionary<string, string>();

        var dict = s_tables[table.languageCode];
        foreach (var entry in table.entries)
        {
            if (!string.IsNullOrEmpty(entry.key))
                dict[entry.key] = entry.value;
        }

        Debug.Log($"[LocalizationManager] Registered table '{table.name}' ({table.languageCode}, {table.entries.Length} entries).");
    }

    private static void LoadAllTables()
    {
        var tables = Resources.LoadAll<LocalizationTable>("");
        foreach (var table in tables)
            RegisterTable(table);

        if (tables.Length == 0)
            Debug.Log("[LocalizationManager] No LocalizationTable assets found in Resources.");
    }
}
