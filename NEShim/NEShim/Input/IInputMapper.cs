using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input;

/// <summary>
/// Strategy interface for translating raw device identifiers into NES button names.
/// Each implementation handles the identifier vocabulary of one <see cref="IInputSource"/>.
/// </summary>
internal interface IInputMapper
{
    /// <summary>
    /// Translates active raw identifiers into NES button names and adds them to
    /// <paramref name="target"/>. Called once per frame per paired source.
    /// Multiple mappers share the same builder; duplicates are suppressed by the builder.
    /// </summary>
    void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
             ImmutableHashSet<string>.Builder target);
}
