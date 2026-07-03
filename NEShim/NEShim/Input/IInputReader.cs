using NEShim.Config;

namespace NEShim.Input;

internal interface IInputReader
{
    InputSnapshot PollSnapshot(AppConfig config);
    void          AdvanceHotkeyState();
    MenuNavInput  PollMenuNav(AppConfig config);
    string?       PollAnyGamepadButtonPressed();
    bool          ConsumeGamepadDisconnect();
    bool          IsAnyInputJustPressed();
    bool          IsEscJustPressed();
    bool          IsGamepadStartJustPressed();
    bool          IsGamepadHotkeyJustPressed(string action, AppConfig config);
    bool          IsHotkeyJustPressed(string action, AppConfig config);
}
