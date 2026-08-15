using NSubstitute;
using NEShim.Input;
using NEShim.Localization;

namespace NEShim.Tests.Input;

/// <summary>
/// Every stubbed result uses IntPtr.Zero for Glyph — CachingGamepadGlyphResolver only calls
/// SDL.DestroySurface for non-zero handles, so this keeps the test free of any real SDL/native
/// boundary crossing (unit test, not integration test).
/// </summary>
[TestFixture]
internal class CachingGamepadGlyphResolverTests
{
    private IGamepadGlyphResolver _inner = null!;
    private CachingGamepadGlyphResolver _resolver = null!;
    private LocalizationData _localization = null!;

    [SetUp]
    public void SetUp()
    {
        _inner = Substitute.For<IGamepadGlyphResolver>();
        _resolver = new CachingGamepadGlyphResolver(_inner);
        _localization = new LocalizationData();
    }

    [TearDown]
    public void TearDown()
    {
        _resolver.Dispose();
        _inner.Dispose();
    }

    [Test]
    public void Resolve_SameIdentifierTwice_OnlyCallsInnerOnce()
    {
        _inner.Resolve("A", _localization).Returns(new GlyphResult(IntPtr.Zero, "cached"));

        _resolver.Resolve("A", _localization);
        _resolver.Resolve("A", _localization);

        _inner.Received(1).Resolve("A", _localization);
    }

    [Test]
    public void Resolve_ReturnsCachedResult()
    {
        var expected = new GlyphResult(IntPtr.Zero, "cached");
        _inner.Resolve("A", _localization).Returns(expected);

        _resolver.Resolve("A", _localization);
        var second = _resolver.Resolve("A", _localization);

        Assert.That(second, Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_DifferentIdentifiers_CallsInnerForEach()
    {
        _inner.Resolve("A", _localization).Returns(new GlyphResult(IntPtr.Zero, "a"));
        _inner.Resolve("B", _localization).Returns(new GlyphResult(IntPtr.Zero, "b"));

        _resolver.Resolve("A", _localization);
        _resolver.Resolve("B", _localization);

        _inner.Received(1).Resolve("A", _localization);
        _inner.Received(1).Resolve("B", _localization);
    }

    [Test]
    public void InvalidateCache_ForcesFreshCallOnNextResolve()
    {
        _inner.Resolve("A", _localization).Returns(new GlyphResult(IntPtr.Zero, "cached"));
        _resolver.Resolve("A", _localization);

        _resolver.InvalidateCache();
        _resolver.Resolve("A", _localization);

        _inner.Received(2).Resolve("A", _localization);
    }

    [Test]
    public void Resolve_NullIdentifier_AlwaysDelegatesToInner_NeverCached()
    {
        _inner.Resolve(null, _localization).Returns(new GlyphResult(IntPtr.Zero, _localization.BindNone));

        _resolver.Resolve(null, _localization);
        _resolver.Resolve(null, _localization);

        _inner.Received(2).Resolve(null, _localization);
    }

    [Test]
    public void Dispose_DisposesInner()
    {
        _resolver.Dispose();
        _inner.Received(1).Dispose();
    }
}
