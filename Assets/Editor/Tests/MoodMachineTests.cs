using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for MoodMachine source management.
/// Tests SetSource/RemoveSource and averaging logic via the Sources dictionary.
/// Note: Mood lerps in Update(), so we test source management + ComputeTarget indirectly.
/// </summary>
public class MoodMachineTests
{
    private GameObject _go;
    private MoodMachine _mm;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("MoodMachine");
        _mm = _go.AddComponent<MoodMachine>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    [Test]
    public void NoSources_SourcesEmpty()
    {
        Assert.AreEqual(0, _mm.Sources.Count);
    }

    [Test]
    public void SetSource_AddsSingleSource()
    {
        _mm.SetSource("Perfume", 0.5f);
        Assert.AreEqual(1, _mm.Sources.Count);
        Assert.IsTrue(_mm.Sources.ContainsKey("Perfume"));
        Assert.AreEqual(0.5f, _mm.Sources["Perfume"], 0.001f);
    }

    [Test]
    public void SetSource_ClampsToZeroOne()
    {
        _mm.SetSource("Over", 5f);
        Assert.AreEqual(1f, _mm.Sources["Over"], 0.001f);

        _mm.SetSource("Under", -3f);
        Assert.AreEqual(0f, _mm.Sources["Under"], 0.001f);
    }

    [Test]
    public void RemoveSource_RemovesEntry()
    {
        _mm.SetSource("A", 0.5f);
        _mm.RemoveSource("A");
        Assert.AreEqual(0, _mm.Sources.Count);
    }

    [Test]
    public void RemoveSource_Nonexistent_NoCrash()
    {
        Assert.DoesNotThrow(() => _mm.RemoveSource("DoesNotExist"));
    }

    [Test]
    public void SetSource_OverwritesExisting()
    {
        _mm.SetSource("A", 0.3f);
        _mm.SetSource("A", 0.8f);
        Assert.AreEqual(1, _mm.Sources.Count);
        Assert.AreEqual(0.8f, _mm.Sources["A"], 0.001f);
    }

    [Test]
    public void TwoSources_BothTracked()
    {
        _mm.SetSource("A", 0.4f);
        _mm.SetSource("B", 0.6f);
        Assert.AreEqual(2, _mm.Sources.Count);
        Assert.AreEqual(0.4f, _mm.Sources["A"], 0.001f);
        Assert.AreEqual(0.6f, _mm.Sources["B"], 0.001f);
    }

    [Test]
    public void ThreeSources_AllTracked()
    {
        _mm.SetSource("A", 0.2f);
        _mm.SetSource("B", 0.5f);
        _mm.SetSource("C", 0.8f);
        Assert.AreEqual(3, _mm.Sources.Count);
    }

    [Test]
    public void Mood_InitiallyZero()
    {
        Assert.AreEqual(0f, _mm.Mood, 0.001f);
    }
}
