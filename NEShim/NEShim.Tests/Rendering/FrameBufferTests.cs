using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class FrameBufferTests
{
    [Test]
    public void WriteBack_ThenSwap_FrontBufferContainsWrittenData()
    {
        var fb  = new FrameBuffer();
        var src = new int[256 * 240];
        src[0]  = 0x12345678;

        fb.WriteBack(src, 256, 240);
        fb.Swap();

        Assert.That(fb.FrontBuffer[0], Is.EqualTo(0x12345678));
    }

    [Test]
    public void WriteBack_UpdatesDimensions()
    {
        var fb = new FrameBuffer();
        fb.WriteBack(new int[128 * 96], 128, 96);
        Assert.That(fb.Width,  Is.EqualTo(128));
        Assert.That(fb.Height, Is.EqualTo(96));
    }

    [Test]
    public void WriteBack_WithoutSwap_FrontBufferIsUnchanged()
    {
        var fb  = new FrameBuffer();
        var src = new int[256 * 240];
        src[0]  = 0xABCD;

        fb.WriteBack(src, 256, 240);

        Assert.That(fb.FrontBuffer[0], Is.EqualTo(0)); // front still holds zeroes
    }

    [Test]
    public void Swap_SecondSwap_RestoresOriginalFront()
    {
        var fb   = new FrameBuffer();
        var src1 = new int[256 * 240];
        src1[0]  = 1;

        fb.WriteBack(src1, 256, 240);
        fb.Swap(); // front = src1
        Assert.That(fb.FrontBuffer[0], Is.EqualTo(1));

        // Overwrite back with zeros, swap again
        fb.WriteBack(new int[256 * 240], 256, 240);
        fb.Swap(); // front = zeros
        Assert.That(fb.FrontBuffer[0], Is.EqualTo(0));
    }

    [Test]
    public void CaptureFront_ReturnsCopyNotReference()
    {
        var fb  = new FrameBuffer();
        var src = new int[256 * 240];
        src[10] = 42;
        fb.WriteBack(src, 256, 240);
        fb.Swap();

        int[] captured = fb.CaptureFront();
        captured[10] = 99;

        Assert.That(fb.FrontBuffer[10], Is.EqualTo(42)); // original unchanged
    }

    [Test]
    public void WriteBack_SrcLargerThanBuffer_DoesNotThrow()
    {
        var fb     = new FrameBuffer();
        var bigSrc = new int[256 * 240 + 100];
        bigSrc[0]  = 7;

        Assert.DoesNotThrow(() => fb.WriteBack(bigSrc, 256, 240));
    }
}
