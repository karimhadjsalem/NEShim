using NEShim.UI.Controls;
using SDL3;

namespace NEShim.Tests.UI.Controls;

/// <summary>
/// Tests for IconRowControl.ComputeLayout — pure arithmetic, no SDL init required. This geometry
/// was previously inline inside each renderer's private DrawItemWithIcon with no test coverage
/// of its own.
/// </summary>
[TestFixture]
internal class IconRowControlTests
{
    private static readonly SDL.Rect ItemRect = new() { X = 100, Y = 50, W = 400, H = 36 };

    [Test]
    public void ComputeLayout_AtDefaultScale_ComputesExpectedRects()
    {
        var layout = IconRowControl.ComputeLayout(ItemRect, scale: 1f);

        Assert.That(layout.IconRect, Is.EqualTo(new SDL.Rect { X = 102, Y = 61, W = 20, H = 14 }));
        Assert.That(layout.TextRect, Is.EqualTo(new SDL.FRect { X = 124, Y = 50, W = 376, H = 36 }));
    }

    [Test]
    public void ComputeLayout_AtDoubleScale_ScalesIconAndOffsetProportionally()
    {
        var layout = IconRowControl.ComputeLayout(ItemRect, scale: 2f);

        Assert.That(layout.IconRect, Is.EqualTo(new SDL.Rect { X = 104, Y = 54, W = 40, H = 28 }));
        Assert.That(layout.TextRect.X, Is.EqualTo(148));
    }

    [Test]
    public void ComputeLayout_IconRect_IsVerticallyCenteredInItemRect()
    {
        var layout = IconRowControl.ComputeLayout(ItemRect, scale: 1f);
        float itemCenterY = ItemRect.Y + ItemRect.H / 2f;
        float iconCenterY = layout.IconRect.Y + layout.IconRect.H / 2f;
        Assert.That(iconCenterY, Is.EqualTo(itemCenterY).Within(1f));
    }

    [Test]
    public void ComputeLayout_TextRect_NeverOverlapsIcon()
    {
        var layout = IconRowControl.ComputeLayout(ItemRect, scale: 1f);
        Assert.That(layout.TextRect.X, Is.GreaterThanOrEqualTo(layout.IconRect.X + layout.IconRect.W));
    }
}
