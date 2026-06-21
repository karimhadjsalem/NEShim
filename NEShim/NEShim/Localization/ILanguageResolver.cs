namespace NEShim.Localization;

/// <summary>
/// Strategy for resolving the active UI language code (e.g. "english", "french").
/// Returns null when this resolver has no opinion so the next in the chain is tried.
/// </summary>
internal interface ILanguageResolver
{
    string? Resolve();
}
