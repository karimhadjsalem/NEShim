using NEShim.UI;
using SDL3;

namespace NEShim.Tests.UI;

[TestFixture]
internal class GameCarouselRendererTests
{
    // ---- ComputeSlotGameIndices ----

    [Test]
    public void ComputeSlotGameIndices_GameCountZero_ReturnsEmpty()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 0, visibleSlotCount: 5);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ComputeSlotGameIndices_GameCountExceedsVisibleSlots_NoDuplicates()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 2, gameCount: 10, visibleSlotCount: 5);
        Assert.That(result, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        Assert.That(result.Distinct().Count(), Is.EqualTo(5));
    }

    [Test]
    public void ComputeSlotGameIndices_SelectedIndexAlwaysCenterSlot()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 7, gameCount: 10, visibleSlotCount: 5);
        Assert.That(result[2], Is.EqualTo(7));
    }

    // ---- ComputeSlotGameIndices: small-library edge cases ----
    // A library smaller than visibleSlotCount wraps around freely to fill the strip — the same
    // neighbor legitimately shows up on both sides of the centered game, since it really is
    // reachable in either direction (that's what makes it read as a carousel). The one game that
    // must never repeat in a non-center slot is the centered/highlighted one itself. The sole
    // exception is a single game, where there's nothing else to wrap to at all.

    [Test]
    public void ComputeSlotGameIndices_OneGame_OnlyCenterFilled_EverythingElseEmpty()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 1, visibleSlotCount: 5);
        Assert.That(result, Is.EqualTo(new[]
        {
            GameCarouselRenderer.NoGame, GameCarouselRenderer.NoGame,
            0,
            GameCarouselRenderer.NoGame, GameCarouselRenderer.NoGame,
        }));
    }

    [Test]
    public void ComputeSlotGameIndices_OneGame_WiderSlideSlotCount_OnlyCenterFilled()
    {
        // Same guarantee must hold for DrawSliding's wider 7-slot pass, not just the 5-slot
        // static filmstrip.
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 1, visibleSlotCount: 7);
        Assert.That(result.Count(i => i != GameCarouselRenderer.NoGame), Is.EqualTo(1));
        Assert.That(result[3], Is.EqualTo(0)); // center of a 7-wide array is index 3
    }

    [Test]
    public void ComputeSlotGameIndices_TwoGames_OtherGameShownOnBothSides()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 2, visibleSlotCount: 5);
        // Center (index 2) is the selected game (0); the only other game (1) is reachable by
        // going either left or right, so it appears immediately on both sides. The outer slots
        // (distance 2) would wrap back to the centered game, so those stay empty instead of
        // repeating it.
        Assert.That(result, Is.EqualTo(new[]
        {
            GameCarouselRenderer.NoGame, 1,
            0,
            1, GameCarouselRenderer.NoGame,
        }));
    }

    [Test]
    public void ComputeSlotGameIndices_TwoGames_CenterGameNeverRepeats()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 2, visibleSlotCount: 5);
        Assert.That(result.Count(i => i == 0), Is.EqualTo(1)); // centered game shown exactly once
        Assert.That(result.Count(i => i == 1), Is.EqualTo(2)); // the other game, on both sides
    }

    [Test]
    public void ComputeSlotGameIndices_TwoGames_OtherGameSelected_StillCenterOnlyOnce()
    {
        // Same guarantee regardless of which of the two games is currently highlighted.
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 1, gameCount: 2, visibleSlotCount: 5);
        Assert.That(result[2], Is.EqualTo(1)); // center is always the selected game
        Assert.That(result.Count(i => i == 1), Is.EqualTo(1));
        Assert.That(result.Count(i => i == 0), Is.EqualTo(2));
    }

    [Test]
    public void ComputeSlotGameIndices_TwoGames_WiderSlideSlotCount_CenterStillOnlyOnce()
    {
        // With a strip wider than the library, the non-center game keeps wrapping in to fill
        // every remaining slot except the two that would land back on the centered game.
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 2, visibleSlotCount: 7);
        Assert.That(result.Count(i => i == 0), Is.EqualTo(1));
        Assert.That(result.Count(i => i == GameCarouselRenderer.NoGame), Is.EqualTo(2));
        Assert.That(result.Count(i => i == 1), Is.EqualTo(4));
    }

    [Test]
    public void ComputeSlotGameIndices_ThreeGames_AllThreeShown_NoGaps()
    {
        // 3 games wrapped into 5 slots: both neighbors are distinct from the centered game and
        // from each other, but with 5 slots to fill from only 3 games, the two farthest-out slots
        // wrap back around and repeat a neighbor rather than sitting empty.
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 3, visibleSlotCount: 5);
        Assert.That(result, Has.None.EqualTo(GameCarouselRenderer.NoGame));
        Assert.That(result.Count(i => i == 0), Is.EqualTo(1)); // centered game shown exactly once
    }

    [Test]
    public void ComputeSlotGameIndices_FourGames_CenterGameNeverRepeats()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 4, visibleSlotCount: 5);
        Assert.That(result, Has.None.EqualTo(GameCarouselRenderer.NoGame));
        Assert.That(result.Count(i => i == 0), Is.EqualTo(1)); // centered game shown exactly once
    }

    [Test]
    public void ComputeSlotGameIndices_FiveGames_ExactlyFillsAllSlots_NoDuplicatesOrGaps()
    {
        // The boundary where the small-library fix and the plain modulo case meet: exactly
        // enough games to fill every slot, so no NoGame sentinel should appear at all.
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 5, visibleSlotCount: 5);
        Assert.That(result, Has.None.EqualTo(GameCarouselRenderer.NoGame));
        Assert.That(result.Distinct().Count(), Is.EqualTo(5));
    }

    // ---- ComputeBoxArtRect ----

    [Test]
    public void ComputeBoxArtRect_WideShortSlot_PillarboxesToAspectRatio()
    {
        // A wide/short slot can't fit a full-height portrait box art without overflowing its
        // width, so height is the binding constraint — width shrinks, leaving empty space on
        // the sides (pillarboxing).
        var slot = new SDL.Rect { X = 0, Y = 0, W = 400, H = 100 };
        var art = GameCarouselRenderer.ComputeBoxArtRect(slot);
        Assert.That(art.H, Is.EqualTo(100));
        Assert.That(art.W, Is.LessThanOrEqualTo(slot.W));
    }

    [Test]
    public void ComputeBoxArtRect_NarrowTallSlot_LetterboxesToAspectRatio()
    {
        // A narrow/tall slot has more height available than a portrait box art needs at full
        // width, so width is the binding constraint — height shrinks, leaving empty space above
        // and below (letterboxing).
        var slot = new SDL.Rect { X = 0, Y = 0, W = 100, H = 400 };
        var art = GameCarouselRenderer.ComputeBoxArtRect(slot);
        Assert.That(art.W, Is.EqualTo(100));
        Assert.That(art.H, Is.LessThanOrEqualTo(slot.H));
    }

    [Test]
    public void ComputeBoxArtRect_SquareSlot_ResultIsPortrait()
    {
        // Regression guard: a real NES box is taller than it is wide (like a book on a shelf) —
        // BoxArtAspectRatio must be applied as Height:Width, not Width:Height, or box art
        // renders sideways/landscape instead of matching the real box's proportions.
        var slot = new SDL.Rect { X = 0, Y = 0, W = 200, H = 200 };
        var art = GameCarouselRenderer.ComputeBoxArtRect(slot);
        Assert.That(art.H, Is.GreaterThan(art.W));
    }

    [Test]
    public void ComputeBoxArtRect_ResultIsCenteredWithinSlot()
    {
        var slot = new SDL.Rect { X = 10, Y = 20, W = 400, H = 100 };
        var art = GameCarouselRenderer.ComputeBoxArtRect(slot);
        int leftGap = art.X - slot.X;
        int rightGap = (slot.X + slot.W) - (art.X + art.W);
        Assert.That(leftGap, Is.EqualTo(rightGap).Within(1));
    }

    // ---- ComputeFlipVisual ----

    [Test]
    public void ComputeFlipVisual_ProgressZero_ShowsStartFace_ScaleOne()
    {
        var (scale, showFront) = GameCarouselRenderer.ComputeFlipVisual(0f, startWithFront: true);
        Assert.That(scale, Is.EqualTo(1f).Within(0.001f));
        Assert.That(showFront, Is.True);
    }

    [Test]
    public void ComputeFlipVisual_ProgressHalf_ScaleNearZero()
    {
        var (scale, _) = GameCarouselRenderer.ComputeFlipVisual(0.5f, startWithFront: true);
        Assert.That(scale, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ComputeFlipVisual_ProgressOne_ShowsEndFace_ScaleOne()
    {
        var (scale, showFront) = GameCarouselRenderer.ComputeFlipVisual(1f, startWithFront: true);
        Assert.That(scale, Is.EqualTo(1f).Within(0.001f));
        Assert.That(showFront, Is.False);
    }

    [Test]
    public void ComputeFlipVisual_StartWithFrontFalse_ReversesFaceOrder()
    {
        var atStart = GameCarouselRenderer.ComputeFlipVisual(0f, startWithFront: false);
        var atEnd   = GameCarouselRenderer.ComputeFlipVisual(1f, startWithFront: false);
        Assert.That(atStart.showFront, Is.False);
        Assert.That(atEnd.showFront, Is.True);
    }

    // ---- EaseInOut ----

    [Test]
    public void EaseInOut_Zero_ReturnsZero()
    {
        Assert.That(GameCarouselRenderer.EaseInOut(0f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void EaseInOut_One_ReturnsOne()
    {
        Assert.That(GameCarouselRenderer.EaseInOut(1f), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void EaseInOut_Midpoint_ReturnsHalf()
    {
        Assert.That(GameCarouselRenderer.EaseInOut(0.5f), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void EaseInOut_EarlyProgress_LagsBehindLinear()
    {
        // Ease-in-out starts slower than linear — the defining property that makes it feel smooth.
        Assert.That(GameCarouselRenderer.EaseInOut(0.25f), Is.LessThan(0.25f));
    }

    // ---- InterpolateByOffset ----

    [Test]
    public void InterpolateByOffset_OffsetZero_ReturnsFirstAnchor()
    {
        var anchors = new[] { 1f, 0.75f, 0.55f };
        Assert.That(GameCarouselRenderer.InterpolateByOffset(anchors, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void InterpolateByOffset_IntegerOffset_ReturnsExactAnchor()
    {
        var anchors = new[] { 1f, 0.75f, 0.55f };
        Assert.That(GameCarouselRenderer.InterpolateByOffset(anchors, 1f), Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(GameCarouselRenderer.InterpolateByOffset(anchors, 2f), Is.EqualTo(0.55f).Within(0.001f));
    }

    [Test]
    public void InterpolateByOffset_FractionalOffset_InterpolatesBetweenAnchors()
    {
        var anchors = new[] { 1f, 0.75f, 0.55f };
        Assert.That(GameCarouselRenderer.InterpolateByOffset(anchors, 0.5f), Is.EqualTo(0.875f).Within(0.001f));
    }

    [Test]
    public void InterpolateByOffset_BeyondLastAnchor_ClampsToLastValue()
    {
        var anchors = new[] { 1f, 0.75f, 0.55f };
        Assert.That(GameCarouselRenderer.InterpolateByOffset(anchors, 5f), Is.EqualTo(0.55f).Within(0.001f));
    }

    // ---- ScaledTitlePtSize ----

    [Test]
    public void ScaledTitlePtSize_FullScale_ReturnsBaseSize()
    {
        Assert.That(GameCarouselRenderer.ScaledTitlePtSize(1f), Is.EqualTo(15f).Within(0.01f));
    }

    [Test]
    public void ScaledTitlePtSize_AboveFloor_ScalesProportionally()
    {
        // 15 * 0.8 = 12, comfortably above the 4pt crash-guard floor, so this exercises the
        // proportional path rather than the floor clamp (see
        // ScaledTitlePtSize_NearZeroScale_FlooredToMinimum). Non-center tiles are deliberately
        // allowed to shrink to illegible — only the centered tile is guaranteed a fixed,
        // full-size textScale (see GameCarouselRenderer.DrawSlotAt), so this floor exists purely
        // to avoid a zero/negative point size, not to preserve readability.
        Assert.That(GameCarouselRenderer.ScaledTitlePtSize(0.8f), Is.EqualTo(12f).Within(0.01f));
    }

    [Test]
    public void ScaledTitlePtSize_NearZeroScale_FlooredToMinimum()
    {
        // Without a floor, a far-offset tile's title would shrink to a zero/negative point size.
        Assert.That(GameCarouselRenderer.ScaledTitlePtSize(0.01f), Is.EqualTo(4f).Within(0.01f));
    }

    [Test]
    public void ScaledTitlePtSize_SmallerScale_ReturnsSmallerSize()
    {
        float far = GameCarouselRenderer.ScaledTitlePtSize(0.55f);
        float near = GameCarouselRenderer.ScaledTitlePtSize(0.75f);
        float center = GameCarouselRenderer.ScaledTitlePtSize(1f);
        Assert.That(far, Is.LessThan(near));
        Assert.That(near, Is.LessThan(center));
    }

    // ---- ScaledPtSize (shared by title, placeholder glyph, and invalid-overlay text) ----

    [Test]
    public void ScaledPtSize_FullScale_ReturnsBaseSize()
    {
        Assert.That(GameCarouselRenderer.ScaledPtSize(10f, 1f), Is.EqualTo(10f).Within(0.01f));
    }

    [Test]
    public void ScaledPtSize_HalfScale_ScalesProportionally()
    {
        Assert.That(GameCarouselRenderer.ScaledPtSize(10f, 0.5f), Is.EqualTo(5f).Within(0.01f));
    }

    [Test]
    public void ScaledPtSize_NearZeroScale_FlooredToMinimum()
    {
        Assert.That(GameCarouselRenderer.ScaledPtSize(10f, 0.01f), Is.EqualTo(4f).Within(0.01f));
    }

    [Test]
    public void ScaledTitlePtSize_DelegatesToScaledPtSize_WithTitleBaseSize()
    {
        // ScaledTitlePtSize is a thin wrapper — this pins that relationship so the two can't
        // silently drift apart (e.g. one gets the floor tweaked and the other doesn't).
        Assert.That(GameCarouselRenderer.ScaledTitlePtSize(0.7f),
            Is.EqualTo(GameCarouselRenderer.ScaledPtSize(15f, 0.7f)).Within(0.001f));
    }

    // ---- DrawTitle wrap decision (indirectly, via the underlying width comparison) ----
    // DrawTitle itself does SDL text measurement and drawing, so it isn't unit-testable in
    // isolation without an SDL context — see the "no Draw() tests" convention for this file.
    // The wrap threshold constant is exercised functionally via manual/smoke testing.
}
