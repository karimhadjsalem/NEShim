using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class MenuRenderConstantsTests
{
    // ---- PanelW: non-Steam-Deck environment ----

    [Test]
    public void PanelW_WhenNotSteamDeck_ReturnsBaseW()
    {
        // SteamDeck env var is never set in the test environment so IsSteamDeck = false.
        Assert.That(MenuRenderConstants.PanelW(520, 1280), Is.EqualTo(520));
    }

    [Test]
    public void PanelW_WhenNotSteamDeck_IgnoresViewportW()
    {
        Assert.That(MenuRenderConstants.PanelW(480, 500), Is.EqualTo(480));
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
