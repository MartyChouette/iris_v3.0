using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for grade computation in DateEndScreen.ComputeGrade (affection)
/// and DateOutcomeCapture's flower grade (via CaptureFlowerResult).
/// Tests exact boundary values for all grade thresholds.
/// </summary>
public class GradeComputationTests
{
    private List<Object> _tempSOs;

    [SetUp]
    public void SetUp()
    {
        _tempSOs = new List<Object>();
        DateOutcomeCapture.ClearForNewDay();
    }

    [TearDown]
    public void TearDown()
    {
        DateOutcomeCapture.ClearForNewDay();
        foreach (var so in _tempSOs)
            if (so != null) Object.DestroyImmediate(so);
    }

    // ─────────────────────────────────────────────────────────────
    // Affection Grades (DateEndScreen.ComputeGrade)
    // Thresholds: ≥90→S, ≥75→A, ≥60→B, ≥40→C, <40→D
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void AffectionGrade_100_IsS() => Assert.AreEqual("S", DateEndScreen.ComputeGrade(100f));

    [Test]
    public void AffectionGrade_90_IsS() => Assert.AreEqual("S", DateEndScreen.ComputeGrade(90f));

    [Test]
    public void AffectionGrade_89_IsA() => Assert.AreEqual("A", DateEndScreen.ComputeGrade(89.9f));

    [Test]
    public void AffectionGrade_75_IsA() => Assert.AreEqual("A", DateEndScreen.ComputeGrade(75f));

    [Test]
    public void AffectionGrade_74_IsB() => Assert.AreEqual("B", DateEndScreen.ComputeGrade(74.9f));

    [Test]
    public void AffectionGrade_60_IsB() => Assert.AreEqual("B", DateEndScreen.ComputeGrade(60f));

    [Test]
    public void AffectionGrade_59_IsC() => Assert.AreEqual("C", DateEndScreen.ComputeGrade(59.9f));

    [Test]
    public void AffectionGrade_40_IsC() => Assert.AreEqual("C", DateEndScreen.ComputeGrade(40f));

    [Test]
    public void AffectionGrade_39_IsD() => Assert.AreEqual("D", DateEndScreen.ComputeGrade(39.9f));

    [Test]
    public void AffectionGrade_0_IsD() => Assert.AreEqual("D", DateEndScreen.ComputeGrade(0f));

    // ─────────────────────────────────────────────────────────────
    // Flower Grades (DateOutcomeCapture.CaptureFlowerResult → ComputeFlowerGrade)
    // Thresholds: ≥90→S, ≥75→A, ≥60→B, ≥40→C, <40→D
    // ─────────────────────────────────────────────────────────────

    private string GetFlowerGrade(int score)
    {
        // Capture a dummy date first so CaptureFlowerResult has an outcome to update
        var date = ScriptableObject.CreateInstance<DatePersonalDefinition>();
        _tempSOs.Add(date);
        DateOutcomeCapture.Capture(date, 50f, true,
            new List<DateSessionManager.AccumulatedReaction>());

        DateOutcomeCapture.CaptureFlowerResult(score, 5, false);
        return DateOutcomeCapture.LastOutcome.flowerGrade;
    }

    [Test]
    public void FlowerGrade_95_IsS() => Assert.AreEqual("S", GetFlowerGrade(95));

    [Test]
    public void FlowerGrade_90_IsS() => Assert.AreEqual("S", GetFlowerGrade(90));

    [Test]
    public void FlowerGrade_89_IsA() => Assert.AreEqual("A", GetFlowerGrade(89));

    [Test]
    public void FlowerGrade_75_IsA() => Assert.AreEqual("A", GetFlowerGrade(75));

    [Test]
    public void FlowerGrade_74_IsB() => Assert.AreEqual("B", GetFlowerGrade(74));

    [Test]
    public void FlowerGrade_60_IsB() => Assert.AreEqual("B", GetFlowerGrade(60));

    [Test]
    public void FlowerGrade_59_IsC() => Assert.AreEqual("C", GetFlowerGrade(59));

    [Test]
    public void FlowerGrade_40_IsC() => Assert.AreEqual("C", GetFlowerGrade(40));

    [Test]
    public void FlowerGrade_39_IsD() => Assert.AreEqual("D", GetFlowerGrade(39));

    [Test]
    public void FlowerGrade_0_IsD() => Assert.AreEqual("D", GetFlowerGrade(0));
}
