using NEShim.Platform;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class PlatformDefaultsTests
{
    // ---- ResolveEmulationSpinMs ----

    [Test]
    public void ResolveEmulationSpinMs_NullConfig_ReturnsOne()
    {
        Assert.That(PlatformDefaults.ResolveEmulationSpinMs(null), Is.EqualTo(1));
    }

    [Test]
    public void ResolveEmulationSpinMs_ConfigOverride_ReturnsConfig()
    {
        Assert.That(PlatformDefaults.ResolveEmulationSpinMs(2), Is.EqualTo(2));
    }

    // ---- ResolveAudioLatencyMs ----

    [Test]
    public void ResolveAudioLatencyMs_NullConfig_ReturnsFifty()
    {
        Assert.That(PlatformDefaults.ResolveAudioLatencyMs(null), Is.EqualTo(50));
    }

    [Test]
    public void ResolveAudioLatencyMs_ConfigOverride_ReturnsConfig()
    {
        Assert.That(PlatformDefaults.ResolveAudioLatencyMs(150), Is.EqualTo(150));
    }
}
