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
    public void ComputeSlotGameIndices_GameCountOne_AllSlotsSameIndex()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 1, visibleSlotCount: 5);
        Assert.That(result, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
    }

    [Test]
    public void ComputeSlotGameIndices_GameCountTwo_ProducesWraparoundDuplication()
    {
        var result = GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex: 0, gameCount: 2, visibleSlotCount: 5);
        // offsets -2..2 against 2 games: (0-2)%2=0, (0-1)%2=1, 0, 1, (0+2)%2=0 — same game at both far edges.
        Assert.That(result, Is.EqualTo(new[] { 0, 1, 0, 1, 0 }));
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

    // ---- ComputeBoxArtRect ----

    [Test]
    public void ComputeBoxArtRect_WideSlot_LetterboxesToAspectRatio()
    {
        var slot = new SDL.Rect { X = 0, Y = 0, W = 400, H = 100 };
        var art = GameCarouselRenderer.ComputeBoxArtRect(slot);
        Assert.That(art.H, Is.EqualTo(100));
        Assert.That(art.W, Is.LessThanOrEqualTo(slot.W));
    }

    [Test]
    public void ComputeBoxArtRect_TallSlot_PillarboxesToAspectRatio()
    {
        var slot = new SDL.Rect { X = 0, Y = 0, W = 100, H = 400 };
        var art = GameCarouselRenderer.ComputeBoxArtRect(slot);
        Assert.That(art.W, Is.EqualTo(100));
        Assert.That(art.H, Is.LessThanOrEqualTo(slot.H));
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
}
