using UnityEngine;
using TMPro;

/// <summary>
/// Scene-scoped singleton that shows a brief text description at the bottom of the
/// screen when the player picks up an apartment item. Auto-hides after a timeout.
/// </summary>
public class PickupDescriptionHUD : MonoBehaviour
{
    public static PickupDescriptionHUD Instance { get; private set; }

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _descriptionText;

    private float _hideTimer;
    private const float AutoHideDuration = 4f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PickupDescriptionHUD] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_panelRoot == null || !_panelRoot.activeSelf) return;

        _hideTimer -= Time.unscaledDeltaTime;
        if (_hideTimer <= 0f)
            Hide();
    }

    public void Show(string text)
    {
        if (_panelRoot == null || _descriptionText == null) return;

        _descriptionText.text = text;
        _panelRoot.SetActive(true);
        _hideTimer = AutoHideDuration;
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
