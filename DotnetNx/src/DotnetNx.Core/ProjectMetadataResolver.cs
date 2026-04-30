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

    public ProjectHostMetadata ResolveProject(string workspaceRoot, string projectFile)
    {
        var diagnostics = new List<DotnetNxDiagnostic>();
        var targetFrameworks = Array.Empty<string>();
        var buildableOn = new SortedSet<string>(StringComparer.Ordinal);
        string resolution = "inferred";
        string? sourceFile = null;

        try
        {
            MSBuildRegistration.EnsureRegistered();
            targetFrameworks = MSBuildProjectReader.GetTargetFrameworks(projectFile);
            var evaluationFrameworks = targetFrameworks.Length == 0 ? [""] : targetFrameworks;

            foreach (var targetFramework in evaluationFrameworks)
            {
                var buildableOnProperty = MSBuildProjectReader.GetProperty(projectFile, "NxBuildableOn", targetFramework);
                if (!string.IsNullOrWhiteSpace(buildableOnProperty.Value))
                {
                    var invalidValues = HostOperatingSystems.GetInvalidValues(buildableOnProperty.Value);
                    foreach (var invalidValue in invalidValues)
                    {
                        diagnostics.Add(new DotnetNxDiagnostic(
                            DotnetNxDiagnosticSeverity.Error,
                            "DNX001",
                            $"Invalid NxBuildableOn value '{invalidValue}'. Supported values are linux, macos, windows, any, and all.",
                            projectFile));
                    }

                    buildableOn.UnionWith(HostOperatingSystems.Parse(buildableOnProperty.Value));
                    resolution = "explicit";
                    sourceFile ??= buildableOnProperty.SourceFile;
                }
                else if (!string.IsNullOrWhiteSpace(targetFramework))
                {
                    buildableOn.UnionWith(HostOperatingSystems.InferFromTargetFramework(targetFramework));
                }
            }

            if (buildableOn.Count == 0)
            {
                buildableOn.UnionWith(HostOperatingSystems.InferFromTargetFrameworks(targetFrameworks));
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

        var normalized = HostOperatingSystems.Normalize(buildableOn);
        var relativeProjectFile = Path.GetRelativePath(workspaceRoot, projectFile);
        var relativeProjectRoot = Path.GetDirectoryName(relativeProjectFile)?.Replace('\\', '/') ?? ".";

        return new ProjectHostMetadata(
            relativeProjectFile.Replace('\\', '/'),
            relativeProjectRoot,
            Path.GetFileNameWithoutExtension(projectFile),
            normalized,
            HostOperatingSystems.ToTags(normalized.ToArray()),
            resolution,
            sourceFile is null ? null : Path.GetRelativePath(workspaceRoot, sourceFile).Replace('\\', '/'),
            targetFrameworks,
            diagnostics);
    }

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
