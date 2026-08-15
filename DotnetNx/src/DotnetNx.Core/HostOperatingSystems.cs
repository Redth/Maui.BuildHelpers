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

        return normalized.ToArray();
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

    public static IReadOnlyList<string> InferBuildHosts(
        string targetPlatformIdentifier,
        IEnumerable<string> runtimeIdentifiers)
    {
        var platform = TargetFrameworkPartsParser.NormalizeTagValue(targetPlatformIdentifier);
        IReadOnlyList<string> platformHosts;
        if (platform is "ios" or "maccatalyst" or "tvos" or "macos")
        {
            platformHosts = ["macos"];
        }
        else if (platform == "windows")
        {
            platformHosts = ["windows"];
        }
        else
        {
            platformHosts = All;
        }

        var ridHosts = runtimeIdentifiers
            .Select(InferHostFromRuntimeIdentifier)
            .Where(host => host is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return ridHosts.Length == 0
            ? platformHosts
            : Intersect([platformHosts, ridHosts]);
    }

    public static IReadOnlyList<string> Intersect(IEnumerable<IReadOnlyList<string>> hostSets)
    {
        HashSet<string>? intersection = null;
        foreach (var hostSet in hostSets)
        {
            if (intersection is null)
            {
                intersection = new HashSet<string>(hostSet, StringComparer.Ordinal);
            }
            else
            {
                intersection.IntersectWith(hostSet);
            }
        }

        return intersection?.OrderBy(host => host, StringComparer.Ordinal).ToArray() ?? [];
    }

    private static string? InferHostFromRuntimeIdentifier(string runtimeIdentifier)
    {
        var rid = runtimeIdentifier.Trim().ToLowerInvariant();
        if (rid.StartsWith("win-", StringComparison.Ordinal) || rid == "win")
        {
            return "windows";
        }

        if (rid.StartsWith("osx-", StringComparison.Ordinal) ||
            rid.StartsWith("ios-", StringComparison.Ordinal) ||
            rid.StartsWith("maccatalyst-", StringComparison.Ordinal))
        {
            return "macos";
        }

        if (rid.StartsWith("linux-", StringComparison.Ordinal) || rid == "linux")
        {
            return "linux";
        }

        return null;
    }
}
