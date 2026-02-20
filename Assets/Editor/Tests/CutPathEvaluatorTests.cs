using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for CutPathEvaluator.PointInPolygon() static method.
/// Pure geometry — no GameObjects needed.
/// </summary>
public class CutPathEvaluatorTests
{
    // Unit square: (0,0) → (1,0) → (1,1) → (0,1)
    private static readonly List<Vector2> UnitSquare = new List<Vector2>
    {
        new Vector2(0f, 0f),
        new Vector2(1f, 0f),
        new Vector2(1f, 1f),
        new Vector2(0f, 1f)
    };

    // Triangle: (0,0) → (2,0) → (1,2)
    private static readonly List<Vector2> Triangle = new List<Vector2>
    {
        new Vector2(0f, 0f),
        new Vector2(2f, 0f),
        new Vector2(1f, 2f)
    };

    // Concave L-shape:
    //   (0,0) → (2,0) → (2,1) → (1,1) → (1,2) → (0,2)
    private static readonly List<Vector2> LShape = new List<Vector2>
    {
        new Vector2(0f, 0f),
        new Vector2(2f, 0f),
        new Vector2(2f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 2f),
        new Vector2(0f, 2f)
    };

    [Test]
    public void Square_CenterIsInside()
    {
        Assert.IsTrue(CutPathEvaluator.PointInPolygon(new Vector2(0.5f, 0.5f), UnitSquare));
    }

    [Test]
    public void Square_FarPointIsOutside()
    {
        Assert.IsFalse(CutPathEvaluator.PointInPolygon(new Vector2(5f, 5f), UnitSquare));
    }

    [Test]
    public void Square_NegativePointIsOutside()
    {
        Assert.IsFalse(CutPathEvaluator.PointInPolygon(new Vector2(-1f, 0.5f), UnitSquare));
    }

    [Test]
    public void Triangle_InsidePoint()
    {
        Assert.IsTrue(CutPathEvaluator.PointInPolygon(new Vector2(1f, 0.5f), Triangle));
    }

    [Test]
    public void Triangle_OutsidePoint()
    {
        Assert.IsFalse(CutPathEvaluator.PointInPolygon(new Vector2(2f, 2f), Triangle));
    }

    [Test]
    public void LShape_InsideBottomArm()
    {
        // (1.5, 0.5) is in the bottom-right arm of the L
        Assert.IsTrue(CutPathEvaluator.PointInPolygon(new Vector2(1.5f, 0.5f), LShape));
    }

    [Test]
    public void LShape_InsideTopArm()
    {
        // (0.5, 1.5) is in the top-left arm of the L
        Assert.IsTrue(CutPathEvaluator.PointInPolygon(new Vector2(0.5f, 1.5f), LShape));
    }

    [Test]
    public void LShape_InConcavity_IsOutside()
    {
        // (1.5, 1.5) is in the concave notch — should be outside
        Assert.IsFalse(CutPathEvaluator.PointInPolygon(new Vector2(1.5f, 1.5f), LShape));
    }

    [Test]
    public void EmptyPolygon_ReturnsFalse()
    {
        var empty = new List<Vector2>();
        Assert.IsFalse(CutPathEvaluator.PointInPolygon(new Vector2(0f, 0f), empty));
    }

    [Test]
    public void TwoPointPolygon_ReturnsFalse()
    {
        var line = new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 1f)
        };
        Assert.IsFalse(CutPathEvaluator.PointInPolygon(new Vector2(0.5f, 0.5f), line));
    }
}
