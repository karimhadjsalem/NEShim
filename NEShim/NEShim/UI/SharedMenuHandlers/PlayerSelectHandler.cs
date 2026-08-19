namespace NEShim.UI;

/// <summary>
/// Lists every player's Gamepad/Keyboard Bindings entries, grouped as gamepad rows (players
/// 1..PlayerCount) followed by a divider then keyboard rows — player 1's entries are included
/// here too (in addition to staying reachable directly from Settings' own "Gamepad Controls"/
/// "Keyboard Controls" items, unchanged) so this screen is the one place to see every player's
/// controls together. Reachable only via Settings' "Player Controls" item, itself only shown when
/// <c>Menu.Config.PlayerCount &gt; 1</c> (see <see cref="SettingsHandler"/>) — mirrors the existing
/// conditional-item-count pattern used for "Change Game" (<c>MainMenuHandlers/MainHandler.cs</c>,
/// <c>InGameMenuHandlers/RootHandler.cs</c>), just applied to a dedicated screen instead of an
/// inline item, since up to 4 players x 2 rows each would otherwise flood the flat Settings list.
/// </summary>
internal sealed class PlayerSelectHandler : SharedScreenHandler
{
    public PlayerSelectHandler(IMenuHost menu) : base(menu) { }

    private int PlayerCount => Menu.Config.PlayerCount;

    // Keyboard rows beyond player 1 are hidden by default (AppConfig.HideKeyboardControlsForExtraPlayers,
    // publisher-only developer setting) — a shared keyboard rarely serves more than one local
    // player, so most publishers only want the gamepad rows for players 2-4 visible here. Player
    // 1's own keyboard row is never hidden by this flag (it only hides entries "beyond player 1").
    private bool ShowKeyboardBeyondPlayer1 => !Menu.Config.HideKeyboardControlsForExtraPlayers;

    private int GamepadRowCount  => PlayerCount;
    private int KeyboardRowCount => ShowKeyboardBeyondPlayer1 ? PlayerCount : 1;

    public override string Title => Menu.Localization.PlayerControlsTitle;

    public override int ItemCount => GamepadRowCount + KeyboardRowCount + 1; // + Back

    /// <summary>Divider between the gamepad group and the keyboard group — always present since
    /// both groups are always non-empty (gamepad: every player; keyboard: at least player 1).</summary>
    public override int SeparatorIndex => GamepadRowCount;

    public override string[] GetItems()
    {
        var items = new string[ItemCount];

        for (int player = 1; player <= GamepadRowCount; player++)
            items[player - 1] = $"{string.Format(Menu.Localization.PlayerLabel, player)}: {Menu.Localization.SettingsGamepad}";

        for (int player = 1; player <= KeyboardRowCount; player++)
            items[GamepadRowCount + player - 1] = $"{string.Format(Menu.Localization.PlayerLabel, player)}: {Menu.Localization.SettingsKeyboard}";

        items[^1] = Menu.Localization.Back;
        return items;
    }

    public override void Activate(int index)
    {
        if (index == ItemCount - 1)
        {
            Menu.NavigateTo(Screen.Settings);
            return;
        }

        if (index < GamepadRowCount)
        {
            Menu.NavigateTo(GamepadScreenFor(index + 1));
            return;
        }

        int keyboardPlayer = index - GamepadRowCount + 1;
        Menu.NavigateTo(KeyboardScreenFor(keyboardPlayer));
    }

    private static Screen GamepadScreenFor(int player) => player switch
    {
        1 => Screen.GamepadBindings,
        2 => Screen.GamepadBindingsP2,
        3 => Screen.GamepadBindingsP3,
        _ => Screen.GamepadBindingsP4,
    };

    private static Screen KeyboardScreenFor(int player) => player switch
    {
        1 => Screen.KeyboardBindings,
        2 => Screen.KeyboardBindingsP2,
        3 => Screen.KeyboardBindingsP3,
        _ => Screen.KeyboardBindingsP4,
    };
}
