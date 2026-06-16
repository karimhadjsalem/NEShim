using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class AudioFilterModeParserTests
{
    [TestCase("Default",      AudioFilterMode.Default)]
    [TestCase("Warm",         AudioFilterMode.Warm)]
    [TestCase("PseudoStereo", AudioFilterMode.PseudoStereo)]
    [TestCase("WarmStereo",   AudioFilterMode.WarmStereo)]
    [TestCase("Compression",  AudioFilterMode.Compression)]
    public void Parse_KnownValue_ReturnsCorrectMode(string input, AudioFilterMode expected)
    {
        Assert.That(AudioFilterModeParser.Parse(input), Is.EqualTo(expected));
    }

    [TestCase("default")]
    [TestCase("warm")]
    [TestCase("")]
    [TestCase("Unknown")]
    public void Parse_UnknownValue_ThrowsArgumentException(string input)
    {
        Assert.That(() => AudioFilterModeParser.Parse(input), Throws.ArgumentException);
    }
}
