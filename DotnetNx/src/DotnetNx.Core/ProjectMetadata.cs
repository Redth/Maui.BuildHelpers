namespace DotnetNx.Core;

public sealed record ProjectHostMetadata(
    string ProjectFile,
    string ProjectRoot,
    string ProjectName,
    IReadOnlyList<string> BuildableOn,
    IReadOnlyList<string> ExplicitTags,
    IReadOnlyList<string> InferredTags,
    IReadOnlyList<string> Tags,
    string Resolution,
    string? SourceFile,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<DotnetNxDiagnostic> Diagnostics);

public sealed record WorkspaceProjectMetadata(
    string WorkspaceRoot,
    IReadOnlyList<ProjectHostMetadata> Projects,
    IReadOnlyList<DotnetNxDiagnostic> Diagnostics);
