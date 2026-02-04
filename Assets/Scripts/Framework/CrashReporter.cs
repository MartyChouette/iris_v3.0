/**
 * @file CrashReporter.cs
 * @brief Static utility that captures unhandled exceptions and logs to a local file.
 *
 * @details
 * Hooks into Application.logMessageReceived to capture errors, exceptions,
 * and assert failures. Writes them to a rolling crash log file at
 * {persistentDataPath}/iris_crashlog.txt.
 *
 * Pattern:
 * - Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 * - File path: {persistentDataPath}/iris_crashlog.txt
 * - Keeps the last N entries (default 200) to avoid unbounded growth.
 * - Duplicates within a short window are suppressed to prevent log spam.
 *
 * @ingroup framework
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CrashReporter
{
    private const string FILE_NAME = "iris_crashlog.txt";
    private const int MAX_ENTRIES = 200;
    private const int DEDUP_WINDOW_SECONDS = 10;

    private static bool s_initialized;
    private static readonly Dictionary<int, float> s_recentHashes =
        new Dictionary<int, float>();

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        if (s_initialized)
        {
            Application.logMessageReceived -= OnLogMessage;
            s_initialized = false;
        }
        s_recentHashes.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (s_initialized) return;
        Application.logMessageReceived += OnLogMessage;
        s_initialized = true;
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            return;

        int hash = condition.GetHashCode();
        float now = Time.realtimeSinceStartup;

        // Suppress duplicates within the dedup window.
        if (s_recentHashes.TryGetValue(hash, out float lastTime))
        {
            if (now - lastTime < DEDUP_WINDOW_SECONDS)
                return;
        }
        s_recentHashes[hash] = now;

        string entry =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}]\n" +
            $"{condition}\n" +
            $"{stackTrace}\n" +
            "---\n";

        try
        {
            File.AppendAllText(FilePath, entry);
            TrimFileIfNeeded();
        }
        catch (Exception)
        {
            // Avoid recursive logging.
        }
    }

    private static void TrimFileIfNeeded()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path)) return;

            string[] lines = File.ReadAllLines(path);
            if (lines.Length <= MAX_ENTRIES * 4) return;

            // Keep last MAX_ENTRIES entries (each entry is ~4 lines).
            int keepFrom = lines.Length - (MAX_ENTRIES * 4);
            if (keepFrom < 0) keepFrom = 0;

            using (var writer = new StreamWriter(path, false))
            {
                for (int i = keepFrom; i < lines.Length; i++)
                    writer.WriteLine(lines[i]);
            }
        }
        catch (Exception)
        {
            // Silently fail to avoid recursive issues.
        }
    }
}
