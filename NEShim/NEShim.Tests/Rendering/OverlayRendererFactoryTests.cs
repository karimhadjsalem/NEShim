using NEShim.Platform;
using NEShim.Rendering;
using NSubstitute;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverlayRendererFactoryTests
{
    private static IWindowHost StubHost()
    {
        var host = Substitute.For<IWindowHost>();
        host.Handle.Returns(IntPtr.Zero);
        host.ClientWidth.Returns(1);
        host.ClientHeight.Returns(1);
        return host;
    }

    [TestCase("gdi")]
    [TestCase("GDI")]
    [TestCase("Gdi")]
    public void Create_WhenForceGdi_ReturnsNullOverlayRenderer(string forceRenderer)
    {
        using var result = OverlayRendererFactory.Create(forceRenderer, StubHost());
        Assert.That(result, Is.InstanceOf<NullOverlayRenderer>());
    }

    [Test]
    public void Create_WhenForceGdi_DeviceIsNull()
    {
        using var result = OverlayRendererFactory.Create("gdi", StubHost());
        Assert.That(result.Device, Is.Null);
    }

    [Test]
    public void Create_WhenForceGdi_SwapChainIsNull()
    {
        using var result = OverlayRendererFactory.Create("gdi", StubHost());
        Assert.That(result.SwapChain, Is.Null);
    }
}
