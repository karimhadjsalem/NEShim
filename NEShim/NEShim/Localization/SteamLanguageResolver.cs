using NEShim.Steam;

namespace NEShim.Localization;

/// <summary>
/// Resolves the UI language from the Steam client.
/// Returns null when Steam is not available or returned no language.
/// </summary>
internal sealed class SteamLanguageResolver : ILanguageResolver
{
    public string? Resolve()
    {
        if (!SteamManager.IsAvailable)
        {
            Logger.Log("[Localization] SteamResolver: Steam not available — skipping.");
            return null;
        }

        string? lang = SteamManager.GameLanguage;
        if (string.IsNullOrEmpty(lang))
        {
            Logger.Log("[Localization] SteamResolver: Steam returned empty language — skipping.");
            return null;
        }

        Logger.Log($"[Localization] SteamResolver: resolved '{lang}'.");
        return lang;
    }
}
