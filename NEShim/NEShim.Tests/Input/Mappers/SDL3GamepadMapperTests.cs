using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;
using NEShim.Input.Mappers;

namespace NEShim.Tests.Input.Mappers;

[TestFixture]
internal class SDL3GamepadMapperTests
{
    private SDL3GamepadMapper _mapper = null!;
    private AppConfig         _config = null!;

    [SetUp]
    public void SetUp()
    {
        _mapper = new SDL3GamepadMapper();
        _config = new AppConfig();
    }

    private static ImmutableHashSet<string>.Builder NewBuilder()
        => ImmutableHashSet.CreateBuilder<string>();

    // ── Analog identifiers (via GamepadButton2 in default config) ───────────────

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

    [Test]
    public void Map_AnalogUp_WhenGamepadButton2Cleared_NotMapped()
    {
        // Removing GamepadButton2 from an action removes the analog binding for that action.
        _config.InputMappings["P1 Up"] = new InputBinding("W", "DPadUp"); // no GamepadButton2
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "AnalogUp" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }

    // ── Digital buttons (config-mapped via GamepadButton) ──────────────────────

    [Test]
    public void Map_ConfiguredDigitalButton_MapsToNesButton()
    {
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

    // ── GamepadButton2 does not apply Start protection ─────────────────────────

    [Test]
    public void Map_GamepadButton2_FiresWithoutStartProtectionCheck()
    {
        // GamepadButton2 is for analog identifiers; the Start-protection guard is
        // only on GamepadButton. Verify that GamepadButton2 fires unconditionally.
        _config.InputMappings["P1 A"] = new InputBinding("Period", null) { GamepadButton2 = "A" };
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "A" }, _config, builder);
        Assert.That(builder.Contains("P1 A"), Is.True);
    }

    // ── Per-player scoping ──────────────────────────────────────────────────────
    // Required, not cosmetic: two different physical gamepads report the same raw identifiers
    // (e.g. both press "DPadUp"), so a player-2 mapper must never satisfy a "P1 ..." binding.

    [Test]
    public void Player2Mapper_IgnoresPlayer1ConfigEntries()
    {
        var player2Mapper = new SDL3GamepadMapper(player: 2);
        var builder = NewBuilder();
        player2Mapper.Map(new HashSet<string> { "DPadUp" }, _config, builder);

        Assert.That(builder.Contains("P1 Up"), Is.False);
        Assert.That(builder.Contains("P2 Up"), Is.True);
    }

    [Test]
    public void Player1Mapper_IgnoresPlayer2ConfigEntries()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "DPadUp" }, _config, builder);

        Assert.That(builder.Contains("P1 Up"), Is.True);
        Assert.That(builder.Contains("P2 Up"), Is.False);
    }

    [Test]
    public void TwoPlayersWithSameRawIdentifier_EachOnlyMapsOwnButton()
    {
        var player2Mapper = new SDL3GamepadMapper(player: 2);
        var builder = NewBuilder();

        // Both players' physical gamepads happen to report "A" simultaneously.
        _mapper.Map(new HashSet<string> { "A" }, _config, builder);
        player2Mapper.Map(new HashSet<string> { "A" }, _config, builder);

        Assert.That(builder.Contains("P1 A"), Is.True);
        Assert.That(builder.Contains("P2 A"), Is.True);
    }
}
