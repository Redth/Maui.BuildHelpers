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

    public static IReadOnlyList<string> InferFromTargetFramework(MSBuildProjectEvaluation evaluation)
    {
        if (string.IsNullOrWhiteSpace(evaluation.TargetFramework))
        {
            return [];
        }

        var tags = new SortedSet<string>(StringComparer.Ordinal);
        var parts = TargetFrameworkPartsParser.FromEvaluation(evaluation);
        if (parts is null)
        {
            tags.Add($"tfm:{TargetFrameworkPartsParser.NormalizeTagValue(evaluation.TargetFramework)}");
            return tags.ToArray();
        }

        tags.Add($"tfm:{parts.ShortName}");
        AddTag(tags, "tfm-framework", parts.Framework);
        AddTag(tags, "tfm-framework-version", parts.FrameworkVersion);

        AddTag(tags, "tfm-profile", parts.Profile);
        if (!string.IsNullOrWhiteSpace(parts.Platform))
        {
            tags.Add($"platform:{parts.Platform}");
            tags.Add($"tfm-platform:{parts.Platform}");
        }

        AddTag(tags, "tfm-platform-version", parts.PlatformVersion);

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

        var packageId = GetPackageId(evaluation);
        if (IsTrue(evaluation.IsPackable) || packageId is not null)
        {
            tags.Add("type:nuget");
            AddTag(tags, "package-id", packageId);
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

    public static string? GetPackageId(MSBuildProjectEvaluation evaluation) =>
        TargetFrameworkPartsParser.NormalizeOptionalTagValue(evaluation.PackageId) ??
        (IsTrue(evaluation.IsPackable)
            ? TargetFrameworkPartsParser.NormalizeOptionalTagValue(evaluation.AssemblyName)
            : null);

    private static IEnumerable<string> Split(string value) =>
        value
            .Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part));

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.Ordinal);

    private static void AddTag(ISet<string> tags, string prefix, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add($"{prefix}:{value}");
        }
    }
}
