using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using System.Collections.Generic;

/// <summary>
/// Editor tool that validates required references and component wiring in the active scene.
/// Access via Window > Iris > Scene Validator.
/// </summary>
public class SceneValidator : EditorWindow
{
    private enum Severity { Critical, Error, Warning }

    private struct Issue
    {
        public Severity severity;
        public string message;
        public Object context;
    }

    private readonly List<Issue> _issues = new List<Issue>();
    private Vector2 _scroll;

    [MenuItem("Window/Iris/Scene Validator")]
    public static void ShowWindow()
    {
        GetWindow<SceneValidator>("Scene Validator");
    }

    private void OnGUI()
    {
        GUILayout.Space(4);
        if (GUILayout.Button("Validate Active Scene", GUILayout.Height(30)))
        {
            RunValidation();
        }

        GUILayout.Space(4);

        if (_issues.Count == 0)
        {
            EditorGUILayout.HelpBox("Click 'Validate Active Scene' to check for issues.", MessageType.Info);
            return;
        }

        int criticals = 0, errors = 0, warnings = 0;
        foreach (var i in _issues)
        {
            switch (i.severity)
            {
                case Severity.Critical: criticals++; break;
                case Severity.Error: errors++; break;
                case Severity.Warning: warnings++; break;
            }
        }

        string summary = $"{criticals} critical, {errors} error(s), {warnings} warning(s)";
        MessageType summaryType = criticals > 0 ? MessageType.Error
            : errors > 0 ? MessageType.Warning
            : MessageType.Info;
        EditorGUILayout.HelpBox(summary, summaryType);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var issue in _issues)
        {
            EditorGUILayout.BeginHorizontal();

            string prefix;
            switch (issue.severity)
            {
                case Severity.Critical: prefix = "[CRITICAL]"; break;
                case Severity.Error:    prefix = "[ERROR]";    break;
                default:                prefix = "[WARNING]";  break;
            }

            GUIStyle style = new GUIStyle(EditorStyles.label) { wordWrap = true };
            EditorGUILayout.LabelField($"{prefix} {issue.message}", style);

            if (issue.context != null)
            {
                if (GUILayout.Button("Select", GUILayout.Width(50)))
                    Selection.activeObject = issue.context;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        _issues.Clear();

        CheckSingletons();
        CheckCoreComponents();
        CheckFlowerHierarchy();
        CheckUIReferences();
        CheckCuttingSystem();
        CheckAudio();
        CheckFluids();

        Repaint();
    }

    // ─────────────────────────────────────────────────────────────
    // Singleton Checks
    // ─────────────────────────────────────────────────────────────

    private void CheckSingletons()
    {
        CheckSingletonCount<FlowerSessionController>("FlowerSessionController");
        CheckSingletonCount<HorrorCameraManager>("HorrorCameraManager");
        CheckSingletonCount<NewspaperManager>("NewspaperManager");
        CheckSingletonCount<BookInteractionManager>("BookInteractionManager");
        CheckSingletonCount<SapParticleController>("SapParticleController");
        CheckSingletonCount<SapDecalPool>("SapDecalPool");
    }

    private void CheckSingletonCount<T>(string label) where T : MonoBehaviour
    {
        var all = FindObjectsByType<T>(FindObjectsSortMode.None);
        if (all.Length > 1)
            Add(Severity.Critical, $"Multiple {label} instances found ({all.Length}). Expected at most 1.", all[0]);
    }

    // ─────────────────────────────────────────────────────────────
    // Core Components
    // ─────────────────────────────────────────────────────────────

    private void CheckCoreComponents()
    {
        // Main Camera
        if (Camera.main == null)
            Add(Severity.Error, "No Camera tagged 'MainCamera' found in scene.");

        // CinemachineBrain (only if HorrorCameraManager exists)
        var hcm = FindAnyObjectByType<HorrorCameraManager>();
        if (hcm != null)
        {
            var brain = FindAnyObjectByType<CinemachineBrain>();
            if (brain == null)
            {
                Add(Severity.Critical, "HorrorCameraManager exists but no CinemachineBrain found.", hcm);
            }
            else
            {
                var so = new SerializedObject(hcm);
                var brainProp = so.FindProperty("brain");
                if (brainProp != null && brainProp.objectReferenceValue == null)
                    Add(Severity.Warning, "HorrorCameraManager.brain is null (will auto-wire, but should be set in Inspector).", hcm);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Flower Hierarchy
    // ─────────────────────────────────────────────────────────────

    private void CheckFlowerHierarchy()
    {
        var sessions = FindObjectsByType<FlowerSessionController>(FindObjectsSortMode.None);
        foreach (var session in sessions)
        {
            var brain = session.GetComponentInChildren<FlowerGameBrain>();
            if (brain == null)
                Add(Severity.Error, $"FlowerSessionController '{session.name}' has no FlowerGameBrain child.", session);
            else
            {
                if (brain.ideal == null)
                    Add(Severity.Error, $"FlowerGameBrain on '{brain.gameObject.name}' has no IdealFlowerDefinition assigned.", brain);
                if (brain.stem == null)
                    Add(Severity.Warning, $"FlowerGameBrain on '{brain.gameObject.name}' has no FlowerStemRuntime assigned.", brain);
            }

            var rebinder = session.GetComponentInChildren<FlowerJointRebinder>();
            if (rebinder == null)
                Add(Severity.Warning, $"FlowerSessionController '{session.name}' has no FlowerJointRebinder child (crown may not fall).", session);
        }

        // Check stem references
        var stems = FindObjectsByType<FlowerStemRuntime>(FindObjectsSortMode.None);
        foreach (var stem in stems)
        {
            if (stem.StemAnchor == null)
                Add(Severity.Error, $"FlowerStemRuntime on '{stem.gameObject.name}' is missing StemAnchor.", stem);
            if (stem.StemTip == null)
                Add(Severity.Error, $"FlowerStemRuntime on '{stem.gameObject.name}' is missing StemTip.", stem);
            if (stem.cutNormalRef == null)
                Add(Severity.Warning, $"FlowerStemRuntime on '{stem.gameObject.name}' is missing cutNormalRef (angle scoring will return 0).", stem);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // UI References
    // ─────────────────────────────────────────────────────────────

    private void CheckUIReferences()
    {
        var gradingUIs = FindObjectsByType<FlowerGradingUI>(FindObjectsSortMode.None);
        foreach (var ui in gradingUIs)
        {
            var so = new SerializedObject(ui);
            var sessionProp = so.FindProperty("session");
            if (sessionProp != null && sessionProp.objectReferenceValue == null)
                Add(Severity.Warning, $"FlowerGradingUI on '{ui.gameObject.name}' has no session assigned (will auto-wire).", ui);
        }

        var feedbackUIs = FindObjectsByType<FlowerHUD_GameplayFeedback>(FindObjectsSortMode.None);
        foreach (var ui in feedbackUIs)
        {
            var so = new SerializedObject(ui);
            var sessionProp = so.FindProperty("session");
            if (sessionProp != null && sessionProp.objectReferenceValue == null)
                Add(Severity.Warning, $"FlowerHUD_GameplayFeedback on '{ui.gameObject.name}' has no session assigned (will auto-wire).", ui);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Cutting System
    // ─────────────────────────────────────────────────────────────

    private void CheckCuttingSystem()
    {
        var controllers = FindObjectsByType<CuttingPlaneController>(FindObjectsSortMode.None);
        foreach (var cpc in controllers)
        {
            if (cpc.plane == null)
                Add(Severity.Error, $"CuttingPlaneController on '{cpc.gameObject.name}' has no PlaneBehaviour assigned.", cpc);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Audio
    // ─────────────────────────────────────────────────────────────

    private void CheckAudio()
    {
        var managers = FindObjectsByType<AudioManager>(FindObjectsSortMode.None);
        if (managers.Length > 1)
            Add(Severity.Critical, $"Multiple AudioManager instances ({managers.Length}). AudioManager uses DontDestroyOnLoad — check for scene duplicates.", managers[0]);
    }

    // ─────────────────────────────────────────────────────────────
    // Fluids
    // ─────────────────────────────────────────────────────────────

    private void CheckFluids()
    {
        var pools = FindObjectsByType<SapDecalPool>(FindObjectsSortMode.None);
        foreach (var pool in pools)
        {
            var so = new SerializedObject(pool);
            var prefabProp = so.FindProperty("decalPrefab");
            if (prefabProp != null && prefabProp.objectReferenceValue == null)
                Add(Severity.Error, $"SapDecalPool on '{pool.gameObject.name}' has no decal prefab assigned.", pool);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private void Add(Severity severity, string message, Object context = null)
    {
        _issues.Add(new Issue { severity = severity, message = message, context = context });
    }
}
