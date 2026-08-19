using NEShim.UI.Controls;
using SDL3;

namespace NEShim.Tests.UI.Controls;

/// <summary>
/// Tests for SliderControl.ComputeGeometry — pure arithmetic, no SDL init required. Relocated
/// from MainMenuRendererTests.cs when the geometry moved out of MainMenuRenderer into this
/// shared component; MenuRenderer previously computed the same geometry inline with no test
/// coverage of its own, so this now covers both menus for the first time.
/// </summary>
[TestFixture]
internal class SliderControlTests
{
    // itemRect = {X=100, Y=50, W=400, H=42}, labelColumnW=80. Expected values below are derived
    // from the same formula as the implementation (ItemTextIndent=13, valueW=48, valuePad=5,
    // barGap=6, barH=8, all * scale) — worked out by hand so a regression in the formula itself
    // would still be caught.
    private static readonly SDL.Rect SliderItemRect = new() { X = 100, Y = 50, W = 400, H = 42 };

    [Test]
    public void ComputeGeometry_AtDefaultScale_ComputesExpectedRects()
    {
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 80, scale: 1f);

        Assert.That(geo.LabelRect, Is.EqualTo(new SDL.FRect { X = 113, Y = 50, W = 80, H = 42 }));
        Assert.That(geo.BarRect,   Is.EqualTo(new SDL.FRect { X = 199, Y = 67, W = 242, H = 8 }));
        Assert.That(geo.ValueRect, Is.EqualTo(new SDL.FRect { X = 447, Y = 50, W = 48, H = 42 }));
    }

    [Test]
    public void ComputeGeometry_ValueRect_StaysPaddedFromContentRightEdge()
    {
        // Regression test for the "value number hugging the panel's right edge" fix: the value
        // rect's right edge must sit exactly valuePad (5 * scale) short of the item's own right
        // edge, at any scale.
        const float scale = 1.75f;
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 80, scale: scale);

        float expectedRightEdge = SliderItemRect.X + SliderItemRect.W - 5f * scale;
        Assert.That(geo.ValueRect.X + geo.ValueRect.W, Is.EqualTo(expectedRightEdge).Within(0.01f));
    }

    [Test]
    public void ComputeGeometry_AtDoubleScale_ScalesSpacingProportionally()
    {
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 80, scale: 2f);

        Assert.That(geo.BarRect.H, Is.EqualTo(16f)); // barH = 8 * 2
        Assert.That(geo.ValueRect.W, Is.EqualTo(96f)); // valueW = 48 * 2
    }

    [Test]
    public void ComputeGeometry_WhenLabelColumnTooWide_BarWidthGoesNegative()
    {
        // labelColumnW (350) leaves no room for the bar once value width/pad/gaps are
        // subtracted — BarRect.W goes negative, which SliderControl.Draw must check for before
        // filling it (drawing is untested here; this only verifies the geometry itself).
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 350, scale: 1f);
        Assert.That(geo.BarRect.W, Is.LessThan(0f));
    }

    [Test]
    public void ComputeGeometry_WhenBarWidthNegative_ValueXIgnoresIt()
    {
        // valueX must clamp the negative bar width to 0 rather than shifting the value rect
        // left of the bar's own starting position.
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 350, scale: 1f);
        Assert.That(geo.ValueRect.X, Is.EqualTo(geo.BarRect.X + 6f)); // barX + barGap (bar width clamped to 0)
    }

    [Test]
    public void ComputeGeometry_LabelRect_StartsAtUnscaledIndent()
    {
        // ItemTextIndent (13f) is deliberately not scaled — matches the original per-renderer
        // constant's usage, unlike most other spacing values in this formula.
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 80, scale: 3f);
        Assert.That(geo.LabelRect.X, Is.EqualTo(SliderItemRect.X + 13f));
    }

    [Test]
    public void ComputeGeometry_BarRect_IsVerticallyCenteredInItemRect()
    {
        var geo = SliderControl.ComputeGeometry(SliderItemRect, labelColumnW: 80, scale: 1f);
        float expectedCenterY = SliderItemRect.Y + SliderItemRect.H / 2f;
        float barCenterY = geo.BarRect.Y + geo.BarRect.H / 2f;
        Assert.That(barCenterY, Is.EqualTo(expectedCenterY).Within(0.01f));
    }
}
