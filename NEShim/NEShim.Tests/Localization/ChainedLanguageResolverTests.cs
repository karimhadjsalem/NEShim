using NEShim.Localization;

namespace NEShim.Tests.Localization;

[TestFixture]
internal class ChainedLanguageResolverTests
{
    private sealed class StubResolver(string? result) : ILanguageResolver
    {
        public string? Resolve() => result;
    }

    [Test]
    public void Resolve_EmptyChain_ReturnsNull()
    {
        var resolver = new ChainedLanguageResolver([]);
        Assert.That(resolver.Resolve(), Is.Null);
    }

    [Test]
    public void Resolve_SingleResolverReturnsValue_ReturnsThatValue()
    {
        var resolver = new ChainedLanguageResolver([new StubResolver("french")]);
        Assert.That(resolver.Resolve(), Is.EqualTo("french"));
    }

    [Test]
    public void Resolve_SingleResolverReturnsNull_ReturnsNull()
    {
        var resolver = new ChainedLanguageResolver([new StubResolver(null)]);
        Assert.That(resolver.Resolve(), Is.Null);
    }

    [Test]
    public void Resolve_FirstNonNullWins_SecondIsNotCalled()
    {
        int calls = 0;
        // Wrap StubResolver to track whether the second was called
        var first  = new StubResolver("english");
        var second = new CallTrackingResolver(() => { calls++; return "french"; });

        var resolver = new ChainedLanguageResolver([first, second]);
        var result   = resolver.Resolve();

        Assert.That(result, Is.EqualTo("english"));
        Assert.That(calls, Is.EqualTo(0));
    }

    [Test]
    public void Resolve_FirstReturnsNull_TriesSecond()
    {
        var resolver = new ChainedLanguageResolver([new StubResolver(null), new StubResolver("german")]);
        Assert.That(resolver.Resolve(), Is.EqualTo("german"));
    }

    [Test]
    public void Resolve_AllReturnNull_ReturnsNull()
    {
        var resolver = new ChainedLanguageResolver([new StubResolver(null), new StubResolver(null)]);
        Assert.That(resolver.Resolve(), Is.Null);
    }

    private sealed class CallTrackingResolver(Func<string?> callback) : ILanguageResolver
    {
        public string? Resolve() => callback();
    }
}
