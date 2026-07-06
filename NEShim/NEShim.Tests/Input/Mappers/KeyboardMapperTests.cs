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
        // SDL.Keycode.Return.ToString() = "Return"; the keyboard source emits "Return" and
        // the config binding "Return" both parse to SDL.Keycode.Return via Enum.TryParse.
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "W", "S", "Return" }, _config, builder);
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
        // Identifiers are SDL.Keycode.ToString() values produced by KeyboardInputSource.
        // Config binding keys are SDL.Keycode names stored in AppConfig.InputMappings defaults.
        var ids = new HashSet<string>
            { "W", "S", "A", "D", "Period", "Comma", "Return", "RShift" };
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

    // ── SDL.Keycode name round-trip tests ────────────────────────────────────────
    // Validates that SDL.Keycode enum names serialise correctly to/from config strings.

    [Test]
    public void ParseKey_Return_ParsesCorrectly()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("Return", null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "Return" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void ParseKey_CommonMovementKeys_ParseCorrectly()
    {
        foreach (var key in new[] { "Up", "Down", "Left", "Right", "Space", "Escape" })
        {
            _config.InputMappings["P1 Up"] = new InputBinding(key, null);
            var builder = NewBuilder();
            _mapper.Map(new HashSet<string> { key }, _config, builder);
            Assert.That(builder.Contains("P1 Up"), Is.True, $"Key '{key}' should parse");
        }
    }

    [Test]
    public void ParseKey_FunctionKeys_ParseCorrectly()
    {
        foreach (var key in new[] { "F1", "F11", "F12" })
        {
            _config.InputMappings["P1 Up"] = new InputBinding(key, null);
            var builder = NewBuilder();
            _mapper.Map(new HashSet<string> { key }, _config, builder);
            Assert.That(builder.Contains("P1 Up"), Is.True, $"Key '{key}' should parse");
        }
    }

    [Test]
    public void ParseKey_Letters_ParseCorrectly()
    {
        foreach (var key in new[] { "A", "Z" })
        {
            _config.InputMappings["P1 Up"] = new InputBinding(key, null);
            var builder = NewBuilder();
            _mapper.Map(new HashSet<string> { key }, _config, builder);
            Assert.That(builder.Contains("P1 Up"), Is.True, $"Key '{key}' should parse");
        }
    }

    [Test]
    public void ParseKey_CaseInsensitive_Succeeds()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("escape", null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "Escape" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void ParseKey_WinFormsName_NoLongerResolves()
    {
        // WinForms name "NumPad0" has no SDL.Keycode equivalent of that name.
        // SDL uses "Kp0" instead — old configs with "NumPad0" will silently fail to bind.
        _config.InputMappings["P1 Up"] = new InputBinding("NumPad0", null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "NumPad0" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }

    // ── SDL key name coverage — keys whose names differ from WinForms ────────────
    // These confirm the positive side: users who rebind to these SDL names in config.json
    // get a working binding. The negative side (WinForms names no longer parse) is above.

    [TestCase("Alpha0")]
    [TestCase("Alpha9")]
    public void ParseKey_SdlDigitRowNames_ParseCorrectly(string key)
    {
        // SDL: "Alpha0"–"Alpha9"   (WinForms was "D0"–"D9")
        _config.InputMappings["P1 Up"] = new InputBinding(key, null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { key }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [TestCase("Kp0")]
    [TestCase("Kp9")]
    public void ParseKey_SdlNumpadNames_ParseCorrectly(string key)
    {
        // SDL: "Kp0"–"Kp9"   (WinForms was "NumPad0"–"NumPad9")
        _config.InputMappings["P1 Up"] = new InputBinding(key, null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { key }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [TestCase("LShift")]
    [TestCase("RShift")]
    [TestCase("LCtrl")]
    [TestCase("RCtrl")]
    [TestCase("LAlt")]
    [TestCase("RAlt")]
    public void ParseKey_SdlModifierNames_ParseCorrectly(string key)
    {
        // SDL: "LShift"/"RShift"   (WinForms: "LShiftKey"/"RShiftKey")
        // SDL: "LCtrl"/"RCtrl"     (WinForms: "LControlKey"/"RControlKey")
        // SDL: "LAlt"/"RAlt"       (WinForms: "LMenu"/"RMenu")
        _config.InputMappings["P1 Up"] = new InputBinding(key, null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { key }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [TestCase("Backspace")]
    [TestCase("Period")]
    [TestCase("Comma")]
    public void ParseKey_SdlPunctuationNames_ParseCorrectly(string key)
    {
        // SDL: "Backspace"   (WinForms: "Back")
        // SDL: "Period"      (WinForms: "OemPeriod")
        // SDL: "Comma"       (WinForms: "OemComma")
        _config.InputMappings["P1 Up"] = new InputBinding(key, null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { key }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [TestCase("D0")]
    [TestCase("LShiftKey")]
    [TestCase("LControlKey")]
    [TestCase("LMenu")]
    [TestCase("Back")]
    [TestCase("OemComma")]
    public void ParseKey_WinFormsRenamedKeys_NoLongerResolve(string key)
    {
        _config.InputMappings["P1 Up"] = new InputBinding(key, null);
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { key }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }
}
