using NEShim.UI.Controls;
using SDL3;

namespace NEShim.Tests.UI.Controls;

/// <summary>
/// Tests for ItemRowControl.ComputeLayout — pure arithmetic, no SDL init required. This geometry
/// (text/glyph rect split, tab-stop, accent bar) was previously inline inside each renderer's
/// private DrawItemRow with no test coverage of its own; extracting it into a shared component
/// makes it independently testable for the first time.
/// </summary>
[TestFixture]
internal class ItemRowControlTests
{
    private static readonly SDL.Rect ItemRect = new() { X = 100, Y = 50, W = 400, H = 36 };

    [Test]
    public void ComputeLayout_NoValueIcon_TextRectSpansRemainingWidthPastIndent()
    {
        var layout = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: false);
        Assert.That(layout.TextRect, Is.EqualTo(new SDL.FRect { X = 113, Y = 50, W = 387, H = 36 }));
    }

    [Test]
    public void ComputeLayout_NoValueIcon_GlyphRectIsDefault()
    {
        var layout = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: false);
        Assert.That(layout.GlyphRect, Is.EqualTo(default(SDL.Rect)));
    }

    [Test]
    public void ComputeLayout_WithValueIcon_TextRectWidthEqualsTabStop()
    {
        var layout = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: true);
        Assert.That(layout.TextRect, Is.EqualTo(new SDL.FRect { X = 113, Y = 50, W = 120, H = 36 }));
    }

    [Test]
    public void ComputeLayout_WithValueIcon_GlyphRectPositionedAfterLabelColumn()
    {
        var layout = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: true);
        Assert.That(layout.GlyphRect, Is.EqualTo(new SDL.Rect { X = 233, Y = 59, W = 18, H = 18 }));
    }

    [Test]
    public void ComputeLayout_AccentBarRect_SameRegardlessOfValueIcon()
    {
        var withoutIcon = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: false);
        var withIcon    = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: true);

        Assert.That(withIcon.AccentBarRect, Is.EqualTo(withoutIcon.AccentBarRect));
        Assert.That(withoutIcon.AccentBarRect, Is.EqualTo(new SDL.FRect { X = 104, Y = 55, W = 3, H = 26 }));
    }

    [Test]
    public void ComputeLayout_AtDoubleScale_TabStopAndGlyphSizeScale()
    {
        var layout = ItemRowControl.ComputeLayout(ItemRect, scale: 2f, hasValueIcon: true);
        Assert.That(layout.TabStop, Is.EqualTo(240f));
        Assert.That(layout.GlyphRect.W, Is.EqualTo(36));
        Assert.That(layout.GlyphRect.H, Is.EqualTo(36));
    }

    [Test]
    public void ComputeLayout_GlyphRect_IsVerticallyCenteredInItemRect()
    {
        var layout = ItemRowControl.ComputeLayout(ItemRect, scale: 1f, hasValueIcon: true);
        float itemCenterY  = ItemRect.Y + ItemRect.H / 2f;
        float glyphCenterY = layout.GlyphRect.Y + layout.GlyphRect.H / 2f;
        Assert.That(glyphCenterY, Is.EqualTo(itemCenterY).Within(1f)); // integer rounding
    }
}
