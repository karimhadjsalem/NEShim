using NEShim.Config;
using NEShim.Input;
using NEShim.UI;
using SDL3;

namespace NEShim.Tests.UI;

[TestFixture]
internal class GameCarouselScreenTests
{
    private static GameManifest Game(string id) => new(id, id.ToUpperInvariant(), SteamDlcAppId: 0, ThumbnailPath: "");

    // ---- Construction ----

    [Test]
    public void SelectedIndex_InitiallyZero()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void Games_ExposesConstructorList()
    {
        var games = new[] { Game("a"), Game("b") };
        var screen = new GameCarouselScreen(games);
        Assert.That(screen.Games, Is.EqualTo(games));
    }

    // ---- MoveNext / MovePrevious ----

    [Test]
    public void MoveNext_AdvancesSelectedIndex()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        screen.MoveNext();
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void MoveNext_WrapsAroundAtEnd()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        screen.MoveNext();
        screen.MoveNext();
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_WrapsAroundAtStart()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        screen.MovePrevious();
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void MoveNext_EmptyList_DoesNotThrow_StaysAtZero()
    {
        var screen = new GameCarouselScreen([]);
        Assert.That(screen.MoveNext, Throws.Nothing);
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void MovePrevious_EmptyList_DoesNotThrow_StaysAtZero()
    {
        var screen = new GameCarouselScreen([]);
        Assert.That(screen.MovePrevious, Throws.Nothing);
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    // ---- Confirm / GameChosen ----

    [Test]
    public void Confirm_RaisesGameChosen_WithSelectedGame()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        screen.MoveNext();
        GameManifest? chosen = null;
        screen.GameChosen += g => chosen = g;

        screen.Confirm();

        Assert.That(chosen?.GameId, Is.EqualTo("b"));
    }

    [Test]
    public void Confirm_EmptyList_DoesNotRaiseGameChosen()
    {
        var screen = new GameCarouselScreen([]);
        bool raised = false;
        screen.GameChosen += _ => raised = true;

        screen.Confirm();

        Assert.That(raised, Is.False);
    }

    // ---- HandleKey ----

    [Test]
    public void HandleKey_Right_MovesNext_ReturnsTrue()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        bool handled = screen.HandleKey(SDL.Keycode.Right);
        Assert.That(handled, Is.True);
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_Left_MovesPrevious_ReturnsTrue()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        bool handled = screen.HandleKey(SDL.Keycode.Left);
        Assert.That(handled, Is.True);
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_Return_ConfirmsSelection()
    {
        var screen = new GameCarouselScreen([Game("a")]);
        GameManifest? chosen = null;
        screen.GameChosen += g => chosen = g;

        bool handled = screen.HandleKey(SDL.Keycode.Return);

        Assert.That(handled, Is.True);
        Assert.That(chosen?.GameId, Is.EqualTo("a"));
    }

    [Test]
    public void HandleKey_UnhandledKey_ReturnsFalse()
    {
        var screen = new GameCarouselScreen([Game("a")]);
        bool handled = screen.HandleKey(SDL.Keycode.F1);
        Assert.That(handled, Is.False);
    }

    // ---- HandleGamepadNav ----

    [Test]
    public void HandleGamepadNav_Right_MovesNext()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        screen.HandleGamepadNav(new MenuNavInput { Right = true });
        Assert.That(screen.SelectedIndex, Is.EqualTo(1));
    }

    [Test]
    public void HandleGamepadNav_Confirm_RaisesGameChosen()
    {
        var screen = new GameCarouselScreen([Game("a")]);
        GameManifest? chosen = null;
        screen.GameChosen += g => chosen = g;

        screen.HandleGamepadNav(new MenuNavInput { Confirm = true });

        Assert.That(chosen?.GameId, Is.EqualTo("a"));
    }

    [Test]
    public void HandleGamepadNav_NoInput_DoesNotMove()
    {
        var screen = new GameCarouselScreen([Game("a"), Game("b")]);
        screen.HandleGamepadNav(new MenuNavInput());
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }
}
