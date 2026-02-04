/**
 * @file SessionSaveData.cs
 * @brief Serializable data container for session results.
 *
 * @details
 * Stores the outcome of a single flower-trimming session for save/load
 * and analytics. Intentionally plain C# (no MonoBehaviour) so it can
 * be serialized to JSON with JsonUtility.
 *
 * @ingroup framework
 */

using System;

[Serializable]
public class SessionSaveData
{
    public string flowerType;
    public string timestamp;
    public float sessionDurationSeconds;
    public float overallScore;
    public float stemLengthScore;
    public float cutAngleScore;
    public float partsScore;
    public int totalCuts;
    public int partsDetached;
    public bool wasGameOver;
    public string gameOverReason;
}
