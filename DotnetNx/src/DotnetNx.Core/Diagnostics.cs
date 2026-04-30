namespace DotnetNx.Core;

public enum DotnetNxDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record DotnetNxDiagnostic(
    DotnetNxDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? File = null);
