using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Zelda-style "You got an item!" presentation for the flower gift after a successful date.
/// Scene-scoped singleton. Spawns flower prefab in front of camera, bobs and spins it,
/// shows dark overlay with announcement text, plays SFX, then cleans up.
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

    [Tooltip("Vertical bob amplitude in world units.")]
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

        // Spawn flower in front of camera
        Vector3 spawnPos = cam.transform.position
            + cam.transform.forward * _spawnOffset.z
            + cam.transform.up * _spawnOffset.y
            + cam.transform.right * _spawnOffset.x;
        var flowerClone = Instantiate(flowerPrefab, spawnPos, Quaternion.identity);
        float baseY = spawnPos.y;

        // Set up overlay
        if (_overlayRoot != null)
            _overlayRoot.SetActive(true);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

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

        // Hold: bob + spin
        elapsed = 0f;
        while (elapsed < _holdDuration)
        {
            elapsed += Time.deltaTime;

            if (flowerClone != null)
            {
                // Bob
                var pos = flowerClone.transform.position;
                pos.y = baseY + Mathf.Sin(elapsed * _bobSpeed * Mathf.PI * 2f) * _bobAmplitude;
                flowerClone.transform.position = pos;

                // Spin
                flowerClone.transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
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
            _canvasGroup.alpha = 0f;

        // Cleanup
        if (flowerClone != null)
            Destroy(flowerClone);

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }
}
