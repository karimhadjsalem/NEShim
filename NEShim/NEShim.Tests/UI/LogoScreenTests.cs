using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
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

    // ---- Instance property tests ----

    private static Bitmap MakeBitmap() =>
        new(16, 16, PixelFormat.Format32bppArgb);

    // Set _startTime via reflection so we can control elapsed time without sleeping.
    private static void SetStartTime(LogoScreen screen, DateTime value)
    {
        var field = typeof(LogoScreen).GetField("_startTime",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(screen, (DateTime?)value);
    }

    [Test]
    public void Image_ReturnsConstructorBitmap()
    {
        using var bmp    = MakeBitmap();
        using var screen = new LogoScreen(bmp);
        Assert.That(screen.Image, Is.SameAs(bmp));
    }

    [Test]
    public void IsComplete_ReturnsFalse_JustAfterCreation()
    {
        using var bmp    = MakeBitmap();
        using var screen = new LogoScreen(bmp);
        // First access initialises _startTime — elapsed is ~0, well below 3s total.
        Assert.That(screen.IsComplete, Is.False);
    }

    [Test]
    public void IsComplete_ReturnsTrue_WhenElapsedBeyondTotal()
    {
        using var bmp    = MakeBitmap();
        using var screen = new LogoScreen(bmp);
        // 10 seconds in the past → elapsed > 3s → complete
        SetStartTime(screen, DateTime.UtcNow - TimeSpan.FromSeconds(10));
        Assert.That(screen.IsComplete, Is.True);
    }

    [Test]
    public void CurrentAlpha_IsZero_JustAfterCreation()
    {
        using var bmp    = MakeBitmap();
        using var screen = new LogoScreen(bmp);
        // elapsed ~0 → ComputeAlpha(0, ...) = 0
        Assert.That(screen.CurrentAlpha, Is.EqualTo(0f).Within(0.05f));
    }

    [Test]
    public void CurrentAlpha_IsOne_DuringHoldPhase()
    {
        using var bmp    = MakeBitmap();
        using var screen = new LogoScreen(bmp);
        // FadeIn=0.5s, Hold starts at 0.5s; set start to 1s ago → elapsed=1s → in hold → alpha=1
        SetStartTime(screen, DateTime.UtcNow - TimeSpan.FromSeconds(1.0));
        Assert.That(screen.CurrentAlpha, Is.EqualTo(1f).Within(0.05f));
    }

    [Test]
    public void CurrentAlpha_IsZero_AfterFadeOut()
    {
        using var bmp    = MakeBitmap();
        using var screen = new LogoScreen(bmp);
        // 10s ago → well into fade-out end → alpha=0
        SetStartTime(screen, DateTime.UtcNow - TimeSpan.FromSeconds(10));
        Assert.That(screen.CurrentAlpha, Is.EqualTo(0f).Within(0.05f));
    }

    [Test]
    public void Dispose_DisposesImage()
    {
        var bmp    = MakeBitmap();
        var screen = new LogoScreen(bmp);
        screen.Dispose();
        // GetPixel on a disposed Bitmap throws ArgumentException ("Parameter is not valid")
        Assert.That(() => bmp.GetPixel(0, 0), Throws.InstanceOf<ArgumentException>());
    }
}
