using System.Drawing;
using System.Windows.Forms;
using NEShim.Rendering;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the in-game pause menu.
/// Rendering is delegated to MenuRenderer; this class owns navigation and actions.
/// </summary>
internal sealed class InGameMenu
{
    public enum Screen { Root, SaveSlots, LoadSlots, Options }

    private readonly SaveStateManager _saveStates;
    private readonly Action _onExitToDesktop;
    private readonly Action<bool> _onWindowModeToggle; // true = fullscreen
    private readonly Action<Screen> _onScreenChanged;

    public bool IsOpen      { get; private set; }
    public Screen Current   { get; private set; } = Screen.Root;
    public int SelectedItem { get; private set; } = 0;

    // Frozen frame shown as blurred background when menu is open
    public int[]? FrozenFrame { get; private set; }

    // Callbacks wired by EmulationThread
    public event Action? Opened;
    public event Action? Closed;

    public InGameMenu(
        SaveStateManager saveStates,
        Action onExitToDesktop,
        Action<bool> onWindowModeToggle,
        Action<Screen> onScreenChanged)
    {
        _saveStates          = saveStates;
        _onExitToDesktop     = onExitToDesktop;
        _onWindowModeToggle  = onWindowModeToggle;
        _onScreenChanged     = onScreenChanged;
    }

    // ---- Root menu items ----
    public static readonly string[] RootItems = { "Resume", "Save State", "Load State", "Options", "Exit to Desktop" };

    // ---- Open / Close ----

    public void Open(int[] frozenFrame)
    {
        if (IsOpen) return;
        FrozenFrame   = frozenFrame;
        IsOpen        = true;
        Current       = Screen.Root;
        SelectedItem  = 0;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Closed?.Invoke();
    }

    // ---- Input (called on UI thread from KeyDown) ----

    public bool HandleKey(Keys key)
    {
        if (!IsOpen) return false;

        switch (key)
        {
            case Keys.Escape:
                if (Current == Screen.Root)
                    Close();
                else
                    NavigateTo(Screen.Root);
                return true;

            case Keys.Up:
                SelectedItem = Math.Max(0, SelectedItem - 1);
                return true;

            case Keys.Down:
                SelectedItem = Math.Min(ItemCount() - 1, SelectedItem + 1);
                return true;

            case Keys.Return:
            case Keys.Z:
            case Keys.Space:
                Activate();
                return true;
        }
        return false;
    }

    private int ItemCount() => Current switch
    {
        Screen.Root       => RootItems.Length,
        Screen.SaveSlots  => SaveStateManager.SlotCount,
        Screen.LoadSlots  => SaveStateManager.SlotCount,
        Screen.Options    => 2,
        _ => 1
    };

    private void NavigateTo(Screen screen)
    {
        Current      = screen;
        SelectedItem = 0;
        _onScreenChanged(screen);
    }

    private void Activate()
    {
        switch (Current)
        {
            case Screen.Root:
                switch (SelectedItem)
                {
                    case 0: Close();                         break; // Resume
                    case 1: NavigateTo(Screen.SaveSlots);   break;
                    case 2: NavigateTo(Screen.LoadSlots);   break;
                    case 3: NavigateTo(Screen.Options);     break;
                    case 4: _onExitToDesktop();              break;
                }
                break;

            case Screen.SaveSlots:
                _saveStates.SaveSlot(SelectedItem);
                _saveStates.ActiveSlot = SelectedItem;
                NavigateTo(Screen.Root);
                break;

            case Screen.LoadSlots:
                if (_saveStates.LoadSlot(SelectedItem))
                {
                    _saveStates.ActiveSlot = SelectedItem;
                    Close();
                }
                break;

            case Screen.Options:
                if (SelectedItem == 0)
                    _onWindowModeToggle(true);   // Toggle fullscreen
                else if (SelectedItem == 1)
                    _onWindowModeToggle(false);  // Toggle windowed
                break;
        }
    }

    // ---- Rendering info accessors ----

    public string[] GetCurrentItems()
    {
        return Current switch
        {
            Screen.Root => RootItems,
            Screen.SaveSlots => Enumerable.Range(0, SaveStateManager.SlotCount)
                .Select(i => _saveStates.SlotLabel(i)).ToArray(),
            Screen.LoadSlots => Enumerable.Range(0, SaveStateManager.SlotCount)
                .Select(i => _saveStates.SlotLabel(i)).ToArray(),
            Screen.Options => new[] { "Fullscreen", "Windowed" },
            _ => Array.Empty<string>()
        };
    }

    public string GetTitle() => Current switch
    {
        Screen.Root       => "PAUSED",
        Screen.SaveSlots  => "SAVE STATE",
        Screen.LoadSlots  => "LOAD STATE",
        Screen.Options    => "OPTIONS",
        _ => ""
    };

    /// <summary>Renders the menu overlay onto the given Graphics context.</summary>
    public void Render(Graphics g, Rectangle bounds)
    {
        MenuRenderer.Draw(g, bounds, this);
    }
}
