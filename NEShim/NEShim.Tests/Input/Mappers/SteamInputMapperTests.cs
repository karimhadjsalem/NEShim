using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;
using NEShim.Input.Mappers;

namespace NEShim.Tests.Input.Mappers;

[TestFixture]
internal class SteamInputMapperTests
{
    private SteamInputMapper _mapper = null!;
    private AppConfig        _config = null!;

    [SetUp]
    public void SetUp()
    {
        _mapper = new SteamInputMapper();
        _config = new AppConfig();
    }

    private static ImmutableHashSet<string>.Builder NewBuilder()
        => ImmutableHashSet.CreateBuilder<string>();

    [Test]
    public void Map_KnownAction_Up_MapsToP1Up()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "up" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void Map_All8KnownActions_AllMapped()
    {
        var ids = new HashSet<string>
            { "up", "down", "left", "right", "a_button", "b_button", "start", "select" };
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
    public void Map_UnknownAction_Ignored()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "unknown_action" }, _config, builder);
        Assert.That(builder, Is.Empty);
    }

    [Test]
    public void Map_EmptyIdentifiers_NothingAdded()
    {
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string>(), _config, builder);
        Assert.That(builder, Is.Empty);
    }

    [Test]
    public void Map_NoConfigDependency_SameResultRegardlessOfConfig()
    {
        var ids = new HashSet<string> { "up" };

        var builder1 = NewBuilder();
        _mapper.Map(ids, new AppConfig(), builder1);

        var builder2 = NewBuilder();
        _mapper.Map(ids, new AppConfig(), builder2);

        Assert.That(builder1.Contains("P1 Up"), Is.True);
        Assert.That(builder2.Contains("P1 Up"), Is.True);
    }

    [Test]
    public void Map_ActionNameCaseSensitive_WrongCase_NotMapped()
    {
        // VDF action names are lowercase; "Up" (capitalised) is not in the table
        var builder = NewBuilder();
        _mapper.Map(new HashSet<string> { "Up" }, _config, builder);
        Assert.That(builder.Contains("P1 Up"), Is.False);
    }
}
