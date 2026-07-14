using NEShim.Config;
using Steamworks;

namespace NEShim.Steam;

/// <summary>
/// Checks whether a game's Steam DLC is owned/installed, for gating the multi-game carousel.
/// A DLC App ID of 0 (no real Steamworks entitlement configured) and the absence of a live
/// Steam session both mean "show unconditionally" — local dev/test game folders work without
/// configuring real Steamworks DLC entitlements first, and DLC gating itself is entirely
/// optional per game: a multi-game deploy can bundle every game directly in the base install
/// (no DLC depots at all) simply by leaving SteamDlcAppId at its default 0 for every game — this
/// is a first-class supported deployment shape, not just an incidental side effect of the
/// dev/test fallback. See <see cref="FilterOwned"/> and CLAUDE.md's Multi-Game Mode section.
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

    /// <summary>
    /// Filters a scanned game list down to entries the player owns/has installed (see
    /// <see cref="IsOwned"/>). Extracted from NEShimApp.InitializeCarousel's call site so that
    /// "a multi-game deploy where every game is bundled (SteamDlcAppId 0) shows the entire
    /// library, unfiltered" is independently testable with multiple games and mixed appIds,
    /// without a live Steam session or NEShimApp's own SDL/renderer dependencies.
    /// </summary>
    internal static IReadOnlyList<GameManifest> FilterOwned(
        IEnumerable<GameManifest> games, Func<uint, bool>? dlcInstalledCheck = null) =>
        games.Where(g => IsOwned(g.SteamDlcAppId, dlcInstalledCheck)).ToList();
}
