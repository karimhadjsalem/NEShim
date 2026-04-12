using System.Drawing;
using System.Windows.Forms;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu shown at application startup.
/// The emulation thread stays paused until the user makes a selection.
/// </summary>
internal sealed class MainMenuScreen : IDisposable
{
    private enum Item { NewGame = 0, ResumeGame = 1, Exit = 2 }

    private const int ItemCount = 3;

    public bool   IsVisible { get; private set; } = true;
    public bool   CanResume { get; }
    public Bitmap? Background { get; }

    // Rendering accessors
    public int SelectedIndex => (int)_selected;
    public bool IsItemEnabled(int idx) => idx != (int)Item.ResumeGame || CanResume;

    public static readonly string[] Items = { "New Game", "Resume Game", "Exit" };

    private Item _selected = Item.NewGame;

    // Fired on the UI thread when the user makes a selection
    public event Action? NewGameChosen;
    public event Action? ResumeChosen;
    public event Action? ExitChosen;

    public MainMenuScreen(bool canResume, string? bgImagePath)
    {
        CanResume = canResume;

        if (!string.IsNullOrWhiteSpace(bgImagePath))
        {
            string resolved = Path.IsPathRooted(bgImagePath)
                ? bgImagePath
                : Path.Combine(AppContext.BaseDirectory, bgImagePath);

            if (File.Exists(resolved))
            {
                try { Background = new Bitmap(resolved); }
                catch { /* leave null */ }
            }
        }

        // If can't resume, start selection on New Game (already 0, no-op but explicit)
        _selected = Item.NewGame;
    }

    /// <summary>
    /// Handle a key while the main menu is visible.
    /// Returns true if the key was consumed.
    /// </summary>
    public bool HandleKey(Keys key)
    {
        if (!IsVisible) return false;

        switch (key)
        {
            case Keys.Up:
                Navigate(-1);
                return true;

            case Keys.Down:
                Navigate(1);
                return true;

            case Keys.Return:
            case Keys.Space:
            case Keys.Z:
                Activate();
                return true;

            case Keys.Escape:
                // Escape on the main menu = exit
                IsVisible = false;
                ExitChosen?.Invoke();
                return true;
        }

        return false;
    }

    private void Navigate(int dir)
    {
        int cur = (int)_selected;
        for (int attempt = 0; attempt < ItemCount; attempt++)
        {
            cur = ((cur + dir) % ItemCount + ItemCount) % ItemCount;
            if (IsItemEnabled(cur))
            {
                _selected = (Item)cur;
                return;
            }
        }
        // All items disabled (shouldn't happen — New Game always enabled)
    }

    private void Activate()
    {
        if (!IsItemEnabled((int)_selected)) return;

        IsVisible = false;

        switch (_selected)
        {
            case Item.NewGame:    NewGameChosen?.Invoke(); break;
            case Item.ResumeGame: ResumeChosen?.Invoke();  break;
            case Item.Exit:       ExitChosen?.Invoke();    break;
        }
    }

    public void Dispose() => Background?.Dispose();
}
