using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Zelda-style "You got an item!" presentation for the flower gift after a successful date.
/// Scene-scoped singleton. Spawns flower prefab in front of camera, bobs and spins it,
/// shows dark overlay with announcement text, plays SFX, then cleans up.
///
/// The flower prefab may come from the flower trimming scene (large scale). This presenter
/// normalizes it to _presentationScale (default 0.1) and parents it to the camera so it
/// stays in view regardless of which Cinemachine camera is active.
/// </summary>
public class FlowerGiftPresenter : MonoBehaviour
{
    public static FlowerGiftPresenter Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root GameObject for the dark overlay (starts hidden).")]
    [SerializeField] private GameObject _overlayRoot;

    [Tooltip("CanvasGroup on the overlay for fade in/out.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Tooltip("Text displaying the gift announcement.")]
    [SerializeField] private TMP_Text _itemNameText;

    [Header("Audio")]
    [Tooltip("Jingle SFX played when the flower is presented.")]
    [SerializeField] private AudioClip _presentSFX;

    [Header("Presentation")]
    [Tooltip("Offset from camera (local space: right, up, forward).")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 0.3f, 1.5f);

    [Tooltip("Scale applied to the flower clone (flower prefabs are often at trimming-scene scale).")]
    [SerializeField] private float _presentationScale = 0.1f;

    [Tooltip("Vertical bob amplitude in local units.")]
    [SerializeField] private float _bobAmplitude = 0.08f;

    [Tooltip("Bob oscillation speed.")]
    [SerializeField] private float _bobSpeed = 2.0f;

    [Tooltip("Y-axis spin speed in degrees per second.")]
    [SerializeField] private float _spinSpeed = 45.0f;

    [Tooltip("How long the flower is held on screen (seconds).")]
    [SerializeField] private float _holdDuration = 2.5f;

    [Tooltip("Fade in/out duration (seconds).")]
    [SerializeField] private float _fadeDuration = 0.3f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[FlowerGiftPresenter] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Present a flower gift. Yields until the full animation + fade sequence is complete.
    /// </summary>
    public Coroutine Present(GameObject flowerPrefab, string characterName)
    {
        return StartCoroutine(PresentSequence(flowerPrefab, characterName));
    }

    private IEnumerator PresentSequence(GameObject flowerPrefab, string characterName)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[FlowerGiftPresenter] No main camera found, skipping presentation.");
            yield break;
        }

        // Spawn flower as child of camera so it stays in view regardless of camera movement.
        var flowerClone = Instantiate(flowerPrefab, cam.transform);
        flowerClone.transform.localPosition = _spawnOffset;
        flowerClone.transform.localRotation = Quaternion.identity;
        flowerClone.transform.localScale = Vector3.one * _presentationScale;

        // Strip physics so the clone doesn't interact with the world
        foreach (var rb in flowerClone.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;
        foreach (var col in flowerClone.GetComponentsInChildren<Collider>())
            col.enabled = false;

        float baseLocalY = _spawnOffset.y;

        // Set up overlay and block input
        if (_overlayRoot != null)
            _overlayRoot.SetActive(true);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
        }

        if (_itemNameText != null)
            _itemNameText.text = $"{characterName} gave you a flower!";

        // Play SFX
        if (_presentSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_presentSFX);

        // Fade in
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
            yield return null;
        }
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        // Hold: bob + spin (in local space since flower is parented to camera)
        elapsed = 0f;
        while (elapsed < _holdDuration)
        {
            elapsed += Time.deltaTime;

            if (flowerClone != null)
            {
                // Bob in local Y
                var localPos = flowerClone.transform.localPosition;
                localPos.y = baseLocalY + Mathf.Sin(elapsed * _bobSpeed * Mathf.PI * 2f) * _bobAmplitude;
                flowerClone.transform.localPosition = localPos;

                // Spin around local up axis
                flowerClone.transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.Self);
            }

            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
            yield return null;
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        // Cleanup
        if (flowerClone != null)
            Destroy(flowerClone);

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }
}
