using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: Window > Iris > Auto-Wire Sounds.
/// Scans scene for empty AudioClip fields and helps wire them.
/// Dry Run shows what's empty. Wire attempts smart matching from Assets/Audio/.
/// </summary>
public class SoundAutoWirer : EditorWindow
{
    [MenuItem("Window/Iris/Auto-Wire Sounds")]
    public static void ShowWindow()
    {
        GetWindow<SoundAutoWirer>("Auto-Wire Sounds");
    }

    // Keyword-based matching: if field name contains key → try to find file containing value
    private static readonly (string fieldKey, string[] fileKeys)[] FuzzyMap =
    {
        ("pickup",       new[] { "Pick Up", "Button Press", "Click, Tap" }),
        ("place",        new[] { "Put Down", "Flower Vase" }),
        ("transition",   new[] { "Pops, Cute", "Tap Reverse" }),
        ("stainComplete",new[] { "Pops", "Positive 03" }),
        ("knock",        new[] { "Metal Click", "Rattle" }),
        ("doorOpen",     new[] { "Lock, Latch", "Latch, Click" }),
        ("open",         new[] { "Lock, Latch", "Click" }),
        ("close",        new[] { "Metal Click", "Close" }),
        ("toggle",       new[] { "Clothes Peg", "Click" }),
        ("play",         new[] { "Fan, Pedestal", "Click 01" }),
        ("stop",         new[] { "Heater Fan", "Thermostat" }),
        ("insert",       new[] { "Bobcat", "Fasten, Click" }),
        ("deposit",      new[] { "Flower Vase", "Put Down" }),
        ("trash",        new[] { "Negative 03", "Return" }),
        ("nav",          new[] { "Button Click, Input", "Tap, Short" }),
        ("dismiss",      new[] { "Negative 04", "Return" }),
        ("select",       new[] { "Positive 04", "Select" }),
        ("present",      new[] { "Pops, Cute" }),
        ("caught",       new[] { "Double Click", "Phone Tap" }),
        ("snap",         new[] { "Click", "Tap" }),
        ("morning",      new[] { "Ocean", "Waves", "Birds" }),
        ("exploration",  new[] { "Waves", "Wind", "Beach" }),
        ("rain",         new[] { "Ocean", "Waves" }),
        ("storm",        new[] { "Ocean", "Waves" }),
        ("ambience",     new[] { "Waves", "Ocean", "Wind" }),
        ("weather",      new[] { "Ocean", "Waves" }),
        ("menu",         new[] { "hourglass", "baegel" }),
    };

    private List<AudioClip> _allClips;
    private List<FieldEntry> _entries = new();
    private Vector2 _scrollPos;

    private struct FieldEntry
    {
        public MonoBehaviour component;
        public string fieldName;
        public string fieldPath;
        public AudioClip suggestion;
        public bool alreadySet;
    }

    private void OnGUI()
    {
        GUILayout.Label("Auto-Wire Sounds", EditorStyles.boldLabel);
        GUILayout.Space(4);

        if (GUILayout.Button("Scan Scene", GUILayout.Height(28)))
            ScanScene();

        if (_entries.Count == 0)
        {
            GUILayout.Label("Click Scan to find empty AudioClip fields.");
            return;
        }

        // Summary
        int empty = _entries.Count(e => !e.alreadySet);
        int matched = _entries.Count(e => !e.alreadySet && e.suggestion != null);
        int set = _entries.Count(e => e.alreadySet);
        GUILayout.Label($"{empty} empty ({matched} auto-matched), {set} already wired");

        if (matched > 0 && GUILayout.Button($"Wire {matched} matched clips", GUILayout.Height(24)))
            WireMatched();

        GUILayout.Space(8);
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        // Show empty fields grouped by component
        string lastComponent = "";
        foreach (var e in _entries)
        {
            if (e.alreadySet) continue;

            string compName = $"{e.component.gameObject.name} ({e.component.GetType().Name})";
            if (compName != lastComponent)
            {
                GUILayout.Space(6);
                GUILayout.Label(compName, EditorStyles.boldLabel);
                lastComponent = compName;
            }

            GUILayout.BeginHorizontal();

            // Field name
            GUILayout.Label(e.fieldName, GUILayout.Width(200));

            // Suggestion or manual picker
            if (e.suggestion != null)
            {
                GUI.color = new Color(0.7f, 1f, 0.7f);
                GUILayout.Label($"→ {e.suggestion.name}", GUILayout.Width(350));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 0.8f, 0.6f);
                GUILayout.Label("(drag clip here →)", GUILayout.Width(200));
                GUI.color = Color.white;

                var newClip = (AudioClip)EditorGUILayout.ObjectField(null, typeof(AudioClip), false, GUILayout.Width(150));
                if (newClip != null)
                {
                    Undo.RecordObject(e.component, "Wire Sound");
                    var so = new SerializedObject(e.component);
                    var prop = so.FindProperty(e.fieldPath);
                    if (prop != null)
                    {
                        prop.objectReferenceValue = newClip;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    private void ScanScene()
    {
        BuildClipList();
        _entries.Clear();

        var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var prop = so.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                // Check if this is an AudioClip field by checking the type string
                if (prop.type != "PPtr<$AudioClip>") continue;

                bool hasValue = prop.objectReferenceValue != null;

                AudioClip suggestion = null;
                if (!hasValue)
                    suggestion = FindFuzzyMatch(prop.name);

                _entries.Add(new FieldEntry
                {
                    component = mb,
                    fieldName = prop.name,
                    fieldPath = prop.propertyPath,
                    suggestion = suggestion,
                    alreadySet = hasValue
                });
            }
        }

        // Sort: empty with suggestions first, then empty without, then already set
        _entries.Sort((a, b) =>
        {
            if (a.alreadySet != b.alreadySet) return a.alreadySet ? 1 : -1;
            if ((a.suggestion != null) != (b.suggestion != null)) return a.suggestion != null ? -1 : 1;
            return string.Compare(a.fieldName, b.fieldName);
        });
    }

    private void WireMatched()
    {
        int count = 0;
        foreach (var e in _entries)
        {
            if (e.alreadySet || e.suggestion == null) continue;

            Undo.RecordObject(e.component, "Wire Sound");
            var so = new SerializedObject(e.component);
            var prop = so.FindProperty(e.fieldPath);
            if (prop != null)
            {
                prop.objectReferenceValue = e.suggestion;
                so.ApplyModifiedProperties();
                count++;
            }
        }

        Debug.Log($"[SoundAutoWirer] Wired {count} clips. Ctrl+Z to undo.");
        ScanScene(); // refresh
    }

    private AudioClip FindFuzzyMatch(string fieldName)
    {
        string lower = fieldName.ToLowerInvariant();

        foreach (var (fieldKey, fileKeys) in FuzzyMap)
        {
            if (!lower.Contains(fieldKey.ToLowerInvariant())) continue;

            // Try each file keyword — return first match
            foreach (var fk in fileKeys)
            {
                var clip = _allClips.FirstOrDefault(c => c.name.Contains(fk));
                if (clip != null) return clip;
            }
        }

        return null;
    }

    private void BuildClipList()
    {
        _allClips = new List<AudioClip>();
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                _allClips.Add(clip);
        }
    }
}
