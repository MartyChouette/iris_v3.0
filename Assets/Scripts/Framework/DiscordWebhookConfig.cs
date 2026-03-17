using UnityEngine;

/// <summary>
/// ScriptableObject holding Discord webhook URLs for bug reports and feedback.
/// Place in Assets/Resources/ named "DiscordWebhookConfig".
/// Create via: right-click Project → Create > Iris > Discord Webhook Config
/// </summary>
[CreateAssetMenu(menuName = "Iris/Discord Webhook Config")]
public class DiscordWebhookConfig : ScriptableObject
{
    [Header("Webhook URLs")]
    [Tooltip("Discord webhook URL for bug reports (F9). Leave empty to skip Discord posting.")]
    [SerializeField] private string _bugReportWebhookURL = "";

    [Tooltip("Discord webhook URL for playtest feedback (F8). Leave empty to skip Discord posting.")]
    [SerializeField] private string _feedbackWebhookURL = "";

    public string BugReportWebhookURL => _bugReportWebhookURL;
    public string FeedbackWebhookURL => _feedbackWebhookURL;

    private static DiscordWebhookConfig _instance;

    public static DiscordWebhookConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<DiscordWebhookConfig>("DiscordWebhookConfig");
            return _instance;
        }
    }
}
