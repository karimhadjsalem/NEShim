using NEShim.Config;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Pre-game screen shown in multi-game mode before any game's config/ROM is loaded — lets the
/// player pick which installed game to play. Mirrors <see cref="LogoScreen"/>'s pattern: a
/// lightweight, config-independent state object with no rendering logic of its own (paired
/// with the stateless <see cref="GameCarouselRenderer"/>), wired through the same
/// <c>IMenuSceneProvider</c> pull-scene mechanism the logo and main menu already use.
/// </summary>
internal sealed class GameCarouselScreen
{
    public IReadOnlyList<GameManifest> Games { get; }
    public int SelectedIndex { get; private set; }

    /// <summary>Raised when the player confirms a selection. Never raised when Games is empty.</summary>
    public event Action<GameManifest>? GameChosen;

    public GameCarouselScreen(IReadOnlyList<GameManifest> games) => Games = games;

    public void MoveNext()
    {
        if (Games.Count > 0) SelectedIndex = (SelectedIndex + 1) % Games.Count;
    }

    public void MovePrevious()
    {
        if (Games.Count > 0) SelectedIndex = (SelectedIndex - 1 + Games.Count) % Games.Count;
    }

    public void Confirm()
    {
        if (Games.Count > 0) GameChosen?.Invoke(Games[SelectedIndex]);
    }

    public bool HandleKey(SDL.Keycode key)
    {
        switch (key)
        {
            case SDL.Keycode.Left:
                MovePrevious();
                return true;

            case SDL.Keycode.Right:
                MoveNext();
                return true;

            case SDL.Keycode.Return:
            case SDL.Keycode.Z:
            case SDL.Keycode.Space:
                Confirm();
                return true;
        }
        return false;
    }

    public void HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (!nav.Any) return;
        if (nav.Left)    MovePrevious();
        if (nav.Right)   MoveNext();
        if (nav.Confirm) Confirm();
    }
}
