using System.Globalization;
using NEShim.Localization;

namespace NEShim.Tests.Localization;

[TestFixture]
internal class LanguageRegistryTests
{
    // ---- AllLanguages ----

    [Test]
    public void AllLanguages_HasTenEntries()
    {
        Assert.That(LanguageRegistry.AllLanguages.Count, Is.EqualTo(10));
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

    [TestCase("en-US",   "english")]
    [TestCase("fr-FR",   "french")]
    [TestCase("de-DE",   "german")]
    [TestCase("es-ES",   "spanish")]
    [TestCase("es-MX",   "latam")]
    [TestCase("es-AR",   "latam")]
    [TestCase("ja-JP",   "japanese")]
    [TestCase("ko-KR",   "korean")]
    [TestCase("ru-RU",   "russian")]
    [TestCase("zh-CN",   "schinese")]
    [TestCase("zh-SG",   "schinese")]
    [TestCase("zh-Hans", "schinese")]
    [TestCase("pt-BR",   "portuguese")]
    public void FindByCulture_KnownCulture_ReturnsCorrectCode(string cultureName, string expectedCode)
    {
        var result = LanguageRegistry.FindByCulture(new CultureInfo(cultureName));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Code, Is.EqualTo(expectedCode));
    }

    // Traditional Chinese cultures must not match schinese — zh-TW and zh-Hant share
    // TwoLetterISOLanguageName "zh" with Simplified Chinese, so the prefix list must be explicit.
    [TestCase("zh-TW")]
    [TestCase("zh-HK")]
    [TestCase("zh-Hant")]
    public void FindByCulture_TraditionalChinese_ReturnsNull(string cultureName)
    {
        var result = LanguageRegistry.FindByCulture(new CultureInfo(cultureName));
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindByCulture_UnknownCulture_ReturnsNull()
    {
        var result = LanguageRegistry.FindByCulture(new CultureInfo("ar"));
        Assert.That(result, Is.Null);
    }

    // ---- FindBySteamCode ----

    // Direct matches: Steam code == our internal code.
    [TestCase("english",    "english")]
    [TestCase("french",     "french")]
    [TestCase("schinese",   "schinese")]
    [TestCase("portuguese", "portuguese")]
    [TestCase("latam",      "latam")]
    public void FindBySteamCode_DirectMatch_ReturnsCorrectCode(string steamCode, string expectedCode)
    {
        var result = LanguageRegistry.FindBySteamCode(steamCode);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Code, Is.EqualTo(expectedCode));
    }

    // Alias matches: known Steam quirks that don't match our internal codes.
    [TestCase("koreana",  "korean")]     // Steam's legacy code for Korean
    [TestCase("brazilian", "portuguese")] // Steam's Brazilian Portuguese → our single Portuguese
    public void FindBySteamCode_AliasMatch_ReturnsCorrectCode(string steamCode, string expectedCode)
    {
        var result = LanguageRegistry.FindBySteamCode(steamCode);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Code, Is.EqualTo(expectedCode));
    }

    [Test]
    public void FindBySteamCode_CaseInsensitive_Matches()
    {
        Assert.That(LanguageRegistry.FindBySteamCode("KOREANA")?.Code, Is.EqualTo("korean"));
    }

    // Unsupported Steam languages (e.g. Traditional Chinese) must return null so the
    // resolver falls through to the CultureInfo chain rather than trying a missing lang file.
    [TestCase("tchinese")]
    [TestCase("arabic")]
    [TestCase("hungarian")]
    public void FindBySteamCode_UnsupportedLanguage_ReturnsNull(string steamCode)
    {
        Assert.That(LanguageRegistry.FindBySteamCode(steamCode), Is.Null);
    }
}
