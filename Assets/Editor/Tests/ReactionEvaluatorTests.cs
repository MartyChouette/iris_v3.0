using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for ReactionEvaluator static utility.
/// Covers tag matching, drink scoring, outfit eval, clutter, cleanliness, and mood range.
/// </summary>
public class ReactionEvaluatorTests
{
    private List<GameObject> _tempGOs;
    private List<Object> _tempSOs;

    [SetUp]
    public void SetUp()
    {
        _tempGOs = new List<GameObject>();
        _tempSOs = new List<Object>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _tempGOs)
            if (go != null) Object.DestroyImmediate(go);
        foreach (var so in _tempSOs)
            if (so != null) Object.DestroyImmediate(so);
    }

    private ReactableTag MakeTag(params string[] tags)
    {
        var go = new GameObject("ReactableTag");
        _tempGOs.Add(go);
        var rt = go.AddComponent<ReactableTag>();

        var so = new UnityEditor.SerializedObject(rt);
        var tagsProp = so.FindProperty("tags");
        tagsProp.arraySize = tags.Length;
        for (int i = 0; i < tags.Length; i++)
            tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        return rt;
    }

    private DatePreferences MakePrefs(
        string[] liked = null, string[] disliked = null,
        float moodMin = 0.2f, float moodMax = 0.5f)
    {
        return new DatePreferences
        {
            likedTags = liked ?? new string[0],
            dislikedTags = disliked ?? new string[0],
            preferredMoodMin = moodMin,
            preferredMoodMax = moodMax,
            likedDrinks = new DrinkRecipeDefinition[0],
            dislikedDrinks = new DrinkRecipeDefinition[0],
            likedOutfitTags = new string[0],
            dislikedOutfitTags = new string[0]
        };
    }

    private DrinkRecipeDefinition MakeDrink(string name)
    {
        var d = ScriptableObject.CreateInstance<DrinkRecipeDefinition>();
        d.drinkName = name;
        _tempSOs.Add(d);
        return d;
    }

    private OutfitDefinition MakeOutfit(params string[] styleTags)
    {
        var o = ScriptableObject.CreateInstance<OutfitDefinition>();
        o.styleTags = styleTags;
        _tempSOs.Add(o);
        return o;
    }

    // ─────────────────────────────────────────────────────────────
    // EvaluateReactable
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EvaluateReactable_NullTag_ReturnsNeutral()
    {
        var prefs = MakePrefs(liked: new[] { "vinyl" });
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateReactable(null, prefs));
    }

    [Test]
    public void EvaluateReactable_NullPrefs_ReturnsNeutral()
    {
        var tag = MakeTag("vinyl");
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateReactable(tag, null));
    }

    [Test]
    public void EvaluateReactable_LikedTag_ReturnsLike()
    {
        var tag = MakeTag("vinyl");
        var prefs = MakePrefs(liked: new[] { "vinyl" });
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateReactable(tag, prefs));
    }

    [Test]
    public void EvaluateReactable_DislikedTag_ReturnsDislike()
    {
        var tag = MakeTag("trash");
        var prefs = MakePrefs(disliked: new[] { "trash" });
        Assert.AreEqual(ReactionType.Dislike, ReactionEvaluator.EvaluateReactable(tag, prefs));
    }

    [Test]
    public void EvaluateReactable_NoMatch_ReturnsNeutral()
    {
        var tag = MakeTag("plant");
        var prefs = MakePrefs(liked: new[] { "vinyl" }, disliked: new[] { "trash" });
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateReactable(tag, prefs));
    }

    [Test]
    public void EvaluateReactable_CaseInsensitive()
    {
        var tag = MakeTag("VINYL");
        var prefs = MakePrefs(liked: new[] { "vinyl" });
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateReactable(tag, prefs));
    }

    [Test]
    public void EvaluateReactable_LikedWinsOverDisliked()
    {
        var tag = MakeTag("ambiguous");
        var prefs = MakePrefs(liked: new[] { "ambiguous" }, disliked: new[] { "ambiguous" });
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateReactable(tag, prefs));
    }

    // ─────────────────────────────────────────────────────────────
    // EvaluateDrink
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EvaluateDrink_NullRecipe_ReturnsNeutral()
    {
        var prefs = MakePrefs();
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateDrink(null, 100, prefs));
    }

    [Test]
    public void EvaluateDrink_LikedAndScoreAbove60_ReturnsLike()
    {
        var drink = MakeDrink("Gin");
        var prefs = MakePrefs();
        prefs.likedDrinks = new[] { drink };
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateDrink(drink, 60, prefs));
    }

    [Test]
    public void EvaluateDrink_LikedButScoreBelow60_FallsThrough()
    {
        var drink = MakeDrink("Gin");
        var prefs = MakePrefs();
        prefs.likedDrinks = new[] { drink };
        // Score 59 < 60 so liked check fails, not disliked, score < 80 → Neutral
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateDrink(drink, 59, prefs));
    }

    [Test]
    public void EvaluateDrink_DislikedDrink_ReturnsDislike()
    {
        var drink = MakeDrink("Bitter Tonic");
        var prefs = MakePrefs();
        prefs.dislikedDrinks = new[] { drink };
        Assert.AreEqual(ReactionType.Dislike, ReactionEvaluator.EvaluateDrink(drink, 100, prefs));
    }

    [Test]
    public void EvaluateDrink_HighScoreAnyDrink_ReturnsLike()
    {
        var drink = MakeDrink("Unknown");
        var prefs = MakePrefs();
        // Not liked, not disliked, but score >= 80
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateDrink(drink, 80, prefs));
    }

    [Test]
    public void EvaluateDrink_MediumScoreUnknown_ReturnsNeutral()
    {
        var drink = MakeDrink("Meh");
        var prefs = MakePrefs();
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateDrink(drink, 50, prefs));
    }

    // ─────────────────────────────────────────────────────────────
    // EvaluateOutfit
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EvaluateOutfit_NullOutfit_ReturnsNeutral()
    {
        var prefs = MakePrefs();
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateOutfit(null, prefs));
    }

    [Test]
    public void EvaluateOutfit_LikedStyle_ReturnsLike()
    {
        var outfit = MakeOutfit("formal");
        var prefs = MakePrefs();
        prefs.likedOutfitTags = new[] { "formal" };
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateOutfit(outfit, prefs));
    }

    [Test]
    public void EvaluateOutfit_DislikedStyle_ReturnsDislike()
    {
        var outfit = MakeOutfit("edgy");
        var prefs = MakePrefs();
        prefs.dislikedOutfitTags = new[] { "edgy" };
        Assert.AreEqual(ReactionType.Dislike, ReactionEvaluator.EvaluateOutfit(outfit, prefs));
    }

    [Test]
    public void EvaluateOutfit_NoMatch_ReturnsNeutral()
    {
        var outfit = MakeOutfit("casual");
        var prefs = MakePrefs();
        prefs.likedOutfitTags = new[] { "formal" };
        prefs.dislikedOutfitTags = new[] { "edgy" };
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateOutfit(outfit, prefs));
    }

    // ─────────────────────────────────────────────────────────────
    // EvaluateClutter
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EvaluateClutter_AboveTolerance_ReturnsLike()
    {
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateClutter(0.8f, 0.7f));
    }

    [Test]
    public void EvaluateClutter_AtHalfTolerance_ReturnsNeutral()
    {
        // tolerance = 0.8, half = 0.4, score = 0.4 → exactly at half → Neutral
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateClutter(0.4f, 0.8f));
    }

    [Test]
    public void EvaluateClutter_BelowHalfTolerance_ReturnsDislike()
    {
        // tolerance = 0.8, half = 0.4, score = 0.3 → Dislike
        Assert.AreEqual(ReactionType.Dislike, ReactionEvaluator.EvaluateClutter(0.3f, 0.8f));
    }

    // ─────────────────────────────────────────────────────────────
    // EvaluateCleanliness
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EvaluateCleanliness_AtPoint8_ReturnsLike()
    {
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateCleanliness(0.8f));
    }

    [Test]
    public void EvaluateCleanliness_JustBelowPoint8_ReturnsNeutral()
    {
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateCleanliness(0.79f));
    }

    [Test]
    public void EvaluateCleanliness_AtPoint5_ReturnsNeutral()
    {
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateCleanliness(0.5f));
    }

    [Test]
    public void EvaluateCleanliness_BelowPoint5_ReturnsDislike()
    {
        Assert.AreEqual(ReactionType.Dislike, ReactionEvaluator.EvaluateCleanliness(0.49f));
    }

    // ─────────────────────────────────────────────────────────────
    // EvaluateMood
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void EvaluateMood_InRange_ReturnsLike()
    {
        var prefs = MakePrefs(moodMin: 0.3f, moodMax: 0.6f);
        Assert.AreEqual(ReactionType.Like, ReactionEvaluator.EvaluateMood(0.45f, prefs));
    }

    [Test]
    public void EvaluateMood_SlightlyOutOfRange_ReturnsNeutral()
    {
        // range 0.3-0.6, mood=0.7 → dist to max = 0.1 ≤ 0.3 → Neutral
        var prefs = MakePrefs(moodMin: 0.3f, moodMax: 0.6f);
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateMood(0.7f, prefs));
    }

    [Test]
    public void EvaluateMood_FarOutOfRange_ReturnsDislike()
    {
        // range 0.3-0.6, mood=1.0 → dist to max = 0.4 > 0.3 → Dislike
        var prefs = MakePrefs(moodMin: 0.3f, moodMax: 0.6f);
        Assert.AreEqual(ReactionType.Dislike, ReactionEvaluator.EvaluateMood(1.0f, prefs));
    }

    [Test]
    public void EvaluateMood_NullPrefs_ReturnsNeutral()
    {
        Assert.AreEqual(ReactionType.Neutral, ReactionEvaluator.EvaluateMood(0.5f, null));
    }
}
