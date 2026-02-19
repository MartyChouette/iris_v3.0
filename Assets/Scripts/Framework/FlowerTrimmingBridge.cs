using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-scoped singleton that manages the transition into and out of the
/// flower trimming scene. Lives in the apartment scene, loads FlowerTrimming
/// additively, captures results, and unloads it.
/// </summary>
public class FlowerTrimmingBridge : MonoBehaviour
{
    public static FlowerTrimmingBridge Instance { get; private set; }

    private const string FlowerSceneName = "FlowerTrimming";

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
    /// Load the flower trimming scene additively, instantiate the flower,
    /// wait for the player to trim it, and invoke onComplete with results.
    /// </summary>
    /// <param name="flowerPrefab">The flower prefab to instantiate at the spawn point.</param>
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

    private IEnumerator TrimmingSequence(GameObject flowerPrefab)
    {
        // Load the flower trimming scene additively
        var loadOp = SceneManager.LoadSceneAsync(FlowerSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"[FlowerTrimmingBridge] Failed to load scene '{FlowerSceneName}'. " +
                           "Is it added to Build Settings?");
            _waitingForResult = false;
            _onComplete?.Invoke(0, 0, true);
            yield break;
        }

        yield return loadOp;

        var flowerScene = SceneManager.GetSceneByName(FlowerSceneName);
        if (!flowerScene.IsValid())
        {
            Debug.LogError("[FlowerTrimmingBridge] Flower scene not valid after load.");
            _waitingForResult = false;
            _onComplete?.Invoke(0, 0, true);
            yield break;
        }

        // Find the spawn point in the flower scene
        Transform spawnPoint = null;
        foreach (var root in flowerScene.GetRootGameObjects())
        {
            var sp = root.transform.Find("FlowerSpawnPoint");
            if (sp != null) { spawnPoint = sp; break; }
            if (root.name == "FlowerSpawnPoint") { spawnPoint = root.transform; break; }
        }

        // Instantiate the flower prefab
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        var flowerGO = Instantiate(flowerPrefab, spawnPos, spawnRot);
        SceneManager.MoveGameObjectToScene(flowerGO, flowerScene);

        // Find or add FlowerSessionController
        var session = flowerGO.GetComponentInChildren<FlowerSessionController>();
        if (session == null)
        {
            Debug.LogWarning("[FlowerTrimmingBridge] No FlowerSessionController on flower prefab. " +
                             "Results will default to 0.");
            _waitingForResult = false;
            _onComplete?.Invoke(0, 0, true);
            yield break;
        }

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

        // Capture results in data pipeline
        DateOutcomeCapture.CaptureFlowerResult(resultScore, resultDays, resultGameOver);
        string grade = DateOutcomeCapture.LastOutcome.flowerGrade;
        DateHistory.UpdateFlowerResult(resultScore, resultDays, grade);

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

        Debug.Log($"[FlowerTrimmingBridge] Trimming complete. Score={resultScore}, " +
                  $"Days={resultDays}, GameOver={resultGameOver}");
    }
}
