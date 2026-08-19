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
}
