using NEShim.Input;

namespace NEShim.Tests.Input;

/// <summary>
/// A silent gap in this table would make the Steam-glyph chain tier quietly no-op for one
/// button, so every identifier the SDL gamepad path can actually produce must be covered.
/// </summary>
[TestFixture]
internal class XboxOriginMapTests
{
    private static readonly string[] AllSdlIdentifiers =
    {
        "A", "B", "X", "Y",
        "LeftShoulder", "RightShoulder", "Start", "Back",
        "LeftThumb", "RightThumb",
        "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
        "AnalogUp", "AnalogDown", "AnalogLeft", "AnalogRight",
    };

    [Test]
    public void Map_ContainsEighteenEntries()
    {
        Assert.That(XboxOriginMap.Map, Has.Count.EqualTo(18));
    }

    [TestCaseSource(nameof(AllSdlIdentifiers))]
    public void Map_ContainsEveryKnownSdlIdentifier(string identifier)
    {
        Assert.That(XboxOriginMap.Map.ContainsKey(identifier), Is.True,
            $"XboxOriginMap is missing an entry for '{identifier}'.");
    }
}
