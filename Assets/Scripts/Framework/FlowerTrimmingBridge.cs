using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-scoped singleton that manages the transition into and out of the
/// flower trimming scene. Lives in the apartment scene, loads the flower
/// scene additively, captures results, and unloads it.
///
/// Supports two modes:
/// 1. Self-contained scene (default): loads a scene that already contains a
///    flower with FlowerSessionController. The flowerPrefab parameter is ignored.
/// 2. Spawn-point scene: if the loaded scene has a "FlowerSpawnPoint" and no
///    existing FlowerSessionController, instantiates the flowerPrefab there.
/// </summary>
public class FlowerTrimmingBridge : MonoBehaviour
{
    public static FlowerTrimmingBridge Instance { get; private set; }

    [Header("Scene")]
    [Tooltip("Name of the flower trimming scene to load additively. Must be in Build Settings.")]
    [SerializeField] private string _flowerSceneName = "Daisy_Flower_Scene";

    private Action<int, int, bool> _onComplete;
    private bool _waitingForResult;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[FlowerTrimmingBridge] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Load the flower trimming scene additively, find or instantiate the flower,
    /// wait for the player to trim it, and invoke onComplete with results.
    /// </summary>
    /// <param name="flowerPrefab">Flower prefab to instantiate if the scene has a spawn point
    /// but no existing FlowerSessionController. Can be null for self-contained scenes.</param>
    /// <param name="onComplete">Callback: (score, daysAlive, isGameOver)</param>
    public void BeginTrimming(GameObject flowerPrefab, Action<int, int, bool> onComplete)
    {
        if (_waitingForResult)
        {
            Debug.LogWarning("[FlowerTrimmingBridge] Already waiting for a trimming result.");
            return;
        }

        _onComplete = onComplete;
        _waitingForResult = true;
        StartCoroutine(TrimmingSequence(flowerPrefab));
    }

    /// <summary>
    /// Resolve which flower scene to load: prefer the current date's flowerSceneName,
    /// fall back to the serialized default.
    /// </summary>
    private string ResolveSceneName()
    {
        if (DateSessionManager.Instance != null &&
            DateSessionManager.Instance.CurrentDate != null &&
            !string.IsNullOrEmpty(DateSessionManager.Instance.CurrentDate.flowerSceneName))
        {
            return DateSessionManager.Instance.CurrentDate.flowerSceneName;
        }

        return _flowerSceneName;
    }

    private IEnumerator TrimmingSequence(GameObject flowerPrefab)
    {
        // Resolve per-date scene name, falling back to default
        string sceneName = ResolveSceneName();

        // Load the flower trimming scene additively
        var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"[FlowerTrimmingBridge] Failed to load scene '{sceneName}'. " +
                           "Is it added to Build Settings?");
            _waitingForResult = false;
            _onComplete?.Invoke(0, 0, true);
            yield break;
        }

        yield return loadOp;

        var flowerScene = SceneManager.GetSceneByName(sceneName);
        if (!flowerScene.IsValid())
        {
            Debug.LogError("[FlowerTrimmingBridge] Flower scene not valid after load.");
            _waitingForResult = false;
            _onComplete?.Invoke(0, 0, true);
            yield break;
        }

        // Look for an existing FlowerSessionController already in the scene
        FlowerSessionController session = null;
        foreach (var root in flowerScene.GetRootGameObjects())
        {
            session = root.GetComponentInChildren<FlowerSessionController>();
            if (session != null) break;
        }

        // If no session found in the scene, try instantiating the prefab at a spawn point
        if (session == null && flowerPrefab != null)
        {
            Transform spawnPoint = null;
            foreach (var root in flowerScene.GetRootGameObjects())
            {
                var sp = root.transform.Find("FlowerSpawnPoint");
                if (sp != null) { spawnPoint = sp; break; }
                if (root.name == "FlowerSpawnPoint") { spawnPoint = root.transform; break; }
            }

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            var flowerGO = Instantiate(flowerPrefab, spawnPos, spawnRot);
            SceneManager.MoveGameObjectToScene(flowerGO, flowerScene);

            session = flowerGO.GetComponentInChildren<FlowerSessionController>();
        }

        if (session == null)
        {
            Debug.LogWarning("[FlowerTrimmingBridge] No FlowerSessionController found in scene or on prefab. " +
                             "Results will default to 0.");
            _waitingForResult = false;
            _onComplete?.Invoke(0, 0, true);
            yield break;
        }

        // Reset session state in case the scene was previously played standalone
        session.sessionEnded = false;
        session.endRequested = false;

        // Wait for the session to produce a result
        int resultScore = 0;
        int resultDays = 0;
        bool resultGameOver = false;
        bool gotResult = false;

        session.OnResult.AddListener((eval, score, days) =>
        {
            resultScore = score;
            resultDays = days;
            resultGameOver = eval.isGameOver;
            gotResult = true;
        });

        // Enable keyboard evaluate so player can press E to finish
        session.allowKeyboardEvaluate = true;

        // Wait until the session produces a result
        while (!gotResult)
            yield return null;

        // Brief pause so the player can see their result
        yield return new WaitForSeconds(2f);

        // Snapshot the trimmed flower visuals BEFORE unloading the scene
        GameObject trimmedVisual = null;
        if (session.brain != null)
        {
            trimmedVisual = TrimmedFlowerSnapshot.Capture(session.brain);
            // Move to DontDestroyOnLoad so it survives the scene unload
            Object.DontDestroyOnLoad(trimmedVisual);
            trimmedVisual.SetActive(false); // hide until placed
        }

        // Capture results in data pipeline
        DateOutcomeCapture.CaptureFlowerResult(resultScore, resultDays, resultGameOver);
        string grade = DateOutcomeCapture.LastOutcome.flowerGrade;
        DateHistory.UpdateFlowerResult(resultScore, resultDays, grade);

        // Spawn living plant in apartment (if score earned any days alive)
        if (resultDays > 0 && LivingFlowerPlantManager.Instance != null)
        {
            string charName = DateOutcomeCapture.LastOutcome.characterName;
            LivingFlowerPlantManager.Instance.SpawnPlant(charName, resultDays, trimmedVisual);
        }
        else if (trimmedVisual != null)
        {
            // No days alive — discard the snapshot
            Object.Destroy(trimmedVisual);
        }

        // Clean up
        DateSessionManager.PendingFlowerPrefab = null;
        _waitingForResult = false;

        // Invoke callback before unloading
        _onComplete?.Invoke(resultScore, resultDays, resultGameOver);
        _onComplete = null;

        // Unload the flower scene
        var unloadOp = SceneManager.UnloadSceneAsync(flowerScene);
        if (unloadOp != null)
            yield return unloadOp;

        Debug.Log($"[FlowerTrimmingBridge] Trimming complete. Scene={sceneName}, " +
                  $"Score={resultScore}, Days={resultDays}, GameOver={resultGameOver}");
    }
}
