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

    // Every valid game's thumbnail is loaded up front (not just the currently visible ones),
    // and each is re-blitted every frame while its tile is on screen — capping the loaded
    // resolution keeps both the one-time GPU upload cost and steady-state texture memory bounded
    // regardless of how large a publisher's source art actually is, since the filmstrip never
    // displays a tile larger than a few hundred pixels wide anyway. Matches the "high-resolution
    // / 4K carousels" recommended canvas in the publishing docs (700x994 — exactly the NES box's
    // 1.42:1 aspect ratio); never upscales art that's already smaller than this.
    private const int ThumbnailMaxWidth  = 700;
    private const int ThumbnailMaxHeight = 994;

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
    private readonly Action<IntPtr>? _onSurfaceDisposing;

    private int _slideDirection;        // -1 previous, +1 next, 0 = no transition in flight
    private int _previousSelectedIndex; // arrangement being slid FROM
    private long _slideStartTicks;

    private bool _descriptionShown;
    private long _flipStartTicks = Environment.TickCount64 - FlipDurationMs; // settled at construction time

    /// <param name="onSurfaceDisposing">
    /// Called for each thumbnail/background-frame surface right before it's destroyed (see
    /// Dispose) — the owner must forward this to IFrameRenderer.InvalidateSurfaceTexture so the
    /// overlay paint context's cached GPU texture for that surface is evicted too, before a
    /// later, unrelated surface can be allocated at the same (recycled) address. Optional so
    /// tests that never touch SDL rendering can omit it.
    /// </param>
    /// <param name="viewportWidth">Current window/viewport pixel width, paired with
    /// <paramref name="viewportHeight"/> — the background is stretched to fill it, so a decoded
    /// frame is never held or re-uploaded at a resolution larger than what will ever actually be
    /// displayed (unlike thumbnails, the background has no fixed max display size to cap
    /// against, since it can legitimately fill anything from a 720p window to a 4K one — the cap
    /// has to track the real render target instead). &lt;= 0 disables the cap (tests that never
    /// load a real background can safely omit these).</param>
    /// <param name="viewportHeight">See <paramref name="viewportWidth"/>.</param>
    public GameCarouselScreen(IReadOnlyList<GameManifest> games, string gamesRoot, string carouselBackgroundPath,
        LocalizationData localization, Action<IntPtr>? onSurfaceDisposing = null,
        int viewportWidth = 0, int viewportHeight = 0)
    {
        Games = games;
        Localization = localization;
        _onSurfaceDisposing = onSurfaceDisposing;

        if (!string.IsNullOrWhiteSpace(carouselBackgroundPath))
        {
            var shellContext = new GameContext(gamesRoot, gameId: "");
            string? resolved = MainMenuScreen.ResolveAssetPath(carouselBackgroundPath, shellContext);
            if (resolved is not null)
            {
                int? maxWidth  = viewportWidth  > 0 ? viewportWidth  : null;
                int? maxHeight = viewportHeight > 0 ? viewportHeight : null;
                _background = AnimatedImagePlayer.LoadFromFile(resolved, onSurfaceDisposing, maxWidth, maxHeight);
            }
        }

        foreach (var game in games)
        {
            if (!game.IsValid || string.IsNullOrWhiteSpace(game.ThumbnailPath)) continue;
            var ctx = GameContext.ForGame(gamesRoot, game.GameId);
            string? resolved = MainMenuScreen.ResolveAssetPath(game.ThumbnailPath, ctx);
            if (resolved is null) continue; // missing art is not an error — renderer falls back to a placeholder

            IntPtr surface = LoadThumbnail(resolved);
            if (surface != IntPtr.Zero) _thumbnails[game.GameId] = surface;
        }
    }

    /// <summary>
    /// Contain-fits source dimensions within maxWidth/maxHeight, preserving aspect ratio.
    /// Returns the source dimensions unchanged when they're already within bounds — never upscales.
    /// </summary>
    internal static (int width, int height) ComputeThumbnailSize(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
    {
        if (sourceWidth <= maxWidth && sourceHeight <= maxHeight) return (sourceWidth, sourceHeight);

        float scale = Math.Min((float)maxWidth / sourceWidth, (float)maxHeight / sourceHeight);
        return (Math.Max(1, (int)(sourceWidth * scale)), Math.Max(1, (int)(sourceHeight * scale)));
    }

    /// <summary>Loads a thumbnail and downscales it to fit within ThumbnailMaxWidth/Height if the source art is larger (never upscales).</summary>
    private static IntPtr LoadThumbnail(string path)
    {
        IntPtr surface = SdlSurfaceLoader.LoadFromFile(path);
        if (surface == IntPtr.Zero) return IntPtr.Zero;

        var (width, height) = SDL3PaintContext.GetSurfaceSize(surface);
        var (scaledWidth, scaledHeight) = ComputeThumbnailSize(width, height, ThumbnailMaxWidth, ThumbnailMaxHeight);
        if (scaledWidth == width && scaledHeight == height) return surface;

        IntPtr scaled = SDL.CreateSurface(scaledWidth, scaledHeight, SDL.PixelFormat.ARGB8888);
        if (scaled == IntPtr.Zero) return surface; // fall back to the full-size original

        var destRect = new SDL.Rect { X = 0, Y = 0, W = scaledWidth, H = scaledHeight };
        SDL.BlitSurfaceScaled(surface, IntPtr.Zero, scaled, in destRect, SDL.ScaleMode.Linear);
        SDL.DestroySurface(surface);
        return scaled;
    }

    public IntPtr? BackgroundFrame => _background?.CurrentFrame;

    public bool TryGetThumbnail(string gameId, out IntPtr surface) => _thumbnails.TryGetValue(gameId, out surface);

    public int SlideDirection => SlideProgress >= 1f ? 0 : _slideDirection;
    public int PreviousSelectedIndex => _previousSelectedIndex;
    public float SlideProgress => ComputeSlideProgress(Environment.TickCount64 - _slideStartTicks, SlideDurationMs);

    public bool DescriptionShown => _descriptionShown;
    public float FlipProgress => ComputeFlipProgress(Environment.TickCount64 - _flipStartTicks, FlipDurationMs);

    // A single game has nothing to wrap to — Left/Right is a no-op rather than a slide
    // animation that lands back on the same tile.
    public void MoveNext()
    {
        if (Games.Count <= 1) return;
        _previousSelectedIndex = SelectedIndex;
        SelectedIndex = (SelectedIndex + 1) % Games.Count;
        _slideDirection = +1;
        _slideStartTicks = Environment.TickCount64;
        CloseDescriptionImmediately();
    }

    public void MovePrevious()
    {
        if (Games.Count <= 1) return;
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
            Logger.Log($"[GameCarouselScreen] Ignoring Confirm on invalid entry '{game.GameId}' — see neshim.log for the reason (GameScanner).");
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
        foreach (var surface in _thumbnails.Values)
        {
            _onSurfaceDisposing?.Invoke(surface);
            SDL.DestroySurface(surface);
        }
        _thumbnails.Clear();
    }
}
