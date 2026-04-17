using System.Collections.Immutable;
using System.Diagnostics;
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
    // -- Controller handle buffer (reused to avoid allocation) --
    private static readonly InputHandle_t[] _controllerBuf =
        new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];

    // Cached connected controller count — updated whenever controllers are enumerated.
    // Written only on the emulation thread; read (as a cached snapshot) on the UI thread.
    private static int _connectedCount;

    /// <summary>True when at least one Steam Input controller is connected and active.</summary>
    public static bool HasConnectedController => IsAvailable && _connectedCount > 0;

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
                Debug.WriteLine("[SteamInput] Init returned false — Steam Controller unavailable.");
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
            Debug.WriteLine("[SteamInput] Initialized — Steam Controller support active.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteamInput] Init exception: {ex.Message}");
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
    /// Returns NES button names currently pressed on the first connected Steam controller.
    /// Returns an empty set when Steam Input is unavailable or no controller is connected.
    /// Intended to be OR-ed with XInput results in InputManager.PollSnapshot().
    /// </summary>
    public static ImmutableHashSet<string> GetActiveGameplayButtons()
    {
        if (!IsAvailable) return ImmutableHashSet<string>.Empty;

        int count = RefreshControllers();
        if (count == 0) return ImmutableHashSet<string>.Empty;

        var h = _controllerBuf[0];
        var builder = ImmutableHashSet.CreateBuilder<string>();

        if (Digital(h, _hUp))     builder.Add("P1 Up");
        if (Digital(h, _hDown))   builder.Add("P1 Down");
        if (Digital(h, _hLeft))   builder.Add("P1 Left");
        if (Digital(h, _hRight))  builder.Add("P1 Right");
        if (Digital(h, _hA))      builder.Add("P1 A");
        if (Digital(h, _hB))      builder.Add("P1 B");
        if (Digital(h, _hStart))  builder.Add("P1 Start");
        if (Digital(h, _hSelect)) builder.Add("P1 Select");

        return builder.ToImmutable();
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
