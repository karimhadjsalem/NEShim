using NEShim.Achievements;
using NUnit.Framework;

namespace NEShim.Tests.Achievements;

[TestFixture]
public class AchievementEvaluatorTests
{
    // A dead-simple IMemoryReader backed by a byte array. Avoids mocking overhead
    // for pure value-computation tests.
    private sealed class ArrayMemoryReader : IMemoryReader
    {
        private readonly byte[] _bytes;
        private readonly int _baseAddress;

        public ArrayMemoryReader(int baseAddress, params byte[] bytes)
        {
            _baseAddress = baseAddress;
            _bytes       = bytes;
        }

        public byte PeekByte(long address) => _bytes[(int)(address - _baseAddress)];
    }

    // --- ReadRaw ---

    [Test]
    public void ReadRaw_SingleByte_ReturnsValue()
    {
        var memory = new ArrayMemoryReader(0, 0xAB);
        Assert.That(AchievementEvaluator.ReadRaw(memory, 0, 1, bigEndian: false), Is.EqualTo(0xAB));
    }

    [Test]
    public void ReadRaw_LittleEndian_TwoBytes_AssemblesLowByteFirst()
    {
        var memory = new ArrayMemoryReader(0, 0x01, 0x02);
        long result = AchievementEvaluator.ReadRaw(memory, 0, 2, bigEndian: false);
        Assert.That(result, Is.EqualTo(0x0201));
    }

    [Test]
    public void ReadRaw_BigEndian_TwoBytes_AssemblesHighByteFirst()
    {
        var memory = new ArrayMemoryReader(0, 0x01, 0x02);
        long result = AchievementEvaluator.ReadRaw(memory, 0, 2, bigEndian: true);
        Assert.That(result, Is.EqualTo(0x0102));
    }

    [Test]
    public void ReadRaw_ReadsFromCorrectAddress()
    {
        var memory = new ArrayMemoryReader(0, 0x00, 0x00, 0xFF);
        long result = AchievementEvaluator.ReadRaw(memory, 2, 1, bigEndian: false);
        Assert.That(result, Is.EqualTo(0xFF));
    }

    [Test]
    public void ReadRaw_NonZeroBaseAddress_OffsetsCorrectly()
    {
        var memory = new ArrayMemoryReader(100, 0x42);
        long result = AchievementEvaluator.ReadRaw(memory, 100, 1, bigEndian: false);
        Assert.That(result, Is.EqualTo(0x42));
    }

    [Test]
    public void ReadRaw_BigEndian_ThreeBytes_AssemblesCorrectly()
    {
        var memory = new ArrayMemoryReader(0, 0x12, 0x34, 0x56);
        long result = AchievementEvaluator.ReadRaw(memory, 0, 3, bigEndian: true);
        Assert.That(result, Is.EqualTo(0x123456));
    }

    // --- DecodeBcd ---

    [Test]
    public void DecodeBcd_SingleByte_DecodesCorrectly()
    {
        // 0x56 → tens=5, units=6 → 56
        Assert.That(AchievementEvaluator.DecodeBcd(0x56, 1), Is.EqualTo(56));
    }

    [Test]
    public void DecodeBcd_Zero_ReturnsZero()
    {
        Assert.That(AchievementEvaluator.DecodeBcd(0x00, 1), Is.EqualTo(0));
    }

    [Test]
    public void DecodeBcd_TwoBytes_DecodesCorrectly()
    {
        // big-endian raw 0x1234 → byte0 (low)=0x34 → 34, byte1 (high)=0x12 → 12 × 100 = 1200; total=1234
        Assert.That(AchievementEvaluator.DecodeBcd(0x1234, 2), Is.EqualTo(1234));
    }

    [Test]
    public void DecodeBcd_ThreeBytes_DecodesCorrectly()
    {
        // 0x123456 → 123456
        Assert.That(AchievementEvaluator.DecodeBcd(0x123456, 3), Is.EqualTo(123456));
    }

    [Test]
    public void DecodeBcd_MaxSingleByteValue_Decodes99()
    {
        Assert.That(AchievementEvaluator.DecodeBcd(0x99, 1), Is.EqualTo(99));
    }

    // --- Matches ---

    [Test]
    public void Matches_Equals_ReturnsTrueWhenEqual()
        => Assert.That(AchievementEvaluator.Matches("equals", 42, 42), Is.True);

    [Test]
    public void Matches_Equals_ReturnsFalseWhenNotEqual()
        => Assert.That(AchievementEvaluator.Matches("equals", 42, 43), Is.False);

    [Test]
    public void Matches_GreaterOrEqual_TrueWhenEqual()
        => Assert.That(AchievementEvaluator.Matches("greaterOrEqual", 5, 5), Is.True);

    [Test]
    public void Matches_GreaterOrEqual_TrueWhenGreater()
        => Assert.That(AchievementEvaluator.Matches("greaterOrEqual", 6, 5), Is.True);

    [Test]
    public void Matches_GreaterOrEqual_FalseWhenLess()
        => Assert.That(AchievementEvaluator.Matches("greaterOrEqual", 4, 5), Is.False);

    [Test]
    public void Matches_GreaterThan_TrueWhenGreater()
        => Assert.That(AchievementEvaluator.Matches("greaterThan", 6, 5), Is.True);

    [Test]
    public void Matches_GreaterThan_FalseWhenEqual()
        => Assert.That(AchievementEvaluator.Matches("greaterThan", 5, 5), Is.False);

    [Test]
    public void Matches_LessOrEqual_TrueWhenEqual()
        => Assert.That(AchievementEvaluator.Matches("lessOrEqual", 5, 5), Is.True);

    [Test]
    public void Matches_LessOrEqual_TrueWhenLess()
        => Assert.That(AchievementEvaluator.Matches("lessOrEqual", 4, 5), Is.True);

    [Test]
    public void Matches_LessOrEqual_FalseWhenGreater()
        => Assert.That(AchievementEvaluator.Matches("lessOrEqual", 6, 5), Is.False);

    [Test]
    public void Matches_LessThan_TrueWhenLess()
        => Assert.That(AchievementEvaluator.Matches("lessThan", 4, 5), Is.True);

    [Test]
    public void Matches_LessThan_FalseWhenEqual()
        => Assert.That(AchievementEvaluator.Matches("lessThan", 5, 5), Is.False);

    [Test]
    public void Matches_UnknownComparison_ReturnsFalse()
        => Assert.That(AchievementEvaluator.Matches("invalid", 1, 1), Is.False);
}
