using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

/// <summary>
/// Tests for the RendererFactory error paths. The D3D11 success path requires real
/// hardware and is not exercised here; only the two throw branches are testable in a
/// unit context.
/// </summary>
[TestFixture]
internal class RendererFactoryTests
{
    [TearDown]
    public void TearDown() => NEShim.Platform.PlatformDetector.SetD3D11Active(false);

    // ── forceRenderer = "gdi" ───────────────────────────────────────────────────

    [TestCase("gdi")]
    [TestCase("GDI")]
    [TestCase("Gdi")]
    public void Create_WhenForceRendererIsGdi_ThrowsInvalidOperationException(string forceRenderer)
    {
        using var overlay = new NullOverlayRenderer();
        Assert.That(
            () => RendererFactory.Create(overlay, 256, 240, forceRenderer),
            Throws.InvalidOperationException);
    }

    [Test]
    public void Create_WhenForceRendererIsGdi_ErrorMessageMentionsRemoved()
    {
        using var overlay = new NullOverlayRenderer();
        var ex = Assert.Throws<InvalidOperationException>(
            () => RendererFactory.Create(overlay, 256, 240, "gdi"));
        Assert.That(ex!.Message, Does.Contain("no longer supported"));
    }

    // ── D3D11 device unavailable (NullOverlayRenderer has null Device/SwapChain) ─

    [Test]
    public void Create_WhenDeviceIsNull_ThrowsInvalidOperationException()
    {
        using var overlay = new NullOverlayRenderer();
        Assert.That(
            () => RendererFactory.Create(overlay, 256, 240),
            Throws.InvalidOperationException);
    }

    [Test]
    public void Create_WhenDeviceIsNull_ErrorMessageMentionsNoFallback()
    {
        using var overlay = new NullOverlayRenderer();
        var ex = Assert.Throws<InvalidOperationException>(
            () => RendererFactory.Create(overlay, 256, 240));
        Assert.That(ex!.Message, Does.Contain("no fallback"));
    }

    [Test]
    public void Create_WhenDeviceIsNull_DoesNotSetD3D11Active()
    {
        using var overlay = new NullOverlayRenderer();
        try { RendererFactory.Create(overlay, 256, 240); } catch (InvalidOperationException) { }
        Assert.That(NEShim.Platform.PlatformDetector.IsD3D11Active, Is.False);
    }
}
