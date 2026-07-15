using NEShim.Config;
using NEShim.Input;
using NEShim.Localization;
using NEShim.UI;
using SDL3;

namespace NEShim.Tests.UI;

[TestFixture]
internal class GameCarouselScreenTests
{
    private static readonly LocalizationData Localization = new();

    private static GameManifest Game(string id, bool isValid = true) =>
        new(id, id.ToUpperInvariant(), SteamDlcAppId: 0, ThumbnailPath: "", IsValid: isValid);

    // Empty gamesRoot/backgroundPath keeps construction filesystem/SDL-free for pure unit tests.
    private static GameCarouselScreen NewScreen(params GameManifest[] games) =>
        new(games, gamesRoot: "", carouselBackgroundPath: "", Localization);

    // ---- Construction ----

    [Test]
    public void SelectedIndex_InitiallyZero()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void Games_ExposesConstructorList()
    {
        var games = new[] { Game("a"), Game("b") };
        var screen = new GameCarouselScreen(games, gamesRoot: "", carouselBackgroundPath: "", Localization);
        Assert.That(screen.Games, Is.EqualTo(games));
    }

    [Test]
    public void Localization_ExposesConstructorValue()
    {
        var custom = new LocalizationData { CarouselNoGamesAvailable = "Aucun jeu disponible" };
        var screen = new GameCarouselScreen([Game("a")], gamesRoot: "", carouselBackgroundPath: "", custom);
        Assert.That(screen.Localization.CarouselNoGamesAvailable, Is.EqualTo("Aucun jeu disponible"));
    }

    // ---- MoveNext / MovePrevious ----

    [Test]
    public void MoveNext_AdvancesSelectedIndex()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MoveNext();
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void MoveNext_WrapsAroundAtEnd()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MoveNext();
        screen.MoveNext();
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_WrapsAroundAtStart()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MovePrevious();
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void MoveNext_EmptyList_DoesNotThrow_StaysAtZero()
    {
        var screen = NewScreen();
        Assert.That(screen.MoveNext, Throws.Nothing);
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_EmptyList_DoesNotThrow_StaysAtZero()
    {
        var screen = NewScreen();
        Assert.That(screen.MovePrevious, Throws.Nothing);
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    // A single game has nothing to wrap to — Left/Right should be a true no-op, not a slide
    // animation that lands back on the same tile.

    [Test]
    public void MoveNext_SingleGame_DoesNotChangeSelectedIndex()
    {
        var screen = NewScreen(Game("a"));
        screen.MoveNext();
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MoveNext_SingleGame_DoesNotTriggerSlideAnimation()
    {
        var screen = NewScreen(Game("a"));
        screen.MoveNext();
        Assert.That(screen.SlideDirection, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_SingleGame_DoesNotChangeSelectedIndex()
    {
        var screen = NewScreen(Game("a"));
        screen.MovePrevious();
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_SingleGame_DoesNotTriggerSlideAnimation()
    {
        var screen = NewScreen(Game("a"));
        screen.MovePrevious();
        Assert.That(screen.SlideDirection, Is.EqualTo(0));
    }

    [Test]
    public void MoveNext_SetsSlideDirectionPositive_AndPreviousIndex()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MoveNext();
        Assert.That(screen.SlideDirection, Is.EqualTo(1));
        Assert.That(screen.PreviousSelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_SetsSlideDirectionNegative_AndPreviousIndex()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MovePrevious();
        Assert.That(screen.SlideDirection, Is.EqualTo(-1));
        Assert.That(screen.PreviousSelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MoveNext_ResetsSlideProgressToNearZero()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MoveNext();
        Assert.That(screen.SlideProgress, Is.LessThan(1f));
    }

    [Test]
    public void MoveNext_WhileDescriptionShown_ClosesDescriptionImmediately()
    {
        // If the description card is open when the player moves to a different tile, it snaps
        // closed instantly instead of animating a flip on a tile that's simultaneously sliding.
        var screen = NewScreen(Game("a"), Game("b"));
        screen.ToggleDescription();
        Assert.That(screen.DescriptionShown, Is.True);

        screen.MoveNext();

        Assert.That(screen.DescriptionShown, Is.False);
        Assert.That(screen.FlipProgress, Is.EqualTo(1f)); // settled, not mid-flip
    }

    // ---- Confirm / GameChosen ----

    [Test]
    public void Confirm_RaisesGameChosen_WithSelectedGame()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.MoveNext();
        GameManifest? chosen = null;
        screen.GameChosen += g => chosen = g;

        screen.Confirm();

        Assert.That(chosen?.GameId, Is.EqualTo("b"));
    }

    [Test]
    public void Confirm_EmptyList_DoesNotRaiseGameChosen()
    {
        var screen = NewScreen();
        bool raised = false;
        screen.GameChosen += _ => raised = true;

        screen.Confirm();

        Assert.That(raised, Is.False);
    }

    [Test]
    public void Confirm_SelectedGameInvalid_DoesNotRaiseGameChosen()
    {
        var screen = NewScreen(Game("a", isValid: false));
        bool raised = false;
        screen.GameChosen += _ => raised = true;

        screen.Confirm();

        Assert.That(raised, Is.False);
    }

    // ---- HandleKey ----

    [Test]
    public void HandleKey_Right_MovesNext_ReturnsTrue()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        bool handled = screen.HandleKey(SDL.Keycode.Right);
        Assert.That(handled, Is.True);
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_Left_MovesPrevious_ReturnsTrue()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        bool handled = screen.HandleKey(SDL.Keycode.Left);
        Assert.That(handled, Is.True);
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_Return_ConfirmsSelection()
    {
        var screen = NewScreen(Game("a"));
        GameManifest? chosen = null;
        screen.GameChosen += g => chosen = g;

        bool handled = screen.HandleKey(SDL.Keycode.Return);

        Assert.That(handled, Is.True);
        Assert.That(chosen?.GameId, Is.EqualTo("a"));
    }

    [Test]
    public void HandleKey_Up_TogglesDescriptionShown()
    {
        var screen = NewScreen(Game("a"));
        bool handled = screen.HandleKey(SDL.Keycode.Up);
        Assert.That(handled, Is.True);
        Assert.That(screen.DescriptionShown, Is.True);
    }

    [Test]
    public void HandleKey_Up_Twice_TogglesDescriptionBackOff()
    {
        var screen = NewScreen(Game("a"));
        screen.HandleKey(SDL.Keycode.Up);
        screen.HandleKey(SDL.Keycode.Up);
        Assert.That(screen.DescriptionShown, Is.False);
    }

    [Test]
    public void HandleKey_UnhandledKey_ReturnsFalse()
    {
        var screen = NewScreen(Game("a"));
        bool handled = screen.HandleKey(SDL.Keycode.F1);
        Assert.That(handled, Is.False);
    }

    [Test]
    public void HandleKey_Escape_RaisesQuitRequested()
    {
        var screen = NewScreen(Game("a"));
        bool quit = false;
        screen.QuitRequested += () => quit = true;

        bool handled = screen.HandleKey(SDL.Keycode.Escape);

        Assert.That(handled, Is.True);
        Assert.That(quit, Is.True);
    }

    [Test]
    public void HandleKey_Escape_EmptyList_StillRaisesQuitRequested()
    {
        var screen = NewScreen();
        bool quit = false;
        screen.QuitRequested += () => quit = true;

        screen.HandleKey(SDL.Keycode.Escape);

        Assert.That(quit, Is.True);
    }

    // ---- HandleGamepadNav ----

    [Test]
    public void HandleGamepadNav_Right_MovesNext()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.HandleGamepadNav(new MenuNavInput { Right = true });
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleGamepadNav_Left_MovesPrevious()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.HandleGamepadNav(new MenuNavInput { Left = true });
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleGamepadNav_Up_TogglesDescriptionShown()
    {
        var screen = NewScreen(Game("a"));
        screen.HandleGamepadNav(new MenuNavInput { Up = true });
        Assert.That(screen.DescriptionShown, Is.True);
    }

    [Test]
    public void HandleGamepadNav_Confirm_RaisesGameChosen()
    {
        var screen = NewScreen(Game("a"));
        GameManifest? chosen = null;
        screen.GameChosen += g => chosen = g;

        screen.HandleGamepadNav(new MenuNavInput { Confirm = true });

        Assert.That(chosen?.GameId, Is.EqualTo("a"));
    }

    [Test]
    public void HandleGamepadNav_Back_RaisesQuitRequested()
    {
        var screen = NewScreen(Game("a"));
        bool quit = false;
        screen.QuitRequested += () => quit = true;

        screen.HandleGamepadNav(new MenuNavInput { Back = true });

        Assert.That(quit, Is.True);
    }

    [Test]
    public void HandleGamepadNav_NoInput_DoesNotMove()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        screen.HandleGamepadNav(new MenuNavInput());
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    // ---- ComputeSlideProgress (pure) ----

    [Test]
    public void ComputeSlideProgress_ZeroElapsed_ReturnsZero()
    {
        Assert.That(GameCarouselScreen.ComputeSlideProgress(0, 220), Is.EqualTo(0f));
    }

    [Test]
    public void ComputeSlideProgress_Midpoint_ReturnsHalf()
    {
        Assert.That(GameCarouselScreen.ComputeSlideProgress(110, 220), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ComputeSlideProgress_AtOrPastDuration_ReturnsOne()
    {
        Assert.That(GameCarouselScreen.ComputeSlideProgress(220, 220), Is.EqualTo(1f));
        Assert.That(GameCarouselScreen.ComputeSlideProgress(999, 220), Is.EqualTo(1f));
    }

    // ---- ComputeFlipProgress (pure) ----

    [Test]
    public void ComputeFlipProgress_ZeroElapsed_ReturnsZero()
    {
        Assert.That(GameCarouselScreen.ComputeFlipProgress(0, 260), Is.EqualTo(0f));
    }

    [Test]
    public void ComputeFlipProgress_Midpoint_ReturnsHalf()
    {
        Assert.That(GameCarouselScreen.ComputeFlipProgress(130, 260), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ComputeFlipProgress_AtOrPastDuration_ReturnsOne()
    {
        Assert.That(GameCarouselScreen.ComputeFlipProgress(260, 260), Is.EqualTo(1f));
        Assert.That(GameCarouselScreen.ComputeFlipProgress(9999, 260), Is.EqualTo(1f));
    }

    // ---- ComputeThumbnailSize (pure) ----

    [Test]
    public void ComputeThumbnailSize_SmallerThanBounds_ReturnsUnchanged()
    {
        var (w, h) = GameCarouselScreen.ComputeThumbnailSize(400, 500, maxWidth: 700, maxHeight: 994);
        Assert.That((w, h), Is.EqualTo((400, 500)));
    }

    [Test]
    public void ComputeThumbnailSize_ExactlyAtBounds_ReturnsUnchanged()
    {
        var (w, h) = GameCarouselScreen.ComputeThumbnailSize(700, 994, maxWidth: 700, maxHeight: 994);
        Assert.That((w, h), Is.EqualTo((700, 994)));
    }

    [Test]
    public void ComputeThumbnailSize_LargerSource_ScalesDownPreservingAspectRatio()
    {
        // 1400x1988 is exactly double the 700x994 bounds — should scale by exactly 0.5.
        var (w, h) = GameCarouselScreen.ComputeThumbnailSize(1400, 1988, maxWidth: 700, maxHeight: 994);
        Assert.That((w, h), Is.EqualTo((700, 994)));
    }

    [Test]
    public void ComputeThumbnailSize_NeverUpscales()
    {
        var (w, h) = GameCarouselScreen.ComputeThumbnailSize(100, 142, maxWidth: 700, maxHeight: 994);
        Assert.That(w, Is.LessThanOrEqualTo(100));
        Assert.That(h, Is.LessThanOrEqualTo(142));
    }

    [Test]
    public void ComputeThumbnailSize_WiderThanTallSource_ClampsToWidthRatio()
    {
        // Source is proportionally much wider than the NES box's own portrait ratio — width is
        // the binding constraint even though the source's height also exceeds maxHeight.
        var (w, h) = GameCarouselScreen.ComputeThumbnailSize(2000, 1200, maxWidth: 700, maxHeight: 994);
        Assert.That(w, Is.EqualTo(700));
        Assert.That(h, Is.LessThanOrEqualTo(994));
    }

    [Test]
    public void ComputeThumbnailSize_ResultAspectRatioMatchesSource()
    {
        var (w, h) = GameCarouselScreen.ComputeThumbnailSize(3000, 4000, maxWidth: 700, maxHeight: 994);
        float sourceRatio = 3000f / 4000f;
        float resultRatio = (float)w / h;
        Assert.That(resultRatio, Is.EqualTo(sourceRatio).Within(0.01f));
    }

    // ---- TryGetThumbnail / BackgroundFrame ----
    // NewScreen's empty gamesRoot/carouselBackgroundPath means no thumbnail or background
    // surface is ever loaded (no SDL/file I/O — see NewScreen's comment), so these only
    // exercise the "nothing loaded" paths — the SDL-loading paths themselves are outside the
    // unit-test boundary (crosses the file system/SDL, same reason GameCarouselRenderer is
    // excluded from coverage — see coverage.runsettings).

    [Test]
    public void TryGetThumbnail_NoThumbnailsLoaded_ReturnsFalse()
    {
        var screen = NewScreen(Game("a"));
        bool found = screen.TryGetThumbnail("a", out var surface);
        Assert.That(found, Is.False);
        Assert.That(surface, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void BackgroundFrame_NoBackgroundConfigured_ReturnsNull()
    {
        var screen = NewScreen(Game("a"));
        Assert.That(screen.BackgroundFrame, Is.Null);
    }

    // ---- Dispose ----

    [Test]
    public void Dispose_NoThumbnailsOrBackground_DoesNotThrow()
    {
        var screen = NewScreen(Game("a"), Game("b"));
        Assert.That(screen.Dispose, Throws.Nothing);
    }
}
