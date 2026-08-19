using NEShim.Config;
using NEShim.Steam;

namespace NEShim.Tests.Steam;

[TestFixture]
internal class SteamDlcManagerTests
{
    private static GameManifest Game(string id, uint steamDlcAppId = 0) =>
        new(id, id.ToUpperInvariant(), steamDlcAppId, ThumbnailPath: "");


    [Test]
    public void IsOwned_AppIdZero_ReturnsTrue_WithoutConsultingDelegate()
    {
        bool result = SteamDlcManager.IsOwned(0, _ => throw new InvalidOperationException("must not be called"));
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsOwned_NonZeroAppId_DelegateReturnsTrue_ReturnsTrue()
    {
        bool result = SteamDlcManager.IsOwned(12345, _ => true);
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsOwned_NonZeroAppId_DelegateReturnsFalse_ReturnsFalse()
    {
        bool result = SteamDlcManager.IsOwned(12345, _ => false);
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsOwned_DelegateReceivesTheAppIdPassedIn()
    {
        uint? received = null;
        SteamDlcManager.IsOwned(999, id => { received = id; return true; });
        Assert.That(received, Is.EqualTo(999u));
    }

    [Test]
    public void IsOwned_NonZeroAppId_NoDelegate_NoLiveSteamSession_ReturnsTrue()
    {
        // Test processes never call SteamManager.Initialize(), so IsAvailable is false —
        // the "no live Steam session" local-discovery fallback must apply.
        bool result = SteamDlcManager.IsOwned(12345);
        Assert.That(result, Is.True);
    }

    // ---- FilterOwned: the single-deploy, no-DLC, "every game bundled" scenario ----
    // A multi-game publish is not required to use Steam DLC at all — a publisher can ship every
    // game directly in the base install by leaving SteamDlcAppId at its default 0 for all of
    // them, and the whole library must show unfiltered. These tests exercise that scenario with
    // a real multi-game library (not just a single appId, like IsOwned's own tests above).

    [Test]
    public void FilterOwned_AllGamesAppIdZero_ReturnsEntireLibrary()
    {
        var games = new[] { Game("a"), Game("b"), Game("c") };

        var result = SteamDlcManager.FilterOwned(games, _ => throw new InvalidOperationException("must not be called"));

        Assert.That(result, Is.EquivalentTo(games));
    }

    [Test]
    public void FilterOwned_EmptyLibrary_ReturnsEmpty()
    {
        var result = SteamDlcManager.FilterOwned(Array.Empty<GameManifest>());
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void FilterOwned_MixOfBundledAndDlcGames_KeepsBundledAndOwnedDlc_DropsUnownedDlc()
    {
        var bundled = Game("bundled"); // SteamDlcAppId 0 — always shown
        var ownedDlc = Game("owned-dlc", steamDlcAppId: 100);
        var unownedDlc = Game("unowned-dlc", steamDlcAppId: 200);
        var games = new[] { bundled, ownedDlc, unownedDlc };

        var result = SteamDlcManager.FilterOwned(games, appId => appId == 100);

        Assert.That(result, Is.EquivalentTo(new[] { bundled, ownedDlc }));
    }

    [Test]
    public void FilterOwned_PreservesInputOrder()
    {
        var games = new[] { Game("z"), Game("a"), Game("m") };

        var result = SteamDlcManager.FilterOwned(games);

        Assert.That(result.Select(g => g.GameId), Is.EqualTo(new[] { "z", "a", "m" }));
    }
}
