using NEShim.Emulation;

namespace NEShim.Achievements;

internal static class AchievementEvaluator
{
    internal static long ReadRaw(IMemoryDomain memory, int address, int byteCount, bool bigEndian)
    {
        long value = 0;
        for (int i = 0; i < byteCount; i++)
        {
            byte b = memory.PeekByte(address + i);
            if (bigEndian)
                value = (value << 8) | b;
            else
                value |= (long)b << (i * 8);
        }
        return value;
    }

    /// <summary>
    /// Decodes a big-endian-assembled raw value as BCD.
    /// Each nibble represents one decimal digit; the most significant nibble of the highest byte
    /// is the most significant digit.
    /// Example: raw = 0x123456 (3 bytes) → 123456.
    /// </summary>
    internal static long DecodeBcd(long bigEndianRaw, int byteCount)
    {
        long result     = 0;
        long multiplier = 1;
        for (int i = 0; i < byteCount; i++)
        {
            byte b  = (byte)((bigEndianRaw >> (i * 8)) & 0xFF);
            result += (b & 0x0F) * multiplier
                    + ((b >> 4) & 0x0F) * multiplier * 10;
            multiplier *= 100;
        }
        return result;
    }

    internal static bool Matches(string comparison, long actual, long threshold) =>
        comparison switch
        {
            "equals"         => actual == threshold,
            "greaterOrEqual" => actual >= threshold,
            "greaterThan"    => actual >  threshold,
            "lessOrEqual"    => actual <= threshold,
            "lessThan"       => actual <  threshold,
            _                => false,
        };
}
