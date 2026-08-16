using NEShim.Platform;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class PlatformDetectorTests
{
    [TearDown]
    public void TearDown()
    {
        PlatformDetector.SetD3D11Active(false);
        PlatformDetector.SetSdlGpuRendererActive(false);
    }

    // ---- IsWine ----

    [Test]
    public void IsWine_ReturnsBoolean()
    {
        // Cannot assert a specific value in a portable test — just verify it's accessible.
        Assert.That(() => _ = PlatformDetector.IsWine, Throws.Nothing);
    }

    // ---- IsSteamDeck ----

    [Test]
    public void IsSteamDeck_IsFalse_WhenEnvVarNotSet()
    {
        // Tests don't run on real Steam Deck hardware; SteamDeck env var is not set.
        // If it were set to "1" by the CI environment, this test would fail — that's acceptable
        // because it's detecting real Deck hardware.
        var envVal = Environment.GetEnvironmentVariable("SteamDeck");
        bool expected = envVal == "1";
        Assert.That(PlatformDetector.IsSteamDeck, Is.EqualTo(expected));
    }

    // ---- IsD3D11Active / SetD3D11Active ----

    [Test]
    public void IsD3D11Active_DefaultIsFalse()
    {
        PlatformDetector.SetD3D11Active(false);
        Assert.That(PlatformDetector.IsD3D11Active, Is.False);
    }

    [Test]
    public void SetD3D11Active_True_SetsIsD3D11ActiveTrue()
    {
        PlatformDetector.SetD3D11Active(true);
        Assert.That(PlatformDetector.IsD3D11Active, Is.True);
    }

    [Test]
    public void SetD3D11Active_ThenFalse_SetsIsD3D11ActiveFalse()
    {
        PlatformDetector.SetD3D11Active(true);
        PlatformDetector.SetD3D11Active(false);
        Assert.That(PlatformDetector.IsD3D11Active, Is.False);
    }

    // ---- IsSdlGpuRendererActive / SetSdlGpuRendererActive ----

    [Test]
    public void IsSdlGpuRendererActive_DefaultIsFalse()
    {
        PlatformDetector.SetSdlGpuRendererActive(false);
        Assert.That(PlatformDetector.IsSdlGpuRendererActive, Is.False);
    }

    [Test]
    public void SetSdlGpuRendererActive_True_SetsIsSdlGpuRendererActiveTrue()
    {
        PlatformDetector.SetSdlGpuRendererActive(true);
        Assert.That(PlatformDetector.IsSdlGpuRendererActive, Is.True);
    }

    [Test]
    public void SetSdlGpuRendererActive_ThenFalse_SetsIsSdlGpuRendererActiveFalse()
    {
        PlatformDetector.SetSdlGpuRendererActive(true);
        PlatformDetector.SetSdlGpuRendererActive(false);
        Assert.That(PlatformDetector.IsSdlGpuRendererActive, Is.False);
    }

    // ---- SupportsAdvancedVideoFeatures ----

    [Test]
    public void SupportsAdvancedVideoFeatures_BothFlagsFalse_IsFalse()
    {
        PlatformDetector.SetD3D11Active(false);
        PlatformDetector.SetSdlGpuRendererActive(false);
        Assert.That(PlatformDetector.SupportsAdvancedVideoFeatures, Is.False);
    }

    [Test]
    public void SupportsAdvancedVideoFeatures_OnlyD3D11Active_IsTrue()
    {
        PlatformDetector.SetD3D11Active(true);
        PlatformDetector.SetSdlGpuRendererActive(false);
        Assert.That(PlatformDetector.SupportsAdvancedVideoFeatures, Is.True);
    }

    [Test]
    public void SupportsAdvancedVideoFeatures_OnlySdlGpuActive_IsTrue()
    {
        PlatformDetector.SetD3D11Active(false);
        PlatformDetector.SetSdlGpuRendererActive(true);
        Assert.That(PlatformDetector.SupportsAdvancedVideoFeatures, Is.True);
    }

    [Test]
    public void SupportsAdvancedVideoFeatures_BothFlagsTrue_IsTrue()
    {
        PlatformDetector.SetD3D11Active(true);
        PlatformDetector.SetSdlGpuRendererActive(true);
        Assert.That(PlatformDetector.SupportsAdvancedVideoFeatures, Is.True);
    }

    // ---- IsX11VideoDriverActive ----

    [Test]
    public void IsX11VideoDriverActive_ReturnsBoolean()
    {
        // Cannot assert a specific value in a portable test — SDL_Init hasn't necessarily run
        // in this process, and the active driver is genuinely environment-dependent even when
        // it has. Just verify the query itself is safe (SDL_GetCurrentVideoDriver returning
        // null pre-SDL_Init must not throw on comparison).
        Assert.That(() => _ = PlatformDetector.IsX11VideoDriverActive, Throws.Nothing);
    }

    // ---- High-resolution timing ----

    [Test]
    public void BeginHighResolutionTiming_DoesNotThrow()
        => Assert.That(PlatformDetector.BeginHighResolutionTiming, Throws.Nothing);

    [Test]
    public void EndHighResolutionTiming_DoesNotThrow()
        => Assert.That(PlatformDetector.EndHighResolutionTiming, Throws.Nothing);
}
