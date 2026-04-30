namespace DotnetNx.Core;

public static class HostOperatingSystems
{
    public static readonly string[] All = ["linux", "macos", "windows"];

    public static IReadOnlyList<string> Normalize(IEnumerable<string> values)
    {
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "":
                    break;
                case "any":
                case "all":
                    normalized.UnionWith(All);
                    break;
                case "linux":
                    normalized.Add("linux");
                    break;
                case "mac":
                case "osx":
                case "macos":
                    normalized.Add("macos");
                    break;
                case "win":
                case "windows":
                    normalized.Add("windows");
                    break;
            }
        }

        return normalized.Count == 0 ? All : normalized.ToArray();
    }

    public static IReadOnlyList<string> Parse(string value)
    {
        var parts = value.Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Normalize(parts);
    }

    public static IReadOnlyList<string> GetInvalidValues(string value)
    {
        var invalid = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in value.Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = part.ToLowerInvariant();
            if (normalized is not ("linux" or "macos" or "mac" or "osx" or "windows" or "win" or "any" or "all"))
            {
                invalid.Add(part);
            }
        }

        return invalid.ToArray();
    }

    public static IReadOnlyList<string> InferFromTargetFrameworks(IEnumerable<string> targetFrameworks)
    {
        var values = new SortedSet<string>(StringComparer.Ordinal);
        var sawTargetFramework = false;

        foreach (var targetFramework in targetFrameworks.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            sawTargetFramework = true;
            values.UnionWith(InferFromTargetFramework(targetFramework));
        }

        return sawTargetFramework ? values.ToArray() : All;
    }

    public static IReadOnlyList<string> InferFromTargetFramework(string targetFramework)
    {
        var normalized = targetFramework.Trim().ToLowerInvariant();

        if (normalized.Contains("-ios", StringComparison.Ordinal) ||
            normalized.Contains("-maccatalyst", StringComparison.Ordinal) ||
            normalized.Contains("-tvos", StringComparison.Ordinal) ||
            normalized.Contains("-macos", StringComparison.Ordinal))
        {
            return ["macos"];
        }

        if (normalized.Contains("-windows", StringComparison.Ordinal))
        {
            return ["windows"];
        }

        return All;
    }

    public static IReadOnlyList<string> ToTags(IReadOnlyCollection<string> buildableOn)
    {
        var tags = buildableOn
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => $"os:{value}")
            .ToList();

        if (All.All(value => buildableOn.Contains(value)))
        {
            tags.Add("os:any");
        }

        return tags;
    }
}
