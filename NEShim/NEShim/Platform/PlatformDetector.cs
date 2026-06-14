using System.Runtime.InteropServices;

namespace NEShim.Platform;

/// <summary>
/// Detects whether the process is running under Wine/Proton or on Steam Deck hardware.
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
    /// All 2.0+ video filters are D3D11-only and must gate on this property before
    /// offering themselves as menu options.
    /// </summary>
    internal static bool IsD3D11Active { get; private set; }

    internal static void SetD3D11Active(bool value) => IsD3D11Active = value;

    private static bool DetectWine()
    {
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
