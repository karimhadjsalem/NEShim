using NEShim.Platform;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class PlatformDetectorTests
{
    private string? _savedVideoDriver;

    [SetUp]
    public void SetUp() => _savedVideoDriver = Environment.GetEnvironmentVariable("SDL_VIDEODRIVER");

    [TearDown]
    public void TearDown()
    {
        PlatformDetector.SetD3D11Active(false);
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", _savedVideoDriver);
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

    // ---- High-resolution timing ----

    [Test]
    public void BeginHighResolutionTiming_DoesNotThrow()
        => Assert.That(PlatformDetector.BeginHighResolutionTiming, Throws.Nothing);

    [Test]
    public void EndHighResolutionTiming_DoesNotThrow()
        => Assert.That(PlatformDetector.EndHighResolutionTiming, Throws.Nothing);

    // ---- ConfigureVideoDriverForSteamOverlay ----

    [Test]
    public void ConfigureVideoDriverForSteamOverlay_DoesNotThrow()
        => Assert.That(PlatformDetector.ConfigureVideoDriverForSteamOverlay, Throws.Nothing);

    [Test]
    public void ConfigureVideoDriverForSteamOverlay_WhenLinux_SetsX11VideoDriver()
    {
        Assume.That(OperatingSystem.IsLinux());
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", null);
        PlatformDetector.ConfigureVideoDriverForSteamOverlay();
        Assert.That(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER"), Is.EqualTo("x11"));
    }

    [Test]
    public void ConfigureVideoDriverForSteamOverlay_WhenNotLinux_DoesNotChangeEnvVar()
    {
        Assume.That(!OperatingSystem.IsLinux());
        string? before = Environment.GetEnvironmentVariable("SDL_VIDEODRIVER");
        PlatformDetector.ConfigureVideoDriverForSteamOverlay();
        Assert.That(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER"), Is.EqualTo(before));
    }
}
