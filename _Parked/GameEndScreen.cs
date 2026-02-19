using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Score summary + credits overlay shown when the game mode ends
/// (GameClock.OnCalendarComplete fires). Displays stats and returns to main menu.
/// </summary>
public class GameEndScreen : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _summaryLabel;
    [SerializeField] private TMP_Text _creditsLabel;
    [SerializeField] private Button _returnButton;

    [Header("Scene")]
    [Tooltip("Build index of the main menu scene.")]
    [SerializeField] private int _menuSceneIndex;

    private void Start()
    {
        if (_root != null)
            _root.SetActive(false);

        if (_returnButton != null)
            _returnButton.onClick.AddListener(ReturnToMenu);
    }

    public void Show()
    {
        if (_root != null)
            _root.SetActive(true);

        BuildSummary();
        BuildCredits();

        Debug.Log("[GameEndScreen] Showing end screen.");
    }

    private void BuildSummary()
    {
        if (_summaryLabel == null) return;

        var sb = new StringBuilder();

        // Mode name
        var mode = MainMenuManager.ActiveGameMode;
        if (mode != null)
            sb.AppendLine($"<b>{mode.modeName}</b> complete!");
        else
            sb.AppendLine("<b>Game Complete!</b>");

        sb.AppendLine();

        // Stats from DateHistory
        int totalDates = DateHistory.TotalDatesCompleted();
        sb.AppendLine($"Dates completed: {totalDates}");

        if (totalDates > 0)
        {
            float bestAffection = DateHistory.BestAffection();
            sb.AppendLine($"Best affection: {bestAffection:F0}%");

            // Find favorite date (highest affection)
            var entries = DateHistory.Entries;
            string favoriteName = "";
            float favoriteScore = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].finalAffection > favoriteScore)
                {
                    favoriteScore = entries[i].finalAffection;
                    favoriteName = entries[i].characterName;
                }
            }
            if (!string.IsNullOrEmpty(favoriteName))
                sb.AppendLine($"Favorite date: {favoriteName}");
        }

        // Days played
        int daysPlayed = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        sb.AppendLine($"Days played: {daysPlayed}");

        _summaryLabel.text = sb.ToString();
    }

    private void BuildCredits()
    {
        if (_creditsLabel == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("<b>IRIS</b>");
        sb.AppendLine("A contemplative flower-trimming game");
        sb.AppendLine();
        sb.AppendLine("Thesis project");
        sb.AppendLine("Thank you for playing.");

        _creditsLabel.text = sb.ToString();
    }

    private void ReturnToMenu()
    {
        // Clear the active game mode so main menu starts fresh
        MainMenuManager.ActiveGameMode = null;
        SceneManager.LoadScene(_menuSceneIndex);
    }
}
