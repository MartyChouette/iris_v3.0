using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple show/dismiss controller for the tutorial card overlay.
/// Activated by MainMenuManager before loading the apartment scene.
/// </summary>
public class TutorialCard : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _startButton;
    [SerializeField] private AudioClip _dismissSFX;

    private Action _onDismiss;

    /// <summary>Show the tutorial card. When the player clicks START, onDismiss is invoked.</summary>
    public void Show(Action onDismiss)
    {
        _onDismiss = onDismiss;
        if (_root != null) _root.SetActive(true);
        if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnStartClicked()
    {
        if (_startButton != null) _startButton.onClick.RemoveListener(OnStartClicked);

        if (_dismissSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_dismissSFX);

        if (_root != null) _root.SetActive(false);

        _onDismiss?.Invoke();
        _onDismiss = null;
    }
}
