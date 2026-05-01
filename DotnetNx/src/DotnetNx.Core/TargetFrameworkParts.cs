namespace DotnetNx.Core;

internal sealed record TargetFrameworkParts(
    string ShortName,
    string Framework,
    string FrameworkVersion,
    string? Profile,
    string? Platform,
    string? PlatformVersion);

internal static class TargetFrameworkPartsParser
{
    public static TargetFrameworkParts? FromEvaluation(MSBuildProjectEvaluation evaluation)
    {
        if (string.IsNullOrWhiteSpace(evaluation.TargetFramework))
        {
            return null;
        }

        return new TargetFrameworkParts(
            NormalizeTagValue(evaluation.TargetFramework),
            NormalizeFramework(evaluation.TargetFrameworkIdentifier),
            NormalizeVersion(evaluation.TargetFrameworkVersion),
            NormalizeOptionalTagValue(evaluation.TargetFrameworkProfile),
            NormalizeOptionalTagValue(evaluation.TargetPlatformIdentifier),
            NormalizeOptionalVersion(evaluation.TargetPlatformVersion));
    }

    public static string NormalizeTagValue(string value) =>
        value.Trim().ToLowerInvariant();

    public static string? NormalizeOptionalTagValue(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeTagValue(value);

    private static string NormalizeFramework(string framework) =>
        NormalizeTagValue(framework).TrimStart('.');

    private static string NormalizeVersion(string version) =>
        NormalizeTagValue(version).TrimStart('v');

    private static string? NormalizeOptionalVersion(string version) =>
        string.IsNullOrWhiteSpace(version) ? null : NormalizeVersion(version);
}
