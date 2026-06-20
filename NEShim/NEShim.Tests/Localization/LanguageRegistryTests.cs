using System.Globalization;
using NEShim.Localization;

namespace NEShim.Tests.Localization;

[TestFixture]
internal class LanguageRegistryTests
{
    // ---- AllLanguages ----

    [Test]
    public void AllLanguages_HasNineEntries()
    {
        Assert.That(LanguageRegistry.AllLanguages.Count, Is.EqualTo(9));
    }

    [Test]
    public void AllLanguages_ContainsEnglish()
    {
        Assert.That(LanguageRegistry.AllLanguages.Select(l => l.Code), Contains.Item("english"));
    }

    // ---- FindByCode ----

    [Test]
    public void FindByCode_KnownCode_ReturnsCorrectInfo()
    {
        var result = LanguageRegistry.FindByCode("french");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.NativeName, Is.EqualTo("Français"));
    }

    [Test]
    public void FindByCode_CaseInsensitive_Matches()
    {
        var result = LanguageRegistry.FindByCode("ENGLISH");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Code, Is.EqualTo("english"));
    }

    [Test]
    public void FindByCode_UnknownCode_ReturnsNull()
    {
        Assert.That(LanguageRegistry.FindByCode("klingon"), Is.Null);
    }

    // ---- FindByCulture ----

    [TestCase("en-US", "english")]
    [TestCase("fr-FR", "french")]
    [TestCase("de-DE", "german")]
    [TestCase("es-ES", "spanish")]
    [TestCase("ja-JP", "japanese")]
    [TestCase("ko-KR", "korean")]
    [TestCase("ru-RU", "russian")]
    [TestCase("zh-CN", "schinese")]
    [TestCase("pt-BR", "portuguese")]
    public void FindByCulture_KnownCulture_ReturnsCorrectCode(string cultureName, string expectedCode)
    {
        var result = LanguageRegistry.FindByCulture(new CultureInfo(cultureName));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Code, Is.EqualTo(expectedCode));
    }

    [Test]
    public void FindByCulture_UnknownCulture_ReturnsNull()
    {
        var result = LanguageRegistry.FindByCulture(new CultureInfo("ar"));
        Assert.That(result, Is.Null);
    }
}
