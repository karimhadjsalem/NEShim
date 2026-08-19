using NSubstitute;
using SDL3;
using NEShim.Input;
using NEShim.Input.Sources;
using NEShim.Localization;

namespace NEShim.Tests.Input.Sources;

/// <summary>
/// Only exercises the "unrecognized brand" path — the "brand recognized, loads a bundled
/// asset" path crosses into real SDL_image decoding and belongs in an integration test, not
/// here (see CLAUDE.md's unit-test boundary-crossing rules).
/// </summary>
[TestFixture]
internal class SdlBundledGlyphSourceTests
{
    private IGamepadDevice _device = null!;
    private SdlBundledGlyphSource _source = null!;
    private LocalizationData _localization = null!;

    [SetUp]
    public void SetUp()
    {
        _device = Substitute.For<IGamepadDevice>();
        _source = new SdlBundledGlyphSource(_device);
        _localization = new LocalizationData();
    }

    [TearDown]
    public void TearDown() => _device.Dispose();

    [Test]
    public void TryResolve_UnknownGamepadType_ReturnsNull()
    {
        _device.GetGamepadType(Arg.Any<uint>()).Returns(SDL.GamepadType.Unknown);

        Assert.That(_source.TryResolve("A", _localization), Is.Null);
    }

    // ── BrandFor: pure mapping logic, no SDL_image decode involved ─────────────

    [TestCase(SDL.GamepadType.Xbox360, "xbox")]
    [TestCase(SDL.GamepadType.XboxOne, "xbox")]
    // "Standard" = SDL's generic/XInput-compatible fallback type. Its face-button semantics
    // match Xbox's (A=south, B=east, X=west, Y=north), so Xbox art is a faithful reuse — unlike
    // the bundled Kenney "Generic" pack, which targets joysticks/HOTAS devices, not gamepads.
    [TestCase(SDL.GamepadType.Standard, "xbox")]
    [TestCase(SDL.GamepadType.PS3, "playstation")]
    [TestCase(SDL.GamepadType.PS4, "playstation")]
    [TestCase(SDL.GamepadType.PS5, "playstation")]
    [TestCase(SDL.GamepadType.NintendoSwitchPro, "switchpro")]
    [TestCase(SDL.GamepadType.NintendoSwitchJoyconLeft, "switchpro")]
    [TestCase(SDL.GamepadType.NintendoSwitchJoyconRight, "switchpro")]
    [TestCase(SDL.GamepadType.NintendoSwitchJoyconPair, "switchjoyconpair")]
    [TestCase(SDL.GamepadType.Steam, "steamdeck")]
    public void BrandFor_RecognizedType_ReturnsExpectedBrand(SDL.GamepadType type, string expectedBrand)
    {
        Assert.That(SdlBundledGlyphSource.BrandFor(type), Is.EqualTo(expectedBrand));
    }

    [TestCase(SDL.GamepadType.Unknown)]
    public void BrandFor_UnrecognizedType_ReturnsNull(SDL.GamepadType type)
    {
        Assert.That(SdlBundledGlyphSource.BrandFor(type), Is.Null);
    }
}
