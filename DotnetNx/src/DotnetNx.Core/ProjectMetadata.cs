namespace DotnetNx.Core;

public enum DotnetMetadataSource
{
    Explicit,
    Inferred,
}

public sealed record DotnetProjectCapabilities(
    bool IsTest,
    bool IsExecutable,
    bool IsPackable,
    bool IsPublishable,
    bool IsTool,
    IReadOnlyList<string> PackageIds);

public sealed record DotnetTargetFrameworkMetadata(
    string ShortName,
    string Framework,
    string FrameworkVersion,
    string? Profile,
    string? Platform,
    string? PlatformVersion);

public sealed record DotnetTargetHostRequirement(
    string Target,
    IReadOnlyList<string> Hosts,
    DotnetMetadataSource Source,
    string? SourceFile,
    string Rationale);

public sealed record DotnetProjectConfigurationMetadata(
    string? TargetFramework,
    DotnetTargetFrameworkMetadata? Framework,
    IReadOnlyList<string> RuntimeIdentifiers,
    DotnetProjectCapabilities Capabilities,
    IReadOnlyList<string> ExplicitTags,
    IReadOnlyList<DotnetTargetHostRequirement> TargetHostRequirements);

public sealed record DotnetProjectMetadata(
    string ProjectFile,
    string ProjectRoot,
    string ProjectName,
    string ProjectType,
    IReadOnlyList<string> Technologies,
    DotnetProjectCapabilities Capabilities,
    IReadOnlyList<DotnetProjectConfigurationMetadata> Configurations,
    IReadOnlyList<DotnetTargetHostRequirement> TargetHostRequirements,
    IReadOnlyList<string> ExplicitTags,
    IReadOnlyList<string> Tags,
    IReadOnlyList<DotnetNxDiagnostic> Diagnostics);

public sealed record WorkspaceProjectMetadata(
    string WorkspaceRoot,
    IReadOnlyList<DotnetProjectMetadata> Projects,
    IReadOnlyList<DotnetNxDiagnostic> Diagnostics);
