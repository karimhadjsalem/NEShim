using NEShim.Localization;

namespace NEShim.Tests.Localization;

/// <summary>
/// Tests SteamLanguageResolver in the absence of a real Steam session.
/// In the test environment SteamManager.IsAvailable is always false (Steam is not
/// initialized), so only the "Steam not available → return null" path is exercised here.
/// The remaining paths (empty language, unsupported language, valid language) require a
/// live Steam client and belong in integration tests that run against a Steam dev environment.
/// </summary>
[TestFixture]
internal class SteamLanguageResolverTests
{
    [Test]
    public void Resolve_WhenSteamNotAvailable_ReturnsNull()
    {
        // SteamManager.IsAvailable is false in a headless test environment because
        // SteamManager.Initialize() is never called.
        var resolver = new SteamLanguageResolver();
        Assert.That(resolver.Resolve(), Is.Null);
    }

    [Test]
    public void Implements_ILanguageResolver()
    {
        var resolver = new SteamLanguageResolver();
        Assert.That(resolver, Is.InstanceOf<ILanguageResolver>());
    }
}
