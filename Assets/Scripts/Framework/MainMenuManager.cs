using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu controller. Starts the game by loading the apartment scene.
/// Supports fade-out via ScreenFade if present, otherwise hard-cuts.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Build index of the apartment scene. -1 = next scene after this one.")]
    [SerializeField] private int _apartmentSceneIndex = -1;

    [Header("Fade")]
    [Tooltip("Fade duration before loading. 0 = instant.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    private bool _starting;

    public void StartGame()
    {
        if (_starting) return;
        _starting = true;

        int targetIndex = _apartmentSceneIndex >= 0
            ? _apartmentSceneIndex
            : SceneManager.GetActiveScene().buildIndex + 1;

        if (ScreenFade.Instance != null && _fadeDuration > 0f)
            StartCoroutine(FadeAndLoad(targetIndex));
        else
            SceneManager.LoadScene(targetIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private IEnumerator FadeAndLoad(int sceneIndex)
    {
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);
        SceneManager.LoadScene(sceneIndex);
    }
}
