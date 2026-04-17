using System.Threading;

namespace NEShim.Rendering;

/// <summary>
/// Double-buffer for NES video frames.
/// Emulation thread writes to the back buffer then calls Swap().
/// Paint thread reads from the front buffer.
/// </summary>
internal sealed class FrameBuffer
{
    private readonly int[][] _buffers = { new int[256 * 240], new int[256 * 240] };
    private int _frontIndex = 0;
    private SpinLock _lock = new SpinLock(enableThreadOwnerTracking: false);

    public int Width  { get; private set; } = 256;
    public int Height { get; private set; } = 240;

    /// <summary>Front buffer — read by paint thread.</summary>
    public int[] FrontBuffer => _buffers[_frontIndex];

    /// <summary>Back buffer — written by emulation thread.</summary>
    private int[] BackBuffer => _buffers[1 - _frontIndex];

    /// <summary>Copies pixel data into the back buffer (emulation thread).</summary>
    public void WriteBack(int[] src, int width, int height)
    {
        int count = Math.Min(src.Length, BackBuffer.Length);
        Buffer.BlockCopy(src, 0, BackBuffer, 0, count * sizeof(int));
        Width  = width;
        Height = height;
    }

    /// <summary>Atomically swaps front and back buffers (emulation thread).</summary>
    public void Swap()
    {
        bool taken = false;
        _lock.Enter(ref taken);
        try
        {
            _frontIndex = 1 - _frontIndex;
        }
        finally
        {
            if (taken) _lock.Exit();
        }
    }

    /// <summary>Copies the front buffer for menu overlay use (UI thread).</summary>
    public int[] CaptureFront()
    {
        bool taken = false;
        _lock.Enter(ref taken);
        try
        {
            var copy = new int[FrontBuffer.Length];
            Buffer.BlockCopy(FrontBuffer, 0, copy, 0, copy.Length * sizeof(int));
            return copy;
        }
        finally
        {
            if (taken) _lock.Exit();
        }
    }
}
