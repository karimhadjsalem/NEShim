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
    // -- VDF action ↔ NES button constant tables --
    // Fixed by game_actions_<appid>.vdf; not user-configurable.

    /// <summary>Maps VDF action names to NES button names for input polling.</summary>
    public static readonly IReadOnlyDictionary<string, string> ActionToNesButton =
        new Dictionary<string, string>
        {
            ["up"]       = "P1 Up",
            ["down"]     = "P1 Down",
            ["left"]     = "P1 Left",
            ["right"]    = "P1 Right",
            ["a_button"] = "P1 A",
            ["b_button"] = "P1 B",
            ["start"]    = "P1 Start",
            ["select"]   = "P1 Select",
        };

    /// <summary>Reverse of ActionToNesButton — used for label lookups in the binding menu.</summary>
    public static readonly IReadOnlyDictionary<string, string> NesButtonToAction =
        new Dictionary<string, string>
        {
            ["P1 Up"]     = "up",
            ["P1 Down"]   = "down",
            ["P1 Left"]   = "left",
            ["P1 Right"]  = "right",
            ["P1 A"]      = "a_button",
            ["P1 B"]      = "b_button",
            ["P1 Start"]  = "start",
            ["P1 Select"] = "select",
        };

    // -- Controller handle buffer (reused to avoid allocation) --
    private static readonly InputHandle_t[] _controllerBuf =
        new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];

    // Cached connected controller count — updated whenever controllers are enumerated.
    // Written only on the emulation thread; read (as a cached snapshot) on the UI thread.
    private static int _connectedCount;

    /// <summary>True when at least one Steam Input controller is connected and active.</summary>
    public static bool HasConnectedController => IsAvailable && _connectedCount > 0;

    /// <summary>
    /// True when the connected controller uses native Steam digital action bindings
    /// (e.g. PS4/PS5/Switch Pro with a full action-binding VDF). False when the VDF
    /// uses XInput passthrough, in which case Gameplay action handles have no origins
    /// and all input flows through XInput instead.
    /// </summary>
    public static bool IsUsingNativeActions()
    {
        if (!IsAvailable || _connectedCount == 0) return false;
        var h = _controllerBuf[0];
        var origins = new EInputActionOrigin[Constants.STEAM_INPUT_MAX_ORIGINS];
        return SteamInput.GetDigitalActionOrigins(h, _gameplaySet, _hA, origins) > 0;
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
    private static bool _prevMenuUp, _prevMenuDown, _prevMenuLeft, _prevMenuRight;
    private static bool _prevMenuConfirm, _prevMenuBack;

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
    /// Returns active VDF action names for the first connected Steam controller.
    /// Returns an empty set when Steam Input is unavailable or no controller is connected.
    /// Intended to be resolved via SteamInputManager.ActionToNesButton in InputManager.PollSnapshot().
    /// </summary>
    public static ImmutableHashSet<string> GetActiveActions()
    {
        if (!IsAvailable) return ImmutableHashSet<string>.Empty;

        int count = RefreshControllers();
        if (count == 0) return ImmutableHashSet<string>.Empty;

        var h = _controllerBuf[0];
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
    /// Returns the native controller button label for the given VDF action name.
    /// Queries Steam's GetDigitalActionOrigins + GetStringForActionOrigin to get the
    /// localised physical button name (e.g. "A Button", "Cross Button").
    /// Falls back to a human-readable formatting of the action name when Steam is
    /// unavailable, no controller is connected, or no origin is configured.
    /// </summary>
    public static string GetNativeLabel(string actionName)
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

        if (!IsAvailable) return formatted;
        int count = RefreshControllers();
        if (count == 0) return formatted;

        var h = _controllerBuf[0];
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
    /// Returns edge-triggered menu navigation from the first connected Steam controller.
    /// Returns a zeroed struct when Steam Input is unavailable or no controller is connected.
    /// Intended to be OR-ed with XInput menu nav in InputManager.PollMenuNav().
    /// </summary>
    public static MenuNavInput GetMenuNav()
    {
        if (!IsAvailable) return default;

        int count = RefreshControllers();
        if (count == 0)
        {
            _prevMenuUp = _prevMenuDown = _prevMenuLeft = _prevMenuRight = false;
            _prevMenuConfirm = _prevMenuBack = false;
            return default;
        }

        var h = _controllerBuf[0];
        bool up      = Digital(h, _hMenuUp);
        bool down    = Digital(h, _hMenuDown);
        bool left    = Digital(h, _hMenuLeft);
        bool right   = Digital(h, _hMenuRight);
        bool confirm = Digital(h, _hMenuConfirm);
        bool back    = Digital(h, _hMenuBack);

        var nav = new MenuNavInput
        {
            Up      = up      && !_prevMenuUp,
            Down    = down    && !_prevMenuDown,
            Left    = left    && !_prevMenuLeft,
            Right   = right   && !_prevMenuRight,
            Confirm = confirm && !_prevMenuConfirm,
            Back    = back    && !_prevMenuBack,
        };

        _prevMenuUp      = up;
        _prevMenuDown    = down;
        _prevMenuLeft    = left;
        _prevMenuRight   = right;
        _prevMenuConfirm = confirm;
        _prevMenuBack    = back;

        return nav;
    }

    private static int RefreshControllers()
    {
        _connectedCount = SteamInput.GetConnectedControllers(_controllerBuf);
        return _connectedCount;
    }

    private static bool Digital(InputHandle_t controller, InputDigitalActionHandle_t action)
        => SteamInput.GetDigitalActionData(controller, action).bState != 0;
}
