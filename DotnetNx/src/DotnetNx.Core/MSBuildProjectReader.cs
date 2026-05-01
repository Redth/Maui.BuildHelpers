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
            GetProperty(project, "NxBuildableOn"),
            GetProperty(project, "NxTags"),
            project.GetPropertyValue("IsTestProject"),
            project.GetPropertyValue("IsPackable"),
            project.GetPropertyValue("PackAsTool"),
            project.GetPropertyValue("UseMaui"),
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
    MSBuildPropertyValue NxBuildableOn,
    MSBuildPropertyValue NxTags,
    string IsTestProject,
    string IsPackable,
    string PackAsTool,
    string UseMaui,
    IReadOnlyList<MSBuildItemValue> NxTagItems);
