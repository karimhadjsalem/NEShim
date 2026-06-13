using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class LogoScreenTests
{
    [Test]
    public void ComputeAlpha_AtZeroElapsed_ReturnsZero()
    {
        Assert.That(LogoScreen.ComputeAlpha(0f, 0.5f, 2f, 0.5f), Is.EqualTo(0f));
    }

    [Test]
    public void ComputeAlpha_NegativeElapsed_ReturnsZero()
    {
        Assert.That(LogoScreen.ComputeAlpha(-1f, 0.5f, 2f, 0.5f), Is.EqualTo(0f));
    }

    [Test]
    public void ComputeAlpha_HalfwayThroughFadeIn_ReturnsHalfAlpha()
    {
        // fadeIn=0.5s; halfway = 0.25s
        Assert.That(LogoScreen.ComputeAlpha(0.25f, 0.5f, 2f, 0.5f), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ComputeAlpha_AtFadeInEnd_ReturnsOne()
    {
        Assert.That(LogoScreen.ComputeAlpha(0.5f, 0.5f, 2f, 0.5f), Is.EqualTo(1f));
    }

    [Test]
    public void ComputeAlpha_DuringHold_ReturnsOne()
    {
        // hold runs from 0.5s to 2.5s; check at 1.5s
        Assert.That(LogoScreen.ComputeAlpha(1.5f, 0.5f, 2f, 0.5f), Is.EqualTo(1f));
    }

    [Test]
    public void ComputeAlpha_HalfwayThroughFadeOut_ReturnsHalfAlpha()
    {
        // fadeOut starts at 2.5s (fadeIn+hold), lasts 0.5s; halfway = 2.75s
        Assert.That(LogoScreen.ComputeAlpha(2.75f, 0.5f, 2f, 0.5f), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ComputeAlpha_AtFadeOutEnd_ReturnsZero()
    {
        Assert.That(LogoScreen.ComputeAlpha(3f, 0.5f, 2f, 0.5f), Is.EqualTo(0f));
    }

    [Test]
    public void ComputeAlpha_BeyondTotal_ReturnsZero()
    {
        Assert.That(LogoScreen.ComputeAlpha(10f, 0.5f, 2f, 0.5f), Is.EqualTo(0f));
    }
}
