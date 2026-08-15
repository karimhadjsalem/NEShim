using NEShim.Config;
using NEShim.Input.Sources;

namespace NEShim.Tests.Input.Sources;

/// <summary>
/// Tests for SteamInputSource exercising the "Steam not available" path,
/// which is always the case in a unit test environment without Steam running.
/// </summary>
[TestFixture]
internal class SteamInputSourceTests
{
    private SteamInputSource _source = null!;
    private AppConfig        _config = null!;

    [SetUp]
    public void SetUp()
    {
        _source = new SteamInputSource();
        _config = new AppConfig();
    }

    [Test]
    public void IsAvailable_InitialState_ReturnsFalse()
    {
        Assert.That(_source.IsAvailable, Is.False);
    }

    [Test]
    public void GetActiveIdentifiers_WhenSteamNotAvailable_ReturnsEmpty()
    {
        Assert.That(_source.GetActiveIdentifiers(_config), Is.Empty);
    }

    [Test]
    public void IsAvailable_AfterGetActiveIdentifiers_WhenSteamNotAvailable_StaysFalse()
    {
        _source.GetActiveIdentifiers(_config);
        Assert.That(_source.IsAvailable, Is.False);
    }

    [Test]
    public void GetMenuNav_WhenSteamNotAvailable_ReturnsDefault()
    {
        Assert.That(_source.GetMenuNav(_config).Any, Is.False);
    }

    [Test]
    public void GetHeldLeftRight_WhenSteamNotAvailable_ReturnsBothFalse()
    {
        var (left, right) = _source.GetHeldLeftRight(_config);
        Assert.That(left,  Is.False);
        Assert.That(right, Is.False);
    }

    // ── AnyJustPressed ──────────────────────────────────────────────────────────

    [Test]
    public void AnyJustPressed_WhenSteamNotAvailable_ReturnsFalse()
    {
        // SteamInputManager.AnyMenuActionActive() returns false when Steam is not running.
        Assert.That(_source.AnyJustPressed(), Is.False);
    }

    [Test]
    public void AnyJustPressed_CalledTwice_WhenSteamNotAvailable_AlwaysFalse()
    {
        // Verifies the edge-detection state doesn't incorrectly flip when Steam is absent.
        _source.AnyJustPressed();
        Assert.That(_source.AnyJustPressed(), Is.False);
    }
}
