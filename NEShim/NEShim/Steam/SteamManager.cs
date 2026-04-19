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

    public static bool IsAvailable     { get; private set; }
    public static bool IsOverlayActive { get; private set; }

    /// <summary>
    /// True once Steam has delivered the initial stats snapshot.
    /// Achievement unlocks are suppressed until this is true.
    /// </summary>
    public static bool StatsReady => _statsReady;

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

            // Stats load automatically in SDK 1.61+; UserStatsReceived_t fires without
            // an explicit RequestCurrentStats() call.

            SteamInputManager.Initialize();
            SteamInputManager.ActivateMenuSet(); // starts at the main menu
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Steam] Init failed: {ex.Message}");
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
    /// No-op when Steam is unavailable, stats are not yet ready, or the achievement
    /// is already unlocked. Stores stats immediately so the unlock is persisted.
    /// </summary>
    internal static void UnlockAchievement(string id)
    {
        if (!IsAvailable || !_statsReady) return;
        if (SteamUserStats.GetAchievement(id, out bool achieved) && achieved) return;
        SteamUserStats.SetAchievement(id);
        SteamUserStats.StoreStats();
        System.Diagnostics.Debug.WriteLine($"[Steam] Achievement unlocked: {id}");
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
        _onOverlayToggle?.Invoke(IsOverlayActive);
    }

    private static void OnStatsReceived(UserStatsReceived_t callback)
    {
        _statsReady = true;
    }
}
