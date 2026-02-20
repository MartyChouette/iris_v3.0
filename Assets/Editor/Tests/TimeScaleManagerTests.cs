using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for TimeScaleManager static priority queue.
/// Covers set/clear/clearAll, priority ordering, and edge cases.
/// </summary>
public class TimeScaleManagerTests
{
    [SetUp]
    public void SetUp()
    {
        TimeScaleManager.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
        TimeScaleManager.ClearAll();
        Time.timeScale = 1f;
    }

    [Test]
    public void NoRequests_ActiveTimeScaleIsOne()
    {
        Assert.AreEqual(1f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Set_SingleRequest_AppliesScale()
    {
        TimeScaleManager.Set(10, 0.5f);
        Assert.AreEqual(0.5f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Set_LowerPriorityWins()
    {
        // Priority 0 (pause) beats priority 10 (game over)
        TimeScaleManager.Set(10, 0.5f);
        TimeScaleManager.Set(0, 0f);
        Assert.AreEqual(0f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Clear_RemovesWinnerFallsToNext()
    {
        TimeScaleManager.Set(0, 0f);
        TimeScaleManager.Set(10, 0.5f);
        Assert.AreEqual(0f, TimeScaleManager.ActiveTimeScale, 0.001f);

        TimeScaleManager.Clear(0);
        Assert.AreEqual(0.5f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void ClearAll_ResetsToOne()
    {
        TimeScaleManager.Set(0, 0f);
        TimeScaleManager.Set(10, 0.3f);
        TimeScaleManager.ClearAll();
        Assert.AreEqual(1f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Set_OverwriteSamePriority_UpdatesValue()
    {
        TimeScaleManager.Set(10, 0.3f);
        Assert.AreEqual(0.3f, TimeScaleManager.ActiveTimeScale, 0.001f);

        TimeScaleManager.Set(10, 0.7f);
        Assert.AreEqual(0.7f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Clear_OnlyRequest_ResetsToOne()
    {
        TimeScaleManager.Set(10, 0.5f);
        TimeScaleManager.Clear(10);
        Assert.AreEqual(1f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Clear_NonexistentPriority_NoCrash()
    {
        Assert.DoesNotThrow(() => TimeScaleManager.Clear(999));
        Assert.AreEqual(1f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }

    [Test]
    public void Set_AppliesTimeScaleToEngine()
    {
        TimeScaleManager.Set(10, 0.25f);
        Assert.AreEqual(0.25f, Time.timeScale, 0.001f);
    }

    [Test]
    public void ThreePriorities_LowestWins()
    {
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_JUICE, 0.8f);
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_GAME_OVER, 0.3f);
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_PAUSE, 0f);
        Assert.AreEqual(0f, TimeScaleManager.ActiveTimeScale, 0.001f);

        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);
        Assert.AreEqual(0.3f, TimeScaleManager.ActiveTimeScale, 0.001f);

        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_GAME_OVER);
        Assert.AreEqual(0.8f, TimeScaleManager.ActiveTimeScale, 0.001f);
    }
}
