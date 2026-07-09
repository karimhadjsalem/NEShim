using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class SDL3FontCacheTests
{
    // ---- CandidateFilenames: Segoe UI ----

    [Test]
    public void CandidateFilenames_SegoeUI_Regular_ReturnsSegoeUI()
    {
        var files = SDL3FontCache.CandidateFilenames("segoe ui", bold: false, italic: false);
        Assert.That(files, Is.EqualTo(new[] { "segoeui.ttf" }));
    }

    [Test]
    public void CandidateFilenames_SegoeUI_Bold_ReturnsSegoeuib()
    {
        var files = SDL3FontCache.CandidateFilenames("segoe ui", bold: true, italic: false);
        Assert.That(files, Is.EqualTo(new[] { "segoeuib.ttf" }));
    }

    [Test]
    public void CandidateFilenames_SegoeUI_Italic_PrefersSegoeuii()
    {
        var files = SDL3FontCache.CandidateFilenames("segoe ui", bold: false, italic: true);
        Assert.That(files[0], Is.EqualTo("segoeuii.ttf"));
    }

    [Test]
    public void CandidateFilenames_SegoeUI_BoldItalic_PrefersSegoeuiz()
    {
        var files = SDL3FontCache.CandidateFilenames("segoe ui", bold: true, italic: true);
        Assert.That(files[0], Is.EqualTo("segoeuiz.ttf"));
    }

    // ---- CandidateFilenames: Arial ----

    [Test]
    public void CandidateFilenames_Arial_Regular_ReturnsArial()
    {
        var files = SDL3FontCache.CandidateFilenames("arial", bold: false, italic: false);
        Assert.That(files, Is.EqualTo(new[] { "arial.ttf" }));
    }

    [Test]
    public void CandidateFilenames_Arial_Bold_ReturnsArialbd()
    {
        var files = SDL3FontCache.CandidateFilenames("arial", bold: true, italic: false);
        Assert.That(files, Is.EqualTo(new[] { "arialbd.ttf" }));
    }

    // ---- CandidateFilenames: case insensitivity ----

    [Test]
    public void CandidateFilenames_FamilyIsCaseInsensitive()
    {
        var lower = SDL3FontCache.CandidateFilenames("segoe ui", bold: true, italic: false);
        var upper = SDL3FontCache.CandidateFilenames("SEGOE UI", bold: true, italic: false);
        var mixed = SDL3FontCache.CandidateFilenames("Segoe UI", bold: true, italic: false);
        Assert.That(lower, Is.EqualTo(upper));
        Assert.That(lower, Is.EqualTo(mixed));
    }

    // ---- CandidateFilenames: unknown family falls back to Segoe UI ----

    [Test]
    public void CandidateFilenames_UnknownFamily_Regular_FallsBackToSegoeUI()
    {
        var files = SDL3FontCache.CandidateFilenames("Comic Sans", bold: false, italic: false);
        Assert.That(files[0], Is.EqualTo("segoeui.ttf"));
    }

    [Test]
    public void CandidateFilenames_UnknownFamily_Bold_FallsBackToSegoeuib()
    {
        var files = SDL3FontCache.CandidateFilenames("unknown", bold: true, italic: false);
        Assert.That(files[0], Is.EqualTo("segoeuib.ttf"));
    }

    // ---- CandidateFilenames: Arial italic / bold-italic ----

    [Test]
    public void CandidateFilenames_Arial_Italic_PrefersAriali()
    {
        var files = SDL3FontCache.CandidateFilenames("arial", bold: false, italic: true);
        Assert.That(files[0], Is.EqualTo("ariali.ttf"));
    }

    [Test]
    public void CandidateFilenames_Arial_Italic_FallsBackToArial()
    {
        var files = SDL3FontCache.CandidateFilenames("arial", bold: false, italic: true);
        Assert.That(files, Contains.Item("arial.ttf"));
    }

    [Test]
    public void CandidateFilenames_Arial_BoldItalic_PrefersArialbi()
    {
        var files = SDL3FontCache.CandidateFilenames("arial", bold: true, italic: true);
        Assert.That(files[0], Is.EqualTo("arialbi.ttf"));
    }

    [Test]
    public void CandidateFilenames_Arial_BoldItalic_FallsBackToArialbd()
    {
        var files = SDL3FontCache.CandidateFilenames("arial", bold: true, italic: true);
        Assert.That(files, Contains.Item("arialbd.ttf"));
    }

    // ---- CandidateFilenames: unknown family italic / bold-italic ----

    [Test]
    public void CandidateFilenames_UnknownFamily_Italic_FallsBackToSegoeuii()
    {
        var files = SDL3FontCache.CandidateFilenames("Tahoma", bold: false, italic: true);
        Assert.That(files[0], Is.EqualTo("segoeuii.ttf"));
    }

    [Test]
    public void CandidateFilenames_UnknownFamily_Italic_IncludesArialiFallback()
    {
        var files = SDL3FontCache.CandidateFilenames("Tahoma", bold: false, italic: true);
        Assert.That(files, Contains.Item("ariali.ttf"));
    }

    [Test]
    public void CandidateFilenames_UnknownFamily_BoldItalic_FallsBackToSegoeuiz()
    {
        var files = SDL3FontCache.CandidateFilenames("Tahoma", bold: true, italic: true);
        Assert.That(files[0], Is.EqualTo("segoeuiz.ttf"));
    }

    [Test]
    public void CandidateFilenames_UnknownFamily_BoldItalic_IncludesArialBoldFallbacks()
    {
        var files = SDL3FontCache.CandidateFilenames("Tahoma", bold: true, italic: true);
        Assert.That(files, Contains.Item("arialbi.ttf"));
    }
}
