using SDL3;
using NEShim.UI;

namespace NEShim.Tests.UI;

/// <summary>
/// Tests for MainMenuRenderer.GetMainPanelRect (panel positioning — pure arithmetic, no SDL init required).
/// </summary>
[TestFixture]
internal class MainMenuRendererTests
{
    // All tests use bounds (0,0,800,600), panelW=300, panelH=200.
    // Margin = 40 (private const in MainMenuRenderer)

    private static readonly SDL.Rect Bounds800x600 = new() { X = 0, Y = 0, W = 800, H = 600 };
    private const int PanelW = 300;
    private const int PanelH = 200;
    private const int Margin = 40;

    [Test]
    public void GetMainPanelRect_BottomCenter_X_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomCenter");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
    }

    [Test]
    public void GetMainPanelRect_BottomCenter_Y_IsNearBottom()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomCenter");
        int expectedY = 600 - PanelH - 600 / 6;
        Assert.That(rect.Y, Is.EqualTo(expectedY));
    }

    [Test]
    public void GetMainPanelRect_Center_X_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "Center");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
    }

    [Test]
    public void GetMainPanelRect_Center_Y_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "Center");
        Assert.That(rect.Y, Is.EqualTo((600 - PanelH) / 2));
    }

    [Test]
    public void GetMainPanelRect_BottomLeft_X_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomLeft");
        Assert.That(rect.X, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_BottomLeft_Y_IsNearBottom()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomLeft");
        int expectedY = 600 - PanelH - 600 / 6;
        Assert.That(rect.Y, Is.EqualTo(expectedY));
    }

    [Test]
    public void GetMainPanelRect_BottomRight_X_IsNearRightEdge()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomRight");
        Assert.That(rect.X, Is.EqualTo(800 - PanelW - Margin));
    }

    [Test]
    public void GetMainPanelRect_TopLeft_X_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopLeft");
        Assert.That(rect.X, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_TopLeft_Y_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopLeft");
        Assert.That(rect.Y, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_TopCenter_X_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopCenter");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
    }

    [Test]
    public void GetMainPanelRect_TopCenter_Y_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopCenter");
        Assert.That(rect.Y, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_TopRight_X_IsNearRightEdge()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopRight");
        Assert.That(rect.X, Is.EqualTo(800 - PanelW - Margin));
    }

    [Test]
    public void GetMainPanelRect_TopRight_Y_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopRight");
        Assert.That(rect.Y, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_AlwaysPreservesSuppliedDimensions()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomCenter");
        Assert.That(rect.W, Is.EqualTo(PanelW));
        Assert.That(rect.H, Is.EqualTo(PanelH));
    }

    [Test]
    public void GetMainPanelRect_NarrowBounds_X_ClampedToEight()
    {
        // panelW > bounds.W → centered X is negative → clamped to 8
        var tinyBounds = new SDL.Rect { X = 0, Y = 0, W = 50, H = 600 };
        var rect = MainMenuRenderer.GetMainPanelRect(tinyBounds, PanelW, PanelH, "Center");
        Assert.That(rect.X, Is.EqualTo(8));
    }

    [Test]
    public void GetMainPanelRect_ShortBounds_Y_ClampedToEight()
    {
        // panelH > bounds.H → centered Y is negative → clamped to 8
        var tinyBounds = new SDL.Rect { X = 0, Y = 0, W = 800, H = 50 };
        var rect = MainMenuRenderer.GetMainPanelRect(tinyBounds, PanelW, PanelH, "Center");
        Assert.That(rect.Y, Is.EqualTo(8));
    }

    [Test]
    public void GetMainPanelRect_UnknownPosition_DefaultsToCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "Mystery");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
        Assert.That(rect.Y, Is.EqualTo((600 - PanelH) / 2));
    }

    // ---- ComputeSliderGeometry ----
    // itemRect = {X=100, Y=50, W=400, H=42}, labelColumnW=80. Expected values below are derived
    // from the same formula as the implementation (ItemTextIndent=13, valueW=48, valuePad=5,
    // barGap=6, barH=8, all * scale) — worked out by hand so a regression in the formula itself
    // would still be caught.

    private static readonly SDL.Rect SliderItemRect = new() { X = 100, Y = 50, W = 400, H = 42 };

    [Test]
    public void ComputeSliderGeometry_AtDefaultScale_ComputesExpectedRects()
    {
        var geo = MainMenuRenderer.ComputeSliderGeometry(SliderItemRect, labelColumnW: 80, scale: 1f);

        Assert.That(geo.LabelRect, Is.EqualTo(new SDL.FRect { X = 113, Y = 50, W = 80, H = 42 }));
        Assert.That(geo.BarRect,   Is.EqualTo(new SDL.FRect { X = 199, Y = 67, W = 242, H = 8 }));
        Assert.That(geo.ValueRect, Is.EqualTo(new SDL.FRect { X = 447, Y = 50, W = 48, H = 42 }));
    }

    [Test]
    public void ComputeSliderGeometry_ValueRect_StaysPaddedFromContentRightEdge()
    {
        // Regression test for the "value number hugging the panel's right edge" fix: the value
        // rect's right edge must sit exactly valuePad (5 * scale) short of the item's own right
        // edge, at any scale.
        const float scale = 1.75f;
        var geo = MainMenuRenderer.ComputeSliderGeometry(SliderItemRect, labelColumnW: 80, scale: scale);

        float expectedRightEdge = SliderItemRect.X + SliderItemRect.W - 5f * scale;
        Assert.That(geo.ValueRect.X + geo.ValueRect.W, Is.EqualTo(expectedRightEdge).Within(0.01f));
    }

    [Test]
    public void ComputeSliderGeometry_AtDoubleScale_ScalesSpacingProportionally()
    {
        var geo = MainMenuRenderer.ComputeSliderGeometry(SliderItemRect, labelColumnW: 80, scale: 2f);

        Assert.That(geo.BarRect.H, Is.EqualTo(16f)); // barH = 8 * 2
        Assert.That(geo.ValueRect.W, Is.EqualTo(96f)); // valueW = 48 * 2
    }

    [Test]
    public void ComputeSliderGeometry_WhenLabelColumnTooWide_BarWidthGoesNegative()
    {
        // labelColumnW (350) leaves no room for the bar once value width/pad/gaps are
        // subtracted — BarRect.W goes negative, which DrawSliderItem must check for before
        // filling it (drawing is untested here; this only verifies the geometry itself).
        var geo = MainMenuRenderer.ComputeSliderGeometry(SliderItemRect, labelColumnW: 350, scale: 1f);
        Assert.That(geo.BarRect.W, Is.LessThan(0f));
    }

    [Test]
    public void ComputeSliderGeometry_WhenBarWidthNegative_ValueXIgnoresIt()
    {
        // valueX must clamp the negative bar width to 0 rather than shifting the value rect
        // left of the bar's own starting position.
        var geo = MainMenuRenderer.ComputeSliderGeometry(SliderItemRect, labelColumnW: 350, scale: 1f);
        Assert.That(geo.ValueRect.X, Is.EqualTo(geo.BarRect.X + 6f)); // barX + barGap (bar width clamped to 0)
    }
}
