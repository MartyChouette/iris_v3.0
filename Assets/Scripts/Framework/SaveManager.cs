/**
 * @file SaveManager.cs
 * @brief Static utility for saving/loading game state and session history.
 *
 * @details
 * Two systems in Application.persistentDataPath:
 * - iris_save_0/1/2.json  — 3 game save slots (IrisSaveData)
 * - iris_sessions.json    — flower trimming session history (rolling)
 *
 * Pattern: Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveManager
{
    // ── Slot System ──────────────────────────────────────────────

    public const int SlotCount = 3;

    /// <summary>Currently active save slot (0-2). Set before loading/saving.</summary>
    public static int ActiveSlot { get; set; }

    private static string SlotFilePath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"iris_save_{slot}.json");

    /// <summary>Save game state to the active slot.</summary>
    public static void SaveGame(IrisSaveData data)
    {
        if (data == null) return;

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SlotFilePath(ActiveSlot), json);
            Debug.Log($"[SaveManager] Slot {ActiveSlot} saved (Day {data.currentDay}).");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to write slot {ActiveSlot}: {e.Message}");
        }
    }

    /// <summary>Load game state from the active slot. Returns null if no save.</summary>
    public static IrisSaveData LoadGame()
    {
        string path = SlotFilePath(ActiveSlot);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<IrisSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to load slot {ActiveSlot}: {e.Message}");
            return null;
        }
    }

    /// <summary>Check if a specific slot has a save file.</summary>
    public static bool HasSave(int slot) => File.Exists(SlotFilePath(slot));

    /// <summary>Peek at a slot's save data without setting it active. Returns null if empty.</summary>
    public static IrisSaveData PeekSlot(int slot)
    {
        string path = SlotFilePath(slot);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<IrisSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to peek slot {slot}: {e.Message}");
            return null;
        }
    }

    /// <summary>Delete a save slot.</summary>
    public static void DeleteSlot(int slot)
    {
        string path = SlotFilePath(slot);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveManager] Slot {slot} deleted.");
        }
    }

    // ── Flower Session History (unchanged) ──────────────────────

    private const string SESSION_FILE = "iris_sessions.json";
    private const int MAX_SESSIONS = 50;

    [Serializable]
    private class SaveFile
    {
        public List<SessionSaveData> sessions = new List<SessionSaveData>();
    }

    private static string SessionFilePath => Path.Combine(Application.persistentDataPath, SESSION_FILE);

    public static void SaveSession(SessionSaveData data)
    {
        if (data == null) return;

        var file = LoadSessionFile();
        data.timestamp = DateTime.Now.ToString("o");
        file.sessions.Add(data);

        while (file.sessions.Count > MAX_SESSIONS)
            file.sessions.RemoveAt(0);

        WriteSessionFile(file);
        Debug.Log($"[SaveManager] Session saved ({file.sessions.Count} total).");
    }

    public static List<SessionSaveData> LoadAllSessions()
    {
        return LoadSessionFile().sessions;
    }

    public static SessionSaveData GetBestSession()
    {
        var sessions = LoadSessionFile().sessions;
        if (sessions.Count == 0) return null;

        SessionSaveData best = sessions[0];
        for (int i = 1; i < sessions.Count; i++)
        {
            if (sessions[i].overallScore > best.overallScore)
                best = sessions[i];
        }
        return best;
    }

    public static void ClearAllSessions()
    {
        WriteSessionFile(new SaveFile());
        Debug.Log("[SaveManager] All session data cleared.");
    }

    private static SaveFile LoadSessionFile()
    {
        string path = SessionFilePath;
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
            Debug.LogWarning($"[SaveManager] Failed to load session file: {e.Message}");
            return new SaveFile();
        }
    }

    private static void WriteSessionFile(SaveFile file)
    {
        try
        {
            string json = JsonUtility.ToJson(file, true);
            File.WriteAllText(SessionFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to write session file: {e.Message}");
        }
    }
}
