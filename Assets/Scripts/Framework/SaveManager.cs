/**
 * @file SaveManager.cs
 * @brief Static utility for saving/loading session history to a local JSON file.
 *
 * @details
 * Persists a rolling list of SessionSaveData entries to Application.persistentDataPath.
 * Keeps the last N sessions (default 50) to avoid unbounded growth.
 *
 * Pattern:
 * - Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 * - File path: {persistentDataPath}/iris_sessions.json
 *
 * @ingroup framework
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveManager
{
    private const string FILE_NAME = "iris_sessions.json";
    private const int MAX_SESSIONS = 50;

    [Serializable]
    private class SaveFile
    {
        public List<SessionSaveData> sessions = new List<SessionSaveData>();
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    public static void SaveSession(SessionSaveData data)
    {
        if (data == null) return;

        var file = LoadFile();
        data.timestamp = DateTime.Now.ToString("o");
        file.sessions.Add(data);

        while (file.sessions.Count > MAX_SESSIONS)
            file.sessions.RemoveAt(0);

        WriteFile(file);
        Debug.Log($"[SaveManager] Session saved ({file.sessions.Count} total).");
    }

    public static List<SessionSaveData> LoadAllSessions()
    {
        return LoadFile().sessions;
    }

    public static SessionSaveData GetBestSession()
    {
        var sessions = LoadFile().sessions;
        if (sessions.Count == 0) return null;

        SessionSaveData best = sessions[0];
        for (int i = 1; i < sessions.Count; i++)
        {
            if (sessions[i].overallScore > best.overallScore)
                best = sessions[i];
        }
        return best;
    }

    public static void ClearAll()
    {
        WriteFile(new SaveFile());
        Debug.Log("[SaveManager] All session data cleared.");
    }

    private static SaveFile LoadFile()
    {
        string path = FilePath;
        if (!File.Exists(path))
            return new SaveFile();

        try
        {
            string json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<SaveFile>(json);
            return file ?? new SaveFile();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to load save file: {e.Message}");
            return new SaveFile();
        }
    }

    private static void WriteFile(SaveFile file)
    {
        try
        {
            string json = JsonUtility.ToJson(file, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to write save file: {e.Message}");
        }
    }
}
