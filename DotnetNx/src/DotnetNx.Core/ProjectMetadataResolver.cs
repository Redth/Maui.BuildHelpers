namespace DotnetNx.Core;

public sealed class ProjectMetadataResolver
{
    private static readonly string[] ProjectGlobs = ["*.csproj", "*.fsproj", "*.vbproj"];
    private static readonly string[] ExcludedDirectoryNames = [".git", ".nx", "bin", "obj", "node_modules"];

    public WorkspaceProjectMetadata ResolveWorkspace(string workspaceRoot, IEnumerable<string>? projectFiles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        ApplyResolverEnvironment(fullWorkspaceRoot);

        var files = (projectFiles ?? DiscoverProjectFiles(fullWorkspaceRoot))
            .Select(path => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(fullWorkspaceRoot, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projects = files
            .Select(file => ResolveProject(fullWorkspaceRoot, file))
            .ToArray();
        var diagnostics = projects
            .SelectMany(project => project.Diagnostics)
            .Where(diagnostic => diagnostic.Severity == DotnetNxDiagnosticSeverity.Error)
            .ToArray();

        return new WorkspaceProjectMetadata(fullWorkspaceRoot, projects, diagnostics);
    }

    private static void ApplyResolverEnvironment(string workspaceRoot)
    {
        var resolverEnvironment = new DotnetSdkResolver().Resolve(workspaceRoot);
        foreach (var (key, value) in resolverEnvironment.Variables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public DotnetProjectMetadata ResolveProject(string workspaceRoot, string projectFile)
    {
        var diagnostics = new List<DotnetNxDiagnostic>();
        var targetFrameworks = Array.Empty<string>();
        var configurations = new List<DotnetProjectConfigurationMetadata>();
        var usesMaui = false;
        var reportedLegacyBuildableOn = false;

        try
        {
            MSBuildRegistration.EnsureRegistered();
            targetFrameworks = MSBuildProjectReader.GetTargetFrameworks(projectFile);
            var evaluationFrameworks = targetFrameworks.Length == 0 ? [""] : targetFrameworks;

            foreach (var targetFramework in evaluationFrameworks)
            {
                var evaluation = MSBuildProjectReader.Evaluate(projectFile, targetFramework);
                usesMaui |= IsTrue(evaluation.UseMaui);
                var explicitTags = ProjectTags.Normalize(
                    [evaluation.NxTags.Value, .. evaluation.NxTagItems.Select(item => item.Value)]);
                var configurationCapabilities = CreateCapabilities(evaluation);
                var hostRequirements = ResolveHostRequirements(
                    workspaceRoot,
                    projectFile,
                    evaluation,
                    diagnostics,
                    ref reportedLegacyBuildableOn);

                configurations.Add(new DotnetProjectConfigurationMetadata(
                    string.IsNullOrWhiteSpace(evaluation.TargetFramework) ? null : evaluation.TargetFramework,
                    TargetFrameworkPartsParser.ToMetadata(evaluation),
                    evaluation.RuntimeIdentifiers
                        .Select(TargetFrameworkPartsParser.NormalizeTagValue)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    configurationCapabilities,
                    explicitTags,
                    hostRequirements));
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new DotnetNxDiagnostic(
                DotnetNxDiagnosticSeverity.Error,
                "DNX000",
                $"Failed to evaluate '{projectFile}': {ex.Message}",
                projectFile));
        }

        var capabilities = AggregateCapabilities(configurations);
        var explicitProjectTags = IntersectTags(configurations.Select(configuration => configuration.ExplicitTags));
        var targetHostRequirements = AggregateHostRequirements(projectFile, configurations, diagnostics);
        var relativeProjectFile = Path.GetRelativePath(workspaceRoot, projectFile);
        var relativeProjectRoot = Path.GetDirectoryName(relativeProjectFile)?.Replace('\\', '/') ?? ".";
        var projectType = capabilities.IsExecutable ? "application" : "library";
        var technologies = GetTechnologies(projectFile, usesMaui);

        return new DotnetProjectMetadata(
            relativeProjectFile.Replace('\\', '/'),
            relativeProjectRoot,
            Path.GetFileNameWithoutExtension(projectFile),
            projectType,
            technologies,
            capabilities,
            configurations,
            targetHostRequirements,
            explicitProjectTags,
            explicitProjectTags,
            diagnostics);
    }

    private static DotnetProjectCapabilities CreateCapabilities(MSBuildProjectEvaluation evaluation)
    {
        var isTest = IsTrue(evaluation.IsTestProject) ||
            evaluation.PackageReferences.Any(package =>
                string.Equals(package, "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) ||
                package.StartsWith("Microsoft.Testing", StringComparison.OrdinalIgnoreCase));
        var isPackable = IsTrue(evaluation.IsPackable);
        var packageId = TargetFrameworkPartsParser.NormalizeOptionalTagValue(evaluation.PackageId) ??
            (isPackable
                ? TargetFrameworkPartsParser.NormalizeOptionalTagValue(evaluation.AssemblyName)
                : null);

        return new DotnetProjectCapabilities(
            isTest,
            evaluation.OutputType is "Exe" or "WinExe",
            isPackable,
            IsTrue(evaluation.IsPublishable),
            IsTrue(evaluation.PackAsTool),
            packageId is null ? [] : [packageId]);
    }

    private static IReadOnlyList<DotnetTargetHostRequirement> ResolveHostRequirements(
        string workspaceRoot,
        string projectFile,
        MSBuildProjectEvaluation evaluation,
        ICollection<DotnetNxDiagnostic> diagnostics,
        ref bool reportedLegacyBuildableOn)
    {
        var requirements = new List<DotnetTargetHostRequirement>();
        var explicitProperties = new[]
        {
            ("build", evaluation.NxBuildHosts),
            ("test", evaluation.NxTestHosts),
            ("run", evaluation.NxRunHosts),
            ("publish", evaluation.NxPublishHosts),
            ("pack", evaluation.NxPackHosts),
            ("restore", evaluation.NxRestoreHosts),
        };

        foreach (var (target, property) in explicitProperties)
        {
            AddExplicitHostRequirement(
                workspaceRoot,
                projectFile,
                target,
                property,
                diagnostics,
                requirements);
        }

        var hasExplicitBuildHosts = requirements.Any(requirement => requirement.Target == "build");
        if (!hasExplicitBuildHosts && !string.IsNullOrWhiteSpace(evaluation.NxBuildableOn.Value))
        {
            AddExplicitHostRequirement(
                workspaceRoot,
                projectFile,
                "build",
                evaluation.NxBuildableOn,
                diagnostics,
                requirements);
            if (!reportedLegacyBuildableOn)
            {
                diagnostics.Add(new DotnetNxDiagnostic(
                    DotnetNxDiagnosticSeverity.Warning,
                    "DNX002",
                    "NxBuildableOn is deprecated. Use NxBuildHosts for an explicit build-target host requirement.",
                    evaluation.NxBuildableOn.SourceFile ?? projectFile));
                reportedLegacyBuildableOn = true;
            }
        }

        if (!requirements.Any(requirement => requirement.Target == "build"))
        {
            var inferredHosts = HostOperatingSystems.InferBuildHosts(
                evaluation.TargetPlatformIdentifier,
                evaluation.RuntimeIdentifiers);
            if (inferredHosts.Count > 0)
            {
                requirements.Add(new DotnetTargetHostRequirement(
                    "build",
                    inferredHosts,
                    DotnetMetadataSource.Inferred,
                    null,
                    "Advisory build-host compatibility inferred from the evaluated target platform and runtime identifiers; workload availability is not verified."));
            }
        }

        return requirements
            .OrderBy(requirement => requirement.Target, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddExplicitHostRequirement(
        string workspaceRoot,
        string projectFile,
        string target,
        MSBuildPropertyValue property,
        ICollection<DotnetNxDiagnostic> diagnostics,
        ICollection<DotnetTargetHostRequirement> requirements)
    {
        if (string.IsNullOrWhiteSpace(property.Value))
        {
            return;
        }

        var invalidValues = HostOperatingSystems.GetInvalidValues(property.Value);
        foreach (var invalidValue in invalidValues)
        {
            diagnostics.Add(new DotnetNxDiagnostic(
                DotnetNxDiagnosticSeverity.Error,
                "DNX001",
                $"Invalid host '{invalidValue}' for the '{target}' target. Supported values are linux, macos, windows, any, and all.",
                property.SourceFile ?? projectFile));
        }

        var hosts = HostOperatingSystems.Parse(property.Value);
        if (hosts.Count == 0)
        {
            return;
        }

        requirements.Add(new DotnetTargetHostRequirement(
            target,
            hosts,
            DotnetMetadataSource.Explicit,
            ToWorkspaceRelativePath(workspaceRoot, property.SourceFile),
            $"Explicit host requirement from MSBuild property {GetHostPropertyName(target)}."));
    }

    private static IReadOnlyList<DotnetTargetHostRequirement> AggregateHostRequirements(
        string projectFile,
        IReadOnlyList<DotnetProjectConfigurationMetadata> configurations,
        ICollection<DotnetNxDiagnostic> diagnostics)
    {
        if (configurations.Count == 0)
        {
            return [];
        }

        var targets = configurations
            .SelectMany(configuration => configuration.TargetHostRequirements.Select(requirement => requirement.Target))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal);
        var aggregate = new List<DotnetTargetHostRequirement>();

        foreach (var target in targets)
        {
            var requirements = configurations
                .Select(configuration => configuration.TargetHostRequirements.SingleOrDefault(
                    requirement => requirement.Target == target))
                .ToArray();
            if (requirements.Any(requirement => requirement is null))
            {
                continue;
            }

            var presentRequirements = requirements.Cast<DotnetTargetHostRequirement>().ToArray();
            var hosts = HostOperatingSystems.Intersect(presentRequirements.Select(requirement => requirement.Hosts));
            if (hosts.Count == 0)
            {
                diagnostics.Add(new DotnetNxDiagnostic(
                    DotnetNxDiagnosticSeverity.Warning,
                    "DNX003",
                    $"The '{target}' target has no host shared by every evaluated project configuration. Use framework-specific target configurations instead of project-level host routing.",
                    projectFile));
                continue;
            }

            var source = presentRequirements.All(requirement => requirement.Source == DotnetMetadataSource.Explicit)
                ? DotnetMetadataSource.Explicit
                : DotnetMetadataSource.Inferred;
            var sourceFiles = presentRequirements
                .Select(requirement => requirement.SourceFile)
                .Where(sourceFile => sourceFile is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            aggregate.Add(new DotnetTargetHostRequirement(
                target,
                hosts,
                source,
                sourceFiles.Length == 1 ? sourceFiles[0] : null,
                $"Hosts compatible with every evaluated configuration of the '{target}' target."));
        }

        return aggregate;
    }

    private static DotnetProjectCapabilities AggregateCapabilities(
        IReadOnlyList<DotnetProjectConfigurationMetadata> configurations)
    {
        var capabilities = configurations.Select(configuration => configuration.Capabilities).ToArray();
        return new DotnetProjectCapabilities(
            capabilities.Any(capability => capability.IsTest),
            capabilities.Any(capability => capability.IsExecutable),
            capabilities.Any(capability => capability.IsPackable),
            capabilities.Any(capability => capability.IsPublishable),
            capabilities.Any(capability => capability.IsTool),
            capabilities
                .SelectMany(capability => capability.PackageIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(packageId => packageId, StringComparer.Ordinal)
                .ToArray());
    }

    private static IReadOnlyList<string> IntersectTags(IEnumerable<IReadOnlyList<string>> tagSets)
    {
        HashSet<string>? intersection = null;
        foreach (var tagSet in tagSets)
        {
            if (intersection is null)
            {
                intersection = new HashSet<string>(tagSet, StringComparer.Ordinal);
            }
            else
            {
                intersection.IntersectWith(tagSet);
            }
        }

        return intersection?.OrderBy(tag => tag, StringComparer.Ordinal).ToArray() ?? [];
    }

    private static IReadOnlyList<string> GetTechnologies(string projectFile, bool usesMaui)
    {
        var technologies = new List<string> { "dotnet" };
        technologies.Add(Path.GetExtension(projectFile).ToLowerInvariant() switch
        {
            ".fsproj" => "F#",
            ".vbproj" => "VB",
            _ => "C#",
        });
        if (usesMaui)
        {
            technologies.Add("maui");
        }

        return technologies;
    }

    private static string GetHostPropertyName(string target) =>
        target switch
        {
            "build" => "NxBuildHosts",
            "test" => "NxTestHosts",
            "run" => "NxRunHosts",
            "publish" => "NxPublishHosts",
            "pack" => "NxPackHosts",
            "restore" => "NxRestoreHosts",
            _ => $"Nx{target}Hosts",
        };

    private static string? ToWorkspaceRelativePath(string workspaceRoot, string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/');

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.Ordinal);

    public static IReadOnlyList<string> DiscoverProjectFiles(string workspaceRoot)
    {
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Workspace root does not exist: {root}");
        }

        return EnumerateProjectFiles(root)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateProjectFiles(string directory)
    {
        foreach (var glob in ProjectGlobs)
        {
            foreach (var project in Directory.EnumerateFiles(directory, glob, SearchOption.TopDirectoryOnly))
            {
                yield return project;
            }
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
        {
            var directoryName = Path.GetFileName(childDirectory);
            if (ExcludedDirectoryNames.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var project in EnumerateProjectFiles(childDirectory))
            {
                yield return project;
            }
        }
    }

}
