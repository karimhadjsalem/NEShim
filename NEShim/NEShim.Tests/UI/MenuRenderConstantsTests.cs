using NEShim.Platform;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class MenuRenderConstantsTests
{
    // MenuScale.Scale is a static, process-global mutable field (see MenuScaleTests) — reset it
    // to the reference resolution (Scale == 1.0) after every test so later tests in the suite
    // aren't left with a polluted value.
    [TearDown]
    public void TearDown() => MenuScale.UpdateViewport(1024, 672);

    // ---- PanelW: non-Steam-Deck environment — fixed width regardless of MenuScale.Scale ----
    // Deliberate: narrower panels (Settings, and anything else using SlimPanelW/MainPanelMaxW/
    // RebindPanelMaxW/DisconnectPanelW) are meant to stay visibly skinnier than the wider
    // binding-screen panels at any resolution — see ScaledPanelW for the one case that should
    // grow with resolution (the binding screens' controller diagram column).

    [Test]
    public void PanelW_WhenNotSteamDeck_ReturnsBaseW()
    {
        // SteamDeck env var is never set in the test environment so IsSteamDeck = false.
        Assert.That(MenuRenderConstants.PanelW(520, 1280), Is.EqualTo(520));
    }

    [Test]
    public void PanelW_WhenNotSteamDeck_IgnoresMenuScale()
    {
        MenuScale.UpdateViewport(2048, 1344); // Scale == 2.0
        Assert.That(MenuRenderConstants.PanelW(480, 4000), Is.EqualTo(480));
    }

    [Test]
    public void PanelW_WhenNotSteamDeck_IgnoresViewportW()
    {
        Assert.That(MenuRenderConstants.PanelW(480, 500), Is.EqualTo(480));
    }

    // ---- ScaledPanelW: always scales with MenuScale.Scale, on every platform ----

    [Test]
    public void ScaledPanelW_AtDefaultScale_ReturnsBaseW()
    {
        Assert.That(MenuRenderConstants.ScaledPanelW(520, 1280), Is.EqualTo(520));
    }

    [Test]
    public void ScaledPanelW_AtDefaultScale_ClampsToViewportMinusMargin()
    {
        // baseW (480) exceeds viewportW - 60 (440), so the clamp applies even at Scale == 1.0.
        Assert.That(MenuRenderConstants.ScaledPanelW(480, 500), Is.EqualTo(440));
    }

    [Test]
    public void ScaledPanelW_AtDoubleScale_ScalesUpProportionally()
    {
        MenuScale.UpdateViewport(2048, 1344); // double the 1024x672 reference -> Scale == 2.0
        Assert.That(MenuRenderConstants.ScaledPanelW(260, 4000), Is.EqualTo(520));
    }

    [Test]
    public void ScaledPanelW_AtLargeScale_StillClampsToViewportMinusMargin()
    {
        MenuScale.UpdateViewport(2048, 1344); // Scale == 2.0
        // baseW * Scale = 600 * 2.0 = 1200, which would overflow this narrow 1100-wide viewport,
        // so the clamp (viewportW - 60 = 1040) applies even at a large scale.
        Assert.That(MenuRenderConstants.ScaledPanelW(600, 1100), Is.EqualTo(1040));
    }

    // ---- Named constants are visible ----

    [Test]
    public void ControllerAreaW_IsPositive()
    {
        Assert.That(MenuRenderConstants.ControllerAreaW, Is.GreaterThan(0));
    }

    [Test]
    public void FullPanelW_IsGreaterThan_SlimPanelW()
    {
        Assert.That(MenuRenderConstants.FullPanelW, Is.GreaterThan(MenuRenderConstants.SlimPanelW));
    }
}
