using System.Collections.Immutable;
using Steamworks;
using NEShim.Input;

namespace NEShim.Steam;

/// <summary>
/// Wraps the Steamworks ISteamInput API for Steam Controller support.
///
/// Requires a game_actions_&lt;appid&gt;.vdf file in the game directory defining
/// "Gameplay" and "Menu" action sets with the actions below.  When Steam Input
/// is unavailable (Steam not running, no controllers, or VDF missing), all
/// methods silently return empty/default values so the XInput path continues
/// to work unaffected.
///
/// Action sets / action names must match game_actions_&lt;appid&gt;.vdf exactly.
/// </summary>
internal static class SteamInputManager
{
    // -- VDF action name ↔ NES button suffix constant table --
    // Fixed by game_actions_<appid>.vdf; not user-configurable. Player-agnostic — the same 8
    // action names are activated per-player by switching which controller handle is polled
    // (see ControllerHandle), not by defining separate per-player VDF actions.

    private static readonly IReadOnlyDictionary<string, string> ActionSuffixes =
        new Dictionary<string, string>
        {
            ["up"]       = "Up",
            ["down"]     = "Down",
            ["left"]     = "Left",
            ["right"]    = "Right",
            ["a_button"] = "A",
            ["b_button"] = "B",
            ["start"]    = "Start",
            ["select"]   = "Select",
        };

    private static readonly IReadOnlyDictionary<string, string> SuffixToAction =
        ActionSuffixes.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Translates a VDF action name to the NES button config key for the given player (1-based).</summary>
    internal static string? NesButtonFor(string actionName, int player) =>
        ActionSuffixes.TryGetValue(actionName, out var suffix) ? $"P{player} {suffix}" : null;

    /// <summary>
    /// Reverse of <see cref="NesButtonFor"/> — used for label/glyph lookups in the binding menu.
    /// Parses the button suffix off any "P{n} &lt;Suffix&gt;" config key, player-agnostic.
    /// </summary>
    internal static string? ActionFor(string nesButtonConfigKey)
    {
        int space = nesButtonConfigKey.IndexOf(' ');
        if (space < 0) return null;
        string suffix = nesButtonConfigKey[(space + 1)..];
        return SuffixToAction.TryGetValue(suffix, out var action) ? action : null;
    }

    // -- Controller handle buffer (reused to avoid allocation) --
    private static readonly InputHandle_t[] _controllerBuf =
        new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];

    // Cached connected controller count — updated whenever controllers are enumerated.
    // Written only on the emulation thread; read (as a cached snapshot) on the UI thread.
    private static int _connectedCount;

    /// <summary>True when at least one Steam Input controller is connected and active.</summary>
    public static bool HasConnectedController => IsAvailable && _connectedCount > 0;

    /// <summary>
    /// True only when the player has actually assigned a trackpad or gyro input to one of
    /// NEShim's gameplay/menu actions — not merely because a Steam-managed controller is
    /// connected. A Steam Controller or Steam Deck used with ordinary face buttons/d-pad
    /// returns false here and is handled entirely by SDL, exactly like any other controller;
    /// this is the one case SDL genuinely can't represent (a physical trackpad click/swipe or
    /// motion-sensor input bound to an NES action), so it's the only case that still needs
    /// native action-set polling + the rebind lockout.
    /// </summary>
    public static bool IsUsingNativeActions(int controllerIndex = 0)
    {
        // Deliberately does NOT call RefreshControllers — relies on a same-frame GetActiveActions
        // call having already refreshed _controllerBuf/_connectedCount (see SteamInputSource's
        // "critical call order" doc comment). Reading the cached snapshot here, rather than
        // triggering its own refresh, preserves that existing contract unchanged.
        var h = ControllerHandleCached(controllerIndex);
        return h != default && AnyBoundOriginIsTouchpadOrGyro(h);
    }

    private static bool AnyBoundOriginIsTouchpadOrGyro(InputHandle_t h)
    {
        var origins = new EInputActionOrigin[Constants.STEAM_INPUT_MAX_ORIGINS];
        foreach (var action in GameplayActionHandles)
        {
            int n = SteamInput.GetDigitalActionOrigins(h, _gameplaySet, action, origins);
            for (int i = 0; i < n; i++)
                if (IsTouchpadOrGyroOrigin(origins[i].ToString())) return true;
        }
        foreach (var action in MenuActionHandles)
        {
            int n = SteamInput.GetDigitalActionOrigins(h, _menuSet, action, origins);
            for (int i = 0; i < n; i++)
                if (IsTouchpadOrGyroOrigin(origins[i].ToString())) return true;
        }
        return false;
    }

    private static InputDigitalActionHandle_t[] GameplayActionHandles =>
        new[] { _hUp, _hDown, _hLeft, _hRight, _hA, _hB, _hStart, _hSelect };

    private static InputDigitalActionHandle_t[] MenuActionHandles =>
        new[] { _hMenuUp, _hMenuDown, _hMenuLeft, _hMenuRight, _hMenuConfirm, _hMenuBack };

    /// <summary>
    /// Pure predicate, separated from the Steamworks I/O loop above — fully unit-testable
    /// without a live Steam Input session or a Steamworks type reference (takes the origin's
    /// name, not the enum itself). Origin names are stable across every controller family with
    /// a trackpad or motion sensor (SteamController_LeftPad_*/RightPad_*, PS4/PS5_*Pad_*,
    /// Switch_*Gyro_*, Neptune/Deck pad+gyro origins) — checking substrings avoids hardcoding
    /// the full EInputActionOrigin list, which Valve documents as still being extended over
    /// time. "LeftPad"/"RightPad"/"CenterPad" deliberately exclude plain "DPad_North" etc.
    /// (regular d-pad/button origins), which never match any of these.
    /// </summary>
    internal static bool IsTouchpadOrGyroOrigin(string originName) =>
        originName.Contains("LeftPad") || originName.Contains("RightPad")
            || originName.Contains("CenterPad") || originName.Contains("Gyro");

    /// <summary>
    /// The connected Steam Input controller handle at <paramref name="controllerIndex"/>
    /// (0-based, player 1 = 0), for glyph lookups (<see cref="Input.Sources.SteamInputGlyphSource"/>
    /// via <see cref="SteamInputGlyphManager"/>). Returns default when no controller is
    /// connected at that index.
    /// </summary>
    internal static InputHandle_t ControllerHandle(int controllerIndex) =>
        ControllerHandleRefreshing(controllerIndex);

    /// <summary>Refreshes _controllerBuf/_connectedCount, then reads the given slot.</summary>
    private static InputHandle_t ControllerHandleRefreshing(int controllerIndex)
    {
        if (!IsAvailable) return default;
        int count = RefreshControllers();
        return controllerIndex < count ? _controllerBuf[controllerIndex] : default;
    }

    /// <summary>Reads the given slot against the last-refreshed snapshot, without refreshing.</summary>
    private static InputHandle_t ControllerHandleCached(int controllerIndex)
    {
        if (!IsAvailable) return default;
        return controllerIndex < _connectedCount ? _controllerBuf[controllerIndex] : default;
    }

    // -- Action set handles --
    private static InputActionSetHandle_t _gameplaySet;
    private static InputActionSetHandle_t _menuSet;

    // -- Gameplay digital action handles (NES buttons) --
    private static InputDigitalActionHandle_t _hUp, _hDown, _hLeft, _hRight;
    private static InputDigitalActionHandle_t _hA, _hB, _hStart, _hSelect;

    // -- Menu navigation action handles --
    private static InputDigitalActionHandle_t _hMenuUp, _hMenuDown, _hMenuLeft, _hMenuRight;
    private static InputDigitalActionHandle_t _hMenuConfirm, _hMenuBack;

    // -- Menu nav edge detection --
    // debounceMs: 0 (disabled) — Steam Input abstracts the underlying controller/driver and
    // already handles signal cleanliness at that layer, unlike SDL3GamepadSource's direct
    // evdev-on-Linux path (see MenuNavEdgeDetector's own doc comment).
    private static readonly MenuNavEdgeDetector MenuNavDetector = new();

    public static bool IsAvailable { get; private set; }

    /// <summary>
    /// Call immediately after SteamAPI.Init() succeeds.
    /// No-op if Steam is not available.
    /// </summary>
    public static void Initialize()
    {
        if (!SteamManager.IsAvailable) return;

        try
        {
            if (!SteamInput.Init(false))
            {
                Logger.Log("[SteamInput] Init returned false — Steam Controller unavailable.");
                return;
            }

            // Cache action set handles
            _gameplaySet = SteamInput.GetActionSetHandle("Gameplay");
            _menuSet     = SteamInput.GetActionSetHandle("Menu");

            // Gameplay actions — names match the VDF "Gameplay" action set
            _hUp     = SteamInput.GetDigitalActionHandle("up");
            _hDown   = SteamInput.GetDigitalActionHandle("down");
            _hLeft   = SteamInput.GetDigitalActionHandle("left");
            _hRight  = SteamInput.GetDigitalActionHandle("right");
            _hA      = SteamInput.GetDigitalActionHandle("a_button");
            _hB      = SteamInput.GetDigitalActionHandle("b_button");
            _hStart  = SteamInput.GetDigitalActionHandle("start");
            _hSelect = SteamInput.GetDigitalActionHandle("select");

            // Menu actions — names match the VDF "Menu" action set
            _hMenuUp      = SteamInput.GetDigitalActionHandle("menu_up");
            _hMenuDown    = SteamInput.GetDigitalActionHandle("menu_down");
            _hMenuLeft    = SteamInput.GetDigitalActionHandle("menu_left");
            _hMenuRight   = SteamInput.GetDigitalActionHandle("menu_right");
            _hMenuConfirm = SteamInput.GetDigitalActionHandle("menu_confirm");
            _hMenuBack    = SteamInput.GetDigitalActionHandle("menu_back");

            IsAvailable = true;
            Logger.Log("[SteamInput] Initialized — Steam Controller support active.");
        }
        catch (Exception ex)
        {
            Logger.Log($"[SteamInput] Init exception: {ex.Message}");
        }
    }

    /// <summary>Call before SteamAPI.Shutdown().</summary>
    public static void Shutdown()
    {
        if (!IsAvailable) return;
        SteamInput.Shutdown();
        IsAvailable = false;
    }

    // ---- Action set switching ----

    /// <summary>Switches all connected Steam controllers to the Gameplay action set.</summary>
    public static void ActivateGameplaySet()
    {
        if (!IsAvailable) return;
        int count = RefreshControllers();
        for (int i = 0; i < count; i++)
            SteamInput.ActivateActionSet(_controllerBuf[i], _gameplaySet);
    }

    /// <summary>Switches all connected Steam controllers to the Menu action set.</summary>
    public static void ActivateMenuSet()
    {
        if (!IsAvailable) return;
        int count = RefreshControllers();
        for (int i = 0; i < count; i++)
            SteamInput.ActivateActionSet(_controllerBuf[i], _menuSet);
    }

    // ---- Gameplay input ----

    /// <summary>
    /// Returns active VDF action names for the connected Steam controller at
    /// <paramref name="controllerIndex"/> (0-based, player 1 = 0). Returns an empty set when
    /// Steam Input is unavailable or no controller is connected at that index. Intended to be
    /// resolved to a per-player NES button name via <see cref="NesButtonFor"/>.
    /// </summary>
    public static ImmutableHashSet<string> GetActiveActions(int controllerIndex = 0)
    {
        var h = ControllerHandleRefreshing(controllerIndex);
        if (h == default) return ImmutableHashSet<string>.Empty;

        var builder = ImmutableHashSet.CreateBuilder<string>();

        if (Digital(h, _hUp))     builder.Add("up");
        if (Digital(h, _hDown))   builder.Add("down");
        if (Digital(h, _hLeft))   builder.Add("left");
        if (Digital(h, _hRight))  builder.Add("right");
        if (Digital(h, _hA))      builder.Add("a_button");
        if (Digital(h, _hB))      builder.Add("b_button");
        if (Digital(h, _hStart))  builder.Add("start");
        if (Digital(h, _hSelect)) builder.Add("select");

        return builder.ToImmutable();
    }

    /// <summary>
    /// Returns the native controller button label for the given VDF action name, read from the
    /// controller at <paramref name="controllerIndex"/> (0-based, player 1 = 0).
    /// Queries Steam's GetDigitalActionOrigins + GetStringForActionOrigin to get the
    /// localised physical button name (e.g. "A Button", "Cross Button").
    /// Falls back to a human-readable formatting of the action name when Steam is
    /// unavailable, no controller is connected at that index, or no origin is configured.
    /// </summary>
    public static string GetNativeLabel(string actionName, int controllerIndex = 0)
    {
        string formatted = actionName switch
        {
            "up"       => "Up",
            "down"     => "Down",
            "left"     => "Left",
            "right"    => "Right",
            "a_button" => "A Button",
            "b_button" => "B Button",
            "start"    => "Start",
            "select"   => "Select",
            _          => actionName,
        };

        var h = ControllerHandleRefreshing(controllerIndex);
        if (h == default) return formatted;

        var handle = actionName switch
        {
            "up"       => _hUp,
            "down"     => _hDown,
            "left"     => _hLeft,
            "right"    => _hRight,
            "a_button" => _hA,
            "b_button" => _hB,
            "start"    => _hStart,
            "select"   => _hSelect,
            _          => default,
        };
        if (handle == default) return formatted;

        var origins = new EInputActionOrigin[Constants.STEAM_INPUT_MAX_ORIGINS];
        int n = SteamInput.GetDigitalActionOrigins(h, _gameplaySet, handle, origins);
        if (n <= 0) return formatted;

        string? native = SteamInput.GetStringForActionOrigin(origins[0]);
        return string.IsNullOrEmpty(native) ? formatted : native;
    }

    // ---- Menu navigation input ----

    /// <summary>
    /// Returns edge-triggered menu navigation from the connected Steam controller at
    /// <paramref name="controllerIndex"/> (0-based, player 1 = 0). Returns a zeroed struct when
    /// Steam Input is unavailable or no controller is connected at that index. Only ever called
    /// with the default (player 1) — menu navigation is intentionally player-1-only, mirroring
    /// SDL3GamepadSource's own instance-per-player design (every method reads its own player's
    /// slot, even the ones InputManager only ever calls on the player-1 instance).
    /// Intended to be OR-ed with XInput menu nav in InputManager.PollMenuNav().
    /// </summary>
    public static MenuNavInput GetMenuNav(int controllerIndex = 0)
    {
        var h = ControllerHandleRefreshing(controllerIndex);
        if (h == default)
        {
            MenuNavDetector.Reset();
            return default;
        }

        bool up      = Digital(h, _hMenuUp);
        bool down    = Digital(h, _hMenuDown);
        bool left    = Digital(h, _hMenuLeft);
        bool right   = Digital(h, _hMenuRight);
        bool confirm = Digital(h, _hMenuConfirm);
        bool back    = Digital(h, _hMenuBack);

        return MenuNavDetector.Advance(up, down, left, right, confirm, back);
    }

    /// <summary>
    /// Returns true if any menu-set button is currently held on the connected controller at
    /// <paramref name="controllerIndex"/> (0-based, player 1 = 0). Raw state only — no edge
    /// detection. Used by <see cref="NEShim.Input.Sources.SteamInputSource"/> to implement
    /// <see cref="NEShim.Input.IAnyButtonSource"/> with its own edge state.
    /// </summary>
    public static bool AnyMenuActionActive(int controllerIndex = 0)
    {
        var h = ControllerHandleRefreshing(controllerIndex);
        if (h == default) return false;
        return Digital(h, _hMenuUp)    || Digital(h, _hMenuDown)  ||
               Digital(h, _hMenuLeft)  || Digital(h, _hMenuRight) ||
               Digital(h, _hMenuConfirm) || Digital(h, _hMenuBack);
    }

    /// <summary>
    /// Returns raw (non-edge-triggered) held state for menu left/right on the connected
    /// controller at <paramref name="controllerIndex"/> (0-based, player 1 = 0).
    /// Used by <see cref="NEShim.Input.InputManager"/> to implement held-slider repeat.
    /// </summary>
    public static (bool Left, bool Right) GetMenuHeldLeftRight(int controllerIndex = 0)
    {
        var h = ControllerHandleRefreshing(controllerIndex);
        if (h == default) return default;
        return (Digital(h, _hMenuLeft), Digital(h, _hMenuRight));
    }

    private static int RefreshControllers()
    {
        _connectedCount = SteamInput.GetConnectedControllers(_controllerBuf);
        return _connectedCount;
    }

    private static bool Digital(InputHandle_t controller, InputDigitalActionHandle_t action)
        => SteamInput.GetDigitalActionData(controller, action).bState != 0;
}
