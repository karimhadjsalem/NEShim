using NEShim.UI.Controls;
using SDL3;

namespace NEShim.Tests.UI.Controls;

/// <summary>
/// Tests for ControllerDiagramControl.ComputeLayout — pure arithmetic, no SDL init required.
/// This aspect-fit + label-gap math was previously inline inside each renderer's private
/// DrawControllerSprite (byte-identical in both files) with no test coverage of its own.
/// </summary>
[TestFixture]
internal class ControllerDiagramControlTests
{
    // Width-constrained: area wide enough that height (area.W / 2.43) fits within area.H,
    // leaving a gap above the sprite tall enough to show the label.
    private static readonly SDL.FRect WideArea = new() { X = 0, Y = 0, W = 243, H = 200 };

    [Test]
    public void ComputeLayout_WidthConstrained_FitsSpriteByWidth()
    {
        var layout = ControllerDiagramControl.ComputeLayout(WideArea, scale: 1f);
        Assert.That(layout.ControllerRect, Is.EqualTo(new SDL.Rect { X = 0, Y = 50, W = 243, H = 100 }));
    }

    [Test]
    public void ComputeLayout_WidthConstrained_LabelShownWhenGapWideEnough()
    {
        var layout = ControllerDiagramControl.ComputeLayout(WideArea, scale: 1f);
        Assert.That(layout.ShowLabel, Is.True);
        Assert.That(layout.LabelRect, Is.EqualTo(new SDL.FRect { X = 0, Y = 0, W = 243, H = 50 }));
    }

    [Test]
    public void ComputeLayout_LabelFontSize_CapsAtFourteen()
    {
        // labelGap (50) * 0.75 = 37.5, above the 14f cap, so the cap wins.
        var layout = ControllerDiagramControl.ComputeLayout(WideArea, scale: 1f);
        Assert.That(layout.LabelFontSize, Is.EqualTo(14f));
    }

    [Test]
    public void ComputeLayout_LabelFontSize_ScalesWithMenuScale()
    {
        var layout = ControllerDiagramControl.ComputeLayout(WideArea, scale: 1.5f);
        Assert.That(layout.LabelFontSize, Is.EqualTo(21f)); // 14 * 1.5
    }

    [Test]
    public void ComputeLayout_HeightConstrained_ClampsToAreaHeightAndHidesLabel()
    {
        // Height (80) shorter than the width-derived fit (100), so height becomes the binding
        // constraint and there's no vertical gap left for a label.
        var shortArea = new SDL.FRect { X = 0, Y = 0, W = 243, H = 80 };
        var layout = ControllerDiagramControl.ComputeLayout(shortArea, scale: 1f);

        Assert.That(layout.ControllerRect.H, Is.EqualTo(80));
        Assert.That(layout.ShowLabel, Is.False);
    }

    [Test]
    public void ComputeLayout_HeightConstrained_SpriteHorizontallyCentered()
    {
        var shortArea = new SDL.FRect { X = 0, Y = 0, W = 243, H = 80 };
        var layout = ControllerDiagramControl.ComputeLayout(shortArea, scale: 1f);

        // ctrlW = 80 * 2.43 = 194.4 -> ox = (243 - 194.4) * 0.5 = 24.3 -> (int) 24
        Assert.That(layout.ControllerRect.X, Is.EqualTo(24));
        Assert.That(layout.ControllerRect.W, Is.EqualTo(194));
    }
}
