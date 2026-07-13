using NEShim.Steam;

namespace NEShim.Tests.Steam;

[TestFixture]
internal class SteamDlcManagerTests
{
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
}
