using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Static utility for posting rich embeds + file attachments to Discord webhooks.
/// Call via StartCoroutine on any MonoBehaviour.
/// </summary>
public static class DiscordWebhookService
{
    /// <summary>
    /// Post a rich embed to a Discord webhook, optionally with a screenshot attachment.
    /// </summary>
    public static IEnumerator PostEmbed(
        string webhookUrl,
        string title,
        string description,
        int color,
        (string name, string value, bool inline)[] fields,
        string footerText,
        byte[] screenshotPng = null)
    {
        if (string.IsNullOrEmpty(webhookUrl)) yield break;

        string timestamp = System.DateTime.UtcNow.ToString("o");
        string json = BuildEmbedJson(title, description, color, fields, footerText, timestamp);

        UnityWebRequest request;

        if (screenshotPng != null && screenshotPng.Length > 0)
        {
            // Multipart: payload_json + file attachment
            var form = new WWWForm();
            form.AddField("payload_json", json);
            form.AddBinaryData("file", screenshotPng, "screenshot.png", "image/png");
            request = UnityWebRequest.Post(webhookUrl, form);
        }
        else
        {
            // JSON only
            var bodyBytes = Encoding.UTF8.GetBytes(json);
            request = new UnityWebRequest(webhookUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("[DiscordWebhook] Post succeeded.");
            else
                Debug.LogWarning($"[DiscordWebhook] Post failed: {request.error} — {request.downloadHandler?.text}");
        }
    }

    private static string BuildEmbedJson(
        string title,
        string description,
        int color,
        (string name, string value, bool inline)[] fields,
        string footerText,
        string timestamp)
    {
        var sb = new StringBuilder(512);
        sb.Append("{\"embeds\":[{");

        sb.Append("\"title\":\"").Append(Escape(title)).Append("\",");

        if (!string.IsNullOrEmpty(description))
            sb.Append("\"description\":\"").Append(Escape(Truncate(description, 4000))).Append("\",");

        sb.Append("\"color\":").Append(color).Append(',');

        if (fields != null && fields.Length > 0)
        {
            sb.Append("\"fields\":[");
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var f = fields[i];
                sb.Append("{\"name\":\"").Append(Escape(Truncate(f.name, 256)));
                sb.Append("\",\"value\":\"").Append(Escape(Truncate(f.value, 1024)));
                sb.Append("\",\"inline\":").Append(f.inline ? "true" : "false").Append('}');
            }
            sb.Append("],");
        }

        if (!string.IsNullOrEmpty(footerText))
            sb.Append("\"footer\":{\"text\":\"").Append(Escape(Truncate(footerText, 2048))).Append("\"},");

        if (!string.IsNullOrEmpty(timestamp))
            sb.Append("\"timestamp\":\"").Append(timestamp).Append("\",");

        // Remove trailing comma
        if (sb[sb.Length - 1] == ',')
            sb.Length--;

        sb.Append("}]}");
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string Truncate(string s, int maxLength)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= maxLength) return s;
        return s.Substring(0, maxLength - 3) + "...";
    }
}
