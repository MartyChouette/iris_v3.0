using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Edit-mode unit tests for DateHistory static registry.
/// Covers Record, HasSucceeded, GetLatestEntry, UpdateFlowerResult, and save/load.
/// </summary>
public class DateHistoryTests
{
    [SetUp]
    public void SetUp()
    {
        DateHistory.LoadFrom(new List<DateHistory.DateHistoryEntry>());
    }

    [TearDown]
    public void TearDown()
    {
        DateHistory.LoadFrom(new List<DateHistory.DateHistoryEntry>());
    }

    [Test]
    public void Fresh_EntriesEmpty()
    {
        Assert.AreEqual(0, DateHistory.Entries.Count);
    }

    [Test]
    public void Record_IncreasesCount()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry { name = "Livii", day = 1 });
        Assert.AreEqual(1, DateHistory.Entries.Count);
    }

    [Test]
    public void Record_EntryIsRetrievable()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = "Sterling", day = 2, affection = 75f, grade = "A", succeeded = true
        });

        var entry = DateHistory.GetLatestEntry("Sterling");
        Assert.IsNotNull(entry);
        Assert.AreEqual("Sterling", entry.name);
        Assert.AreEqual(75f, entry.affection, 0.001f);
        Assert.AreEqual("A", entry.grade);
    }

    [Test]
    public void HasSucceeded_TrueForSucceeded()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = "Sage", day = 1, succeeded = true
        });
        Assert.IsTrue(DateHistory.HasSucceeded("Sage"));
    }

    [Test]
    public void HasSucceeded_FalseForFailed()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = "Clover", day = 1, succeeded = false
        });
        Assert.IsFalse(DateHistory.HasSucceeded("Clover"));
    }

    [Test]
    public void HasSucceeded_FalseForUnknown()
    {
        Assert.IsFalse(DateHistory.HasSucceeded("Nobody"));
    }

    [Test]
    public void GetLatestEntry_ReturnsNewestMatch()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = "Livii", day = 1, grade = "C"
        });
        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = "Livii", day = 3, grade = "A"
        });

        var entry = DateHistory.GetLatestEntry("Livii");
        Assert.AreEqual("A", entry.grade);
        Assert.AreEqual(3, entry.day);
    }

    [Test]
    public void GetLatestEntry_NullForUnknown()
    {
        Assert.IsNull(DateHistory.GetLatestEntry("Nobody"));
    }

    [Test]
    public void UpdateFlowerResult_PopulatesLastEntry()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry { name = "Livii", day = 1 });
        DateHistory.UpdateFlowerResult(82, 5, "A");

        var entry = DateHistory.Entries[0];
        Assert.AreEqual(82, entry.flowerScore);
        Assert.AreEqual(5, entry.flowerDaysAlive);
        Assert.AreEqual("A", entry.flowerGrade);
    }

    [Test]
    public void UpdateFlowerResult_EmptyHistory_NoException()
    {
        Assert.DoesNotThrow(() => DateHistory.UpdateFlowerResult(50, 3, "B"));
    }

    [Test]
    public void GetAllForSave_ReturnsIndependentCopy()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry { name = "Livii", day = 1 });
        var copy = DateHistory.GetAllForSave();
        Assert.AreEqual(1, copy.Count);

        copy.Clear();
        Assert.AreEqual(1, DateHistory.Entries.Count, "Clearing copy should not affect original");
    }

    [Test]
    public void LoadFrom_ReplacesAll()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry { name = "OldEntry", day = 1 });

        var newEntries = new List<DateHistory.DateHistoryEntry>
        {
            new DateHistory.DateHistoryEntry { name = "NewEntry", day = 5 }
        };
        DateHistory.LoadFrom(newEntries);

        Assert.AreEqual(1, DateHistory.Entries.Count);
        Assert.AreEqual("NewEntry", DateHistory.Entries[0].name);
    }

    [Test]
    public void LoadFrom_Null_ClearsAll()
    {
        DateHistory.Record(new DateHistory.DateHistoryEntry { name = "A", day = 1 });
        DateHistory.LoadFrom(null);
        Assert.AreEqual(0, DateHistory.Entries.Count);
    }
}
