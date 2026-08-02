using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class FilterUniformWriterTests
{
    [Test]
    public void Write_CallsStructuralWriter_WithContentWidthAndNesHeight()
    {
        int? capturedWidth  = null;
        int? capturedHeight = null;
        FilterUniformWriter.ParamWriter writer = (_, nesWidth, nesHeight) =>
        {
            capturedWidth  = nesWidth;
            capturedHeight = nesHeight;
        };

        Span<float> buffer = stackalloc float[4];
        FilterUniformWriter.Write(buffer, writer, contentWidth: 256, nesHeight: 240,
            VideoColorFilterMode.None, applyColorMode: true);

        Assert.That(capturedWidth,  Is.EqualTo(256));
        Assert.That(capturedHeight, Is.EqualTo(240));
    }

    // Directly targets the real bug this class exists to prevent: a caller silently passing a
    // frame-varying content height instead of the fixed NES texture height (see the class's own
    // doc comment). Write's signature has exactly one "height" parameter, so a caller cannot
    // supply two different values for it — this test locks in that whatever value is passed
    // reaches the filter's own writer unchanged, so a future refactor can't reintroduce a silent
    // substitution between the two backends.
    [Test]
    public void Write_StructuralWriter_ReceivesNesHeightUnchanged()
    {
        int captured = -1;
        FilterUniformWriter.ParamWriter writer = (_, _, nesHeight) => captured = nesHeight;

        Span<float> buffer = stackalloc float[4];
        FilterUniformWriter.Write(buffer, writer, contentWidth: 256, nesHeight: 224,
            VideoColorFilterMode.None, applyColorMode: true);

        Assert.That(captured, Is.EqualTo(224));
    }

    [Test]
    public void Write_ApplyColorModeTrue_WritesColorModeToSlot3()
    {
        Span<float> buffer = stackalloc float[4];
        FilterUniformWriter.Write(buffer, static (_, _, _) => { }, 256, 240,
            VideoColorFilterMode.Cool, applyColorMode: true);

        Assert.That(buffer[3], Is.EqualTo((float)VideoColorFilterMode.Cool));
    }

    [Test]
    public void Write_ApplyColorModeFalse_WritesZeroToSlot3_RegardlessOfColorMode()
    {
        Span<float> buffer = stackalloc float[4];
        FilterUniformWriter.Write(buffer, static (_, _, _) => { }, 256, 240,
            VideoColorFilterMode.PhosphorAmber, applyColorMode: false);

        Assert.That(buffer[3], Is.EqualTo(0f));
    }

    [Test]
    public void Write_StructuralWriter_CanPopulateSlots0Through2()
    {
        Span<float> buffer = stackalloc float[4];
        FilterUniformWriter.Write(buffer, static (b, _, _) => { b[0] = 1f; b[1] = 2f; b[2] = 3f; },
            256, 240, VideoColorFilterMode.None, applyColorMode: true);

        Assert.That(buffer[0], Is.EqualTo(1f));
        Assert.That(buffer[1], Is.EqualTo(2f));
        Assert.That(buffer[2], Is.EqualTo(3f));
    }

    [Test]
    public void Write_DoesNotOverwriteStructuralSlots_WhenApplyColorModeFalse()
    {
        Span<float> buffer = stackalloc float[4];
        FilterUniformWriter.Write(buffer, static (b, _, _) => { b[0] = 9f; b[1] = 8f; b[2] = 7f; },
            256, 240, VideoColorFilterMode.Warm, applyColorMode: false);

        Assert.That(buffer[0], Is.EqualTo(9f));
        Assert.That(buffer[1], Is.EqualTo(8f));
        Assert.That(buffer[2], Is.EqualTo(7f));
        Assert.That(buffer[3], Is.EqualTo(0f));
    }
}
