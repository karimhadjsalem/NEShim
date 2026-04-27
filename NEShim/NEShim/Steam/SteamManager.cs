using Steamworks;

namespace NEShim.Steam;

/// <summary>
/// Wraps Steamworks.NET. Initializes Steam on startup and shuts it down on exit.
/// Registers a callback for GameOverlayActivated_t to pause emulation when the
/// Steam overlay opens.
///
/// If Steam is not available (not installed, or app not run through Steam),
/// IsAvailable is false and all methods become no-ops.
/// </summary>
internal static class SteamManager
{
    private static Callback<GameOverlayActivated_t>? _overlayCallback;
    private static Callback<UserStatsReceived_t>?   _statsReceivedCallback;
    private static Action<bool>? _onOverlayToggle; // bool = isActive

    private static volatile bool _statsReady;
    private static DateTime     _statsInitTime;
    private const  int          StatsTimeoutSeconds = 5;

    public static bool IsAvailable     { get; private set; }
    public static bool IsOverlayActive { get; private set; }

    /// <summary>
    /// True once Steam has delivered the initial stats snapshot, or immediately
    /// when Steam is not available (no gate needed without a live Steam session).
    /// Achievement unlocks are suppressed until this is true.
    /// </summary>
    public static bool StatsReady => !IsAvailable || _statsReady;

    /// <summary>
    /// Call before Application.Run. Reads App ID from steam_appid.txt automatically.
    /// </summary>
    public static void Initialize(Action<bool> onOverlayToggle)
    {
        _onOverlayToggle = onOverlayToggle;

        try
        {
            if (!SteamAPI.Init())
            {
                IsAvailable = false;
                return;
            }

            _overlayCallback       = Callback<GameOverlayActivated_t>.Create(OnOverlayActivated);
            _statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
            IsAvailable = true;

            bool overlayEnabled = SteamUtils.IsOverlayEnabled();
            Logger.Log($"[Steam] Overlay enabled for this app: {overlayEnabled}");
            if (!overlayEnabled)
                Logger.Log("[Steam] Overlay is disabled — check Steam settings for this game (Properties → General → Steam Overlay).");

            uint appId = (uint)SteamUtils.GetAppID();
            Logger.Log($"[Steam] App ID: {appId}");
            if (appId == 0)
            {
                // App ID 0 = development build. Steam won't deliver stats for a
                // non-existent app, so skip the handshake immediately.
                _statsReady = true;
                Logger.Log("[Steam] App ID is 0 — StatsReady set immediately (development mode).");
            }
            else
            {
                // SDK 1.61+ is supposed to deliver UserStatsReceived_t automatically.
                // In practice it does not always fire via Steamworks.NET 2025.x/SDK 1.63.
                // Record when we initialised so Tick() can apply a timeout fallback.
                _statsInitTime = DateTime.UtcNow;
                Logger.Log("[Steam] Waiting for UserStatsReceived_t (timeout in 5s).");
            }

            SteamInputManager.Initialize();
            SteamInputManager.ActivateMenuSet(); // starts at the main menu
        }
        catch (Exception ex)
        {
            Logger.Log($"[Steam] Init failed: {ex.Message}");
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Dispatches Steamworks callbacks. Must be called on the same thread as Initialize().
    /// </summary>
    public static void Tick()
    {
        if (!IsAvailable) return;
        SteamAPI.RunCallbacks();
        ApplyStatsTimeout();
    }

    private static void ApplyStatsTimeout()
    {
        if (_statsReady || _statsInitTime == default) return;
        if ((DateTime.UtcNow - _statsInitTime).TotalSeconds >= StatsTimeoutSeconds)
        {
            _statsReady = true;
            Logger.Log("[Steam] UserStatsReceived_t never arrived — forcing StatsReady after 5s timeout. Achievements will fire; StoreStats may silently no-op if Steam is not ready.");
        }
    }

    /// <summary>
    /// Switches all connected Steam controllers to the Menu action set.
    /// No-op when Steam is unavailable.
    /// </summary>
    public static void ActivateMenuSet() => SteamInputManager.ActivateMenuSet();

    /// <summary>
    /// Switches all connected Steam controllers to the Gameplay action set.
    /// No-op when Steam is unavailable.
    /// </summary>
    public static void ActivateGameplaySet() => SteamInputManager.ActivateGameplaySet();

    /// <summary>
    /// Unlocks a Steam achievement by its API name.
    /// Returns true if the achievement was newly unlocked this call.
    /// Returns false when Steam is unavailable, stats are not yet ready, or it was
    /// already unlocked. Stores stats immediately so the unlock is persisted.
    /// </summary>
    internal static bool UnlockAchievement(string id)
    {
        if (!IsAvailable)
        {
            Logger.Log($"[Steam] UnlockAchievement '{id}' skipped — Steam not available.");
            return false;
        }
        if (!_statsReady)
        {
            Logger.Log($"[Steam] UnlockAchievement '{id}' skipped — StatsReady is false.");
            return false;
        }
        if (SteamUserStats.GetAchievement(id, out bool achieved) && achieved)
        {
            Logger.Log($"[Steam] Achievement '{id}' already unlocked.");
            return false;
        }
        SteamUserStats.SetAchievement(id);
        SteamUserStats.StoreStats();
        Logger.Log($"[Steam] Achievement unlocked: {id}");
        return true;
    }

    /// <summary>
    /// Returns the display name for an achievement as configured in the Steamworks
    /// dashboard, or null when Steam is unavailable or the name is empty.
    /// </summary>
    internal static string? GetAchievementDisplayName(string id)
    {
        if (!IsAvailable) return null;
        string name = SteamUserStats.GetAchievementDisplayAttribute(id, "name");
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>Call after Application.Run exits.</summary>
    public static void Shutdown()
    {
        if (!IsAvailable) return;
        SteamInputManager.Shutdown();
        SteamAPI.Shutdown();
        IsAvailable = false;
    }

    private static void OnOverlayActivated(GameOverlayActivated_t callback)
    {
        IsOverlayActive = callback.m_bActive != 0;
        Logger.Log($"[Steam] GameOverlayActivated_t fired — m_bActive={callback.m_bActive}, IsOverlayActive={IsOverlayActive}.");
        _onOverlayToggle?.Invoke(IsOverlayActive);
    }

    private static void OnStatsReceived(UserStatsReceived_t callback)
    {
        _statsReady = true;
        Logger.Log("[Steam] UserStatsReceived — StatsReady is now true, achievements can fire.");
    }
}
