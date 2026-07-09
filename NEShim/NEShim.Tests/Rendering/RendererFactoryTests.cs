using NEShim.Platform;
using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

/// <summary>
/// Tests the invariants of RendererFactory that don't require real GPU hardware.
/// The D3D11 and SDL3HwRenderer success paths both need a live device or SDL window
/// and cannot be exercised in a unit-test context.
/// </summary>
[TestFixture]
internal class RendererFactoryTests
{
    [TearDown]
    public void TearDown() => PlatformDetector.SetD3D11Active(false);

    [Test]
    public void Create_WhenDeviceIsNull_DoesNotSetD3D11Active()
    {
        using var overlay    = new NullOverlayRenderer();
        var       windowHost = new NullWindowHost();
        // SDL3HwRenderer will throw because SdlWindow is IntPtr.Zero and SDL isn't running.
        // What matters is that the D3D11Active flag was never set before the throw.
        try { RendererFactory.Create(overlay, 256, 240, windowHost)?.Dispose(); }
        catch (Exception) { }
        Assert.That(PlatformDetector.IsD3D11Active, Is.False);
    }

    private sealed class NullWindowHost : IWindowHost
    {
        public IntPtr Handle      => IntPtr.Zero;
        public IntPtr SdlWindow   => IntPtr.Zero;
        public int    ClientWidth  => 0;
        public int    ClientHeight => 0;
        public event Action<int, int>? Resized     { add { } remove { } }
        public event Action<bool>?     FocusChanged { add { } remove { } }
    }
}
