using NEShim.Input.Sources;
using NEShim.Localization;

namespace NEShim.Tests.Input.Sources;

/// <summary>
/// Tests for the "Steam not available" path, which is always the case in a unit test
/// environment without Steam running — matches the existing SteamInputManagerTests convention.
/// </summary>
[TestFixture]
internal class SteamInputGlyphSourceTests
{
    private SteamInputGlyphSource _source = null!;
    private LocalizationData _localization = null!;

    [SetUp]
    public void SetUp()
    {
        _source = new SteamInputGlyphSource();
        _localization = new LocalizationData();
    }

    [Test]
    public void TryResolve_KnownIdentifier_WhenSteamUnavailable_ReturnsNull()
    {
        Assert.That(_source.TryResolve("A", _localization), Is.Null);
    }

    [Test]
    public void TryResolve_UnknownIdentifier_ReturnsNull()
    {
        Assert.That(_source.TryResolve("NotARealIdentifier", _localization), Is.Null);
    }
}
