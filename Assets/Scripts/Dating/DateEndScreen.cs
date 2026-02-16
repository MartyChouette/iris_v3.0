using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Results screen shown after a date ends. Displays affection score, grade, and summary.
/// On continue, fades to black and loads the flower trimming scene.
/// </summary>
public class DateEndScreen : MonoBehaviour
{
    public static DateEndScreen Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private TMP_Text dateNameText;
    [SerializeField] private TMP_Text affectionScoreText;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button continueButton;

    [Header("Audio")]
    [SerializeField] private AudioClip goodDateSFX;
    [SerializeField] private AudioClip badDateSFX;

    [Header("Scene Transition")]
    [Tooltip("Build index of the flower trimming scene. -1 = no scene load (stay in apartment).")]
    [SerializeField] private int _flowerSceneIndex = -1;
    [Tooltip("Fade duration before loading flower scene.")]
    [SerializeField] private float _fadeDuration = 0.8f;

    private bool _transitioning;
    private bool _dateWasFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DateEndScreen] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (screenRoot != null)
            screenRoot.SetActive(false);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Show the date end screen with results.</summary>
    public void Show(DatePersonalDefinition date, float affection, bool failed = false)
    {
        if (screenRoot == null) return;

        screenRoot.SetActive(true);
        _transitioning = false;
        _dateWasFailed = failed;

        string grade = failed ? "F" : ComputeGrade(affection);

        if (dateNameText != null)
            dateNameText.text = date != null ? date.characterName : "???";

        if (affectionScoreText != null)
            affectionScoreText.text = $"{affection:F0}%";

        if (gradeText != null)
        {
            gradeText.text = grade;
            gradeText.color = grade == "F"
                ? new Color(1f, 0.3f, 0.3f)
                : grade switch
                {
                    "S" => new Color(1f, 0.84f, 0f),
                    "A" => new Color(0.4f, 1f, 0.4f),
                    "B" => new Color(0.4f, 0.8f, 1f),
                    "C" => new Color(1f, 0.8f, 0.4f),
                    _ => new Color(1f, 0.4f, 0.4f)
                };
        }

        if (summaryText != null)
        {
            summaryText.text = failed
                ? "They left early..."
                : grade switch
                {
                    "S" => "A perfect connection! They'll definitely call back.",
                    "A" => "A wonderful time together. Very promising!",
                    "B" => "A pleasant evening. Room for improvement.",
                    "C" => "An awkward date. Better luck next time.",
                    _ => "That did not go well at all..."
                };
        }

        // Play SFX
        AudioClip clip = failed ? badDateSFX : (affection >= 60f ? goodDateSFX : badDateSFX);
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip);

        Debug.Log($"[DateEndScreen] Grade: {grade} (Affection: {affection:F0}%) Failed: {failed}");
    }

    /// <summary>Compute letter grade from affection score.</summary>
    public static string ComputeGrade(float affection)
    {
        if (affection >= 90f) return "S";
        if (affection >= 75f) return "A";
        if (affection >= 60f) return "B";
        if (affection >= 40f) return "C";
        return "D";
    }

    private void OnContinue()
    {
        if (_transitioning) return;

        if (screenRoot != null)
            screenRoot.SetActive(false);

        // Only load flower scene on success
        if (!_dateWasFailed && _flowerSceneIndex >= 0)
        {
            _transitioning = true;
            if (ScreenFade.Instance != null && _fadeDuration > 0f)
                StartCoroutine(FadeAndLoad(_flowerSceneIndex));
            else
                SceneManager.LoadScene(_flowerSceneIndex);
        }
        else
        {
            Debug.Log(_dateWasFailed
                ? "[DateEndScreen] Date failed — no flower scene."
                : "[DateEndScreen] No flower scene configured. Staying in apartment.");
        }
    }

    private IEnumerator FadeAndLoad(int sceneIndex)
    {
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);
        SceneManager.LoadScene(sceneIndex);
    }
}
