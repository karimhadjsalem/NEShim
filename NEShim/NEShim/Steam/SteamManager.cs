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
    private static Action<bool>? _onOverlayToggle; // bool = isActive

    public static bool IsAvailable    { get; private set; }
    public static bool IsOverlayActive { get; private set; }

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
                System.Diagnostics.Debug.WriteLine("[Steam] SteamAPI.Init() returned false — running without Steam.");
                IsAvailable = false;
                return;
            }

            _overlayCallback = Callback<GameOverlayActivated_t>.Create(OnOverlayActivated);
            IsAvailable = true;
            System.Diagnostics.Debug.WriteLine("[Steam] Initialized successfully.");

            // Initialize Steam Input for Steam Controller support
            SteamInputManager.Initialize();
            SteamInputManager.ActivateMenuSet(); // starts at the main menu
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Steam] Init exception: {ex.Message}");
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Call once per emulation frame to dispatch Steamworks callbacks.
    /// Must be called on the same thread as Initialize().
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
}
