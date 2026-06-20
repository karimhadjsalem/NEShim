using NEShim.Localization;

namespace NEShim.Tests.Localization;

[TestFixture]
internal class LanguageInfoTests
{
    [Test]
    public void Constructor_SetsCodeCorrectly()
    {
        var info = new LanguageInfo("french", "Français", ["fr"]);
        Assert.That(info.Code, Is.EqualTo("french"));
    }

    [Test]
    public void Constructor_SetsNativeNameCorrectly()
    {
        var info = new LanguageInfo("french", "Français", ["fr"]);
        Assert.That(info.NativeName, Is.EqualTo("Français"));
    }

    [Test]
    public void Constructor_SetsCulturePrefixesCorrectly()
    {
        var info = new LanguageInfo("french", "Français", ["fr"]);
        Assert.That(info.CulturePrefixes, Is.EqualTo(new[] { "fr" }));
    }

    [Test]
    public void SameCode_SameNativeName_SamePrefixes_IsExplicitlyVerifiableByField()
    {
        var a = new LanguageInfo("english", "English", ["en"]);
        var b = new LanguageInfo("english", "English", ["en"]);
        Assert.That(a.Code,           Is.EqualTo(b.Code));
        Assert.That(a.NativeName,     Is.EqualTo(b.NativeName));
        Assert.That(a.CulturePrefixes, Is.EqualTo(b.CulturePrefixes));
    }

    [Test]
    public void DifferentCode_IsDistinct()
    {
        var a = new LanguageInfo("english", "English", ["en"]);
        var b = new LanguageInfo("french",  "English", ["en"]);
        Assert.That(a.Code, Is.Not.EqualTo(b.Code));
    }
}
