using BizHawk.Common;
using System.Runtime.InteropServices;
using SDL3;

namespace NEShim.Platform;

/// <summary>
/// Detects platform capabilities and encapsulates platform-specific startup/teardown
/// that would otherwise leak OS checks into higher-level code.
/// Properties are evaluated once at startup; there is no runtime cost after initialisation.
/// </summary>
internal static class PlatformDetector
{
    /// <summary>
    /// True when running under Wine or Proton. Detected via the <c>wine_get_version</c>
    /// export that Wine always injects into <c>ntdll.dll</c>.
    /// </summary>
    public static bool IsWine { get; } = DetectWine();

    /// <summary>
    /// True when running on Steam Deck hardware. Steam sets the <c>SteamDeck</c>
    /// environment variable to <c>"1"</c> on Deck regardless of Proton or native mode.
    /// </summary>
    public static bool IsSteamDeck { get; } =
        Environment.GetEnvironmentVariable("SteamDeck") == "1";

    /// <summary>
    /// True when D3D11 initialisation succeeded and the D3D11 render path is active.
    /// Set once at startup by <c>RendererFactory.Windows.cs</c> after <see cref="D3D11Renderer"/>
    /// is (or isn't) constructed. False on systems where D3D11 is unavailable
    /// (<c>SDL3HwRenderer</c> fallback is used). All D3D11-only video filters (CRT, NTSC,
    /// palette shaders) must gate on this property before offering themselves as menu options.
    /// </summary>
    internal static bool IsD3D11Active { get; private set; }

    internal static void SetD3D11Active(bool value) => IsD3D11Active = value;

    /// <summary>
    /// True when SDL3HwRenderer initialised with SDL_CreateGPURenderer (SPIR-V shader
    /// support) rather than falling back to the plain SDL_CreateRenderer. Set once at
    /// startup by RendererFactory after SDL3HwRenderer is constructed.
    /// </summary>
    internal static bool IsSdlGpuRendererActive { get; private set; }

    internal static void SetSdlGpuRendererActive(bool value) => IsSdlGpuRendererActive = value;

    /// <summary>
    /// True when the active render path supports shader-based video features (structural
    /// filters, two-pass overlay, motion effects, picture adjust) — either D3D11 or the
    /// SDL_GPU-backed path. Menus gate advanced video options on this rather than
    /// <see cref="IsD3D11Active"/> directly, since SDL_GPU on Linux (and the Windows
    /// D3D11-unavailable fallback) supports the same feature set as D3D11.
    /// </summary>
    internal static bool SupportsAdvancedVideoFeatures => IsD3D11Active || IsSdlGpuRendererActive;

    /// <summary>
    /// True when SDL's active video driver is <c>x11</c> — the only driver Steam's Vulkan
    /// overlay layer can hook (see <see cref="SDL3WindowBuilder.ConfigureVideoDriverForSteamOverlay"/>).
    /// Live query, not cached like <see cref="IsWine"/>/<see cref="IsSteamDeck"/>: SDL must
    /// already be initialised for <c>SDL_GetCurrentVideoDriver</c> to return a meaningful value,
    /// which isn't guaranteed at static-class-initialisation time. Used to decide whether a
    /// custom achievement-toast fallback is still needed on <c>SDL3HwRenderer</c> — Steam's own
    /// overlay notification only renders when this is true, so the fallback only matters when
    /// it's false (never on D3D11, which always runs through an XCB/Xlib-compatible swap chain
    /// on Windows and doesn't use this check at all).
    /// </summary>
    internal static bool IsX11VideoDriverActive => SDL.GetCurrentVideoDriver() == "x11";

    /// <summary>
    /// Raises the Windows multimedia timer resolution to 1 ms so that Thread.Sleep and
    /// Stopwatch-based frame timing have sub-millisecond granularity. No-op on Linux/macOS
    /// where the kernel scheduler already provides sufficient resolution.
    /// Call once at process startup; pair with <see cref="EndHighResolutionTiming"/>.
    /// </summary>
    internal static void BeginHighResolutionTiming()
    {
        if (OperatingSystem.IsWindows()) Win32Imports.timeBeginPeriod(1);
    }

    /// <summary>
    /// Restores the Windows multimedia timer resolution changed by
    /// <see cref="BeginHighResolutionTiming"/>. No-op on Linux/macOS.
    /// </summary>
    internal static void EndHighResolutionTiming()
    {
        if (OperatingSystem.IsWindows()) Win32Imports.timeEndPeriod(1);
    }

    private static bool DetectWine()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            IntPtr ntdll = GetModuleHandle("ntdll.dll");
            return ntdll != IntPtr.Zero
                && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
