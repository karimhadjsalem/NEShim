using NEShim.Input;
using NEShim.Input.Sources;
using NEShim.Localization;

namespace NEShim.Tests.Input.Sources;

[TestFixture]
internal class TextGlyphSourceTests
{
    private TextGlyphSource _source = null!;
    private LocalizationData _localization = null!;

    [SetUp]
    public void SetUp()
    {
        _source = new TextGlyphSource();
        _localization = new LocalizationData();
    }

    [Test]
    public void TryResolve_NeverReturnsNull_GuaranteeingChainTermination()
    {
        Assert.That(_source.TryResolve("A", _localization), Is.Not.Null);
    }

    [Test]
    public void TryResolve_ReturnsZeroGlyph_TextOnly()
    {
        var result = _source.TryResolve("A", _localization);

        Assert.That(result!.Value.Glyph, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void TryResolve_MatchesGamepadButtonLocalizer()
    {
        var result = _source.TryResolve("DPadUp", _localization);

        Assert.That(result!.Value.Text, Is.EqualTo(GamepadButtonLocalizer.Localize("DPadUp", _localization)));
    }

    [Test]
    public void TryResolve_UnknownIdentifier_PassesThroughUnchanged()
    {
        var result = _source.TryResolve("SomeFutureButton", _localization);

        Assert.That(result!.Value.Text, Is.EqualTo("SomeFutureButton"));
    }
}
