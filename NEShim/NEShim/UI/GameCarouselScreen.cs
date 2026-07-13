using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Pre-game screen shown in multi-game mode before any game's config/ROM is loaded — lets the
/// player pick which installed game to play. Mirrors <see cref="LogoScreen"/>'s pattern: a
/// state object with no rendering logic of its own (paired with the stateless
/// <see cref="GameCarouselRenderer"/>), wired through the same <c>IMenuSceneProvider</c>
/// pull-scene mechanism the logo and main menu already use — and, like <see cref="LogoScreen"/>,
/// owns unmanaged SDL resources (per-game thumbnail surfaces, an optional animated background)
/// and must be disposed by its owner when replaced or torn down.
///
/// Slide (Left/Right) and flip (Up, toggling the description card) transitions are exposed as
/// progress properties computed from wall-clock elapsed time on every access — the same
/// convention <c>LogoScreen.CurrentAlpha</c> uses — backed by pure, unit-testable
/// <see cref="ComputeSlideProgress"/>/<see cref="ComputeFlipProgress"/> functions.
/// </summary>
internal sealed class GameCarouselScreen : IDisposable
{
    private const int SlideDurationMs = 220;
    private const int FlipDurationMs  = 260;

    public IReadOnlyList<GameManifest> Games { get; }
    public int SelectedIndex { get; private set; }
    public LocalizationData Localization { get; }

    /// <summary>Raised when the player confirms a selection. Never raised for an invalid entry or when Games is empty.</summary>
    public event Action<GameManifest>? GameChosen;

    /// <summary>
    /// Raised on Escape/Back. The carousel is the top-level screen in multi-game mode — there is
    /// no parent screen to return to (unlike MainMenuScreen, which only navigates Escape to a
    /// parent sub-screen and does nothing at its own top level) — so this exits the app.
    /// </summary>
    public event Action? QuitRequested;

    private readonly Dictionary<string, IntPtr> _thumbnails = new();
    private readonly AnimatedImagePlayer? _background;

    private int _slideDirection;        // -1 previous, +1 next, 0 = no transition in flight
    private int _previousSelectedIndex; // arrangement being slid FROM
    private long _slideStartTicks;

    private bool _descriptionShown;
    private long _flipStartTicks = Environment.TickCount64 - FlipDurationMs; // settled at construction time

    public GameCarouselScreen(IReadOnlyList<GameManifest> games, string gamesRoot, string carouselBackgroundPath,
        LocalizationData localization)
    {
        Games = games;
        Localization = localization;

        if (!string.IsNullOrWhiteSpace(carouselBackgroundPath))
        {
            var shellContext = new GameContext(gamesRoot, gameId: "");
            string? resolved = MainMenuScreen.ResolveAssetPath(carouselBackgroundPath, shellContext);
            if (resolved is not null)
                _background = AnimatedImagePlayer.LoadFromFile(resolved);
        }

        foreach (var game in games)
        {
            if (!game.IsValid || string.IsNullOrWhiteSpace(game.ThumbnailPath)) continue;
            var ctx = GameContext.ForGame(gamesRoot, game.GameId);
            string? resolved = MainMenuScreen.ResolveAssetPath(game.ThumbnailPath, ctx);
            if (resolved is null) continue; // missing art is not an error — renderer falls back to a placeholder

            IntPtr surface = SdlSurfaceLoader.LoadFromFile(resolved);
            if (surface != IntPtr.Zero) _thumbnails[game.GameId] = surface;
        }
    }

    public IntPtr? BackgroundFrame => _background?.CurrentFrame;

    public bool TryGetThumbnail(string gameId, out IntPtr surface) => _thumbnails.TryGetValue(gameId, out surface);

    public int SlideDirection => SlideProgress >= 1f ? 0 : _slideDirection;
    public int PreviousSelectedIndex => _previousSelectedIndex;
    public float SlideProgress => ComputeSlideProgress(Environment.TickCount64 - _slideStartTicks, SlideDurationMs);

    public bool DescriptionShown => _descriptionShown;
    public float FlipProgress => ComputeFlipProgress(Environment.TickCount64 - _flipStartTicks, FlipDurationMs);

    public void MoveNext()
    {
        if (Games.Count == 0) return;
        _previousSelectedIndex = SelectedIndex;
        SelectedIndex = (SelectedIndex + 1) % Games.Count;
        _slideDirection = +1;
        _slideStartTicks = Environment.TickCount64;
        CloseDescriptionImmediately();
    }

    public void MovePrevious()
    {
        if (Games.Count == 0) return;
        _previousSelectedIndex = SelectedIndex;
        SelectedIndex = (SelectedIndex - 1 + Games.Count) % Games.Count;
        _slideDirection = -1;
        _slideStartTicks = Environment.TickCount64;
        CloseDescriptionImmediately();
    }

    public void ToggleDescription()
    {
        if (Games.Count == 0) return;
        _descriptionShown = !_descriptionShown;
        _flipStartTicks = Environment.TickCount64;
    }

    public void Confirm()
    {
        if (Games.Count == 0) return;
        var game = Games[SelectedIndex];
        if (!game.IsValid)
        {
            Logger.Log($"[GameCarouselScreen] Ignoring Confirm on invalid entry '{game.GameId}': {game.ValidationError}");
            return;
        }
        GameChosen?.Invoke(game);
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

            case SDL.Keycode.Up:
                ToggleDescription();
                return true;

            case SDL.Keycode.Return:
            case SDL.Keycode.Z:
            case SDL.Keycode.Space:
                Confirm();
                return true;

            case SDL.Keycode.Escape:
                QuitRequested?.Invoke();
                return true;
        }
        return false;
    }

    public void HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (!nav.Any) return;
        if (nav.Left)    MovePrevious();
        if (nav.Right)   MoveNext();
        if (nav.Up)      ToggleDescription();
        if (nav.Confirm) Confirm();
        if (nav.Back)    QuitRequested?.Invoke();
    }

    // If the description card is open when the player moves to a different tile, snap it
    // closed instantly rather than animating a flip on a tile that's simultaneously sliding.
    private void CloseDescriptionImmediately()
    {
        if (!_descriptionShown) return;
        _descriptionShown = false;
        _flipStartTicks = Environment.TickCount64 - FlipDurationMs;
    }

    internal static float ComputeSlideProgress(long elapsedMs, int durationMs) =>
        Math.Clamp(durationMs <= 0 ? 1f : (float)elapsedMs / durationMs, 0f, 1f);

    internal static float ComputeFlipProgress(long elapsedMs, int durationMs) =>
        Math.Clamp(durationMs <= 0 ? 1f : (float)elapsedMs / durationMs, 0f, 1f);

    public void Dispose()
    {
        _background?.Dispose();
        foreach (var surface in _thumbnails.Values) SDL.DestroySurface(surface);
        _thumbnails.Clear();
    }
}
