using NEShim.Platform;
using SDL3;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class SDL3WindowHostTests
{
    // ---- Letters ----

    [Test]
    public void TryMapKey_LowercaseLetter_MapsToUppercaseKey()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.A, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.A));
    }

    [Test]
    public void TryMapKey_LastLetter_MapsCorrectly()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Z, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Z));
    }

    // ---- Digits ----

    [Test]
    public void TryMapKey_Digit_MapsToD0Format()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Alpha0, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.D0));
    }

    [Test]
    public void TryMapKey_Digit9_MapsToD9()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Alpha9, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.D9));
    }

    // ---- Function keys ----

    [Test]
    public void TryMapKey_F1_MapsToF1()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.F1, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.F1));
    }

    [Test]
    public void TryMapKey_F11_MapsToF11()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.F11, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.F11));
    }

    [Test]
    public void TryMapKey_F12_MapsToF12()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.F12, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.F12));
    }

    // ---- Critical control keys ----

    [Test]
    public void TryMapKey_Escape_MapsToEscape()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Escape, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Escape));
    }

    [Test]
    public void TryMapKey_Return_MapsToReturn()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Return, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Return));
    }

    [Test]
    public void TryMapKey_NumpadEnter_MapsToReturn()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.KpEnter, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Return));
    }

    [Test]
    public void TryMapKey_Space_MapsToSpace()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Space, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Space));
    }

    // ---- Arrow keys ----

    [Test]
    public void TryMapKey_ArrowUp_MapsToUp()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Up, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Up));
    }

    [Test]
    public void TryMapKey_ArrowDown_MapsToDown()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.Down, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.Down));
    }

    // ---- Modifier keys ----

    [Test]
    public void TryMapKey_LShift_MapsToLShiftKey()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.LShift, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.LShiftKey));
    }

    [Test]
    public void TryMapKey_LCtrl_MapsToLControlKey()
    {
        bool result = SDL3WindowHost.TryMapKey(SDL.Keycode.LCtrl, out var key);
        Assert.That(result, Is.True);
        Assert.That(key, Is.EqualTo(Keys.LControlKey));
    }

    // ---- Unknown keycode ----

    [Test]
    public void TryMapKey_UnknownKeycode_ReturnsFalse()
    {
        bool result = SDL3WindowHost.TryMapKey((SDL.Keycode)0x7FFFFFFF, out _);
        Assert.That(result, Is.False);
    }
}
