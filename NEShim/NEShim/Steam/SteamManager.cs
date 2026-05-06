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
    private static Callback<UserStatsStored_t>?     _statsStoredCallback;
    private static Action<bool>? _onOverlayToggle; // bool = isActive

    private static volatile bool _statsReady;
    private static volatile bool _pendingStoreStats;

    // StoreStats is rate-limited by Steam; retrying faster than once per minute
    // can itself cause failures. _storeRetryCountdown is UI-thread-only (only
    // read/written from Tick(), which runs on the Steam timer on the UI thread).
    private static int  _storeRetryCountdown;
    private const  int  StoreRetryIntervalTicks = 360; // ~6s at 60Hz — well within Steam's rate limit

    public static bool IsAvailable     { get; private set; }
    public static bool IsOverlayActive { get; private set; }

    /// <summary>
    /// Returns the current game language from Steam (e.g. "english", "french", "japanese"),
    /// or null when Steam is not available.
    /// </summary>
    public static string? GameLanguage => IsAvailable ? SteamApps.GetCurrentGameLanguage() : null;

    /// <summary>
    /// True once stats are confirmed ready, or immediately when Steam is not
    /// available (no gate needed without a live Steam session).
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
            _statsStoredCallback   = Callback<UserStatsStored_t>.Create(OnStatsStored);
            IsAvailable = true;

            bool overlayEnabled = SteamUtils.IsOverlayEnabled();
            Logger.Log($"[Steam] Overlay enabled for this app: {overlayEnabled}");
            if (!overlayEnabled)
                Logger.Log("[Steam] Overlay is disabled — check Steam settings for this game (Properties → General → Steam Overlay).");

            uint appId = (uint)SteamUtils.GetAppID();
            Logger.Log($"[Steam] App ID: {appId}");
            if (appId == 0)
            {
                // Development build — no live app, no stats handshake needed.
                _statsReady = true;
                Logger.Log("[Steam] App ID is 0 — StatsReady set immediately (development mode).");
            }
            else
            {
                // SDK 1.61+: the Steam client synchronises stats with the server before
                // the game process launches, so stats are already in the local cache when
                // SteamAPI_Init returns. RequestCurrentStats() is deprecated and no longer
                // needed. Set StatsReady immediately — UserStatsReceived_t is registered
                // below only for diagnostic logging if Steam sends it anyway.
                _statsReady = true;
                Logger.Log("[Steam] StatsReady set immediately — Steam client pre-loads stats before launch (SDK 1.61+).");
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
        RetryPendingStore();
    }

    private static void RetryPendingStore()
    {
        if (!_pendingStoreStats) return;
        if (_storeRetryCountdown > 0) { _storeRetryCountdown--; return; }

        bool stored = SteamUserStats.StoreStats();
        Logger.Log($"[Steam] StoreStats retry — result: {stored}.");
        if (stored)
            _pendingStoreStats = false;
        else
            _storeRetryCountdown = StoreRetryIntervalTicks;
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
        bool set    = SteamUserStats.SetAchievement(id);
        bool stored = SteamUserStats.StoreStats();
        Logger.Log($"[Steam] Achievement '{id}' — SetAchievement={set}, StoreStats={stored}.");
        if (!stored)
        {
            _pendingStoreStats    = true;
            _storeRetryCountdown  = StoreRetryIntervalTicks;
            Logger.Log("[Steam] StoreStats returned false — will retry from Tick() after backoff.");
        }
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
        // Stats are already ready (set at init). This callback fires only if something
        // calls RequestCurrentStats() explicitly; log it for diagnostics but take no action.
        Logger.Log($"[Steam] UserStatsReceived_t — result={callback.m_eResult} (diagnostic; StatsReady was already true).");
    }

    private static void OnStatsStored(UserStatsStored_t callback)
    {
        Logger.Log($"[Steam] UserStatsStored_t — result={callback.m_eResult}.");
        // k_EResultOK: clean store. k_EResultInvalidParam: server rejected a stat and sent
        // back corrected values — not expected for achievements, but stop retrying either way.
        if (callback.m_eResult == EResult.k_EResultOK || callback.m_eResult == EResult.k_EResultInvalidParam)
            _pendingStoreStats = false;
    }
}
