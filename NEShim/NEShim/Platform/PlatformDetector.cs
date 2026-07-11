using BizHawk.Common;
using System.Runtime.InteropServices;

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
    /// Set once at startup by <see cref="MainForm"/> after <see cref="D3D11Renderer"/> is
    /// constructed. False on systems where D3D11 is unavailable (GDI+ fallback is used).
    /// All D3D11-only video filters (CRT, NTSC, palette shaders) must gate on this
    /// property before offering themselves as menu options.
    /// </summary>
    internal static bool IsD3D11Active { get; private set; }

    internal static void SetD3D11Active(bool value) => IsD3D11Active = value;

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

    /// <summary>
    /// Forces SDL3 to use X11 (via XWayland) on Linux so Steam's overlay Vulkan layer
    /// can hook the swap chain. <c>steamoverlayvulkanlayer.so</c> intercepts
    /// <c>vkQueuePresentKHR</c> for <c>VK_KHR_xcb_surface</c> and
    /// <c>VK_KHR_xlib_surface</c> but not <c>VK_KHR_wayland_surface</c>. SDL3 prefers
    /// Wayland on modern Linux; without this the overlay never appears (regression,
    /// April 2026). Must be called before <c>SDL_Init</c>.
    /// </summary>
    internal static void ConfigureVideoDriverForSteamOverlay()
    {
        if (OperatingSystem.IsLinux())
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");
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
