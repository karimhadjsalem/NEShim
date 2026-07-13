using NEShim.Config;

namespace NEShim.Tests.Config;

[TestFixture]
internal class GameContextTests
{
    // ---- ForGame ----

    [Test]
    public void ForGame_CombinesGamesRootAndGameId()
    {
        var ctx = GameContext.ForGame(@"C:\install\games", "kaaz");
        Assert.That(ctx.RootDirectory, Is.EqualTo(Path.Combine(@"C:\install\games", "kaaz")));
    }

    [Test]
    public void ForGame_SetsGameId()
    {
        var ctx = GameContext.ForGame(@"C:\install\games", "kaaz");
        Assert.That(ctx.GameId, Is.EqualTo("kaaz"));
    }

    // ---- ResolvePath: null ctx (single-game, unchanged) ----

    [Test]
    public void ResolvePath_NullCtx_RelativePath_ResolvesAgainstBaseDirectory()
    {
        string resolved = GameContext.ResolvePath("game.nes", null);
        Assert.That(resolved, Is.EqualTo(Path.Combine(AppContext.BaseDirectory, "game.nes")));
    }

    [Test]
    public void ResolvePath_NullCtx_AbsolutePath_ReturnedVerbatim()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "game.nes");
        string resolved = GameContext.ResolvePath(absolute, null);
        Assert.That(resolved, Is.EqualTo(absolute));
    }

    // ---- ResolvePath: set ctx (multi-game) ----

    [Test]
    public void ResolvePath_WithCtx_RelativePath_ResolvesAgainstGameRoot()
    {
        var ctx = new GameContext(@"C:\install\games\kaaz", "kaaz");
        string resolved = GameContext.ResolvePath("game.nes", ctx);
        Assert.That(resolved, Is.EqualTo(Path.Combine(@"C:\install\games\kaaz", "game.nes")));
    }

    [Test]
    public void ResolvePath_WithCtx_AbsolutePath_ReturnedVerbatim_IgnoringRootDirectory()
    {
        var ctx = new GameContext(@"C:\install\games\kaaz", "kaaz");
        string absolute = Path.Combine(Path.GetTempPath(), "game.nes");
        string resolved = GameContext.ResolvePath(absolute, ctx);
        Assert.That(resolved, Is.EqualTo(absolute));
    }

    [Test]
    public void ResolvePath_NullCtx_And_SetCtx_ResolveToDifferentRoots()
    {
        var ctx = new GameContext(@"C:\install\games\kaaz", "kaaz");
        string single = GameContext.ResolvePath("game.nes", null);
        string multi  = GameContext.ResolvePath("game.nes", ctx);
        Assert.That(single, Is.Not.EqualTo(multi));
    }

    // ---- MultiGameMode ----

    [Test]
    public void MultiGameMode_GamesRoot_IsGamesUnderBaseDirectory()
    {
        Assert.That(MultiGameMode.GamesRoot,
            Is.EqualTo(Path.Combine(AppContext.BaseDirectory, "games")));
    }

    [Test]
    public void MultiGameMode_ManifestPath_IsMultigameJsonUnderGamesRoot()
    {
        Assert.That(MultiGameMode.ManifestPath,
            Is.EqualTo(Path.Combine(MultiGameMode.GamesRoot, "multigame.json")));
    }

    [Test]
    public void MultiGameMode_IsActive_MatchesManifestFileExistence()
    {
        // Test binaries never ship games/multigame.json — this is the single-game-mode
        // regression guardrail: IsActive must be false in every normal test run.
        Assert.That(MultiGameMode.IsActive, Is.EqualTo(File.Exists(MultiGameMode.ManifestPath)));
    }
}
