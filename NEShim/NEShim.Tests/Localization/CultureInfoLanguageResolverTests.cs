using System.Globalization;
using NEShim.Localization;

namespace NEShim.Tests.Localization;

[TestFixture]
internal class CultureInfoLanguageResolverTests
{
    private CultureInfo _originalCulture = null!;

    [SetUp]
    public void SetUp() => _originalCulture = CultureInfo.CurrentUICulture;

    [TearDown]
    public void TearDown() => CultureInfo.CurrentUICulture = _originalCulture;

    [TestCase("en-US", "english")]
    [TestCase("fr-FR", "french")]
    [TestCase("de-DE", "german")]
    [TestCase("es-ES", "spanish")]
    [TestCase("es-MX", "latam")]
    [TestCase("es-AR", "latam")]
    [TestCase("ja-JP", "japanese")]
    [TestCase("ko-KR", "korean")]
    [TestCase("ru-RU", "russian")]
    [TestCase("zh-CN", "schinese")]
    [TestCase("pt-BR", "portuguese")]
    public void Resolve_KnownCulture_ReturnsExpectedCode(string cultureName, string expectedCode)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        var resolver = new CultureInfoLanguageResolver();
        Assert.That(resolver.Resolve(), Is.EqualTo(expectedCode));
    }

    [Test]
    public void Resolve_UnknownCulture_ReturnsNull()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ar");
        var resolver = new CultureInfoLanguageResolver();
        Assert.That(resolver.Resolve(), Is.Null);
    }
}
