using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Full-screen content and seizure warning shown once at app startup.
/// Auto-spawns via RuntimeInitializeOnLoadMethod, dismisses on click or after hold duration.
/// </summary>
public class ContentWarningScreen : MonoBehaviour
{
    private static bool s_hasShown;

    private GameObject _canvasRoot;
    private CanvasGroup _canvasGroup;
    private float _holdTimer;
    private float _fadeTimer;
    private bool _dismissing;

    private const float HoldDuration = 4f;
    private const float FadeDuration = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (s_hasShown) return;
        s_hasShown = true;

        var go = new GameObject("ContentWarningScreen");
        DontDestroyOnLoad(go);
        go.AddComponent<ContentWarningScreen>();
    }

    private void Awake()
    {
        BuildUI();
    }

    private void Update()
    {
        if (_dismissing)
        {
            _fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_fadeTimer / FadeDuration);
            if (_canvasGroup != null) _canvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
            return;
        }

        _holdTimer += Time.unscaledDeltaTime;

        // Dismiss on click or after hold duration
        if (_holdTimer >= HoldDuration || Input.anyKeyDown)
        {
            _dismissing = true;
        }
    }

    private void OnDestroy()
    {
        if (_canvasRoot != null)
            Destroy(_canvasRoot);
    }

    private void BuildUI()
    {
        _canvasRoot = new GameObject("WarningCanvas");
        _canvasRoot.transform.SetParent(transform, false);

        var canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // on top of everything

        var scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasRoot.AddComponent<GraphicRaycaster>();

        _canvasGroup = _canvasRoot.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // Full-screen black background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(_canvasRoot.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.03f, 1f);

        // Warning title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(_canvasRoot.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = new Vector2(800f, 60f);
        titleRT.anchoredPosition = new Vector2(0f, 100f);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "Content Warning";
        titleTMP.fontSize = 40f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.95f, 0.85f, 0.7f);

        // Warning body
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(_canvasRoot.transform, false);
        var bodyRT = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRT.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRT.sizeDelta = new Vector2(700f, 200f);
        bodyRT.anchoredPosition = new Vector2(0f, -20f);
        var bodyTMP = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyTMP.text =
            "This game contains flashing lights, screen effects, and visual patterns\n" +
            "that may cause discomfort or trigger seizures in photosensitive individuals.\n\n" +
            "It also contains themes of mild horror, sharp objects, and stylized violence.\n\n" +
            "Player discretion is advised.";
        bodyTMP.fontSize = 22f;
        bodyTMP.alignment = TextAlignmentOptions.Center;
        bodyTMP.color = new Color(0.75f, 0.73f, 0.68f);
        bodyTMP.enableWordWrapping = true;
        bodyTMP.lineSpacing = 8f;

        // Dismiss hint
        var hintGO = new GameObject("Hint");
        hintGO.transform.SetParent(_canvasRoot.transform, false);
        var hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0.5f, 0.5f);
        hintRT.anchorMax = new Vector2(0.5f, 0.5f);
        hintRT.sizeDelta = new Vector2(400f, 30f);
        hintRT.anchoredPosition = new Vector2(0f, -180f);
        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        hintTMP.text = "Press any key to continue";
        hintTMP.fontSize = 16f;
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.color = new Color(0.5f, 0.48f, 0.45f);
    }
}
