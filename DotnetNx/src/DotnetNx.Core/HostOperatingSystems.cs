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

    public static IReadOnlyList<string> InferFromTargetFrameworks(IEnumerable<string> targetFrameworks) => All;

    public static IReadOnlyList<string> InferFromTargetPlatform(string targetPlatformIdentifier)
    {
        var platform = TargetFrameworkPartsParser.NormalizeTagValue(targetPlatformIdentifier);
        if (platform is "ios" or "maccatalyst" or "tvos" or "macos")
        {
            return ["macos"];
        }

        if (platform == "windows")
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
