using DotnetNx.Core;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DotnetNx.MSBuild;

public sealed class WriteNxProjectMetadata : Microsoft.Build.Utilities.Task
{
    [Required]
    public string WorkspaceRoot { get; set; } = string.Empty;

    [Required]
    public string OutputFile { get; set; } = string.Empty;

    public ITaskItem[] ProjectFiles { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            var resolver = new ProjectMetadataResolver();
            var projectFiles = ProjectFiles.Length == 0
                ? null
                : ProjectFiles.Select(item => item.GetMetadata("FullPath")).Where(path => !string.IsNullOrWhiteSpace(path));
            var metadata = resolver.ResolveWorkspace(WorkspaceRoot, projectFiles);

            var outputDirectory = Path.GetDirectoryName(OutputFile);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(OutputFile, JsonServices.Serialize(metadata));

            foreach (var diagnostic in metadata.Projects.SelectMany(project => project.Diagnostics).Concat(metadata.Diagnostics))
            {
                LogDiagnostic(diagnostic);
            }

            return !Log.HasLoggedErrors;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: false);
            return false;
        }
    }

    private void LogDiagnostic(DotnetNxDiagnostic diagnostic)
    {
        switch (diagnostic.Severity)
        {
            case DotnetNxDiagnosticSeverity.Error:
                Log.LogError(
                    subcategory: null,
                    errorCode: diagnostic.Code,
                    helpKeyword: null,
                    file: diagnostic.File,
                    lineNumber: 0,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: diagnostic.Message);
                break;
            case DotnetNxDiagnosticSeverity.Warning:
                Log.LogWarning(
                    subcategory: null,
                    warningCode: diagnostic.Code,
                    helpKeyword: null,
                    file: diagnostic.File,
                    lineNumber: 0,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: diagnostic.Message);
                break;
            default:
                Log.LogMessage(MessageImportance.Low, $"{diagnostic.Code}: {diagnostic.Message}");
                break;
        }
    }
}
