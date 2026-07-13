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
        var custom = new LocalizationData { CarouselSelectGame = "Choisir un jeu" };
        var screen = new GameCarouselScreen([Game("a")], gamesRoot: "", carouselBackgroundPath: "", custom);
        Assert.That(screen.Localization.CarouselSelectGame, Is.EqualTo("Choisir un jeu"));
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
}
