using System.Diagnostics.CodeAnalysis;
using SDL3;

namespace NEShim.Platform;

/// <summary>
/// Builds the SDL3 window and its prerequisite subsystems. Isolates the platform-conditional
/// SDL_Init/SDL_CreateWindow bootstrapping out of <see cref="SDL3WindowHost"/>'s constructor,
/// which otherwise accumulates unrelated concerns (audio fallback, per-platform window flags,
/// ...) as new platform-specific requirements are discovered.
/// </summary>
internal static class SDL3WindowBuilder
{
    [ExcludeFromCodeCoverage]
    internal static IntPtr Build(string title, int width, int height)
    {
        ConfigureVideoDriverForSteamOverlay();
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");
        Logger.LogAlways($"[Platform] SDL video driver in use: {SDL.GetCurrentVideoDriver()}");

        InitializeAudioSubsystem();

        IntPtr window = SDL.CreateWindow(title, width, height, ComputeWindowFlags());
        if (window == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");

        return window;
    }

    [ExcludeFromCodeCoverage]
    private static void InitializeAudioSubsystem()
    {
        // Audio is initialized separately and non-fatally: some Linux environments have no
        // working audio backend at all (e.g. missing libjack.so.0 with no PulseAudio/ALSA/
        // PipeWire fallback available either), which must not prevent the app from starting.
        // AudioPlayer.Start() already tolerates SDL_OpenAudioDeviceStream failing (logs and
        // runs muted) — this applies the same tolerance one level up, at subsystem init.
        // LogAlways, not Log: this runs before Logger.Enable() (called from ConfigLoader.Load,
        // well after SDL3WindowHost's constructor returns), so Log() would silently no-op here —
        // the exact class of bug the sibling video-driver diagnostic above was added to avoid.
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            Logger.LogAlways($"[Audio] SDL audio subsystem init failed, running muted: {SDL.GetError()}");
    }

    /// <summary>
    /// Forces SDL3 to use X11 (via XWayland) on Linux so Steam's overlay Vulkan layer
    /// can hook the swap chain. <c>steamoverlayvulkanlayer.so</c> intercepts
    /// <c>vkQueuePresentKHR</c> for <c>VK_KHR_xcb_surface</c> and
    /// <c>VK_KHR_xlib_surface</c> but not <c>VK_KHR_wayland_surface</c>. SDL3 prefers
    /// Wayland on modern Linux; without this the overlay never appears (regression,
    /// April 2026). Must be called before <c>SDL_Init</c>.
    ///
    /// Sets the hint three ways, in order of decreasing certainty:
    /// (1) <c>SDL_SetHintWithPriority(..., Override)</c> — SDL's own recommended API for a
    ///     programmatic, in-process hint; Override priority beats any pre-existing hint or env
    ///     var regardless of how it got there, and it's the only one of the three confirmed to
    ///     actually take effect on Steam Deck (see below). (2) <c>SDL_VIDEO_DRIVER</c> — the real
    ///     SDL3 env var name (SDL3 renamed SDL2's <c>SDL_VIDEODRIVER</c>; per
    ///     libsdl-org/SDL#11115 the old name is not read as a fallback). (3) legacy
    ///     <c>SDL_VIDEODRIVER</c>, for insurance.
    /// Env-var-only forcing (setting just #2/#3, the original fix) is a real, reproduced bug: it
    /// looked like it worked under WSL2/WSLg (a minimal Wayland compositor that doesn't advertise
    /// the fifo-v1/commit-timing-v1 protocols SDL3 checks for its own Wayland-preference default,
    /// so SDL3's own fallback happened to land on X11 anyway — coincidence, not our hint taking
    /// effect) but silently failed on Steam Deck Desktop Mode's full KDE Plasma Wayland session,
    /// which does support those protocols, so SDL3 picked Wayland — breaking the overlay hook
    /// with no error, even with the env vars independently confirmed set correctly at the moment
    /// SDL_Init ran (August 2026). Adding the SetHintWithPriority(Override) call fixed it.
    /// </summary>
    internal static void ConfigureVideoDriverForSteamOverlay()
    {
        if (!OperatingSystem.IsLinux()) return;
        SDL.SetHintWithPriority(SDL.Hints.VideoDriver, "x11", SDL.HintPriority.Override);
        Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", "x11");
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");
    }

    /// <summary>
    /// SDL's own docs (SDL_Vulkan_CreateSurface) require the window to have been created with
    /// SDL_WINDOW_VULKAN before it's valid for any Vulkan surface/GPU-renderer use. Linux's
    /// primary render path (SDL3HwRenderer) requests SPIR-V/Vulkan via SDL_CreateGPURenderer —
    /// without this flag, SDL_Vulkan_LoadLibrary() is never called for the window, and
    /// SDL_CreateGPURenderer's internal Vulkan surface/device claim fails; SDL's own
    /// cleanup-on-failure path then null-derefs inside SDL_RemoveWindowRenderer instead of
    /// returning NULL cleanly (reproduced identically on Steam Deck and WSL2, July 2026).
    /// Windows' primary path is D3D11, which needs no Vulkan surface at all; SDL's docs also
    /// warn that SDL_CreateWindow itself fails outright if SDL_WINDOW_VULKAN is requested on a
    /// system with no working Vulkan driver, so this must not be added unconditionally.
    /// </summary>
    internal static SDL.WindowFlags ComputeWindowFlags(bool? isLinuxOverride = null)
    {
        bool isLinux = isLinuxOverride ?? OperatingSystem.IsLinux();
        var flags = SDL.WindowFlags.Resizable | SDL.WindowFlags.Hidden;
        if (isLinux)
            flags |= SDL.WindowFlags.Vulkan;
        return flags;
    }
}
