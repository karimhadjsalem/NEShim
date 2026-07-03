using NEShim.Rendering;
using NUnit.Framework;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal sealed class LetterboxGeometryTests
{
    private const float Tolerance = 0.0001f;

    // NES standard: 256px wide, 224px display height, PAR 8/7
    // Display aspect = 256 * (8/7) / 224 ≈ 1.3061

    [Test]
    public void Compute_WideViewport_Pillarboxes()
    {
        // 1920×1080 viewport is wider than NES 4:3-ish, so height fills and sides get bars
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: false);

        // destH should equal viewport height (height limited)
        Assert.That(result.PixelH, Is.EqualTo(1080));

        // destW < 1920 (not full width — pillarboxed)
        Assert.That(result.PixelW, Is.LessThan(1920));

        // X edges must be symmetric around 0
        Assert.That(result.X0 + result.X1, Is.EqualTo(0f).Within(Tolerance));

        // Y edges fill the full clip-space height
        Assert.That(result.Y0, Is.EqualTo(1f).Within(Tolerance));
        Assert.That(result.Y1, Is.EqualTo(-1f).Within(Tolerance));
    }

    [Test]
    public void Compute_TallViewport_Letterboxes()
    {
        // NES display aspect ≈ 1.306. Use 640×600 (aspect ≈ 1.067 < 1.306) so width fills.
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 640, 600, underscan: false);

        // destW should equal viewport width (width limited)
        Assert.That(result.PixelW, Is.EqualTo(640));

        // destH < 600 (not full height — letterboxed)
        Assert.That(result.PixelH, Is.LessThan(600));

        // X edges fill the full clip-space width
        Assert.That(result.X0, Is.EqualTo(-1f).Within(Tolerance));
        Assert.That(result.X1, Is.EqualTo(1f).Within(Tolerance));

        // Y edges must be symmetric around 0
        Assert.That(result.Y0 + result.Y1, Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void Compute_Underscan_ShrinksRect()
    {
        var normal   = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: false);
        var underscan = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: true);

        Assert.That(underscan.PixelW, Is.LessThan(normal.PixelW));
        Assert.That(underscan.PixelH, Is.LessThan(normal.PixelH));

        // Scale factor on dimensions should be close to UnderscanScale
        float wRatio = (float)underscan.PixelW / normal.PixelW;
        Assert.That(wRatio, Is.EqualTo(LetterboxGeometry.UnderscanScale).Within(0.01f));
    }

    [Test]
    public void Compute_UnderscanScale_IsCorrectConstant()
    {
        Assert.That(LetterboxGeometry.UnderscanScale, Is.EqualTo(0.88f));
    }

    [Test]
    public void Compute_SquareViewport_MatchingAspect_FillsCompletely()
    {
        // If viewport aspect exactly matches the display aspect, content should fill exactly
        // Display aspect = 256 * (8/7) / 224 = (256*8) / (7*224) = 2048 / 1568 ≈ 1.30612
        // Use a viewport that matches: width=1306, height=1000 (approx)
        float displayAspect = 256f * (8f / 7f) / 224f;
        int vpH = 1000;
        int vpW = (int)(vpH * displayAspect);

        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, vpW, vpH, underscan: false);

        // Should fill the full clip space (within integer rounding)
        Assert.That(result.X0, Is.EqualTo(-1f).Within(0.01f));
        Assert.That(result.X1, Is.EqualTo(1f).Within(0.01f));
        Assert.That(result.Y0, Is.EqualTo(1f).Within(0.01f));
        Assert.That(result.Y1, Is.EqualTo(-1f).Within(0.01f));
    }

    [Test]
    public void Compute_ClipSpaceY_TopIsPositive()
    {
        // D3D11 clip space: y=+1 is the top of the screen
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: false);

        Assert.That(result.Y0, Is.GreaterThan(result.Y1),
            "Y0 (top edge) must be greater than Y1 (bottom edge) in D3D clip space");
    }

    [Test]
    public void Compute_ClipSpaceX_LeftIsNegative()
    {
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: false);

        Assert.That(result.X0, Is.LessThan(result.X1),
            "X0 (left edge) must be less than X1 (right edge)");
    }

    [Test]
    public void Compute_PixelDimensions_AreAtLeastOne()
    {
        // Even a 1×1 viewport shouldn't produce zero-dimension results
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1, 1, underscan: false);

        Assert.That(result.PixelW, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.PixelH, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Compute_HighPar_WidensContent()
    {
        // A higher PAR means the content appears wider — same viewport should produce a wider rect
        var lowPar  = LetterboxGeometry.Compute(256, 224, 1f, 1920, 1080, underscan: false);
        var highPar = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: false);

        Assert.That(highPar.PixelW, Is.GreaterThan(lowPar.PixelW));
    }

    [Test]
    public void Compute_WideViewport_ResultIsCenteredHorizontally()
    {
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 1920, 1080, underscan: false);

        float centerX = (result.X0 + result.X1) / 2f;
        Assert.That(centerX, Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void Compute_TallViewport_ResultIsCenteredVertically()
    {
        var result = LetterboxGeometry.Compute(256, 224, 8f / 7f, 800, 600, underscan: false);

        float centerY = (result.Y0 + result.Y1) / 2f;
        Assert.That(centerY, Is.EqualTo(0f).Within(Tolerance));
    }
}
