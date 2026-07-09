using NEShim.Platform;
using NEShim.Rendering;
using NSubstitute;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverlayRendererFactoryTests
{
    [Test]
    public void Create_Always_ReturnsSteamOverlayRenderer()
    {
        var host = Substitute.For<IWindowHost>();
        host.Handle.Returns(IntPtr.Zero);
        host.SdlWindow.Returns(IntPtr.Zero);
        host.ClientWidth.Returns(1);
        host.ClientHeight.Returns(1);

        // SteamOverlayRenderer.Initialize silently absorbs D3D11 failures; no throw expected.
        using var result = OverlayRendererFactory.Create(host);

        Assert.That(result, Is.InstanceOf<SteamOverlayRenderer>());
    }
}
