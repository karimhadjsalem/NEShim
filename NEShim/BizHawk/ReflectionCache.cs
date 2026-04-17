using System.Reflection;
using BizHawk.Common;
using BizHawk.Common.StringExtensions;

namespace BizHawk;

public static class ReflectionCache
{
    private const string EMBED_PREFIX = "BizHawk";

    private static Type[]? _types = null;

    private static readonly Assembly Asm = typeof(BizHawk.ReflectionCache).Assembly;

    public static readonly Version AsmVersion = Asm.GetName().Version!;

    public static Type[] Types => _types ??= Asm.GetTypesWithoutLoadErrors().ToArray();

    public static IEnumerable<string> EmbeddedResourceList(string extraPrefix)
    {
        var fullPrefix = EMBED_PREFIX + extraPrefix;
        return Asm.GetManifestResourceNames().Where(s => s.StartsWithOrdinal(fullPrefix)) // seems redundant with `RemovePrefix`, but we only want these in the final list
            .Select(s => s.RemovePrefix(fullPrefix));
    }

    public static IEnumerable<string> EmbeddedResourceList()
        => EmbeddedResourceList(string.Empty); // can't be simplified to `Asm.GetManifestResourceNames` call

    /// <exception cref=""ArgumentException"">not found</exception>
    public static Stream EmbeddedResourceStream(string embedPath)
    {
        var fullPath = EMBED_PREFIX + embedPath;
        return Asm.GetManifestResourceStream(fullPath)
               ?? throw new ArgumentException(paramName: nameof(embedPath), message: $"resource at {{fullPath}} not found");
    }
}