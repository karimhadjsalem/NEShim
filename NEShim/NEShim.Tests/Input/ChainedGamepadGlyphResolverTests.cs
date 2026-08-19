using NSubstitute;
using NEShim.Input;
using NEShim.Localization;

namespace NEShim.Tests.Input;

[TestFixture]
internal class ChainedGamepadGlyphResolverTests
{
    private IGamepadGlyphSource _first  = null!;
    private IGamepadGlyphSource _second = null!;
    private IGamepadGlyphSource _third  = null!;
    private LocalizationData    _localization = null!;

    [SetUp]
    public void SetUp()
    {
        _first  = Substitute.For<IGamepadGlyphSource>();
        _second = Substitute.For<IGamepadGlyphSource>();
        _third  = Substitute.For<IGamepadGlyphSource>();
        _localization = new LocalizationData();
    }

    private ChainedGamepadGlyphResolver Chain() =>
        new(new[] { _first, _second, _third });

    [Test]
    public void Resolve_NullIdentifier_ReturnsBindNone_WithoutCallingAnySource()
    {
        var result = Chain().Resolve(null, _localization);

        Assert.That(result.Glyph, Is.EqualTo(IntPtr.Zero));
        Assert.That(result.Text, Is.EqualTo(_localization.BindNone));
        _first.DidNotReceiveWithAnyArgs().TryResolve(default!, default!);
        _second.DidNotReceiveWithAnyArgs().TryResolve(default!, default!);
        _third.DidNotReceiveWithAnyArgs().TryResolve(default!, default!);
    }

    [Test]
    public void Resolve_FirstSourceHits_ShortCircuits_LaterSourcesNeverCalled()
    {
        var expected = new GlyphResult((IntPtr)123, "first");
        _first.TryResolve("A", _localization).Returns(expected);

        var result = Chain().Resolve("A", _localization);

        Assert.That(result, Is.EqualTo(expected));
        _second.DidNotReceiveWithAnyArgs().TryResolve(default!, default!);
        _third.DidNotReceiveWithAnyArgs().TryResolve(default!, default!);
    }

    [Test]
    public void Resolve_FirstSourceMisses_FallsThroughToSecond()
    {
        _first.TryResolve("A", _localization).Returns((GlyphResult?)null);
        var expected = new GlyphResult((IntPtr)456, "second");
        _second.TryResolve("A", _localization).Returns(expected);

        var result = Chain().Resolve("A", _localization);

        Assert.That(result, Is.EqualTo(expected));
        _third.DidNotReceiveWithAnyArgs().TryResolve(default!, default!);
    }

    [Test]
    public void Resolve_AllSourcesMiss_FallsBackToLocalizedText()
    {
        _first.TryResolve("A", _localization).Returns((GlyphResult?)null);
        _second.TryResolve("A", _localization).Returns((GlyphResult?)null);
        _third.TryResolve("A", _localization).Returns((GlyphResult?)null);

        var result = Chain().Resolve("A", _localization);

        Assert.That(result.Glyph, Is.EqualTo(IntPtr.Zero));
        Assert.That(result.Text, Is.EqualTo(GamepadButtonLocalizer.Localize("A", _localization)));
    }
}
