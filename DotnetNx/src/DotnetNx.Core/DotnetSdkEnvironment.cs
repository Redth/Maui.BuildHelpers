using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotnetNx.Core;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);
}

public sealed class DefaultProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }
}

public sealed record DotnetSdkResolverEnvironment(
    string DotnetPath,
    string DotnetRoot,
    string SdkVersion,
    string SdkDirectory,
    string SdkResolversDirectory,
    string SdksDirectory,
    IReadOnlyDictionary<string, string> Variables);

public sealed class DotnetSdkResolver
{
    private readonly IProcessRunner processRunner;
    private readonly Func<string, bool> fileExists;
    private readonly Func<string, bool> directoryExists;

    public DotnetSdkResolver(
        IProcessRunner? processRunner = null,
        Func<string, bool>? fileExists = null,
        Func<string, bool>? directoryExists = null)
    {
        this.processRunner = processRunner ?? new DefaultProcessRunner();
        this.fileExists = fileExists ?? File.Exists;
        this.directoryExists = directoryExists ?? Directory.Exists;
    }

    public DotnetSdkResolverEnvironment Resolve(string workspaceRoot, string? dotnetPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var resolvedDotnetPath = Path.GetFullPath(dotnetPath ?? FindDotnet());
        var dotnetRoot = Path.GetDirectoryName(resolvedDotnetPath)
            ?? throw new InvalidOperationException($"Could not determine the dotnet root for '{resolvedDotnetPath}'.");

        var sdkVersion = RunDotnet(resolvedDotnetPath, ["--version"], workspaceRoot, "determine the selected .NET SDK version")
            .Trim();
        var sdkList = RunDotnet(resolvedDotnetPath, ["--list-sdks"], workspaceRoot, "list installed .NET SDKs");
        var sdkDirectory = GetSdkDirectory(sdkList, sdkVersion);
        var sdkResolversDirectory = Path.Combine(sdkDirectory, "SdkResolvers");
        var sdksDirectory = Path.Combine(sdkDirectory, "Sdks");

        if (!directoryExists(sdkResolversDirectory))
        {
            throw new DirectoryNotFoundException($"Could not find the MSBuild SDK resolvers directory: {sdkResolversDirectory}");
        }

        if (!directoryExists(sdksDirectory))
        {
            throw new DirectoryNotFoundException($"Could not find the MSBuild SDKs directory: {sdksDirectory}");
        }

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = $"{dotnetRoot}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH") ?? string.Empty}",
            ["DOTNET_ROOT"] = dotnetRoot,
            ["DOTNET_ROLL_FORWARD_TO_PRERELEASE"] = "1",
            ["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = dotnetRoot,
            ["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = sdksDirectory,
            ["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER"] = sdkVersion,
            ["MSBUILDADDITIONALSDKRESOLVERSFOLDER"] = sdkResolversDirectory,
            ["MSBuildSDKsPath"] = sdksDirectory,
            ["NX_DAEMON"] = "false",
        };

        return new DotnetSdkResolverEnvironment(
            resolvedDotnetPath,
            dotnetRoot,
            sdkVersion,
            sdkDirectory,
            sdkResolversDirectory,
            sdksDirectory,
            new ReadOnlyDictionary<string, string>(variables));
    }

    public string FindDotnet()
    {
        foreach (var candidate in GetDotnetCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate) && fileExists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not find dotnet. Install the .NET SDK or set DOTNET_ROOT/PATH so dotnet is available.");
    }

    public static string GetSdkDirectory(string sdkListOutput, string sdkVersion)
    {
        foreach (var rawLine in sdkListOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(sdkVersion, StringComparison.Ordinal))
            {
                continue;
            }

            var start = line.IndexOf('[', StringComparison.Ordinal);
            var end = line.LastIndexOf(']');
            if (start < 0 || end <= start)
            {
                continue;
            }

            var sdkRoot = line[(start + 1)..end];
            return Path.Combine(sdkRoot, sdkVersion);
        }

        throw new InvalidOperationException($"Could not find SDK directory for .NET SDK {sdkVersion}.");
    }

    private string RunDotnet(string dotnetPath, string[] arguments, string workspaceRoot, string description)
    {
        var result = processRunner.Run(dotnetPath, arguments, workspaceRoot);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to {description}.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        return result.StandardOutput;
    }

    private IEnumerable<string?> GetDotnetCandidates()
    {
        yield return Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        foreach (var dotnetRootCandidate in GetDotnetRootCandidates())
        {
            yield return Path.Combine(dotnetRootCandidate, GetDotnetFileName());
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".dotnet", GetDotnetFileName());
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", GetDotnetFileName());
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", GetDotnetFileName());
        }
        else
        {
            yield return "/usr/local/share/dotnet/dotnet";
            yield return "/opt/homebrew/share/dotnet/dotnet";
        }

        foreach (var pathEntry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(pathEntry))
            {
                yield return Path.Combine(pathEntry, GetDotnetFileName());
            }
        }
    }

    private static IEnumerable<string> GetDotnetRootCandidates()
    {
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
        var names = new[]
        {
            "DOTNET_ROOT",
            $"DOTNET_ROOT_{architecture}",
            "DOTNET_ROOT_X64",
            "DOTNET_ROOT_X86",
            "DOTNET_ROOT_ARM64",
            "DOTNET_ROOT(x86)",
        };

        return names
            .Select(Environment.GetEnvironmentVariable)
            .Where(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static string GetDotnetFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
}
