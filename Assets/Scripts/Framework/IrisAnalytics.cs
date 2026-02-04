/**
 * @file IrisAnalytics.cs
 * @brief Static utility for logging gameplay telemetry events to a local file.
 *
 * @details
 * Records timestamped events (cuts, detachments, scores, session boundaries)
 * to a JSON-lines file for thesis research analysis.
 *
 * Pattern:
 * - Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 * - File path: {persistentDataPath}/iris_analytics.jsonl
 * - One JSON object per line (JSON-lines format) for easy parsing.
 *
 * Performance:
 * - Events are appended incrementally (no full-file rewrite).
 * - StreamWriter is opened/closed per event to avoid data loss on crash.
 *
 * @ingroup framework
 */

using System;
using System.IO;
using UnityEngine;

public static class IrisAnalytics
{
    private const string FILE_NAME = "iris_analytics.jsonl";

    private static bool s_enabled = true;
    private static string s_sessionId;

    public static bool Enabled
    {
        get => s_enabled;
        set => s_enabled = value;
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_enabled = true;
        s_sessionId = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        s_sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    // ── Public API ──

    public static void LogSessionStart(string flowerType)
    {
        Log("session_start", $"\"flowerType\":\"{Escape(flowerType)}\"");
    }

    public static void LogSessionEnd(float durationSeconds, float score, bool gameOver, string reason)
    {
        Log("session_end",
            $"\"duration\":{durationSeconds:F2}," +
            $"\"score\":{score:F3}," +
            $"\"gameOver\":{BoolStr(gameOver)}," +
            $"\"reason\":\"{Escape(reason)}\"");
    }

    public static void LogCut(string partId, Vector3 position, float angle)
    {
        Log("cut",
            $"\"partId\":\"{Escape(partId)}\"," +
            $"\"x\":{position.x:F2},\"y\":{position.y:F2},\"z\":{position.z:F2}," +
            $"\"angle\":{angle:F1}");
    }

    public static void LogDetach(string partId, string reason)
    {
        Log("detach",
            $"\"partId\":\"{Escape(partId)}\",\"reason\":\"{Escape(reason)}\"");
    }

    public static void LogCustom(string eventName, string jsonFields)
    {
        Log(eventName, jsonFields);
    }

    // ── Internals ──

    private static void Log(string eventType, string fields)
    {
        if (!s_enabled) return;

        string timestamp = DateTime.Now.ToString("o");
        string line = $"{{\"t\":\"{timestamp}\",\"sid\":\"{s_sessionId}\",\"e\":\"{eventType}\",{fields}}}";

        try
        {
            File.AppendAllText(FilePath, line + "\n");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IrisAnalytics] Failed to write event: {e.Message}");
        }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    private static string BoolStr(bool b) => b ? "true" : "false";
}
