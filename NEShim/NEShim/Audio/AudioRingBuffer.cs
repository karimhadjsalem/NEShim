namespace NEShim.Audio;

/// <summary>
/// Single-producer/single-consumer ring buffer for 16-bit audio samples.
/// Not thread-safe — callers must synchronise access externally.
/// </summary>
internal sealed class AudioRingBuffer
{
    private readonly short[] _ring;
    private readonly int     _capacity;
    private int _writePos;
    private int _readPos;
    private int _available;

    public int Available => _available;
    public int Capacity  => _capacity;

    public AudioRingBuffer(int capacity)
    {
        _capacity = capacity;
        _ring     = new short[capacity];
    }

    /// <summary>
    /// Writes <paramref name="count"/> shorts from <paramref name="samples"/> into the ring.
    /// Drops samples silently when the buffer is full.
    /// </summary>
    public void Enqueue(short[] samples, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_available >= _capacity) break;
            _ring[_writePos] = samples[i];
            _writePos        = (_writePos + 1) % _capacity;
            _available++;
        }
    }

    /// <summary>
    /// Reads the left-channel sample from the next stereo pair and discards the right
    /// (both channels are identical for NES mono output).
    /// Returns false and leaves <paramref name="sample"/> at 0 when fewer than 2 shorts are available.
    /// </summary>
    public bool TryDequeueMonoL(out short sample)
    {
        if (_available < 2) { sample = 0; return false; }

        sample    = _ring[_readPos];
        _readPos  = (_readPos + 1) % _capacity;
        _available--;

        // Discard paired R channel.
        _readPos  = (_readPos + 1) % _capacity;
        _available--;

        return true;
    }

    /// <summary>Resets the buffer to empty without reallocating.</summary>
    public void Drain() => _writePos = _readPos = _available = 0;
}
