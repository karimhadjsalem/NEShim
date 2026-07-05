using System.Windows.Forms;
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
        _source.OnKeyDown(Keys.W);
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Contains.Item("W"));
    }

    [Test]
    public void OnKeyUp_RemovesKeyFromIdentifiers()
    {
        _source.OnKeyDown(Keys.W);
        _source.OnKeyUp(Keys.W);
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Is.Empty);
    }

    [Test]
    public void MultipleKeysDown_AllAppearInIdentifiers()
    {
        _source.OnKeyDown(Keys.W);
        _source.OnKeyDown(Keys.A);
        _source.OnKeyDown(Keys.Return); // Keys.Return.ToString() = "Enter"

        var ids = _source.GetActiveIdentifiers(new AppConfig());
        Assert.That(ids, Is.SupersetOf(new[] { "W", "A", "Enter" }));
    }

    [Test]
    public void GetActiveIdentifiers_UsesKeyToStringConvention()
    {
        // Keys.OemPeriod → "OemPeriod", not a numeric code
        _source.OnKeyDown(Keys.OemPeriod);
        Assert.That(_source.GetActiveIdentifiers(new AppConfig()), Contains.Item("OemPeriod"));
    }

    [Test]
    public void IsKeyPressed_TrueWhenKeyIsDown()
    {
        _source.OnKeyDown(Keys.Space);
        Assert.That(_source.IsKeyPressed(Keys.Space), Is.True);
    }

    [Test]
    public void IsKeyPressed_FalseWhenKeyNotDown()
    {
        Assert.That(_source.IsKeyPressed(Keys.Space), Is.False);
    }

    [Test]
    public void IsKeyPressed_FalseAfterKeyUp()
    {
        _source.OnKeyDown(Keys.Space);
        _source.OnKeyUp(Keys.Space);
        Assert.That(_source.IsKeyPressed(Keys.Space), Is.False);
    }

    [Test]
    public void GetPressedKeysCopy_ReflectsCurrentState()
    {
        _source.OnKeyDown(Keys.F5);
        Assert.That(_source.GetPressedKeysCopy(), Contains.Item(Keys.F5));
    }

    [Test]
    public void GetPressedKeysCopy_IsSnapshot_NotLive()
    {
        _source.OnKeyDown(Keys.F5);
        var copy = _source.GetPressedKeysCopy();
        _source.OnKeyUp(Keys.F5);
        // The snapshot captured before KeyUp should still have F5
        Assert.That(copy, Contains.Item(Keys.F5));
    }
}
