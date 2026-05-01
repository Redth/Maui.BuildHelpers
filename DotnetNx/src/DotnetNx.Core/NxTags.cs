namespace DotnetNx.Core;

internal static class NxTags
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string> values)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            foreach (var tag in Split(value))
            {
                tags.Add(tag);
            }
        }

        return tags.ToArray();
    }

    public static IReadOnlyList<string> InferFromTargetFramework(string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return [];
        }

        var normalized = targetFramework.Trim().ToLowerInvariant();
        var tags = new SortedSet<string>(StringComparer.Ordinal)
        {
            $"tfm:{normalized}",
        };

        if (normalized.Contains("-android", StringComparison.Ordinal))
        {
            tags.Add("platform:android");
        }

        if (normalized.Contains("-ios", StringComparison.Ordinal))
        {
            tags.Add("platform:ios");
        }

        if (normalized.Contains("-maccatalyst", StringComparison.Ordinal))
        {
            tags.Add("platform:maccatalyst");
        }

        if (normalized.Contains("-tvos", StringComparison.Ordinal))
        {
            tags.Add("platform:tvos");
        }

        if (normalized.Contains("-macos", StringComparison.Ordinal))
        {
            tags.Add("platform:macos");
        }

        if (normalized.Contains("-windows", StringComparison.Ordinal))
        {
            tags.Add("platform:windows");
        }

        return tags.ToArray();
    }

    public static IReadOnlyList<string> InferFromProjectProperties(MSBuildProjectEvaluation evaluation)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        if (IsTrue(evaluation.IsTestProject))
        {
            tags.Add("type:test");
        }

        if (IsTrue(evaluation.IsPackable))
        {
            tags.Add("type:packable");
        }

        if (IsTrue(evaluation.PackAsTool))
        {
            tags.Add("type:tool");
        }

        if (IsTrue(evaluation.UseMaui))
        {
            tags.Add("sdk:maui");
        }

        return tags.ToArray();
    }

    private static IEnumerable<string> Split(string value) =>
        value
            .Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part));

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.Ordinal);
}
