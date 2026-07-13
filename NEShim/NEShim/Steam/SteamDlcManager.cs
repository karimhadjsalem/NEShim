using Steamworks;

namespace NEShim.Steam;

/// <summary>
/// Checks whether a game's Steam DLC is owned/installed, for gating the multi-game carousel.
/// A DLC App ID of 0 (no real Steamworks entitlement configured) and the absence of a live
/// Steam session both mean "show unconditionally" — local dev/test game folders work without
/// configuring real Steamworks DLC entitlements first.
/// </summary>
internal static class SteamDlcManager
{
    /// <summary>
    /// <paramref name="dlcInstalledCheck"/> is an injection point for tests. When supplied, it
    /// stands in for a live Steam session entirely (bypassing <see cref="SteamManager.IsAvailable"/>,
    /// which is always false in a test process) so the appId==0 and "DLC owned/not owned"
    /// branches are exercisable without a real Steamworks session. Production callers omit it
    /// and get the real <see cref="SteamManager.IsAvailable"/> gate plus the real
    /// <see cref="SteamApps.BIsDlcInstalled"/> call.
    /// </summary>
    internal static bool IsOwned(uint appId, Func<uint, bool>? dlcInstalledCheck = null)
    {
        if (appId == 0) return true; // unfiltered — dev/test content folders
        if (dlcInstalledCheck is not null) return dlcInstalledCheck(appId);
        if (!SteamManager.IsAvailable) return true; // no live Steam session — local discovery fallback

        return SteamApps.BIsDlcInstalled(new AppId_t(appId));
    }
}
