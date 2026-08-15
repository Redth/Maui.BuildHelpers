using Microsoft.Build.Evaluation;

namespace DotnetNx.Core;

internal static class MSBuildProjectReader
{
    public static string[] GetTargetFrameworks(string projectFile)
    {
        using var collection = CreateProjectCollection(targetFramework: null);
        var project = new Project(projectFile, collection.GlobalProperties, toolsVersion: null, collection);
        var targetFrameworks = project.GetPropertyValue("TargetFrameworks");
        if (!string.IsNullOrWhiteSpace(targetFrameworks))
        {
            return SplitPropertyList(targetFrameworks);
        }

        var targetFramework = project.GetPropertyValue("TargetFramework");
        return string.IsNullOrWhiteSpace(targetFramework) ? [] : [targetFramework];
    }

    public static MSBuildProjectEvaluation Evaluate(string projectFile, string? targetFramework)
    {
        using var collection = CreateProjectCollection(targetFramework);
        var project = new Project(projectFile, collection.GlobalProperties, toolsVersion: null, collection);
        return new MSBuildProjectEvaluation(
            GetProperty(project, "NxTags"),
            GetProperty(project, "NxBuildHosts"),
            GetProperty(project, "NxBuildableOn"),
            GetProperty(project, "NxTestHosts"),
            GetProperty(project, "NxRunHosts"),
            GetProperty(project, "NxPublishHosts"),
            GetProperty(project, "NxPackHosts"),
            GetProperty(project, "NxRestoreHosts"),
            project.GetPropertyValue("TargetFramework"),
            project.GetPropertyValue("TargetFrameworkIdentifier"),
            project.GetPropertyValue("TargetFrameworkVersion"),
            project.GetPropertyValue("TargetFrameworkProfile"),
            project.GetPropertyValue("TargetPlatformIdentifier"),
            project.GetPropertyValue("TargetPlatformVersion"),
            project.GetPropertyValue("IsTestProject"),
            project.GetPropertyValue("IsPackable"),
            project.GetPropertyValue("IsPublishable"),
            project.GetPropertyValue("PackAsTool"),
            project.GetPropertyValue("UseMaui"),
            project.GetPropertyValue("OutputType"),
            project.GetPropertyValue("PackageId"),
            project.GetPropertyValue("AssemblyName"),
            SplitPropertyList(project.GetPropertyValue("RuntimeIdentifier"))
                .Concat(SplitPropertyList(project.GetPropertyValue("RuntimeIdentifiers")))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            project.GetItems("PackageReference")
                .Select(item => item.EvaluatedInclude)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            project.GetItems("NxTag")
                .Select(item => new MSBuildItemValue(item.EvaluatedInclude, item.Xml?.Location.File))
                .ToArray());
    }

    private static string[] SplitPropertyList(string value) =>
        value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static ProjectCollection CreateProjectCollection(string? targetFramework)
    {
        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            globalProperties["TargetFramework"] = targetFramework;
        }

        return new ProjectCollection(globalProperties);
    }

    private static MSBuildPropertyValue GetProperty(Project project, string propertyName)
    {
        var property = project.GetProperty(propertyName);
        return new MSBuildPropertyValue(
            property?.EvaluatedValue ?? string.Empty,
            property?.Xml?.Location.File);
    }
}

internal sealed record MSBuildPropertyValue(string Value, string? SourceFile);

internal sealed record MSBuildItemValue(string Value, string? SourceFile);

internal sealed record MSBuildProjectEvaluation(
    MSBuildPropertyValue NxTags,
    MSBuildPropertyValue NxBuildHosts,
    MSBuildPropertyValue NxBuildableOn,
    MSBuildPropertyValue NxTestHosts,
    MSBuildPropertyValue NxRunHosts,
    MSBuildPropertyValue NxPublishHosts,
    MSBuildPropertyValue NxPackHosts,
    MSBuildPropertyValue NxRestoreHosts,
    string TargetFramework,
    string TargetFrameworkIdentifier,
    string TargetFrameworkVersion,
    string TargetFrameworkProfile,
    string TargetPlatformIdentifier,
    string TargetPlatformVersion,
    string IsTestProject,
    string IsPackable,
    string IsPublishable,
    string PackAsTool,
    string UseMaui,
    string OutputType,
    string PackageId,
    string AssemblyName,
    IReadOnlyList<string> RuntimeIdentifiers,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<MSBuildItemValue> NxTagItems);
