using NEShim.Platform;
using SDL3;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class SDL3WindowBuilderTests
{
    private string? _savedVideoDriver;
    private string? _savedVideoDriverReal;

    [SetUp]
    public void SetUp()
    {
        _savedVideoDriver     = Environment.GetEnvironmentVariable("SDL_VIDEODRIVER");
        _savedVideoDriverReal = Environment.GetEnvironmentVariable("SDL_VIDEO_DRIVER");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", _savedVideoDriver);
        Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", _savedVideoDriverReal);
    }

    [Test]
    public void ComputeWindowFlags_OnLinux_IncludesVulkan()
    {
        var flags = SDL3WindowBuilder.ComputeWindowFlags(isLinuxOverride: true);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Vulkan), Is.True);
    }

    [Test]
    public void ComputeWindowFlags_NotOnLinux_ExcludesVulkan()
    {
        var flags = SDL3WindowBuilder.ComputeWindowFlags(isLinuxOverride: false);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Vulkan), Is.False);
    }

    [Test]
    public void ComputeWindowFlags_AlwaysIncludesResizableAndHidden()
    {
        var flags = SDL3WindowBuilder.ComputeWindowFlags(isLinuxOverride: false);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Resizable), Is.True);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Hidden), Is.True);
    }

    // ---- ConfigureVideoDriverForSteamOverlay ----

    [Test]
    public void ConfigureVideoDriverForSteamOverlay_DoesNotThrow()
        => Assert.That(SDL3WindowBuilder.ConfigureVideoDriverForSteamOverlay, Throws.Nothing);

    [Test]
    public void ConfigureVideoDriverForSteamOverlay_WhenLinux_SetsX11VideoDriver()
    {
        Assume.That(OperatingSystem.IsLinux());
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", null);
        Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", null);
        SDL3WindowBuilder.ConfigureVideoDriverForSteamOverlay();
        Assert.That(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER"), Is.EqualTo("x11"));
    }

    // SDL3 renamed SDL2's SDL_VIDEODRIVER hint to SDL_VIDEO_DRIVER and does not read the old
    // name as a fallback (libsdl-org/SDL#11115) — setting only the legacy name is a real,
    // reproduced bug (silently ignored, SDL3 falls back to its own Wayland/X11 preference
    // logic). This is the actual hint SDL3 reads; regression coverage for that bug.
    [Test]
    public void ConfigureVideoDriverForSteamOverlay_WhenLinux_SetsX11OnRealSdl3HintName()
    {
        Assume.That(OperatingSystem.IsLinux());
        Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", null);
        SDL3WindowBuilder.ConfigureVideoDriverForSteamOverlay();
        Assert.That(Environment.GetEnvironmentVariable("SDL_VIDEO_DRIVER"), Is.EqualTo("x11"));
    }

    // The env-var tests above cover the insurance layer, but the fix that actually matters is
    // SDL_SetHintWithPriority(..., Override) — env vars alone were confirmed (Steam Deck Desktop
    // Mode, August 2026) to lose to SDL3's own Wayland-preference default despite being correctly
    // set at the moment SDL_Init ran. Asserting via SDL.GetHint verifies the real mechanism
    // rather than only its (previously insufficient) env-var side effect.
    [Test]
    public void ConfigureVideoDriverForSteamOverlay_WhenLinux_SetsSdlHintDirectly()
    {
        Assume.That(OperatingSystem.IsLinux());
        SDL3WindowBuilder.ConfigureVideoDriverForSteamOverlay();
        Assert.That(SDL.GetHint(SDL.Hints.VideoDriver), Is.EqualTo("x11"));
    }

    [Test]
    public void ConfigureVideoDriverForSteamOverlay_WhenNotLinux_DoesNotChangeEnvVar()
    {
        Assume.That(!OperatingSystem.IsLinux());
        string? beforeLegacy = Environment.GetEnvironmentVariable("SDL_VIDEODRIVER");
        string? beforeReal   = Environment.GetEnvironmentVariable("SDL_VIDEO_DRIVER");
        SDL3WindowBuilder.ConfigureVideoDriverForSteamOverlay();
        Assert.That(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER"), Is.EqualTo(beforeLegacy));
        Assert.That(Environment.GetEnvironmentVariable("SDL_VIDEO_DRIVER"), Is.EqualTo(beforeReal));
    }
}
