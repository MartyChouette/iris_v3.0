using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-scoped singleton that manages the transition into and out of a
/// flower trimming scene. Lives in the apartment scene, loads the flower
/// scene additively (which must contain a FlowerSessionController),
/// captures results, snapshots the trimmed flower, and unloads it.
/// </summary>
public class FlowerTrimmingBridge : MonoBehaviour
{
    public static FlowerTrimmingBridge Instance { get; private set; }

    [Header("Scene")]
    [Tooltip("Default flower trimming scene. Overridden per-date via DatePersonalDefinition.flowerSceneName.")]
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
    /// Load the flower trimming scene additively, find the FlowerSessionController,
    /// wait for the player to trim, and invoke onComplete with results.
    /// </summary>
    /// <param name="onComplete">Callback: (score, daysAlive, isGameOver)</param>
    public void BeginTrimming(Action<int, int, bool> onComplete)
    {
        if (_waitingForResult)
        {
            Debug.LogWarning("[FlowerTrimmingBridge] Already waiting for a trimming result.");
            return;
        }

        _onComplete = onComplete;
        _waitingForResult = true;
        StartCoroutine(TrimmingSequence());
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

    private IEnumerator TrimmingSequence()
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

        // Find the FlowerSessionController in the loaded scene
        FlowerSessionController session = null;
        foreach (var root in flowerScene.GetRootGameObjects())
        {
            session = root.GetComponentInChildren<FlowerSessionController>();
            if (session != null) break;
        }

        if (session == null)
        {
            Debug.LogWarning($"[FlowerTrimmingBridge] No FlowerSessionController found in '{sceneName}'. " +
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
            UnityEngine.Object.DontDestroyOnLoad(trimmedVisual);
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
            UnityEngine.Object.Destroy(trimmedVisual);
        }

        // Clean up
        DateSessionManager.PendingFlowerTrim = false;
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
