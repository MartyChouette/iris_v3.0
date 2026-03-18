using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: Window > Iris > Auto-Wire Sounds.
/// Scans all MonoBehaviours in the open scene, finds empty AudioClip fields,
/// and wires them to the best matching clip from Assets/Audio/.
/// Review the log and Ctrl+Z to undo if needed.
/// </summary>
public class SoundAutoWirer : EditorWindow
{
    [MenuItem("Window/Iris/Auto-Wire Sounds")]
    public static void ShowWindow()
    {
        GetWindow<SoundAutoWirer>("Auto-Wire Sounds");
    }

    // Field name keyword → audio file keyword mapping
    // More specific matches first — first match wins
    private static readonly (string fieldKeyword, string fileKeyword, float volume)[] Mappings =
    {
        // Object grab
        ("_pickupSFX",         "Button Press Click, Tap, Video Game, Main Menu, Select, Positive 01", 0.5f),
        ("_placeSFX",          "Flower Vase, Big, Pick Up, Put Down",                                0.6f),
        ("_pickupSFXOverride", "",                                                                    0f), // skip — per-object, user sets manually
        ("_placeSFXOverride",  "",                                                                    0f), // skip

        // Area transition
        ("_areaTransitionSFX", "Pops, Cute, Tap Reverse",                                            0.35f),

        // Cleaning
        ("_stainCompleteSFX",  "Button Press Click, Tap, Video Game, Main Menu, Select, Positive 03", 0.4f),

        // Door
        ("_knockSFX",          "Metal Click, Rattle, Close Variations",                               0.6f),
        ("_doorOpenSFX",       "Lock, Latch, Click",                                                  0.5f),

        // Fridge
        ("_openSFX",           "Lock, Latch, Click",                                                  0.45f),
        ("_closeSFX",          "Metal Click, Rattle, Close Variations",                               0.5f),

        // Light switch
        ("_toggleSFX",         "Clothes Peg, Click",                                                  0.4f),

        // Record player
        ("_playSFX",           "Fan, Pedestal, Head Adjust Click 01",                                 0.35f),
        ("_stopSFX",           "Heater Fan, Small, Thermostat, Click",                                0.3f),

        // Disco ball
        ("_insertSFX",         "Bobcat, Micro, Digger, Safety, Belt, Fasten, Click",                 0.45f),

        // Drop zones
        ("_depositSFX",        "Flower Vase, Big, Pick Up, Put Down",                                0.4f),
        ("_trashSFX",          "Button Press Click, Tap, Video Game, Main Menu, Select, Return, Negative 03", 0.5f),

        // UI / Nav
        ("_navClickSFX",       "Button Click, Input Response, Tap, Short",                            0.3f),
        ("_dismissSFX",        "Button Press Click, Tap, Video Game, Main Menu, Select, Return, Negative 04", 0.3f),
        ("_selectSFX",         "Button Press Click, Tap, Video Game, Main Menu, Select, Positive 04", 0.4f),

        // Date
        ("_presentSFX",        "Pops, Cute, Tap Reverse",                                            0.5f),
        ("_caughtSFX",         "Button, Double Click, Fast, Phone Tap",                               0.55f),

        // Pairing
        ("_snapSound",         "",                                                                    0f), // skip — category-specific, user sets per item

        // Ambience
        ("_morningAmbienceClip",    "Ocean, Rhythmic Waves, Crashing In On Beach, Distant Birds",     0.3f),
        ("_explorationAmbienceClip","Waves, Ocean, Coming In On Beach, Wind, Birds",                  0.25f),
        ("_rainAmbience",           "Ocean, Rhythmic Waves",                                          0.4f),
        ("_stormAmbience",          "Ocean, Rhythmic Waves",                                          0.55f),
        ("_ambienceClip",           "Waves, Ocean, Coming In On Beach",                               0.3f),
        ("_weatherClip",            "Ocean, Rhythmic Waves",                                          0.3f),

        // Music
        ("_menuSong",               "hourglass - baegel",                                             0.5f),
    };

    private Dictionary<string, AudioClip> _clipCache;
    private List<string> _results = new();

    private Vector2 _scrollPos;

    private void OnGUI()
    {
        GUILayout.Label("Auto-Wire Sounds", EditorStyles.boldLabel);
        GUILayout.Label("Scans scene for empty AudioClip fields and wires matching clips from Assets/Audio/.");
        GUILayout.Space(8);

        if (GUILayout.Button("Scan Scene (Dry Run)", GUILayout.Height(30)))
        {
            _results.Clear();
            RunScan(dryRun: true);
        }

        if (GUILayout.Button("Wire All Empty Slots", GUILayout.Height(30)))
        {
            _results.Clear();
            RunScan(dryRun: false);
        }

        GUILayout.Space(8);
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);
        foreach (var r in _results)
            GUILayout.Label(r, EditorStyles.wordWrappedMiniLabel);
        GUILayout.EndScrollView();
    }

    private void RunScan(bool dryRun)
    {
        BuildClipCache();

        int wired = 0;
        int skipped = 0;
        int alreadySet = 0;

        var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var prop = so.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (!prop.name.Contains("SFX") && !prop.name.Contains("Clip") &&
                    !prop.name.Contains("Sound") && !prop.name.Contains("Song") &&
                    !prop.name.Contains("Ambience") && !prop.name.Contains("ambience"))
                    continue;

                // Check if it's an AudioClip field
                if (prop.objectReferenceValue != null)
                {
                    alreadySet++;
                    continue;
                }

                // Find matching clip
                string fieldName = prop.name;
                var match = FindMatch(fieldName);

                if (match == null)
                {
                    _results.Add($"  ? {mb.GetType().Name}.{fieldName} — no match found");
                    skipped++;
                    continue;
                }

                if (match == "") // explicitly skipped
                {
                    skipped++;
                    continue;
                }

                if (!_clipCache.TryGetValue(match, out var clip))
                {
                    _results.Add($"  ! {mb.GetType().Name}.{fieldName} — matched '{match}' but file not found");
                    skipped++;
                    continue;
                }

                if (dryRun)
                {
                    _results.Add($"  > {mb.GetType().Name}.{fieldName} → {clip.name}");
                }
                else
                {
                    Undo.RecordObject(mb, "Auto-Wire Sound");
                    prop.objectReferenceValue = clip;
                    so.ApplyModifiedProperties();
                    _results.Add($"  + {mb.GetType().Name}.{fieldName} → {clip.name}");
                }
                wired++;
            }
        }

        string mode = dryRun ? "DRY RUN" : "WIRED";
        _results.Insert(0, $"--- {mode}: {wired} to wire, {alreadySet} already set, {skipped} skipped ---");
        Debug.Log($"[SoundAutoWirer] {mode}: {wired} clips, {alreadySet} already set, {skipped} skipped");
    }

    private string FindMatch(string fieldName)
    {
        foreach (var (fieldKeyword, fileKeyword, _) in Mappings)
        {
            if (fieldName == fieldKeyword || fieldName.Contains(fieldKeyword.TrimStart('_')))
            {
                return fileKeyword; // empty string = explicitly skip
            }
        }
        return null; // no match at all
    }

    private void BuildClipCache()
    {
        _clipCache = new Dictionary<string, AudioClip>();

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);
            // Strip "ES_" prefix for matching
            string key = fileName.StartsWith("ES_") ? fileName.Substring(3) : fileName;
            _clipCache[key] = clip;

            // Also store with full filename for exact matches
            if (!_clipCache.ContainsKey(fileName))
                _clipCache[fileName] = clip;
        }
    }
}
