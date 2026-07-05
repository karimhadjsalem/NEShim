using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverlayRendererFactoryTests
{
    [TestCase("gdi")]
    [TestCase("GDI")]
    [TestCase("Gdi")]
    public void Create_WhenForceGdi_ReturnsNullOverlayRenderer(string forceRenderer)
    {
        using var result = OverlayRendererFactory.Create(forceRenderer, IntPtr.Zero, 1, 1);
        Assert.That(result, Is.InstanceOf<NullOverlayRenderer>());
    }

    [Test]
    public void Create_WhenForceGdi_DeviceIsNull()
    {
        using var result = OverlayRendererFactory.Create("gdi", IntPtr.Zero, 1, 1);
        Assert.That(result.Device, Is.Null);
    }

    [Test]
    public void Create_WhenForceGdi_SwapChainIsNull()
    {
        using var result = OverlayRendererFactory.Create("gdi", IntPtr.Zero, 1, 1);
        Assert.That(result.SwapChain, Is.Null);
    }
}
