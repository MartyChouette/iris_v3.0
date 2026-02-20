using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for DateOutcomeCapture static bridge.
/// Covers Capture, flower grade computation, drink detection, and ClearForNewDay.
/// </summary>
public class DateOutcomeCaptureTests
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

    private DatePersonalDefinition MakeDate(string name)
    {
        var d = ScriptableObject.CreateInstance<DatePersonalDefinition>();
        d.characterName = name;
        _tempSOs.Add(d);
        return d;
    }

    private List<DateSessionManager.AccumulatedReaction> MakeReactions(params string[] itemNames)
    {
        var list = new List<DateSessionManager.AccumulatedReaction>();
        foreach (var n in itemNames)
            list.Add(new DateSessionManager.AccumulatedReaction { itemName = n, type = ReactionType.Like });
        return list;
    }

    [Test]
    public void Fresh_HadDateIsFalse()
    {
        Assert.IsFalse(DateOutcomeCapture.LastOutcome.hadDate);
    }

    [Test]
    public void Capture_PopulatesFields()
    {
        var date = MakeDate("Livii");
        var reactions = MakeReactions("vinyl", "plant");

        DateOutcomeCapture.Capture(date, 85f, true, reactions);

        var o = DateOutcomeCapture.LastOutcome;
        Assert.IsTrue(o.hadDate);
        Assert.AreEqual("Livii", o.characterName);
        Assert.AreEqual(85f, o.affection, 0.001f);
        Assert.IsTrue(o.succeeded);
        Assert.AreEqual(2, o.reactionTags.Length);
        Assert.AreEqual("vinyl", o.reactionTags[0]);
    }

    [Test]
    public void Capture_DetectsDrinkInItemName()
    {
        var date = MakeDate("Sterling");
        var reactions = MakeReactions("coffee_drink", "vinyl");

        DateOutcomeCapture.Capture(date, 50f, false, reactions);
        Assert.IsTrue(DateOutcomeCapture.LastOutcome.drinkServed);
    }

    [Test]
    public void Capture_DrinkDetectionCaseInsensitive()
    {
        var date = MakeDate("Sterling");
        var reactions = MakeReactions("DRINK_special");

        DateOutcomeCapture.Capture(date, 50f, false, reactions);
        Assert.IsTrue(DateOutcomeCapture.LastOutcome.drinkServed);
    }

    [Test]
    public void Capture_NoDrinkInReactions()
    {
        var date = MakeDate("Sage");
        var reactions = MakeReactions("vinyl", "plant");

        DateOutcomeCapture.Capture(date, 70f, true, reactions);
        Assert.IsFalse(DateOutcomeCapture.LastOutcome.drinkServed);
    }

    [Test]
    public void CaptureFlowerResult_PopulatesFlowerFields()
    {
        var date = MakeDate("Clover");
        DateOutcomeCapture.Capture(date, 60f, true, MakeReactions());

        DateOutcomeCapture.CaptureFlowerResult(82, 5, false);

        var o = DateOutcomeCapture.LastOutcome;
        Assert.IsTrue(o.hadFlowerTrim);
        Assert.AreEqual(82, o.flowerScore);
        Assert.AreEqual(5, o.flowerDaysAlive);
        Assert.AreEqual("A", o.flowerGrade); // 82 >= 75 → A
        Assert.IsFalse(o.flowerWasGameOver);
    }

    [Test]
    public void CaptureFlowerResult_GameOverFlag()
    {
        var date = MakeDate("Livii");
        DateOutcomeCapture.Capture(date, 40f, false, MakeReactions());

        DateOutcomeCapture.CaptureFlowerResult(20, 1, true);

        Assert.IsTrue(DateOutcomeCapture.LastOutcome.flowerWasGameOver);
        Assert.AreEqual("D", DateOutcomeCapture.LastOutcome.flowerGrade); // 20 < 40 → D
    }

    [Test]
    public void ClearForNewDay_ResetsAll()
    {
        var date = MakeDate("Livii");
        DateOutcomeCapture.Capture(date, 90f, true, MakeReactions("vinyl"));
        DateOutcomeCapture.CaptureFlowerResult(95, 7, false);

        DateOutcomeCapture.ClearForNewDay();

        var o = DateOutcomeCapture.LastOutcome;
        Assert.IsFalse(o.hadDate);
        Assert.IsFalse(o.hadFlowerTrim);
        Assert.AreEqual(0f, o.affection, 0.001f);
        Assert.IsNull(o.reactionTags);
    }
}
