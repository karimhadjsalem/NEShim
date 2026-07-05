using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;
using NEShim.Input.Mappers;

namespace NEShim.Tests.Input.Mappers;

[TestFixture]
internal class KeyboardMapperTests
{
    private KeyboardMapper _mapper = null!;
    private AppConfig      _config = null!;

    [SetUp]
    public void SetUp()
    {
        _mapper = new KeyboardMapper();
        _config = new AppConfig();
    }

    private static ImmutableHashSet<string>.Builder NewBuilder()
        => ImmutableHashSet.CreateBuilder<string>();

    [Test]
    public void Map_KeyPresentInIdentifiers_AddsNesButton()
    {
        // Default: W → P1 Up
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "W" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void Map_KeyAbsent_DoesNotAdd()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string>(), _config, builder);
        Assert.That(builder, Is.Empty);
    }

    [Test]
    public void Map_NullKeyBinding_Skipped()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, "DPadUp");
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "W" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }

    [Test]
    public void Map_MultipleKeys_AllMapped()
    {
        // Keys.Return.ToString() = "Enter" (alias), so identifier is "Enter" not "Return".
        // The mapper resolves binding key "Return" → Keys.Return → "Enter" via Enum.TryParse.
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "W", "S", "Enter" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"),    Is.True);
        Assert.That(builder.Contains("P1 Down"),  Is.True);
        Assert.That(builder.Contains("P1 Start"), Is.True);
    }

    [Test]
    public void Map_EmptyIdentifiers_NothingAdded()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string>(), _config, builder);
        Assert.That(builder, Is.Empty);
    }

    [Test]
    public void Map_UnmappedIdentifier_Ignored()
    {
        // "Z" is not in the default InputMappings
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "Z" }, _config, builder);
        Assert.That(builder, Is.Empty);
    }

    [Test]
    public void Map_AllDefaultBindings_MappedCorrectly()
    {
        // The mapper resolves identifiers and binding keys to Keys enum values,
        // so aliases ("Return"/"Enter", "OemComma"/"Oemcomma") match correctly
        // regardless of which casing the keyboard source or config uses.
        var ids = new HashSet<string>
            { "W", "S", "A", "D", "OemPeriod", "OemComma", "Return", "RShiftKey" };
        var builder = NewBuilder();
        _mapper.Map(ids, _config, builder);

        Assert.That(builder.Contains("P1 Up"),     Is.True);
        Assert.That(builder.Contains("P1 Down"),   Is.True);
        Assert.That(builder.Contains("P1 Left"),   Is.True);
        Assert.That(builder.Contains("P1 Right"),  Is.True);
        Assert.That(builder.Contains("P1 A"),      Is.True);
        Assert.That(builder.Contains("P1 B"),      Is.True);
        Assert.That(builder.Contains("P1 Start"),  Is.True);
        Assert.That(builder.Contains("P1 Select"), Is.True);
    }

    [Test]
    public void Map_InvalidKeyName_Skipped()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("NotAValidKey!!!", null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "NotAValidKey!!!" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }
}
