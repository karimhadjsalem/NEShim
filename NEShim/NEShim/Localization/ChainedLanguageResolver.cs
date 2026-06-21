namespace NEShim.Localization;

/// <summary>
/// Chain of Responsibility: tries each <see cref="ILanguageResolver"/> in order and returns
/// the first non-null result. Returns null if all resolvers decline.
/// </summary>
internal sealed class ChainedLanguageResolver : ILanguageResolver
{
    private readonly IReadOnlyList<ILanguageResolver> _resolvers;

    public ChainedLanguageResolver(IReadOnlyList<ILanguageResolver> resolvers)
        => _resolvers = resolvers;

    public string? Resolve()
    {
        Logger.Log($"[Localization] ChainedResolver: trying {_resolvers.Count} resolver(s).");
        foreach (var resolver in _resolvers)
        {
            string? result = resolver.Resolve();
            if (result != null)
            {
                Logger.Log($"[Localization] ChainedResolver: {resolver.GetType().Name} returned '{result}' — using this.");
                return result;
            }
        }
        Logger.Log("[Localization] ChainedResolver: all resolvers returned null.");
        return null;
    }
}
