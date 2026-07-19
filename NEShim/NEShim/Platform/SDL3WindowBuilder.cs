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
        PlatformDetector.ConfigureVideoDriverForSteamOverlay();
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");

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
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            Logger.Log($"[Audio] SDL audio subsystem init failed, running muted: {SDL.GetError()}");
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
