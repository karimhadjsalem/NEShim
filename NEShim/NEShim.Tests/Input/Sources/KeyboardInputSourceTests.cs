using SDL3;
using NEShim.Config;
using NEShim.Input.Sources;

namespace NEShim.Tests.Input.Sources;

[TestFixture]
internal class KeyboardInputSourceTests
{
    private KeyboardInputSource _source = null!;

    [SetUp]
    public void SetUp() => _source = new KeyboardInputSource();

    [Test]
    public void IsAvailable_AlwaysTrue()
    {
        Assert.That(_source.IsAvailable, Is.True);
    }

    [Test]
    public void GetActiveIdentifiers_NoKeysPressed_ReturnsEmpty()
    {
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Is.Empty);
    }

    [Test]
    public void OnKeyDown_KeyAppearsInIdentifiers()
    {
        _source.OnKeyDown(SDL.Keycode.W);
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Contains.Item("W"));
    }

    [Test]
    public void OnKeyUp_RemovesKeyFromIdentifiers()
    {
        _source.OnKeyDown(SDL.Keycode.W);
        _source.OnKeyUp(SDL.Keycode.W);
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Is.Empty);
    }

    [Test]
    public void MultipleKeysDown_AllAppearInIdentifiers()
    {
        _source.OnKeyDown(SDL.Keycode.W);
        _source.OnKeyDown(SDL.Keycode.A);
        _source.OnKeyDown(SDL.Keycode.Return); // SDL.Keycode.Return.ToString() = "Return"

        var ids = _source.GetActiveIdentifiers(new AppConfig());
        Assert.That(ids, Is.SupersetOf(new[] { "W", "A", "Return" }));
    }

    [Test]
    public void GetActiveIdentifiers_UsesKeyToStringConvention()
    {
        // SDL.Keycode.Period → "Period" (SDL name, not WinForms "OemPeriod")
        _source.OnKeyDown(SDL.Keycode.Period);
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Contains.Item("Period"));
    }

    [Test]
    public void IsKeyPressed_TrueWhenKeyIsDown()
    {
        _source.OnKeyDown(SDL.Keycode.Space);
        Assert.That(_source.IsKeyPressed(SDL.Keycode.Space), Is.True);
    }

    [Test]
    public void IsKeyPressed_FalseWhenKeyNotDown()
    {
        Assert.That(_source.IsKeyPressed(SDL.Keycode.Space), Is.False);
    }

    [Test]
    public void IsKeyPressed_FalseAfterKeyUp()
    {
        _source.OnKeyDown(SDL.Keycode.Space);
        _source.OnKeyUp(SDL.Keycode.Space);
        Assert.That(_source.IsKeyPressed(SDL.Keycode.Space), Is.False);
    }

    [Test]
    public void GetPressedKeysCopy_ReflectsCurrentState()
    {
        _source.OnKeyDown(SDL.Keycode.F5);
        Assert.That(_source.GetPressedKeysCopy(), Contains.Item(SDL.Keycode.F5));
    }

    [Test]
    public void GetPressedKeysCopy_IsSnapshot_NotLive()
    {
        _source.OnKeyDown(SDL.Keycode.F5);
        var copy = _source.GetPressedKeysCopy();
        _source.OnKeyUp(SDL.Keycode.F5);
        // The snapshot captured before KeyUp should still have F5
        Assert.That(copy, Contains.Item(SDL.Keycode.F5));
    }
}
