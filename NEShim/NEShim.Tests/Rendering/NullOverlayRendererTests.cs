using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class NullOverlayRendererTests
{
    private NullOverlayRenderer _renderer = null!;

    [SetUp]
    public void SetUp() => _renderer = new NullOverlayRenderer();

    [TearDown]
    public void TearDown() => _renderer.Dispose();

    [Test]
    public void Implements_IOverlayRenderer()
        => Assert.That(_renderer, Is.InstanceOf<IOverlayRenderer>());

    [Test]
    public void Device_ReturnsNull()
        => Assert.That(((IOverlayRenderer)_renderer).Device, Is.Null);

    [Test]
    public void SwapChain_ReturnsNull()
        => Assert.That(((IOverlayRenderer)_renderer).SwapChain, Is.Null);

    [Test]
    public void Present_DoesNotThrow()
        => Assert.That(() => _renderer.Present(), Throws.Nothing);

    [Test]
    public void Resize_DoesNotThrow()
        => Assert.That(() => _renderer.Resize(1280, 720), Throws.Nothing);

    [Test]
    public void Dispose_DoesNotThrow()
        => Assert.That(() => _renderer.Dispose(), Throws.Nothing);
}
