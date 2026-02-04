using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for FlowerGameBrain.EvaluateFlower() scoring logic.
/// Covers stem length, cut angle, part evaluation, game-over conditions, and edge cases.
/// Run via Window > General > Test Runner (Edit Mode tab).
/// </summary>
public class FlowerGameBrainTests
{
    private GameObject _brainGO;
    private FlowerGameBrain _brain;
    private IdealFlowerDefinition _ideal;

    // Stem mock objects
    private GameObject _stemGO;
    private FlowerStemRuntime _stem;
    private Transform _anchor;
    private Transform _tip;
    private Transform _cutNormalRef;

    [SetUp]
    public void SetUp()
    {
        _brainGO = new GameObject("TestBrain");
        _brain = _brainGO.AddComponent<FlowerGameBrain>();
        _ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        _brain.ideal = _ideal;

        // Set up stem with measurable transforms
        _stemGO = new GameObject("TestStem");
        _stem = _stemGO.AddComponent<FlowerStemRuntime>();

        var anchorGO = new GameObject("Anchor");
        _anchor = anchorGO.transform;
        _anchor.SetParent(_stemGO.transform);

        var tipGO = new GameObject("Tip");
        _tip = tipGO.transform;
        _tip.SetParent(_stemGO.transform);

        var cutRefGO = new GameObject("CutNormalRef");
        _cutNormalRef = cutRefGO.transform;
        _cutNormalRef.SetParent(_stemGO.transform);

        _stem.StemAnchor = _anchor;
        _stem.StemTip = _tip;
        _stem.cutNormalRef = _cutNormalRef;

        _brain.stem = _stem;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_stemGO);
        Object.DestroyImmediate(_brainGO);
        Object.DestroyImmediate(_ideal);
    }

    // ─────────────────────────────────────────────────────────────
    // Helper: set stem length by positioning anchor and tip
    // ─────────────────────────────────────────────────────────────

    private void SetStemLength(float length)
    {
        _anchor.position = Vector3.zero;
        _tip.position = new Vector3(0f, -length, 0f);
    }

    private void SetCutAngle(float angleDeg)
    {
        // Point cutNormalRef so that GetCurrentCutAngleDeg(Vector3.up) returns angleDeg
        // Vector3.Angle measures the unsigned angle between the transformed referenceAxisLocal and Vector3.up
        // referenceAxisLocal defaults to Vector3.up, so we rotate cutNormalRef to produce the desired angle
        _cutNormalRef.rotation = Quaternion.AngleAxis(angleDeg, Vector3.forward);
    }

    private FlowerPartRuntime AddPart(string partId, FlowerPartKind kind,
        FlowerPartCondition condition = FlowerPartCondition.Normal, bool attached = true)
    {
        var go = new GameObject($"Part_{partId}");
        go.transform.SetParent(_brainGO.transform);
        var part = go.AddComponent<FlowerPartRuntime>();
        part.PartId = partId;
        part.kind = kind;
        part.condition = condition;
        part.isAttached = attached;

        _brain.parts.Add(part);
        return part;
    }

    private IdealFlowerDefinition.PartRule AddPartRule(string partId, FlowerPartKind kind,
        FlowerPartCondition idealCondition = FlowerPartCondition.Normal,
        bool canCauseGameOver = false, bool isSpecial = false,
        bool contributesToScore = true, bool allowedWithered = true,
        bool allowedMissing = false, float scoreWeight = 1f)
    {
        var rule = new IdealFlowerDefinition.PartRule
        {
            partId = partId,
            kind = kind,
            idealCondition = idealCondition,
            canCauseGameOver = canCauseGameOver,
            isSpecial = isSpecial,
            contributesToScore = contributesToScore,
            allowedWithered = allowedWithered,
            allowedMissing = allowedMissing,
            scoreWeight = scoreWeight
        };
        _ideal.partRules.Add(rule);
        return rule;
    }

    // ─────────────────────────────────────────────────────────────
    // Stem Length Tests
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void StemLength_PerfectLength_ScoresOne()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.cutAngleContributesToScore = false;

        SetStemLength(1.0f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(1f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void StemLength_HalfDelta_ScoresHalf()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.cutAngleContributesToScore = false;

        // Delta = 0.25, half of hardFailDelta 0.5 → score = 1 - 0.25/0.5 = 0.5
        SetStemLength(1.25f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(0.5f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void StemLength_AtHardFail_ScoresZero()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.stemCanCauseGameOver = true;
        _ideal.cutAngleContributesToScore = false;

        // Delta = 0.5 = hardFailDelta → score = 0, but NOT game over (not >)
        SetStemLength(1.5f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void StemLength_BeyondHardFail_CausesGameOver_CutTooLow()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.stemCanCauseGameOver = true;
        _ideal.cutAngleContributesToScore = false;

        // Current > ideal → "Stem cut too low"
        SetStemLength(1.6f);

        var result = _brain.EvaluateFlower();

        Assert.IsTrue(result.isGameOver);
        Assert.That(result.gameOverReason, Does.Contain("too low"));
    }

    [Test]
    public void StemLength_BeyondHardFail_CausesGameOver_CutTooHigh()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.stemCanCauseGameOver = true;
        _ideal.cutAngleContributesToScore = false;

        // Current < ideal → "Crown decapitated"
        SetStemLength(0.4f);

        var result = _brain.EvaluateFlower();

        Assert.IsTrue(result.isGameOver);
        Assert.That(result.gameOverReason, Does.Contain("decapitated"));
    }

    [Test]
    public void StemLength_BeyondHardFail_NoGameOverWhenDisabled()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.stemCanCauseGameOver = false; // disabled
        _ideal.cutAngleContributesToScore = false;

        SetStemLength(2.0f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
    }

    [Test]
    public void StemLength_DisabledScoring_NoContribution()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        SetStemLength(999f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    // ─────────────────────────────────────────────────────────────
    // Cut Angle Tests
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void CutAngle_PerfectAngle_ScoresOne()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = true;
        _ideal.idealCutAngleDeg = 45f;
        _ideal.cutAngleHardFailDelta = 20f;
        _ideal.cutAngleScoreWeight = 1.0f;

        SetCutAngle(45f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(1f, result.scoreNormalized, 0.05f);
    }

    [Test]
    public void CutAngle_BeyondHardFail_CausesGameOver()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = true;
        _ideal.cutAngleCanCauseGameOver = true;
        _ideal.idealCutAngleDeg = 45f;
        _ideal.cutAngleHardFailDelta = 20f;
        _ideal.cutAngleScoreWeight = 1.0f;

        SetCutAngle(90f); // way off

        var result = _brain.EvaluateFlower();

        Assert.IsTrue(result.isGameOver);
        Assert.That(result.gameOverReason, Does.Contain("angle"));
    }

    [Test]
    public void CutAngle_BeyondHardFail_NoGameOverWhenDisabled()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = true;
        _ideal.cutAngleCanCauseGameOver = false; // disabled
        _ideal.idealCutAngleDeg = 45f;
        _ideal.cutAngleHardFailDelta = 20f;
        _ideal.cutAngleScoreWeight = 1.0f;

        SetCutAngle(90f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
    }

    // ─────────────────────────────────────────────────────────────
    // Part Scoring Tests
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void Part_AttachedMatchingIdeal_ScoresOne()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: true);
        AddPartRule("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(1f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void Part_DetachedNotAllowed_ScoresZero()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: false);
        AddPartRule("leaf1", FlowerPartKind.Leaf, allowedMissing: false, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void Part_DetachedAllowed_ScoresHalf()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: false);
        AddPartRule("leaf1", FlowerPartKind.Leaf, allowedMissing: true, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0.5f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void Part_WitheredAllowed_ScoresHalf()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Withered, attached: true);
        AddPartRule("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal,
            allowedWithered: true, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0.5f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void Part_WitheredNotAllowed_ScoresLow()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Withered, attached: true);
        AddPartRule("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal,
            allowedWithered: false, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0.2f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void Part_ConditionMismatch_ScoresLow()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        // Part is Normal but ideal expects Perfect
        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: true);
        AddPartRule("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Perfect, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0.2f, result.scoreNormalized, 0.001f);
    }

    // ─────────────────────────────────────────────────────────────
    // Game Over Conditions
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void GameOver_CrownRemoved()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("crown", FlowerPartKind.Crown, attached: false);
        AddPartRule("crown", FlowerPartKind.Crown, canCauseGameOver: true, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.IsTrue(result.isGameOver);
        Assert.That(result.gameOverReason, Does.Contain("Crown"));
    }

    [Test]
    public void GameOver_SpecialPartRemoved()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("special_leaf", FlowerPartKind.Leaf, attached: false);
        AddPartRule("special_leaf", FlowerPartKind.Leaf, isSpecial: true, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        Assert.IsTrue(result.isGameOver);
        Assert.That(result.gameOverReason, Does.Contain("special"));
    }

    [Test]
    public void GameOver_ScoreStillComputedOnFailure()
    {
        // Even on game over, scoreNormalized should reflect actual trim quality
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 0.5f;
        _ideal.stemScoreWeight = 1.0f;
        _ideal.stemContributesToScore = true;
        _ideal.stemCanCauseGameOver = true;
        _ideal.cutAngleContributesToScore = false;

        // Delta = 0.6, beyond hard fail. Score = max(0, 1 - 0.6/0.5) = 0
        SetStemLength(1.6f);

        var result = _brain.EvaluateFlower();

        Assert.IsTrue(result.isGameOver);
        // Score is clamped to 0, not left undefined
        Assert.GreaterOrEqual(result.scoreNormalized, 0f);
        Assert.LessOrEqual(result.scoreNormalized, 1f);
    }

    // ─────────────────────────────────────────────────────────────
    // Weighted Average Tests
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void WeightedAverage_StemAndParts()
    {
        _ideal.idealStemLength = 1.0f;
        _ideal.stemHardFailDelta = 1.0f;
        _ideal.stemScoreWeight = 0.3f;
        _ideal.stemContributesToScore = true;
        _ideal.stemCanCauseGameOver = false;
        _ideal.cutAngleContributesToScore = false;

        // Stem: perfect → score 1.0
        SetStemLength(1.0f);

        // Part: detached, not allowed → score 0.0
        AddPart("leaf1", FlowerPartKind.Leaf, attached: false);
        AddPartRule("leaf1", FlowerPartKind.Leaf, allowedMissing: false, scoreWeight: 0.7f);

        // Expected: (1.0 * 0.3 + 0.0 * 0.7) / (0.3 + 0.7) = 0.3
        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0.3f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void WeightedAverage_MultiplePartsEqualWeight()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        // Two parts, equal weight: one perfect (1.0), one missing not allowed (0.0)
        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: true);
        AddPartRule("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, scoreWeight: 1f);

        AddPart("leaf2", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: false);
        AddPartRule("leaf2", FlowerPartKind.Leaf, allowedMissing: false, scoreWeight: 1f);

        // Expected: (1.0 * 1 + 0.0 * 1) / (1 + 1) = 0.5
        var result = _brain.EvaluateFlower();

        Assert.AreEqual(0.5f, result.scoreNormalized, 0.001f);
    }

    // ─────────────────────────────────────────────────────────────
    // Edge Cases
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EdgeCase_NoScoringContributors_ScoresZero()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;
        // No parts added

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void EdgeCase_NullStem_SkipsStemAndAngle()
    {
        _brain.stem = null;

        _ideal.stemContributesToScore = true;
        _ideal.cutAngleContributesToScore = true;

        // Should not throw, just skip stem/angle scoring
        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void EdgeCase_NullIdeal_SkipsAllScoring()
    {
        _brain.ideal = null;

        var result = _brain.EvaluateFlower();

        Assert.IsFalse(result.isGameOver);
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void EdgeCase_PartRuleWithNoMatchingRuntime_TreatedAsMissing()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        // Rule exists but no runtime part matches
        AddPartRule("ghost_leaf", FlowerPartKind.Leaf, allowedMissing: false, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        // Missing + not allowed = 0
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }

    [Test]
    public void EdgeCase_NonScoringPart_DoesNotAffectScore()
    {
        _ideal.stemContributesToScore = false;
        _ideal.cutAngleContributesToScore = false;

        AddPart("leaf1", FlowerPartKind.Leaf, FlowerPartCondition.Normal, attached: true);
        AddPartRule("leaf1", FlowerPartKind.Leaf, contributesToScore: false, scoreWeight: 1f);

        var result = _brain.EvaluateFlower();

        // No scoring contributors → 0
        Assert.AreEqual(0f, result.scoreNormalized, 0.001f);
    }
}
