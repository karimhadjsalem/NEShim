using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverlayRendererTests
{
    [Test]
    public void ToastDurationSeconds_IsPositive()
        => Assert.That(OverlayRenderer.ToastDurationSeconds, Is.GreaterThan(0));
}
