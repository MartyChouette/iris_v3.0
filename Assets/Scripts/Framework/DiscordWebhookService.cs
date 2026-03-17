using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Static utility for posting rich embeds + file attachments to Discord webhooks.
/// Uses System.Net.Http.HttpClient instead of UnityWebRequest to avoid
/// Unity 6's curl HTTP/2 PROTOCOL_ERROR with Discord's API.
/// Call via StartCoroutine on any MonoBehaviour.
/// </summary>
public static class DiscordWebhookService
{
    private static readonly HttpClient s_client = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

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
        if (string.IsNullOrEmpty(webhookUrl))
        {
            Debug.LogWarning("[DiscordWebhook] No webhook URL configured — skipping.");
            yield break;
        }

        string timestamp = DateTime.UtcNow.ToString("o");
        string json = BuildEmbedJson(title, description, color, fields, footerText, timestamp);

        Debug.Log($"[DiscordWebhook] Posting to Discord... " +
            $"(screenshot: {(screenshotPng != null ? $"{screenshotPng.Length / 1024}KB" : "none")})");

        bool done = false;
        bool success = false;
        string error = null;

        // Run on background thread to avoid blocking Unity main thread
        Task.Run(async () =>
        {
            try
            {
                HttpResponseMessage response;

                if (screenshotPng != null && screenshotPng.Length > 0)
                {
                    var multipart = new MultipartFormDataContent();
                    multipart.Add(new StringContent(json), "payload_json");

                    var fileContent = new ByteArrayContent(screenshotPng);
                    fileContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    multipart.Add(fileContent, "file", "screenshot.jpg");

                    response = await s_client.PostAsync(webhookUrl, multipart);
                }
                else
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = await s_client.PostAsync(webhookUrl, content);
                }

                success = response.IsSuccessStatusCode;
                if (!success)
                    error = $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            finally
            {
                done = true;
            }
        });

        // Wait for background task to complete
        while (!done)
            yield return null;

        if (success)
            Debug.Log("[DiscordWebhook] Post succeeded.");
        else
            Debug.LogWarning($"[DiscordWebhook] Post failed: {error}");
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
