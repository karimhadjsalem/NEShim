using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input.Sources;

/// <summary>
/// Wraps the static SteamInputManager for per-frame gameplay and menu input.
///
/// Scope boundary: this class replaces only the four SteamInputManager call sites
/// that were inline in InputManager (GetActiveActions, ActionToNesButton loop,
/// HasConnectedController check, GetMenuNav call). All lifecycle methods
/// (Initialize, Shutdown, ActivateMenuSet/GameplaySet) and UI helpers
/// (GetNativeLabel, IsUsingNativeActions in menus) remain as direct static calls
/// from their existing call sites and are not touched by this refactor.
///
/// Critical call order: GetActiveActions() must run before IsUsingNativeActions()
/// in the same frame because it refreshes the internal _controllerBuf that
/// IsUsingNativeActions reads.
/// </summary>
internal sealed class SteamInputSource : IInputSource, IMenuNavSource, IAnyButtonSource
{
    private bool   _lastAvailable;
    private bool   _prevNative;
    private string _lastLoggedActions = "";
    private bool   _prevAnyActive;

    public bool IsAvailable => _lastAvailable;

    public IReadOnlySet<string> GetActiveIdentifiers(AppConfig config)
    {
        if (!Steam.SteamInputManager.IsAvailable)
        {
            _lastAvailable = false;
            return new HashSet<string>();
        }

        // GetActiveActions refreshes _controllerBuf — must come before IsUsingNativeActions.
        var actions = Steam.SteamInputManager.GetActiveActions();

        bool native = Steam.SteamInputManager.HasConnectedController
                   && Steam.SteamInputManager.IsUsingNativeActions();
        _lastAvailable = native;

        if (native != _prevNative)
        {
            if (native)
                Logger.Log($"[Steam] Controller switched to native actions. Connected={Steam.SteamInputManager.HasConnectedController}");
            else
                Logger.Log($"[Steam] Controller switched to XInput passthrough. Connected={Steam.SteamInputManager.HasConnectedController}");
            _prevNative = native;
        }

        // When the controller is in XInput-passthrough mode, Steam reports no native
        // action origins and XInput handles all button input. Return empty to prevent
        // double-reporting the same press through both paths.
        if (!native)
            return new HashSet<string>();

        if (Logger.IsEnabled)
        {
            // Log active Steam actions on change so the user can see what the action set
            // is reporting (useful for diagnosing missing analog stick mappings).
            string sorted = string.Join(",", actions.OrderBy(x => x));
            if (sorted != _lastLoggedActions)
            {
                Logger.Log($"[Steam] Active actions ({actions.Count}): {(actions.Count > 0 ? sorted : "none")}");
                _lastLoggedActions = sorted;
            }
        }

        return actions;
    }

    public MenuNavInput GetMenuNav(AppConfig config)
        => Steam.SteamInputManager.GetMenuNav();

    // ── IAnyButtonSource ───────────────────────────────────────────────────────

    public bool AnyJustPressed()
    {
        bool anyNow = Steam.SteamInputManager.AnyMenuActionActive();
        bool result = anyNow && !_prevAnyActive;
        _prevAnyActive = anyNow;
        return result;
    }
}
