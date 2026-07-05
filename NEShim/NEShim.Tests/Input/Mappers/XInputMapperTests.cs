using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;
using NEShim.Input.Mappers;

namespace NEShim.Tests.Input.Mappers;

[TestFixture]
internal class XInputMapperTests
{
    private XInputMapper _mapper = null!;
    private AppConfig    _config = null!;

    [SetUp]
    public void SetUp()
    {
        _mapper = new XInputMapper();
        _config = new AppConfig();
    }

    private static ImmutableHashSet<string>.Builder NewBuilder()
        => ImmutableHashSet.CreateBuilder<string>();

    // ── Analog identifiers (hardcoded mapping) ──────────────────────────────────

    [Test]
    public void Map_AnalogUp_MapsToP1Up()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "AnalogUp" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void Map_AnalogDown_MapsToP1Down()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "AnalogDown" }, _config, builder);
        Assert.That(builder.Contains("P1 Down"), Is.True);
    }

    [Test]
    public void Map_AnalogLeft_MapsToP1Left()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "AnalogLeft" }, _config, builder);
        Assert.That(builder.Contains("P1 Left"), Is.True);
    }

    [Test]
    public void Map_AnalogRight_MapsToP1Right()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "AnalogRight" }, _config, builder);
        Assert.That(builder.Contains("P1 Right"), Is.True);
    }

    // ── Digital buttons (config-mapped) ────────────────────────────────────────

    [Test]
    public void Map_ConfiguredDigitalButton_MapsToNesButton()
    {
        // Default: P1 Up → DPadUp
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "DPadUp" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void Map_NullGamepadBinding_Skipped()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("W", null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "DPadUp" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }

    [Test]
    public void Map_EmptyIdentifiers_NothingAdded()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string>(), _config, builder);
        Assert.That(builder, Is.Empty);
    }

    [Test]
    public void Map_DigitalAndAnalogSameDirection_BothMapToSameButton()
    {
        // DPadUp → P1 Up (digital) and AnalogUp → P1 Up (analog) — deduped by builder
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "DPadUp", "AnalogUp" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
        Assert.That(builder.Count, Is.EqualTo(1));
    }

    [Test]
    public void Map_StartButton_ProtectedByDefault_NotMapped()
    {
        _config.InputMappings["P1 Start"] = new InputBinding("Return", "Start");
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "Start" }, _config, builder);
        // OverrideStartBindingProtection = false → Start is blocked from NES mapping
        Assert.That(builder.Contains("P1 Start"), Is.False);
    }

    [Test]
    public void Map_StartButton_ProtectionOverridden_IsMapped()
    {
        _config.InputMappings["P1 Start"] = new InputBinding("Return", "Start");
        _config.OverrideStartBindingProtection = true;
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "Start" }, _config, builder);
        Assert.That(builder.Contains("P1 Start"), Is.True);
    }

    [Test]
    public void Map_UnrecognizedIdentifier_Ignored()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "UnknownButton" }, _config, builder);
        Assert.That(builder, Is.Empty);
    }
}
